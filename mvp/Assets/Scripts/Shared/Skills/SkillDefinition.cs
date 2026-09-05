namespace Mvp.Shared.Skills
{
    /// <summary>
    /// Static skill definition (战斗技能系统开发文档 §4.1). Values come from
    /// SkillCatalog so runtime controllers never hard-code tuning numbers.
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Id;
        public string DisplayName;
        public SkillCategory Category;
        public SkillTargetMode TargetMode;
        public UnitTag RequiredUnitTags;
        public SkillTerrainKind RequiredTerrain;
        public float DurationSeconds;
        public float CooldownSeconds;
        public float RangeMultiplier;
        public float MoveSpeedMultiplier;
        public int RangeCells;
        public int EffectRangeCells;
        public int EffectHealth;
        public string IconAssetId;
        public string CursorAssetId;
    }
}
