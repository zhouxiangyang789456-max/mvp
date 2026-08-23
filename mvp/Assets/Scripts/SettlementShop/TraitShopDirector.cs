using System;
using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.SettlementShop
{
    /// <summary>
    /// Local shop director: weighted, reproducible, without-replacement sampling of
    /// trait offers from battle performance. Uses System.Random(seed) so the same
    /// seed + context reproduces the same offers.
    /// </summary>
    public static class TraitShopDirector
    {
        const float CommonWeight = 1.0f;
        const float RareWeight = 0.9f;
        const float EpicWeight = 0.7f;
        const float DefenseBoostOnHeavyLoss = 1.6f;
        const float AttackBoostOnLightLoss = 1.3f;
        const float SameTagDebuff = 0.5f;
        const float NonCommonRefreshBoost = 1.2f;
        const float ArchetypeSynergyBoost = 0.25f; // 每点主流派公式权重加成;可调
        const float CommanderMainDirectionBoost = 1.5f; // §8.3 主方向补强
        const float CommanderSubDirectionBoost = 1.25f; // §8.3 副方向补强
        const int SameTagThreshold = 3;

        public static TraitCardDefinition[] Roll(int seed, TraitOfferRollContext context,
            IReadOnlyList<TraitCardDefinition> pool, int count)
        {
            if (pool == null || count <= 0) return new TraitCardDefinition[0];
            var candidates = new List<TraitCardDefinition>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) candidates.Add(pool[i]);

            // §11.1.1 一次构建标签计数表;权重预计算进并行数组,removeAt 后左移保持并行。
            var tagCounts = BuildTagCountTable(context);
            var weights = new float[candidates.Count];
            var random = new Random(seed);
            var result = new List<TraitCardDefinition>(Math.Min(count, candidates.Count));
            bool weightsDirty = true;
            while (result.Count < count && candidates.Count > 0)
            {
                if (weightsDirty)
                {
                    for (int i = 0; i < candidates.Count; i++)
                        weights[i] = WeightFor(candidates[i], context, tagCounts);
                    weightsDirty = false;
                }
                float totalWeight = 0f;
                for (int i = 0; i < candidates.Count; i++)
                    totalWeight += weights[i];
                if (totalWeight <= 0f) break;

                double roll = random.NextDouble() * totalWeight;
                float acc = 0f;
                int pick = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float w = weights[i];
                    if (w <= 0f) continue;
                    acc += w;
                    if (roll <= acc) { pick = i; break; }
                }
                result.Add(candidates[pick]);
                candidates.RemoveAt(pick);
                for (int i = pick; i < candidates.Count; i++)
                    weights[i] = weights[i + 1];
            }
            return result.ToArray();
        }

        public static float WeightFor(TraitCardDefinition def, TraitOfferRollContext context)
        {
            return WeightFor(def, context, BuildTagCountTable(context));
        }

        /// <summary>带标签计数表的内部重载;单次 Roll 内建表一次,多卡复用查表。</summary>
        static float WeightFor(TraitCardDefinition def, TraitOfferRollContext context,
            Dictionary<string, int> tagCounts)
        {
            if (def == null) return 0f;
            float weight = BaseWeight(def.Rarity);
            if (context == null) return weight;

            bool isDefense = def.HasTag("defense");
            bool isAttack = def.HasTag("attack");
            if (context.LossRatio >= 0.4f && isDefense)
                weight *= DefenseBoostOnHeavyLoss;
            else if (context.LossRatio > 0f && context.LossRatio < 0.4f && isAttack)
                weight *= AttackBoostOnLightLoss;

            if (def.Tags != null)
            {
                for (int t = 0; t < def.Tags.Count; t++)
                {
                    string tag = def.Tags[t];
                    if (string.IsNullOrEmpty(tag)) continue;
                    if (CountOwnedTag(tagCounts, tag) >= SameTagThreshold)
                        weight *= SameTagDebuff;
                }
            }

            if (context.RefreshCount >= 2 && def.Rarity != TraitRarity.Common)
                weight *= NonCommonRefreshBoost;
            weight *= ArchetypeSynergyMultiplier(def, context);
            weight *= CommanderAffinityMultiplier(def, context);
            return weight;
        }

        /// <summary>
        /// 专属卡池过滤(§8.1):专属卡仅当其所属指挥官在 <paramref name="activeCommanderIds"/>
        /// 时进入输出池;通用卡恒进。纯静态 helper,确定性由调用方(会话)保持。
        /// </summary>
        public static void BuildEligiblePool(IReadOnlyList<TraitCardDefinition> source,
            IReadOnlyCollection<string> activeCommanderIds, List<TraitCardDefinition> output)
        {
            output.Clear();
            if (source == null) return;
            var active = new HashSet<string>();
            if (activeCommanderIds != null)
                foreach (var id in activeCommanderIds)
                    if (!string.IsNullOrEmpty(id)) active.Add(id);
            for (int i = 0; i < source.Count; i++)
            {
                var def = source[i];
                if (def == null) continue;
                string owner = TraitCatalog.ExclusiveOwner(def.Id);
                if (owner == null || active.Contains(owner)) output.Add(def);
            }
        }

        /// <summary>
        /// 指挥官方向补强(§8.3):候选卡携带所选指挥官主方向任一公式标签(含独占标签)→ ×1.5;
        /// 否则副方向 → ×1.25;否则 ×1。独占标签在主副都出现,故所选指挥官的专属卡基本吃 ×1.5。
        /// </summary>
        public static float CommanderAffinityMultiplier(TraitCardDefinition def,
            TraitOfferRollContext context)
        {
            if (def == null || context == null || context.CommanderAffinity == null) return 1f;
            var c = context.CommanderAffinity;
            for (int i = 0; i < c.MainTags.Count; i++)
                if (def.HasTag(c.MainTags[i])) return CommanderMainDirectionBoost;
            for (int i = 0; i < c.SubTags.Count; i++)
                if (def.HasTag(c.SubTags[i])) return CommanderSubDirectionBoost;
            return 1f;
        }

        /// <summary>
        /// 流派补强加成:成型后(非 Unformed)候选卡携带的主流派公式标签越多,权重越高。
        /// 乘性、阈值以下恒 1;Director 只读 BuildAffinitySummary,不依赖 TraitBuildAnalyzer。
        /// </summary>
        public static float ArchetypeSynergyMultiplier(TraitCardDefinition def,
            TraitOfferRollContext context)
        {
            if (def == null || context == null || context.Affinity == null) return 1f;
            var affinity = context.Affinity;
            if (affinity.PrimaryArchetypeId == null) return 1f;
            if (affinity.Maturity == TraitBuildMaturity.Unformed) return 1f; // 仅形成/已成型
            if (affinity.PrimaryTags == null || affinity.PrimaryTags.Count == 0) return 1f;
            int synergy = 0;
            for (int i = 0; i < affinity.PrimaryTags.Count; i++)
            {
                var w = affinity.PrimaryTags[i];
                if (w == null || string.IsNullOrEmpty(w.Tag)) continue;
                if (def.HasTag(w.Tag)) synergy += w.Weight;
            }
            return synergy <= 0 ? 1f : 1f + ArchetypeSynergyBoost * synergy;
        }

        /// <summary>Collects tags of all inventory + equipped cards into <paramref name="output"/>.</summary>
        public static void CollectOwnedTags(PlayerProgressionSnapshot progression, List<string> output)
        {
            if (output == null) return;
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
                    if (!string.IsNullOrEmpty(def.Tags[t])) output.Add(def.Tags[t]);
            }
        }

        /// <summary>从 OwnedTraitTags 构建一次标签计数表(§11.1.1)。</summary>
        static Dictionary<string, int> BuildTagCountTable(TraitOfferRollContext context)
        {
            var table = new Dictionary<string, int>();
            if (context == null || context.OwnedTraitTags == null) return table;
            for (int i = 0; i < context.OwnedTraitTags.Count; i++)
            {
                string tag = context.OwnedTraitTags[i];
                if (string.IsNullOrEmpty(tag)) continue;
                int current;
                table.TryGetValue(tag, out current);
                table[tag] = current + 1;
            }
            return table;
        }

        static int CountOwnedTag(Dictionary<string, int> tagCounts, string tag)
        {
            if (tagCounts == null || string.IsNullOrEmpty(tag)) return 0;
            int count;
            return tagCounts.TryGetValue(tag, out count) ? count : 0;
        }

        static float BaseWeight(TraitRarity rarity)
        {
            switch (rarity)
            {
                case TraitRarity.Common: return CommonWeight;
                case TraitRarity.Rare: return RareWeight;
                case TraitRarity.Epic: return EpicWeight;
                default: return 0.6f;
            }
        }
    }
}
