using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;
using Mvp.Battle.Commanders;
using Mvp.Battle.Vision;

namespace Mvp.Battle.Formation
{
    /// <summary>
    /// Owns each commander's 3x3 deployment grid and preset/custom slot layout.
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
        public bool IsCombatEditing { get; private set; }
        public bool WillHoldAfterCombatEdit { get { return _holdAfterCombatEdit; } }
        public Vector2Int Anchor { get { return _anchor; } }

        const int RangeRadius = 1; // fixed 3x3 deployment grid
        const int MaxSlots = 9;

        static readonly int[] VerticalPreset = { 1, 4, 7, 0, 3, 6, 2, 5, 8 };
        static readonly int[] HorizontalPreset = { 3, 4, 5, 0, 1, 2, 6, 7, 8 };
        static readonly int[] SquarePreset = { 4, 3, 5, 1, 7, 0, 2, 6, 8 };

        Vector2Int _anchor;
        readonly List<PoolableUi> _rangeCells = new List<PoolableUi>();
        readonly List<PoolableUi> _slotCells = new List<PoolableUi>();
        readonly List<UnitView> _slotTargets = new List<UnitView>();
        readonly List<Vector2Int> _slotBuffer = new List<Vector2Int>();
        readonly Dictionary<string, int> _draftAssignments = new Dictionary<string, int>();
        CommanderGroupRuntime _editingGroup;
        FormationType _draftFormation;
        bool _holdAfterCombatEdit;

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
            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (active == null || BattlePhaseState.Current != BattlePhase.Deployment) return;
            _anchor = FindValidAnchor(active, active.Layout.UnitSlotAssignments.Count > 0
                ? active.AnchorCell : ComputeAnchor());
            active.AnchorCell = _anchor;
            IsDeploying = true;
            ShowRange(_anchor);
            if (active.Layout.UnitSlotAssignments.Count == 0)
                ApplyFormation(active.Formation);
            else
                RefreshCustomPreview(active);
        }

        public void ExitDeployMode()
        {
            if (!IsDeploying) return;
            IsDeploying = false;
            HideRange();
            HideSlots();
        }

        public bool CanBeginCombatEdit(CommanderGroupRuntime group, out string reason)
        {
            reason = null;
            if (BattlePhaseState.Current != BattlePhase.Combat)
            {
                reason = "仅战斗阶段需要使用战斗重整";
                return false;
            }
            if (group == null || group.IsDefeated)
            {
                reason = "没有可调整的指挥官编队";
                return false;
            }
            if (group.State != CommanderGroupState.Idle ||
                group.CurrentCommand.Type != GroupCommandType.None)
            {
                reason = group.State == CommanderGroupState.Regrouping
                    ? "编队正在重整" : "编队正在移动或交战，无法调整阵型";
                return false;
            }
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                if (member.Data.State != UnitState.Idle)
                {
                    reason = "编队单位尚未全部待命";
                    return false;
                }
            }
            return true;
        }

        public bool BeginCombatEdit(CommanderGroupRuntime group, out string reason)
        {
            return BeginCombatEdit(group, false, out reason);
        }

        public bool BeginCombatEdit(CommanderGroupRuntime group, bool holdAfterConfirm, out string reason)
        {
            if (!CanBeginCombatEdit(group, out reason)) return false;
            CancelCombatEdit();
            _editingGroup = group;
            _holdAfterCombatEdit = holdAfterConfirm;
            _draftFormation = group.Formation;
            _anchor = group.AnchorCell;
            _draftAssignments.Clear();
            var used = new HashSet<int>();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                int slot = member.Data.FormationSlotIndex;
                if (slot < 0 || slot >= MaxSlots || !used.Add(slot))
                    slot = FirstFreeSlot(used);
                used.Add(slot);
                _draftAssignments[member.Data.Id] = slot;
            }
            IsCombatEditing = true;
            ShowRange(_anchor);
            RefreshDraftPreview();
            return true;
        }

        public void CancelCombatEdit()
        {
            IsCombatEditing = false;
            _editingGroup = null;
            _holdAfterCombatEdit = false;
            _draftFormation = FormationType.Custom;
            _draftAssignments.Clear();
            HideRange();
            HideSlots();
        }

        public bool ConfirmCombatEdit(out string reason)
        {
            reason = null;
            if (!IsCombatEditing || _editingGroup == null)
            {
                reason = "当前没有待确认的阵型修改";
                return false;
            }
            if (!CanBeginCombatEdit(_editingGroup, out reason)) return false;
            var commands = CommanderGroupCommandController.Instance;
            bool enterHold = _holdAfterCombatEdit;
            if (enterHold)
            {
                _editingGroup.Skills.ResetModes();
                _editingGroup.Skills.PersistentMode = PersistentSkillMode.Guard;
            }
            if (commands == null || !commands.CommandCustomRegroup(
                _editingGroup, _draftAssignments, _draftFormation))
            {
                if (enterHold) _editingGroup.Skills.ResetModes();
                reason = "目标格被占用或部分单位无法到达，重整未执行";
                return false;
            }
            CurrentFormation = _draftFormation;
            CancelCombatEdit();
            return true;
        }

        public bool TryEditCombatSlot(UnitView unit, Vector2Int cell)
        {
            if (!IsCombatEditing || _editingGroup == null || unit == null || unit.Data == null ||
                unit.Data.CommanderGroupId != _editingGroup.GroupId || !InRange(cell)) return false;
            int targetSlot = CellToSlot(cell);
            if (targetSlot < 0) return false;
            string occupantId = null;
            foreach (var pair in _draftAssignments)
                if (pair.Value == targetSlot) { occupantId = pair.Key; break; }
            int oldSlot;
            if (!_draftAssignments.TryGetValue(unit.Data.Id, out oldSlot)) return false;
            _draftAssignments[unit.Data.Id] = targetSlot;
            if (!string.IsNullOrEmpty(occupantId) && occupantId != unit.Data.Id)
                _draftAssignments[occupantId] = oldSlot;
            CurrentFormation = FormationType.Custom;
            _draftFormation = FormationType.Custom;
            RefreshDraftPreview();
            return true;
        }

        static int FirstFreeSlot(HashSet<int> used)
        {
            for (int i = 0; i < MaxSlots; i++) if (!used.Contains(i)) return i;
            return -1;
        }

        // ---- formation selection ---------------------------------------------------

        /// <summary>Updates the UI formation context without issuing a regroup command.</summary>
        public void SyncFormationContext(FormationType type)
        {
            CurrentFormation = type;
        }

        public void SetFormation(FormationType type)
        {
            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;

            if (BattlePhaseState.Current == BattlePhase.Combat)
            {
                if (IsCombatEditing && active == _editingGroup)
                {
                    ApplyDraftPreset(type);
                    CurrentFormation = type;
                }
                return;
            }

            if (type == FormationType.Custom) return;
            CurrentFormation = type;
            if (active != null) active.Formation = type;
            _anchor = FindValidAnchor(active, _anchor);
            ApplyFormation(type);
        }

        /// <summary>Recomputes slots for the player army and applies them.</summary>
        public void ApplyFormation(FormationType type)
        {
            var sel = UnitSelectionController.Instance;
            var grid = BattleGridController.Instance;
            if (sel == null || grid == null) return;

            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (active == null) return;

            _slotTargets.Clear();
            for (int i = 0; i < active.Members.Count; i++)
            {
                var u = active.Members[i];
                if (u == null || u.Data == null) continue;
                if (u.Data.Team != TeamId.Player || u.Data.State == UnitState.Dead) continue;
                _slotTargets.Add(u);
            }

            _slotTargets.Sort((a, b) => a.Data.SpawnOrder.CompareTo(b.Data.SpawnOrder));
            if (_slotTargets.Count > MaxSlots)
            {
                for (int i = 0; i < _slotTargets.Count; i++) _slotTargets[i].FlashInvalid();
                return;
            }
            ComputeSlots(type, _slotTargets.Count, _anchor, _slotBuffer);
            int[] preset = GetPreset(type);
            for (int i = 0; i < _slotTargets.Count; i++)
                _slotTargets[i].Data.FormationSlotIndex = preset[i];
            active.Layout.Capture(active, _anchor, _slotTargets, _slotBuffer,
                BattlePhaseState.Current == BattlePhase.Combat);
            RefreshSlotPreview(_slotBuffer);
            ApplySlotsToUnits(grid);
        }

        public void LockAllFormations()
        {
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            for (int g = 0; g < registry.Groups.Count; g++)
            {
                var group = registry.Groups[g];
                if (group.Team != TeamId.Player || group.IsDefeated) continue;
                CaptureCurrentLayout(group, true);
            }
        }

        public bool ValidateAllDeployments(out string reason)
        {
            reason = null;
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) { reason = "没有可用的指挥官编队"; return false; }
            var used = new HashSet<int>();
            for (int g = 0; g < registry.Groups.Count; g++)
            {
                var group = registry.Groups[g];
                if (group.Team != TeamId.Player || group.IsDefeated) continue;
                used.Clear();
                int alive = 0;
                for (int i = 0; i < group.Members.Count; i++)
                {
                    var member = group.Members[i];
                    if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                    alive++;
                    int slot = member.Data.FormationSlotIndex;
                    if (slot < 0 || slot >= MaxSlots || !used.Add(slot))
                    {
                        reason = group.Definition.DisplayName + " 的布阵槽位无效或重复";
                        return false;
                    }
                }
                if (alive == 0) { reason = group.Definition.DisplayName + " 没有可出战单位"; return false; }
                if (alive > MaxSlots) { reason = group.Definition.DisplayName + " 超过 3×3 布阵上限"; return false; }
            }
            return true;
        }

        void CaptureCurrentLayout(CommanderGroupRuntime group, bool locked)
        {
            _slotTargets.Clear();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                _slotTargets.Add(member);
            }
            _slotTargets.Sort((a, b) => a.Data.FormationSlotIndex.CompareTo(b.Data.FormationSlotIndex));
            Vector2Int anchor = group.AnchorCell;
            if (_slotTargets.Count > 0 && !group.Layout.Locked &&
                group.Layout.UnitSlotAssignments.Count == 0)
                anchor = ComputeAnchorFor(group);
            _slotBuffer.Clear();
            for (int i = 0; i < _slotTargets.Count; i++)
                _slotBuffer.Add(_slotTargets[i].Data.GridPosition);
            group.AnchorCell = anchor;
            group.Layout.Capture(group, anchor, _slotTargets, _slotBuffer, locked);
        }

        static Vector2Int ComputeAnchorFor(CommanderGroupRuntime group)
        {
            int sx = 0, sy = 0, count = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                sx += member.Data.GridPosition.x;
                sy += member.Data.GridPosition.y;
                count++;
            }
            return count > 0
                ? new Vector2Int(Mathf.RoundToInt(sx / (float)count), Mathf.RoundToInt(sy / (float)count))
                : group.AnchorCell;
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

        /// <summary>Moves the selected unit into a 3x3 slot, swapping with its occupant.</summary>
        public bool TryPlace(UnitView unit, Vector2Int cell)
        {
            if (unit == null || unit.Data == null || !IsDeploying) return false;
            if (unit.Data.Team != TeamId.Player || unit.Data.State == UnitState.Dead) return false;
            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (active == null || unit.Data.CommanderGroupId != active.GroupId) return false;

            var grid = BattleGridController.Instance;
            if (grid == null) return false;
            if (!InRange(cell)) { unit.FlashInvalid(); return false; }
            if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) { unit.FlashInvalid(); return false; }

            var sel = UnitSelectionController.Instance;
            var other = sel != null ? sel.FindAtCell(cell) : null;
            if (other != null && (other.Data == null ||
                other.Data.CommanderGroupId != active.GroupId))
            {
                unit.FlashInvalid();
                return false;
            }
            if (BattlePhaseState.Current != BattlePhase.Deployment)
            {
                unit.FlashInvalid();
                return false;
            }

            int oldSlot = unit.Data.FormationSlotIndex;
            int targetSlot = CellToSlot(cell);
            if (targetSlot < 0) return false;
            if (other == unit) return true;

            Vector2Int oldCell = unit.Data.GridPosition;
            SnapUnit(unit, cell);
            unit.Data.FormationSlotIndex = targetSlot;
            if (other != null)
            {
                SnapUnit(other, oldCell);
                other.Data.FormationSlotIndex = oldSlot;
            }

            active.Formation = FormationType.Custom;
            CurrentFormation = FormationType.Custom;
            SaveCustomLayout(active);
            RefreshCustomPreview(active);
            return true;
        }

        /// <summary>Moves the active commander's whole formation anchor during deployment.</summary>
        public bool PlaceActiveGroup(Vector2Int cell)
        {
            return false;
        }

        // ---- internals --------------------------------------------------------------

        Vector2Int ComputeAnchor()
        {
            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (active == null) return new Vector2Int(5, 5);
            int sx = 0, sz = 0, n = 0;
            for (int i = 0; i < active.Members.Count; i++)
            {
                var u = active.Members[i];
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
            var active = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (active == null) return 0;
            int n = 0;
            for (int i = 0; i < active.Members.Count; i++)
            {
                var u = active.Members[i];
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

        static Vector2Int FindValidAnchor(CommanderGroupRuntime group, Vector2Int preferred)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return preferred;
            preferred.x = Mathf.Clamp(preferred.x, 1, grid.Width - 2);
            preferred.y = Mathf.Clamp(preferred.y, 1, grid.Height - 2);
            for (int radius = 0; radius < Mathf.Max(grid.Width, grid.Height); radius++)
            {
                for (int y = preferred.y - radius; y <= preferred.y + radius; y++)
                for (int x = preferred.x - radius; x <= preferred.x + radius; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (candidate.x < 1 || candidate.x >= grid.Width - 1 ||
                        candidate.y < 1 || candidate.y >= grid.Height - 1) continue;
                    bool valid = true;
                    for (int slot = 0; slot < MaxSlots; slot++)
                    {
                        var cell = candidate + SlotOffset(slot);
                        if (!grid.IsWalkable(cell)) { valid = false; break; }
                        if (!grid.IsOccupied(cell)) continue;
                        var selection = UnitSelectionController.Instance;
                        var occupant = selection != null ? selection.FindAtCell(cell) : null;
                        if (occupant == null || occupant.Data == null || group == null ||
                            occupant.Data.CommanderGroupId != group.GroupId)
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid) return candidate;
                }
            }
            return preferred;
        }

        bool InRange(Vector2Int cell)
        {
            int dx = Mathf.Abs(cell.x - _anchor.x);
            int dz = Mathf.Abs(cell.y - _anchor.y);
            return dx <= RangeRadius && dz <= RangeRadius;
        }

        int CellToSlot(Vector2Int cell)
        {
            int x = cell.x - _anchor.x + 1;
            int y = cell.y - _anchor.y + 1;
            return x >= 0 && x < 3 && y >= 0 && y < 3 ? y * 3 + x : -1;
        }

        static Vector2Int SlotOffset(int slot)
        {
            return new Vector2Int((slot % 3) - 1, (slot / 3) - 1);
        }

        static int[] GetPreset(FormationType type)
        {
            switch (type)
            {
                case FormationType.Vertical: return VerticalPreset;
                case FormationType.Horizontal: return HorizontalPreset;
                default: return SquarePreset;
            }
        }

        static void ComputeSlots(FormationType type, int count, Vector2Int anchor, List<Vector2Int> outSlots)
        {
            outSlots.Clear();
            if (count <= 0) return;
            int[] preset = GetPreset(type);
            int capped = Mathf.Min(count, MaxSlots);
            for (int i = 0; i < capped; i++)
                outSlots.Add(anchor + SlotOffset(preset[i]));
        }

        void SaveCustomLayout(CommanderGroupRuntime group)
        {
            _slotTargets.Clear();
            _slotBuffer.Clear();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                _slotTargets.Add(member);
                _slotBuffer.Add(_anchor + SlotOffset(member.Data.FormationSlotIndex));
            }
            group.AnchorCell = _anchor;
            group.Layout.Capture(group, _anchor, _slotTargets, _slotBuffer, false);
        }

        void RefreshCustomPreview(CommanderGroupRuntime group)
        {
            _slotBuffer.Clear();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                _slotBuffer.Add(_anchor + SlotOffset(member.Data.FormationSlotIndex));
            }
            RefreshSlotPreview(_slotBuffer);
        }

        void ApplyDraftPreset(FormationType type)
        {
            if (_editingGroup == null || type == FormationType.Custom) return;
            _slotTargets.Clear();
            for (int i = 0; i < _editingGroup.Members.Count; i++)
            {
                var member = _editingGroup.Members[i];
                if (member != null && member.Data != null && member.Data.State != UnitState.Dead)
                    _slotTargets.Add(member);
            }
            _slotTargets.Sort((a, b) => a.Data.SpawnOrder.CompareTo(b.Data.SpawnOrder));
            int[] preset = GetPreset(type);
            for (int i = 0; i < _slotTargets.Count && i < MaxSlots; i++)
                _draftAssignments[_slotTargets[i].Data.Id] = preset[i];
            _draftFormation = type;
            RefreshDraftPreview();
        }

        void RefreshDraftPreview()
        {
            _slotBuffer.Clear();
            foreach (var pair in _draftAssignments)
                _slotBuffer.Add(_anchor + SlotOffset(pair.Value));
            RefreshSlotPreview(_slotBuffer);
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
            var registry = CommanderGroupRegistry.Instance;
            var commandController = CommanderGroupCommandController.Instance;
            var group = registry != null ? registry.Find(unit) : null;
            if (commandController != null && group != null &&
                group.CurrentCommand.Type != GroupCommandType.None)
                commandController.CancelGroupCommand(group);
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
            var spatial = BattleSpatialIndex.Instance;
            if (spatial != null) spatial.Move(unit, old, cell);

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
