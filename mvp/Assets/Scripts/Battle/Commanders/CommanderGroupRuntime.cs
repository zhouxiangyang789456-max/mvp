using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Battle.Skills;
using Mvp.Shared;
using Mvp.Shared.Skills;
using Mvp.Battle.Formation;

namespace Mvp.Battle.Commanders
{
    public enum CommanderGroupState
    {
        Idle,
        Moving,
        Attacking,
        Regrouping,
        Capturing,
        Defeated,
        Extracted,
        /// <summary>Defensive/guard mode: units hold their formation slots and only fire
        /// at enemies that enter range; they never chase (战斗技能系统开发文档 §5).</summary>
        Holding
    }

    public enum GroupCommandType
    {
        None,
        Move,
        AttackGroup,
        Hold,
        Regroup,
        FormationDeploy,
        CaptureBuilding,
        SkillAttack,
        AttackTacticalTarget
    }

    public sealed class GroupCommand
    {
        public GroupCommandType Type;
        public Vector2Int TargetCell;
        public string TargetGroupId;
        public string TargetTacticalId;
        public int Sequence;
    }

    public sealed class CommanderGroupRuntime
    {
        public string GroupId;
        public string CommanderId;
        public int RosterIndex;
        public TeamId Team;
        public CommanderDefinition Definition;
        public readonly List<UnitView> Members = new List<UnitView>();
        public CommanderMapMarker MapMarker;
        public Vector2Int AnchorCell;
        public FormationType Formation;
        public CommanderGroupState State;

        /// <summary>8-direction formation facing, from the most recent command's click direction; defaults to +Z.</summary>
        public Vector2Int Facing = FormationFacing.Default;

        /// <summary>Instance id of the building the group is capturing; null/empty when none (阶段B).</summary>
        public string CaptureBuildingId;

        public readonly GroupCommand CurrentCommand = new GroupCommand();
        public readonly FormationLayoutSnapshot Layout = new FormationLayoutSnapshot();
        /// <summary>Skill runtime: persistent tactical mode, targeting state and per-unit
        /// skill cooldowns (战斗技能系统开发文档 §4.2).</summary>
        public readonly GroupSkillRuntime Skills = new GroupSkillRuntime();
        public bool DefeatNotified;
        public int ExtractedMemberCount;

        public int AliveMemberCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Members.Count; i++)
                {
                    var member = Members[i];
                    if (member != null && member.Data != null &&
                        member.Data.State != UnitState.Dead && member.Data.CurrentHealth > 0)
                        count++;
                }
                return count;
            }
        }

        public bool IsDefeated
        {
            get
            {
                return State == CommanderGroupState.Defeated ||
                    (AliveMemberCount == 0 && State != CommanderGroupState.Extracted);
            }
        }

        public bool IsExtracted { get { return State == CommanderGroupState.Extracted; } }

        public Vector3 CurrentWorldCenter
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < Members.Count; i++)
                {
                    var member = Members[i];
                    if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                    sum += member.transform.position;
                    count++;
                }
                return count > 0 ? sum / count : Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Group-level skill attack plan created when a special skill (远攻) is confirmed.
    /// Carries a SkillSequence so delayed/async attack results can be validated against
    /// the group's current skill state before applying (战斗技能系统开发文档 §4.2).
    /// </summary>
    public sealed class SkillAttackPlan
    {
        public string SkillId;
        public long SkillSequence;
        public GroupCommandType CommandType = GroupCommandType.SkillAttack;
        public Vector2Int TargetCell;
        public string TargetGroupId;
        /// <summary>Unit ids that participate in this skill attack (already range-checked).</summary>
        public readonly List<string> MemberIds = new List<string>();
        /// <summary>True when this is a ground/area attack (unit AreaRadius &gt; 0).</summary>
        public bool IsAreaAttack;
    }

    /// <summary>Stable unit-to-slot bindings created during deployment.</summary>
    public sealed class FormationLayoutSnapshot
    {
        public FormationType Formation;
        public Vector2Int AnchorCell;
        public readonly Dictionary<int, Vector2Int> SlotOffsets =
            new Dictionary<int, Vector2Int>();
        public readonly Dictionary<string, int> UnitSlotAssignments =
            new Dictionary<string, int>();
        public bool Locked;

        public void Capture(CommanderGroupRuntime group, Vector2Int anchor,
            IList<UnitView> members, IList<Vector2Int> worldSlots, bool locked)
        {
            Formation = group.Formation;
            AnchorCell = anchor;
            SlotOffsets.Clear();
            UnitSlotAssignments.Clear();
            for (int i = 0; i < members.Count && i < worldSlots.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null) continue;
                int slot = member.Data.FormationSlotIndex;
                SlotOffsets[slot] = worldSlots[i] - anchor;
                UnitSlotAssignments[member.Data.Id] = slot;
            }
            Locked = locked;
        }

        public bool TryGetTarget(UnitView member, Vector2Int facing, Vector2Int targetAnchor,
            out Vector2Int target)
        {
            target = targetAnchor;
            if (member == null || member.Data == null) return false;
            int slot;
            Vector2Int offset;
            if (!UnitSlotAssignments.TryGetValue(member.Data.Id, out slot)) return false;
            if (!SlotOffsets.TryGetValue(slot, out offset)) return false;
            target = targetAnchor + FormationFacing.RotateOffset(offset, facing);
            return true;
        }
    }
}
