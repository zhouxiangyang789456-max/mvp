using System;
using System.Collections.Generic;

namespace Mvp.Battle.Map.Generation
{
    public sealed class MapValidationResult
    {
        public bool Passed;
        public readonly List<string> Failures = new List<string>();

        public override string ToString()
        {
            return Passed ? "OK" : string.Join("; ", Failures);
        }
    }

    /// <summary>
    /// Validates a generated battle map before it is handed to the battle scene.
    /// Pure C#: walkability is supplied as a predicate so tests and tools can pass
    /// the same rule the runtime uses (TerrainCatalog.IsWalkable) while staying
    /// independent of UnityEngine.
    ///
    /// Connectivity uses 8-direction flood fill WITHOUT corner-cutting, matching the
    /// current PathfindingService behaviour (diagonals may pass between two blocked
    /// cells). Keep this in sync with the runtime pathfinding neighbour rules.
    /// </summary>
    public static class MapGenerationValidator
    {
        /// <summary>Default walkability rule mirroring TerrainCatalog.IsWalkable.</summary>
        public static bool IsWalkableDefault(TerrainType t)
        {
            return t != TerrainType.Ocean && t != TerrainType.SnowMountain;
        }

        public static MapValidationResult Validate(
            TerrainType[,] terrain,
            int width,
            int height,
            bool requireMirror,
            float minWalkableRatio,
            float maxWalkableRatio,
            float minWalkableComponentRatio,
            Func<TerrainType, bool> isWalkable = null)
        {
            isWalkable = isWalkable ?? IsWalkableDefault;
            var result = new MapValidationResult();

            if (width < 8 || width > 96 || height < 8 || height > 72)
                result.Failures.Add("尺寸越界: " + width + "x" + height);

            int total = width * height;
            int walkable = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (isWalkable(terrain[y, x])) walkable++;

            float ratio = total > 0 ? (float)walkable / total : 0f;
            if (ratio < minWalkableRatio || ratio > maxWalkableRatio)
                result.Failures.Add("可通行比例 " + ratio.ToString("0.00") +
                    " 超出 [" + minWalkableRatio + ", " + maxWalkableRatio + "]");

            if (requireMirror)
            {
                bool symmetric = true;
                for (int y = 0; y < height && symmetric; y++)
                    for (int x = 0; x < width && symmetric; x++)
                        if (terrain[y, x] != terrain[height - 1 - y, width - 1 - x])
                            symmetric = false;
                if (!symmetric) result.Failures.Add("镜像对称校验失败");
            }

            int largest = LargestWalkableComponent(terrain, width, height, isWalkable);
            if (walkable > 0)
            {
                float compRatio = (float)largest / walkable;
                if (compRatio < minWalkableComponentRatio)
                    result.Failures.Add("最大可通行连通分量占比 " + compRatio.ToString("0.00") +
                        " 低于 " + minWalkableComponentRatio);
            }

            result.Passed = result.Failures.Count == 0;
            return result;
        }

        static int LargestWalkableComponent(TerrainType[,] terrain, int w, int h,
            Func<TerrainType, bool> isWalkable)
        {
            var visited = new bool[h, w];
            int best = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (visited[y, x] || !isWalkable(terrain[y, x])) continue;

                    int size = 0;
                    var stack = new Stack<(int x, int y)>();
                    stack.Push((x, y));
                    visited[y, x] = true;

                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        size++;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = cx + dx, ny = cy + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                if (visited[ny, nx] || !isWalkable(terrain[ny, nx])) continue;
                                visited[ny, nx] = true;
                                stack.Push((nx, ny));
                            }
                        }
                    }
                    if (size > best) best = size;
                }
            }
            return best;
        }
    }
}
