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
        const string RoadRoot = Root + "Roads/";

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
        static readonly Dictionary<string, Sprite> ConnectedCache =
            new Dictionary<string, Sprite>();
        static Material _chromaKeyMaterial;

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
                case TerrainType.Road:
                case TerrainType.Bridge: return 1.24f;
                default: return 1.24f;
            }
        }

        public static bool NeedsPlainUnderlay(TerrainType terrain)
        {
            return terrain == TerrainType.Forest;
        }

        public static Color GetDecorationBaseTint(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest: return new Color(0.55f, 0.78f, 0.52f);
                case TerrainType.Hill: return new Color(0.82f, 0.74f, 0.53f);
                case TerrainType.Mountain: return new Color(0.72f, 0.70f, 0.66f);
                case TerrainType.SnowMountain: return new Color(0.94f, 0.97f, 1f);
                case TerrainType.Desert: return new Color(1f, 0.82f, 0.48f);
                default: return Color.white;
            }
        }

        public static Sprite GetConnectedSprite(TerrainType terrain, int mask,
            out float rotationDegrees)
        {
            rotationDegrees = 0f;
            string name;
            if (terrain == TerrainType.Bridge)
            {
                name = "terrain_bridge_straight";
                rotationDegrees = HasHorizontal(mask) ? 90f : 0f;
            }
            else if (terrain == TerrainType.Road)
            {
                int count = CountBits(mask);
                if (count >= 4) name = "terrain_road_cross";
                else if (count == 3)
                {
                    name = "terrain_road_t";
                    rotationDegrees = RotationForT(mask);
                }
                else if (count == 2 && !IsOpposite(mask))
                {
                    name = "terrain_road_corner";
                    rotationDegrees = RotationForCorner(mask);
                }
                else
                {
                    name = "terrain_road_straight";
                    rotationDegrees = HasHorizontal(mask) ? 90f : 0f;
                }
            }
            else return GetSprite(terrain);

            if (ConnectedCache.TryGetValue(name, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>(RoadRoot + name);
            ConnectedCache[name] = sprite;
            return sprite;
        }

        public static Material GetChromaKeyMaterial()
        {
            if (_chromaKeyMaterial != null) return _chromaKeyMaterial;
            var shader = Resources.Load<Shader>("Battle/Terrain/SpriteChromaKey");
            if (shader != null) _chromaKeyMaterial = new Material(shader);
            return _chromaKeyMaterial;
        }

        public static float GetConnectedScale(Sprite sprite)
        {
            if (sprite == null || sprite.bounds.size.x <= 0.0001f)
                return GetScale(TerrainType.Plain);
            var plain = GetSprite(TerrainType.Plain);
            if (plain == null || plain.bounds.size.x <= 0.0001f)
                return GetScale(TerrainType.Plain);
            float targetWidth = plain.bounds.size.x * GetScale(TerrainType.Plain);
            return targetWidth / sprite.bounds.size.x;
        }

        static int CountBits(int mask)
        {
            int count = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        static bool HasHorizontal(int mask) { return (mask & 10) != 0; }
        static bool IsOpposite(int mask) { return mask == 5 || mask == 10; }

        static float RotationForCorner(int mask)
        {
            if (mask == 3) return 0f;
            if (mask == 6) return 90f;
            if (mask == 12) return 180f;
            return 270f;
        }

        static float RotationForT(int mask)
        {
            if ((mask & 1) == 0) return 0f;
            if ((mask & 2) == 0) return 90f;
            if ((mask & 4) == 0) return 180f;
            return 270f;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            ConnectedCache.Clear();
        }
    }
}
