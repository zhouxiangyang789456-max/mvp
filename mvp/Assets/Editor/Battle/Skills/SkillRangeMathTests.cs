#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Skills;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.EditorTests.Battle.Skills
{
    /// <summary>
    /// 远攻射程数学(战斗技能系统开发文档 §7.2):Chebyshev 格距 × RangeMultiplier。
    /// 全静态,直接调用 SkillRangeMath。
    /// </summary>
    public sealed class SkillRangeMathTests
    {
        static readonly SkillDefinition LongRange = SkillCatalog.Get(SkillIds.LongRange);

        static UnitView Ranged(string id, Vector2Int cell, float min, float max)
        {
            return SkillTestFixtures.MakeUnit(SkillTestFixtures.MakeData(id,
                SkillTestFixtures.MakeDefinition(UnitTag.Ranged, min, max), cell));
        }

        [Test]
        public void Chebyshev_斜向距离_等于最大轴距()
        {
            Assert.AreEqual(3, SkillRangeMath.Chebyshev(new Vector2Int(0, 0),
                new Vector2Int(2, 3)));
            Assert.AreEqual(0, SkillRangeMath.Chebyshev(new Vector2Int(5, 5),
                new Vector2Int(5, 5)));
        }

        [Test]
        public void ComputeMemberRanges_射程按1_5倍放大()
        {
            var unit = Ranged("u1", new Vector2Int(0, 0), 0f, 4f);
            int min, max;
            SkillRangeMath.ComputeMemberRanges(unit, LongRange, out min, out max);
            Assert.AreEqual(0, min);
            Assert.AreEqual(6, max); // RoundToInt(4 * 1.5)
        }

        [Test]
        public void ComputeMemberRanges_带盲区_最小射程同样放大()
        {
            var unit = Ranged("u1", new Vector2Int(0, 0), 2f, 4f);
            int min, max;
            SkillRangeMath.ComputeMemberRanges(unit, LongRange, out min, out max);
            Assert.AreEqual(3, min); // RoundToInt(2 * 1.5)
            Assert.AreEqual(6, max);
        }

        [Test]
        public void IsCellInRange_边界与越界()
        {
            var unit = Ranged("u1", new Vector2Int(5, 5), 0f, 4f);
            Assert.IsTrue(SkillRangeMath.IsCellInRange(new Vector2Int(8, 7), unit, LongRange));
            Assert.IsTrue(SkillRangeMath.IsCellInRange(new Vector2Int(11, 5), unit, LongRange)); // cheb 6 = max
            Assert.IsFalse(SkillRangeMath.IsCellInRange(new Vector2Int(12, 5), unit, LongRange)); // cheb 7 > max
        }

        [Test]
        public void IsCellInRange_盲区_最小射程内不可用()
        {
            // After ×1.5: min = RoundToInt(2*1.5) = 3, max = 6. Cheb < 3 is the blind zone.
            var unit = Ranged("u1", new Vector2Int(5, 5), 2f, 4f);
            Assert.IsFalse(SkillRangeMath.IsCellInRange(new Vector2Int(7, 5), unit, LongRange)); // cheb 2 < 3
            Assert.IsTrue(SkillRangeMath.IsCellInRange(new Vector2Int(8, 5), unit, LongRange));  // cheb 3 = min
        }

        [Test]
        public void IsCellCoveredByAny_任一成员覆盖_即可()
        {
            var u1 = Ranged("u1", new Vector2Int(0, 0), 0f, 2f);
            var u2 = Ranged("u2", new Vector2Int(10, 0), 0f, 4f);
            var members = new List<UnitView> { u1, u2 };
            Assert.IsTrue(SkillRangeMath.IsCellCoveredByAny(new Vector2Int(12, 0), members,
                LongRange)); // covered by u2 (cheb 2 <= 6)
            Assert.IsFalse(SkillRangeMath.IsCellCoveredByAny(new Vector2Int(20, 0), members,
                LongRange)); // neither covers
        }
    }
}
#endif
