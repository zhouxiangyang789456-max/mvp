using System;
using System.Collections.Generic;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Pure C# port of aw-map1/mapgen-core.js generateMap(). No MonoBehaviour, no
    /// UnityEngine dependency. Input <see cref="MapGenerationSettings"/>, output
    /// <see cref="GeneratedMapData"/> with aw-map1-style intermediate terrain.
    ///
    /// Pipeline (identical order to the JS source):
    ///   Perlin/fBm height+moisture -> classify -> [mirror] -> smooth ->
    ///   rivers -> beach -> [buildings+roads] -> terrain toggles -> stats.
    /// </summary>
    public static class ProceduralMapGenerator
    {
        public const int GeneratorVersion = 2;

        public static GeneratedMapData Generate(MapGenerationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            int w = Math.Max(8, Math.Min(96, settings.Width));
            int h = Math.Max(8, Math.Min(72, settings.Height));
            uint seed = settings.Seed != 0 ? settings.Seed : 1u;
            if (settings.Mirror && w % 2 != 0) w--;
            bool mirror = settings.Mirror && w % 2 == 0;
            int halfW = w / 2;

            var pnE = new PerlinNoise2D(seed);
            var pnM = new PerlinNoise2D(seed ^ 0x9E3779B9u);
            var rng = new SeededRandom(seed ^ 0x85EBCA6Bu);

            // 1. elevation + moisture height maps.
            var elev = new double[h, w];
            var moist = new double[h, w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    elev[y, x] = Fbm(pnE, x, y, 4, 0.10, 2.0, 0.5);
                    moist[y, x] = Fbm(pnM, x, y, 3, 0.16, 2.0, 0.5);
                }
            }

            // 2. base classification.
            var grid = new GeneratedTerrain[h, w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double e = elev[y, x];
                    if (e < settings.SeaLevel) grid[y, x] = GeneratedTerrain.Ocean;
                    else if (e > settings.MountainLevel) grid[y, x] = GeneratedTerrain.Mountain;
                    else if (moist[y, x] > settings.ForestMoisture) grid[y, x] = GeneratedTerrain.Forest;
                    else grid[y, x] = GeneratedTerrain.Plain;
                }
            }

            // 3. symmetric initial mirror (local rules keep symmetry after this).
            if (mirror) grid = Mirror180(grid, w, h);

            // 4. cellular-automata smoothing.
            grid = SmoothTerrain(grid, w, h, settings.SmoothRounds);

            // 5. rivers (mirror mode carves only the left half, then mirrors).
            if (settings.River && settings.Ocean)
            {
                CarveRivers(grid, elev, w, h, Math.Max(0, settings.Rivers), rng, mirror ? halfW : w);
                if (mirror) grid = Mirror180(grid, w, h);
            }

            // 6. coastal beach.
            if (settings.Beach) grid = AddBeach(grid, w, h);

            // 7. buildings + 8. roads.
            GeneratedBuilding[,] buildings = null;
            if (settings.Buildings)
            {
                buildings = EmptyBuildings(w, h);
                if (mirror)
                {
                    // Mirror counts are per side: place the full requested count on the
                    // left half, then mirror terrain + buildings + roads to the right half.
                    PlaceBuildingsMirror(grid, buildings, w, h, rng, settings);
                    if (settings.Roads)
                        ConnectRoads(grid, buildings, w, h, settings.BridgeSpan);
                    grid = Mirror180(grid, w, h);
                    buildings = Mirror180(buildings, w, h);
                }
                else
                {
                    PlaceBuildingsFree(grid, buildings, w, h, rng, settings);
                    if (settings.Roads)
                        ConnectRoads(grid, buildings, w, h, settings.BridgeSpan);
                }
            }

            // 9. terrain toggles (disable -> replace with plain).
            if (!settings.Ocean) grid = ReplaceType(grid, w, h, GeneratedTerrain.Ocean, GeneratedTerrain.Plain);
            if (!settings.Beach) grid = ReplaceType(grid, w, h, GeneratedTerrain.Beach, GeneratedTerrain.Plain);
            if (!settings.River)
            {
                grid = ReplaceType(grid, w, h, GeneratedTerrain.River, GeneratedTerrain.Plain);
                grid = ReplaceType(grid, w, h, GeneratedTerrain.Bridge, GeneratedTerrain.Plain);
            }
            if (!settings.Forest) grid = ReplaceType(grid, w, h, GeneratedTerrain.Forest, GeneratedTerrain.Plain);
            if (!settings.Mountain) grid = ReplaceType(grid, w, h, GeneratedTerrain.Mountain, GeneratedTerrain.Plain);

            // 10. stats.
            var stats = new int[8];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    stats[(int)grid[y, x]]++;

            var buildingStats = new int[2];
            if (buildings != null)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var b = buildings[y, x];
                        if (b != GeneratedBuilding.None) buildingStats[(int)b]++;
                    }
            }

            var data = new GeneratedMapData
            {
                Width = w,
                Height = h,
                Seed = seed,
                Mirror = mirror,
                GeneratorVersion = GeneratorVersion,
                Terrain = grid,
                Buildings = buildings,
                TerrainStats = stats,
                BuildingStats = buildingStats
            };
            data.BuildingReport = AnalyzeBuildingPlacement(data, settings);
            data.MapHash = GeneratedMapData.ComputeHash(data);
            return data;
        }

        // ---- fBm -------------------------------------------------------------

        static double Fbm(PerlinNoise2D noise, double x, double y,
            int octaves, double freq, double lac, double gain)
        {
            double amp = 1, f = freq, sum = 0, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * noise.Noise2(x * f, y * f);
                norm += amp;
                amp *= gain;
                f *= lac;
            }
            return sum / norm;
        }

        // ---- cellular automata smoothing -------------------------------------

        static GeneratedTerrain[,] SmoothTerrain(GeneratedTerrain[,] grid, int w, int h, int rounds)
        {
            var g = grid;
            for (int r = 0; r < rounds; r++)
            {
                var g2 = (GeneratedTerrain[,])g.Clone();
                for (int y = 1; y < h - 1; y++)
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        var cur = g[y, x];
                        if (cur == GeneratedTerrain.River || cur == GeneratedTerrain.Beach ||
                            cur == GeneratedTerrain.Road || cur == GeneratedTerrain.Bridge)
                            continue;

                        int ocean = 0, land = 0, mt = 0, fs = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                var n = g[y + dy, x + dx];
                                if (n == GeneratedTerrain.Ocean) ocean++; else land++;
                                if (n == GeneratedTerrain.Mountain) mt++;
                                if (n == GeneratedTerrain.Forest) fs++;
                            }
                        }

                        // land/sea binary smoothing.
                        if (cur == GeneratedTerrain.Ocean)
                        {
                            if (land >= 6) g2[y, x] = GeneratedTerrain.Plain;
                        }
                        else if (cur == GeneratedTerrain.Plain || cur == GeneratedTerrain.Forest ||
                                 cur == GeneratedTerrain.Mountain)
                        {
                            if (ocean >= 6) g2[y, x] = GeneratedTerrain.Ocean;
                        }

                        // terrain clustering: drop isolated mountain/forest singles.
                        if (g2[y, x] == GeneratedTerrain.Mountain && mt < 2) g2[y, x] = GeneratedTerrain.Plain;
                        else if (g2[y, x] == GeneratedTerrain.Plain && mt >= 5) g2[y, x] = GeneratedTerrain.Mountain;

                        if (g2[y, x] == GeneratedTerrain.Forest && fs < 2) g2[y, x] = GeneratedTerrain.Plain;
                        else if (g2[y, x] == GeneratedTerrain.Plain && fs >= 5) g2[y, x] = GeneratedTerrain.Forest;
                    }
                }
                g = g2;
            }
            return g;
        }

        // ---- rivers ----------------------------------------------------------

        static readonly int[,] Dir8 =
        {
            { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 },
            { 1, -1 }, { 1, 1 }, { -1, 1 }, { -1, -1 }
        };

        static int CarveRivers(GeneratedTerrain[,] grid, double[,] elev, int w, int h,
            int count, SeededRandom rng, int maxX)
        {
            if (maxX == 0) maxX = w;
            int carved = 0, attempts = 0;
            int maxAttempts = Math.Max(20, count * 30);

            while (carved < count && attempts < maxAttempts)
            {
                attempts++;

                int sx = -1, sy = -1, guard = 0;
                while (guard < 60)
                {
                    guard++;
                    int rx = rng.NextInt(maxX);
                    int ry = rng.NextInt(h);
                    var t0 = grid[ry, rx];
                    if ((t0 == GeneratedTerrain.Plain || t0 == GeneratedTerrain.Forest ||
                         t0 == GeneratedTerrain.Mountain) && elev[ry, rx] > 0.52)
                    {
                        sx = rx;
                        sy = ry;
                        break;
                    }
                }
                if (sx < 0) continue;

                int px = sx, py = sy, prev = -1, steps = 0;
                var path = new List<(int x, int y)>();
                bool reachedSea = false;
                while (steps < 500)
                {
                    if (grid[py, px] == GeneratedTerrain.Ocean) { reachedSea = true; break; }
                    path.Add((px, py));

                    int best = -1;
                    double bestE = double.PositiveInfinity;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = px + Dir8[d, 0];
                        int ny = py + Dir8[d, 1];
                        if (nx < 0 || ny < 0 || nx >= maxX || ny >= h) continue;
                        if (ny * w + nx == prev) continue;
                        double sc = elev[ny, nx] + rng.NextFloat() * 0.05;
                        if (sc < bestE) { bestE = sc; best = d; }
                    }
                    if (best < 0) break;
                    prev = py * w + px;
                    px += Dir8[best, 0];
                    py += Dir8[best, 1];
                    steps++;
                }

                if (reachedSea && path.Count >= 4)
                {
                    (int x, int y) last = (path[0].x, path[0].y);
                    if (grid[last.y, last.x] != GeneratedTerrain.Ocean)
                        grid[last.y, last.x] = GeneratedTerrain.River;
                    for (int i = 1; i < path.Count; i++)
                        last = CarveStep(grid, elev, last.x, last.y, path[i].x, path[i].y);
                    carved++;
                }
            }
            return carved;
        }

        /// <summary>
        /// Carves one step of river from (x0,y0) to (x1,y1) while guaranteeing
        /// 4-connectivity: a diagonal step inserts the lower (non-ocean) L-corner.
        /// </summary>
        static (int x, int y) CarveStep(GeneratedTerrain[,] grid, double[,] elev,
            int x0, int y0, int x1, int y1)
        {
            if (x0 == x1 || y0 == y1)
            {
                if (grid[y1, x1] != GeneratedTerrain.Ocean) grid[y1, x1] = GeneratedTerrain.River;
                return (x1, y1);
            }

            var c1 = (x: x0, y: y1);
            var c2 = (x: x1, y: y0);
            var t1 = grid[c1.y, c1.x];
            var t2 = grid[c2.y, c2.x];
            double e1 = elev[c1.y, c1.x];
            double e2 = elev[c2.y, c2.x];
            bool ok1 = t1 != GeneratedTerrain.Ocean;
            bool ok2 = t2 != GeneratedTerrain.Ocean;

            (int x, int y) corner;
            if (ok1 && (!ok2 || e1 <= e2)) corner = c1;
            else if (ok2) corner = c2;
            else
            {
                // both corners are ocean: river just empties into the sea here.
                if (grid[y1, x1] != GeneratedTerrain.Ocean) grid[y1, x1] = GeneratedTerrain.River;
                return (x1, y1);
            }

            if (grid[corner.y, corner.x] != GeneratedTerrain.Ocean)
                grid[corner.y, corner.x] = GeneratedTerrain.River;
            if (grid[y1, x1] != GeneratedTerrain.Ocean) grid[y1, x1] = GeneratedTerrain.River;
            return (x1, y1);
        }

        // ---- beach -----------------------------------------------------------

        static GeneratedTerrain[,] AddBeach(GeneratedTerrain[,] grid, int w, int h)
        {
            var result = (GeneratedTerrain[,])grid.Clone();

            bool IsSea(int x, int y)
            {
                return x >= 0 && y >= 0 && x < w && y < h && grid[y, x] == GeneratedTerrain.Ocean;
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var t = grid[y, x];
                    if (t == GeneratedTerrain.Plain || t == GeneratedTerrain.Forest ||
                        t == GeneratedTerrain.Mountain)
                    {
                        if (IsSea(x - 1, y) || IsSea(x + 1, y) || IsSea(x, y - 1) || IsSea(x, y + 1))
                            result[y, x] = GeneratedTerrain.Beach;
                    }
                }
            }
            return result;
        }

        // ---- 180-degree mirror ----------------------------------------------

        /// <summary>Copies the right half from the rotated left half (aw-map1 mirror180).</summary>
        static T[,] Mirror180<T>(T[,] g, int w, int h)
        {
            var result = (T[,])g.Clone();
            int startX = (w + 1) / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = startX; x < w; x++)
                    result[y, x] = g[h - 1 - y, w - 1 - x];
            }
            return result;
        }

        // ---- buildings -------------------------------------------------------

        static GeneratedBuilding[,] EmptyBuildings(int w, int h)
        {
            var b = new GeneratedBuilding[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    b[y, x] = GeneratedBuilding.None;
            return b;
        }

        /// <summary>
        /// Buildings may only sit on plain terrain. Not beach, not road, not forest,
        /// not any water/mountain type (建筑平原约束). The runtime registry enforces the
        /// same rule as a final defense.
        /// </summary>
        static bool IsBuildable(GeneratedTerrain t)
        {
            return t == GeneratedTerrain.Plain;
        }

        static int Manhattan((int x, int y) a, (int x, int y) b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        static (int x, int y)? TryFindSpot(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            int w, int h, SeededRandom rng, Func<int, int, bool> cond, int tries = 400)
        {
            for (int i = 0; i < tries; i++)
            {
                int x = rng.NextInt(w);
                int y = rng.NextInt(h);
                if (bld[y, x] != GeneratedBuilding.None) continue;
                if (cond(x, y)) return (x, y);
            }
            return null;
        }

        /// <summary>True when the cell is inside the 1-cell map margin so roads can reach it.</summary>
        static bool InMapMargin(int x, int y, int w, int h)
        {
            return x >= 1 && y >= 1 && x < w - 1 && y < h - 1;
        }

        /// <summary>Free-mode buildings: armories + houses scattered with min spacing.</summary>
        static void PlaceBuildingsFree(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            int w, int h, SeededRandom rng, MapGenerationSettings opts)
        {
            var spots = new List<(int x, int y)>();

            bool SpacingOk((int x, int y) p, int minSpacing)
            {
                for (int i = 0; i < spots.Count; i++)
                    if (Manhattan(spots[i], p) < minSpacing) return false;
                return true;
            }

            // Armories are strategic: fewer, more spread out.
            for (int i = 0; i < opts.ArmoryCount; i++)
            {
                var f = TryFindSpot(grid, bld, w, h, rng, (x, y) =>
                    InMapMargin(x, y, w, h) && IsBuildable(grid[y, x]) && SpacingOk((x, y), 3));
                if (f != null) { bld[f.Value.y, f.Value.x] = GeneratedBuilding.Armory; spots.Add(f.Value); }
            }
            // Houses are numerous: tighter spacing, placed after armories.
            for (int j = 0; j < opts.HouseCount; j++)
            {
                var c = TryFindSpot(grid, bld, w, h, rng, (x, y) =>
                    InMapMargin(x, y, w, h) && IsBuildable(grid[y, x]) && SpacingOk((x, y), 2));
                if (c != null) { bld[c.Value.y, c.Value.x] = GeneratedBuilding.House; spots.Add(c.Value); }
            }
        }

        /// <summary>
        /// Mirror-mode buildings, placed on the left half only (the caller mirrors).
        /// Counts are per side, so both halves get identical armory/house layouts.
        /// </summary>
        static void PlaceBuildingsMirror(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            int w, int h, SeededRandom rng, MapGenerationSettings opts)
        {
            int halfW = w / 2;
            bool InLeft((int x, int y) p) => p.x < halfW;
            bool InLeftMargin(int x, int y) => x >= 1 && y >= 1 && x < halfW - 1 && y < h - 1;

            var spots = new List<(int x, int y)>();

            bool SpacingOk((int x, int y) p, int minSpacing)
            {
                for (int i = 0; i < spots.Count; i++)
                    if (Manhattan(spots[i], p) < minSpacing) return false;
                return true;
            }

            for (int i = 0; i < opts.ArmoryCount; i++)
            {
                var f = TryFindSpot(grid, bld, w, h, rng, (x, y) =>
                {
                    var p = (x, y);
                    return InLeft(p) && InLeftMargin(x, y) && IsBuildable(grid[y, x]) && SpacingOk(p, 3);
                }, 800);
                if (f != null) { bld[f.Value.y, f.Value.x] = GeneratedBuilding.Armory; spots.Add(f.Value); }
            }
            for (int j = 0; j < opts.HouseCount; j++)
            {
                var c = TryFindSpot(grid, bld, w, h, rng, (x, y) =>
                {
                    var p = (x, y);
                    return InLeft(p) && InLeftMargin(x, y) && IsBuildable(grid[y, x]) && SpacingOk(p, 2);
                }, 800);
                if (c != null) { bld[c.Value.y, c.Value.x] = GeneratedBuilding.House; spots.Add(c.Value); }
            }
        }

        // ---- roads & bridges -------------------------------------------------

        static List<(int x, int y)> LineCells(int x0, int y0, int x1, int y1)
        {
            var cells = new List<(int x, int y)>();
            if (x0 == x1)
            {
                int step = y1 > y0 ? 1 : -1;
                for (int y = y0 + step; y != y1 + step; y += step) cells.Add((x0, y));
            }
            else if (y0 == y1)
            {
                int step = x1 > x0 ? 1 : -1;
                for (int x = x0 + step; x != x1 + step; x += step) cells.Add((x, y0));
            }
            return cells;
        }

        /// <summary>
        /// A road segment is feasible when no cell is a building cell (roads may only
        /// connect to a building's outer adjacent cells, never cover the building itself)
        /// and every non-pavable cell is a bridgeable river span.
        /// </summary>
        static bool SegmentFeasible(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            List<(int x, int y)> cells, int maxBridgeSpan)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i].x, y = cells[i].y;
                if (bld[y, x] != GeneratedBuilding.None) return false; // never pave a building cell
                var t = grid[y, x];
                if (t == GeneratedTerrain.Plain || t == GeneratedTerrain.Beach ||
                    t == GeneratedTerrain.Road || t == GeneratedTerrain.Bridge) continue;
                if (t != GeneratedTerrain.River) return false;

                int span = 0, j = i;
                while (j < cells.Count && grid[cells[j].y, cells[j].x] == GeneratedTerrain.River) { span++; j++; }
                if (span > maxBridgeSpan) return false;
                i = j - 1;
            }
            return true;
        }

        static void SegmentCommit(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            List<(int x, int y)> cells)
        {
            for (int k = 0; k < cells.Count; k++)
            {
                int gx = cells[k].x, gy = cells[k].y;
                if (bld[gy, gx] != GeneratedBuilding.None) continue; // never pave a building cell
                if (grid[gy, gx] == GeneratedTerrain.River) grid[gy, gx] = GeneratedTerrain.Bridge;
                else if (grid[gy, gx] != GeneratedTerrain.Bridge) grid[gy, gx] = GeneratedTerrain.Road;
            }
        }

        static bool PaveSegment(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            List<(int x, int y)> cells, int maxBridgeSpan)
        {
            if (!SegmentFeasible(grid, bld, cells, maxBridgeSpan)) return false;
            SegmentCommit(grid, bld, cells);
            return true;
        }

        /// <summary>Paves a horizontal-then-vertical (or vertical-then-horizontal) road.</summary>
        static bool TryRoad(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            int x0, int y0, int x1, int y1, int maxBridgeSpan)
        {
            if (x0 == x1 || y0 == y1)
                return PaveSegment(grid, bld, LineCells(x0, y0, x1, y1), maxBridgeSpan);

            var c1h = LineCells(x0, y0, x1, y0);
            var c1v = LineCells(x1, y0, x1, y1);
            if (SegmentFeasible(grid, bld, c1h, maxBridgeSpan) && SegmentFeasible(grid, bld, c1v, maxBridgeSpan))
            {
                SegmentCommit(grid, bld, c1h);
                SegmentCommit(grid, bld, c1v);
                return true;
            }

            var c2v = LineCells(x0, y0, x0, y1);
            var c2h = LineCells(x0, y1, x1, y1);
            if (SegmentFeasible(grid, bld, c2v, maxBridgeSpan) && SegmentFeasible(grid, bld, c2h, maxBridgeSpan))
            {
                SegmentCommit(grid, bld, c2v);
                SegmentCommit(grid, bld, c2h);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Connects every building to the first placed building via roads. Roads terminate
        /// at a cardinal neighbor of each target building (never on the building cell itself)
        /// and are skipped when no feasible path exists.
        /// </summary>
        static void ConnectRoads(GeneratedTerrain[,] grid, GeneratedBuilding[,] bld,
            int w, int h, int maxBridgeSpan)
        {
            int ax = -1, ay = -1;
            for (int y = 0; y < h && ax < 0; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (bld[y, x] != GeneratedBuilding.None) { ax = x; ay = y; break; }
                }
            }
            if (ax < 0) return; // no buildings, nothing to connect

            // Cardinal neighbors of the target building: the road ends on one of these.
            int[,] dirs = { { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 } };

            for (int y2 = 0; y2 < h; y2++)
            {
                for (int x2 = 0; x2 < w; x2++)
                {
                    if (bld[y2, x2] == GeneratedBuilding.None || (x2 == ax && y2 == ay)) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x2 + dirs[d, 0];
                        int ny = y2 + dirs[d, 1];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (bld[ny, nx] != GeneratedBuilding.None) continue; // another building's cell
                        if (nx == ax && ny == ay) continue;                  // the anchor itself
                        if (TryRoad(grid, bld, ax, ay, nx, ny, maxBridgeSpan)) break;
                    }
                }
            }
        }

        // ---- terrain toggle --------------------------------------------------

        static GeneratedTerrain[,] ReplaceType(GeneratedTerrain[,] grid, int w, int h,
            GeneratedTerrain from, GeneratedTerrain to)
        {
            var result = (GeneratedTerrain[,])grid.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (result[y, x] == from) result[y, x] = to;
            return result;
        }

        // ---- building placement report ---------------------------------------

        /// <summary>
        /// Measures requested/placed counts and any illegal placement (non-plain,
        /// out-of-bounds, overlapping) against the final terrain + buildings grids.
        /// The new generator only places on Plain, so NonPlainCells should be zero.
        /// </summary>
        static BuildingPlacementReport AnalyzeBuildingPlacement(GeneratedMapData data,
            MapGenerationSettings opts)
        {
            var report = new BuildingPlacementReport
            {
                RequestedHouse = Math.Max(0, opts.HouseCount),
                RequestedArmory = Math.Max(0, opts.ArmoryCount)
            };
            if (data.Buildings == null) return report;

            int w = data.Width;
            int h = data.Height;
            var seen = new HashSet<int>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var b = data.Buildings[y, x];
                    if (b == GeneratedBuilding.None) continue;
                    if (b == GeneratedBuilding.House) report.PlacedHouse++;
                    else if (b == GeneratedBuilding.Armory) report.PlacedArmory++;

                    if (x < 0 || y < 0 || x >= w || y >= h) { report.OutOfBoundsCells++; continue; }
                    if (!seen.Add(y * w + x)) { report.OverlapCells++; continue; }
                    if (data.Terrain[y, x] != GeneratedTerrain.Plain) report.NonPlainCells++;
                }
            }
            return report;
        }
    }
}
