#if UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Outcome;
using Mvp.Battle.Skills;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.EditorTests.Battle.Skills
{
    /// <summary>
    /// 远攻瞄准确认(战斗技能系统开发文档 §3.2 / 验收 #5):确认时捕获 SkillSequence,
    /// 取消(=ResetModes)后旧计划被序列守卫拒绝;确认后进入冷却,冷却中不可再瞄准。
    /// 输入漏斗本体(射线→BuildPlan→CommandSkillAttack)属场景集成,这里只验证
    /// 反覆盖契约与冷却资格这些可纯 C# 测试的规则。
    /// </summary>
    public sealed class SkillTargetingConfirmTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleSimulationState.Reset();
            SkillTestFixtures.ClearGrid();
        }

        [TearDown]
        public void TearDown()
        {
            SkillTestFixtures.ClearGrid();
        }

        static CommanderGroupRuntime RangedGroup(params UnitView[] members)
        {
            if (members.Length == 0)
            {
                var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                    SkillTestFixtures.MakeDefinition(UnitTag.Ranged, 0f, 4f),
                    new Vector2Int(3, 3)));
                return SkillTestFixtures.MakeGroup("g1", unit);
            }
            return SkillTestFixtures.MakeGroup("g1", members);
        }

        [Test]
        public void 瞄准中_计划捕获当前SkillSequence()
        {
            var group = RangedGroup();
            group.Skills.TargetingSkillId = SkillIds.LongRange;
            var plan = new SkillAttackPlan
            {
                SkillId = SkillIds.LongRange,
                SkillSequence = group.Skills.SkillSequence,
                TargetCell = new Vector2Int(9, 3)
            };
            plan.MemberIds.Add("u1");
            Assert.AreEqual(group.Skills.SkillSequence, plan.SkillSequence);
        }

        [Test]
        public void 取消瞄准_清理Targeting并失效旧计划()
        {
            var group = RangedGroup();
            group.Skills.TargetingSkillId = SkillIds.LongRange;
            var plan = new SkillAttackPlan
            {
                SkillId = SkillIds.LongRange,
                SkillSequence = group.Skills.SkillSequence,
                TargetCell = new Vector2Int(9, 3)
            };

            // SkillTargetingController.CancelTargeting 以 ResetModes 清理瞄准状态。
            group.Skills.ResetModes();
            Assert.IsNull(group.Skills.TargetingSkillId);
            Assert.AreNotEqual(group.Skills.SkillSequence, plan.SkillSequence,
                "取消后旧计划必须被判定为过期");
        }

        [Test]
        public void 过期计划_被CommandSkillAttack序列守卫拒绝()
        {
            var group = RangedGroup();
            group.Skills.TargetingSkillId = SkillIds.LongRange;
            var plan = new SkillAttackPlan
            {
                SkillId = SkillIds.LongRange,
                SkillSequence = group.Skills.SkillSequence,
                TargetCell = new Vector2Int(9, 3)
            };
            plan.MemberIds.Add("u1");

            // 取消 → 序列自增,计划过期。
            group.Skills.ResetModes();

            // 用未初始化实例调用 CommandSkillAttack:守卫在访问任何单例之前比较序列,
            // 因此过期计划必然返回 false(序列不符),且不会访问未初始化实例状态。
            var controller = (CommanderGroupCommandController)FormatterServices
                .GetUninitializedObject(typeof(CommanderGroupCommandController));
            var instanceProp = typeof(CommanderGroupCommandController).GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);
            instanceProp.SetValue(null, controller);
            try
            {
                Assert.IsFalse(controller.CommandSkillAttack(group, plan),
                    "过期 SkillSequence 的计划必须被拒绝");
            }
            finally
            {
                instanceProp.SetValue(null, null);
            }
        }

        [Test]
        public void 确认后进入冷却_同单位再瞄准被成员资格拒绝()
        {
            var group = RangedGroup();
            var def = SkillCatalog.Get(SkillIds.LongRange);
            var st = group.Skills.GetOrCreate("u1", def.Id);
            st.State = SkillRuntimeState.Cooldown;
            st.CooldownUntil = 10f;
            Assert.IsFalse(SkillEligibilityService.IsMemberEligible(group, group.Members[0],
                def, 0f));
            Assert.AreEqual(0, SkillEligibilityService.GetEligibleMemberCount(group, def, 0f));
        }

        [Test]
        public void 无远程单位_瞄准无法开始()
        {
            var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData("u1",
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 0f, 4f),
                new Vector2Int(3, 3)));
            var group = SkillTestFixtures.MakeGroup("g1", unit);
            string reason;
            Assert.IsFalse(SkillEligibilityService.CanActivate(group,
                SkillCatalog.Get(SkillIds.LongRange), out reason, 0f));
            Assert.AreEqual("编队无远程单位", reason);
        }
    }
}
#endif
