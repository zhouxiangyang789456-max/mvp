using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle;
using Mvp.Battle.Map.Generation;
using Mvp.Battle.Map.Decorations;
using Mvp.Shared;

namespace Mvp.Battle.Map
{
    /// <summary>Where the battle grid takes its terrain from.</summary>
    public enum BattleMapSource
    {
        TestMap,
        Procedural,
        HandAuthored
    }

    /// <summary>
    /// Owns the logical grid and its visual: one flat quad per cell, no colliders
    /// (perf rule). Provides the dual coordinate system (logical Vector2Int <-> world Vector3)
    /// and the occupancy/walkability queries used by pathfinding and movement.
    ///
    /// World convention: cell (x, z) sits at world (x, 0, z). The iso look comes from the
    /// fixed oblique orthographic camera, not from skewing the grid.
    /// </summary>
    public sealed class BattleGridController : MonoBehaviour, IGridDataProvider
    {
        public static BattleGridController Instance { get; private set; }

        [Header("Grid")]
        [SerializeField] int _width = TestBattleMapData.Width;
        [SerializeField] int _height = TestBattleMapData.Height;

        [Header("Map Source")]
        [Tooltip("TestMap always uses the hand-authored TestBattleMapData. Procedural runs the " +
                 "generator, honoring BattleMapContext.PendingRequest when the level-select scene " +
                 "queued one, otherwise building a request from the serialized settings below.")]
        [SerializeField] BattleMapSource _mapSource = BattleMapSource.TestMap;
        [SerializeField] MapGenerationSettings _proceduralSettings = new MapGenerationSettings();
        [SerializeField] int _proceduralLevel = 1;
        [Tooltip("When enabled by Random Map Tool, use the settings saved in this scene instead of a map profile supplied by the commander-select scene.")]
        [SerializeField] bool _useAppliedToolSettings;
        [Tooltip("Fallback level->map rule profile used when the level-select scene did not supply one via BattleStartContext.")]
        [SerializeField] LevelMapGenerationProfile _proceduralProfile;
        [SerializeField] HandAuthoredMapData _handMapOverride;
        [Header("3D terrain")]
        [SerializeField] bool _enable3DTerrain = true;
        [SerializeField] TerrainPrefabCatalog _terrainPrefabCatalog;

        TerrainType[,] _terrain;
        float[,] _surfaceHeights;
        bool[,] _rampCells;
        readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();
        // Buildings mark their footprint cells blocked + occupied so pathfinding,
        // movement, spawning and slot validation automatically avoid them (阶段B).
        readonly HashSet<Vector2Int> _blocked = new HashSet<Vector2Int>();
        readonly List<BattleCellView> _cells = new List<BattleCellView>();

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _terrain = ResolveMap();
            BuildVisual();
        }

        TerrainType[,] ResolveMap()
        {
            var effectiveSource = BattleStartContext.MapSourceOverride.HasValue
                ? BattleStartContext.MapSourceOverride.Value : _mapSource;
            if (effectiveSource == BattleMapSource.TestMap)
            {
                BattleMapContext.LastGeneratedData = null;
                BattleMapContext.LastIdentity = null;
                BattleMapContext.LastHandMapData = null;
                return TestBattleMapData.Create();
            }

            // 1) Explicit request queued by the level-select scene wins (e.g. a specific
            //    reproduce request). One-shot so a stale request never leaks into a later battle.
            if (BattleMapContext.PendingRequest != null)
            {
                var pending = BattleMapContext.PendingRequest;
                BattleMapContext.PendingRequest = null;
                return GenerateAndStore(pending);
            }

            // 2) A map explicitly activated by HandMapBuilder must beat the Random Map Tool's
            // serialized scene settings. Both tools can leave data in BattleScene, but the
            // active hand-map config expresses the designer's most recent map choice.
            var runtimeConfig = HandMapRuntimeConfig.Load();
            var profile = BattleStartContext.MapProfile != null
                ? BattleStartContext.MapProfile
                : (_proceduralProfile != null ? _proceduralProfile : runtimeConfig != null ? runtimeConfig.ActiveProfile : null);
            var configuredHandMap = profile != null && profile.HandMapOverride != null
                ? profile.HandMapOverride
                : runtimeConfig != null ? runtimeConfig.ActiveMap : null;
            int level = BattleStartContext.LevelIndex > 0 ? BattleStartContext.LevelIndex : _proceduralLevel;
            if (effectiveSource == BattleMapSource.HandAuthored ||
                (effectiveSource == BattleMapSource.Procedural && configuredHandMap != null))
            {
                var handMap = effectiveSource == BattleMapSource.HandAuthored && _handMapOverride != null ? _handMapOverride
                    : configuredHandMap;
                if (handMap != null)
                {
                    BattleMapContext.LastGeneratedData = null;
                    BattleMapContext.LastIdentity = null;
                    BattleMapContext.LastHandMapData = handMap;
                    _width = Mathf.Max(1, handMap.Width);
                    _height = Mathf.Max(1, handMap.Height);
                    var terrain = HandMapBattleMapProvider.CreateBattleMap(handMap, _blocked);
                    BuildHandMapSurfaceData(handMap);
                    return terrain;
                }
                Debug.LogError("[BattleGridController] HandAuthored selected without a HandMap; falling back to Procedural.");
            }

            // Random Map Tool's scene override applies only when no authored map was selected.
            if (_useAppliedToolSettings)
                return GenerateAndStore(BuildDefaultRequest());

            // 3) Profile-driven procedural map.
            BattleMapContext.LastHandMapData = null;
            if (profile != null)
                return GenerateAndStore(profile.BuildRequest(level));

            // 4) Serialized inline settings fallback (direct scene open / editor preview).
            return GenerateAndStore(BuildDefaultRequest());
        }

