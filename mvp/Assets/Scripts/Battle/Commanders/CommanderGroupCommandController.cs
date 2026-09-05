using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Battle.Buildings;
using Mvp.Battle.Skills;
using Mvp.Shared;
using Mvp.Shared.Skills;
using Mvp.Battle.Formation;
using Mvp.Battle.Outcome;

namespace Mvp.Battle.Commanders
{
    /// <summary>Translates one commander command into coordinated member commands.</summary>
    public sealed class CommanderGroupCommandController : MonoBehaviour
    {
        public static CommanderGroupCommandController Instance { get; private set; }
        readonly List<Vector2Int> _slots = new List<Vector2Int>();
        readonly List<UnitView> _members = new List<UnitView>();
        readonly List<Vector2Int> _pathBuffer = new List<Vector2Int>();
        readonly List<List<Vector2Int>> _validatedPaths = new List<List<Vector2Int>>(9);
        int _validatedPathCount;
        readonly List<string> _completedAttackGroups = new List<string>();
        readonly Dictionary<string, AttackPlan> _attackPlans =
            new Dictionary<string, AttackPlan>();
        public string LastMoveFailureReason { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.MediumTick += OnMediumTick;
        }

        void OnDisable()
        {
            BattleTickService.MediumTick -= OnMediumTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnMediumTick()
        {
            RefreshCompletedGroupMoves();
            if (_attackPlans.Count == 0) return;
            _completedAttackGroups.Clear();
            foreach (var pair in _attackPlans)
            {
                var registry = CommanderGroupRegistry.Instance;
                var group = registry != null ? registry.Find(pair.Key) : null;
                if (group == null || group.IsDefeated) { _completedAttackGroups.Add(pair.Key); continue; }
                var plan = pair.Value;
                if (plan.Sequence != group.CurrentCommand.Sequence) { _completedAttackGroups.Add(pair.Key); continue; }
                if (plan.Started) continue;
                if (!HasValidAttackTarget(plan))
                {
                    AbortAttackPlan(group);
                    _completedAttackGroups.Add(pair.Key);
                    continue;
                }
                if (plan.PrimaryTarget == null || plan.PrimaryTarget.Data == null ||
                    plan.PrimaryTarget.Data.State == UnitState.Dead)
                    plan.PrimaryTarget = FindNearestEnemy(group, plan.TargetGroup);
                if (!AtLockedSlots(group, plan.Anchor)) continue;
                ReleaseReservation(group);
                BeginStationaryAttack(group, plan.TargetGroup, plan.PrimaryTarget);
                plan.Started = true;
            }
            for (int i = 0; i < _completedAttackGroups.Count; i++)
                _attackPlans.Remove(_completedAttackGroups[i]);
        }

        void RefreshCompletedGroupMoves()
        {
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            var combat = UnitCombatController.Instance;
            for (int i = 0; i < registry.Groups.Count; i++)
            {
                var group = registry.Groups[i];
                if (group == null || group.IsDefeated) continue;
                if (group.State == CommanderGroupState.Capturing)
                {
                    RefreshCapturingGroup(group);
                    continue;
                }
                if (group.State == CommanderGroupState.Moving ||
                    group.State == CommanderGroupState.Regrouping)
                {
                    if (!AtLockedSlots(group, group.AnchorCell)) continue;
                    ReleaseReservation(group);
                    if (group.Skills.PersistentMode == PersistentSkillMode.Guard)
                    {
                        // 防御/警戒: once the group reaches its locked slots it enters
                        // Holding and only fires at enemies in range (never chases).
                        group.State = CommanderGroupState.Holding;
                        group.CurrentCommand.Type = GroupCommandType.Hold;
                        continue;
                    }
                    group.State = CommanderGroupState.Idle;
                    group.CurrentCommand.Type = GroupCommandType.None;
                    continue;
                }
                if (group.State != CommanderGroupState.Attacking) continue;
                AttackPlan plan;
                if (_attackPlans.TryGetValue(group.GroupId, out plan))
                {
                    if (!HasValidAttackTarget(plan))
                    {
                        AbortAttackPlan(group);
                        _attackPlans.Remove(group.GroupId);
                    }
                    else if (plan.Started && !GroupHasEngagedMember(group, combat))
                    {
                        plan.PrimaryTarget = FindNearestEnemy(group, plan.TargetGroup);
                        if (plan.PrimaryTarget != null) BeginStationaryAttack(group, plan.TargetGroup, plan.PrimaryTarget);
                    }
                    continue;
                }
                if (GroupHasEngagedMember(group, combat)) continue;
                var targetGroup = registry != null ? registry.Find(group.CurrentCommand.TargetGroupId) : null;
                var nextTarget = FindNearestEnemy(group, targetGroup);
                if (nextTarget != null)
                {
                    BeginStationaryAttack(group, targetGroup, nextTarget);
                    continue;
                }
                AbortAttackPlan(group);
            }
        }

        /// <summary>
        /// Quantizes the clicked direction into the group's 8-direction facing.
        /// Zero / in-place clicks keep the previous facing (§3.3).
        /// </summary>
        static void ApplyFacing(CommanderGroupRuntime group, Vector2Int dir)
        {
            var q = FormationFacing.Quantize(dir);
            if (q != Vector2Int.zero) group.Facing = q;
        }

        public bool CommandMove(CommanderGroupRuntime group, Vector2Int targetAnchor)
        {
            LastMoveFailureReason = null;
            if (BattleSimulationState.IsFrozen) return false;
            var grid = BattleGridController.Instance;
            var movement = UnitMovementController.Instance;
            if (group == null || grid == null || movement == null || group.IsDefeated) return false;

            InterruptPersistentModes(group);
            _attackPlans.Remove(group.GroupId);
            group.CaptureBuildingId = null;
            ApplyFacing(group, targetAnchor - group.AnchorCell);
            EnsureLockedLayout(group);
            if (!IssueLockedMove(group, targetAnchor))
            {
                CollectAlive(group, _members);
                FlashGroup(_members);
                if (string.IsNullOrEmpty(LastMoveFailureReason))
                    LastMoveFailureReason = "阵型受阻";
                return false;
            }

            group.AnchorCell = targetAnchor;
            group.State = CommanderGroupState.Moving;
            group.CurrentCommand.Type = GroupCommandType.Move;
            group.CurrentCommand.TargetCell = targetAnchor;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.Sequence++;
            return true;
        }

        public bool CommandMove(CommanderGroupRuntime group, Vector2Int targetAnchor,
            out string failureReason)
        {
            bool accepted = CommandMove(group, targetAnchor);
            failureReason = accepted ? null :
                (string.IsNullOrEmpty(LastMoveFailureReason) ? "目标不可达" : LastMoveFailureReason);
            return accepted;
        }

        /// <summary>
        /// 防御/警戒 (skill_guard): the group keeps its locked formation, members return
        /// to their locked slots, then the group enters Holding. Holding groups only fire
        /// at enemies that enter range — they never chase (the auto-attack tick lives in
        /// GroupSkillController). GroupSkillController sets PersistentMode = Guard before
        /// calling this; the Regrouping→Holding transition happens in RefreshCompletedGroupMoves.
        /// </summary>
        public bool CommandHold(CommanderGroupRuntime group)
        {
            if (BattleSimulationState.IsFrozen) return false;
            var grid = BattleGridController.Instance;
            if (group == null || grid == null || group.IsDefeated) return false;

            _attackPlans.Remove(group.GroupId);
            group.CaptureBuildingId = null;
            EnsureLockedLayout(group);
            if (!IssueLockedMove(group, group.AnchorCell))
            {
                CollectAlive(group, _members);
                FlashGroup(_members);
                return false;
            }
            group.State = CommanderGroupState.Regrouping;
            group.CurrentCommand.Type = GroupCommandType.Hold;
            group.CurrentCommand.TargetCell = group.AnchorCell;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.Sequence++;
            return true;
        }

        /// <summary>
        /// Clears the active persistent tactical mode (战斗技能系统开发文档 §3.1). Called
        /// before any command that conflicts with a persistent mode (move / attack /
        /// regroup / capture). Uses ResetModes so the skill sequence bumps and any
        /// in-flight skill targeting/result is invalidated.
        /// </summary>
        public void InterruptPersistentModes(CommanderGroupRuntime group)
        {
            if (group == null || group.Skills.PersistentMode == PersistentSkillMode.None) return;
            var previous = group.Skills.PersistentMode;
            group.Skills.ResetModes();
            if (previous == PersistentSkillMode.Concealment)
                ConcealmentService.EndConcealment(group);
            if (previous == PersistentSkillMode.Guard && group.State == CommanderGroupState.Holding)
                group.State = CommanderGroupState.Idle;
        }

        /// <summary>
        /// Executes a confirmed special-skill attack plan (远攻). Validates the skill
        /// sequence so a stale plan can never fire after a newer command. Area units
        /// (AreaRadius&gt;0) fire at the ground cell; single-target units attack the enemy
        /// standing on the cell. Members get per-unit attacks and never create a group
        /// AttackPlan, so the group command sequence stays intact.
        /// </summary>
        public bool CommandSkillAttack(CommanderGroupRuntime group, SkillAttackPlan plan)
        {
            if (BattleSimulationState.IsFrozen) return false;
            if (group == null || plan == null || group.IsDefeated) return false;
            if (group.Skills.SkillSequence != plan.SkillSequence) return false;

            var combat = UnitCombatController.Instance;
            if (combat == null) return false;
            var selection = UnitSelectionController.Instance;
            UnitView enemyOnCell = !plan.IsAreaAttack && selection != null
                ? selection.FindAtCell(plan.TargetCell) : null;

            bool any = false;
            for (int i = 0; i < plan.MemberIds.Count; i++)
            {
                UnitView member = FindMember(group, plan.MemberIds[i]);
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                if (plan.IsAreaAttack)
                {
                    if (member.Data.Definition != null &&
                        member.Data.Definition.AreaRadius > 0f &&
                        combat.FireAtCell(member, plan.TargetCell)) any = true;
                    continue;
                }
                if (enemyOnCell != null && enemyOnCell.Data != null &&
                    enemyOnCell.Data.Team != member.Data.Team &&
                    enemyOnCell.Data.State != UnitState.Dead &&
                    combat.CommandSkillAttack(member, enemyOnCell)) any = true;
            }
            if (!any) return false;

            group.CurrentCommand.Sequence++;
            group.CurrentCommand.Type = GroupCommandType.SkillAttack;
            group.CurrentCommand.TargetCell = plan.TargetCell;
            group.CurrentCommand.TargetGroupId = plan.TargetGroupId;
            return true;
        }

        static UnitView FindMember(CommanderGroupRuntime group, string unitId)
        {
            if (group == null || string.IsNullOrEmpty(unitId)) return null;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member != null && member.Data != null && member.Data.Id == unitId)
                    return member;
            }
            return null;
        }

