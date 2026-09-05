namespace Mvp.Shared.Skills
{
    /// <summary>
    /// Terrain requirement for a skill, kept independent of Battle.Map.TerrainType so
    /// the skill data layer stays testable without a grid. Grass is mapped to Forest
    /// (战斗技能系统开发文档 §6.1): the project has no separate bush terrain yet, so
    /// 草丛 is treated as Forest until a Bush/Grass terrain is added later.
    /// </summary>
    public enum SkillTerrainKind
    {
        None,
        Forest
    }
}