        TerrainType[,] GenerateAndStore(BattleMapRequest request)
        {
            int rosterCount = BattleStartContext.ExpeditionRoster != null
                ? BattleStartContext.ExpeditionRoster.Commanders.Count
                : 0;
            request.PlayerDeploymentGroupCount = Mathf.Max(1, rosterCount);
            var battle = ProceduralBattleMapProvider.CreateBattleMap(request,
                out var data, out var identity);
            BattleMapContext.LastGeneratedData = data;
            BattleMapContext.LastIdentity = identity;

            // The generated grid may differ from the serialized _width/_height (settings Win/H).
            _width = battle.GetLength(1);
            _height = battle.GetLength(0);
            Debug.Log("[BattleGridController] map identity=" + identity);
            return battle;
        }

        BattleMapRequest BuildDefaultRequest()
        {
            return new BattleMapRequest
            {
                ProfileId = "default",
                ProfileVersion = 1,
                RuleId = "default",
                LevelIndex = _proceduralLevel,
                SeedMode = SeedMode.Fixed,
                FixedSeed = _proceduralSettings != null ? _proceduralSettings.Seed : 20260818u,
                Settings = _proceduralSettings
            };
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildVisual()
        {
            var catalog = _enable3DTerrain ? (_terrainPrefabCatalog != null
                ? _terrainPrefabCatalog : Resources.Load<TerrainPrefabCatalog>(TerrainPrefabCatalog.ResourcePath)) : null;
            uint visualSeed = BattleMapContext.LastGeneratedData != null
                ? BattleMapContext.LastGeneratedData.Seed : 0x25D5EEDu;
            int prefabCells = 0;
            var rootGo = new GameObject("GridVisual");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = Vector3.zero;

            // The dark oversized base belongs to the legacy/procedural grid. A hand-authored
            // 3D map already supplies its own edge and side geometry; keeping this quad would
            // expose the extra +1-cell margin as a black frame around the authored terrain.
            if (BattleMapContext.LastHandMapData == null)
            {
                var baseGo = new GameObject("BaseGround");
                baseGo.transform.SetParent(rootGo.transform, false);
                baseGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                baseGo.transform.localScale = new Vector3(_width + 2f, _height + 2f, 1f);
                baseGo.transform.localPosition = new Vector3(
                    (_width - 1) * 0.5f, -0.03f, (_height - 1) * 0.5f);
                var baseSr = baseGo.AddComponent<SpriteRenderer>();
                baseSr.sprite = SharedSprites.White;
                baseSr.color = new Color(0.08f, 0.10f, 0.13f);
                // Always behind the terrain tiles, otherwise the large quad's distance-sorted
                // draw order would cover the rear tiles.
                baseSr.sortingOrder = -10;
            }

            _cells.Clear();
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var grid = new Vector2Int(x, z);
                    var go = new GameObject("Cell_" + x + "_" + z);
                    go.transform.SetParent(rootGo.transform, false);
                    go.transform.position = GridToWorld(grid);
                    var view = go.AddComponent<BattleCellView>();
                    var handMap = BattleMapContext.LastHandMapData;
                    if (handMap != null && handMap.HasGroundVisual(x, z))
                        view.SetupLogicalOnly(grid, _terrain[z, x]);
                    else
                        view.Setup(grid, _terrain[z, x], catalog, visualSeed, BuildRoadMask(x, z));
                    if (view.UsesPrefab) prefabCells++;
                    _cells.Add(view);
                }
            }

