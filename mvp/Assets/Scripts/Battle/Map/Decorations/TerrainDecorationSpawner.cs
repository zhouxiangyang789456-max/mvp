using System.Collections.Generic;
using Mvp.Battle.Map.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mvp.Battle.Map.Decorations
{
    /// <summary>
    /// Map-level, deterministic visual decoration pass. Decorations never participate
    /// in occupancy, pathfinding or player input.
    /// </summary>
    public sealed class TerrainDecorationSpawner : MonoBehaviour
    {
        const string ProfileResourcePath =
            "Battle/TerrainDecorations/DefaultTerrainDecorationProfile";

        readonly List<GameObject> _spawned = new List<GameObject>();
        Transform _root;

        public int SpawnedCount { get { return _spawned.Count; } }

        public void Build(BattleGridController grid, GeneratedMapData generated)
        {
            Clear();
            var profile = Resources.Load<TerrainDecorationProfile>(ProfileResourcePath);
            if (profile == null)
            {
                Debug.LogWarning("[TerrainDecorations] Profile not found; keeping full 2D terrain fallback.");
                return;
            }
            if (!profile.Enabled || profile.GlobalDensity <= 0f) return;

            _root = new GameObject("TerrainDecorationRoot").transform;
            _root.SetParent(grid.transform, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;

            var excluded = BuildExclusionSet(generated, profile);
            uint mapSeed = generated != null ? generated.Seed : 0x25D5EEDu;
            int decoratedCells = 0;

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (excluded.Contains(cell)) continue;
                if (grid.HasEmbeddedTerrainDecorations(cell)) continue;
                var terrain = grid.GetTerrain(cell);
                var rule = profile.FindRule(terrain);
                if (rule == null || !rule.IsUsable) continue;

                var rng = new StableRandom(Hash(mapSeed, x, y, (int)terrain,
                    profile.DecorationVersion));
                if (rng.Next01() > rule.SpawnChance * profile.GlobalDensity) continue;

                int min = Mathf.Max(0, rule.MinCount);
                int max = Mathf.Max(min, rule.MaxCount);
                int count = rng.RangeInclusive(min, max);
                int before = _spawned.Count;
                for (int i = 0; i < count; i++)
                    SpawnOne(grid, cell, rule, ref rng, i);

                if (_spawned.Count > before)
                {
                    decoratedCells++;
                    if (rule.UseDecorationBase) grid.SetDecorationBase(cell, true);
                }
            }

            Debug.Log("[TerrainDecorations] seed=" + mapSeed + " cells=" + decoratedCells +
                " objects=" + _spawned.Count + " version=" + profile.DecorationVersion);
        }

        void SpawnOne(BattleGridController grid, Vector2Int cell,
            TerrainDecorationRule rule, ref StableRandom rng, int index)
        {
            var prefab = rule.Prefabs[rng.RangeInclusive(0, rule.Prefabs.Length - 1)];
            if (prefab == null) return;

            var instance = Instantiate(prefab, _root, false);
            instance.name = terrainName(rule.Terrain) + "_" + cell.x + "_" + cell.y + "_" + index;
            float jitter = Mathf.Clamp(rule.PositionJitter, 0f, 0.4f);
            var world = grid.GridToWorld(cell);
            world.x += rng.Range(-jitter, jitter);
            world.z += rng.Range(-jitter, jitter);
            world.y = TerrainCatalog.GetElevation(rule.Terrain) + rule.VerticalOffset;
            instance.transform.position = world;
            float yaw = rule.Terrain == TerrainType.Bridge
                ? ResolveBridgeYaw(grid, cell)
                : (rule.RandomYaw ? rng.Range(0f, 360f) : 0f);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            SetLayerRecursively(instance.transform, 2); // built-in Ignore Raycast
            DisablePhysics(instance);
            NormalizeAndGround(instance, rule.TargetHeight, rule.MaxFootprint, world.y);
            ConfigureRenderers(instance, rule);
            _spawned.Add(instance);
        }

        static float ResolveBridgeYaw(BattleGridController grid, Vector2Int cell)
        {
            bool horizontal = IsRoadLike(grid, cell + Vector2Int.left) ||
                IsRoadLike(grid, cell + Vector2Int.right);
            bool vertical = IsRoadLike(grid, cell + Vector2Int.down) ||
                IsRoadLike(grid, cell + Vector2Int.up);
            return horizontal && !vertical ? 90f : 0f;
        }

        static bool IsRoadLike(BattleGridController grid, Vector2Int cell)
        {
            if (!grid.InBounds(cell)) return false;
            var terrain = grid.GetTerrain(cell);
            return terrain == TerrainType.Road || terrain == TerrainType.Bridge;
        }

        static string terrainName(TerrainType terrain)
        {
            return terrain.ToString() + "Decoration";
        }

        static void NormalizeAndGround(GameObject instance, float targetHeight,
            float maxFootprint, float groundY)
        {
            if (!TryGetBounds(instance, out var bounds)) return;
            float currentHeight = Mathf.Max(0.001f, bounds.size.y);
            float currentFootprint = Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.z));
            float factor = Mathf.Min(
                Mathf.Max(0.05f, targetHeight) / currentHeight,
                Mathf.Max(0.05f, maxFootprint) / currentFootprint);
            factor = Mathf.Clamp(factor, 0.01f, 10f);
            instance.transform.localScale *= factor;
            if (TryGetBounds(instance, out bounds))
                instance.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = default(Bounds);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderers[i].bounds; found = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return found;
        }

        static void ConfigureRenderers(GameObject instance, TerrainDecorationRule rule)
        {
            var block = new MaterialPropertyBlock();
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                renderer.shadowCastingMode = rule.CastShadows
                    ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                if (rule.Tint != Color.white)
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_Color", rule.Tint);
                    block.SetColor("_BaseColor", rule.Tint);
                    renderer.SetPropertyBlock(block);
                    block.Clear();
                }
            }
        }

        static void DisablePhysics(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        static HashSet<Vector2Int> BuildExclusionSet(GeneratedMapData data,
            TerrainDecorationProfile profile)
        {
            var result = new HashSet<Vector2Int>();
            if (data == null) return result;

            for (int i = 0; i < data.PlayerDeploymentCells.Count; i++)
                AddArea(result, new Vector2Int(data.PlayerDeploymentCells[i].X,
                    data.PlayerDeploymentCells[i].Y), 1, 1, profile.DeploymentClearance);
            for (int i = 0; i < data.EnemyDeploymentCells.Count; i++)
                AddArea(result, new Vector2Int(data.EnemyDeploymentCells[i].X,
                    data.EnemyDeploymentCells[i].Y), 1, 1, profile.DeploymentClearance);
            for (int i = 0; i < data.BuildingSpawnData.Count; i++)
                AddArea(result, data.BuildingSpawnData[i].AnchorCell, 1, 1,
                    profile.BuildingClearance);
            if (data.Portal != null)
                AddArea(result, data.Portal.AnchorCell, data.Portal.Width,
                    data.Portal.Height, profile.PortalClearance);
            return result;
        }

        static void AddArea(HashSet<Vector2Int> result, Vector2Int anchor,
            int width, int height, int clearance)
        {
            int margin = Mathf.Max(0, clearance);
            for (int y = -margin; y < height + margin; y++)
            for (int x = -margin; x < width + margin; x++)
                result.Add(anchor + new Vector2Int(x, y));
        }

        public void Clear()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
        }

        void OnDestroy()
        {
            _spawned.Clear();
            _root = null;
        }

        static uint Hash(uint seed, int x, int y, int terrain, int version)
        {
            uint hash = seed ^ 2166136261u;
            hash = Mix(hash, unchecked((uint)x));
            hash = Mix(hash, unchecked((uint)y));
            hash = Mix(hash, unchecked((uint)terrain));
            hash = Mix(hash, unchecked((uint)version));
            return hash == 0u ? 0x9E3779B9u : hash;
        }

        static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        struct StableRandom
        {
            uint _state;
            public StableRandom(uint seed) { _state = seed == 0u ? 1u : seed; }
            uint Next()
            {
                uint value = _state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                _state = value;
                return value;
            }
            public float Next01() { return (Next() & 0x00FFFFFFu) / 16777216f; }
            public float Range(float min, float max) { return Mathf.Lerp(min, max, Next01()); }
            public int RangeInclusive(int min, int max)
            {
                if (max <= min) return min;
                return min + (int)(Next() % (uint)(max - min + 1));
            }
        }
    }
}
