using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.Vision
{
    /// <summary>Grid-bucket index for living combat units.</summary>
    public sealed class BattleSpatialIndex : MonoBehaviour
    {
        public static BattleSpatialIndex Instance { get; private set; }

        readonly Dictionary<Vector2Int, List<UnitView>> _buckets =
            new Dictionary<Vector2Int, List<UnitView>>();
        readonly Dictionary<UnitView, Vector2Int> _unitCells =
            new Dictionary<UnitView, Vector2Int>();

        public int UnitCount => _unitCells.Count;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            Clear();
            if (Instance == this) Instance = null;
        }

        public void Register(UnitView unit)
        {
            if (!IsIndexable(unit)) return;
            Vector2Int existing;
            if (_unitCells.TryGetValue(unit, out existing))
            {
                if (existing != unit.Data.GridPosition) Move(unit, existing, unit.Data.GridPosition);
                return;
            }
            AddToBucket(unit, unit.Data.GridPosition);
        }

        public void Move(UnitView unit, Vector2Int oldCell, Vector2Int newCell)
        {
            if (unit == null || oldCell == newCell) return;
            Vector2Int indexedCell;
            if (!_unitCells.TryGetValue(unit, out indexedCell))
            {
                Register(unit);
                return;
            }
            RemoveFromBucket(unit, indexedCell);
            if (IsIndexable(unit)) AddToBucket(unit, newCell);
        }

        public void Unregister(UnitView unit)
        {
            if (unit == null) return;
            Vector2Int cell;
            if (!_unitCells.TryGetValue(unit, out cell)) return;
            RemoveFromBucket(unit, cell);
        }

        public void QueryEnemies(Vector2Int center, int radius, TeamId observerTeam,
            List<UnitView> output)
        {
            if (output == null) return;
            int radiusSq = radius * radius;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy > radiusSq) continue;
                List<UnitView> bucket;
                if (!_buckets.TryGetValue(new Vector2Int(x, y), out bucket)) continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var unit = bucket[i];
                    if (!IsIndexable(unit) || unit.Data.Team == observerTeam) continue;
                    output.Add(unit);
                }
            }
        }

        /// <summary>
        /// Queries opposing units within a Chebyshev radius (used by concealment
        /// discovery and guard auto-attack, whose range metric is the attack grid
        /// distance rather than a Euclidean circle).
        /// </summary>
        public void QueryEnemiesChebyshev(Vector2Int center, int radius, TeamId observerTeam,
            List<UnitView> output)
        {
            if (output == null) return;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                int dx = x - center.x;
                int dy = y - center.y;
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > radius) continue;
                List<UnitView> bucket;
                if (!_buckets.TryGetValue(new Vector2Int(x, y), out bucket)) continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var unit = bucket[i];
                    if (!IsIndexable(unit) || unit.Data.Team == observerTeam) continue;
                    output.Add(unit);
                }
            }
        }

        public void Clear()
        {
            _buckets.Clear();
            _unitCells.Clear();
        }

        void AddToBucket(UnitView unit, Vector2Int cell)
        {
            List<UnitView> bucket;
            if (!_buckets.TryGetValue(cell, out bucket))
            {
                bucket = new List<UnitView>(4);
                _buckets[cell] = bucket;
            }
            bucket.Add(unit);
            _unitCells[unit] = cell;
        }

        void RemoveFromBucket(UnitView unit, Vector2Int cell)
        {
            List<UnitView> bucket;
            if (_buckets.TryGetValue(cell, out bucket))
            {
                int index = bucket.IndexOf(unit);
                if (index >= 0)
                {
                    int last = bucket.Count - 1;
                    bucket[index] = bucket[last];
                    bucket.RemoveAt(last);
                }
                if (bucket.Count == 0) _buckets.Remove(cell);
            }
            _unitCells.Remove(unit);
        }

        static bool IsIndexable(UnitView unit)
        {
            return unit != null && unit.Data != null && unit.Data.State != UnitState.Dead;
        }
    }
}
