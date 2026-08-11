using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Shared;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Spawns the starting army from BattleStartContext (伊莲娜: 2 步兵 + 1 坦克)
    /// plus one enemy test unit for verifying attack commands. Placeholder visuals
    /// per 回退策略. Attach to the scene; Start() runs once at battle start.
    /// </summary>
    public sealed class UnitSpawner : MonoBehaviour
    {
        static readonly Vector2Int[] PlayerCells =
        {
            new Vector2Int(4, 7), // desert (walkable)
            new Vector2Int(5, 7), // plain
            new Vector2Int(6, 7)  // plain
        };

        static readonly Vector2Int EnemyCell = new Vector2Int(6, 4); // rear mountain

        bool _spawned;

        void Start()
        {
            if (_spawned) return;
            _spawned = true;
            SpawnAll();
        }

        public void SpawnAll()
        {
            BattleCore.Ensure();
            var grid = BattleGridController.Instance;
            if (grid == null)
            {
                Debug.LogError("[UnitSpawner] BattleGridController.Instance missing");
                return;
            }

            var commander = BattleStartContext.SelectedCommander;
            if (commander != null && commander.StartingUnits.Count > 0)
            {
                int idx = 0;
                for (int e = 0; e < commander.StartingUnits.Count; e++)
                {
                    var entry = commander.StartingUnits[e];
                    var def = UnitCatalog.Get(entry.UnitType);
                    if (def == null) continue;
                    for (int i = 0; i < entry.Count; i++)
                    {
                        SpawnUnit(def, TeamId.Player, PlayerCells[idx % PlayerCells.Length]);
                        idx++;
                    }
                }
            }
            else
            {
                // Editor fallback when the scene is opened directly.
                SpawnUnit(UnitCatalog.Get(UnitType.Infantry), TeamId.Player, PlayerCells[0]);
                SpawnUnit(UnitCatalog.Get(UnitType.Infantry), TeamId.Player, PlayerCells[1]);
                SpawnUnit(UnitCatalog.Get(UnitType.Tank), TeamId.Player, PlayerCells[2]);
            }

            SpawnUnit(UnitCatalog.Get(UnitType.Tank), TeamId.Enemy, EnemyCell);
        }

        /// <summary>Creates a unit's runtime data + placeholder view on a grid cell.</summary>
        public UnitView SpawnUnit(UnitDefinition def, TeamId team, Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (def == null || grid == null) return null;
            if (!grid.InBounds(cell) || !grid.IsWalkable(cell) || grid.IsOccupied(cell)) return null;

            grid.SetOccupied(cell, true);

            var data = new UnitRuntimeData
            {
                Id = "unit_" + team + "_" + cell.x + "_" + cell.y,
                Team = team,
                Definition = def,
                CurrentHealth = def.MaxHealth,
                State = UnitState.Idle,
                GridPosition = cell
            };

            var go = new GameObject(data.Id);
            var view = go.AddComponent<UnitView>();

            var world = grid.GridToWorld(cell);
            world.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            view.Spawn(data, world);
            view.AttachHealthBar();

            return view;
        }
    }
}
