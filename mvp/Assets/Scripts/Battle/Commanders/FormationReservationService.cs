using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Commanders
{
    /// <summary>
    /// Owns final-cell reservations for commander groups. Candidate reservations are
    /// isolated from committed reservations so a failed replacement command can roll back.
    /// </summary>
    public sealed class FormationReservationService : MonoBehaviour
    {
        public static FormationReservationService Instance { get; private set; }

        readonly Dictionary<Vector2Int, string> _committedByCell =
            new Dictionary<Vector2Int, string>();
        readonly Dictionary<string, HashSet<Vector2Int>> _committedByGroup =
            new Dictionary<string, HashSet<Vector2Int>>();
        readonly Dictionary<Vector2Int, string> _candidateByCell =
            new Dictionary<Vector2Int, string>();
        readonly Dictionary<string, HashSet<Vector2Int>> _candidateByGroup =
            new Dictionary<string, HashSet<Vector2Int>>();

        public int CommittedCellCount => _committedByCell.Count;
        public int CandidateCellCount => _candidateByCell.Count;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            ReleaseAll();
            if (Instance == this) Instance = null;
        }

        public bool TryReserveCandidate(string groupId, IReadOnlyList<Vector2Int> cells)
        {
            if (string.IsNullOrEmpty(groupId) || cells == null || cells.Count == 0) return false;
            Rollback(groupId);

            var candidate = GetOrCreate(_candidateByGroup, groupId);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                if (!candidate.Add(cell) || IsOwnedByOther(_committedByCell, groupId, cell) ||
                    IsOwnedByOther(_candidateByCell, groupId, cell))
                {
                    Rollback(groupId);
                    return false;
                }
            }

            foreach (Vector2Int cell in candidate) _candidateByCell[cell] = groupId;
            return true;
        }

        public bool Commit(string groupId)
        {
            HashSet<Vector2Int> candidate;
            if (!_candidateByGroup.TryGetValue(groupId, out candidate)) return false;

            ReleaseCommitted(groupId);
            var committed = GetOrCreate(_committedByGroup, groupId);
            foreach (Vector2Int cell in candidate)
            {
                committed.Add(cell);
                _committedByCell[cell] = groupId;
                _candidateByCell.Remove(cell);
            }
            _candidateByGroup.Remove(groupId);
            return true;
        }

        public void Rollback(string groupId)
        {
            HashSet<Vector2Int> cells;
            if (!_candidateByGroup.TryGetValue(groupId, out cells)) return;
            foreach (Vector2Int cell in cells)
            {
                string owner;
                if (_candidateByCell.TryGetValue(cell, out owner) && owner == groupId)
                    _candidateByCell.Remove(cell);
            }
            _candidateByGroup.Remove(groupId);
        }

        public void Release(string groupId)
        {
            Rollback(groupId);
            ReleaseCommitted(groupId);
        }

        public bool IsReservedByOther(string groupId, Vector2Int cell)
        {
            return IsOwnedByOther(_committedByCell, groupId, cell) ||
                IsOwnedByOther(_candidateByCell, groupId, cell);
        }

        public void ReleaseAll()
        {
            _committedByCell.Clear();
            _committedByGroup.Clear();
            _candidateByCell.Clear();
            _candidateByGroup.Clear();
        }

        void ReleaseCommitted(string groupId)
        {
            HashSet<Vector2Int> cells;
            if (!_committedByGroup.TryGetValue(groupId, out cells)) return;
            foreach (Vector2Int cell in cells)
            {
                string owner;
                if (_committedByCell.TryGetValue(cell, out owner) && owner == groupId)
                    _committedByCell.Remove(cell);
            }
            _committedByGroup.Remove(groupId);
        }

        static bool IsOwnedByOther(Dictionary<Vector2Int, string> owners,
            string groupId, Vector2Int cell)
        {
            string owner;
            return owners.TryGetValue(cell, out owner) && owner != groupId;
        }

        static HashSet<Vector2Int> GetOrCreate(
            Dictionary<string, HashSet<Vector2Int>> groups, string groupId)
        {
            HashSet<Vector2Int> cells;
            if (!groups.TryGetValue(groupId, out cells))
            {
                cells = new HashSet<Vector2Int>();
                groups[groupId] = cells;
            }
            return cells;
        }
    }
}
