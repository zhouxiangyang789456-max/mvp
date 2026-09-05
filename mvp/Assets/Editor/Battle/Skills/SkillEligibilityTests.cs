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
    /// 战斗技能系统开发文档 §4.1 / §6:编队级技能资格矩阵。覆盖基础编队资格、
    /// 每项技能的激活条件(标签/地形/冷却)与成员资格。纯 C#,无场景依赖。
    /// </summary>
    public sealed class SkillEligibilityTests
    {
        [SetUp]
        public void SetUp()
        {
            BattlePhaseState.StartCombat();
            ConcealmentService.Shutdown();
        }

        [TearDown]
        public void TearDown()
        {
            BattlePhaseState.ResetToDeployment();
            ConcealmentService.Shutdown();
            SkillTestFixtures.ClearGrid();
        }

        static CommanderGroupRuntime InfantryGroup(string id, params UnitView[] members)
        {
            if (members.Length == 0)
            {
                var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                    SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f),
                    new Vector2Int(3, 3)));
                return SkillTestFixtures.MakeGroup(id, unit);
            }
            return SkillTestFixtures.MakeGroup(id, members);
        }

        [Test]
        public void IsGroupEligible_null编队_返回未激活()
        {
            string reason;
            Assert.IsFalse(SkillEligibilityService.IsGroupEligible(null, out reason));
            Assert.AreEqual("未激活编队", reason);
        }

        [Test]
        public void IsGroupEligible_战斗阶段_存活编队_可用()
        {
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsTrue(SkillEligibilityService.IsGroupEligible(group, out reason));
        }

        [Test]
        public void IsGroupEligible_部署阶段_不可用()
        {
            BattlePhaseState.ResetToDeployment();
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsFalse(SkillEligibilityService.IsGroupEligible(group, out reason));
            Assert.AreEqual("仅在战斗阶段", reason);
        }

        [Test]
        public void IsGroupEligible_全员阵亡_不可用()
        {
            var data = SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f),
                new Vector2Int(3, 3), state: UnitState.Dead);
            data.CurrentHealth = 0;
            var group = SkillTestFixtures.MakeGroup("g1", SkillTestFixtures.MakeUnit(data));
            string reason;
            Assert.IsFalse(SkillEligibilityService.IsGroupEligible(group, out reason));
            Assert.AreEqual("编队已被消灭", reason);
        }

        [Test]
        public void CanActivate_防御_战斗阶段_可用()
        {
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsTrue(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Guard), out reason, 0f));
        }

        [Test]
        public void CanActivate_隐蔽_不满足森林前提_不可用()
        {
            SkillTestFixtures.InstallGrid(10, 10); // no Forest cells
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsFalse(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Concealment), out reason, 0f));
            Assert.AreEqual("需全员静止且位于森林格", reason);
        }

        [Test]
        public void CanActivate_隐蔽_全员森林静止_可用()
        {
            SkillTestFixtures.InstallGrid(10, 10, new Vector2Int(3, 3));
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsTrue(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Concealment), out reason, 0f));
        }

        [Test]
        public void CanActivate_远攻_无远程单位_不可用()
        {
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsFalse(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.LongRange), out reason, 0f));
            Assert.AreEqual("编队无远程单位", reason);
        }

        [Test]
        public void CanActivate_远攻_有远程单位_可用()
        {
            var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Ranged, 1f, 4f),
                new Vector2Int(3, 3)));
            var group = SkillTestFixtures.MakeGroup("g1", unit);
            string reason;
            Assert.IsTrue(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.LongRange), out reason, 0f));
        }

        [Test]
        public void CanActivate_冲刺_非全坦克_不可用()
        {
            var group = InfantryGroup("g1");
            string reason;
            Assert.IsFalse(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Sprint), out reason, 0f));
            Assert.AreEqual("编队不全是坦克单位", reason);
        }

        [Test]
        public void CanActivate_冲刺_全坦克_可用()
        {
            var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Tank, 1f, 2f),
                new Vector2Int(3, 3)));
            var group = SkillTestFixtures.MakeGroup("g1", unit);
            string reason;
            Assert.IsTrue(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Sprint), out reason, 0f));
        }

        [Test]
        public void CanActivate_冲刺_冷却中_不可用()
        {
            var data = SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Tank, 1f, 2f),
                new Vector2Int(3, 3));
            var group = SkillTestFixtures.MakeGroup("g1", SkillTestFixtures.MakeUnit(data));
            var st = group.Skills.GetOrCreate("u1", SkillIds.Sprint);
            st.State = SkillRuntimeState.Cooldown;
            st.CooldownUntil = 5f;
            string reason;
            Assert.IsFalse(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.Sprint), out reason, 0f));
            Assert.AreEqual("冲刺冷却中", reason);
        }

        [Test]
        public void IsMemberEligible_标签不符_不可用()
        {
            var data = SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 4f),
                new Vector2Int(3, 3));
            var unit = SkillTestFixtures.MakeUnit(data);
            var group = SkillTestFixtures.MakeGroup("g1", unit);
            var def = SkillCatalog.Get(SkillIds.LongRange);
            Assert.IsFalse(SkillEligibilityService.IsMemberEligible(group, unit, def, 0f));
        }

        [Test]
        public void IsMemberEligible_冷却中_不可用()
        {
            var data = SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Ranged, 1f, 4f),
                new Vector2Int(3, 3));
            var unit = SkillTestFixtures.MakeUnit(data);
            var group = SkillTestFixtures.MakeGroup("g1", unit);
            var def = SkillCatalog.Get(SkillIds.LongRange);
            var st = group.Skills.GetOrCreate("u1", def.Id);
            st.State = SkillRuntimeState.Cooldown;
            st.CooldownUntil = 5f;
            Assert.IsFalse(SkillEligibilityService.IsMemberEligible(group, unit, def, 0f));
        }

        [Test]
        public void GetMaxRemainingCooldown_取最大剩余冷却()
        {
            var group = SkillTestFixtures.MakeGroup("g1",
                SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                    SkillTestFixtures.MakeDefinition(UnitTag.Ranged, 1f, 4f),
                    new Vector2Int(3, 3))),
                SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u2",
                    SkillTestFixtures.MakeDefinition(UnitTag.Ranged, 1f, 4f),
                    new Vector2Int(4, 3))));
            var def = SkillCatalog.Get(SkillIds.LongRange);
            var st1 = group.Skills.GetOrCreate("u1", def.Id);
            st1.State = SkillRuntimeState.Cooldown;
            st1.CooldownUntil = 5f;
            var st2 = group.Skills.GetOrCreate("u2", def.Id);
            st2.State = SkillRuntimeState.Cooldown;
            st2.CooldownUntil = 9f;
            Assert.AreEqual(9f, SkillEligibilityService.GetMaxRemainingCooldown(group, def, 0f));
        }
    }
}
#endif
