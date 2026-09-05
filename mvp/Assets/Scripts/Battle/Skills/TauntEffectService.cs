using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.AI;
using Mvp.Battle.Commanders;
using Mvp.Battle.Vision;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>Group-level taunt: forces nearby enemy groups to attack the source.</summary>
    public static class TauntEffectService
    {
        public sealed class TauntRuntimeState
        {
            public string AffectedGroupId;
            public string SourceGroupId;
            public float ExpiresAt;
            public long SkillSequence;
        }

        static readonly Dictionary<string, TauntRuntimeState> _states =
            new Dictionary<string, TauntRuntimeState>();
        static readonly Dictionary<string, float> _cooldownUntil =
            new Dictionary<string, float>();
        static readonly List<CommanderGroupRuntime> _targets =
            new List<CommanderGroupRuntime>(8);
        static readonly List<string> _removeBuffer = new List<string>(8);
        static TauntHost _host;

        public static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("TauntHost");
            _host = go.AddComponent<TauntHost>();
        }

        public static void Shutdown()
        {
            if (_host != null)
            {
                var host = _host;
                _host = null;
                if (host != null) Object.Destroy(host.gameObject);
            }
            _states.Clear();
            _cooldownUntil.Clear();
            _targets.Clear();
            _removeBuffer.Clear();
        }

        public static bool TryActivate(CommanderGroupRuntime source, float now,
            out int affectedCount, out string reason)
        {
            affectedCount = 0;
            reason = null;
            if (source == null) { reason = "未激活编队"; return false; }
            if (source.IsDefeated) { reason = "编队已被消灭"; return false; }
            if (source.Team != TeamId.Player) { reason = "仅玩家编队可使用嘲讽"; return false; }
            var def = SkillCatalog.Get(SkillIds.Taunt);
            if (def == null) { reason = "嘲讽技能不存在"; return false; }
            if (GetRemainingCooldown(source, now) > 0.01f)
            {
                reason = "嘲讽冷却中";
                return false;
            }

            CollectTargets(source, def.RangeCells, _targets);
            if (_targets.Count == 0)
            {
                reason = "范围内没有可嘲讽的敌军";
                return false;
            }

            var commands = CommanderGroupCommandController.Instance;
            if (commands != null) commands.InterruptPersistentModes(source);
            else
            {
                if (source.Skills.PersistentMode == PersistentSkillMode.Concealment)
                    ConcealmentService.EndConcealment(source);
                source.Skills.ResetModes();
                if (source.State == CommanderGroupState.Holding)
                    source.State = CommanderGroupState.Idle;
            }
            float expiresAt = now + def.DurationSeconds;
            long sequence = source.Skills.SkillSequence;
            var vision = BattleVisionService.Instance;
            var ai = EnemyGroupAiController.Instance;
            for (int i = 0; i < _targets.Count; i++)
            {
                var affected = _targets[i];
                // Last successful forced-aggro effect wins.
                TacticalDecoyService.ClearForcedTarget(affected.GroupId);
                TauntRuntimeState previous;
                if (_states.TryGetValue(affected.GroupId, out previous) &&
                    previous.SourceGroupId != source.GroupId)
                    RemoveForcedVision(previous);
                _states[affected.GroupId] = new TauntRuntimeState
                {
                    AffectedGroupId = affected.GroupId,
                    SourceGroupId = source.GroupId,
                    ExpiresAt = expiresAt,
                    SkillSequence = sequence
                };
                if (vision != null) vision.AddForcedVisibility(affected, source, expiresAt);
                if (ai != null) ai.NotifyTaunted(affected, source);
                affectedCount++;
            }
            _cooldownUntil[source.GroupId] = now + def.CooldownSeconds;
            return true;
        }

        /// <summary>Clears an older direct taunt when a newer forced target is applied.</summary>
        public static void ClearAffected(string affectedGroupId)
        {
            if (string.IsNullOrEmpty(affectedGroupId)) return;
            TauntRuntimeState state;
            if (_states.TryGetValue(affectedGroupId, out state)) RemoveForcedVision(state);
            _states.Remove(affectedGroupId);
        }

        public static bool TryGetForcedTarget(CommanderGroupRuntime affected, float now,
            out CommanderGroupRuntime source)
        {
            source = null;
            if (affected == null) return false;
            TauntRuntimeState state;
            if (!_states.TryGetValue(affected.GroupId, out state)) return false;
            if (state.ExpiresAt <= now)
            {
                RemoveForcedVision(state);
                _states.Remove(affected.GroupId);
                return false;
            }
            var registry = CommanderGroupRegistry.Instance;
            source = registry != null ? registry.Find(state.SourceGroupId) : null;
            if (source == null || source.IsDefeated)
            {
                _states.Remove(affected.GroupId);
                source = null;
                return false;
            }
            return true;
        }

        public static float GetRemainingCooldown(CommanderGroupRuntime source, float now)
        {
            if (source == null) return 0f;
            float until;
            return _cooldownUntil.TryGetValue(source.GroupId, out until) && until > now
                ? until - now : 0f;
        }

        public static bool IsInRange(Vector2Int source, Vector2Int target, int range)
        {
            return SkillRangeMath.Chebyshev(source, target) <= range;
        }

        public static void RemoveGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            TauntRuntimeState affectedState;
            if (_states.TryGetValue(groupId, out affectedState))
                RemoveForcedVision(affectedState);
            _states.Remove(groupId);
            _cooldownUntil.Remove(groupId);
            _removeBuffer.Clear();
            foreach (var pair in _states)
                if (pair.Value.SourceGroupId == groupId) _removeBuffer.Add(pair.Key);
            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                TauntRuntimeState state;
                if (_states.TryGetValue(_removeBuffer[i], out state)) RemoveForcedVision(state);
                _states.Remove(_removeBuffer[i]);
            }
        }

        static void CollectTargets(CommanderGroupRuntime source, int range,
            List<CommanderGroupRuntime> output)
        {
            output.Clear();
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            for (int i = 0; i < registry.Groups.Count; i++)
            {
                var group = registry.Groups[i];
                if (group == null || group.IsDefeated || group.Team != TeamId.Enemy) continue;
                if (IsInRange(source.AnchorCell, group.AnchorCell, range)) output.Add(group);
            }
        }

        static void Sweep(float now)
        {
            _removeBuffer.Clear();
            foreach (var pair in _states)
                if (pair.Value.ExpiresAt <= now) _removeBuffer.Add(pair.Key);
            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                TauntRuntimeState state;
                if (_states.TryGetValue(_removeBuffer[i], out state)) RemoveForcedVision(state);
                _states.Remove(_removeBuffer[i]);
            }
        }

        static void RemoveForcedVision(TauntRuntimeState state)
        {
            if (state == null) return;
            var registry = CommanderGroupRegistry.Instance;
            var vision = BattleVisionService.Instance;
            if (registry == null || vision == null) return;
            var observer = registry.Find(state.AffectedGroupId);
            var source = registry.Find(state.SourceGroupId);
            if (observer != null && source != null)
                vision.RemoveForcedVisibility(observer, source);
        }

        sealed class TauntHost : MonoBehaviour
        {
            void OnEnable()
            {
                BattleTickService.MediumTick += OnMediumTick;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated += OnGroupDefeated;
            }

            void OnDisable()
            {
                BattleTickService.MediumTick -= OnMediumTick;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated -= OnGroupDefeated;
            }

            void OnMediumTick() { Sweep(Time.time); }
            void OnGroupDefeated(CommanderGroupRuntime group)
            {
                if (group != null) RemoveGroup(group.GroupId);
            }
            void OnDestroy()
            {
                _states.Clear();
                _cooldownUntil.Clear();
            }
        }
    }
}
