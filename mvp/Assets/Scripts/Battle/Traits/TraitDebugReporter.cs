#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Units;
using Mvp.Progression;
using Mvp.Shared;

namespace Mvp.Battle.Traits
{
    /// <summary>
    /// Editor/development-only diagnostics for the trait runtime (阶段一 Task A).
    /// Prints a per-player-formation resolution report when a battle starts and
    /// on F8, and logs a brief line whenever a group's effect Active flags change
    /// (e.g. crossing the 35% / 15% health thresholds). Enemy groups are never in
    /// the runtime dictionary, so they never appear. Compiled out of release
    /// player builds; no gameplay code path depends on it.
    /// </summary>
    public static class TraitDebugReporter
    {
        static TraitDebugReporterHost _host;
        static readonly Dictionary<string, string> _lastActiveSignature =
            new Dictionary<string, string>();

        /// <summary>Armed once per battle from TraitEffectService.BuildRuntime.</summary>
        public static void NotifyBattleStart()
        {
            EnsureHost();
            _lastActiveSignature.Clear();
            if (_host != null) _host.ArmInitialDump();
        }

        /// <summary>Prints the full report for every player formation in the runtime.</summary>
        public static void Dump()
        {
            var runtimes = TraitEffectService.GetDebugRuntimes();
            if (runtimes.Count == 0)
            {
                Debug.Log("[TraitDebug] === Trait Runtime Report ===\n(no player formations in runtime)");
                return;
            }

            var registry = CommanderGroupRegistry.Instance;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[TraitDebug] === Trait Runtime Report ===");
            for (int i = 0; i < runtimes.Count; i++)
            {
                var runtime = runtimes[i];
                var group = registry != null ? registry.Find(runtime.GroupId) : null;
                sb.AppendLine();
                sb.AppendLine("-- 编队 " + runtime.GroupId +
                    " | CommanderId=" + runtime.CommanderId + " --");
                sb.AppendLine("  装备卡: " + JoinNames(
                    TraitEffectService.GetEquippedTraitNames(runtime.CommanderId)));
                sb.AppendLine("  编队生命比: " +
                    (group != null ? FormatRatio(HealthRatioOf(group)) : "组未注册"));
                for (int e = 0; e < runtime.Effects.Count; e++)
                {
                    var re = runtime.Effects[e];
                    var effect = re.Effect;
                    sb.AppendLine("  效果[" + e + "] " + KindName(effect) +
                        " | def=" + re.DefinitionId +
                        " | 数值=" + (effect != null ? FormatValue(effect) : "?") +
                        " | 阈值=" + (effect != null ? FormatThreshold(effect) : "?") +
                        " | active=" + (re.Active ? "on" : "off"));
                }
                sb.AppendLine("  实际倍率: " + FormatMultipliers(group));
            }
            Debug.Log(sb.ToString());
        }

        static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("TraitDebugReporterHost");
            go.hideFlags = HideFlags.HideInHierarchy;
            _host = go.AddComponent<TraitDebugReporterHost>();
        }

