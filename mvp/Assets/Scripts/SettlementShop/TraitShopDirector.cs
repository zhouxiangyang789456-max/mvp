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
        const int SameTagThreshold = 3;

        public static TraitCardDefinition[] Roll(int seed, TraitOfferRollContext context,
            IReadOnlyList<TraitCardDefinition> pool, int count)
        {
            if (pool == null || count <= 0) return new TraitCardDefinition[0];
            var candidates = new List<TraitCardDefinition>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) candidates.Add(pool[i]);

            var random = new Random(seed);
            var result = new List<TraitCardDefinition>(Math.Min(count, candidates.Count));
            while (result.Count < count && candidates.Count > 0)
            {
                float totalWeight = 0f;
                for (int i = 0; i < candidates.Count; i++)
                    totalWeight += WeightFor(candidates[i], context);
                if (totalWeight <= 0f) break;

                double roll = random.NextDouble() * totalWeight;
                float acc = 0f;
                int pick = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float w = WeightFor(candidates[i], context);
                    if (w <= 0f) continue;
                    acc += w;
                    if (roll <= acc) { pick = i; break; }
                }
                result.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }
            return result.ToArray();
        }

        public static float WeightFor(TraitCardDefinition def, TraitOfferRollContext context)
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
                    if (CountOwnedTag(context, tag) >= SameTagThreshold)
                        weight *= SameTagDebuff;
                }
            }

            if (context.RefreshCount >= 2 && def.Rarity != TraitRarity.Common)
                weight *= NonCommonRefreshBoost;
            return weight;
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

        static int CountOwnedTag(TraitOfferRollContext context, string tag)
        {
            if (context == null) return 0;
            int count = 0;
            for (int i = 0; i < context.OwnedTraitTags.Count; i++)
                if (context.OwnedTraitTags[i] == tag) count++;
            return count;
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
