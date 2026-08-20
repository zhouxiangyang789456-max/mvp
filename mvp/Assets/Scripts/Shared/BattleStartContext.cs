using Mvp.Battle.Map.Generation;

namespace Mvp.Shared
{
    /// <summary>
    /// Hand-off data carried from CommanderSelectScene to BattleScene.
    /// Stored statically so the battle page knows which commander was picked.
    /// </summary>
    public static class BattleStartContext
    {
        public static ExpeditionRosterSnapshot ExpeditionRoster;

        // Compatibility fallback for opening BattleScene directly and for older saves.
        public static CommanderDefinition SelectedCommander;

        // ---- Level-driven random map (随机地图生成接入方案 §7) ----
        // The pre-battle scene writes the current level + the map rule profile; the
        // battle grid resolves the rule and generates/validates the map from them.
        public static int LevelIndex = 1;
        public static LevelMapGenerationProfile MapProfile;
    }
}
