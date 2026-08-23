using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Progression;
using Mvp.Shared;

namespace Mvp.Battle.Traits
{
    /// <summary>
    /// Static query API for battle systems. Runtime trait state is built once at
    /// battle start and refreshed every MediumTick; the query methods are
    /// allocation-free O(equipped effects per group) lookups (计划文档 13.4/14.2).
    /// A hidden TraitRuntimeHost MonoBehaviour subscribes to MediumTick and
    /// GroupDefeated, and cleans up on scene teardown via OnDestroy.
    /// </summary>
    public static class TraitEffectService
    {
        // ---- multipliers (13.2) ------------------------------------------------

        const float AttackPowerMin = 0.5f, AttackPowerMax = 1.8f;
        const float CooldownMin = 0.6f, CooldownMax = 1.5f;
        const float IncomingDamageMin = 0.55f, IncomingDamageMax = 1.5f;
        const float MoveSpeedMin = 0.5f, MoveSpeedMax = 1.4f;

        static readonly Dictionary<string, CommanderTraitRuntime> _runtime =
            new Dictionary<string, CommanderTraitRuntime>();
        static TraitRuntimeHost _host;

        // ---- lifecycle ----------------------------------------------------------

        /// <summary>
        /// Clears, resolves and activates runtime traits for the given roster, and
        /// creates the hidden host. Called by UnitSpawner before spawning units so
        /// SpawnUnit can read the max-health bonus.
        /// </summary>
        public static void BuildRuntime(IReadOnlyList<ExpeditionCommanderEntry> roster)
        {
            ClearRuntimeInternal();
            var warnings = new List<string>();
            TraitRuntimeResolver.TryBuildRuntime(roster, _runtime, warnings);
            if (warnings.Count > 0)
            {
                for (int i = 0; i < warnings.Count; i++)
                    Debug.LogWarning(warnings[i]);
            }
            EnsureHost();
            // Groups are not registered in the registry until after spawning, so a
            // RefreshConditions() at this point cannot reach them. Always-triggered
            // effects are activated immediately so spawn-time queries (max health
            // bonus) return the correct value before the first MediumTick.
            ActivateAlwaysEffects();
            RefreshConditions();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TraitDebugReporter.NotifyBattleStart();
#endif
        }

        static void ActivateAlwaysEffects()
        {
            foreach (var pair in _runtime)
            {
                var effects = pair.Value.Effects;
                for (int i = 0; i < effects.Count; i++)
                {
                    var re = effects[i];
                    if (re.Effect != null && re.Effect.Trigger == TraitTriggerKind.Always)
                        re.Active = true;
                }
            }
        }

        /// <summary>Clears runtime state and destroys the hidden host (if any).</summary>
        public static void ClearRuntime()
        {
            ClearRuntimeInternal();
            if (_host != null)
            {
                var host = _host;
                _host = null;
                if (host != null) Object.Destroy(host.gameObject);
            }
        }

        /// <summary>
        /// Clears the runtime dictionary only. Called by the host's OnDestroy so the
        /// scene-teardown path never tries to destroy the host again (no recursion).
        /// </summary>
        internal static void ClearRuntimeInternal()
        {
            _runtime.Clear();
        }

        internal static void RemoveRuntime(string groupId)
        {
            if (!string.IsNullOrEmpty(groupId)) _runtime.Remove(groupId);
        }

        /// <summary>
        /// Read-only snapshot of the current runtime trait state for editor/dev
        /// diagnostics (TraitDebugReporter). Copies the values so concurrent
        /// mutation by battle systems can never throw during a read.
        /// </summary>
        internal static List<CommanderTraitRuntime> GetDebugRuntimes()
            => new List<CommanderTraitRuntime>(_runtime.Values);

        static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("TraitRuntimeHost");
            _host = go.AddComponent<TraitRuntimeHost>();
        }

        // ---- condition refresh ---------------------------------------------------

