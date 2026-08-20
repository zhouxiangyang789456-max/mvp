using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Shared;
using Mvp.Battle.Vision;
using Mvp.Battle.Outcome;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Drives world-space smooth movement for all units (性能文档: 点击地面→寻路→
    /// 平滑移动到目标). A* over the logical grid gives cell-center waypoints; the unit
    /// interpolates between them every frame, never teleporting cell-to-cell.
    ///
    /// - New move commands replace the old path (no per-frame re-path).
    /// - Unwalkable / occupied targets are rejected with invalid feedback.
    /// - Occupancy + grid position follow the unit cell-by-cell as it advances.
    /// </summary>
    public sealed class UnitMovementController : MonoBehaviour
    {
        public static UnitMovementController Instance { get; private set; }

        PathfindingService _pathfinder;
        readonly List<Vector2Int> _pathBuffer = new List<Vector2Int>();
        readonly Dictionary<UnitView, MoveState> _moveStates =
            new Dictionary<UnitView, MoveState>();
        readonly List<UnitView> _toRemove = new List<UnitView>();

        PoolableUi _targetMarker;
        float _markerTimer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.FastTick += OnFastTick;
        }

        void OnDisable()
        {
            BattleTickService.FastTick -= OnFastTick;
        }

        void OnDestroy()
        {
            var queue = PathRequestQueue.ExistingInstance;
            if (queue != null) queue.Clear();
            if (Instance == this) Instance = null;
            ReleaseMarker();
        }

        void Start()
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return;
            _pathfinder = new PathfindingService(grid);
            PathRequestQueue.Instance.Initialize(_pathfinder);
        }

        /// <summary>Shared A* instance (reused by the combat controller for pursuit).</summary>
        public PathfindingService Pathfinder { get { return _pathfinder; } }

        /// <summary>Issues a move command to the target cell. Returns false on reject.</summary>
        public bool CommandMove(UnitView unit, Vector2Int targetCell)
        {
            if (BattleSimulationState.IsFrozen) return false;
            var grid = BattleGridController.Instance;
            if (unit == null || unit.Data == null || grid == null) return false;

            // A new move command immediately cancels any active attack/pursuit
            // (战斗页面开发文档: 攻击单位被下达移动命令时，立即取消当前攻击命令).
            var combat = UnitCombatController.Instance;
            if (combat != null) combat.CancelCombat(unit);

            var data = unit.Data;
            if (data.State == UnitState.Dead) return false;
            if (!grid.InBounds(targetCell) || !grid.IsWalkable(targetCell))
            {
                InvalidFeedback(unit);
                return false;
            }
            if (targetCell == data.GridPosition) return true;
            if (grid.IsOccupied(targetCell))
            {
                InvalidFeedback(unit);
                return false;
            }
            if (_pathfinder == null) return false;

            if (!_pathfinder.FindPath(data.GridPosition, targetCell, _pathBuffer, false)
                || _pathBuffer.Count < 2)
            {
                InvalidFeedback(unit);
                return false;
            }

            MoveState ms;
            if (!_moveStates.TryGetValue(unit, out ms))
            {
                ms = new MoveState();
                _moveStates[unit] = ms;
            }
            ms.Cells.Clear();
            // output[0] is the start cell; waypoints are the rest.
            for (int i = 1; i < _pathBuffer.Count; i++) ms.Cells.Add(_pathBuffer[i]);
            ms.Index = 0;

            data.CurrentCommand.Type = UnitCommandType.Move;
            data.CurrentCommand.TargetPosition = GridToWorldWithElevation(targetCell);
            data.CurrentCommand.TargetUnit = null;
            data.State = UnitState.Moving;

            ShowTargetMarker(targetCell);
            return true;
        }

        public void CancelMove(UnitView unit)
        {
            if (unit == null) return;
            _moveStates.Remove(unit);
            var data = unit.Data;
            if (data != null && data.State == UnitState.Moving)
            {
                data.State = UnitState.Idle;
                data.CurrentCommand.Type = UnitCommandType.None;
            }
        }

        void OnFastTick()
        {
            if (_moveStates.Count == 0)
            {
                TickMarker();
                return;
            }

            _toRemove.Clear();
            foreach (var kv in _moveStates)
            {
                var unit = kv.Key;
                var ms = kv.Value;
                if (unit == null || unit.Data == null)
                {
                    _toRemove.Add(unit);
                    continue;
                }
                if (unit.Data.State == UnitState.Dead)
                {
                    _toRemove.Add(unit);
                    continue;
                }
                Advance(unit, ms);
                if (ms.Index >= ms.Cells.Count) _toRemove.Add(unit);
            }
            for (int i = 0; i < _toRemove.Count; i++) _moveStates.Remove(_toRemove[i]);

            TickMarker();
        }

        void TickMarker()
        {
            if (_targetMarker == null) return;
            _markerTimer -= Time.deltaTime;
            if (_markerTimer <= 0f) ReleaseMarker();
        }

        void Advance(UnitView unit, MoveState ms)
        {
            var data = unit.Data;
            float step = data.Definition.MoveSpeed * Time.deltaTime;
            Vector2Int cell = ms.Cells[ms.Index];
            Vector3 target = GridToWorldWithElevation(cell);
            Vector3 pos = unit.transform.position;

            float dx = target.x - pos.x;
            float dz = target.z - pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist <= MoveState.ArrivalTolerance)
            {
                SnapToCell(unit, cell);
                ms.Index++;
                if (ms.Index >= ms.Cells.Count)
                {
                    data.State = UnitState.Idle;
                    data.CurrentCommand.Type = UnitCommandType.None;
                }
                return;
            }

            pos.x += dx / dist * step;
            pos.z += dz / dist * step;
            pos.y = Mathf.MoveTowards(pos.y, target.y, step);
            unit.transform.position = pos;
        }

        /// <summary>Updates occupancy + grid position for a unit and snaps it to the cell center.</summary>
        public void SnapToCell(UnitView unit, Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            var data = unit.Data;
            if (grid == null || data == null) return;

            if (data.GridPosition != cell)
            {
                Vector2Int old = data.GridPosition;
                grid.SetOccupied(old, false);
                grid.SetOccupied(cell, true);
                data.GridPosition = cell;
                var sel = UnitSelectionController.Instance;
                if (sel != null) sel.UpdateCell(unit, old, cell);
                var spatial = BattleSpatialIndex.Instance;
                if (spatial != null) spatial.Move(unit, old, cell);
            }

            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            unit.transform.position = p;
        }

        /// <summary>Grid cell center elevated to the terrain surface.</summary>
        public Vector3 GridToWorldWithElevation(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return new Vector3(cell.x, 0f, cell.y);
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            return p;
        }

        void InvalidFeedback(UnitView unit)
        {
            if (unit != null) unit.FlashInvalid();
        }

        void ShowTargetMarker(Vector2Int cell)
        {
            ReleaseMarker();
            var p = GridToWorldWithElevation(cell);
            p.y += 0.02f;
            _targetMarker = UiPool.Instance.Get(
                UiPoolType.MoveTargetMarker, p,
                Quaternion.Euler(-90f, 0f, 0f),
                new Vector3(0.5f, 0.5f, 1f));
            _markerTimer = 1.2f;
        }

        void ReleaseMarker()
        {
            if (_targetMarker == null) return;
            var pool = UiPool.Instance;
            if (pool != null) pool.Release(_targetMarker);
            _targetMarker = null;
        }

        sealed class MoveState
        {
            public const float ArrivalTolerance = 0.08f;
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public int Index;
        }
    }
}
