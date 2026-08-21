using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map
{
    /// <summary>
    /// Runtime terrain-art lookup. Resources paths keep BattleScene free from eight
    /// fragile serialized sprite references while retaining the color-tile fallback.
    /// </summary>
    public static class TerrainVisualCatalog
    {
        const string Root = "Battle/Terrain/Generated/";

        static readonly Dictionary<TerrainType, string> Paths =
            new Dictionary<TerrainType, string>
            {
                { TerrainType.Plain, Root + "terrain_plain_01" },
                { TerrainType.Forest, Root + "terrain_forest_01" },
                { TerrainType.Hill, Root + "terrain_hill_01" },
                { TerrainType.Mountain, Root + "terrain_mountain_01" },
                { TerrainType.SnowMountain, Root + "terrain_snow_mountain_01" },
                { TerrainType.Desert, Root + "terrain_desert_01" },
                { TerrainType.ShallowWater, Root + "terrain_shallow_water_01" },
                { TerrainType.Ocean, Root + "terrain_ocean_01" }
            };

        static readonly Dictionary<TerrainType, Sprite> Cache =
            new Dictionary<TerrainType, Sprite>();

        public static Sprite GetSprite(TerrainType terrain)
        {
            if (Cache.TryGetValue(terrain, out var cached)) return cached;
            if (!Paths.TryGetValue(terrain, out var path)) return null;
            var sprite = Resources.Load<Sprite>(path);
            Cache[terrain] = sprite;
            return sprite;
        }

        public static float GetScale(TerrainType terrain)
        {
            // The generated files share a canvas size, but their painted diamond
            // footprints differ. Scale only the undersized art instead of widening
            // every tile and making water overlap excessively.
            switch (terrain)
            {
                case TerrainType.Desert: return 1.48f;
                default: return 1.24f;
            }
        }

        public static bool NeedsPlainUnderlay(TerrainType terrain)
        {
            return terrain == TerrainType.Forest;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
