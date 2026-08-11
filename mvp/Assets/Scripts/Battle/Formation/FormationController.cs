using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.Formation
{
    /// <summary>
    /// 阵型系统 (战斗页面开发文档). Owns the deployment range (5x5 green cells) and the
    /// Vertical / Horizontal / Square formation slots for the player's army.
    ///
    /// - EnterDeployMode shows the range and applies the current formation.
    /// - SetFormation swaps the pattern and re-applies it (deployment snaps units;
    ///   real-time combat issues move commands).
    /// - TryPlace lets the selection controller snap/move a single unit onto a range cell.
    ///
    /// Entry is normally the commander portrait (UI milestone); 'F' is a temporary debug
    /// toggle and '1'/'2'/'3' switch Vertical / Horizontal / Square for testing.
    /// </summary>
    public sealed class FormationController : MonoBehaviour
    {
        public static FormationController Instance { get; private set; }

        public FormationType CurrentFormation { get; private set; } = FormationType.Vertical;
        public bool IsDeploying { get; private set; }
        public Vector2Int Anchor { get { return _anchor; } }

        const int RangeRadius = 2; // 5x5 deployment range

        Vector2Int _anchor;
        readonly List<PoolableUi> _rangeCells = new List<PoolableUi>();
        readonly List<PoolableUi> _slotCells = new List<PoolableUi>();
        readonly List<UnitView> _slotTargets = new List<UnitView>();
        readonly List<Vector2Int> _slotBuffer = new List<Vector2Int>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseCells(_rangeCells);
            ReleaseCells(_slotCells);
        }

        void Update()
        {
            // Temporary debug entry points until the commander portrait / formation
            // buttons are wired in the UI milestone.
            if (Input.GetKeyDown(KeyCode.F)) ToggleDeploy();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetFormation(FormationType.Vertical);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetFormation(FormationType.Horizontal);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetFormation(FormationType.Square);

            if (IsDeploying && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                ExitDeployMode();
            }
        }

        // ---- deploy mode -----------------------------------------------------------

        public void ToggleDeploy()
        {
            if (IsDeploying) ExitDeployMode();
            else EnterDeployMode();
        }

        public void EnterDeployMode()
        {
            if (IsDeploying) return;
            _anchor = ClampAnchor(ComputeAnchor(), CurrentFormation, CountPlayerUnits());
            IsDeploying = true;
            ShowRange(_anchor);
            ApplyFormation(CurrentFormation);
        }

        public void ExitDeployMode()
        {
            if (!IsDeploying) return;
            IsDeploying = false;
            HideRange();
            HideSlots();
        }

        // ---- formation selection ---------------------------------------------------

        public void SetFormation(FormationType type)
        {
            if (CurrentFormation == type) return;
            CurrentFormation = type;
            _anchor = ClampAnchor(_anchor, type, CountPlayerUnits());
            ApplyFormation(type);
        }

        /// <summary>Recomputes slots for the player army and applies them.</summary>
        public void ApplyFormation(FormationType type)
        {
            var sel = UnitSelectionController.Instance;
            var grid = BattleGridController.Instance;
            if (sel == null || grid == null) return;

            _slotTargets.Clear();
            for (int i = 0; i < sel.Units.Count; i++)
            {
                var u = sel.Units[i];
                if (u == null || u.Data == null) continue;
                if (u.Data.Team != TeamId.Player || u.Data.State == UnitState.Dead) continue;
                _slotTargets.Add(u);
            }

            ComputeSlots(type, _slotTargets.Count, _anchor, _slotBuffer);
            RefreshSlotPreview(_slotBuffer);
            ApplySlotsToUnits(grid);
        }

        void ApplySlotsToUnits(BattleGridController grid)
        {
            bool deploy = BattlePhaseState.Current == BattlePhase.Deployment;

            // Deployment snap rearranges everyone at once: pre-free all player cells
            // first so the formation slots cannot collide (SnapUnit only marks the
            // NEW cell occupied). Combat mode must NOT pre-free - occupancy is owned
            // by the movement controller as units walk, so freeing here would leave
            // stale unoccupied cells under units that are still moving.
            if (deploy)
            {
                for (int i = 0; i < _slotTargets.Count; i++)
                {
                    if (_slotTargets[i] != null && _slotTargets[i].Data != null)
                        grid.SetOccupied(_slotTargets[i].Data.GridPosition, false);
                }
            }

            for (int i = 0; i < _slotTargets.Count; i++)
            {
                var unit = _slotTargets[i];
                if (unit == null || unit.Data == null) continue;
                Vector2Int slot = _slotBuffer[i];

                if (!grid.InBounds(slot) || !grid.IsWalkable(slot))
                {
                    // Invalid slot: keep the unit where it is. (Deploy mode re-occupies
                    // its cell because the pre-free pass cleared it.)
                    if (deploy) grid.SetOccupied(unit.Data.GridPosition, true);
                    continue;
                }

                if (slot == unit.Data.GridPosition)
                {
                    // Already on the slot: keep it occupied.
                    if (deploy) grid.SetOccupied(slot, true);
                    continue;
                }

                if (deploy) SnapUnit(unit, slot);
                else
                {
                    var move = UnitMovementController.Instance;
                    if (move != null) move.CommandMove(unit, slot);
                }
            }
        }

        /// <summary>Single-unit placement from a deploy-range click (selection controller).</summary>
        public bool TryPlace(UnitView unit, Vector2Int cell)
        {
            if (unit == null || unit.Data == null || !IsDeploying) return false;
            if (unit.Data.Team != TeamId.Player || unit.Data.State == UnitState.Dead) return false;

            var grid = BattleGridController.Instance;
            if (grid == null) return false;
            if (!InRange(cell)) { unit.FlashInvalid(); return false; }
            if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) { unit.FlashInvalid(); return false; }

            var sel = UnitSelectionController.Instance;
            var other = sel != null ? sel.FindAtCell(cell) : null;
            if (grid.IsOccupied(cell) && other != null && other != unit)
            {
                unit.FlashInvalid();
                return false;
            }

            if (BattlePhaseState.Current == BattlePhase.Deployment) SnapUnit(unit, cell);
            else
            {
                var move = UnitMovementController.Instance;
                if (move != null) move.CommandMove(unit, cell);
            }
            return true;
        }

        // ---- internals --------------------------------------------------------------

        Vector2Int ComputeAnchor()
        {
            var sel = UnitSelectionController.Instance;
            if (sel == null) return new Vector2Int(5, 5);
            int sx = 0, sz = 0, n = 0;
            for (int i = 0; i < sel.Units.Count; i++)
            {
                var u = sel.Units[i];
                if (u == null || u.Data == null) continue;
                if (u.Data.Team != TeamId.Player || u.Data.State == UnitState.Dead) continue;
                sx += u.Data.GridPosition.x;
                sz += u.Data.GridPosition.y;
                n++;
            }
            if (n == 0) return new Vector2Int(5, 5);
            return new Vector2Int(
                Mathf.RoundToInt(sx / (float)n),
                Mathf.RoundToInt(sz / (float)n));
        }

        int CountPlayerUnits()
        {
            var sel = UnitSelectionController.Instance;
            if (sel == null) return 0;
            int n = 0;
            for (int i = 0; i < sel.Units.Count; i++)
            {
                var u = sel.Units[i];
                if (u == null || u.Data == null) continue;
                if (u.Data.Team != TeamId.Player || u.Data.State == UnitState.Dead) continue;
                n++;
            }
            return n;
        }

        /// <summary>
        /// Keeps the formation anchor inside the grid so every slot stays in bounds:
        /// Vertical spills upward, Horizontal spills rightward, Square spills in a
        /// rows-first block. Without this, an anchor near the map edge produces
        /// out-of-bounds slots and ApplySlotsToUnits leaves units on stale cells,
        /// which can stack two units on one cell.
        /// </summary>
        static Vector2Int ClampAnchor(Vector2Int anchor, FormationType type, int count)
        {
            var grid = BattleGridController.Instance;
            if (grid == null || count <= 1) return anchor;
            switch (type)
            {
                case FormationType.Vertical:
                    anchor.y = Mathf.Clamp(anchor.y, 0, grid.Height - count);
                    break;
                case FormationType.Horizontal:
                    anchor.x = Mathf.Clamp(anchor.x, 0, grid.Width - count);
                    break;
                case FormationType.Square:
                    int side = Mathf.CeilToInt(Mathf.Sqrt(count));
                    anchor.x = Mathf.Clamp(anchor.x, 0, grid.Width - side);
                    anchor.y = Mathf.Clamp(anchor.y, 0, grid.Height - side);
                    break;
            }
            return anchor;
        }

        bool InRange(Vector2Int cell)
        {
            int dx = Mathf.Abs(cell.x - _anchor.x);
            int dz = Mathf.Abs(cell.y - _anchor.y);
            return dx <= RangeRadius && dz <= RangeRadius;
        }

        static void ComputeSlots(FormationType type, int count, Vector2Int anchor, List<Vector2Int> outSlots)
        {
            outSlots.Clear();
            if (count <= 0) return;
            switch (type)
            {
                case FormationType.Vertical:
                    for (int i = 0; i < count; i++) outSlots.Add(new Vector2Int(anchor.x, anchor.y + i));
                    break;
                case FormationType.Horizontal:
                    for (int i = 0; i < count; i++) outSlots.Add(new Vector2Int(anchor.x + i, anchor.y));
                    break;
                case FormationType.Square:
                    int side = Mathf.CeilToInt(Mathf.Sqrt(count));
                    for (int i = 0; i < count; i++)
                    {
                        outSlots.Add(new Vector2Int(anchor.x + (i % side), anchor.y + (i / side)));
                    }
                    break;
            }
        }

        /// <summary>
        /// Deployment-phase placement. Frees the unit's own old cell only when the
        /// selection registry still maps it to this unit (so we never free another
        /// unit's slot during a swap/rearrange; ApplySlotsToUnits pre-frees all
        /// player cells anyway), then marks the new cell occupied and updates
        /// grid position / transform.
        /// </summary>
        void SnapUnit(UnitView unit, Vector2Int cell)
        {
            var data = unit.Data;
            if (data == null) return;
            var combat = UnitCombatController.Instance;
            if (combat != null) combat.CancelCombat(unit);
            var move = UnitMovementController.Instance;
            if (move != null) move.CancelMove(unit);

            var grid = BattleGridController.Instance;
            if (grid == null) return;
            Vector2Int old = data.GridPosition;
            var sel = UnitSelectionController.Instance;
            if (sel != null && sel.FindAtCell(old) == unit) grid.SetOccupied(old, false);
            grid.SetOccupied(cell, true);
            data.GridPosition = cell;
            if (sel != null) sel.UpdateCell(unit, old, cell);

            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            unit.transform.position = p;
            data.State = UnitState.Idle;
            data.CurrentCommand.Type = UnitCommandType.None;
            data.CurrentCommand.TargetUnit = null;
        }

        // ---- pooled visuals ----------------------------------------------------------

        void ShowRange(Vector2Int anchor)
        {
            var grid = BattleGridController.Instance;
            var pool = UiPool.Instance;
            if (grid == null || pool == null) return;

            ReleaseCells(_rangeCells);
            for (int dx = -RangeRadius; dx <= RangeRadius; dx++)
            {
                for (int dz = -RangeRadius; dz <= RangeRadius; dz++)
                {
                    var cell = new Vector2Int(anchor.x + dx, anchor.y + dz);
                    if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                    var ui = pool.Get(UiPoolType.DeploymentCellHighlight,
                        GridWorld(cell), Quaternion.Euler(-90f, 0f, 0f),
                        new Vector3(0.98f, 0.98f, 1f));
                    if (ui != null) _rangeCells.Add(ui);
                }
            }
        }

        void HideRange()
        {
            ReleaseCells(_rangeCells);
        }

        void RefreshSlotPreview(List<Vector2Int> slots)
        {
            var grid = BattleGridController.Instance;
            var pool = UiPool.Instance;
            ReleaseCells(_slotCells);
            if (grid == null || pool == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                Vector2Int cell = slots[i];
                if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                var ui = pool.Get(UiPoolType.DeploymentCellHighlight,
                    GridWorld(cell), Quaternion.Euler(-90f, 0f, 0f),
                    new Vector3(0.72f, 0.72f, 1f));
                if (ui != null)
                {
                    // Render slot preview dots above the range grid.
                    var sr = ui.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = 86;
                    _slotCells.Add(ui);
                }
            }
        }

        void HideSlots()
        {
            ReleaseCells(_slotCells);
        }

        static void ReleaseCells(List<PoolableUi> cells)
        {
            var pool = UiPool.Instance;
            for (int i = 0; i < cells.Count; i++)
            {
                if (pool != null) pool.Release(cells[i]);
            }
            cells.Clear();
        }

        static Vector3 GridWorld(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return new Vector3(cell.x, 0.02f, cell.y);
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell)) + 0.02f;
            return p;
        }
    }
}