        /// <summary>Explicitly rebuilds slot assignments; this is the only combat action that may fill gaps.</summary>
        public bool CommandRegroup(CommanderGroupRuntime group, FormationType formation)
        {
            if (BattleSimulationState.IsFrozen) return false;
            var grid = BattleGridController.Instance;
            if (group == null || grid == null || group.IsDefeated) return false;

            InterruptPersistentModes(group);
            _attackPlans.Remove(group.GroupId);
            group.CaptureBuildingId = null;
            CollectAlive(group, _members);
            if (_members.Count == 0) return false;
            ComputeSlots(formation, _members.Count, group.AnchorCell, _slots);
            if (!PrepareReservation(group, _slots) ||
                !ValidateSlots(group, grid, _slots) || !CanReachAll(_members, _slots, grid))
            {
                RollbackReservation(group);
                FlashGroup(_members);
                return false;
            }

            if (!DispatchMoves(group, _members, _slots, grid, group.Facing))
            {
                RollbackReservation(group);
                return false;
            }
            for (int i = 0; i < _members.Count; i++)
                _members[i].Data.FormationSlotIndex = i;
            group.Formation = formation;
            group.Layout.Capture(group, group.AnchorCell, _members, _slots, true);
            CommitReservation(group);

            group.State = CommanderGroupState.Regrouping;
            group.CurrentCommand.Type = GroupCommandType.Regroup;
            group.CurrentCommand.TargetCell = group.AnchorCell;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.Sequence++;
            return true;
        }

