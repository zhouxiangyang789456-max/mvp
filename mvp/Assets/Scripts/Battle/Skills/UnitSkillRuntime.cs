namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Per-unit skill lifecycle state. Special skills flow
    /// Ready -&gt; Targeting / Active -&gt; Cooldown -&gt; Ready
    /// (战斗技能系统开发文档 §3.2). Persistent skills stay Ready.
    /// </summary>
    public enum SkillRuntimeState
    {
        Ready,
        Targeting,
        Active,
        Cooldown
    }

    /// <summary>
    /// Runtime skill state for one unit in a commander group. Never written back to
    /// UnitDefinition; static config and battle state stay separate (§4.3).
    /// </summary>
    public sealed class UnitSkillRuntime
    {
        public string SkillId;
        public SkillRuntimeState State = SkillRuntimeState.Ready;
        public float ActiveUntil;
        public float CooldownUntil;
        public bool IsEligible;
    }
}