        static float HealthRatioOf(CommanderGroupRuntime group)
        {
            if (group == null) return 1f;
            int current = 0, max = 0;
            var members = group.Members;
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null) continue;
                var data = member.Data;
                if (data.State == UnitState.Dead || data.CurrentHealth <= 0) continue;
                current += data.CurrentHealth;
                max += RuntimeMaxHealthOf(data);
            }
            return max > 0 ? (float)current / max : 1f;
        }

        static int RuntimeMaxHealthOf(UnitRuntimeData data)
        {
            if (data == null) return 1;
            return data.RuntimeMaxHealth > 0
                ? data.RuntimeMaxHealth
                : (data.Definition != null ? data.Definition.MaxHealth : 1);
        }

        static string FormatMultipliers(CommanderGroupRuntime group)
        {
            if (group == null) return "组未注册";
            var member = FirstAliveMember(group);
            if (member == null || member.Data == null) return "无存活成员";
            var data = member.Data;
            return "攻击=" + FormatFloat(TraitEffectService.GetAttackPowerMultiplier(data)) +
                " | 冷却=" + FormatFloat(TraitEffectService.GetAttackCooldownMultiplier(data)) +
                " | 移速=" + FormatFloat(TraitEffectService.GetMoveSpeedMultiplier(data)) +
                " | 受击=" + FormatFloat(TraitEffectService.GetIncomingDamageMultiplier(data)) +
                " | 最大生命加成=" + TraitEffectService.GetMaxHealthBonus(
                    data.Definition, data.CommanderGroupId);
        }

        static UnitView FirstAliveMember(CommanderGroupRuntime group)
        {
            if (group == null) return null;
            var members = group.Members;
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null) continue;
                if (member.Data.State != UnitState.Dead && member.Data.CurrentHealth > 0)
                    return member;
            }
            return null;
        }

        static string KindName(TraitEffect effect)
        {
            return effect != null ? effect.Kind.ToString() : "?";
        }

        static string FormatValue(TraitEffect e)
        {
            switch (e.Kind)
            {
                case TraitEffectKind.ModifyMaxHealth:
                case TraitEffectKind.ModifyAttackPower:
                case TraitEffectKind.ModifyMoveSpeed:
                    return SignedPercent(e.Value);
                case TraitEffectKind.ModifyAttackCooldown:
                case TraitEffectKind.ReduceIncomingDamage:
                    return SignedPercent(-e.Value);
                default:
                    return SignedPercent(e.Value);
            }
        }

        static string FormatThreshold(TraitEffect e)
        {
            return e.Trigger == TraitTriggerKind.Always
                ? "始终"
                : (int)Math.Round(e.TriggerValue * 100f) + "%";
        }

        static string FormatRatio(float ratio)
        {
            return (int)Math.Round(ratio * 100f) + "%";
        }

        static string FormatFloat(float v)
        {
            return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string SignedPercent(float v)
        {
            int p = (int)Math.Round(v * 100f);
            return p >= 0 ? "+" + p + "%" : p + "%";
        }

        static string JoinNames(string[] names)
        {
            if (names == null || names.Length == 0) return "(无)";
            return string.Join("、", names);
        }

        /// <summary>
        /// Hidden MonoBehaviour bridging the reporter to MediumTick and F8. Created
        /// once per battle by NotifyBattleStart; OnDestroy clears the static ref.
        /// </summary>
        sealed class TraitDebugReporterHost : MonoBehaviour
        {
            bool _initialDumpDone;

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
                _host = null;
            }

            public void ArmInitialDump()
            {
                _initialDumpDone = false;
            }

            void Update()
            {
                if (Input.GetKeyDown(KeyCode.F8)) Dump();
            }

            void OnMediumTick()
            {
                bool anyRegistered = false;
                var registry = CommanderGroupRegistry.Instance;
                var runtimes = TraitEffectService.GetDebugRuntimes();
                for (int i = 0; i < runtimes.Count; i++)
                {
                    if (registry != null && registry.Find(runtimes[i].GroupId) != null)
                    {
                        anyRegistered = true;
                        break;
                    }
                }
                if (anyRegistered && !_initialDumpDone)
                {
                    _initialDumpDone = true;
                    Dump();
                }
                CheckActiveChanges();
            }

            void CheckActiveChanges()
            {
                var registry = CommanderGroupRegistry.Instance;
                if (registry == null) return;
                var runtimes = TraitEffectService.GetDebugRuntimes();
                for (int i = 0; i < runtimes.Count; i++)
                {
                    var runtime = runtimes[i];
                    var group = registry.Find(runtime.GroupId);
                    if (group == null) continue;
                    string signature = BuildActiveSignature(runtime);
                    string last;
                    if (_lastActiveSignature.TryGetValue(runtime.GroupId, out last) &&
                        last != signature)
                    {
                        Debug.Log("[TraitDebug] 编队 " + runtime.GroupId + " 效果状态变化 | " +
                            last + " -> " + signature +
                            " | 生命比 " + FormatRatio(HealthRatioOf(group)));
                    }
                    _lastActiveSignature[runtime.GroupId] = signature;
                }
            }

            static string BuildActiveSignature(CommanderTraitRuntime runtime)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < runtime.Effects.Count; i++)
                {
                    if (i > 0) sb.Append('|');
                    sb.Append(runtime.Effects[i].DefinitionId);
                    sb.Append(':');
                    sb.Append(runtime.Effects[i].Active ? "on" : "off");
                }
                return sb.ToString();
            }
        }
    }
}
#endif
