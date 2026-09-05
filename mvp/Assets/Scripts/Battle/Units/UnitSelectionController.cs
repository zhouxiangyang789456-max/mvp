using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Mvp.Battle.Map;
using Mvp.Battle.Buildings;
using Mvp.Battle.Skills;
using Mvp.Shared;
using Mvp.Battle.Commanders;
using Mvp.Battle.UI;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Single-selection controller (per 战斗页面开发文档: 第一版只支持单选).
    /// Maintains a cell->unit registry so clicks resolve to units through the
    /// grid (no physics raycast, consistent with the map's no-collider rule).
    ///
    /// Click behavior (selection only for this milestone):
    ///   - click a Player unit      -> select it, show pooled selection ring
    ///   - click ground / enemy     -> clear selection (move/attack arrive later)
    ///   - click over UI            -> ignored
    /// </summary>
    public sealed class UnitSelectionController : MonoBehaviour
    {
        const float UnitPickRadiusPixels = 34f;
        public static UnitSelectionController Instance { get; private set; }

        public UnitView Selected { get; private set; }
        public Vector3 LastMapAimPoint { get; private set; }
        public bool HasMapAimPoint { get; private set; }

        readonly Dictionary<Vector2Int, UnitView> _byCell =
            new Dictionary<Vector2Int, UnitView>();
        readonly List<UnitView> _units = new List<UnitView>();
        readonly List<PoolableUi> _attackRangeCells = new List<PoolableUi>();

        PoolableUi _selectionRing;
        bool _attackRangeHiddenForMovement;

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
            ReleaseRing();
            ReleaseAttackRange();
        }

        void Start()
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return;
            LastMapAimPoint = new Vector3(
                (grid.Width - 1) * 0.5f,
                0f,
                (grid.Height - 1) * 0.5f);
            HasMapAimPoint = true;
        }

        void Update()
        {
            RefreshAttackRangeVisibility();

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                // Targeting intercepts right-click / Esc to cancel the skill cast.
                var skillTargeting = SkillTargetingController.Instance;
                if (skillTargeting != null && skillTargeting.IsTargeting)
                    skillTargeting.CancelTargeting();
                var formation = Mvp.Battle.Formation.FormationController.Instance;
                if (formation != null && formation.IsCombatEditing)
                {
                    formation.CancelCombatEdit();
                    ClearSelection();
                    var battleUi = Mvp.Battle.UI.BattleUiController.Instance;
                    if (battleUi != null) battleUi.RefreshCombatFormationControls();
                    return;
                }
                ClearSelection();
                if (CommanderGroupRegistry.Instance != null)
                    CommanderGroupRegistry.Instance.CloseCommanderInspection();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            RecordMapAimPoint(Input.mousePosition);

            // Skill targeting sits after right-click/Esc but before any normal selection:
            // while a target is being chosen, left-click goes to the skill and is consumed.
            var targeting = SkillTargetingController.Instance;
            if (targeting != null && targeting.IsTargeting)
            {
                targeting.TryHandleLeftClick(Input.mousePosition);
                return;
            }

            var groups = CommanderGroupRegistry.Instance;
            if (groups != null && groups.TryPickMarker(Input.mousePosition))
            {
                Selected = null;
                ReleaseRing();
                ReleaseAttackRange();
                return;
            }

            var cam = Camera.main;
            var grid = BattleGridController.Instance;
            if (cam == null || grid == null) return;

            var pickedUnit = PickUnitAtScreen(Input.mousePosition, cam);
            if (pickedUnit != null)
            {
                HandleUnitClick(pickedUnit);
                return;
            }

            if (grid.RayToGrid(cam.ScreenPointToRay(Input.mousePosition), out var cell))
            {
                // Building footprint cells are blocked/occupied, so no unit can stand on
                // them — check the building registry before the generic click handler (阶段B).
                var building = BuildingRegistry.Instance != null
                    ? BuildingRegistry.Instance.GetAt(cell) : null;
                if (building != null)
                {
                    HandleBuildingClick(building);
                    return;
                }
                HandleClick(cell);
            }
        }

        void RecordMapAimPoint(Vector2 screenPosition)
        {
            var cam = Camera.main;
            var grid = BattleGridController.Instance;
            if (cam == null || grid == null) return;
            Vector2Int cell;
            if (!grid.RayToGrid(cam.ScreenPointToRay(screenPosition), out cell)) return;
            LastMapAimPoint = grid.GetCellCenterWorld(cell);
            LastMapAimPoint += Vector3.up * TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            HasMapAimPoint = true;
        }

        // ---- registry ------------------------------------------------------------

        public IReadOnlyList<UnitView> Units { get { return _units; } }

        public void Register(UnitView unit)
        {
            if (unit == null || unit.Data == null) return;
            _byCell[unit.Data.GridPosition] = unit;
            if (!_units.Contains(unit)) _units.Add(unit);
        }

        public void Unregister(UnitView unit)
        {
            if (unit == null) return;
            if (unit.Data != null) _byCell.Remove(unit.Data.GridPosition);
            _units.Remove(unit);
            if (Selected == unit) ClearSelection();
        }

        public UnitView FindAtCell(Vector2Int cell)
        {
            UnitView unit;
            return _byCell.TryGetValue(cell, out unit) ? unit : null;
        }

        /// <summary>Moves a unit's registry entry when it advances to a new cell.</summary>
        public void UpdateCell(UnitView unit, Vector2Int oldCell, Vector2Int newCell)
        {
            if (unit == null || unit.Data == null) return;
            UnitView existing;
            if (_byCell.TryGetValue(oldCell, out existing) && existing == unit)
            {
                _byCell.Remove(oldCell);
            }
            _byCell[newCell] = unit;
        }

        // ---- selection -------------------------------------------------------------

        void HandleUnitClick(UnitView unit)
        {
            if (unit == null || unit.Data == null) return;
            var formation = Mvp.Battle.Formation.FormationController.Instance;
            bool deploying = formation != null && formation.IsDeploying;
            bool combatEditing = formation != null && formation.IsCombatEditing;

            if (unit.Data.Team == TeamId.Player && unit.Data.State != UnitState.Dead)
            {
                if (combatEditing && Selected != null && Selected != unit &&
                    Selected.Data.CommanderGroupId == unit.Data.CommanderGroupId)
                {
                    formation.TryEditCombatSlot(Selected, unit.Data.GridPosition);
                    return;
                }
                if (deploying && Selected != null && Selected != unit &&
                    Selected.Data.CommanderGroupId == unit.Data.CommanderGroupId)
                {
                    formation.TryPlace(Selected, unit.Data.GridPosition);
                    return;
                }
                Select(unit);
                return;
            }

            if (deploying || combatEditing) return;
            var registry = CommanderGroupRegistry.Instance;
            var group = registry != null ? registry.ActiveGroup : null;
            if (group == null)
            {
                ClearSelection();
                return;
            }
            // 隐蔽编队不可被选中/锁定：敌人看不到隐蔽单位，玩家也无法锁定隐藏目标。
            var targetGroup = registry.Find(unit);
            if (targetGroup != null && ConcealmentService.IsConcealed(targetGroup))
            {
                ClearSelection();
                return;
            }
            var commands = CommanderGroupCommandController.Instance;
            if (commands != null) commands.CommandAttack(group, unit);
        }

        /// <summary>Routes a left-click on a grid cell through selection / move rules.</summary>
        public void HandleClick(Vector2Int cell)
        {
            var formation = Mvp.Battle.Formation.FormationController.Instance;
            bool deploying = formation != null && formation.IsDeploying;
            bool combatEditing = formation != null && formation.IsCombatEditing;

            var unit = FindAtCell(cell);

            if (unit != null)
            {
                if (unit.Data.Team == TeamId.Player && unit.Data.State != UnitState.Dead)
                {
                    if (combatEditing && Selected != null && Selected != unit &&
                        Selected.Data.CommanderGroupId == unit.Data.CommanderGroupId)
                    {
                        formation.TryEditCombatSlot(Selected, cell);
                        return;
                    }
                    if (deploying && Selected != null && Selected != unit &&
                        Selected.Data.CommanderGroupId == unit.Data.CommanderGroupId)
                    {
                        formation.TryPlace(Selected, cell);
                        return;
                    }
                    Select(unit);
                }
                else if (deploying || combatEditing)
                {
                    // Formation editing ignores enemy clicks; only slot edits apply.
                }
                else
                {
                    var registry = CommanderGroupRegistry.Instance;
                    var group = registry != null ? registry.ActiveGroup : null;
                    if (group != null)
                    {
                        var targetGroup = registry.Find(unit);
                        if (targetGroup != null && ConcealmentService.IsConcealed(targetGroup))
                        {
                            ClearSelection();
                        }
                        else
                        {
                            var commands = CommanderGroupCommandController.Instance;
                            if (commands != null) commands.CommandAttack(group, unit);
                        }
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
                return;
            }

            // Ground click.
            var activeGroup = CommanderGroupRegistry.Instance != null
                ? CommanderGroupRegistry.Instance.ActiveGroup : null;
            if (combatEditing && activeGroup != null)
            {
                if (Selected != null) formation.TryEditCombatSlot(Selected, cell);
                return;
            }
            if (deploying && activeGroup != null)
            {
                if (Selected != null) formation.TryPlace(Selected, cell);
                return;
            }

            if (activeGroup != null)
            {
                var commands = CommanderGroupCommandController.Instance;
                string failureReason;
                if (commands != null && !commands.CommandMove(activeGroup, cell, out failureReason))
                {
                    var status = Mvp.Battle.BattleUiStatusText.Instance;
                    if (status != null) status.SetStatus(failureReason ?? "目标不可达");
                }
            }
            else
            {
                ClearSelection();
            }
        }

        /// <summary>Routes a left-click on a building to a capture command (§阶段B).</summary>
        void HandleBuildingClick(BuildingRuntime building)
        {
            if (building == null) return;
            var formation = Mvp.Battle.Formation.FormationController.Instance;
            bool deploying = formation != null && formation.IsDeploying;
            bool combatEditing = formation != null && formation.IsCombatEditing;
            if (deploying || combatEditing) return;

            var groups = CommanderGroupRegistry.Instance;
            var group = groups != null ? groups.ActiveGroup : null;
            if (group == null || group.IsDefeated) return;

            var status = BattleUiStatusText.Instance;

            // Already owned by our team: armories open production UI, other buildings just report status.
            bool ownedByTeam = building.Owner ==
                (group.Team == TeamId.Player ? BuildingOwner.Player : BuildingOwner.Enemy);
            if (ownedByTeam)
            {
                if (building.Type == BuildingType.Armory && group.Team == TeamId.Player)
                {
                    ArmoryProductionPanel.Show(building);
                    if (status != null) status.SetStatus("打开兵工厂");
                }
                else if (status != null) status.SetStatus("该建筑已被占领");
                return;
            }

            // Only step units (infantry / machine gunner / scout) can capture (§5.2).
            if (!CommanderGroupCommandController.HasCaptureCapableMember(group))
            {
                if (status != null) status.SetStatus("需要步系单位（步兵/机枪/侦察）才能占领建筑");
                return;
            }

            var commands = CommanderGroupCommandController.Instance;
            if (commands != null) commands.CommandCaptureBuilding(group, building);
        }

        public bool Select(UnitView unit)
        {
            if (unit == null || unit.Data == null) return false;
            if (unit.Data.Team != TeamId.Player) return false;
            if (unit.Data.State == UnitState.Dead) return false;

            var registry = CommanderGroupRegistry.Instance;
            var group = registry != null ? registry.Find(unit) : null;
            if (group == null) return false;
            var formation = Mvp.Battle.Formation.FormationController.Instance;
            bool editingSameGroup = formation != null && formation.IsCombatEditing &&
                registry.ActiveGroup == group;
            if (!editingSameGroup && !registry.Inspect(group)) return false;
            Selected = unit;
            ShowRing(unit);
            _attackRangeHiddenForMovement = IsSelectedUnitMoving(unit);
            if (!_attackRangeHiddenForMovement) ShowAttackRange(unit);
            return true;
        }

        public void ClearSelection()
        {
            if (Selected == null && _selectionRing == null && _attackRangeCells.Count == 0) return;
            Selected = null;
            _attackRangeHiddenForMovement = false;
            ReleaseRing();
            ReleaseAttackRange();
        }

        /// <summary>Called when the selected unit dies so the selection auto-clears.</summary>
        public void NotifyDeath(UnitView unit)
        {
            if (Selected == unit) ClearSelection();
        }

        void ShowRing(UnitView unit)
        {
            if (_selectionRing == null)
            {
                _selectionRing = UiPool.Instance.Get(
                    UiPoolType.SelectionRing,
                    Vector3.zero,
                    Quaternion.Euler(-90f, 0f, 0f),
                    new Vector3(1.15f, 1.15f, 1f));
            }
            if (_selectionRing == null) return;

            _selectionRing.transform.SetParent(unit.transform, false);
            _selectionRing.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            _selectionRing.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _selectionRing.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
        }

        void ReleaseRing()
        {
            if (_selectionRing == null) return;
            var pool = UiPool.Instance;
            if (pool != null) pool.Release(_selectionRing);
            _selectionRing = null;
        }

        UnitView PickUnitAtScreen(Vector2 screenPosition, Camera cam)
        {
            UnitView best = null;
            float bestDistance = UnitPickRadiusPixels;
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || unit.Data == null || unit.Data.State == UnitState.Dead)
                    continue;
                Vector3 world = unit.transform.position + Vector3.up * 0.55f;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                float distance = Vector2.Distance(screenPosition,
                    new Vector2(screen.x, screen.y));
                if (distance >= bestDistance) continue;
                best = unit;
                bestDistance = distance;
            }
            return best;
        }

        void ShowAttackRange(UnitView unit)
        {
            ReleaseAttackRange();
            if (unit == null || unit.Data == null || unit.Data.Definition == null) return;
            var grid = BattleGridController.Instance;
            var pool = UiPool.Instance;
            if (grid == null || pool == null) return;

            int range = Mathf.Max(0, Mathf.RoundToInt(unit.Data.Definition.AttackRangeMax));
            var origin = unit.Data.GridPosition;
            for (int dz = -range; dz <= range; dz++)
            {
                for (int dx = -range; dx <= range; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) > range) continue;
                    var cell = new Vector2Int(origin.x + dx, origin.y + dz);
                    if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                    var ui = pool.Get(UiPoolType.AttackRangeHighlight,
                        GridWorld(cell), Quaternion.Euler(-90f, 0f, 0f),
                        new Vector3(0.92f, 0.92f, 1f));
                    if (ui == null) continue;
                    var sr = ui.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = 84;
                    _attackRangeCells.Add(ui);
                }
            }
        }

        void RefreshAttackRangeVisibility()
        {
            if (Selected == null || Selected.Data == null) return;
            bool moving = IsSelectedUnitMoving(Selected);
            if (moving)
            {
                if (_attackRangeCells.Count > 0) ReleaseAttackRange();
                _attackRangeHiddenForMovement = true;
                return;
            }
            if (!_attackRangeHiddenForMovement) return;
            _attackRangeHiddenForMovement = false;
            ShowAttackRange(Selected);
        }

        static bool IsSelectedUnitMoving(UnitView unit)
        {
            if (unit == null || unit.Data == null) return false;
            if (unit.Data.State == UnitState.Moving ||
                unit.Data.State == UnitState.Chasing ||
                unit.Data.State == UnitState.Capturing)
                return true;
            var registry = CommanderGroupRegistry.Instance;
            var group = registry != null ? registry.Find(unit) : null;
            return group != null &&
                (group.State == CommanderGroupState.Moving ||
                 group.State == CommanderGroupState.Regrouping ||
                 group.State == CommanderGroupState.Capturing);
        }

        void ReleaseAttackRange()
        {
            var pool = UiPool.Instance;
            for (int i = 0; i < _attackRangeCells.Count; i++)
                if (_attackRangeCells[i] != null && pool != null)
                    pool.Release(_attackRangeCells[i]);
            _attackRangeCells.Clear();
        }

        static Vector3 GridWorld(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return new Vector3(cell.x, 0.025f, cell.y);
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell)) + 0.025f;
            return p;
        }

        static UnitView FirstAlive(CommanderGroupRuntime group)
        {
            if (group == null) return null;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member != null && member.Data != null && member.Data.State != UnitState.Dead)
                    return member;
            }
            return null;
        }
    }
}
