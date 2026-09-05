#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Mvp.Progression;

namespace Mvp.EditorTests.Progression
{
    /// <summary>
    /// EditMode NUnit tests for TraitBuildAnalyzer(阶段二:流派成型判定,§11.1)。Pure C#; no
    /// Unity scene state required. Run via the Unity Test Runner window.
    /// </summary>
    public sealed class TraitBuildAnalyzerTests
    {
        [Test]
        public void Archetypes_包含十七个流派_且定义符合公式()
        {
            Assert.AreEqual(17, TraitBuildAnalyzer.Archetypes.Count);

            var ids = new HashSet<string>();
            for (int i = 0; i < TraitBuildAnalyzer.Archetypes.Count; i++)
                Assert.IsTrue(ids.Add(TraitBuildAnalyzer.Archetypes[i].Id),
                    "Duplicate archetype id: " + TraitBuildAnalyzer.Archetypes[i].Id);

            AssertFormula("archetype_low_health_burst",
                new Dictionary<string, int> { { "low_health", 2 }, { "attack", 1 }, { "cooldown", 1 } });
            AssertFormula("archetype_position_defense",
                new Dictionary<string, int> { { "defense", 2 }, { "formation", 1 }, { "idle", 1 } });
            AssertFormula("archetype_high_health_mobility",
                new Dictionary<string, int> { { "mobility", 2 }, { "high_health", 1 }, { "attack", 1 } });
            AssertFormula("archetype_thick_health_grind",
                new Dictionary<string, int> { { "max_health", 2 }, { "defense", 1 }, { "sustain", 1 } });
            AssertFormula("archetype_balanced_command",
                new Dictionary<string, int> { { "commander", 2 }, { "balanced", 1 }, { "support", 1 } });
            // 150 卡重构新增:12 条独占流派,旗舰均为独占标签 ×2。
            AssertFormula("archetype_frenzy_burst",
                new Dictionary<string, int> { { "frenzy", 2 }, { "attack", 1 }, { "low_health", 1 } });
            AssertFormula("archetype_frenzy_sustain",
                new Dictionary<string, int> { { "frenzy", 2 }, { "lifesteal", 1 }, { "defense", 1 } });
            AssertFormula("archetype_bulwark_shield",
                new Dictionary<string, int> { { "bulwark", 2 }, { "shield", 1 }, { "formation", 1 } });
            AssertFormula("archetype_bulwark_thorns",
                new Dictionary<string, int> { { "bulwark", 2 }, { "reflect", 1 }, { "defense", 1 } });
            AssertFormula("archetype_lethality_crit",
                new Dictionary<string, int> { { "lethality", 2 }, { "critical", 1 }, { "attack", 1 } });
            AssertFormula("archetype_lethality_execute",
                new Dictionary<string, int> { { "lethality", 2 }, { "execute", 1 }, { "attack", 1 } });
            AssertFormula("archetype_scorch_burn",
                new Dictionary<string, int> { { "scorch", 2 }, { "burn", 1 }, { "attack", 1 } });
            AssertFormula("archetype_scorch_range",
                new Dictionary<string, int> { { "scorch", 2 }, { "range", 1 }, { "attack", 1 } });
            AssertFormula("archetype_mercenary_snowball",
                new Dictionary<string, int> { { "mercenary", 2 }, { "economy", 1 }, { "commander", 1 } });
            AssertFormula("archetype_mercenary_goldpower",
                new Dictionary<string, int> { { "mercenary", 2 }, { "economy", 1 }, { "attack", 1 } });
            AssertFormula("archetype_frost_burstcontrol",
                new Dictionary<string, int> { { "frost", 2 }, { "slow", 1 }, { "control", 1 } });
            AssertFormula("archetype_frost_zone",
                new Dictionary<string, int> { { "frost", 2 }, { "idle", 1 }, { "defense", 1 } });
        }

