using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle;

namespace Mvp.Battle.Map
{
    /// <summary>
    /// Owns the 12x10 logical grid and its visual: one flat quad per cell, no colliders
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

        TerrainType[,] _terrain;
        readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();
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
            _terrain = TestBattleMapData.Create();
            BuildVisual();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildVisual()
        {
            var rootGo = new GameObject("GridVisual");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = Vector3.zero;

            // Dark base so the 2% tile inset reads as a grid line.
            var baseGo = new GameObject("BaseGround");
            baseGo.transform.SetParent(rootGo.transform, false);
            baseGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            baseGo.transform.localScale = new Vector3(14f, 12f, 1f);
            baseGo.transform.localPosition = new Vector3(5.5f, -0.03f, 4.5f);
            var baseSr = baseGo.AddComponent<SpriteRenderer>();
            baseSr.sprite = SharedSprites.White;
            baseSr.color = new Color(0.08f, 0.10f, 0.13f);
            // Always behind the terrain tiles, otherwise the large quad's distance-sorted
            // draw order would cover the rear tiles.
            baseSr.sortingOrder = -10;

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
                    view.Setup(grid, _terrain[z, x]);
                    _cells.Add(view);
                }
            }
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
            return InBounds(c) && TerrainCatalog.IsWalkable(_terrain[c.y, c.x]);
        }

        public bool IsOccupied(Vector2Int c)
        {
            return _occupied.Contains(c);
        }

        public TerrainType GetTerrain(Vector2Int c)
        {
            if (!InBounds(c)) return TerrainType.Ocean;
            return _terrain[c.y, c.x];
        }

        // ---- occupancy ----------------------------------------------------------------

        public void SetOccupied(Vector2Int c, bool occupied)
        {
            if (occupied) _occupied.Add(c);
            else _occupied.Remove(c);
        }

        public void ClearOccupancy()
        {
            _occupied.Clear();
        }
    }
}
