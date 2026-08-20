using System;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>How a rule derives its seed. See 随机地图生成接入方案 §7.</summary>
    public enum SeedMode
    {
        Fixed,
        LevelBased,
        ProfileSeedPlusLevel,
        RandomAtRuntime
    }

    /// <summary>
    /// What the battle scene needs to build its map. Level-driven rule selection
    /// (LevelMapGenerationProfile) arrives in a later phase; the request already
    /// carries the level + seed strategy so the battle can log a reproducible
    /// identity. Pure C#.
    /// </summary>
    public sealed class BattleMapRequest
    {
        public string ProfileId;
        public int ProfileVersion;
        public string RuleId;

        public int LevelIndex = 1;
        public SeedMode SeedMode = SeedMode.Fixed;
        public uint FixedSeed = 20260818u;
        public uint ProfileSalt;

        public int RetryCount = 10;

        public MapGenerationSettings Settings;

        public float MinWalkableRatio = 0.50f;
        public float MaxWalkableRatio = 0.90f;
        public float MinWalkableComponentRatio = 0.90f;

        public int PlayerDeploymentGroupCount = 1;
        public int EnemyDeploymentGroupCount = 2;

        /// <summary>Resolves the deterministic seed for this request. RandomAtRuntime is the only non-deterministic mode.</summary>
        public uint ResolveSeed()
        {
            switch (SeedMode)
            {
                case SeedMode.LevelBased:
                    return unchecked((uint)(LevelIndex * 1009 + (int)ProfileSalt));
                case SeedMode.ProfileSeedPlusLevel:
                    return unchecked((uint)((int)FixedSeed + LevelIndex * 1009 + (int)ProfileSalt));
                case SeedMode.RandomAtRuntime:
                    long ticks = DateTime.UtcNow.Ticks;
                    return unchecked((uint)(ticks ^ (ticks >> 32)));
                default:
                    return FixedSeed;
            }
        }
    }
}
