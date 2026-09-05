using UnityEngine;

namespace Mvp.Battle.Map
{
    /// <summary>
    /// Static rules and visuals for terrain types. Colors are MVP placeholders so the
    /// test map reads clearly; the real oblique tile art can be attached later via the
    /// per-terrain art slots without touching the grid logic.
    /// </summary>
    public static class TerrainCatalog
    {
        public static bool IsWalkable(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Ocean:
                case TerrainType.SnowMountain:
                    return false;
                default:
                    return true;
            }
        }

        public static Color GetColor(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Plain: return new Color(0.52f, 0.73f, 0.44f);
                case TerrainType.Forest: return new Color(0.20f, 0.42f, 0.22f);
                case TerrainType.Hill: return new Color(0.58f, 0.52f, 0.32f);
                case TerrainType.Mountain: return new Color(0.56f, 0.55f, 0.52f);
                case TerrainType.SnowMountain: return new Color(0.87f, 0.90f, 0.94f);
                case TerrainType.Desert: return new Color(0.85f, 0.74f, 0.48f);
                case TerrainType.ShallowWater: return new Color(0.42f, 0.68f, 0.83f);
                case TerrainType.Ocean: return new Color(0.16f, 0.32f, 0.55f);
                case TerrainType.Road: return new Color(0.20f, 0.22f, 0.25f);
                case TerrainType.Bridge: return new Color(0.48f, 0.38f, 0.25f);
                default: return Color.magenta;
            }
        }

        /// <summary>Pseudo-3D height of a tile above the ground plane (for the iso camera).</summary>
        public static float GetElevation(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Hill: return 0.04f;
                case TerrainType.Mountain: return 0.08f;
                case TerrainType.SnowMountain: return 0.12f;
                default: return 0f;
            }
        }

        public static string GetDisplayName(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Plain: return "平原";
                case TerrainType.Forest: return "森林";
                case TerrainType.Hill: return "丘陵";
                case TerrainType.Mountain: return "山地";
                case TerrainType.SnowMountain: return "雪山";
                case TerrainType.Desert: return "沙漠";
                case TerrainType.ShallowWater: return "浅水";
                case TerrainType.Ocean: return "海洋";
                case TerrainType.Road: return "道路";
                case TerrainType.Bridge: return "桥梁";
                default: return t.ToString();
            }
        }
    }
}
