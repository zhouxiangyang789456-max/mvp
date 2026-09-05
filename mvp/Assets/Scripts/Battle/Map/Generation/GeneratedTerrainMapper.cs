using Mvp.Battle.Map;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Converts the generator's aw-map1-style intermediate terrain into the
    /// project's battle <see cref="TerrainType"/>. First-version mapping keeps the
    /// battle TerrainType set, including dedicated Road and Bridge visuals, and keeps
    /// everything walkable except Ocean, matching TerrainCatalog.IsWalkable.
    ///
    /// Table (see 随机地图生成接入方案 §4):
    ///   Ocean -> Ocean (blocked)
    ///   Beach -> Desert (walkable land coast)
    ///   Plain -> Plain
    ///   Forest -> Forest
    ///   Mountain -> Mountain (walkable; not SnowMountain so we avoid visual confusion)
    ///   River -> ShallowWater (walkable, no bridge system yet)
    ///   Road -> Road
    ///   Bridge -> Bridge
    /// </summary>
    public static class GeneratedTerrainMapper
    {
        public static TerrainType ToBattleTerrain(GeneratedTerrain t)
        {
            switch (t)
            {
                case GeneratedTerrain.Ocean: return TerrainType.Ocean;
                case GeneratedTerrain.Beach: return TerrainType.Desert;
                case GeneratedTerrain.Plain: return TerrainType.Plain;
                case GeneratedTerrain.Forest: return TerrainType.Forest;
                case GeneratedTerrain.Mountain: return TerrainType.Mountain;
                case GeneratedTerrain.River: return TerrainType.ShallowWater;
                case GeneratedTerrain.Road: return TerrainType.Road;
                case GeneratedTerrain.Bridge: return TerrainType.Bridge;
                default: return TerrainType.Plain;
            }
        }

        public static TerrainType[,] ToBattleGrid(GeneratedMapData data)
        {
            int h = data.Height;
            int w = data.Width;
            var result = new TerrainType[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[y, x] = ToBattleTerrain(data.Terrain[y, x]);
            return result;
        }
    }
}
