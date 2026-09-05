using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Skills;
using Mvp.Shared.Skills;

namespace Mvp.EditorTests.Battle.Skills
{
    public sealed class TacticalDecoyRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            TacticalDecoyService.Shutdown();
        }

        [Test]
        public void Catalog_UsesApprovedMvpValues()
        {
            var def = SkillCatalog.Get(SkillIds.Decoy);
            Assert.IsNotNull(def);
            Assert.AreEqual("疑兵", def.DisplayName);
            Assert.AreEqual(4, def.RangeCells);
            Assert.AreEqual(6, def.EffectRangeCells);
            Assert.AreEqual(8f, def.DurationSeconds);
            Assert.AreEqual(18f, def.CooldownSeconds);
            Assert.AreEqual(80, def.EffectHealth);
        }

        [Test]
        public void PlacementRange_UsesChebyshevDistance()
        {
            Assert.IsTrue(TacticalDecoyService.IsInRange(Vector2Int.zero,
                new Vector2Int(4, 4), 4));
            Assert.IsFalse(TacticalDecoyService.IsInRange(Vector2Int.zero,
                new Vector2Int(5, 0), 4));
        }

        [Test]
        public void LureRange_IncludesSixAndExcludesSevenCells()
        {
            Assert.IsTrue(TacticalDecoyService.IsInRange(Vector2Int.zero,
                new Vector2Int(6, -6), 6));
            Assert.IsFalse(TacticalDecoyService.IsInRange(Vector2Int.zero,
                new Vector2Int(7, 0), 6));
        }
    }
}
