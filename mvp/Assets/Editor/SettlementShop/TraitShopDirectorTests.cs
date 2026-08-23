#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mvp.Progression;
using Mvp.SettlementShop;

namespace Mvp.EditorTests.SettlementShop
{
    /// <summary>
    /// EditMode NUnit tests for TraitShopDirector (阶段一 Task B). Pure C#; no
    /// Unity scene state required. Run via the Unity Test Runner window.
    /// </summary>
    public sealed class TraitShopDirectorTests
    {
        [Test]
        public void Roll_同种子同上下文_结果一致()
        {
            var context = HeavyLossContext();
            var a = TraitShopDirector.Roll(12345, context, TraitCatalog.Definitions, 3);
            var b = TraitShopDirector.Roll(12345, context, TraitCatalog.Definitions, 3);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i].Id, b[i].Id);
        }

        [Test]
        public void WeightFor_高损失_defense权重提高()
        {
            var defense = TraitCatalog.Get("trait_cautious");
            Assert.IsNotNull(defense);

            var baseline = new TraitOfferRollContext();
            var heavyLoss = HeavyLossContext();

            Assert.Greater(
                TraitShopDirector.WeightFor(defense, heavyLoss),
                TraitShopDirector.WeightFor(defense, baseline));
        }

        [Test]
        public void WeightFor_同标签大于等于3_权重下降()
        {
            var brave = TraitCatalog.Get("trait_brave");
            Assert.IsNotNull(brave);

            var baseline = new TraitOfferRollContext();
            var owned = new TraitOfferRollContext();
            owned.OwnedTraitTags.Add("attack");
            owned.OwnedTraitTags.Add("attack");
            owned.OwnedTraitTags.Add("attack");

            Assert.Less(
                TraitShopDirector.WeightFor(brave, owned),
                TraitShopDirector.WeightFor(brave, baseline));
        }

        [Test]
        public void Roll_一轮三张_无重复DefinitionId()
        {
            var context = HeavyLossContext();
            var result = TraitShopDirector.Roll(99, context, TraitCatalog.Definitions, 3);

            Assert.AreEqual(3, result.Length);
            var seen = new HashSet<string>();
            for (int i = 0; i < result.Length; i++)
                Assert.IsTrue(seen.Add(result[i].Id),
                    "Duplicate definition id in same roll: " + result[i].Id);
        }

        [Test]
        public void Roll_null池或count不大于0_返回空()
        {
            var context = new TraitOfferRollContext();

            Assert.AreEqual(0, TraitShopDirector.Roll(1, context, null, 3).Length);
            Assert.AreEqual(0, TraitShopDirector.Roll(1, context, TraitCatalog.Definitions, 0).Length);
            Assert.AreEqual(0, TraitShopDirector.Roll(1, context, TraitCatalog.Definitions, -1).Length);
        }

        [Test]
        public void BuildEffectSummary_非空且含真实数值()
        {
            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                string summary = def.BuildEffectSummary();
                Assert.IsNotNull(summary, def.Id + " summary is null");
                Assert.IsNotEmpty(summary, def.Id + " summary empty");
                string expected = ExpectedPercent(def.Effects[0]);
                StringAssert.Contains(expected, summary,
                    def.Id + " missing " + expected);
            }
        }

        [Test]
        public void CollectOwnedTags_含已装备与库存()
        {
            var progression = new PlayerProgressionSnapshot();
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_inventory_1",
                DefinitionId = "trait_brave",          // attack, low_health
                Location = TraitCardLocation.Inventory
            });
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_equipped_1",
                DefinitionId = "trait_guard",          // defense
                Location = TraitCardLocation.Equipped,
                EquippedCommanderId = "commander_1",
                EquippedSlotIndex = 0
            });
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_sold_1",
                DefinitionId = "trait_hold",           // defense, formation
                Location = TraitCardLocation.Sold
            });

            var tags = new List<string>();
            TraitShopDirector.CollectOwnedTags(progression, tags);

            CollectionAssert.Contains(tags, "attack");
            CollectionAssert.Contains(tags, "low_health");
            CollectionAssert.Contains(tags, "defense");
            Assert.IsFalse(tags.Contains("formation"),
                "Sold card tags must be excluded");
        }

        static TraitOfferRollContext HeavyLossContext()
        {
            return new TraitOfferRollContext
            {
                HasBattleResult = true,
                InitialPlayerUnits = 2,
                PlayerUnitsLost = 1
            };
        }

        static string ExpectedPercent(TraitEffect e)
        {
            float v;
            switch (e.Kind)
            {
                case TraitEffectKind.ModifyAttackCooldown:
                case TraitEffectKind.ReduceIncomingDamage:
                    v = -e.Value;
                    break;
                default:
                    v = e.Value;
                    break;
            }
            int p = (int)Math.Round(v * 100f);
            return p >= 0 ? "+" + p + "%" : p + "%";
        }
    }
}
#endif
