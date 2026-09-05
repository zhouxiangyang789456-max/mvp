using System.Collections.Generic;

namespace Mvp.Shared.Skills
{
    /// <summary>
    /// Static registry of skill definitions (战斗技能系统开发文档 §4.1 / §12).
    /// First-version numbers come from the doc and are kept here so controllers only
    /// read config; tuning happens without touching logic.
    /// </summary>
    public static class SkillCatalog
    {
        static readonly Dictionary<string, SkillDefinition> _definitions;

        static SkillCatalog()
        {
            _definitions = new Dictionary<string, SkillDefinition>
            {
                {
                    SkillIds.Guard,
                    new SkillDefinition
                    {
                        Id = SkillIds.Guard,
                        DisplayName = "坚守",
                        Category = SkillCategory.Persistent,
                        TargetMode = SkillTargetMode.None,
                        RequiredUnitTags = UnitTag.None,
                        RequiredTerrain = SkillTerrainKind.None,
                        DurationSeconds = 0f,
                        CooldownSeconds = 0f,
                        RangeMultiplier = 1f,
                        MoveSpeedMultiplier = 1f
                    }
                },
                {
                    SkillIds.Concealment,
                    new SkillDefinition
                    {
                        Id = SkillIds.Concealment,
                        DisplayName = "隐蔽",
                        Category = SkillCategory.Persistent,
                        TargetMode = SkillTargetMode.None,
                        RequiredUnitTags = UnitTag.None,
                        RequiredTerrain = SkillTerrainKind.Forest,
                        DurationSeconds = 0f,
                        CooldownSeconds = 0f,
                        RangeMultiplier = 1f,
                        MoveSpeedMultiplier = 1f
                    }
                },
                {
                    SkillIds.LongRange,
                    new SkillDefinition
                    {
                        Id = SkillIds.LongRange,
                        DisplayName = "远攻",
                        Category = SkillCategory.Special,
                        TargetMode = SkillTargetMode.Cell,
                        RequiredUnitTags = UnitTag.Ranged,
                        RequiredTerrain = SkillTerrainKind.None,
                        DurationSeconds = 0f,
                        CooldownSeconds = 8f,
                        RangeMultiplier = 1.5f,
                        MoveSpeedMultiplier = 1f
                    }
                },
                {
                    SkillIds.Sprint,
                    new SkillDefinition
                    {
                        Id = SkillIds.Sprint,
                        DisplayName = "冲刺",
                        Category = SkillCategory.Special,
                        TargetMode = SkillTargetMode.None,
                        RequiredUnitTags = UnitTag.Tank,
                        RequiredTerrain = SkillTerrainKind.None,
                        DurationSeconds = 4f,
                        CooldownSeconds = 12f,
                        RangeMultiplier = 1f,
                        MoveSpeedMultiplier = 1.8f
                    }
                },
                {
                    SkillIds.Taunt,
                    new SkillDefinition
                    {
                        Id = SkillIds.Taunt,
                        DisplayName = "嘲讽",
                        Category = SkillCategory.Special,
                        TargetMode = SkillTargetMode.None,
                        RequiredUnitTags = UnitTag.None,
                        RequiredTerrain = SkillTerrainKind.None,
                        DurationSeconds = 5f,
                        CooldownSeconds = 12f,
                        RangeMultiplier = 1f,
                        MoveSpeedMultiplier = 1f,
                        RangeCells = 6
                    }
                },
                {
                    SkillIds.Decoy,
                    new SkillDefinition
                    {
                        Id = SkillIds.Decoy,
                        DisplayName = "疑兵",
                        Category = SkillCategory.Special,
                        TargetMode = SkillTargetMode.Cell,
                        RequiredUnitTags = UnitTag.None,
                        RequiredTerrain = SkillTerrainKind.None,
                        DurationSeconds = 8f,
                        CooldownSeconds = 18f,
                        RangeMultiplier = 1f,
                        MoveSpeedMultiplier = 1f,
                        RangeCells = 4,
                        EffectRangeCells = 6,
                        EffectHealth = 80
                    }
                }
            };
        }

        public static SkillDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            SkillDefinition def;
            return _definitions.TryGetValue(id, out def) ? def : null;
        }

        public static IReadOnlyCollection<SkillDefinition> All { get { return _definitions.Values; } }
    }
}
