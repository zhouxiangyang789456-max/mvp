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
        ModifyCommanderMorale
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
        AfterRegroup
    }

    /// <summary>Which units of the commander group an effect applies to.</summary>
    public enum TraitTargetScope
    {
        CommanderOnly,
        AllGroupMembers,
        FrontlineMembers,
        RangedMembers,
        LowestHealthMember
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
