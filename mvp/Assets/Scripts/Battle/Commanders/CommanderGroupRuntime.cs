using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Battle.Formation;

namespace Mvp.Battle.Commanders
{
    public enum CommanderGroupState
    {
        Idle,
        Moving,
        Attacking,
        Regrouping,
        Defeated
    }

    public enum GroupCommandType
    {
        None,
        Move,
        AttackGroup,
        Hold,
        Regroup,
        FormationDeploy
    }

    public sealed class GroupCommand
    {
        public GroupCommandType Type;
        public Vector2Int TargetCell;
        public string TargetGroupId;
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
        public readonly GroupCommand CurrentCommand = new GroupCommand();
        public readonly FormationLayoutSnapshot Layout = new FormationLayoutSnapshot();
        public bool DefeatNotified;

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
                return AliveMemberCount == 0;
            }
        }

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

        public bool TryGetTarget(UnitView member, Vector2Int targetAnchor,
            out Vector2Int target)
        {
            target = targetAnchor;
            if (member == null || member.Data == null) return false;
            int slot;
            Vector2Int offset;
            if (!UnitSlotAssignments.TryGetValue(member.Data.Id, out slot)) return false;
            if (!SlotOffsets.TryGetValue(slot, out offset)) return false;
            target = targetAnchor + offset;
            return true;
        }
    }
}
