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
    /// 冲刺规则(战斗技能系统开发文档 §8):全坦克编队、1.8× 移速、4s 持续时间、
    /// 12s 冷却、攻击打断。显式 now 驱动,无场景依赖。
    /// </summary>
    public sealed class SprintRulesTests
    {
        [SetUp]
        public void SetUp()
        {
            SprintEffectService.Shutdown();
        }

        [TearDown]
        public void TearDown()
        {
            SprintEffectService.Shutdown();
        }

        static UnitView Tank(string id, Vector2Int cell)
        {
            return SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData(id,
                SkillTestFixtures.MakeDefinition(UnitTag.Tank, 1f, 2f), cell));
        }

        static UnitView Infantry(string id, Vector2Int cell)
        {
            return SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData(id,
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f), cell));
        }

        [Test]
        public void 非全坦克编队_不可激活()
        {
            var group = SkillTestFixtures.MakeGroup("g1",
                Tank("u1", new Vector2Int(3, 3)),
                Infantry("u2", new Vector2Int(4, 3)));
            Assert.IsFalse(SprintEffectService.IsAllTankGroup(group));
            string reason;
            Assert.IsFalse(SprintEffectService.TryActivate(group, 0f, out reason));
            Assert.AreEqual("编队不全是坦克单位", reason);
        }

        [Test]
        public void 全坦克_激活成功_倍率1_8()
        {
            var tank = Tank("u1", new Vector2Int(3, 3));
            var group = SkillTestFixtures.MakeGroup("g1", tank);
            string reason;
            Assert.IsTrue(SprintEffectService.TryActivate(group, 0f, out reason));
            Assert.AreEqual(1.8f, SprintEffectService.GetMoveSpeedMultiplier(group, tank.Data, 0f));
            Assert.IsTrue(SprintEffectService.IsActive(group, 0f));
        }

        [Test]
        public void 持续时间结束_懒切冷却_倍率恢复()
        {
            var tank = Tank("u1", new Vector2Int(3, 3));
            var group = SkillTestFixtures.MakeGroup("g1", tank);
            string reason;
            SprintEffectService.TryActivate(group, 0f, out reason);
            // DurationSeconds = 4; at now=4 the multiplier expires (lazy Active→Cooldown).
            Assert.AreEqual(1f, SprintEffectService.GetMoveSpeedMultiplier(group, tank.Data, 4f));
            Assert.IsFalse(SprintEffectService.IsActive(group, 4f));
            // CooldownUntil = 0 + 4 + 12 = 16 → 11s remaining at now=5.
            Assert.AreEqual(11f, SprintEffectService.GetRemainingCooldown(group, 5f), 0.01f);
        }

        [Test]
        public void 冷却到期_可再次激活()
        {
            var tank = Tank("u1", new Vector2Int(3, 3));
            var group = SkillTestFixtures.MakeGroup("g1", tank);
            string reason;
            SprintEffectService.TryActivate(group, 0f, out reason);
            SprintEffectService.GetMoveSpeedMultiplier(group, tank.Data, 4f); // → Cooldown until 16
            Assert.IsTrue(SprintEffectService.GetRemainingCooldown(group, 15f) > 0f);
            Assert.IsFalse(SprintEffectService.GetRemainingCooldown(group, 16f) > 0f);
            Assert.IsTrue(SprintEffectService.TryActivate(group, 16f, out reason));
        }

        [Test]
        public void 攻击打断冲刺_进入冷却()
        {
            var tank = Tank("u1", new Vector2Int(3, 3));
            var group = SkillTestFixtures.MakeGroup("g1", tank);
            string reason;
            SprintEffectService.TryActivate(group, 0f, out reason);
            Assert.IsTrue(SprintEffectService.IsActive(group, 0f));

            SprintEffectService.NotifyAttack(group, 2f);
            Assert.IsFalse(SprintEffectService.IsActive(group, 2f));
            // CooldownUntil = 2 + 12 = 14 → 12s remaining.
            Assert.AreEqual(12f, SprintEffectService.GetRemainingCooldown(group, 2f), 0.01f);
        }

        [Test]
        public void null编队_不可激活()
        {
            string reason;
            Assert.IsFalse(SprintEffectService.TryActivate(null, 0f, out reason));
        }

        [Test]
        public void 空编队_不可激活()
        {
            var group = SkillTestFixtures.MakeGroup("g_empty");
            Assert.IsFalse(SprintEffectService.IsAllTankGroup(group));
        }
    }
}
#endif