        /// <summary>Commits a player-edited 3x3 layout as one atomic regroup command.</summary>
        public bool CommandCustomRegroup(CommanderGroupRuntime group,
            IDictionary<string, int> assignments, FormationType formation)
        {
            var grid = BattleGridController.Instance;
            if (group == null || assignments == null || grid == null || group.IsDefeated ||
                group.State != CommanderGroupState.Idle) return false;

            InterruptPersistentModes(group);
            _attackPlans.Remove(group.GroupId);
            group.CaptureBuildingId = null;
            CollectAlive(group, _members);
            _slots.Clear();
            var usedSlots = new HashSet<int>();
            for (int i = 0; i < _members.Count; i++)
            {
                int slot;
                if (!assignments.TryGetValue(_members[i].Data.Id, out slot) ||
                    slot < 0 || slot >= 9 || !usedSlots.Add(slot))
                    return false;
                _slots.Add(group.AnchorCell + SlotOffset3x3(slot));
            }

            if (!PrepareReservation(group, _slots) ||
                !ValidateSlots(group, grid, _slots) || !CanReachAll(_members, _slots, grid))
            {
                RollbackReservation(group);
                FlashGroup(_members);
                return false;
            }

            if (!DispatchMoves(group, _members, _slots, grid, group.Facing))
            {
                RollbackReservation(group);
                return false;
            }
            for (int i = 0; i < _members.Count; i++)
                _members[i].Data.FormationSlotIndex = assignments[_members[i].Data.Id];
            group.Formation = formation;
            group.Layout.Capture(group, group.AnchorCell, _members, _slots, true);
            CommitReservation(group);

            group.State = CommanderGroupState.Regrouping;
            group.CurrentCommand.Type = GroupCommandType.Regroup;
            group.CurrentCommand.TargetCell = group.AnchorCell;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.Sequence++;
            return true;
        }

