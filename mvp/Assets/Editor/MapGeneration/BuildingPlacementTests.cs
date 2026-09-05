#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;
using Mvp.Battle.Buildings;

namespace Mvp.EditorTests.MapGeneration
{
    /// <summary>
    /// Tests for the 建筑平原约束 (buildings only on plain terrain) pipeline:
    /// generator placement + roads, the provider's StoreBuildings data conversion,
    /// the runtime BuildingPlacementRules, and the BuildingPlacementReport produced
    /// by <see cref="ProceduralMapGenerator"/>.
    /// </summary>
    public sealed class BuildingPlacementTests
    {
        static MapGenerationSettings BaseSettings()
        {
            return new MapGenerationSettings
            {
                Width = 16,
                Height = 14,
                Seed = 20260818u,
                Buildings = true,
                HouseCount = 5,
                ArmoryCount = 2,
                Ocean = true,
                Beach = true,
                River = true,
                Forest = true,
                Mountain = true
            };
        }

        // ---- generator: free maps ----------------------------------------------

        [Test]
        public void FreeMaps_AllBuildingsOnPlainAcrossSeeds()
        {
            for (uint seed = 1; seed <= 50; seed++)
            {
                var settings = BaseSettings();
                settings.Mirror = false;
                settings.Seed = seed;

                var data = ProceduralMapGenerator.Generate(settings);

                Assert.IsNotNull(data.Buildings);
                Assert.IsTrue(data.BuildingReport.IsValid,
                    "seed=" + seed + " report=" + ReportText(data.BuildingReport));
                Assert.AreEqual(0, data.BuildingReport.NonPlainCells, "seed=" + seed);
                AssertAreAllBuildingsOnPlain(data, "free seed=" + seed);
            }
        }

        // ---- generator: mirror maps ---------------------------------------------

        [Test]
        public void MirrorMaps_BuildingsSymmetricAndOnPlain()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var settings = BaseSettings();
                settings.Mirror = true;
                settings.Seed = seed;

                var data = ProceduralMapGenerator.Generate(settings);

                Assert.IsTrue(data.Mirror, "seed=" + seed);
                Assert.IsTrue(data.BuildingReport.IsValid,
                    "seed=" + seed + " report=" + ReportText(data.BuildingReport));

                // Mirror-mode counts are per side (user-confirmed semantic).
                Assert.AreEqual(settings.HouseCount, data.BuildingReport.RequestedHouse, "seed=" + seed);
                Assert.AreEqual(settings.ArmoryCount, data.BuildingReport.RequestedArmory, "seed=" + seed);

