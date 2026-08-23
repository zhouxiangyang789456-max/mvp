using System.Collections.Generic;

namespace Mvp.Progression
{
    /// <summary>
    /// 流派成型判定器(§11.1)。分析玩家卡池标签 → 判定正在成型的流派,产出 BuildAffinitySummary。
    /// 公式是数据(注册表),缺失标签计 0,因此扩池加标签时零代码改动。
    /// </summary>
    public static class TraitBuildAnalyzer
    {
        public const int MaturityFormingScore = 4;   // 正在成型
        public const int MaturityFormedScore = 6;   // 流派已成型

        static readonly TraitArchetypeSpec[] ArchetypeRegistry =
        {
            new TraitArchetypeSpec
            {
                Id = "archetype_low_health_burst",
                DisplayName = "低血爆发流",
                Tags = new List<TraitTagWeight>
                {
                    new TraitTagWeight { Tag = "low_health", Weight = 2 },
                    new TraitTagWeight { Tag = "attack", Weight = 1 },
                    new TraitTagWeight { Tag = "cooldown", Weight = 1 }
                }
            },
            new TraitArchetypeSpec
            {
                Id = "archetype_position_defense",
                DisplayName = "阵地防守流",
                Tags = new List<TraitTagWeight>
                {
                    new TraitTagWeight { Tag = "defense", Weight = 2 },
                    new TraitTagWeight { Tag = "formation", Weight = 1 },
                    new TraitTagWeight { Tag = "idle", Weight = 1 }
                }
            },
            new TraitArchetypeSpec
            {
                Id = "archetype_high_health_mobility",
                DisplayName = "高血机动流",
                Tags = new List<TraitTagWeight>
                {
                    new TraitTagWeight { Tag = "mobility", Weight = 2 },
                    new TraitTagWeight { Tag = "high_health", Weight = 1 },
                    new TraitTagWeight { Tag = "attack", Weight = 1 }
                }
            },
            new TraitArchetypeSpec
            {
                Id = "archetype_thick_health_grind",
                DisplayName = "厚血消耗流",
                Tags = new List<TraitTagWeight>
                {
                    new TraitTagWeight { Tag = "max_health", Weight = 2 },
                    new TraitTagWeight { Tag = "defense", Weight = 1 },
                    new TraitTagWeight { Tag = "sustain", Weight = 1 }
                }
            },
            new TraitArchetypeSpec
            {
                Id = "archetype_balanced_command",
                DisplayName = "均衡指挥流",
                Tags = new List<TraitTagWeight>
                {
                    new TraitTagWeight { Tag = "commander", Weight = 2 },
                    new TraitTagWeight { Tag = "balanced", Weight = 1 },
                    new TraitTagWeight { Tag = "support", Weight = 1 }
                }
            }
        };

        /// <summary>流派注册表;注册表顺序即同分平局规则。</summary>
        public static IReadOnlyList<TraitArchetypeSpec> Archetypes => ArchetypeRegistry;

        /// <summary>快照 → 标签计数 → 判定,委托给纯逻辑重载。</summary>
        public static BuildAffinitySummary Analyze(PlayerProgressionSnapshot progression)
        {
            var counts = new Dictionary<string, int>();
            BuildTagCounts(progression, counts);
            return Analyze(counts);
        }

        /// <summary>纯逻辑核心:直接按标签计数判定流派,可测接缝。</summary>
        public static BuildAffinitySummary Analyze(IReadOnlyDictionary<string, int> tagCounts)
        {
            if (tagCounts == null) return SelectSummary(new Dictionary<string, int>());
            return SelectSummary(tagCounts);
        }

        public static TraitBuildMaturity GetMaturity(int primaryScore)
        {
            if (primaryScore >= MaturityFormedScore) return TraitBuildMaturity.Formed;
            if (primaryScore >= MaturityFormingScore) return TraitBuildMaturity.Forming;
            return TraitBuildMaturity.Unformed;
        }

        /// <summary>Σ weight(tag) * count(tag),缺失标签计 0。</summary>
        public static int ScoreFor(TraitArchetypeSpec archetype,
            IReadOnlyDictionary<string, int> tagCounts)
        {
            if (archetype == null || archetype.Tags == null || tagCounts == null) return 0;
            int score = 0;
            for (int i = 0; i < archetype.Tags.Count; i++)
            {
                var w = archetype.Tags[i];
                if (w == null || string.IsNullOrEmpty(w.Tag)) continue;
                int count;
                tagCounts.TryGetValue(w.Tag, out count);
                score += w.Weight * count;
            }
            return score;
        }

