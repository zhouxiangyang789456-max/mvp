using System.Collections.Generic;

namespace Mvp.Progression
{
    public static class TraitCatalog
    {
        static readonly TraitCardDefinition[] All =
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
                new[] { "attack", "low_health" })
        };

        public static IReadOnlyList<TraitCardDefinition> Definitions => All;

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
            return new TraitEffect
            {
                Kind = kind,
                Trigger = trigger,
                Scope = TraitTargetScope.AllGroupMembers,
                Value = value,
                TriggerValue = triggerValue,
                DurationSeconds = 0f,
                MaxStacks = 1
            };
        }
    }
}
