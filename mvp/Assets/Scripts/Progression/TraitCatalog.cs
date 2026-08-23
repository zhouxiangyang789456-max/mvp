using System.Collections.Generic;
using Mvp.CommanderSelect;

namespace Mvp.Progression
{
    public static partial class TraitCatalog
    {
        /// <summary>原有 22 张通用卡(阶段一白名单已实现)。</summary>
        static readonly TraitCardDefinition[] LegacyGeneral =
        {
            Make("trait_brave", "勇敢", "生命低于 35% 时，攻击力提高 20%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ModifyAttackPower,
                    TraitTriggerKind.WhileGroupHealthBelowPercent, 0.20f, 0.35f),
                new[] { "attack", "low_health" }),
            Make("trait_cautious", "谨慎", "编队生命高于 70% 时，所受伤害降低 10%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.10f, 0.70f),
                new[] { "defense" }),
            Make("trait_inspiring", "鼓舞", "编队成员生命高于 70% 时，提高移动速度。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyMoveSpeed,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.15f, 0.70f),
                new[] { "mobility" }),
            Make("trait_tenacious", "坚韧", "编队成员生命上限提高 10%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyMaxHealth,
                    TraitTriggerKind.Always, 0.10f, 0f),
                new[] { "defense" }),
            Make("trait_disciplined", "纪律", "编队生命高于 70% 时，攻击冷却降低 15%。", TraitRarity.Epic, 9, 4,
                Effect(TraitEffectKind.ModifyAttackCooldown,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.15f, 0.70f),
                new[] { "attack", "formation" }),
            Make("trait_calm", "沉着", "编队生命高于 70% 时，所受伤害降低 6%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.06f, 0.70f),
                new[] { "defense" }),
            Make("trait_guard", "守备", "始终降低所受伤害 8%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.Always, 0.08f, 0f),
                new[] { "defense" }),
            Make("trait_hold", "坚守", "始终降低所受伤害 8%。", TraitRarity.Epic, 9, 4,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.Always, 0.08f, 0f),
                new[] { "defense", "formation" }),
            Make("trait_reflection", "回光反照", "编队生命低于 15% 时，攻击力提高 50%。", TraitRarity.Epic, 10, 5,
                Effect(TraitEffectKind.ModifyAttackPower,
                    TraitTriggerKind.WhileGroupHealthBelowPercent, 0.50f, 0.15f),
                new[] { "attack", "low_health" }),
            Make("trait_swift", "迅捷", "编队生命高于 70% 时，攻击冷却降低 12%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyAttackCooldown,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.12f, 0.70f),
                new[] { "cooldown", "attack" }),
            Make("trait_rapid", "疾攻", "始终降低攻击冷却 8%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ModifyAttackCooldown,
                    TraitTriggerKind.Always, 0.08f, 0f),
                new[] { "cooldown" }),
            Make("trait_holdline", "拒止", "编队生命低于 40% 时，所受伤害降低 15%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.WhileGroupHealthBelowPercent, 0.15f, 0.40f),
                new[] { "defense", "idle" }),
            Make("trait_entrench", "深垒", "编队生命高于 70% 时，所受伤害降低 15%。", TraitRarity.Epic, 9, 4,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.15f, 0.70f),
                new[] { "idle" }),
            Make("trait_frenzy", "昂扬", "编队生命高于 70% 时，攻击力提高 15%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyAttackPower,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.15f, 0.70f),
                new[] { "high_health", "attack" }),
            Make("trait_healthpool", "充沛", "编队成员生命上限提高 8%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ModifyMaxHealth,
                    TraitTriggerKind.Always, 0.08f, 0f),
                new[] { "high_health", "max_health" }),
            Make("trait_flanking", "迂回", "始终提高移动速度 12%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyMoveSpeed,
                    TraitTriggerKind.Always, 0.12f, 0f),
                new[] { "mobility", "formation" }),
            Make("trait_bulk", "厚重", "编队成员生命上限提高 15%。", TraitRarity.Epic, 9, 4,
                Effect(TraitEffectKind.ModifyMaxHealth,
                    TraitTriggerKind.Always, 0.15f, 0f),
                new[] { "max_health", "defense" }),
            Make("trait_recover", "复苏", "编队生命低于 35% 时，所受伤害降低 15%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ReduceIncomingDamage,
                    TraitTriggerKind.WhileGroupHealthBelowPercent, 0.15f, 0.35f),
                new[] { "sustain", "defense" }),
            Make("trait_command", "号令", "始终提高攻击力 10%。", TraitRarity.Rare, 7, 3,
                Effect(TraitEffectKind.ModifyAttackPower,
                    TraitTriggerKind.Always, 0.10f, 0f),
                new[] { "commander" }),
            Make("trait_morale", "军心", "编队生命高于 70% 时，提高移动速度 10%。", TraitRarity.Common, 5, 2,
                Effect(TraitEffectKind.ModifyMoveSpeed,
                    TraitTriggerKind.WhileGroupHealthAbovePercent, 0.10f, 0.70f),
                new[] { "commander", "support" }),
            AndEffect(
                Make("trait_balance", "均衡", "始终提高生命上限与攻击力 8%。", TraitRarity.Common, 5, 2,
                    Effect(TraitEffectKind.ModifyMaxHealth,
                        TraitTriggerKind.Always, 0.08f, 0f),
                    new[] { "balanced" }),
                Effect(TraitEffectKind.ModifyAttackPower, TraitTriggerKind.Always, 0.08f, 0f)),
            AndEffect(
                Make("trait_support", "支援", "始终降低所受伤害 8%，提高移动速度 8%。", TraitRarity.Rare, 7, 3,
                    Effect(TraitEffectKind.ReduceIncomingDamage,
                        TraitTriggerKind.Always, 0.08f, 0f),
                    new[] { "support" }),
                Effect(TraitEffectKind.ModifyMoveSpeed, TraitTriggerKind.Always, 0.08f, 0f))
        };

        static readonly TraitCardDefinition[] All = BuildAll();

        static TraitCardDefinition[] BuildAll()
        {
            var list = new List<TraitCardDefinition>(160);
            list.AddRange(LegacyGeneral);
            list.AddRange(GeneralNew());
            list.AddRange(ElenaExclusive());
            list.AddRange(CassianExclusive());
            list.AddRange(VeraExclusive());
            list.AddRange(OliviaExclusive());
            list.AddRange(DarioExclusive());
            list.AddRange(IvanExclusive());
            return list.ToArray();
        }

        public static IReadOnlyList<TraitCardDefinition> Definitions => All;

        /// <summary>原有 22 张通用卡(阶段一白名单);数据校验用它做回归守卫。</summary>
        public static IReadOnlyList<TraitCardDefinition> LegacyGeneralDefinitions => LegacyGeneral;

        public static TraitCardDefinition Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static TraitCardDefinition FindByDisplayName(string name)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].DisplayName == name) return All[i];
            return null;
        }

        static Dictionary<string, string> _exclusiveOwnerMap;

        /// <summary>专属卡 → 所属指挥官 Id(§8.1);通用卡返回 null。惰性构建一次。</summary>
        public static string ExclusiveOwner(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_exclusiveOwnerMap == null)
            {
                var map = new Dictionary<string, string>();
                var commanders = CommanderCatalog.GetAll();
                for (int c = 0; c < commanders.Count; c++)
                    for (int i = 0; i < commanders[c].ExclusiveTraitIds.Count; i++)
                        map[commanders[c].ExclusiveTraitIds[i]] = commanders[c].Id;
                _exclusiveOwnerMap = map;
            }
            string owner;
            return _exclusiveOwnerMap.TryGetValue(id, out owner) ? owner : null;
        }

        static TraitCardDefinition Make(string id, string name, string description,
            TraitRarity rarity, int buy, int sell,
            TraitEffect effect = null, string[] tags = null)
        {
            var def = new TraitCardDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                IconAssetId = "Battle/UI/Traits/" + id,
                Rarity = rarity,
                BuyPrice = buy,
                SellPrice = sell,
                StackPolicy = TraitStackPolicy.UniquePerCommander
            };
            if (effect != null) def.Effects.Add(effect);
            if (tags != null) for (int i = 0; i < tags.Length; i++) def.Tags.Add(tags[i]);
            return def;
        }

        static TraitEffect Effect(TraitEffectKind kind, TraitTriggerKind trigger,
            float value, float triggerValue)
        {
            return EffectFull(kind, trigger, value, triggerValue,
                TraitTargetScope.AllGroupMembers, 0f, 1);
        }

        /// <summary>全参效果构造:自定义作用域 / 持续秒数 / 叠加上限。</summary>
        static TraitEffect EffectFull(TraitEffectKind kind, TraitTriggerKind trigger,
            float value, float triggerValue, TraitTargetScope scope, float duration, int maxStacks)
        {
            return new TraitEffect
            {
                Kind = kind,
                Trigger = trigger,
                Scope = scope,
                Value = value,
                TriggerValue = triggerValue,
                DurationSeconds = duration,
                MaxStacks = maxStacks
            };
        }

        /// <summary>自定义作用域,无持续时间。</summary>
        static TraitEffect EffectScoped(TraitEffectKind kind, TraitTriggerKind trigger,
            float value, float triggerValue, TraitTargetScope scope)
        {
            return EffectFull(kind, trigger, value, triggerValue, scope, 0f, 1);
        }

        /// <summary>带持续秒数,全队作用域。</summary>
        static TraitEffect EffectTimed(TraitEffectKind kind, TraitTriggerKind trigger,
            float value, float triggerValue, float duration)
        {
            return EffectFull(kind, trigger, value, triggerValue,
                TraitTargetScope.AllGroupMembers, duration, 1);
        }

        /// <summary>可叠加效果(本场叠加),全队作用域。</summary>
        static TraitEffect EffectStacks(TraitEffectKind kind, TraitTriggerKind trigger,
            float value, float triggerValue, int maxStacks)
        {
            return EffectFull(kind, trigger, value, triggerValue,
                TraitTargetScope.AllGroupMembers, 0f, maxStacks);
        }

        /// <summary>Appends a secondary effect to a card built by Make(...). Returns the def for chaining.</summary>
        static TraitCardDefinition AndEffect(TraitCardDefinition def, TraitEffect effect)
        {
            if (def != null && effect != null) def.Effects.Add(effect);
            return def;
        }
    }
}