        /// <summary>
        /// Recomputes group condition summaries and each effect's Active flag.
        /// Subscribed to BattleTickService.MediumTick (every 0.1s).
        /// </summary>
        public static void RefreshConditions()
        {
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            var groups = registry.Groups;
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null) continue;
                CommanderTraitRuntime runtime;
                if (!_runtime.TryGetValue(group.GroupId, out runtime)) continue;
                if (runtime.Effects.Count == 0) continue;
                var state = ComputeConditionState(group);
                for (int e = 0; e < runtime.Effects.Count; e++)
                {
                    var re = runtime.Effects[e];
                    re.Active = EvaluateTrigger(re.Effect, state);
                }
            }
        }

        static GroupTraitConditionState ComputeConditionState(CommanderGroupRuntime group)
        {
            var state = new GroupTraitConditionState();
            var members = group.Members;
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.Data == null) continue;
                var data = member.Data;
                if (data.State == UnitState.Dead || data.CurrentHealth <= 0) continue;
                state.AliveCount++;
                state.CurrentHealthTotal += data.CurrentHealth;
                state.MaxHealthTotal += RuntimeMaxHealthOf(data);
            }
            return state;
        }

        static bool EvaluateTrigger(TraitEffect effect, GroupTraitConditionState state)
        {
            if (effect == null) return false;
            switch (effect.Trigger)
            {
                case TraitTriggerKind.Always:
                    return true;
                case TraitTriggerKind.WhileGroupHealthBelowPercent:
                    return state.AliveCount > 0 && state.HealthRatio < effect.TriggerValue;
                case TraitTriggerKind.WhileGroupHealthAbovePercent:
                    return state.AliveCount > 0 && state.HealthRatio >= effect.TriggerValue;
                default:
                    return false;
            }
        }

        // ---- query API ----------------------------------------------------------

        /// <summary>
        /// Max-health bonus for a group at spawn time. Enemy group ids are not in the
        /// runtime dictionary, so they always receive 0.
        /// </summary>
        public static int GetMaxHealthBonus(UnitDefinition def, string commanderGroupId)
        {
            if (def == null) return 0;
            float sum = SumActiveByGroup(commanderGroupId, TraitEffectKind.ModifyMaxHealth);
            return sum != 0f ? Mathf.RoundToInt(def.MaxHealth * sum) : 0;
        }

        public static float GetAttackPowerMultiplier(UnitRuntimeData unit)
        {
            return Mathf.Clamp(1f + SumActive(unit, TraitEffectKind.ModifyAttackPower),
                AttackPowerMin, AttackPowerMax);
        }

        public static float GetAttackCooldownMultiplier(UnitRuntimeData unit)
        {
            return Mathf.Clamp(1f - SumActive(unit, TraitEffectKind.ModifyAttackCooldown),
                CooldownMin, CooldownMax);
        }

        public static float GetMoveSpeedMultiplier(UnitRuntimeData unit)
        {
            return Mathf.Clamp(1f + SumActive(unit, TraitEffectKind.ModifyMoveSpeed),
                MoveSpeedMin, MoveSpeedMax);
        }

        public static float GetIncomingDamageMultiplier(UnitRuntimeData unit)
        {
            return Mathf.Clamp(1f - SumActive(unit, TraitEffectKind.ReduceIncomingDamage),
                IncomingDamageMin, IncomingDamageMax);
        }

        static float SumActive(UnitRuntimeData unit, TraitEffectKind kind)
        {
            if (unit == null) return 0f;
            return SumActiveByGroup(unit.CommanderGroupId, kind);
        }

        static float SumActiveByGroup(string groupId, TraitEffectKind kind)
        {
            if (string.IsNullOrEmpty(groupId)) return 0f;
            CommanderTraitRuntime runtime;
            if (!_runtime.TryGetValue(groupId, out runtime)) return 0f;
            float sum = 0f;
            var effects = runtime.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var re = effects[i];
                if (!re.Active || re.Effect == null || re.Effect.Kind != kind) continue;
                sum += re.Effect.Value;
            }
            return sum;
        }

        static int RuntimeMaxHealthOf(UnitRuntimeData data)
        {
            if (data == null) return 1;
            return data.RuntimeMaxHealth > 0
                ? data.RuntimeMaxHealth
                : (data.Definition != null ? data.Definition.MaxHealth : 1);
        }

        // ---- battle UI ------------------------------------------------------------

        /// <summary>Display names of the cards equipped on the given commander's loadout.</summary>
        public static string[] GetEquippedTraitNames(string commanderId)
        {
            var progression = PlayerProgressionStore.Current;
            if (progression == null || string.IsNullOrEmpty(commanderId))
                return new string[0];
            CommanderLoadoutSnapshot loadout = null;
            for (int i = 0; i < progression.CommanderLoadouts.Count; i++)
            {
                if (progression.CommanderLoadouts[i].CommanderId == commanderId)
                {
                    loadout = progression.CommanderLoadouts[i];
                    break;
                }
            }
            if (loadout == null) return new string[0];
            var names = new List<string>();
            for (int s = 0; s < loadout.TraitCardInstanceIds.Length; s++)
            {
                string instanceId = loadout.TraitCardInstanceIds[s];
                if (string.IsNullOrEmpty(instanceId)) continue;
                var card = FindCard(progression, instanceId);
                if (card == null) continue;
                var def = TraitCatalog.Get(card.DefinitionId);
                if (def != null) names.Add(def.DisplayName);
            }
            return names.ToArray();
        }

        static TraitCardInstance FindCard(PlayerProgressionSnapshot progression, string instanceId)
        {
            if (progression == null || string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < progression.TraitCards.Count; i++)
                if (progression.TraitCards[i].InstanceId == instanceId)
                    return progression.TraitCards[i];
            return null;
        }

        /// <summary>
        /// Hidden MonoBehaviour that bridges the static service to Unity lifecycle
        /// and the central tick scheduler. Destroyed automatically when its scene
        /// unloads; OnDestroy clears the runtime dictionary.
        /// </summary>
        sealed class TraitRuntimeHost : MonoBehaviour
        {
            void OnEnable()
            {
                BattleTickService.MediumTick += RefreshConditions;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated += OnGroupDefeated;
            }

            void OnDisable()
            {
                BattleTickService.MediumTick -= RefreshConditions;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated -= OnGroupDefeated;
            }

            void OnGroupDefeated(CommanderGroupRuntime group)
            {
                if (group != null) RemoveRuntime(group.GroupId);
            }

            void OnDestroy()
            {
                ClearRuntimeInternal();
            }
        }
    }
}
