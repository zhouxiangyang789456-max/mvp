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
    }
}
