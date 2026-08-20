using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.Vision
{
    /// <summary>Staggered group-level logical vision built from living member vision ranges.</summary>
    public sealed class BattleVisionService : MonoBehaviour
    {
        public static BattleVisionService Instance { get; private set; }

        public event Action<CommanderGroupRuntime, CommanderGroupRuntime> GroupDiscovered;
        public event Action<CommanderGroupRuntime, CommanderGroupRuntime> GroupLost;

        readonly Dictionary<string, HashSet<string>> _visibleByObserver =
            new Dictionary<string, HashSet<string>>();
        readonly Dictionary<string, int> _versionByObserver =
            new Dictionary<string, int>();
        readonly HashSet<string> _nextVisible = new HashSet<string>();
        readonly List<UnitView> _queryBuffer = new List<UnitView>(32);
        readonly List<string> _changeBuffer = new List<string>(16);
        int _groupCursor;

        public int RefreshCount { get; private set; }
        public int DiscoveryCount { get; private set; }
        public int LostCount { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.SlowTick += OnSlowTick;
        }

        void OnDisable()
        {
            BattleTickService.SlowTick -= OnSlowTick;
        }

        void OnDestroy()
        {
            Clear();
            if (Instance == this) Instance = null;
        }

        public bool IsVisible(CommanderGroupRuntime observer, CommanderGroupRuntime target)
        {
            if (observer == null || target == null) return false;
            HashSet<string> visible;
            return _visibleByObserver.TryGetValue(observer.GroupId, out visible) &&
                visible.Contains(target.GroupId);
        }

        public int GetSnapshotVersion(CommanderGroupRuntime observer)
        {
            if (observer == null) return 0;
            int version;
            return _versionByObserver.TryGetValue(observer.GroupId, out version) ? version : 0;
        }

        public void GetVisibleEnemyGroups(CommanderGroupRuntime observer,
            List<CommanderGroupRuntime> output)
        {
            if (output == null) return;
            output.Clear();
            if (observer == null) return;
            HashSet<string> visible;
            if (!_visibleByObserver.TryGetValue(observer.GroupId, out visible)) return;
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            foreach (string groupId in visible)
            {
                var group = registry.Find(groupId);
                if (group != null && !group.IsDefeated) output.Add(group);
            }
        }

        public void RefreshNow(CommanderGroupRuntime observer)
        {
            if (observer == null || observer.IsDefeated)
            {
                if (observer != null) RemoveObserver(observer);
                return;
            }
            var spatial = BattleSpatialIndex.Instance;
            var registry = CommanderGroupRegistry.Instance;
            if (spatial == null || registry == null) return;
            RefreshCount++;

            _nextVisible.Clear();
            for (int i = 0; i < observer.Members.Count; i++)
            {
                var member = observer.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead ||
                    member.Data.Definition == null) continue;
                int range = Mathf.Max(0, member.Data.Definition.VisionRange);
                _queryBuffer.Clear();
                spatial.QueryEnemies(member.Data.GridPosition, range, observer.Team, _queryBuffer);
                for (int q = 0; q < _queryBuffer.Count; q++)
                {
                    var targetGroup = registry.Find(_queryBuffer[q]);
                    if (targetGroup != null && !targetGroup.IsDefeated)
                        _nextVisible.Add(targetGroup.GroupId);
                }
            }

            HashSet<string> current;
            if (!_visibleByObserver.TryGetValue(observer.GroupId, out current))
            {
                current = new HashSet<string>();
                _visibleByObserver[observer.GroupId] = current;
            }

            _changeBuffer.Clear();
            foreach (string groupId in current)
                if (!_nextVisible.Contains(groupId)) _changeBuffer.Add(groupId);
            for (int i = 0; i < _changeBuffer.Count; i++)
            {
                string groupId = _changeBuffer[i];
                current.Remove(groupId);
                var target = registry.Find(groupId);
                if (target != null)
                {
                    LostCount++;
                    GroupLost?.Invoke(observer, target);
                }
            }

            foreach (string groupId in _nextVisible)
            {
                if (!current.Add(groupId)) continue;
                var target = registry.Find(groupId);
                if (target != null)
                {
                    DiscoveryCount++;
                    GroupDiscovered?.Invoke(observer, target);
                }
            }
            int version;
            _versionByObserver.TryGetValue(observer.GroupId, out version);
            _versionByObserver[observer.GroupId] = version == int.MaxValue ? 1 : version + 1;
        }

        public void RemoveGroup(CommanderGroupRuntime group)
        {
            if (group == null) return;
            RemoveObserver(group);
            _versionByObserver.Remove(group.GroupId);
            foreach (var pair in _visibleByObserver) pair.Value.Remove(group.GroupId);
        }

        public void Clear()
        {
            _visibleByObserver.Clear();
            _versionByObserver.Clear();
            _nextVisible.Clear();
            _queryBuffer.Clear();
            _changeBuffer.Clear();
            _groupCursor = 0;
        }

        void OnSlowTick()
        {
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null || registry.Groups.Count == 0) return;
            if (_groupCursor >= registry.Groups.Count) _groupCursor = 0;
            RefreshNow(registry.Groups[_groupCursor]);
            _groupCursor = (_groupCursor + 1) % registry.Groups.Count;
        }

        void RemoveObserver(CommanderGroupRuntime observer)
        {
            _visibleByObserver.Remove(observer.GroupId);
        }
    }
}
