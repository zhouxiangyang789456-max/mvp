using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
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
        readonly Dictionary<string, Dictionary<string, float>> _forcedVisibleUntil =
            new Dictionary<string, Dictionary<string, float>>();
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
            if (IsForcedVisible(observer, target, Time.time)) return true;
            HashSet<string> visible;
            return _visibleByObserver.TryGetValue(observer.GroupId, out visible) &&
                visible.Contains(target.GroupId);
        }

        public void AddForcedVisibility(CommanderGroupRuntime observer,
            CommanderGroupRuntime target, float until)
        {
            if (observer == null || target == null) return;
            bool wasVisible = IsVisible(observer, target);
            Dictionary<string, float> targets;
            if (!_forcedVisibleUntil.TryGetValue(observer.GroupId, out targets))
            {
                targets = new Dictionary<string, float>();
                _forcedVisibleUntil[observer.GroupId] = targets;
            }
            float existing;
            if (!targets.TryGetValue(target.GroupId, out existing) || until > existing)
                targets[target.GroupId] = until;
            if (!wasVisible && !target.IsDefeated)
            {
                DiscoveryCount++;
                GroupDiscovered?.Invoke(observer, target);
            }
            BumpVersion(observer.GroupId);
        }

        public void RemoveForcedVisibility(CommanderGroupRuntime observer,
            CommanderGroupRuntime target)
        {
            if (observer == null || target == null) return;
            Dictionary<string, float> targets;
            if (!_forcedVisibleUntil.TryGetValue(observer.GroupId, out targets) ||
                !targets.Remove(target.GroupId)) return;
            if (targets.Count == 0) _forcedVisibleUntil.Remove(observer.GroupId);
            BumpVersion(observer.GroupId);
            RefreshNow(observer);
        }

        public bool IsForcedVisible(CommanderGroupRuntime observer,
            CommanderGroupRuntime target, float now)
        {
            if (observer == null || target == null) return false;
            Dictionary<string, float> targets;
            float until;
            return _forcedVisibleUntil.TryGetValue(observer.GroupId, out targets) &&
                targets.TryGetValue(target.GroupId, out until) && until > now;
        }

        /// <summary>
        /// Immediately grants <paramref name="observer"/> vision of
        /// <paramref name="target"/> regardless of range / concealment (used by close-range
        /// concealment discovery). Fires GroupDiscovered and bumps the observer's version.
        /// </summary>
        public void RevealNow(CommanderGroupRuntime observer, CommanderGroupRuntime target)
        {
            if (observer == null || target == null) return;
            HashSet<string> current;
            if (!_visibleByObserver.TryGetValue(observer.GroupId, out current))
            {
                current = new HashSet<string>();
                _visibleByObserver[observer.GroupId] = current;
            }
            if (current.Add(target.GroupId))
            {
                if (!target.IsDefeated)
                {
                    DiscoveryCount++;
                    GroupDiscovered?.Invoke(observer, target);
                }
            }
            int version;
            _versionByObserver.TryGetValue(observer.GroupId, out version);
            _versionByObserver[observer.GroupId] = version == int.MaxValue ? 1 : version + 1;
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
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            if (_visibleByObserver.TryGetValue(observer.GroupId, out visible))
            {
                foreach (string groupId in visible)
                {
                    var group = registry.Find(groupId);
                    if (group != null && !group.IsDefeated) output.Add(group);
                }
            }
            Dictionary<string, float> forced;
            if (!_forcedVisibleUntil.TryGetValue(observer.GroupId, out forced)) return;
            foreach (var pair in forced)
            {
                if (pair.Value <= Time.time) continue;
                var group = registry.Find(pair.Key);
                if (group != null && !group.IsDefeated && !output.Contains(group)) output.Add(group);
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
                    if (targetGroup == null || targetGroup.IsDefeated) continue;
                    // 隐蔽编队不出现在敌方视野 (战斗技能系统开发文档 §6.4).
                    if (ConcealmentService.IsConcealed(targetGroup)) continue;
                    _nextVisible.Add(targetGroup.GroupId);
                }
            }
            AddForcedTargets(observer.GroupId, Time.time);

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
            _forcedVisibleUntil.Remove(group.GroupId);
            foreach (var pair in _forcedVisibleUntil) pair.Value.Remove(group.GroupId);
        }

        public void Clear()
        {
            _visibleByObserver.Clear();
            _versionByObserver.Clear();
            _nextVisible.Clear();
            _queryBuffer.Clear();
            _changeBuffer.Clear();
            _forcedVisibleUntil.Clear();
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
            _forcedVisibleUntil.Remove(observer.GroupId);
        }

        void AddForcedTargets(string observerGroupId, float now)
        {
            Dictionary<string, float> targets;
            if (!_forcedVisibleUntil.TryGetValue(observerGroupId, out targets)) return;
            _changeBuffer.Clear();
            foreach (var pair in targets)
            {
                if (pair.Value > now) _nextVisible.Add(pair.Key);
                else _changeBuffer.Add(pair.Key);
            }
            for (int i = 0; i < _changeBuffer.Count; i++) targets.Remove(_changeBuffer[i]);
            if (targets.Count == 0) _forcedVisibleUntil.Remove(observerGroupId);
        }

        void BumpVersion(string observerGroupId)
        {
            int version;
            _versionByObserver.TryGetValue(observerGroupId, out version);
            _versionByObserver[observerGroupId] = version == int.MaxValue ? 1 : version + 1;
        }
    }
}
