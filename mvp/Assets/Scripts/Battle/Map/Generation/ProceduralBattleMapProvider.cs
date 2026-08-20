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
    }
}
