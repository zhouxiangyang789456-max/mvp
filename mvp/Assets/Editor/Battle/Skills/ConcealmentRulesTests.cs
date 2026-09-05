#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.EditorTests.Battle.Skills
{
    /// <summary>
    /// 隐蔽规则(战斗技能系统开发文档 §6):全员静止+森林→准备后隐蔽;任一成员
    /// 非森林/移动/受击/近距敌兵发现即暴露;暴露后 RevealLockSeconds 锁定期。
    /// 核心静态状态机用显式 now 驱动,无场景依赖。
    /// </summary>
    public sealed class ConcealmentRulesTests
    {
        [SetUp]
        public void SetUp()
        {
            ConcealmentService.Shutdown();
            SkillTestFixtures.InstallGrid(10, 10, new Vector2Int(3, 3), new Vector2Int(3, 4));
        }

        [TearDown]
        public void TearDown()
        {
            ConcealmentService.Shutdown();
            SkillTestFixtures.ClearGrid();
        }

        static CommanderGroupRuntime ConcealmentGroup(params UnitView[] members)
        {
            var group = SkillTestFixtures.MakeGroup("g_hidden", members);
            group.Skills.PersistentMode = PersistentSkillMode.Concealment;
            return group;
        }

        static UnitView ForestUnit(string id, Vector2Int cell, int health = 100)
        {
            var data = SkillTestFixtures.MakeData(id,
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f), cell);
            data.CurrentHealth = health;
            return SkillTestFixtures.MakeUnit(data);
        }

        [Test]
        public void 全员静止森林_准备时间后_进入隐蔽()
        {
            var group = ConcealmentGroup(ForestUnit("u1", new Vector2Int(3, 3)));
            var early = ConcealmentService.Evaluate(group, 0f);
            Assert.IsFalse(early.Concealed);
            var ready = ConcealmentService.Evaluate(group, 2f); // > PrepareSeconds (1.5)
            Assert.IsTrue(ready.Concealed);
            Assert.IsTrue(ConcealmentService.IsConcealed(group));
        }

        [Test]
        public void 任一成员不在森林_不可隐蔽()
        {
            var group = ConcealmentGroup(
                ForestUnit("u1", new Vector2Int(3, 3)),
                ForestUnit("u2", new Vector2Int(5, 5))); // Plain cell
            var eval = ConcealmentService.Evaluate(group, 2f);
            Assert.IsFalse(eval.Concealed);
        }

        [Test]
        public void 成员移动_不可隐蔽()
        {
            var u1 = ForestUnit("u1", new Vector2Int(3, 3));
            u1.Data.State = UnitState.Moving;
            var group = ConcealmentGroup(u1);
            var eval = ConcealmentService.Evaluate(group, 2f);
            Assert.IsFalse(eval.Concealed);
        }

        [Test]
        public void 隐蔽后受击_立即暴露()
        {
            var u1 = ForestUnit("u1", new Vector2Int(3, 3));
            var group = ConcealmentGroup(u1);
            Assert.IsTrue(ConcealmentService.Evaluate(group, 2f).Concealed);

            u1.Data.CurrentHealth = 80;
            var eval = ConcealmentService.Evaluate(group, 3f);
            Assert.IsTrue(eval.Exposed);
            Assert.IsFalse(eval.Concealed);
            Assert.IsTrue(ConcealmentService.IsInRevealLock(group, 3f));
        }

        [Test]
        public void 近距离敌兵_立即发现并暴露()
        {
            var group = ConcealmentGroup(ForestUnit("u1", new Vector2Int(3, 3)));
            var enemy = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("enemy1",
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f),
                new Vector2Int(3, 4), TeamId.Enemy));
            var eval = ConcealmentService.Evaluate(group, 0f, g => enemy);
            Assert.IsTrue(eval.DiscoveredByEnemy);
            Assert.IsTrue(eval.Exposed);
            Assert.IsFalse(eval.Concealed);
        }

        [Test]
        public void 暴露后锁定期内_满足前提也不再隐蔽()
        {
            var u1 = ForestUnit("u1", new Vector2Int(3, 3));
            var group = ConcealmentGroup(u1);
            Assert.IsTrue(ConcealmentService.Evaluate(group, 2f).Concealed);
            u1.Data.CurrentHealth = 70;
            ConcealmentService.Evaluate(group, 3f); // expose → RevealLockUntil = 6
            var locked = ConcealmentService.Evaluate(group, 4f);
            Assert.IsFalse(locked.Concealed);
        }

        [Test]
        public void 锁定到期_重新满足前提_再次隐蔽()
        {
            var u1 = ForestUnit("u1", new Vector2Int(3, 3));
            var group = ConcealmentGroup(u1);
            Assert.IsTrue(ConcealmentService.Evaluate(group, 2f).Concealed);
            u1.Data.CurrentHealth = 70;
            ConcealmentService.Evaluate(group, 3f); // expose → lock until 6

            Assert.IsFalse(ConcealmentService.Evaluate(group, 4f).Concealed); // in lock
            Assert.IsFalse(ConcealmentService.Evaluate(group, 7f).Concealed); // lock over, preparing until 8.5
            Assert.IsTrue(ConcealmentService.Evaluate(group, 9f).Concealed);  // re-concealed
        }

        [Test]
        public void 非隐蔽模式_永不返回隐蔽()
        {
            var group = SkillTestFixtures.MakeGroup("g_other", ForestUnit("u1", new Vector2Int(3, 3)));
            group.Skills.PersistentMode = PersistentSkillMode.Guard;
            var eval = ConcealmentService.Evaluate(group, 2f);
            Assert.IsFalse(eval.Concealed);
        }
    }
}
#endif
