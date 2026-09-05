using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Places the timed-extraction portal during map generation (限时传送门撤离关卡 §4).
    /// Pure C# and fully deterministic: for a fixed map, settings and deployment plan it
    /// always returns the same anchor, so the result is stable under the fixed seed and
    /// can be folded into the map hash and batch-validated.
    ///
    /// Rules:
    ///   - Only runs when settings.EnableExtractionPortal is true.
    ///   - The whole footprint must be walkable (not ocean, not a building cell).
    ///   - Must not overlap player/enemy deployment zones or the 1-cell map margin.
    ///   - Reachable (8-dir flood fill) from the player deployment zones.
    ///   - Shortest-path distance from players within [Min, Max], so the portal is
    ///     neither trivially next to spawn nor beyond the time budget.
    ///   - Deterministic pick: closest to the distance-band midpoint, then map centre,
    ///     then top-left order.
    /// </summary>
    public static class PortalPlacementPlanner
    {
        public static PortalSpawnData Plan(GeneratedMapData data,
            DeploymentPlan deployment, MapGenerationSettings settings, out string failure)
        {
            failure = null;
            if (data == null)
            {
                failure = "地图数据为空";
                return null;
            }
            if (settings == null || !settings.EnableExtractionPortal)
            {
                failure = "未启用撤离传送门";
                return null;
            }
            if (deployment == null || deployment.PlayerZones.Count == 0)
            {
                failure = "缺少玩家部署区";
                return null;
            }

            int w = data.Width;
            int h = data.Height;
            int zw = Clamp(settings.ExtractionZoneWidth, 1, 4);
            int zh = Clamp(settings.ExtractionZoneHeight, 1, 4);
            if (zw > w - 2 || zh > h - 2)
            {
                failure = "撤离区尺寸超出地图";
                return null;
            }

            // Walkable = not ocean and not a building cell.
            var walkable = new bool[h, w];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool building = data.Buildings != null && data.Buildings[y, x] != GeneratedBuilding.None;
                walkable[y, x] = data.Terrain[y, x] != GeneratedTerrain.Ocean && !building;
            }

            // Reserved = any deployment cell (player or enemy).
            var reserved = new bool[h, w];
            MarkZones(reserved, deployment.PlayerZones);
            MarkZones(reserved, deployment.EnemyZones);

            // Multi-source BFS distance from all player deployment cells. Because
            // DeploymentAreaPlanner allocates every zone inside the single largest
            // walkable component, a reachable footprint (dist >= 0) is reachable from
            // every player zone.
            int[,] dist = BfsDistance(walkable, w, h, deployment.PlayerZones);

            int minDist = Math.Max(0, settings.MinPortalPathDistanceFromPlayer);
            int maxDist = Math.Max(minDist, settings.MaxPortalPathDistanceFromPlayer);

            var candidates = new List<(int x, int y, int d)>();
            // Full footprint must be interior (no edge clipping, 计划 §4): anchor in [1, size-zw-1].
            for (int y = 1; y <= h - zh - 1; y++)
            for (int x = 1; x <= w - zw - 1; x++)
            {
                int d = MinFootprintDistance(dist, x, y, zw, zh);
                if (d < 0 || d < minDist || d > maxDist) continue;
                if (!FootprintClear(walkable, reserved, x, y, zw, zh)) continue;
                candidates.Add((x, y, d));
            }

            if (candidates.Count == 0)
            {
                failure = "找不到满足可达性与距离约束的撤离区";
                return null;
            }

            // Deterministic selection: distance-band midpoint first, then map centre,
            // then top-left order. No RNG so the result is stable across any seed.
            float bandMid = (minDist + maxDist) * 0.5f;
            int cx = w / 2, cy = h / 2;
            var best = candidates[0];
            long bestScore = long.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                long bandPenalty = (long)Math.Abs((c.d - bandMid) * 1000f);
                long centerPenalty = (Math.Abs(c.x - cx) + Math.Abs(c.y - cy)) * 10L;
                long order = (long)c.y * w + c.x;
                long score = bandPenalty + centerPenalty;
                if (score < bestScore || (score == bestScore && order < (long)best.y * w + best.x))
                {
                    bestScore = score;
                    best = c;
                }
            }

            return new PortalSpawnData
            {
                AnchorCell = new Vector2Int(best.x, best.y),
                Width = zw,
                Height = zh,
                TimeLimitSeconds = Math.Max(1, settings.ExtractionTimeLimitSeconds),
                OpeningDelaySeconds = Math.Max(0f, settings.PortalOpeningDelaySeconds)
            };
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        static void MarkZones(bool[,] reserved, List<DeploymentZone> zones)
        {
            int h = reserved.GetLength(0);
            int w = reserved.GetLength(1);
            for (int i = 0; i < zones.Count; i++)
            for (int j = 0; j < zones[i].Cells.Count; j++)
            {
                var c = zones[i].Cells[j];
                if (c.X >= 0 && c.Y >= 0 && c.X < w && c.Y < h) reserved[c.Y, c.X] = true;
            }
        }

        static int[,] BfsDistance(bool[,] walkable, int w, int h, List<DeploymentZone> playerZones)
        {
            var dist = new int[h, w];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) dist[y, x] = -1;

            var queue = new Queue<(int x, int y)>();
            for (int i = 0; i < playerZones.Count; i++)
            for (int j = 0; j < playerZones[i].Cells.Count; j++)
            {
                int x = playerZones[i].Cells[j].X;
                int y = playerZones[i].Cells[j].Y;
                if (x < 0 || y < 0 || x >= w || y >= h || !walkable[y, x] || dist[y, x] >= 0) continue;
                dist[y, x] = 0;
                queue.Enqueue((x, y));
            }

            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + dx[d], ny = cy + dy[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || !walkable[ny, nx] || dist[ny, nx] >= 0) continue;
                    dist[ny, nx] = dist[cy, cx] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
            return dist;
        }

        static bool FootprintClear(bool[,] walkable, bool[,] reserved, int x, int y, int zw, int zh)
        {
            int h = walkable.GetLength(0);
            int w = walkable.GetLength(1);
            for (int dy = 0; dy < zh; dy++)
            for (int dx = 0; dx < zw; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) return false;
                if (!walkable[yy, xx] || reserved[yy, xx]) return false;
            }
            return true;
        }

        /// <summary>Shortest distance over reachable footprint cells; -1 when none is reachable.</summary>
        static int MinFootprintDistance(int[,] dist, int x, int y, int zw, int zh)
        {
            int best = int.MaxValue;
            for (int dy = 0; dy < zh; dy++)
            for (int dx = 0; dx < zw; dx++)
            {
                int d = dist[y + dy, x + dx];
                if (d >= 0 && d < best) best = d;
            }
            return best == int.MaxValue ? -1 : best;
        }
    }
}
