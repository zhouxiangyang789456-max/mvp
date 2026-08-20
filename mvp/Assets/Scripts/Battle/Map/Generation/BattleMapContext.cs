namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Static hand-off for the battle map, mirroring the BattleStartContext pattern.
    /// The level-select / lobby scene sets PendingRequest before loading the battle
    /// scene; the battle reads it in BattleGridController.Awake and stores the
    /// produced data for the unit spawner, camera and editor tools.
    /// </summary>
    public static class BattleMapContext
    {
        public static BattleMapRequest PendingRequest;
        public static GeneratedMapData LastGeneratedData;
        public static GeneratedMapIdentity LastIdentity;
    }
}
