using Mvp.Battle.Map;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Shared building placement rule (建筑平原约束): a building may only sit on plain
    /// terrain — never forest, hill, mountain, snow, desert, water, beach, river, bridge
    /// or road. <see cref="BuildingRegistry"/> uses this as the runtime final line of
    /// defense; the map generator enforces the same rule when choosing candidate cells.
    /// </summary>
    public static class BuildingPlacementRules
    {
        public static bool CellAllowed(TerrainType t)
        {
            return t == TerrainType.Plain;
        }
    }
}