        [Test]
        public void 卡池_每个流派公式标签_至少一张卡覆盖()
        {
            var defs = TraitCatalog.Definitions;
            for (int i = 0; i < TraitBuildAnalyzer.Archetypes.Count; i++)
            {
                var archetype = TraitBuildAnalyzer.Archetypes[i];
                for (int t = 0; t < archetype.Tags.Count; t++)
                {
                    var tag = archetype.Tags[t].Tag;
                    bool covered = false;
                    for (int d = 0; d < defs.Count; d++)
                        if (defs[d].HasTag(tag)) { covered = true; break; }
                    Assert.IsTrue(covered,
                        archetype.Id + " 公式标签 " + tag + " 在卡池中无卡覆盖");
                }
            }
        }

        [Test]
        public void 卡池_id与展示名唯一()
        {
            var defs = TraitCatalog.Definitions;
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                Assert.IsNotNull(def, "卡池第 " + i + " 项为 null");
                Assert.IsTrue(ids.Add(def.Id), "重复 id: " + def.Id);
                Assert.IsTrue(names.Add(def.DisplayName), "重复展示名: " + def.DisplayName);
            }
        }

        [Test]
        public void 阶段一白名单_仅限原有22张通用卡()
        {
            var legacy = TraitCatalog.LegacyGeneralDefinitions;
            for (int i = 0; i < legacy.Count; i++)
                Assert.IsTrue(legacy[i].IsSupportedInPhase1(),
                    legacy[i].Id + " 效果不在阶段一白名单内");
        }

        [Test]
        public void ScoreFor_低血爆发_按公式求和()
        {
            var archetype = FindArchetype("archetype_low_health_burst");
            var counts = new Dictionary<string, int>
            {
                { "low_health", 2 },
                { "attack", 1 },
                { "cooldown", 0 }  // 显式 0,同缺失
            };

            Assert.AreEqual(5, TraitBuildAnalyzer.ScoreFor(archetype, counts));

            // 缺失标签计 0
            Assert.AreEqual(0, TraitBuildAnalyzer.ScoreFor(archetype, new Dictionary<string, int>()));
            Assert.AreEqual(1, TraitBuildAnalyzer.ScoreFor(archetype,
                new Dictionary<string, int> { { "attack", 1 } }));
        }

        [Test]
        public void GetMaturity_边界分数_正确()
        {
            Assert.AreEqual(TraitBuildMaturity.Unformed, TraitBuildAnalyzer.GetMaturity(3));
            Assert.AreEqual(TraitBuildMaturity.Forming, TraitBuildAnalyzer.GetMaturity(4));
            Assert.AreEqual(TraitBuildMaturity.Forming, TraitBuildAnalyzer.GetMaturity(5));
            Assert.AreEqual(TraitBuildMaturity.Formed, TraitBuildAnalyzer.GetMaturity(6));
        }

