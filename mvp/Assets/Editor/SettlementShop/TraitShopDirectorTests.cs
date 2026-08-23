#if UNITY_INCLUDE_TESTS
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
                string expected = TraitEffectCatalogExtensions.EffectSummaryLine(def.Effects[0]);
                Assert.IsNotNull(expected, def.Id + " 首条效果无中文渲染");
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

        [Test]
        public void WeightFor_形成中_主流派标签卡_权重大于无Affinity()
        {
            var brave = TraitCatalog.Get("trait_brave"); // attack, low_health
            Assert.IsNotNull(brave);

            var forming = LowHealthBurstForming();
            var baseline = new TraitOfferRollContext();

            Assert.Greater(
                TraitShopDirector.WeightFor(brave, forming),
                TraitShopDirector.WeightFor(brave, baseline));
        }

        [Test]
        public void WeightFor_未成型_主流派无加成()
        {
            var brave = TraitCatalog.Get("trait_brave");
            Assert.IsNotNull(brave);

            var unformed = new TraitOfferRollContext
            {
                Affinity = new BuildAffinitySummary
                {
                    PrimaryArchetypeId = "archetype_low_health_burst",
                    PrimaryScore = 1,
                    Maturity = TraitBuildMaturity.Unformed
                }
            };

            Assert.AreEqual(1.0f, TraitShopDirector.WeightFor(brave, unformed), 0.0001f);
            Assert.AreEqual(1.0f, TraitShopDirector.WeightFor(brave, null), 0.0001f);
        }

        [Test]
        public void WeightFor_主流派标签缺失_无加成()
        {
            var guard = TraitCatalog.Get("trait_guard"); // defense
            Assert.IsNotNull(guard);

            var forming = LowHealthBurstForming(); // low_health, attack, cooldown

            Assert.AreEqual(0.9f, TraitShopDirector.WeightFor(guard, forming), 0.0001f);
            Assert.AreEqual(0.9f, TraitShopDirector.WeightFor(guard, null), 0.0001f);
        }

        [Test]
        public void ArchetypeSynergyMultiplier_按公式权重求和()
        {
            // 期望值绑定 ArchetypeSynergyBoost=0.25:brave=1+2→1.75, swift=1+1→1.5,
            // rapid=1→1.25, guard=0→1.0。调整常量时需同步更新本测试。
            var forming = LowHealthBurstForming();

            Assert.AreEqual(1.75f,
                TraitShopDirector.ArchetypeSynergyMultiplier(TraitCatalog.Get("trait_brave"), forming), 0.0001f);
            Assert.AreEqual(1.50f,
                TraitShopDirector.ArchetypeSynergyMultiplier(TraitCatalog.Get("trait_swift"), forming), 0.0001f);
            Assert.AreEqual(1.25f,
                TraitShopDirector.ArchetypeSynergyMultiplier(TraitCatalog.Get("trait_rapid"), forming), 0.0001f);
            Assert.AreEqual(1.00f,
                TraitShopDirector.ArchetypeSynergyMultiplier(TraitCatalog.Get("trait_guard"), forming), 0.0001f);
        }

        [Test]
        public void Roll_同种子同Affinity_结果一致()
        {
            var a = TraitShopDirector.Roll(777, LowHealthBurstForming(), TraitCatalog.Definitions, 3);
            var b = TraitShopDirector.Roll(777, LowHealthBurstForming(), TraitCatalog.Definitions, 3);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i].Id, b[i].Id);
        }

        [Test]
        public void BuildEligiblePool_选中伊莲娜_池内无其他指挥官专属卡()
        {
            var pool = new List<TraitCardDefinition>();
            TraitShopDirector.BuildEligiblePool(TraitCatalog.Definitions,
                new[] { "commander_elena" }, pool);

            Assert.AreEqual(50, pool.Count, "选中 1 人时应 150 − 100 = 50 张");
            for (int i = 0; i < pool.Count; i++)
            {
                string owner = TraitCatalog.ExclusiveOwner(pool[i].Id);
                Assert.IsTrue(owner == null || owner == "commander_elena",
                    pool[i].Id + " 归属 " + owner + ",不应进伊莲娜卡池");
            }
        }

        [Test]
        public void BuildEligiblePool_无指挥官_仅30张通用卡()
        {
            var pool = new List<TraitCardDefinition>();
            TraitShopDirector.BuildEligiblePool(TraitCatalog.Definitions, null, pool);
            Assert.AreEqual(30, pool.Count, "无指挥官时仅 22 原有 + 8 新增通用卡");

            var emptyPool = new List<TraitCardDefinition>();
            TraitShopDirector.BuildEligiblePool(TraitCatalog.Definitions, new string[0], emptyPool);
            Assert.AreEqual(30, emptyPool.Count, "空 activeIds 等同无指挥官");
            for (int i = 0; i < emptyPool.Count; i++)
                Assert.IsNull(TraitCatalog.ExclusiveOwner(emptyPool[i].Id),
                    emptyPool[i].Id + " 应是通用卡");
        }

        [Test]
        public void CommanderAffinityMultiplier_主方向_1_5倍()
        {
            var context = ElenaAffinityContext();
            Assert.AreEqual(1.5f,
                TraitShopDirector.CommanderAffinityMultiplier(TraitCatalog.Get("trait_brave"), context), 0.0001f);
        }

        [Test]
        public void CommanderAffinityMultiplier_副方向_1_25倍()
        {
            var context = ElenaAffinityContext();
            Assert.AreEqual(1.25f,
                TraitShopDirector.CommanderAffinityMultiplier(TraitCatalog.Get("trait_guard"), context), 0.0001f);
        }

        [Test]
        public void CommanderAffinityMultiplier_无匹配或null_1倍()
        {
            var context = ElenaAffinityContext();
            var commander = TraitCatalog.Get("trait_command"); // 标签 commander,与主副方向无关
            Assert.AreEqual(1.0f,
                TraitShopDirector.CommanderAffinityMultiplier(commander, context), 0.0001f);
            Assert.AreEqual(1.0f,
                TraitShopDirector.CommanderAffinityMultiplier(commander, null), 0.0001f);
            Assert.AreEqual(1.0f,
                TraitShopDirector.CommanderAffinityMultiplier(commander, new TraitOfferRollContext()), 0.0001f);
        }

        [Test]
        public void WeightFor_指挥官主方向_权重乘1_5()
        {
            var brave = TraitCatalog.Get("trait_brave");
            var baseline = new TraitOfferRollContext();
            var main = ElenaAffinityContext();

            Assert.AreEqual(TraitShopDirector.WeightFor(brave, baseline) * 1.5f,
                TraitShopDirector.WeightFor(brave, main), 0.0001f);
        }

        [Test]
        public void Roll_150卡池_平均耗时低于2ms()
        {
            var context = new TraitOfferRollContext();
            // 预热,避免首轮 JIT/惰性构建拖慢均值
            for (int i = 0; i < 5; i++)
                TraitShopDirector.Roll(i, context, TraitCatalog.Definitions, 3);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int iterations = 20;
            for (int i = 0; i < iterations; i++)
                TraitShopDirector.Roll(i, context, TraitCatalog.Definitions, 3);
            watch.Stop();

            double avgMs = watch.Elapsed.TotalMilliseconds / iterations;
            Assert.Less(avgMs, 2.0, "150 卡池单次 Roll 平均耗时 " + avgMs + " ms,应低于 2 ms");
        }

        /// <summary>伊莲娜方向补强上下文:主 {frenzy,attack,low_health} 副 {frenzy,lifesteal,defense}。</summary>
        static TraitOfferRollContext ElenaAffinityContext()
        {
            var affinity = new CommanderAffinityOverride();
            affinity.MainTags.Add("frenzy");
            affinity.MainTags.Add("attack");
            affinity.MainTags.Add("low_health");
            affinity.SubTags.Add("frenzy");
            affinity.SubTags.Add("lifesteal");
            affinity.SubTags.Add("defense");
            return new TraitOfferRollContext { CommanderAffinity = affinity };
        }

        /// <summary>低血爆发流 5 分、正在成型:主流派标签 {low_health:2, attack:1, cooldown:1}。</summary>
        static TraitOfferRollContext LowHealthBurstForming()
        {
            var affinity = new BuildAffinitySummary
            {
                PrimaryArchetypeId = "archetype_low_health_burst",
                PrimaryScore = 5,
                Maturity = TraitBuildMaturity.Forming
            };
            affinity.PrimaryTags.Add(new TraitTagWeight { Tag = "low_health", Weight = 2 });
            affinity.PrimaryTags.Add(new TraitTagWeight { Tag = "attack", Weight = 1 });
            affinity.PrimaryTags.Add(new TraitTagWeight { Tag = "cooldown", Weight = 1 });
            return new TraitOfferRollContext { Affinity = affinity };
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
    }
}
#endif
