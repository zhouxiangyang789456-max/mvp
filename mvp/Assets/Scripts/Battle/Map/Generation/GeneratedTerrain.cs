namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Intermediate terrain types produced by <see cref="ProceduralMapGenerator"/>.
    /// This mirrors aw-map1's TERRAIN enum and is NOT the project's battle
    /// TerrainType. Always convert with <see cref="GeneratedTerrainMapper"/>
    /// before handing terrain to battle code.
    /// </summary>
    public enum GeneratedTerrain
    {
        Ocean = 0,
        Beach = 1,
        Plain = 2,
        Forest = 3,
        Mountain = 4,
        River = 5,
        Road = 6,
        Bridge = 7
    }

    /// <summary>Intermediate building types (aw-map1 BUILDINGS).</summary>
    public enum GeneratedBuilding
    {
        None = -1,
        Hq = 0,
        Factory = 1,
        City = 2
    }
}
