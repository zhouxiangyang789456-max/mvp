using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Sprint special skill (战斗技能系统开发文档 §8): all-tank commander groups move at
    /// MoveSpeedMultiplier × base speed for DurationSeconds, then enter a cooldown.
    /// Attacks end the sprint early. Values come from SkillCatalog; core rules take an
    /// explicit <c>now</c> so tests can drive timers without a scene.
    /// </summary>
    public static class SprintEffectService
    {
        static SprintHost _host;

        public static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("SprintHost");
            _host = go.AddComponent<SprintHost>();
        }

        public static void Shutdown()
        {
            if (_host != null)
            {
                var host = _host;
                _host = null;
                if (host != null) UnityEngine.Object.Destroy(host.gameObject);
            }
        }

        /// <summary>§8.2: every alive member must carry UnitTag.Tank.</summary>
        public static bool IsAllTankGroup(CommanderGroupRuntime group)
        {
            if (group == null) return false;
            int alive = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                if (member.Data.Definition == null ||
                    (member.Data.Definition.Tags & UnitTag.Tank) == 0) return false;
                alive++;
            }
            return alive > 0;
        }

        public static bool TryActivate(CommanderGroupRuntime group, float now, out string reason)
        {
            reason = null;
            if (group == null) { reason = "未激活编队"; return false; }
            if (group.IsDefeated) { reason = "编队已被消灭"; return false; }
            var def = SkillCatalog.Get(SkillIds.Sprint);
            if (def == null) { reason = "冲刺技能不存在"; return false; }
            if (IsActive(group, now)) { reason = "冲刺已在进行"; return false; }
            if (GetRemainingCooldown(group, now) > 0.01f) { reason = "冲刺冷却中"; return false; }
            if (!IsAllTankGroup(group)) { reason = "编队不全是坦克单位"; return false; }

            float activeUntil = now + def.DurationSeconds;
            float cooldownUntil = now + def.DurationSeconds + def.CooldownSeconds;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                var st = group.Skills.GetOrCreate(member.Data.Id, SkillIds.Sprint);
                st.State = SkillRuntimeState.Active;
                st.ActiveUntil = activeUntil;
                st.CooldownUntil = cooldownUntil;
                st.IsEligible = true;
            }
            return true;
        }

        public static bool IsActive(CommanderGroupRuntime group, float now)
        {
            if (group == null) return false;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                UnitSkillRuntime st;
                if (!group.Skills.UnitStates.TryGetValue(member.Data.Id, out st)) continue;
                if (st.SkillId == SkillIds.Sprint && st.State == SkillRuntimeState.Active &&
                    st.ActiveUntil > now) return true;
            }
            return false;
        }

        /// <summary>Remaining cooldown in seconds (0 when ready).</summary>
        public static float GetRemainingCooldown(CommanderGroupRuntime group, float now)
        {
            if (group == null) return 0f;
            float max = 0f;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                UnitSkillRuntime st;
                if (!group.Skills.UnitStates.TryGetValue(member.Data.Id, out st)) continue;
                if (st.SkillId != SkillIds.Sprint || st.State != SkillRuntimeState.Cooldown) continue;
                if (st.CooldownUntil > now) max = Mathf.Max(max, st.CooldownUntil - now);
            }
            return max;
        }

        /// <summary>
        /// Move-speed multiplier applied while sprint is active on a member. Expires
        /// Active→Cooldown lazily here (also swept by the host) and defaults to 1.
        /// </summary>
        public static float GetMoveSpeedMultiplier(CommanderGroupRuntime group,
            UnitRuntimeData member, float now)
        {
            if (group == null || member == null) return 1f;
            UnitSkillRuntime st;
            if (!group.Skills.UnitStates.TryGetValue(member.Id, out st)) return 1f;
            if (st.SkillId != SkillIds.Sprint || st.State != SkillRuntimeState.Active) return 1f;
            if (now >= st.ActiveUntil)
            {
                st.State = SkillRuntimeState.Cooldown;
                st.ActiveUntil = 0f;
                return 1f;
            }
            var def = SkillCatalog.Get(SkillIds.Sprint);
            return def != null && def.MoveSpeedMultiplier > 0f ? def.MoveSpeedMultiplier : 1f;
        }

        /// <summary>§8.4: an attack command ends the sprint and starts the cooldown.</summary>
        public static void NotifyAttack(CommanderGroupRuntime group, float now)
        {
            if (group == null) return;
            var def = SkillCatalog.Get(SkillIds.Sprint);
            if (def == null) return;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null) continue;
                UnitSkillRuntime st;
                if (!group.Skills.UnitStates.TryGetValue(member.Data.Id, out st)) continue;
                if (st.SkillId != SkillIds.Sprint || st.State != SkillRuntimeState.Active) continue;
                st.State = SkillRuntimeState.Cooldown;
                st.ActiveUntil = 0f;
                st.CooldownUntil = now + def.CooldownSeconds;
            }
        }

        sealed class SprintHost : MonoBehaviour
        {
            readonly List<string> _keys = new List<string>(8);

            void OnEnable()
            {
                BattleTickService.MediumTick += Sweep;
            }

            void OnDisable()
            {
                BattleTickService.MediumTick -= Sweep;
            }

            void Sweep()
            {
                var registry = CommanderGroupRegistry.Instance;
                if (registry == null) return;
                float now = Time.time;
                for (int i = 0; i < registry.Groups.Count; i++)
                {
                    var group = registry.Groups[i];
                    if (group == null) continue;
                    var states = group.Skills.UnitStates;
                    if (states.Count == 0) continue;
                    _keys.Clear();
                    foreach (var pair in states)
                        if (pair.Value.SkillId == SkillIds.Sprint &&
                            pair.Value.State == SkillRuntimeState.Active &&
                            now >= pair.Value.ActiveUntil)
                            _keys.Add(pair.Key);
                    for (int k = 0; k < _keys.Count; k++)
                    {
                        UnitSkillRuntime st;
                        if (states.TryGetValue(_keys[k], out st))
                        {
                            st.State = SkillRuntimeState.Cooldown;
                            st.ActiveUntil = 0f;
                        }
                    }
                }
            }
        }
    }
}