        static Vector2Int SlotOffset3x3(int slot)
        {
            return new Vector2Int((slot % 3) - 1, (slot / 3) - 1);
        }

        public bool CommandAttack(CommanderGroupRuntime group, UnitView clickedTarget)
        {
            return CommandAttack(group, clickedTarget, true);
        }

        /// <summary>Orders the complete formation to attack an independent tactical target.</summary>
        public bool CommandAttackTacticalTarget(CommanderGroupRuntime group, UnitView target,
            string tacticalId)
        {
            if (string.IsNullOrEmpty(tacticalId)) return false;
            if (!CommandAttack(group, target, true)) return false;
            group.CurrentCommand.Type = GroupCommandType.AttackTacticalTarget;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.TargetTacticalId = tacticalId;
            return true;
        }

        public bool CommandAttack(CommanderGroupRuntime group, UnitView clickedTarget,
            bool moveIntoRange)
        {
            if (BattleSimulationState.IsFrozen) return false;
            var registry = CommanderGroupRegistry.Instance;
            if (group == null || clickedTarget == null || clickedTarget.Data == null ||
                group.IsDefeated) return false;

            InterruptPersistentModes(group);
            SprintEffectService.NotifyAttack(group, Time.time);
            var targetGroup = registry != null ? registry.Find(clickedTarget) : null;
            group.CaptureBuildingId = null;
            ApplyFacing(group, clickedTarget.Data.GridPosition - group.AnchorCell);
            EnsureLockedLayout(group);
            Vector2Int attackAnchor = group.AnchorCell;
            bool started = true;
            if (moveIntoRange)
            {
                if (!FindCombatAnchor(group, targetGroup, clickedTarget, out attackAnchor))
                {
                    CollectAlive(group, _members);
                    FlashGroup(_members);
                    return false;
                }

                ApplyFacing(group, clickedTarget.Data.GridPosition - attackAnchor);
                if (!IssueLockedMove(group, attackAnchor)) return false;
                started = false;
            }

            group.State = CommanderGroupState.Attacking;
            group.CurrentCommand.Type = GroupCommandType.AttackGroup;
            group.CurrentCommand.TargetGroupId = targetGroup != null ? targetGroup.GroupId : null;
            group.CurrentCommand.TargetTacticalId = null;
            group.CurrentCommand.Sequence++;
            _attackPlans[group.GroupId] = new AttackPlan
            {
                Anchor = attackAnchor,
                TargetGroup = targetGroup,
                PrimaryTarget = clickedTarget,
                Sequence = group.CurrentCommand.Sequence,
                Started = started
            };
            if (started)
            {
                ReleaseReservation(group);
                FaceGroupImmediately(group);
                BeginStationaryAttack(group, targetGroup, clickedTarget);
            }
            return true;
        }

