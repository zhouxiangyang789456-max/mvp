using System;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Bridge between the pure generator and the battle scene. Produces a battle
    /// TerrainType[,] plus a reproducible identity, validating and retrying per the
    /// request, then falling back to TestBattleMapData so a battle always starts.
    /// </summary>
    public static class ProceduralBattleMapProvider
    {
        public const int GeneratorVersion = ProceduralMapGenerator.GeneratorVersion;

        public static TerrainType[,] CreateBattleMap(BattleMapRequest request,
            out GeneratedMapData data, out GeneratedMapIdentity identity)
        {
            if (request == null)
            {
                data = null;
                identity = new GeneratedMapIdentity
                {
                    GeneratorVersion = GeneratorVersion,
                    UsedFallback = true,
                    MapHash = "fallback"
                };
                return TestBattleMapData.Create();
            }

            var settings = request.Settings != null ? request.Settings : new MapGenerationSettings();
            int retryCount = Math.Max(1, request.RetryCount);
            uint baseSeed = request.ResolveSeed();
            string lastFail = "no attempts";

            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                var attemptSettings = settings.Clone();
                // Deterministic retries: step the seed by a fixed prime so the same
                // request always fails/recovers in the same order.
                attemptSettings.Seed = unchecked(baseSeed + (uint)attempt * 7919u);

                var generated = ProceduralMapGenerator.Generate(attemptSettings);

                // Normalize building cells to plain terrain and build spawn hints BEFORE
                // converting to the battle grid, so the runtime grid reflects the
                // "buildings sit on plain" rule (also fixes legacy generated data).
                StoreBuildings(generated);
                var battle = GeneratedTerrainMapper.ToBattleGrid(generated);

                var validation = MapGenerationValidator.Validate(
                    battle, generated.Width, generated.Height,
                    requireMirror: generated.Mirror,
                    minWalkableRatio: request.MinWalkableRatio,
                    maxWalkableRatio: request.MaxWalkableRatio,
                    minWalkableComponentRatio: request.MinWalkableComponentRatio,
                    TerrainCatalog.IsWalkable);

                if (validation.Passed)
                {
                    var deployment = DeploymentAreaPlanner.Plan(battle,
                        request.PlayerDeploymentGroupCount,
                        request.EnemyDeploymentGroupCount,
                        TerrainCatalog.IsWalkable);
                    if (!deployment.Passed)
                    {
                        lastFail = deployment.FailureReason;
                        continue;
                    }

                    StoreDeployment(generated, deployment);

                    // 限时传送门撤离关卡: place the seed-stable extraction portal when
                    // enabled. A legal spot is required (unreachable / overlapped / out of
                    // distance band) otherwise this seed fails and the retry loop advances.
                    if (attemptSettings.EnableExtractionPortal)
                    {
                        generated.Portal = PortalPlacementPlanner.Plan(generated, deployment,
                            attemptSettings, out var portalFailure);
                        if (generated.Portal == null)
                        {
                            lastFail = portalFailure;
                            continue;
                        }
                        generated.MapHash = GeneratedMapData.ComputeHash(generated);
                    }

                    data = generated;
                    identity = new GeneratedMapIdentity
                    {
                        ProfileId = request.ProfileId,
                        ProfileVersion = request.ProfileVersion,
                        GeneratorVersion = GeneratorVersion,
                        LevelIndex = request.LevelIndex,
                        RuleId = request.RuleId,
                        FinalSeed = generated.Seed,
                        AttemptIndex = attempt,
                        UsedFallback = false,
                        MapHash = generated.MapHash
                    };
                    return battle;
                }
                lastFail = validation.ToString();
            }

            // Fallback so the battle never fails to start.
            data = null;
            identity = new GeneratedMapIdentity
            {
                ProfileId = request.ProfileId,
                ProfileVersion = request.ProfileVersion,
                GeneratorVersion = GeneratorVersion,
                LevelIndex = request.LevelIndex,
                RuleId = request.RuleId,
                FinalSeed = baseSeed,
                AttemptIndex = retryCount,
                UsedFallback = true,
                MapHash = "fallback"
            };
            Debug.LogWarning("[ProceduralBattleMapProvider] generation failed after " + retryCount +
                " attempts (" + lastFail + "); using test map. identity=" + identity);
            return TestBattleMapData.Create();
        }

        static void StoreDeployment(GeneratedMapData data, DeploymentPlan plan)
        {
            data.PlayerDeploymentZones.AddRange(plan.PlayerZones);
            data.EnemyDeploymentZones.AddRange(plan.EnemyZones);
            CopyCells(plan.PlayerZones, data.PlayerDeploymentCells);
            CopyCells(plan.EnemyZones, data.EnemyDeploymentCells);
        }

        static void CopyCells(System.Collections.Generic.List<DeploymentZone> zones,
            System.Collections.Generic.List<GridCoord> output)
        {
            for (int z = 0; z < zones.Count; z++)
                for (int i = 0; i < zones[z].Cells.Count; i++)
                    output.Add(zones[z].Cells[i]);
        }

        /// <summary>
        /// Converts the generator's single-cell building grid into battle spawn hints
        /// (阶段B). Each building marker maps to one formal building on one grid cell, and
        /// that cell is normalized to plain terrain. Out-of-bounds markers are skipped and
        /// logged. This is the data-conversion fix that also repairs legacy generated data.
        /// </summary>
        public static void StoreBuildings(GeneratedMapData data)
        {
            if (data.Buildings == null) return;
            int w = data.Width;
            int h = data.Height;
            data.BuildingSpawnData.Clear();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var b = data.Buildings[y, x];
                    if (b == GeneratedBuilding.None) continue;

                    string id;
                    switch (b)
                    {
                        case GeneratedBuilding.House: id = "building_house"; break;
                        case GeneratedBuilding.Armory: id = "building_armory"; break;
                        default: continue;
                    }

                    if (x < 0 || y < 0 || x >= w || y >= h)
                    {
                        UnityEngine.Debug.LogWarning("[ProceduralBattleMapProvider] building out of bounds, skipped. "
                            + "seed=" + data.Seed + " type=" + b + " cell=(" + x + "," + y + ")");
                        continue;
                    }

                    // Normalize the building cell to plain (new generator output is already
                    // plain, so this is a no-op for it; legacy data gets repaired here).
                    data.Terrain[y, x] = GeneratedTerrain.Plain;
                    data.BuildingSpawnData.Add(new BuildingSpawnData
                    {
                        DefinitionId = id,
                        AnchorCell = new Vector2Int(x, y)
                    });
                }
            }

            // Final line of defense: every produced spawn anchor must be on plain terrain.
            for (int i = 0; i < data.BuildingSpawnData.Count; i++)
            {
                var anchor = data.BuildingSpawnData[i].AnchorCell;
                if (anchor.x < 0 || anchor.y < 0 || anchor.x >= w || anchor.y >= h ||
                    data.Terrain[anchor.y, anchor.x] != GeneratedTerrain.Plain)
                {
                    UnityEngine.Debug.LogWarning("[ProceduralBattleMapProvider] spawn anchor not on plain terrain. "
                        + "seed=" + data.Seed + " id=" + data.BuildingSpawnData[i].DefinitionId
                        + " anchor=" + anchor);
                }
            }
        }
    }
}