        /// <summary>完全镜像 CollectOwnedTags 语义:跳过 null 卡、非 Inventory/Equipped、null def、null/空标签。</summary>
        static void BuildTagCounts(PlayerProgressionSnapshot progression, Dictionary<string, int> output)
        {
            output.Clear();
            if (progression == null) return;
            for (int i = 0; i < progression.TraitCards.Count; i++)
            {
                var card = progression.TraitCards[i];
                if (card == null ||
                    (card.Location != TraitCardLocation.Inventory &&
                     card.Location != TraitCardLocation.Equipped))
                    continue;
                var def = TraitCatalog.Get(card.DefinitionId);
                if (def == null || def.Tags == null) continue;
                for (int t = 0; t < def.Tags.Count; t++)
                {
                    string tag = def.Tags[t];
                    if (string.IsNullOrEmpty(tag)) continue;
                    int current;
                    output.TryGetValue(tag, out current);
                    output[tag] = current + 1;
                }
            }
        }

        /// <summary>
        /// 确定性选择:主=注册表顺序首个最高分;主分 0 时主副均 null;
        /// 副=首个「&gt;0 且 ≠ 主」的最高分;推荐标签按可行动性排序。
        /// </summary>
        static BuildAffinitySummary SelectSummary(IReadOnlyDictionary<string, int> counts)
        {
            var summary = new BuildAffinitySummary();
            foreach (var kv in counts) summary.TagCounts[kv.Key] = kv.Value; // 防御性拷贝

            int bestScore = 0;
            TraitArchetypeSpec primary = null;
            for (int i = 0; i < ArchetypeRegistry.Length; i++)
            {
                int score = ScoreFor(ArchetypeRegistry[i], summary.TagCounts);
                if (score > bestScore) { bestScore = score; primary = ArchetypeRegistry[i]; }
            }

            if (primary == null || bestScore <= 0)
            {
                summary.PrimaryScore = 0;
                summary.PrimaryArchetypeId = null;
                summary.SecondaryArchetypeId = null;
                return summary;
            }

            summary.PrimaryScore = bestScore;
            summary.PrimaryArchetypeId = primary.Id;

            TraitArchetypeSpec secondary = null;
            int secondaryScore = 0;
            for (int i = 0; i < ArchetypeRegistry.Length; i++)
            {
                var archetype = ArchetypeRegistry[i];
                if (archetype == primary) continue;
                int score = ScoreFor(archetype, summary.TagCounts);
                if (score > 0 && score > secondaryScore) { secondaryScore = score; secondary = archetype; }
            }
            summary.SecondaryArchetypeId = secondary != null ? secondary.Id : null;

            summary.RecommendedTags.AddRange(BuildRecommendedTags(primary, summary.TagCounts));
            return summary;
        }

        /// <summary>主流派公式标签,排序:权重降序(旗舰 *2 在前)→ 当前计数升序(0 张先出)→ 标签字符串序。</summary>
        static List<string> BuildRecommendedTags(TraitArchetypeSpec primary,
            IReadOnlyDictionary<string, int> counts)
        {
            var tags = new List<string>(primary.Tags.Count);
            for (int i = 0; i < primary.Tags.Count; i++)
            {
                var w = primary.Tags[i];
                if (w != null && !string.IsNullOrEmpty(w.Tag)) tags.Add(w.Tag);
            }
            tags.Sort((x, y) =>
            {
                int weightCompare = WeightIn(primary, y).CompareTo(WeightIn(primary, x)); // 降序
                if (weightCompare != 0) return weightCompare;
                int countCompare = CountIn(counts, x).CompareTo(CountIn(counts, y));     // 升序
                if (countCompare != 0) return countCompare;
                return string.CompareOrdinal(x, y);                                        // 稳定平局
            });
            return tags;
        }

        static int WeightIn(TraitArchetypeSpec archetype, string tag)
        {
            for (int i = 0; i < archetype.Tags.Count; i++)
            {
                var w = archetype.Tags[i];
                if (w != null && w.Tag == tag) return w.Weight;
            }
            return 0;
        }

        static int CountIn(IReadOnlyDictionary<string, int> counts, string tag)
        {
            int count;
            counts.TryGetValue(tag, out count);
            return count;
        }
    }

    /// <summary>流派定义:Id + 展示名 + 标签权重公式。</summary>
    public sealed class TraitArchetypeSpec
    {
        public string Id;
        public string DisplayName;
        public IReadOnlyList<TraitTagWeight> Tags;
    }

    /// <summary>公式中的 (tag, weight) 项。</summary>
    public sealed class TraitTagWeight
    {
        public string Tag;
        public int Weight;
    }
}