        static void FaceGroupImmediately(CommanderGroupRuntime group)
        {
            if (group == null || group.Facing == Vector2Int.zero) return;
            Vector3 direction = FormationFacing.WorldDirection(group.Facing);
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead) continue;
                member.SetFacingDirection(direction);
            }
        }
        /// <summary>
        /// Issues a capture command: up to 4 capture-capable members move to free
        /// cells on the building's adjacent ring.
        /// </summary>
        public bool CommandCaptureBuilding(CommanderGroupRuntime group, BuildingRuntime building)
        {
            if (BattleSimulationState.IsFrozen) return false;
            var grid = BattleGridController.Instance;
            var movement = UnitMovementController.Instance;
            if (group == null || building == null || grid == null || movement == null ||
                group.IsDefeated) return false;

            InterruptPersistentModes(group);
            _attackPlans.Remove(group.GroupId);
            group.CaptureBuildingId = null;
            ApplyFacing(group, building.AnchorCell - group.AnchorCell);

            CollectAlive(group, _members);
            if (_members.Count == 0) return false;

            var capturers = new List<UnitView>();
            for (int i = 0; i < _members.Count && capturers.Count < 4; i++)
            {
                var member = _members[i];
                if (member == null || member.Data == null || member.Data.Definition == null) continue;
                if ((member.Data.Definition.Tags & UnitTag.CanCaptureBuilding) == 0) continue;
                capturers.Add(member);
            }
            if (capturers.Count == 0) return false;

            var ring = new List<Vector2Int>();
            BuildingCaptureController.BuildAdjacentRing(building, ring);
            var reservations = FormationReservationService.Instance;
            _slots.Clear();
            for (int i = 0; i < ring.Count; i++)
            {
                var cell = ring[i];
                if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                if (grid.IsOccupied(cell)) continue;
                if (reservations != null &&
                    reservations.IsReservedByOther(group.GroupId, cell)) continue;
                _slots.Add(cell);
            }
            if (_slots.Count == 0) return false;

            _members.Clear();
            for (int i = 0; i < capturers.Count && i < _slots.Count; i++)
                _members.Add(capturers[i]);
            if (_slots.Count > _members.Count)
                _slots.RemoveRange(_members.Count, _slots.Count - _members.Count);
            if (_members.Count == 0) return false;

            if (!PrepareReservation(group, _slots) ||
                !ValidateSlots(group, grid, _slots) || !CanReachAll(_members, _slots, grid))
            {
                RollbackReservation(group);
                FlashGroup(_members);
                return false;
            }
            if (!DispatchMoves(group, _members, _slots, grid, group.Facing))
            {
                RollbackReservation(group);
                return false;
            }
            CommitReservation(group);

            group.State = CommanderGroupState.Capturing;
            group.CaptureBuildingId = building.InstanceId.ToString();
            group.CurrentCommand.Type = GroupCommandType.CaptureBuilding;
            group.CurrentCommand.TargetCell = building.AnchorCell;
            group.CurrentCommand.TargetGroupId = null;
            group.CurrentCommand.Sequence++;
            return true;
        }

        /// <summary>True when the group has at least one alive capture-capable member (§5.2).</summary>
        public static bool HasCaptureCapableMember(CommanderGroupRuntime group)
        {
            if (group == null) return false;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                if (member.Data.Definition == null) continue;
                if ((member.Data.Definition.Tags & UnitTag.CanCaptureBuilding) != 0) return true;
            }
            return false;
        }

        public void CancelGroupCommand(CommanderGroupRuntime group)
        {
            if (group == null) return;
            group.CurrentCommand.Sequence++;
            group.CurrentCommand.Type = GroupCommandType.None;
            group.CurrentCommand.TargetGroupId = null;
            group.CaptureBuildingId = null;
            _attackPlans.Remove(group.GroupId);

            var movement = UnitMovementController.Instance;
            var combat = UnitCombatController.Instance;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null) continue;
                if (movement != null) movement.CancelMove(member);
                if (combat != null) combat.CancelCombat(member);
            }

            ReleaseReservation(group);
            if (!group.IsDefeated) group.State = CommanderGroupState.Idle;
        }

        bool IssueLockedMove(CommanderGroupRuntime group, Vector2Int targetAnchor)
        {
            var grid = BattleGridController.Instance;
            var movement = UnitMovementController.Instance;
            if (grid == null || movement == null) return false;
            CollectAlive(group, _members);
            BuildLockedTargets(group, targetAnchor, _members, _slots);
            if (!PrepareReservation(group, _slots))
            {
                LastMoveFailureReason = "区域不足";
                return false;
            }
            if (!ValidateSlots(group, grid, _slots))
            {
                LastMoveFailureReason = "阵型受阻";
                RollbackReservation(group);
                return false;
            }
            if (!CanReachAll(_members, _slots, grid))
            {
                LastMoveFailureReason = "目标不可达";
                RollbackReservation(group);
                return false;
            }
            if (!DispatchMoves(group, _members, _slots, grid, group.Facing))
            {
                LastMoveFailureReason = "目标不可达";
                RollbackReservation(group);
                return false;
            }
            CommitReservation(group);
            group.AnchorCell = targetAnchor;
            return true;
        }

        bool CanReachAll(List<UnitView> members, List<Vector2Int> targets,
            BattleGridController grid)
        {
            var movement = UnitMovementController.Instance;
            if (movement == null || movement.Pathfinder == null) return false;
            _validatedPathCount = 0;
            for (int i = 0; i < _validatedPaths.Count; i++) _validatedPaths[i].Clear();
            ClearGroupOccupancy(members, grid, false);
            bool reachable = true;
            for (int i = 0; i < members.Count; i++)
            {
                if (_validatedPaths.Count <= i)
                    _validatedPaths.Add(new List<Vector2Int>(32));
                var path = _validatedPaths[i];
                _validatedPathCount++;
                if (members[i].Data.GridPosition == targets[i])
                {
                    path.Add(members[i].Data.GridPosition);
                    continue;
                }
                if (!movement.Pathfinder.FindPath(members[i].Data.GridPosition,
                    targets[i], _pathBuffer, false))
                {
                    reachable = false;
                    break;
                }
                path.AddRange(_pathBuffer);
            }
            ClearGroupOccupancy(members, grid, true);
            return reachable;
        }

        bool DispatchMoves(CommanderGroupRuntime group, List<UnitView> members,
            List<Vector2Int> targets, BattleGridController grid, Vector2Int facing)
        {
            var movement = UnitMovementController.Instance;
            if (movement == null) return false;
            ClearGroupOccupancy(members, grid, false);
            float groupSpeed = ComputeGroupMoveSpeed(group, members);
            int accepted = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (i < _validatedPathCount && movement.CommandMoveWithPath(
                    members[i], targets[i], groupSpeed, facing, _validatedPaths[i])) accepted++;
                else break;
            }
            if (accepted != members.Count)
                for (int i = 0; i < accepted; i++) movement.CancelMove(members[i]);
            ClearGroupOccupancy(members, grid, true);
            return accepted == members.Count;
        }

        void BeginStationaryAttack(CommanderGroupRuntime group,
            CommanderGroupRuntime targetGroup, UnitView primaryTarget)
        {
            var combat = UnitCombatController.Instance;
            if (combat == null) return;
            CollectAlive(group, _members);
            for (int i = 0; i < _members.Count; i++)
            {
                UnitView target = IsAliveEnemy(primaryTarget, _members[i])
                    ? primaryTarget
                    : targetGroup != null
                        ? FindNearestEnemy(_members[i], targetGroup.Members)
                        : null;
                if (target != null) combat.CommandAttack(_members[i], target, false);
            }
        }

        static bool IsAliveEnemy(UnitView target, UnitView attacker)
        {
            return target != null && attacker != null &&
                target.Data != null && attacker.Data != null &&
                target.Data.State != UnitState.Dead &&
                target.Data.Team != attacker.Data.Team;
        }

        static bool HasValidAttackTarget(AttackPlan plan)
        {
            if (plan.TargetGroup != null) return !plan.TargetGroup.IsDefeated;
            return plan.PrimaryTarget != null && plan.PrimaryTarget.Data != null &&
                plan.PrimaryTarget.Data.State != UnitState.Dead;
        }

        static void AbortAttackPlan(CommanderGroupRuntime group)
        {
            group.CurrentCommand.Sequence++;
            group.CurrentCommand.Type = GroupCommandType.None;
            group.CurrentCommand.TargetGroupId = null;
            group.CaptureBuildingId = null;
            var movement = UnitMovementController.Instance;
            var combat = UnitCombatController.Instance;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null) continue;
                if (movement != null) movement.CancelMove(member);
                if (combat != null) combat.CancelCombat(member);
            }
            ReleaseReservation(group);
            if (!group.IsDefeated) group.State = CommanderGroupState.Idle;
        }

        void RefreshCapturingGroup(CommanderGroupRuntime group)
        {
            BuildingRuntime building = null;
            if (!string.IsNullOrEmpty(group.CaptureBuildingId))
            {
                var registry = BuildingRegistry.Instance;
                if (registry != null)
                {
                    int id;
                    if (int.TryParse(group.CaptureBuildingId, out id))
                        building = registry.GetByInstanceId(id);
                }
            }

            if (building == null)
            {
                // Building vanished (battle reset / map swap): abandon the capture.
                group.CaptureBuildingId = null;
                ReleaseReservation(group);
                group.State = CommanderGroupState.Idle;
                group.CurrentCommand.Type = GroupCommandType.None;
                group.CurrentCommand.Sequence++;
                return;
            }

            // Ownership flipped to our team → capture complete; return to formation.
            bool ownedByTeam = building.Owner ==
                (group.Team == TeamId.Player ? BuildingOwner.Player : BuildingOwner.Enemy);
            if (ownedByTeam)
            {
                EndCaptureAndRegroup(group);
                return;
            }

            // No alive capture-capable member left → abandon.
            if (!HasCaptureCapableMember(group))
            {
                group.CaptureBuildingId = null;
                ReleaseReservation(group);
                group.State = CommanderGroupState.Idle;
                group.CurrentCommand.Type = GroupCommandType.None;
                group.CurrentCommand.Sequence++;
            }
            // Otherwise keep capturing; units remain on the adjacent ring while
            // BuildingCaptureController accumulates progress.
        }

        /// <summary>
        /// Capture finished for our team: release the ring reservation and re-issue a move
        /// back to the formation anchor (reuses the existing Regrouping → Idle path).
        /// </summary>
        void EndCaptureAndRegroup(CommanderGroupRuntime group)
        {
            group.CaptureBuildingId = null;
            group.CurrentCommand.Type = GroupCommandType.None;
            ReleaseReservation(group);
            EnsureLockedLayout(group);
            if (IssueLockedMove(group, group.AnchorCell))
            {
                group.State = CommanderGroupState.Regrouping;
                group.CurrentCommand.Type = GroupCommandType.Regroup;
                group.CurrentCommand.TargetCell = group.AnchorCell;
                group.CurrentCommand.TargetGroupId = null;
                group.CurrentCommand.Sequence++;
            }
            else
            {
                group.State = CommanderGroupState.Idle;
                group.CurrentCommand.Type = GroupCommandType.None;
                group.CurrentCommand.Sequence++;
            }
        }

        bool FindCombatAnchor(CommanderGroupRuntime group,
            CommanderGroupRuntime targetGroup, UnitView primary, out Vector2Int anchor)
        {
            anchor = group.AnchorCell;
            if (primary == null || primary.Data == null) return false;

            var grid = BattleGridController.Instance;
            CollectAlive(group, _members);
            if (_members.Count == 0 || grid == null) return false;

            Vector2Int targetCell = primary.Data.GridPosition;
            BuildLockedTargets(group, group.AnchorCell, _members, _slots);
            if (AnySlotCoversTarget(_members, _slots, targetCell))
                return true;

            int maxRange = MaxAttackRange(_members);
            if (maxRange <= 0) return false;

            for (int radius = maxRange; radius >= 1; radius--)
            {
                int bestScore = int.MaxValue;
                bool foundAtRadius = false;
                for (int y = targetCell.y - radius; y <= targetCell.y + radius; y++)
                for (int x = targetCell.x - radius; x <= targetCell.x + radius; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - targetCell.x), Mathf.Abs(y - targetCell.y)) != radius) continue;
                    var candidate = new Vector2Int(x, y);
                    BuildLockedTargets(group, candidate, _members, _slots);
                    if (!AnySlotCoversTarget(_members, _slots, targetCell)) continue;
                    if (!ValidateSlots(group, grid, _slots)) continue;
                    int score = Mathf.Abs(candidate.x - group.AnchorCell.x) +
                        Mathf.Abs(candidate.y - group.AnchorCell.y);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    anchor = candidate;
                    foundAtRadius = true;
                }
                if (foundAtRadius) return true;
            }
            return false;
        }

        static int MaxAttackRange(List<UnitView> members)
        {
            int range = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead ||
                    member.Data.Definition == null) continue;
                range = Mathf.Max(range,
                    Mathf.RoundToInt(member.Data.Definition.AttackRangeMax));
            }
            return range;
        }

        static bool AnySlotCoversTarget(List<UnitView> members,
            List<Vector2Int> slots, Vector2Int targetCell)
        {
            for (int i = 0; i < members.Count && i < slots.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead ||
                    member.Data.Definition == null) continue;
                int range = Mathf.RoundToInt(member.Data.Definition.AttackRangeMax);
                int dx = Mathf.Abs(slots[i].x - targetCell.x);
                int dz = Mathf.Abs(slots[i].y - targetCell.y);
                if (Mathf.Max(dx, dz) <= range) return true;
            }
            return false;
        }

        static void EnsureLockedLayout(CommanderGroupRuntime group)
        {
            if (group.Layout.Locked) return;
            var members = new List<UnitView>();
            var slots = new List<Vector2Int>();
            CollectAlive(group, members);
            for (int i = 0; i < members.Count; i++)
            {
                members[i].Data.FormationSlotIndex = i;
                slots.Add(members[i].Data.GridPosition);
            }
            group.Layout.Capture(group, group.AnchorCell, members, slots, true);
        }

        static void BuildLockedTargets(CommanderGroupRuntime group, Vector2Int anchor,
            List<UnitView> members, List<Vector2Int> targets)
        {
            targets.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                Vector2Int target;
                if (!group.Layout.TryGetTarget(members[i], group.Facing, anchor, out target))
                    target = anchor;
                targets.Add(target);
            }
        }

        public static bool AtLockedSlots(CommanderGroupRuntime group, Vector2Int anchor)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                Vector2Int target;
                if (!group.Layout.TryGetTarget(member, group.Facing, anchor, out target)) continue;
                if (member.Data.GridPosition != target || member.Data.State == UnitState.Moving) return false;
            }
            return true;
        }

        static void ClearGroupOccupancy(List<UnitView> members, BattleGridController grid, bool occupied)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && members[i].Data != null)
                    grid.SetOccupied(members[i].Data.GridPosition, occupied);
        }

        static bool ValidateSlots(CommanderGroupRuntime group, BattleGridController grid,
            List<Vector2Int> slots)
        {
            var selection = UnitSelectionController.Instance;
            var uniqueSlots = new HashSet<Vector2Int>();
            for (int i = 0; i < slots.Count; i++)
            {
                var cell = slots[i];
                if (!uniqueSlots.Add(cell)) return false;
                if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) return false;
                var reservations = FormationReservationService.Instance;
                if (reservations != null && reservations.IsReservedByOther(group.GroupId, cell))
                    return false;
                if (!grid.IsOccupied(cell)) continue;
                var occupant = selection != null ? selection.FindAtCell(cell) : null;
                if (occupant == null || occupant.Data == null ||
                    occupant.Data.CommanderGroupId != group.GroupId) return false;
            }
            return true;
        }

        static bool PrepareReservation(CommanderGroupRuntime group,
            IReadOnlyList<Vector2Int> slots)
        {
            var reservations = FormationReservationService.Instance;
            return reservations == null || reservations.TryReserveCandidate(group.GroupId, slots);
        }

        static void CommitReservation(CommanderGroupRuntime group)
        {
            var reservations = FormationReservationService.Instance;
            if (reservations != null) reservations.Commit(group.GroupId);
        }

        static void RollbackReservation(CommanderGroupRuntime group)
        {
            var reservations = FormationReservationService.Instance;
            if (reservations != null) reservations.Rollback(group.GroupId);
        }

        static void ReleaseReservation(CommanderGroupRuntime group)
        {
            var reservations = FormationReservationService.Instance;
            if (reservations != null) reservations.Release(group.GroupId);
        }

        public static void ComputeSlots(FormationType formation, int count,
            Vector2Int anchor, List<Vector2Int> output)
        {
            output.Clear();
            for (int i = 0; i < count; i++)
            {
                switch (formation)
                {
                    case FormationType.Vertical:
                        output.Add(new Vector2Int(anchor.x, anchor.y + i));
                        break;
                    case FormationType.Horizontal:
                        output.Add(new Vector2Int(anchor.x + i, anchor.y));
                        break;
                    default:
                        int side = Mathf.CeilToInt(Mathf.Sqrt(count));
                        output.Add(new Vector2Int(anchor.x + (i % side), anchor.y + (i / side)));
                        break;
                }
            }
        }

        static void CollectAlive(CommanderGroupRuntime group, List<UnitView> output)
        {
            output.Clear();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member != null && member.Data != null && member.Data.State != UnitState.Dead)
                    output.Add(member);
            }
            output.Sort((a, b) => a.Data.SpawnOrder.CompareTo(b.Data.SpawnOrder));
        }

        static UnitView FindNearestEnemy(UnitView from, List<UnitView> candidates)
        {
            UnitView best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.Data == null || candidate.Data.State == UnitState.Dead) continue;
                int distance = Mathf.Abs(from.Data.GridPosition.x - candidate.Data.GridPosition.x) +
                    Mathf.Abs(from.Data.GridPosition.y - candidate.Data.GridPosition.y);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        static float ComputeGroupMoveSpeed(CommanderGroupRuntime group, List<UnitView> members)
        {
            float speed = float.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null || member.Data.Definition == null) continue;
                float memberSpeed = member.Data.Definition.MoveSpeed *
                    Mvp.Battle.Traits.TraitEffectService.GetMoveSpeedMultiplier(member.Data) *
                    SprintEffectService.GetMoveSpeedMultiplier(group, member.Data, Time.time);
                if (memberSpeed < speed) speed = memberSpeed;
            }
            return speed == float.MaxValue ? 0f : Mathf.Max(0.01f, speed);
        }

        static UnitView FindNearestEnemy(CommanderGroupRuntime group, CommanderGroupRuntime targetGroup)
        {
            if (group == null || targetGroup == null) return null;
            UnitView best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                var target = FindNearestEnemy(member, targetGroup.Members);
                if (target == null || target.Data == null) continue;
                int distance = Mathf.Abs(member.Data.GridPosition.x - target.Data.GridPosition.x) +
                    Mathf.Abs(member.Data.GridPosition.y - target.Data.GridPosition.y);
                if (distance >= bestDistance) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        static bool GroupHasEngagedMember(CommanderGroupRuntime group, UnitCombatController combat)
        {
            if (group == null || combat == null) return false;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member != null && member.Data != null && member.Data.State != UnitState.Dead &&
                    combat.IsEngaged(member))
                    return true;
            }
            return false;
        }

        static void FlashGroup(List<UnitView> members)
        {
            for (int i = 0; i < members.Count; i++) members[i].FlashInvalid();
        }

        sealed class AttackPlan
        {
            public Vector2Int Anchor;
            public CommanderGroupRuntime TargetGroup;
            public UnitView PrimaryTarget;
            public int Sequence;
            public bool Started;
        }
    }
}
