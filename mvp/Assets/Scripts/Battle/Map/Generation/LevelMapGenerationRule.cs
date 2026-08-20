using System;
using System.Collections.Generic;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// One "level range -> map settings" rule (随机地图生成接入方案 §7). Pure C# so the
    /// rule-matching and overlap checks can be unit-tested without the editor. A rule is
    /// identified by its stable RuleId; DisplayName is cosmetic only.
    /// </summary>
    [Serializable]
    public sealed class LevelMapGenerationRule
    {
        /// <summary>Stable identity; must not change when the display name is edited.</summary>
        public string RuleId;

        public string DisplayName;

        public int StartLevel = 1;
        public int EndLevel = 1;

        public MapGenerationSettings Settings = new MapGenerationSettings();

        public SeedMode SeedMode = SeedMode.LevelBased;
        public uint FixedSeed = 20260818u;

        public int RetryCount = 10;

        public MapValidationSettings Validation = new MapValidationSettings();

        public bool ContainsLevel(int levelIndex)
        {
            return StartLevel >= 1 && levelIndex >= StartLevel && levelIndex <= EndLevel;
        }

        public LevelMapGenerationRule Clone()
        {
            var c = (LevelMapGenerationRule)MemberwiseClone();
            c.Settings = Settings != null ? Settings.Clone() : new MapGenerationSettings();
            c.Validation = Validation != null ? Validation.Clone() : new MapValidationSettings();
            return c;
        }
    }

    /// <summary>
    /// Static, order-independent rule lookup and configuration validation. Kept separate
    /// from the ScriptableObject so it is testable in the pure-C# verification harness.
    /// </summary>
    public static class LevelRuleResolver
    {
        /// <summary>Returns the rule covering <paramref name="levelIndex"/>, or null. Overlap is assumed pre-validated.</summary>
        public static LevelMapGenerationRule FindRule(IReadOnlyList<LevelMapGenerationRule> rules, int levelIndex)
        {
            if (rules == null) return null;
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r != null && r.ContainsLevel(levelIndex)) return r;
            }
            return null;
        }

        /// <summary>
        /// Validates a rule list; returns null when valid, else a user-facing Chinese error.
        /// Checks StartLevel>=1, EndLevel>=StartLevel, non-empty unique RuleId and no level overlap.
        /// Gaps between ranges are allowed (runtime falls back for those levels).
        /// </summary>
        public static string ValidateConfiguration(IReadOnlyList<LevelMapGenerationRule> rules)
        {
            if (rules == null) return "规则列表为空";
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                if (r.StartLevel < 1) return "规则[" + i + "] " + NameOf(r) + " 起始关卡必须 >= 1";
                if (r.EndLevel < r.StartLevel) return "规则[" + i + "] " + NameOf(r) + " 结束关卡小于起始关卡";
                if (string.IsNullOrEmpty(r.RuleId)) return "规则[" + i + "] " + NameOf(r) + " 缺少 RuleId";

                for (int j = 0; j < rules.Count; j++)
                {
                    if (j == i) continue;
                    var o = rules[j];
                    if (o == null) continue;
                    if (r.RuleId == o.RuleId) return "规则[" + i + "] 与 规则[" + j + "] RuleId 重复: " + r.RuleId;
                    if (Overlaps(r, o)) return "规则[" + i + "] " + NameOf(r) + " 与 规则[" + j + "] " + NameOf(o) + " 关卡范围重叠";
                }
            }
            return null;
        }

        static bool Overlaps(LevelMapGenerationRule a, LevelMapGenerationRule b)
        {
            return a.StartLevel <= b.EndLevel && b.StartLevel <= a.EndLevel;
        }

        static string NameOf(LevelMapGenerationRule r)
        {
            return string.IsNullOrEmpty(r.DisplayName) ? r.RuleId : r.DisplayName;
        }
    }
}
