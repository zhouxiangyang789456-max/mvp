namespace Mvp.Battle.Map
{
    /// <summary>
    /// Hand-written 12x10 test map matching the reference layout: rear hills/mountains/
    /// snow, center plains, left forest, front desert/shallow water, outer ocean edges.
    /// Rows are indexed [z][x] with z=0 at the rear (top of the iso view) and z=9 at the
    /// front (bottom of the iso view).
    /// </summary>
    public static class TestBattleMapData
    {
        public const int Width = 12;
        public const int Height = 10;

        // Rows are mirrored on X so the forest sits on the screen-LEFT and the ocean
        // column on the screen-RIGHT when viewed by the battle camera (yaw -135).
        static readonly string[] Rows =
        {
            "OOOSSSSSSOOO", // z=0 rear: snow, ocean corners
            "OOSSMMMMSSSO",
            "OOSMMMMMMMSO",
            "OOHMMMMMMHHOO",
            "OOHHMMMMHHHO",
            "OOHFHHHHHFHO",
            "OOFFFPPPFFFO",
            "WFFDDPPDDFFW",
            "WWWDDDDDDWWW",
            "OOOWWWWWWOOO", // z=9 front: water, desert behind
        };

        public static TerrainType[,] Create()
        {
            var map = new TerrainType[Height, Width];
            for (int z = 0; z < Height; z++)
            {
                string row = Rows[z];
                for (int x = 0; x < Width && x < row.Length; x++)
                {
                    map[z, x] = CharToTerrain(row[x]);
                }
            }
            return map;
        }

        static TerrainType CharToTerrain(char c)
        {
            switch (c)
            {
                case 'P': return TerrainType.Plain;
                case 'F': return TerrainType.Forest;
                case 'H': return TerrainType.Hill;
                case 'M': return TerrainType.Mountain;
                case 'S': return TerrainType.SnowMountain;
                case 'D': return TerrainType.Desert;
                case 'W': return TerrainType.ShallowWater;
                case 'O': return TerrainType.Ocean;
                default: return TerrainType.Plain;
            }
        }
    }
}
