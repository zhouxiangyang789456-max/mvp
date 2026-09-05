#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Shared.Skills;

namespace Mvp.EditorTests.Battle.Skills
{
    /// <summary>
    /// GroupSkillRuntime(战斗技能系统开发文档 §4.2):持久模式互斥、SkillSequence 自增
    /// 使旧技能结果失效、单单位状态创建/复用/切换。
    /// </summary>
    public sealed class GroupSkillRuntimeTests
    {
        [Test]
        public void ResetModes_清除持久模式与瞄准目标()
        {
            var runtime = new GroupSkillRuntime();
            runtime.PersistentMode = PersistentSkillMode.Guard;
            runtime.TargetingSkillId = SkillIds.LongRange;
            runtime.ResetModes();
            Assert.AreEqual(PersistentSkillMode.None, runtime.PersistentMode);
            Assert.IsNull(runtime.TargetingSkillId);
        }

        [Test]
        public void ResetModes_自增SkillSequence_使旧计划失效()
        {
            var runtime = new GroupSkillRuntime();
            long before = runtime.SkillSequence;
            runtime.ResetModes();
            Assert.Greater(runtime.SkillSequence, before);
            runtime.ResetModes();
            Assert.Greater(runtime.SkillSequence, before + 1);
        }

        [Test]
        public void GetOrCreate_同一单位复用_保留状态()
        {
            var runtime = new GroupSkillRuntime();
            var st = runtime.GetOrCreate("u1", SkillIds.Sprint);
            st.State = SkillRuntimeState.Active;
            st.ActiveUntil = 99f;
            var again = runtime.GetOrCreate("u1", SkillIds.Sprint);
            Assert.AreSame(st, again);
            Assert.AreEqual(SkillRuntimeState.Active, again.State);
            Assert.AreEqual(99f, again.ActiveUntil);
        }

        [Test]
        public void GetOrCreate_切换技能_重置为Ready()
        {
            var runtime = new GroupSkillRuntime();
            var st = runtime.GetOrCreate("u1", SkillIds.Sprint);
            st.State = SkillRuntimeState.Active;
            st.ActiveUntil = 99f;
            var switched = runtime.GetOrCreate("u1", SkillIds.LongRange);
            Assert.AreSame(st, switched);
            Assert.AreEqual(SkillIds.LongRange, switched.SkillId);
            Assert.AreEqual(SkillRuntimeState.Ready, switched.State);
            Assert.AreEqual(0f, switched.ActiveUntil);
        }

        [Test]
        public void UnitStates_多单位独立()
        {
            var runtime = new GroupSkillRuntime();
            var a = runtime.GetOrCreate("u1", SkillIds.LongRange);
            var b = runtime.GetOrCreate("u2", SkillIds.LongRange);
            Assert.AreNotSame(a, b);
            a.State = SkillRuntimeState.Cooldown;
            Assert.AreEqual(SkillRuntimeState.Ready, b.State);
        }

        [Test]
        public void SkillAttackPlan_携带SkillSequence_与运行期一致()
        {
            var runtime = new GroupSkillRuntime();
            var plan = new SkillAttackPlan
            {
                SkillId = SkillIds.LongRange,
                SkillSequence = runtime.SkillSequence
            };
            Assert.AreEqual(runtime.SkillSequence, plan.SkillSequence);
            runtime.ResetModes();
            Assert.AreNotEqual(runtime.SkillSequence, plan.SkillSequence);
        }
    }
}
#endif
