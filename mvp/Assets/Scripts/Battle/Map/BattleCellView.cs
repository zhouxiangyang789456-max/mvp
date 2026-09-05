using UnityEngine;

namespace Mvp.Battle.Map
{
    /// <summary>
    /// Visual for one terrain cell. Uses a camera-facing isometric terrain sprite
    /// when available and falls back to the original tinted ground quad.
    /// </summary>
    public sealed class BattleCellView : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public TerrainType Terrain { get; private set; }

        SpriteRenderer _renderer;
        SpriteRenderer _underlayRenderer;
        GameObject _prefabInstance;
        public bool UsesPrefab { get { return _prefabInstance != null; } }
        public bool IncludesDecorations { get; private set; }

        public void SetupLogicalOnly(Vector2Int grid, TerrainType terrain)
        {
            GridPosition = grid;
            Terrain = terrain;
            transform.position = new Vector3(transform.position.x,
                TerrainCatalog.GetElevation(terrain), transform.position.z);
        }

        public void Setup(Vector2Int grid, TerrainType terrain, TerrainPrefabCatalog catalog,
            uint seed, int connectionMask)
        {
            if (catalog != null && catalog.TryPick(terrain, seed, grid, connectionMask,
                out var entry, out var variant))
            {
                GridPosition = grid;
                Terrain = terrain;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                transform.position = new Vector3(transform.position.x,
                    TerrainCatalog.GetElevation(terrain), transform.position.z);
                _prefabInstance = Instantiate(variant.Prefab, transform, false);
                _prefabInstance.transform.localPosition = Vector3.zero;
                _prefabInstance.transform.localRotation = Quaternion.Euler(0f, variant.Yaw, 0f);
                TerrainGeometryHelper.Prepare(_prefabInstance, entry, transform.position.y);
                IncludesDecorations = entry.IncludesDecorations;
                return;
            }
            Setup(grid, terrain);
        }

        public void Setup(Vector2Int grid, TerrainType terrain)
        {
            GridPosition = grid;
            Terrain = terrain;

            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            Sprite terrainSprite = TerrainVisualCatalog.GetSprite(terrain);
            if (terrainSprite != null)
            {
                _renderer.sprite = terrainSprite;
                _renderer.color = Color.white;
                var camera = Camera.main;
                transform.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
                transform.localScale = Vector3.one * TerrainVisualCatalog.GetScale(terrain);

                if (TerrainVisualCatalog.NeedsPlainUnderlay(terrain))
                {
                    var underlay = new GameObject("TerrainUnderlay");
                    underlay.transform.SetParent(transform, false);
                    _underlayRenderer = underlay.AddComponent<SpriteRenderer>();
                    _underlayRenderer.sprite = TerrainVisualCatalog.GetSprite(TerrainType.Plain);
                    _underlayRenderer.color = Color.white;
                    _underlayRenderer.sortingOrder = (grid.x + grid.y) * 2;
                }
            }
            else
            {
                transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                _renderer.sprite = Mvp.Battle.SharedSprites.White;
                _renderer.color = TerrainCatalog.GetColor(terrain);
                transform.localScale = new Vector3(0.98f, 0.98f, 1f);
            }
            // Explicit depth order (higher = drawn on top). The iso camera sees cells
            // with larger x+z as "in front", so those render over the rear tiles.
            _renderer.sortingOrder = (grid.x + grid.y) * 2 + 1;

            transform.position = new Vector3(transform.position.x,
                TerrainCatalog.GetElevation(terrain), transform.position.z);
        }

        public void ApplyConnections(int mask)
        {
            if (UsesPrefab) return;
            if (_renderer == null ||
                (Terrain != TerrainType.Road && Terrain != TerrainType.Bridge)) return;

            float rotation;
            var sprite = TerrainVisualCatalog.GetConnectedSprite(Terrain, mask, out rotation);
            if (sprite == null) return;
            _renderer.sprite = sprite;
            _renderer.color = Color.white;
            var material = TerrainVisualCatalog.GetChromaKeyMaterial();
            if (material != null) _renderer.sharedMaterial = material;
            var camera = Camera.main;
            transform.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            transform.Rotate(0f, 0f, rotation, Space.Self);
            transform.localScale = Vector3.one * TerrainVisualCatalog.GetConnectedScale(sprite);
        }

        /// <summary>
        /// Switches object-heavy terrain to a quiet base sprite while a 3D decoration is
        /// present. The original full sprite remains the automatic fallback.
        /// </summary>
        public void SetDecorationBase(bool active)
        {
            if (UsesPrefab) return;
            if (!active || _renderer == null) return;
            if (Terrain == TerrainType.Road || Terrain == TerrainType.Bridge ||
                Terrain == TerrainType.ShallowWater || Terrain == TerrainType.Ocean) return;

            // A camera-facing sprite would cut through an upright 3D prop. Use a quiet,
            // flat XZ quad for decorated cells so normal depth testing stays correct.
            _renderer.sprite = Mvp.Battle.SharedSprites.White;
            _renderer.color = TerrainVisualCatalog.GetDecorationBaseTint(Terrain);
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            transform.localScale = new Vector3(0.98f, 0.98f, 1f);
            if (_underlayRenderer != null) _underlayRenderer.gameObject.SetActive(false);
        }
    }
}
