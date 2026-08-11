using UnityEngine;

namespace Mvp.Battle.Map
{
    /// <summary>
    /// Visual for one terrain cell: a flat quad lying on the ground (no collider,
    /// per the performance rules). Tinted by terrain color; raised slightly for
    /// hills/mountains so the iso camera reads pseudo-3D depth.
    /// </summary>
    public sealed class BattleCellView : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public TerrainType Terrain { get; private set; }

        SpriteRenderer _renderer;

        public void Setup(Vector2Int grid, TerrainType terrain)
        {
            GridPosition = grid;
            Terrain = terrain;

            // Lie flat, facing up so the iso camera sees the tile.
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = Mvp.Battle.SharedSprites.White;
            _renderer.color = TerrainCatalog.GetColor(terrain);
            // Explicit depth order (higher = drawn on top). The iso camera sees cells
            // with larger x+z as "in front", so those render over the rear tiles.
            _renderer.sortingOrder = grid.x + grid.y;

            // Small inset creates a dark grid line between tiles (the base ground shows through).
            transform.localScale = new Vector3(0.98f, 0.98f, 1f);

            // Pseudo-3D depth for terrain, matching the doc's rear hills/mountains/snow.
            transform.position = new Vector3(transform.position.x, GetElevation(terrain), transform.position.z);
        }

        static float GetElevation(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Hill: return 0.04f;
                case TerrainType.Mountain: return 0.08f;
                case TerrainType.SnowMountain: return 0.12f;
                default: return 0f;
            }
        }
    }
}
