using System;
using System.Collections.Generic;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>A contiguous 3x3 deployment block owned by one commander group.</summary>
    public sealed class DeploymentZone
    {
        public GridCoord Anchor;
        public readonly List<GridCoord> Cells = new List<GridCoord>(9);
    }

    public sealed class DeploymentPlan
    {
        public readonly List<DeploymentZone> PlayerZones = new List<DeploymentZone>();
        public readonly List<DeploymentZone> EnemyZones = new List<DeploymentZone>();
        public string FailureReason;
        public bool Passed { get { return string.IsNullOrEmpty(FailureReason); } }
    }

    /// <summary>
    /// Finds non-overlapping contiguous 3x3 blocks in the largest walkable component.
    /// Players prefer the upper map edge and enemies the lower edge, matching the
    /// battle scene's current orientation. Selection is deterministic.
    /// </summary>
    public static class DeploymentAreaPlanner
    {
        static readonly int[] SlotOrder = { 4, 3, 5, 1, 7, 0, 2, 6, 8 };

        public static DeploymentPlan Plan(TerrainType[,] terrain, int playerGroups,
            int enemyGroups, Func<TerrainType, bool> isWalkable = null)
        {
            var result = new DeploymentPlan();
            if (terrain == null)
            {
                result.FailureReason = "地图数据为空";
                return result;
            }

            int height = terrain.GetLength(0);
            int width = terrain.GetLength(1);
            if (width < 3 || height < 3)
            {
                result.FailureReason = "地图不足以容纳 3x3 部署区";
                return result;
            }

            isWalkable = isWalkable ?? MapGenerationValidator.IsWalkableDefault;
            bool[,] mainComponent = FindLargestComponent(terrain, isWalkable);
            var reserved = new bool[height, width];

            if (!AllocateSide(terrain, mainComponent, reserved, Math.Max(0, playerGroups),
                true, isWalkable, result.PlayerZones, out var playerFailure))
            {
                result.FailureReason = playerFailure;
                return result;
            }

            if (!AllocateSide(terrain, mainComponent, reserved, Math.Max(0, enemyGroups),
                false, isWalkable, result.EnemyZones, out var enemyFailure))
            {
                result.FailureReason = enemyFailure;
                return result;
            }

            return result;
        }

        public static int GetFormationSlotIndex(int spawnOrder)
        {
            return spawnOrder >= 0 && spawnOrder < SlotOrder.Length
                ? SlotOrder[spawnOrder]
                : -1;
        }

        static bool AllocateSide(TerrainType[,] terrain, bool[,] mainComponent,
            bool[,] reserved, int groupCount, bool playerSide,
            Func<TerrainType, bool> isWalkable, List<DeploymentZone> output,
            out string failure)
        {
            failure = null;
            int height = terrain.GetLength(0);
            int width = terrain.GetLength(1);
            for (int group = 0; group < groupCount; group++)
            {
                int targetX = (group + 1) * (width - 1) / (groupCount + 1);
                int targetY = playerSide ? height - 2 : 1;
                DeploymentZone best = null;
                int bestScore = int.MaxValue;

                for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    if (!CanReserveBlock(terrain, mainComponent, reserved, x, y, isWalkable))
                        continue;

                    int sidePenalty = playerSide
                        ? Math.Max(0, height / 2 - y) * height
                        : Math.Max(0, y - (height - 1) / 2) * height;
                    int score = sidePenalty + Math.Abs(x - targetX) * 3 + Math.Abs(y - targetY) * 5;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = CreateZone(x, y);
                }

                if (best == null)
                {
                    failure = (playerSide ? "玩家" : "敌方") + "第 " + (group + 1) +
                        " 支编队找不到连续 3x3 部署区";
                    return false;
                }

                output.Add(best);
                Reserve(best, reserved);
            }
            return true;
        }

        static bool CanReserveBlock(TerrainType[,] terrain, bool[,] mainComponent,
            bool[,] reserved, int anchorX, int anchorY, Func<TerrainType, bool> isWalkable)
        {
            int height = terrain.GetLength(0);
            int width = terrain.GetLength(1);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = anchorX + dx;
                int y = anchorY + dy;
                if (x < 0 || y < 0 || x >= width || y >= height ||
                    !isWalkable(terrain[y, x]) || !mainComponent[y, x] || reserved[y, x])
                    return false;
            }
            return true;
        }

        static DeploymentZone CreateZone(int anchorX, int anchorY)
        {
            var zone = new DeploymentZone { Anchor = new GridCoord(anchorX, anchorY) };
            for (int slot = 0; slot < 9; slot++)
                zone.Cells.Add(new GridCoord(anchorX + slot % 3 - 1, anchorY + slot / 3 - 1));
            return zone;
        }

        static void Reserve(DeploymentZone zone, bool[,] reserved)
        {
            for (int i = 0; i < zone.Cells.Count; i++)
                reserved[zone.Cells[i].Y, zone.Cells[i].X] = true;
        }

        static bool[,] FindLargestComponent(TerrainType[,] terrain,
            Func<TerrainType, bool> isWalkable)
        {
            int height = terrain.GetLength(0);
            int width = terrain.GetLength(1);
            var visited = new bool[height, width];
            var best = new List<GridCoord>();
            var queue = new Queue<GridCoord>();

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (visited[y, x] || !isWalkable(terrain[y, x])) continue;
                var current = new List<GridCoord>();
                visited[y, x] = true;
                queue.Enqueue(new GridCoord(x, y));
                while (queue.Count > 0)
                {
                    GridCoord cell = queue.Dequeue();
                    current.Add(cell);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cell.X + dx;
                        int ny = cell.Y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height ||
                            visited[ny, nx] || !isWalkable(terrain[ny, nx])) continue;
                        visited[ny, nx] = true;
                        queue.Enqueue(new GridCoord(nx, ny));
                    }
                }
                if (current.Count > best.Count) best = current;
            }

            var mask = new bool[height, width];
            for (int i = 0; i < best.Count; i++) mask[best[i].Y, best[i].X] = true;
            return mask;
        }
    }
}
