#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;

namespace Mvp.EditorTests.MapGeneration
{
    public sealed class DeploymentAreaPlannerTests
    {
        [Test]
        public void TestBattleMap_AllocatesPlayerAndEnemyZones()
        {
            var plan = DeploymentAreaPlanner.Plan(TestBattleMapData.Create(), 1, 2,
                TerrainCatalog.IsWalkable);

            Assert.IsTrue(plan.Passed, plan.FailureReason);
            Assert.AreEqual(1, plan.PlayerZones.Count);
            Assert.AreEqual(2, plan.EnemyZones.Count);
            AssertZonesAreValidAndDisjoint(TestBattleMapData.Create(), plan);
        }

        [Test]
        public void OpenMap_AllocatesSixPlayerAndTwoEnemyZones()
        {
            var terrain = FilledMap(24, 18, TerrainType.Plain);
            var plan = DeploymentAreaPlanner.Plan(terrain, 6, 2,
                TerrainCatalog.IsWalkable);

            Assert.IsTrue(plan.Passed, plan.FailureReason);
            Assert.AreEqual(6, plan.PlayerZones.Count);
            Assert.AreEqual(2, plan.EnemyZones.Count);
            AssertZonesAreValidAndDisjoint(terrain, plan);
        }

        [Test]
        public void BlockedMap_ReturnsSpecificFailure()
        {
            var terrain = FilledMap(16, 14, TerrainType.Ocean);
            var plan = DeploymentAreaPlanner.Plan(terrain, 1, 2,
                TerrainCatalog.IsWalkable);

            Assert.IsFalse(plan.Passed);
            StringAssert.Contains("3x3", plan.FailureReason);
        }

        static TerrainType[,] FilledMap(int width, int height, TerrainType value)
        {
            var result = new TerrainType[height, width];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[y, x] = value;
            return result;
        }

        static void AssertZonesAreValidAndDisjoint(TerrainType[,] terrain,
            DeploymentPlan plan)
        {
            var used = new HashSet<string>();
            var all = new List<DeploymentZone>();
            all.AddRange(plan.PlayerZones);
            all.AddRange(plan.EnemyZones);
            for (int z = 0; z < all.Count; z++)
            {
                Assert.AreEqual(9, all[z].Cells.Count);
                for (int i = 0; i < all[z].Cells.Count; i++)
                {
                    GridCoord cell = all[z].Cells[i];
                    Assert.IsTrue(TerrainCatalog.IsWalkable(terrain[cell.Y, cell.X]));
                    Assert.IsTrue(used.Add(cell.X + ":" + cell.Y),
                        "Deployment cell overlaps at " + cell);
                    Assert.LessOrEqual(System.Math.Abs(cell.X - all[z].Anchor.X), 1);
                    Assert.LessOrEqual(System.Math.Abs(cell.Y - all[z].Anchor.Y), 1);
                }
            }
        }
    }
}
#endif
