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

        /// <summary>Count of each GeneratedBuilding value for Hq/Factory/City.</summary>
        public int[] BuildingStats;

        /// <summary>Recommended player deployment cells (filled in a later phase).</summary>
        public readonly List<GridCoord> PlayerDeploymentCells = new List<GridCoord>();

        /// <summary>Recommended enemy deployment cells (filled in a later phase).</summary>
        public readonly List<GridCoord> EnemyDeploymentCells = new List<GridCoord>();

        /// <summary>One contiguous deployment block per player commander group.</summary>
        public readonly List<DeploymentZone> PlayerDeploymentZones = new List<DeploymentZone>();

        /// <summary>One contiguous deployment block per enemy commander group.</summary>
        public readonly List<DeploymentZone> EnemyDeploymentZones = new List<DeploymentZone>();

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
            return hash.ToString("X8");
        }

        static uint Combine(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }
}
