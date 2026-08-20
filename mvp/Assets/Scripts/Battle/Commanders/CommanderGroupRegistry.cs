using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Battle.Vision;

namespace Mvp.Battle.Commanders
{
    public sealed class CommanderGroupRegistry : MonoBehaviour
    {
        public static CommanderGroupRegistry Instance { get; private set; }
        public event Action<CommanderGroupRuntime> ActiveGroupChanged;
        public event Action<CommanderGroupRuntime> CommanderInspected;
        public event Action CommanderInspectionClosed;
        public event Action<CommanderGroupRuntime> GroupRegistered;
        public event Action<CommanderGroupRuntime> GroupDefeated;

        readonly List<CommanderGroupRuntime> _groups = new List<CommanderGroupRuntime>();
        public IReadOnlyList<CommanderGroupRuntime> Groups { get { return _groups; } }
        public CommanderGroupRuntime ActiveGroup { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(CommanderGroupRuntime group, bool createMarker)
        {
            if (group == null || string.IsNullOrEmpty(group.GroupId) || _groups.Contains(group)) return;
            _groups.Add(group);
            if (createMarker && group.Definition != null)
                group.MapMarker = CommanderMapMarker.Create(group, transform);
            if (GroupRegistered != null) GroupRegistered(group);
        }

        public CommanderGroupRuntime Find(string groupId)
        {
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i].GroupId == groupId) return _groups[i];
            return null;
        }

        public CommanderGroupRuntime Find(UnitView unit)
        {
            return unit != null && unit.Data != null ? Find(unit.Data.CommanderGroupId) : null;
        }

        public bool SetActive(CommanderGroupRuntime group)
        {
            if (group == null || group.Team != TeamId.Player || group.IsDefeated) return false;
            if (ActiveGroup == group) return true;
            ActiveGroup = group;
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i].MapMarker != null) _groups[i].MapMarker.SetSelected(_groups[i] == group);
            if (ActiveGroupChanged != null) ActiveGroupChanged(group);
            return true;
        }

        public bool Inspect(CommanderGroupRuntime group)
        {
            if (!SetActive(group)) return false;
            if (CommanderInspected != null) CommanderInspected(group);
            return true;
        }

        public void ClearActive()
        {
            if (ActiveGroup == null) return;
            ActiveGroup = null;
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i].MapMarker != null) _groups[i].MapMarker.SetSelected(false);
            if (ActiveGroupChanged != null) ActiveGroupChanged(null);
        }

        public bool TryPickMarker(Vector2 screenPosition)
        {
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var group = _groups[i];
                if (group.Team != TeamId.Player || group.MapMarker == null) continue;
                if (!group.MapMarker.HitTest(screenPosition)) continue;
                return Inspect(group);
            }
            return false;
        }

        public void CloseCommanderInspection()
        {
            ClearActive();
            if (CommanderInspectionClosed != null) CommanderInspectionClosed();
        }

        public void NotifyUnitRemoved(UnitView unit)
        {
            var group = Find(unit);
            if (group == null) return;
            group.Members.Remove(unit);
            if (!group.IsDefeated || group.DefeatNotified) return;
            group.DefeatNotified = true;
            group.State = CommanderGroupState.Defeated;
            if (BattleVisionService.Instance != null)
                BattleVisionService.Instance.RemoveGroup(group);
            if (FormationReservationService.Instance != null)
                FormationReservationService.Instance.Release(group.GroupId);
            if (group.MapMarker != null) group.MapMarker.SetDefeated();
            if (ActiveGroup == group)
            {
                ClearActive();
                if (CommanderInspectionClosed != null) CommanderInspectionClosed();
            }
            if (GroupDefeated != null) GroupDefeated(group);
        }
    }
}
