namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Lightweight identity of one generated battle map. Saved in logs / save data
    /// so a specific map can be reproduced later (see 随机地图生成接入方案 §7).
    /// </summary>
    public sealed class GeneratedMapIdentity
    {
        public string ProfileId;
        public int ProfileVersion;
        public int GeneratorVersion;
        public int LevelIndex;
        public string RuleId;
        public uint FinalSeed;
        public int AttemptIndex;
        public bool UsedFallback;
        public string MapHash;

        public override string ToString()
        {
            return "level=" + LevelIndex
                + " profile=" + ProfileId + "v" + ProfileVersion
                + " gen=" + GeneratorVersion
                + " rule=" + RuleId
                + " seed=" + FinalSeed
                + " attempt=" + AttemptIndex
                + " fallback=" + UsedFallback
                + " hash=" + MapHash;
        }
    }
}