                int w = data.Width;
                int h = data.Height;
                for (int y = 0; y < h; y++)
                for (int x = (w + 1) / 2; x < w; x++)
                {
                    Assert.AreEqual(data.Buildings[h - 1 - y, w - 1 - x], data.Buildings[y, x],
                        "mirror building mismatch seed=" + seed + " at (" + x + "," + y + ")");
                }
                AssertAreAllBuildingsOnPlain(data, "mirror seed=" + seed);
            }
        }

        // ---- generator: small maps ----------------------------------------------

        [Test]
        public void SmallMap_BuildingsStayOnPlainAndWithinPlainCells()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var settings = new MapGenerationSettings
                {
                    Width = 8,
                    Height = 8,
                    Seed = seed,
                    Buildings = true,
                    HouseCount = 8,
                    ArmoryCount = 4,
                    Ocean = true,
                    Beach = true,
                    River = true,
                    Forest = true,
                    Mountain = true
                };

                var data = ProceduralMapGenerator.Generate(settings);

                Assert.AreEqual(0, data.BuildingReport.NonPlainCells, "seed=" + seed);
                Assert.AreEqual(0, data.BuildingReport.OutOfBoundsCells, "seed=" + seed);
                int plainCount = data.TerrainStats[(int)GeneratedTerrain.Plain];
                int placed = data.BuildingReport.PlacedHouse + data.BuildingReport.PlacedArmory;
                Assert.LessOrEqual(placed, plainCount, "seed=" + seed);
            }
        }

        // ---- roads never cover buildings -----------------------------------------

        [Test]
        public void RoadsNeverCoverBuildingCells()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var settings = BaseSettings();
                settings.Mirror = false;
                settings.Seed = seed;

                var data = ProceduralMapGenerator.Generate(settings);

                for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    if (data.Buildings[y, x] == GeneratedBuilding.None) continue;
                    Assert.AreNotEqual(GeneratedTerrain.Road, data.Terrain[y, x],
                        "road over building seed=" + seed + " at (" + x + "," + y + ")");
                    Assert.AreNotEqual(GeneratedTerrain.Bridge, data.Terrain[y, x],
                        "bridge over building seed=" + seed + " at (" + x + "," + y + ")");
                }
            }
        }

        // ---- report counts match actual grid ---------------------------------------

        [Test]
        public void ReportCountsMatchActualPlacedBuildings()
        {
            var settings = BaseSettings();
            var data = ProceduralMapGenerator.Generate(settings);

            int placedHouse = 0;
            int placedArmory = 0;
            for (int y = 0; y < data.Height; y++)
            for (int x = 0; x < data.Width; x++)
            {
                if (data.Buildings[y, x] == GeneratedBuilding.House) placedHouse++;
                else if (data.Buildings[y, x] == GeneratedBuilding.Armory) placedArmory++;
            }

            Assert.AreEqual(settings.HouseCount, data.BuildingReport.RequestedHouse);
            Assert.AreEqual(settings.ArmoryCount, data.BuildingReport.RequestedArmory);
            Assert.AreEqual(placedHouse, data.BuildingReport.PlacedHouse);
            Assert.AreEqual(placedArmory, data.BuildingReport.PlacedArmory);
        }

        // ---- reproducibility --------------------------------------------------------

        [Test]
        public void SameSeedAndConfig_ProducesIdenticalMap()
        {
            var a = ProceduralMapGenerator.Generate(BaseSettings());
            var b = ProceduralMapGenerator.Generate(BaseSettings());

            Assert.AreEqual(a.MapHash, b.MapHash);
            Assert.AreEqual(a.BuildingReport.PlacedHouse, b.BuildingReport.PlacedHouse);
            Assert.AreEqual(a.BuildingReport.PlacedArmory, b.BuildingReport.PlacedArmory);
            AssertTerrainArraysEqual(a.Terrain, b.Terrain);
        }

        // ---- provider StoreBuildings normalization ------------------------------------

        [Test]
        public void StoreBuildings_NormalizesLegacyBuildingCellsToPlain()
        {
            var data = new GeneratedMapData
            {
                Width = 4,
                Height = 4,
                Seed = 1u,
                GeneratorVersion = 1,
                Mirror = false,
                Terrain = new GeneratedTerrain[4, 4],
                Buildings = new GeneratedBuilding[4, 4]
            };
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                data.Terrain[y, x] = GeneratedTerrain.Forest; // legacy non-plain placement
                data.Buildings[y, x] = GeneratedBuilding.None;
            }
            data.Buildings[1, 1] = GeneratedBuilding.House;
            data.Buildings[2, 3] = GeneratedBuilding.Armory;

            ProceduralBattleMapProvider.StoreBuildings(data);

            Assert.AreEqual(2, data.BuildingSpawnData.Count);
            Assert.AreEqual(GeneratedTerrain.Plain, data.Terrain[1, 1]); // house at y=1,x=1
            Assert.AreEqual(GeneratedTerrain.Plain, data.Terrain[2, 3]); // armory at y=2,x=3
            Assert.AreEqual("building_house", data.BuildingSpawnData[0].DefinitionId);
            Assert.AreEqual(new Vector2Int(1, 1), data.BuildingSpawnData[0].AnchorCell);
            Assert.AreEqual("building_armory", data.BuildingSpawnData[1].DefinitionId);
            Assert.AreEqual(new Vector2Int(3, 2), data.BuildingSpawnData[1].AnchorCell); // anchor = (x,y)
        }

        // ---- runtime BuildingPlacementRules ---------------------------------------------

        [Test]
        public void BuildingPlacementRules_OnlyPlainAllowed()
        {
            Assert.IsTrue(BuildingPlacementRules.CellAllowed(TerrainType.Plain));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.Forest));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.Hill));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.Mountain));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.SnowMountain));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.Desert));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.ShallowWater));
            Assert.IsFalse(BuildingPlacementRules.CellAllowed(TerrainType.Ocean));
        }

        // ---- catalog footprints ----------------------------------------------------------

        [Test]
        public void CatalogDefinitions_AreSingleCellFootprints()
        {
            var house = BuildingCatalog.Get("building_house");
            var armory = BuildingCatalog.Get("building_armory");
            Assert.IsNotNull(house);
            Assert.IsNotNull(armory);
            Assert.AreEqual(new Vector2Int(1, 1), house.Footprint);
            Assert.AreEqual(new Vector2Int(1, 1), armory.Footprint);
        }

        // ---- settings defaults + clone ----------------------------------------------------

        [Test]
        public void Settings_ExposeHouseAndArmoryCountsAndClone()
        {
            var s = new MapGenerationSettings();
            Assert.AreEqual(5, s.HouseCount);
            Assert.AreEqual(2, s.ArmoryCount);

            var clone = s.Clone();
            clone.HouseCount = 11;
            clone.ArmoryCount = 4;
            Assert.AreEqual(11, clone.HouseCount);
            Assert.AreEqual(4, clone.ArmoryCount);
            Assert.AreEqual(5, s.HouseCount);
            Assert.AreEqual(2, s.ArmoryCount);
        }

        // ---- helpers -------------------------------------------------------------------------

        static string ReportText(BuildingPlacementReport r)
        {
            return "req=" + r.RequestedHouse + "/" + r.RequestedArmory
                + " placed=" + r.PlacedHouse + "/" + r.PlacedArmory
                + " nonPlain=" + r.NonPlainCells
                + " oob=" + r.OutOfBoundsCells
                + " overlap=" + r.OverlapCells;
        }

        static void AssertAreAllBuildingsOnPlain(GeneratedMapData data, string context)
        {
            for (int y = 0; y < data.Height; y++)
            for (int x = 0; x < data.Width; x++)
            {
                if (data.Buildings[y, x] == GeneratedBuilding.None) continue;
                Assert.AreEqual(GeneratedTerrain.Plain, data.Terrain[y, x],
                    context + " building not on plain at (" + x + "," + y + ")");
            }
        }

        static void AssertTerrainArraysEqual(GeneratedTerrain[,] a, GeneratedTerrain[,] b)
        {
            Assert.AreEqual(a.GetLength(0), b.GetLength(0));
            Assert.AreEqual(a.GetLength(1), b.GetLength(1));
            for (int y = 0; y < a.GetLength(0); y++)
            for (int x = 0; x < a.GetLength(1); x++)
                Assert.AreEqual(a[y, x], b[y, x], "terrain mismatch at (" + x + "," + y + ")");
        }
    }
}
#endif