        [Test]
        public void Analyze_空数据_未定型且无主副流派()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>());

            Assert.AreEqual(0, summary.PrimaryScore);
            Assert.IsNull(summary.PrimaryArchetypeId);
            Assert.IsNull(summary.SecondaryArchetypeId);
            Assert.IsEmpty(summary.RecommendedTags);
        }

        [Test]
        public void Analyze_得分5_正在成型()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>
            {
                { "low_health", 2 },
                { "attack", 1 }
            });

            Assert.AreEqual("archetype_low_health_burst", summary.PrimaryArchetypeId);
            Assert.AreEqual(5, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Forming,
                TraitBuildAnalyzer.GetMaturity(summary.PrimaryScore));
            CollectionAssert.AreEqual(
                new[] { "low_health", "cooldown", "attack" }, summary.RecommendedTags);
        }

        [Test]
        public void Analyze_得分6_流派已成型()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int> { { "low_health", 3 } });

            Assert.AreEqual("archetype_low_health_burst", summary.PrimaryArchetypeId);
            Assert.AreEqual(6, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Formed,
                TraitBuildAnalyzer.GetMaturity(summary.PrimaryScore));
        }

        [Test]
        public void Analyze_阵地防守流_缺idle标签仍可成型()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int> { { "defense", 3 } });

            Assert.AreEqual("archetype_position_defense", summary.PrimaryArchetypeId);
            Assert.AreEqual(6, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Formed,
                TraitBuildAnalyzer.GetMaturity(summary.PrimaryScore));
            CollectionAssert.AreEqual(
                new[] { "defense", "formation", "idle" }, summary.RecommendedTags);
        }

        [Test]
        public void Analyze_副流派为第二高分()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>
            {
                { "defense", 3 },
                { "low_health", 2 }
            });

            Assert.AreEqual("archetype_position_defense", summary.PrimaryArchetypeId);
            Assert.AreEqual(6, summary.PrimaryScore);
            Assert.AreEqual("archetype_low_health_burst", summary.SecondaryArchetypeId);
        }

        [Test]
        public void Analyze_同分_按注册表顺序取主副()
        {
            // attack:1 → 低血爆发(索引0)与高血机动(索引2)同 1 分
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int> { { "attack", 1 } });

            Assert.AreEqual("archetype_low_health_burst", summary.PrimaryArchetypeId);
            Assert.AreEqual("archetype_high_health_mobility", summary.SecondaryArchetypeId);
            Assert.AreEqual(1, summary.PrimaryScore);
        }

        [Test]
        public void Analyze_均衡指挥流_公式已就绪_合成计数可成型()
        {
            // 仅 attack 时均衡指挥 0 分,永不当主
            var unreachable = TraitBuildAnalyzer.Analyze(new Dictionary<string, int> { { "attack", 1 } });
            Assert.AreNotEqual("archetype_balanced_command", unreachable.PrimaryArchetypeId);

            // 合成计数 {commander:3, balanced:1} → 均衡指挥 7 分成型
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>
            {
                { "commander", 3 },
                { "balanced", 1 }
            });

            Assert.AreEqual("archetype_balanced_command", summary.PrimaryArchetypeId);
            Assert.AreEqual(7, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Formed,
                TraitBuildAnalyzer.GetMaturity(summary.PrimaryScore));
        }

        [Test]
        public void Analyze_快照_仅统计已装备与库存()
        {
            var progression = new PlayerProgressionSnapshot();
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_inventory_1",
                DefinitionId = "trait_brave",   // attack, low_health
                Location = TraitCardLocation.Inventory
            });
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_equipped_1",
                DefinitionId = "trait_guard",   // defense
                Location = TraitCardLocation.Equipped,
                EquippedCommanderId = "commander_1",
                EquippedSlotIndex = 0
            });
            progression.TraitCards.Add(new TraitCardInstance
            {
                InstanceId = "inst_sold_1",
                DefinitionId = "trait_hold",    // defense, formation (排除)
                Location = TraitCardLocation.Sold
            });

            var summary = TraitBuildAnalyzer.Analyze(progression);

            Assert.AreEqual(1, summary.TagCounts["attack"]);
            Assert.AreEqual(1, summary.TagCounts["low_health"]);
            Assert.AreEqual(1, summary.TagCounts["defense"]);
            Assert.IsFalse(summary.TagCounts.ContainsKey("formation"),
                "Sold card tags must be excluded");

            Assert.AreEqual("archetype_low_health_burst", summary.PrimaryArchetypeId);
            Assert.AreEqual(3, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Unformed,
                TraitBuildAnalyzer.GetMaturity(summary.PrimaryScore));
        }

        [Test]
        public void Analyze_null入参_不抛异常_返回未定型()
        {
            var fromSnapshot = TraitBuildAnalyzer.Analyze((PlayerProgressionSnapshot)null);
            Assert.AreEqual(0, fromSnapshot.PrimaryScore);
            Assert.IsNull(fromSnapshot.PrimaryArchetypeId);
            Assert.AreEqual(TraitBuildMaturity.Unformed,
                TraitBuildAnalyzer.GetMaturity(fromSnapshot.PrimaryScore));

            var fromCounts = TraitBuildAnalyzer.Analyze((IReadOnlyDictionary<string, int>)null);
            Assert.AreEqual(0, fromCounts.PrimaryScore);
            Assert.IsNull(fromCounts.PrimaryArchetypeId);
            Assert.AreEqual(TraitBuildMaturity.Unformed,
                TraitBuildAnalyzer.GetMaturity(fromCounts.PrimaryScore));
        }

        [Test]
        public void Analyze_IReadOnlyList_构建计数并判定()
        {
            var summary = TraitBuildAnalyzer.Analyze(
                new List<string> { "attack", "attack", "low_health" });

            Assert.AreEqual("archetype_low_health_burst", summary.PrimaryArchetypeId);
            Assert.AreEqual(4, summary.PrimaryScore);
            Assert.AreEqual(TraitBuildMaturity.Forming, summary.Maturity);
        }

        [Test]
        public void Analyze_IReadOnlyList_null或空_未定型()
        {
            var fromNull = TraitBuildAnalyzer.Analyze((IReadOnlyList<string>)null);
            Assert.AreEqual(0, fromNull.PrimaryScore);
            Assert.IsNull(fromNull.PrimaryArchetypeId);
            Assert.IsEmpty(fromNull.PrimaryTags);
            Assert.AreEqual(TraitBuildMaturity.Unformed, fromNull.Maturity);

            var fromEmpty = TraitBuildAnalyzer.Analyze(new List<string>());
            Assert.AreEqual(0, fromEmpty.PrimaryScore);
            Assert.IsNull(fromEmpty.PrimaryArchetypeId);
            Assert.IsEmpty(fromEmpty.PrimaryTags);
            Assert.AreEqual(TraitBuildMaturity.Unformed, fromEmpty.Maturity);
        }

        [Test]
        public void Analyze_PrimaryTags_镜像主流派公式()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>
            {
                { "low_health", 2 },
                { "attack", 1 }
            });

            Assert.AreEqual(5, summary.PrimaryScore);
            Assert.AreEqual(3, summary.PrimaryTags.Count);
            // 顺序与注册表公式一致:low_health(2), attack(1), cooldown(1)
            Assert.AreEqual("low_health", summary.PrimaryTags[0].Tag);
            Assert.AreEqual(2, summary.PrimaryTags[0].Weight);
            Assert.AreEqual("attack", summary.PrimaryTags[1].Tag);
            Assert.AreEqual(1, summary.PrimaryTags[1].Weight);
            Assert.AreEqual("cooldown", summary.PrimaryTags[2].Tag);
            Assert.AreEqual(1, summary.PrimaryTags[2].Weight);
        }

        [Test]
        public void PrimaryTags_防御性拷贝_修改不影响注册表()
        {
            var summary = TraitBuildAnalyzer.Analyze(new Dictionary<string, int>
            {
                { "low_health", 2 },
                { "attack", 1 }
            });

            summary.PrimaryTags[0].Weight = 999;

            var archetype = FindArchetype("archetype_low_health_burst");
            Assert.AreEqual(2, WeightInArchetype(archetype, "low_health"));
        }

        static TraitArchetypeSpec FindArchetype(string id)
        {
            for (int i = 0; i < TraitBuildAnalyzer.Archetypes.Count; i++)
                if (TraitBuildAnalyzer.Archetypes[i].Id == id)
                    return TraitBuildAnalyzer.Archetypes[i];
            return null;
        }

        static int WeightInArchetype(TraitArchetypeSpec archetype, string tag)
        {
            for (int i = 0; i < archetype.Tags.Count; i++)
                if (archetype.Tags[i].Tag == tag) return archetype.Tags[i].Weight;
            return -1;
        }

        static void AssertFormula(string id, Dictionary<string, int> expected)
        {
            var archetype = FindArchetype(id);
            Assert.IsNotNull(archetype, id + " not found");
            Assert.IsNotNull(archetype.Tags, id + " tags null");
            Assert.AreEqual(expected.Count, archetype.Tags.Count, id + " tag count");

            for (int i = 0; i < archetype.Tags.Count; i++)
            {
                var w = archetype.Tags[i];
                Assert.IsNotNull(w, id + " tag weight null at " + i);
                Assert.IsTrue(expected.ContainsKey(w.Tag),
                    id + " unexpected tag: " + w.Tag);
                Assert.AreEqual(expected[w.Tag], w.Weight,
                    id + " weight for " + w.Tag);
            }
        }
    }
}
#endif
