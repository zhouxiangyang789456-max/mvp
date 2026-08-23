namespace Mvp.Progression
{
    public static partial class TraitCatalog
    {
        /// <summary>新增通用卡 8 张(全指挥官共享)。</summary>
        static TraitCardDefinition[] GeneralNew()
        {
            return new[]
            {
                Make("trait_field_medic", "战地医师",
                    "始终每秒回复编队生命 1%。", TraitRarity.Rare, 7, 3,
                    Effect(TraitEffectKind.RegenerateHealth, TraitTriggerKind.Always, 0.01f, 0f),
                    new[] { "sustain", "defense" }),
                Make("trait_quartermaster", "军需官",
                    "始终提高金币获取 8%。", TraitRarity.Common, 5, 2,
                    Effect(TraitEffectKind.EconomyModifier, TraitTriggerKind.Always, 0.08f, 0f),
                    new[] { "economy", "commander" }),
                Make("trait_scout_balloon", "侦察气球",
                    "开战时,先手攻击力提高 10%,持续 3 秒。", TraitRarity.Common, 5, 2,
                    EffectTimed(TraitEffectKind.FirstStrikeBonus, TraitTriggerKind.OnBattleStart,
                        0.10f, 0f, 3f),
                    new[] { "first_strike", "attack" }),
                AndEffect(
                    Make("trait_legion_banner", "军团旗手",
                        "始终提高生命上限 5% 与攻击力 3%。", TraitRarity.Common, 5, 2,
                        Effect(TraitEffectKind.ModifyMaxHealth, TraitTriggerKind.Always, 0.05f, 0f),
                        new[] { "commander", "support" }),
                    Effect(TraitEffectKind.ModifyAttackPower, TraitTriggerKind.Always, 0.03f, 0f)),
                Make("trait_double_ammo", "双倍弹药",
                    "始终降低攻击冷却 10%。", TraitRarity.Rare, 7, 3,
                    Effect(TraitEffectKind.ModifyAttackCooldown, TraitTriggerKind.Always, 0.10f, 0f),
                    new[] { "cooldown", "attack" }),
                Make("trait_tracking_mark", "追踪标记",
                    "攻击命中时,对敌方生命最低单位破甲 8%。", TraitRarity.Rare, 7, 3,
                    EffectScoped(TraitEffectKind.ReduceEnemyDefense, TraitTriggerKind.OnHit,
                        0.08f, 0f, TraitTargetScope.EnemyLowestHealth),
                    new[] { "attack", "armor_pen" }),
                Make("trait_battle_cry", "战场呐喊",
                    "编队生命低于 50% 时,攻击力提高 12%。", TraitRarity.Rare, 7, 3,
                    Effect(TraitEffectKind.ModifyAttackPower,
                        TraitTriggerKind.WhileGroupHealthBelowPercent, 0.12f, 0.50f),
                    new[] { "low_health", "commander" }),
                AndEffect(
                    Make("trait_war_horn", "战争号角",
                        "开战时,攻击力提高 10%,移动速度提高 5%。", TraitRarity.Rare, 7, 3,
                        Effect(TraitEffectKind.ModifyAttackPower, TraitTriggerKind.OnBattleStart,
                            0.10f, 0f),
                        new[] { "commander", "first_strike" }),
                    Effect(TraitEffectKind.ModifyMoveSpeed, TraitTriggerKind.OnBattleStart, 0.05f, 0f))
            };
        }
    }
}
