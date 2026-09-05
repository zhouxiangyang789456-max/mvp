#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Mvp.Progression;
using Mvp.CommanderSelect;
using Mvp.Shared;

namespace Mvp.EditorTests.Progression
{
    /// <summary>
    /// 阶段一数据校验:150 卡池 / 稀有度分布 / 6×20 专属卡 / 独占标签唯一性 /
    /// 效果渲染 / 价格规则。纯数据,不依赖 Unity 场景。Run via the Unity Test Runner.
    /// </summary>
    public sealed class TraitCatalogDataValidationTests
    {
        [Test]
        public void 卡池总数_150张()
        {
            Assert.AreEqual(150, TraitCatalog.Definitions.Count);
        }

        [Test]
        public void 稀有度分布_58普通_63稀有_29史诗_0传说()
        {
            Assert.AreEqual(58, CountRarity(TraitRarity.Common));
            Assert.AreEqual(63, CountRarity(TraitRarity.Rare));
            Assert.AreEqual(29, CountRarity(TraitRarity.Epic));
            Assert.AreEqual(0, CountRarity(TraitRarity.Legendary));
        }

        [Test]
        public void 每张卡_至少一个效果与一个标签_且标签无重复()
        {
            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                Assert.IsTrue(def.Effects != null && def.Effects.Count > 0,
                    def.Id + " 无结构化效果");
                Assert.IsTrue(def.Tags != null && def.Tags.Count > 0,
                    def.Id + " 无标签");

                var seen = new HashSet<string>();
                for (int t = 0; t < def.Tags.Count; t++)
                {
                    var tag = def.Tags[t];
                    Assert.IsFalse(string.IsNullOrEmpty(tag), def.Id + " 含空标签");
                    Assert.IsTrue(seen.Add(tag), def.Id + " 标签重复: " + tag);
                }
            }
        }

