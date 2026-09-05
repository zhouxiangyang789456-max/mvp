#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.EditorTests.Battle.Skills
{
    public sealed class TauntRulesTests
    {
        GameObject _registryGo;
        GameObject _visionGo;

        [SetUp]
        public void SetUp()
        {
            TauntEffectService.Shutdown();
            _registryGo = new GameObject("TauntTestRegistry");
            _registryGo.AddComponent<CommanderGroupRegistry>();
            _visionGo = new GameObject("TauntTestVision");
            _visionGo.AddComponent<Mvp.Battle.Vision.BattleVisionService>();
        }

        [TearDown]
        public void TearDown()
        {
            TauntEffectService.Shutdown();
            if (_registryGo != null) Object.DestroyImmediate(_registryGo);
            if (_visionGo != null) Object.DestroyImmediate(_visionGo);
        }

        static CommanderGroupRuntime Group(string id, TeamId team, Vector2Int cell)
        {
            var unit = SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData(
                "unit_" + id,
                SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f),
                cell, team));
            var group = SkillTestFixtures.MakeGroup(id, unit);
            group.Team = team;
            group.AnchorCell = cell;
            unit.Data.CommanderGroupId = id;
            return group;
        }

        void Register(params CommanderGroupRuntime[] groups)
        {
            var registry = CommanderGroupRegistry.Instance;
            for (int i = 0; i < groups.Length; i++) registry.Register(groups[i], false);
        }

        [Test]
        public void 六格内命中_七格外不命中()
        {
            Assert.IsTrue(TauntEffectService.IsInRange(Vector2Int.zero,
                new Vector2Int(6, 0), 6));
            Assert.IsFalse(TauntEffectService.IsInRange(Vector2Int.zero,
                new Vector2Int(7, 0), 6));
        }

        [Test]
        public void 对角六格按Chebyshev距离命中()
        {
            Assert.IsTrue(TauntEffectService.IsInRange(Vector2Int.zero,
                new Vector2Int(6, 6), 6));
        }

        [Test]
        public void 范围内编队被强制锁定五秒()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(6, 6));
            Register(source, enemy);

            int count;
            string reason;
            Assert.IsTrue(TauntEffectService.TryActivate(source, 10f,
                out count, out reason), reason);
            Assert.AreEqual(1, count);
            CommanderGroupRuntime forced;
            Assert.IsTrue(TauntEffectService.TryGetForcedTarget(enemy, 14.99f, out forced));
            Assert.AreSame(source, forced);
            Assert.IsFalse(TauntEffectService.TryGetForcedTarget(enemy, 15f, out forced));
        }

        [Test]
        public void 无目标失败且不消耗冷却()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            Register(source);
            int count;
            string reason;
            Assert.IsFalse(TauntEffectService.TryActivate(source, 10f,
                out count, out reason));
            Assert.AreEqual("范围内没有可嘲讽的敌军", reason);
            Assert.AreEqual(0f, TauntEffectService.GetRemainingCooldown(source, 10f));
        }

        [Test]
        public void 成功释放后冷却十二秒()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            Register(source, enemy);
            int count;
            string reason;
            Assert.IsTrue(TauntEffectService.TryActivate(source, 10f,
                out count, out reason));
            Assert.AreEqual(12f, TauntEffectService.GetRemainingCooldown(source, 10f), 0.01f);
            Assert.AreEqual(0f, TauntEffectService.GetRemainingCooldown(source, 22f), 0.01f);
        }

        [Test]
        public void 新来源覆盖旧来源并刷新时间()
        {
            var first = Group("player_a", TeamId.Player, Vector2Int.zero);
            var second = Group("player_b", TeamId.Player, new Vector2Int(1, 0));
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            Register(first, second, enemy);
            int count;
            string reason;
            TauntEffectService.TryActivate(first, 0f, out count, out reason);
            TauntEffectService.TryActivate(second, 2f, out count, out reason);
            CommanderGroupRuntime forced;
            Assert.IsTrue(TauntEffectService.TryGetForcedTarget(enemy, 6.9f, out forced));
            Assert.AreSame(second, forced);
        }

        [Test]
        public void 施法者被移除时强制目标立即清理()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            Register(source, enemy);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            TauntEffectService.RemoveGroup(source.GroupId);
            CommanderGroupRuntime forced;
            Assert.IsFalse(TauntEffectService.TryGetForcedTarget(enemy, 1f, out forced));
        }

        [Test]
        public void 一个敌方编队多个成员只计数一次()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            enemy.Members.Add(SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData(
                "enemy_extra", SkillTestFixtures.MakeDefinition(UnitTag.Infantry, 1f, 2f),
                new Vector2Int(3, 0), TeamId.Enemy)));
            Register(source, enemy);
            int count;
            string reason;
            Assert.IsTrue(TauntEffectService.TryActivate(source, 0f, out count, out reason));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void 范围外敌军不会获得强制目标()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var near = Group("near", TeamId.Enemy, new Vector2Int(6, 0));
            var far = Group("far", TeamId.Enemy, new Vector2Int(7, 0));
            Register(source, near, far);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            CommanderGroupRuntime forced;
            Assert.IsFalse(TauntEffectService.TryGetForcedTarget(far, 1f, out forced));
        }

        [Test]
        public void 施法者移动出范围后既有效果仍持续()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            Register(source, enemy);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            source.AnchorCell = new Vector2Int(20, 20);
            CommanderGroupRuntime forced;
            Assert.IsTrue(TauntEffectService.TryGetForcedTarget(enemy, 4f, out forced));
        }

        [Test]
        public void 成功释放会退出隐蔽模式()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            source.Skills.PersistentMode = Mvp.Shared.Skills.PersistentSkillMode.Concealment;
            ConcealmentService.BeginConcealment(source);
            Register(source, enemy);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            Assert.AreEqual(Mvp.Shared.Skills.PersistentSkillMode.None,
                source.Skills.PersistentMode);
            Assert.IsFalse(ConcealmentService.IsConcealed(source));
        }

        [Test]
        public void 仅受嘲讽敌军获得定向视野()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var affected = Group("affected", TeamId.Enemy, new Vector2Int(2, 0));
            var unaffected = Group("unaffected", TeamId.Enemy, new Vector2Int(8, 0));
            Register(source, affected, unaffected);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            var vision = Mvp.Battle.Vision.BattleVisionService.Instance;
            Assert.IsTrue(vision.IsForcedVisible(affected, source, 4.9f));
            Assert.IsFalse(vision.IsForcedVisible(unaffected, source, 4.9f));
            Assert.IsFalse(vision.IsForcedVisible(affected, source, 5f));
        }

        [Test]
        public void 敌方编队不能释放玩家专属嘲讽()
        {
            var source = Group("enemy_source", TeamId.Enemy, Vector2Int.zero);
            var player = Group("player", TeamId.Player, new Vector2Int(2, 0));
            Register(source, player);
            int count;
            string reason;
            Assert.IsFalse(TauntEffectService.TryActivate(source, 0f,
                out count, out reason));
            Assert.AreEqual("仅玩家编队可使用嘲讽", reason);
        }

        [Test]
        public void 受影响编队移除后状态立即清理()
        {
            var source = Group("player", TeamId.Player, Vector2Int.zero);
            var enemy = Group("enemy", TeamId.Enemy, new Vector2Int(2, 0));
            Register(source, enemy);
            int count;
            string reason;
            TauntEffectService.TryActivate(source, 0f, out count, out reason);
            TauntEffectService.RemoveGroup(enemy.GroupId);
            CommanderGroupRuntime forced;
            Assert.IsFalse(TauntEffectService.TryGetForcedTarget(enemy, 1f, out forced));
        }
    }
}
#endif