            for (int z = 0; z < _height; z++)
            for (int x = 0; x < _width; x++)
            {
                var terrain = _terrain[z, x];
                if (terrain != TerrainType.Road && terrain != TerrainType.Bridge) continue;
                _cells[z * _width + x].ApplyConnections(BuildRoadMask(x, z));
            }

            if (BattleMapContext.LastHandMapData != null)
            {
                WarnBridgeOverNonWater(BattleMapContext.LastHandMapData);
                HandMapVisualRenderer.Render(BattleMapContext.LastHandMapData, rootGo.transform);
            }
            else
            {
                var decorationSpawner = gameObject.GetComponent<TerrainDecorationSpawner>();
                if (decorationSpawner == null)
                    decorationSpawner = gameObject.AddComponent<TerrainDecorationSpawner>();
                decorationSpawner.Build(this, BattleMapContext.LastGeneratedData);
            }
            Debug.Log("[Terrain3D] prefabCells=" + prefabCells + " fallbackCells=" + (_cells.Count - prefabCells));
        }

        public bool HasEmbeddedTerrainDecorations(Vector2Int cell)
        {
            int index = cell.y * _width + cell.x;
            return InBounds(cell) && index < _cells.Count && _cells[index].IncludesDecorations;
        }

        public void SetDecorationBase(Vector2Int cell, bool active)
        {
            if (!InBounds(cell)) return;
            int index = cell.y * _width + cell.x;
            if (index >= 0 && index < _cells.Count)
                _cells[index].SetDecorationBase(active);
        }

        int BuildRoadMask(int x, int z)
        {
            int mask = 0;
            if (ConnectsToRoad(x, z - 1)) mask |= 1;
            if (ConnectsToRoad(x + 1, z)) mask |= 2;
            if (ConnectsToRoad(x, z + 1)) mask |= 4;
            if (ConnectsToRoad(x - 1, z)) mask |= 8;
            return mask;
        }

        bool ConnectsToRoad(int x, int z)
        {
            if (x < 0 || x >= _width || z < 0 || z >= _height) return false;
            var terrain = _terrain[z, x];
            return terrain == TerrainType.Road || terrain == TerrainType.Bridge;
        }

        // ---- coordinate conversion -------------------------------------------------

        public Vector3 GridToWorld(Vector2Int g)
        {
            return new Vector3(g.x, 0f, g.y);
        }

        public Vector2Int WorldToGrid(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
        }

        public Vector3 GetCellCenterWorld(Vector2Int g)
        {
            return GridToWorld(g);
        }

        /// <summary>Intersect a ray with the ground plane (y=0) and return the cell.</summary>
        public bool RayToGrid(Ray ray, out Vector2Int cell)
        {
            cell = default(Vector2Int);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (!plane.Raycast(ray, out dist)) return false;
            Vector3 hit = ray.GetPoint(dist);
            Vector2Int g = WorldToGrid(hit);
            if (!InBounds(g)) return false;
            cell = g;
            return true;
        }

        // ---- grid queries -----------------------------------------------------------

        public bool InBounds(Vector2Int c)
        {
            return c.x >= 0 && c.x < _width && c.y >= 0 && c.y < _height;
        }

        public bool IsWalkable(Vector2Int c)
        {
            return InBounds(c) && TerrainCatalog.IsWalkable(_terrain[c.y, c.x]) &&
                !_blocked.Contains(c);
        }

        public float GetSurfaceHeight(Vector2Int c)
        {
            if (!InBounds(c)) return 0f;
            if (_surfaceHeights != null) return _surfaceHeights[c.y, c.x];
            return TerrainCatalog.GetElevation(_terrain[c.y, c.x]);
        }

        public bool CanTraverse(Vector2Int from, Vector2Int to)
        {
            if (!InBounds(from) || !InBounds(to) || !IsWalkable(to)) return false;
            float difference = Mathf.Abs(GetSurfaceHeight(to) - GetSurfaceHeight(from));
            if (difference <= 0.26f) return true;
            bool ramp = _rampCells != null &&
                (_rampCells[from.y, from.x] || _rampCells[to.y, to.x]);
            if (!ramp || difference > 2.05f) return false;
            Vector2Int step = to - from;
            return RampConnects(from, step) || RampConnects(to, step);
        }

        bool RampConnects(Vector2Int cell, Vector2Int step)
        {
            if (_rampCells == null || !_rampCells[cell.y, cell.x]) return false;
            // Imported ramp prefabs do not share one native forward axis, so yaw alone
            // cannot reliably tell which grid axis their stairs use. Treat a ramp cell
            // as a four-way cardinal height connector; diagonal height changes remain
            // forbidden, which still prevents corner-cutting across cliffs.
            return Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1;
        }

        void BuildHandMapSurfaceData(HandAuthoredMapData map)
        {
            _surfaceHeights = new float[_height, _width];
            _rampCells = new bool[_height, _width];
            if (map == null || map.Tiles == null) return;

            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.X < 0 || tile.Y < 0 || tile.X >= _width || tile.Y >= _height) continue;
                if (tile.Category == HandTileCategory.Ramp)
                {
                    _rampCells[tile.Y, tile.X] = true;
                }
                if (!HandMapBattleMapProvider.ProvidesWalkableSurface(tile.Category)) continue;
                float height = tile.Z * map.LayerHeightScale + tile.HeightOffset;
                if (height > _surfaceHeights[tile.Y, tile.X])
                    _surfaceHeights[tile.Y, tile.X] = height;
            }
        }

        public bool IsOccupied(Vector2Int c)
        {
            return _occupied.Contains(c);
        }

        /// <summary>True when the cell is inside a building footprint (阶段B).</summary>
        public bool IsBlocked(Vector2Int c)
        {
            return _blocked.Contains(c);
        }

        public TerrainType GetTerrain(Vector2Int c)
        {
            if (!InBounds(c)) return TerrainType.Ocean;
            return _terrain[c.y, c.x];
        }

        public TerrainType[,] CreateTerrainSnapshot()
        {
            return (TerrainType[,])_terrain.Clone();
        }

        // ---- occupancy ----------------------------------------------------------------

        public void SetOccupied(Vector2Int c, bool occupied)
        {
            if (occupied) _occupied.Add(c);
            else _occupied.Remove(c);
        }

        /// <summary>
        /// Marks a cell as a building footprint cell (blocked + occupied). Blocked cells
        /// are unwalkable and cannot be occupied by units; used by BuildingRegistry on
        /// register/destroy. (阶段B)
        /// </summary>
        public void SetBlocked(Vector2Int c, bool blocked)
        {
            if (blocked) _blocked.Add(c);
            else _blocked.Remove(c);
            SetOccupied(c, blocked);
        }

        public void ClearOccupancy()
        {
            _occupied.Clear();
            _blocked.Clear();
        }

        /// <summary>
        /// 扫描 HandMap 找出"桥架在非 Water 之上"的情况。语义上桥必须跨在水面或山地上,
        /// 否则视觉与走位不一致。请到 HandMapBuilder 里把桥下方的格子改成 Water(或删除这座桥)。
        /// 只 Console 警告,不强制改数据——避免运行期偷偷改用户的 .asset。
        /// </summary>
        static void WarnBridgeOverNonWater(HandAuthoredMapData map)
        {
            if (map == null || map.Tiles == null) return;
            int bad = 0;
            var report = new System.Text.StringBuilder();
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.Category != HandTileCategory.Bridge) continue;
                if (tile.Z < 1) continue; // Z=0 桥不警告(用户意图明确)
                bool groundIsWater = false;
                for (int j = 0; j < map.Tiles.Count; j++)
                {
                    var t = map.Tiles[j];
                    if (t.X == tile.X && t.Y == tile.Y && t.Z == 0 &&
                        t.Category == HandTileCategory.Water) { groundIsWater = true; break; }
                }
                if (!groundIsWater)
                {
                    bad++;
                    if (report.Length < 256)
                        report.Append("(").Append(tile.X).Append(",").Append(tile.Y).Append(") ");
                }
            }
            if (bad > 0)
                Debug.LogWarning("[BattleGridController] " + map.name + " 包含 " + bad +
                    " 座 Z>=1 的桥跨在非 Water 之上。桥下应改为 Water tile,否则视觉与走位语义不一致。" +
                    (report.Length > 0 ? "首例: " + report.ToString() : ""));
        }
    }
}