        [Test]
        public void 每个效果_均有中文渲染()
        {
            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                for (int e = 0; e < def.Effects.Count; e++)
                {
                    string line = TraitEffectCatalogExtensions.EffectSummaryLine(def.Effects[e]);
                    Assert.IsNotNull(line, def.Id + " 效果 " + e + " 无中文渲染");
                }
            }
        }

        [Test]
        public void 价格_买卖价合理_且新增卡按稀有度定价()
        {
            var legacyIds = new HashSet<string>();
            for (int i = 0; i < TraitCatalog.LegacyGeneralDefinitions.Count; i++)
                legacyIds.Add(TraitCatalog.LegacyGeneralDefinitions[i].Id);

            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                Assert.Greater(def.BuyPrice, def.SellPrice, def.Id + " 买入价应高于卖出价");
                Assert.Greater(def.SellPrice, 0, def.Id + " 卖出价应大于 0");

                if (legacyIds.Contains(def.Id)) continue; // 原有卡沿用旧价(史诗 9/4 或 10/5)

                switch (def.Rarity)
                {
                    case TraitRarity.Common:
                        Assert.AreEqual(5, def.BuyPrice, def.Id + " 普通买入价");
                        Assert.AreEqual(2, def.SellPrice, def.Id + " 普通卖出价");
                        break;
                    case TraitRarity.Rare:
                        Assert.AreEqual(7, def.BuyPrice, def.Id + " 稀有买入价");
                        Assert.AreEqual(3, def.SellPrice, def.Id + " 稀有卖出价");
                        break;
                    case TraitRarity.Epic:
                        Assert.AreEqual(10, def.BuyPrice, def.Id + " 史诗买入价");
                        Assert.AreEqual(5, def.SellPrice, def.Id + " 史诗卖出价");
                        break;
                    default:
                        Assert.Fail(def.Id + " 存在传说级卡,重构阶段一不应有");
                        break;
                }
            }
        }

        [Test]
        public void 指挥官专属卡_各20张_全部解析且携带独占标签()
        {
            var commanders = CommanderCatalog.GetAll();
            Assert.AreEqual(6, commanders.Count);

            var allExclusiveIds = new HashSet<string>();
            for (int c = 0; c < commanders.Count; c++)
            {
                var commander = commanders[c];
                var ids = commander.ExclusiveTraitIds;
                Assert.AreEqual(20, ids.Count,
                    commander.Id + " 专属卡数量应为 20");

                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    Assert.IsTrue(allExclusiveIds.Add(id),
                        "专属卡 id 跨指挥官重复: " + id);

                    var card = TraitCatalog.Get(id);
                    Assert.IsNotNull(card, commander.Id + " 专属卡未注册: " + id);
                    Assert.IsTrue(card.HasTag(commander.ExclusiveTag),
                        id + " 未携带独占标签 " + commander.ExclusiveTag);
                }
            }
            Assert.AreEqual(120, allExclusiveIds.Count,
                "6×20 专属卡应恰好 120 张不重复");
        }

        [Test]
        public void 独占标签_只出现在对应指挥官专属卡上()
        {
            var exclusiveByTag = new Dictionary<string, CommanderDefinition>();
            var commanders = CommanderCatalog.GetAll();
            for (int c = 0; c < commanders.Count; c++)
            {
                var commander = commanders[c];
                Assert.IsFalse(exclusiveByTag.ContainsKey(commander.ExclusiveTag),
                    "独占标签重复: " + commander.ExclusiveTag);
                exclusiveByTag.Add(commander.ExclusiveTag, commander);
            }

            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var card = defs[i];
                for (int e = 0; e < card.Tags.Count; e++)
                {
                    var tag = card.Tags[e];
                    CommanderDefinition owner;
                    if (!exclusiveByTag.TryGetValue(tag, out owner)) continue;

                    // 独占标签只能出现在该指挥官的专属卡上
                    Assert.IsTrue(owner.ExclusiveTraitIds.Contains(card.Id),
                        card.Id + " 携带独占标签 " + tag + " 但不在 " + owner.Id + " 专属卡列表中");
                }
            }

            // 每个独占标签在全池恰好出现 20 次(专属卡 20 张)
            foreach (var kv in exclusiveByTag)
            {
                int count = 0;
                for (int i = 0; i < defs.Count; i++)
                    if (defs[i].HasTag(kv.Key)) count++;
                Assert.AreEqual(20, count,
                    "独占标签 " + kv.Key + " 出现 " + count + " 次,应恰为 20");
            }
        }

        [Test]
        public void 卡池结构_22原有_8通用新_120专属()
        {
            var legacyIds = new HashSet<string>();
            for (int i = 0; i < TraitCatalog.LegacyGeneralDefinitions.Count; i++)
                legacyIds.Add(TraitCatalog.LegacyGeneralDefinitions[i].Id);

            var exclusiveIds = new HashSet<string>();
            var commanders = CommanderCatalog.GetAll();
            for (int c = 0; c < commanders.Count; c++)
                for (int i = 0; i < commanders[c].ExclusiveTraitIds.Count; i++)
                    exclusiveIds.Add(commanders[c].ExclusiveTraitIds[i]);

            int legacy = 0, exclusive = 0, generalNew = 0;
            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var id = defs[i].Id;
                if (legacyIds.Contains(id)) legacy++;
                else if (exclusiveIds.Contains(id)) exclusive++;
                else generalNew++;
            }

            Assert.AreEqual(22, legacy);
            Assert.AreEqual(120, exclusive);
            Assert.AreEqual(8, generalNew);
        }

        [Test]
        public void ExclusiveOwner_专属卡映射所属指挥官_通用卡null()
        {
            var commanders = CommanderCatalog.GetAll();
            for (int c = 0; c < commanders.Count; c++)
            {
                var commander = commanders[c];
                for (int i = 0; i < commander.ExclusiveTraitIds.Count; i++)
                {
                    string id = commander.ExclusiveTraitIds[i];
                    Assert.AreEqual(commander.Id, TraitCatalog.ExclusiveOwner(id),
                        id + " 专属卡应映射到 " + commander.Id);
                }
            }

            Assert.IsNull(TraitCatalog.ExclusiveOwner("trait_brave"),
                "原有通用卡无专属归属");
            Assert.IsNull(TraitCatalog.ExclusiveOwner("trait_field_medic"),
                "新增通用卡无专属归属");
        }

        [Test]
        public void 阶段一白名单_22张原有通用卡全部通过()
        {
            var legacy = TraitCatalog.LegacyGeneralDefinitions;
            Assert.AreEqual(22, legacy.Count);
            for (int i = 0; i < legacy.Count; i++)
                Assert.IsTrue(legacy[i].IsSupportedInPhase1(),
                    legacy[i].Id + " 效果不在阶段一白名单内");
        }

        static int CountRarity(TraitRarity rarity)
        {
            var defs = TraitCatalog.Definitions;
            int n = 0;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].Rarity == rarity) n++;
            return n;
        }
    }
}
#endif
