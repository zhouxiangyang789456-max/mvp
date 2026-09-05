using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    public static class HandMapBattleMapProvider
    {
        public static TerrainType[,] CreateBattleMap(HandAuthoredMapData map,
            HashSet<Vector2Int> blockedCells)
        {
            int width = Mathf.Max(1, map != null ? map.Width : 1);
            int height = Mathf.Max(1, map != null ? map.Height : 1);
            var result = new TerrainType[height, width];
            blockedCells.Clear();
            if (map == null || map.Tiles == null) return result;

            var priority = new int[height, width];
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height) continue;
                if (tile.Category == HandTileCategory.Building)
                    blockedCells.Add(new Vector2Int(tile.X, tile.Y));
                if (tile.Category == HandTileCategory.Mountain ||
                    tile.Category == HandTileCategory.Decoration)
                    blockedCells.Add(new Vector2Int(tile.X, tile.Y));
                if (tile.Z != 0) continue;
                var terrain = Resolve(tile.Category);
                int candidatePriority = Priority(tile.Category);
                if (candidatePriority >= priority[tile.Y, tile.X])
                {
                    priority[tile.Y, tile.X] = candidatePriority;
                    result[tile.Y, tile.X] = terrain;
                }
            }

            // 决策 5：Z>=1 Bridge/Ramp 提升，让"架在水上的桥"在走位上可通。
            // 任何 Z>=1 的 Bridge tile → 该 (x,y) 最终 TerrainType 提升为 Bridge(覆盖 Z=0 Ocean)。
            // 任何 Z>=1 的 Ramp tile → 标记为可走(保留 Plain,但 Priority 提升避免被 Ground 覆盖)。
            // 不影响视觉（视觉层仍按 Y 偏移渲染），只改走位层 TerrainType[,]。
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.Z < 1) continue;
                if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height) continue;
                if (tile.Category != HandTileCategory.Bridge &&
                    tile.Category != HandTileCategory.Ramp) continue;
                int candidatePriority = Priority(tile.Category);
                if (candidatePriority > priority[tile.Y, tile.X])
                {
                    priority[tile.Y, tile.X] = candidatePriority;
                    result[tile.Y, tile.X] = Resolve(tile.Category);
                }
            }
            return result;
        }

        static TerrainType Resolve(HandTileCategory category)
        {
            switch (category)
            {
                case HandTileCategory.Path: return TerrainType.Road;
                case HandTileCategory.Forest: return TerrainType.Forest;
                // HandMap 水一律按深水 Ocean 走：陆军单位无法在河里行军（程序化关卡仍走
                // ShallowWater=可走的浅滩语义；只在玩家手作的地图里把水视为不可通行的河流）。
                case HandTileCategory.Water: return TerrainType.Ocean;
                case HandTileCategory.Bridge: return TerrainType.Bridge;
                case HandTileCategory.Mountain: return TerrainType.Mountain;
                default: return TerrainType.Plain;
            }
        }

        public static bool ProvidesWalkableSurface(HandTileCategory category)
        {
            switch (category)
            {
                case HandTileCategory.Base:
                case HandTileCategory.Path:
                case HandTileCategory.Forest: // 树干按设计允许穿行
                case HandTileCategory.Plant:
                case HandTileCategory.Ramp:
                case HandTileCategory.Bridge:
                    return true;
                default:
                    return false;
            }
        }

        static int Priority(HandTileCategory category)
        {
            switch (category)
            {
                case HandTileCategory.Building: return 100;
                case HandTileCategory.Bridge: return 80;
                case HandTileCategory.Path: return 70;
                case HandTileCategory.Mountain: return 60;
                case HandTileCategory.Forest: return 50;
                case HandTileCategory.Water: return 40;
                case HandTileCategory.Base: return 10;
                default: return 1;
            }
        }
    }
}
