using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// A* over the logical grid. 8-direction movement with diagonal cost.
    /// All working arrays are pre-allocated once per grid so FindPath does not
    /// allocate lists/dictionaries per call. Callers pass a reusable output list.
    ///
    /// Start cell may be occupied (the unit itself). End cell may be occupied when
    /// allowOccupiedEnd is true (attack target cell).
    /// </summary>
    public sealed class PathfindingService
    {
        static readonly Vector2Int[] Dir8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };
        static readonly int[] DirCost = { 10, 10, 10, 10, 14, 14, 14, 14 };

        readonly IGridDataProvider _grid;
        readonly int _w;
        readonly int _h;
        readonly int[] _gScore;
        readonly int[] _fScore;
        readonly int[] _cameFrom;
        readonly int[] _visitedGeneration;
        readonly int[] _closedGeneration;
        readonly MinHeap _open;
        int _searchGeneration;

        public PathfindingService(IGridDataProvider grid)
        {
            _grid = grid;
            _w = grid.Width;
            _h = grid.Height;
            int count = _w * _h;
            _gScore = new int[count];
            _fScore = new int[count];
            _cameFrom = new int[count];
            _visitedGeneration = new int[count];
            _closedGeneration = new int[count];
            _open = new MinHeap(Math.Max(16, count * 4));
        }

        /// <summary>
        /// Computes a path from start to end into <paramref name="output"/> (cleared first).
        /// Returns true if a path exists.
        /// </summary>
        public bool FindPath(Vector2Int start, Vector2Int end, List<Vector2Int> output, bool allowOccupiedEnd)
        {
            output.Clear();
            if (!_grid.InBounds(start) || !_grid.InBounds(end)) return false;
            if (!_grid.IsWalkable(start)) return false;
            if (!_grid.IsWalkable(end) && !allowOccupiedEnd) return false;

            int s = Index(start);
            int e = Index(end);
            if (s == e)
            {
                output.Add(start);
                return true;
            }

            int generation = NextSearchGeneration();
            _open.Clear();

            _visitedGeneration[s] = generation;
            _gScore[s] = 0;
            _fScore[s] = Heuristic(s, e);
            _open.Push(s, _fScore[s]);

            while (_open.Count > 0)
            {
                int cur = _open.Pop();
                if (cur == e)
                {
                    Reconstruct(s, e, output);
                    return true;
                }
                if (_closedGeneration[cur] == generation) continue;
                _closedGeneration[cur] = generation;

                int cx = cur % _w;
                int cy = cur / _w;
                for (int i = 0; i < Dir8.Length; i++)
                {
                    int nx = cx + Dir8[i].x;
                    int ny = cy + Dir8[i].y;
                    if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;

                    int n = ny * _w + nx;
                    if (_closedGeneration[n] == generation) continue;
                    if (n == s) continue;
                    var currentCell = new Vector2Int(cx, cy);
                    var nextCell = new Vector2Int(nx, ny);
                    if (!_grid.CanTraverse(currentCell, nextCell)) continue;
                    if (n != e)
                    {
                        if (!_grid.IsWalkable(nextCell)) continue;
                        if (_grid.IsOccupied(nextCell)) continue;
                    }

                    // Do not cut diagonally through the corners of cliffs/obstacles.
                    if (Dir8[i].x != 0 && Dir8[i].y != 0 &&
                        (!_grid.CanTraverse(currentCell, new Vector2Int(nx, cy)) ||
                         !_grid.CanTraverse(currentCell, new Vector2Int(cx, ny)))) continue;

                    int ng = _gScore[cur] + DirCost[i];
                    if (_visitedGeneration[n] != generation || ng < _gScore[n])
                    {
                        _visitedGeneration[n] = generation;
                        _gScore[n] = ng;
                        _cameFrom[n] = cur;
                        _fScore[n] = ng + Heuristic(n, e);
                        _open.Push(n, _fScore[n]);
                    }
                }
            }

            return false;
        }

        int NextSearchGeneration()
        {
            if (_searchGeneration == int.MaxValue)
            {
                Array.Clear(_visitedGeneration, 0, _visitedGeneration.Length);
                Array.Clear(_closedGeneration, 0, _closedGeneration.Length);
                _searchGeneration = 0;
            }
            return ++_searchGeneration;
        }

        int Index(Vector2Int c) => c.y * _w + c.x;

        int Heuristic(int a, int b)
        {
            int ax = a % _w;
            int ay = a / _w;
            int bx = b % _w;
            int by = b / _w;
            int dx = Math.Abs(ax - bx);
            int dy = Math.Abs(ay - by);
            return 10 * (dx + dy) + 4 * Math.Min(dx, dy);
        }

        void Reconstruct(int s, int e, List<Vector2Int> output)
        {
            int cur = e;
            while (cur != s)
            {
                output.Add(new Vector2Int(cur % _w, cur / _w));
                cur = _cameFrom[cur];
            }
            output.Add(new Vector2Int(s % _w, s / _w));
            output.Reverse();
        }

        sealed class MinHeap
        {
            readonly int[] _indices;
            readonly int[] _keys;
            int _count;

            public MinHeap(int capacity)
            {
                _indices = new int[capacity];
                _keys = new int[capacity];
            }

            public int Count => _count;

            public void Clear() => _count = 0;

            public void Push(int idx, int key)
            {
                int i = _count++;
                _indices[i] = idx;
                _keys[i] = key;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (_keys[p] <= _keys[i]) break;
                    Swap(p, i);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = _indices[0];
                _count--;
                if (_count > 0)
                {
                    _indices[0] = _indices[_count];
                    _keys[0] = _keys[_count];
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1;
                        int r = l + 1;
                        int smallest = i;
                        if (l < _count && _keys[l] < _keys[smallest]) smallest = l;
                        if (r < _count && _keys[r] < _keys[smallest]) smallest = r;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = _indices[a]; _indices[a] = _indices[b]; _indices[b] = ti;
                int tk = _keys[a]; _keys[a] = _keys[b]; _keys[b] = tk;
            }
        }
    }
}
