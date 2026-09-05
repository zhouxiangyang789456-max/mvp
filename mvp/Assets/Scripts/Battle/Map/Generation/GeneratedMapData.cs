using System.Collections.Generic;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// A simple integer grid coordinate used by pure-C# generation code so the
    /// generator does not depend on UnityEngine.Vector2Int.
    /// </summary>
    public readonly struct GridCoord
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>
    /// Output of <see cref="ProceduralMapGenerator"/>. Holds the intermediate
    /// terrain grid (aw-map1 style), optional buildings, stats, deployment hints
    /// and a stable map hash. Deployment areas are filled in a later phase.
    /// </summary>
    public sealed class GeneratedMapData
    {
        public int Width;
        public int Height;
        public uint Seed;
        public bool Mirror;
        public int GeneratorVersion;

        public GeneratedTerrain[,] Terrain;
        public GeneratedBuilding[,] Buildings; // null when Buildings disabled

        /// <summary>Count of each GeneratedTerrain value (indexed by enum value).</summary>
        public int[] TerrainStats;

        /// <summary>Count of each GeneratedBuilding value (House / Armory).</summary>
        public int[] BuildingStats;

        /// <summary>
        /// Result of validating building placement against the "must be plain" rule.
        /// Filled by <see cref="ProceduralMapGenerator"/> so the editor tool and tests
        /// can report requested/actual counts and any illegal placement.
        /// </summary>
        public BuildingPlacementReport BuildingReport;

        /// <summary>Recommended player deployment cells (filled in a later phase).</summary>
        public readonly List<GridCoord> PlayerDeploymentCells = new List<GridCoord>();

        /// <summary>Recommended enemy deployment cells (filled in a later phase).</summary>
        public readonly List<GridCoord> EnemyDeploymentCells = new List<GridCoord>();

        /// <summary>One contiguous deployment block per player commander group.</summary>
        public readonly List<DeploymentZone> PlayerDeploymentZones = new List<DeploymentZone>();

        /// <summary>One contiguous deployment block per enemy commander group.</summary>
        public readonly List<DeploymentZone> EnemyDeploymentZones = new List<DeploymentZone>();

        /// <summary>
        /// Spawn hints for buildings on the generated map (阶段B). The registry places
        /// these when present; the hand-authored TestMap has none and uses runtime defaults.
        /// </summary>
        public readonly List<BuildingSpawnData> BuildingSpawnData = new List<BuildingSpawnData>();

        /// <summary>
        /// Seed-stable timed-extraction portal; null when the level is an Elimination
        /// objective. Filled by <see cref="PortalPlacementPlanner"/> via the provider.
        /// </summary>
        public PortalSpawnData Portal;

        /// <summary>Stable hash of the final terrain grid + seed, used for golden tests and bug reports.</summary>
        public string MapHash;

        /// <summary>
        /// FNV-1a 32-bit over seed/version/dimensions and every terrain cell.
        /// Deterministic pure C#; detects unexpected generator behaviour changes.
        /// </summary>
        public static string ComputeHash(GeneratedMapData data)
        {
            uint hash = 2166136261u;
            hash = Combine(hash, data.Seed);
            hash = Combine(hash, (uint)data.Width);
            hash = Combine(hash, (uint)data.Height);
            hash = Combine(hash, (uint)data.GeneratorVersion);
            hash = Combine(hash, data.Mirror ? 1u : 0u);
            for (int y = 0; y < data.Height; y++)
            for (int x = 0; x < data.Width; x++)
                hash = Combine(hash, (uint)(int)data.Terrain[y, x]);

            // Fold the extraction portal into the hash so a changed portal position or
            // objective config also invalidates golden tests (限时传送门撤离关卡 §4).
            if (data.Portal != null)
            {
                hash = Combine(hash, (uint)data.Portal.AnchorCell.x);
                hash = Combine(hash, (uint)data.Portal.AnchorCell.y);
                hash = Combine(hash, (uint)data.Portal.Width);
                hash = Combine(hash, (uint)data.Portal.Height);
                hash = Combine(hash, (uint)data.Portal.TimeLimitSeconds);
            }

            return hash.ToString("X8");
        }

        static uint Combine(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }

    /// <summary>
    /// Summary of building placement for one generated map. Requested counts come from
    /// <see cref="MapGenerationSettings"/>; placed counts and any illegal placement are
    /// measured on the final terrain/buildings grids (after roads). A valid map has zero
    /// non-plain, out-of-bounds and overlapping building cells.
    /// </summary>
    public sealed class BuildingPlacementReport
    {
        public int RequestedHouse;
        public int RequestedArmory;
        public int PlacedHouse;
        public int PlacedArmory;
        public int NonPlainCells;
        public int OutOfBoundsCells;
        public int OverlapCells;

        public bool IsValid
        {
            get { return NonPlainCells == 0 && OutOfBoundsCells == 0 && OverlapCells == 0; }
        }
    }
}
