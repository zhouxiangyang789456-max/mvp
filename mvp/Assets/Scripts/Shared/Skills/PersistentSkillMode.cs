namespace Mvp.Shared.Skills
{
    /// <summary>Active persistent tactical mode for a commander group. Only one
    /// persistent mode may be active at a time (战斗技能系统开发文档 §3.1).</summary>
    public enum PersistentSkillMode
    {
        None,
        Guard,
        Concealment
    }
}
