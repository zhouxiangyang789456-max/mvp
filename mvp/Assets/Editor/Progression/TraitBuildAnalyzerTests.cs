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
        public void Archetypes_包含五个流派_且定义符合四()
        {
            Assert.AreEqual(5, TraitBuildAnalyzer.Archetypes.Count);

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
        public void Analyze_均衡指挥流_当前池不可达但公式已就绪()
        {
            // 当前池 {attack:1} 时均衡指挥 0 分,永不当主
            var unreachable = TraitBuildAnalyzer.Analyze(new Dictionary<string, int> { { "attack", 1 } });
            Assert.AreNotEqual("archetype_balanced_command", unreachable.PrimaryArchetypeId);

            // 合成计数 {commander:3, balanced:1} → 均衡指挥 7 分成型(向前兼容)
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

        static TraitArchetypeSpec FindArchetype(string id)
        {
            for (int i = 0; i < TraitBuildAnalyzer.Archetypes.Count; i++)
                if (TraitBuildAnalyzer.Archetypes[i].Id == id)
                    return TraitBuildAnalyzer.Archetypes[i];
            return null;
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
