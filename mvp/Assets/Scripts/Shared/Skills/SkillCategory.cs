namespace Mvp.Shared.Skills
{
    /// <summary>Skill category (战斗技能系统开发文档 §3). Persistent skills are
    /// always-on tactical modes; special skills have an explicit cast flow.</summary>
    public enum SkillCategory
    {
        Persistent,
        Special
    }
}
