using System;

namespace Mvp.Progression
{
    /// <summary>
    /// Structured effect kinds. The phase-1 battle service only resolves the
    /// first five; the remainder are defined for future phases (计划文档 14.8).
    /// </summary>
    public enum TraitEffectKind
    {
        ModifyMaxHealth,
        ModifyAttackPower,
        ModifyAttackCooldown,
        ModifyMoveSpeed,
        ReduceIncomingDamage,
        ModifyHealingReceived,
        GrantOpeningShield,
        ModifyCommanderMorale,
        // 以下为 150 卡重构新增效果(只加不改,旧值位置不变)。
        ModifyAttackRange,      // 攻击范围,正值为增大(奥莉薇射程)
        ModifyCriticalChance,   // 暴击率,正值 = 概率提升
        ModifyCriticalDamage,   // 暴击伤害,正值 = 倍率提升
        ModifyLifeSteal,        // 吸血,正值 = 造成伤害百分比回血
        GrantShield,            // 护盾,正值 = 获得最大生命百分比护盾
        ApplyBurn,              // 灼烧,正值 = 每秒受攻百分比伤害,DurationSeconds 为持续秒数
        SlowEnemy,              // 减速敌人,正值 = 敌人移速下降幅度
        ModifyDamageVsSlowed,   // 对减速敌增伤
        ExecuteBonus,           // 斩杀增伤,对低血敌人
        ReflectDamage,          // 反弹伤害,正值 = 反弹所受伤害百分比
        EconomyModifier,        // 经济加成,商店金币结算乘区
        GoldOnWin,              // 胜利额外金币
        GoldOnKill,             // 击杀额外金币
        FirstStrikeBonus,       // 先手加成,开场 N 秒增攻
        ReduceEnemyDefense,     // 破甲,敌人防御下降
        StunEnemy,              // 眩晕敌人,Value = 秒数
        RegenerateHealth        // 每秒回复编队生命百分比(战地医师)
    }

    /// <summary>
    /// Condition kinds. Phase 1 resolves only Always / WhileGroupHealthBelowPercent /
    /// WhileGroupHealthAbovePercent; the rest are reserved for later phases.
    /// </summary>
    public enum TraitTriggerKind
    {
        Always,
        OnBattleStart,
        WhileGroupHealthBelowPercent,
        WhileGroupHealthAbovePercent,
        WhileGroupIdle,
        OnFirstHitTaken,
        OnFirstAttack,
        WhileFormationIntact,
        AfterRegroup,
        // 以下为 150 卡重构新增触发(只加不改)。
        OnEnemyDeath,          // 敌人死亡时
        OnHit,                 // 攻击命中时
        OnReceiveHit,          // 受到攻击时
        WhileEnemyBurning,     // 敌人处于灼烧时
        WhileEnemySlowed,      // 敌人处于减速时
        WhileEnemyLowHealth,   // 敌人生命低于阈值时
        BasedOnHealthLost,     // 按损失生命插值(每损失 TriggerValue 生效一次)
        BasedOnGold,           // 按当前金币插值(每 TriggerValue 金币生效一次)
        OnFriendlyUnitLost     // 我方损失单位时(血海深仇)
    }

    /// <summary>Which units of the commander group an effect applies to.</summary>
    public enum TraitTargetScope
    {
        CommanderOnly,
        AllGroupMembers,
        FrontlineMembers,
        RangedMembers,
        LowestHealthMember,
        // 以下为 150 卡重构新增作用域(只加不改,统一敌方目标选取)。
        EnemyAll,           // 所有敌人
        EnemyFrontline,     // 敌方前排
        EnemyLowestHealth,  // 敌方最低生命单体(处决目标)
        EnemyBurning,       // 灼烧中的敌人
        EnemySlowed,        // 减速中的敌人
        NearbyEnemies       // 编队周围敌人(阵地控场)
    }

    /// <summary>
    /// One structured effect of a trait card. Value semantics (13.2):
    /// positive Value raises the stat for add-kinds (attack / move / max health)
    /// and lowers it for reduction-kinds (cooldown / incoming damage).
    /// </summary>
    [Serializable]
    public sealed class TraitEffect
    {
        public TraitEffectKind Kind;
        public TraitTriggerKind Trigger;
        public TraitTargetScope Scope;
        public float Value;          // magnitude as a decimal fraction (0.20 = +20%)
        public float TriggerValue;   // trigger threshold (0.35 = health < 35%)
        public float DurationSeconds;
        public int MaxStacks;
        public string[] Tags;
    }
}
