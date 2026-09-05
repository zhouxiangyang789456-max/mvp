using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// World-space range highlight for special skills (远攻, 战斗技能系统开发文档 §7).
    /// Shows the union of every eligible member's scaled Chebyshev range; overlapping
    /// cells get a brighter fill so the player reads the strongest coverage. A pulsing
    /// cursor marks the hovered cell while targeting is active.
    ///
    /// Uses pooled quads (UiPool SkillRangeHighlight / SkillRangeCursor) and is driven
    /// by SkillTargetingController.
    /// </summary>
    public sealed class SkillRangePreview : MonoBehaviour
    {
        public static SkillRangePreview Instance { get; private set; }

        readonly List<PoolableUi> _cells = new List<PoolableUi>();
        readonly Dictionary<Vector2Int, int> _coverage = new Dictionary<Vector2Int, int>();
        readonly List<Vector2Int> _cellsOrder = new List<Vector2Int>();
        PoolableUi _cursor;
        Vector2Int _hoverCell;
        bool _hoverActive;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            Hide();
            if (Instance == this) Instance = null;
        }

        public bool IsShowing { get { return _cells.Count > 0; } }

        /// <summary>True when <paramref name="cell"/> lies inside the shown range union.</summary>
        public bool Covers(Vector2Int cell)
        {
            return _coverage.ContainsKey(cell);
        }

        /// <summary>
        /// Rebuilds the union of eligible members' scaled ranges. Only members that can
        /// actually cast right now (off cooldown, alive, carries the required tag)
        /// contribute, so the highlight always matches what will fire on confirm.
        /// </summary>
        public void ShowRange(CommanderGroupRuntime group, SkillDefinition def)
        {
            Hide();
            if (group == null || def == null) return;
            var grid = BattleGridController.Instance;
            var pool = UiPool.Instance;
            if (grid == null || pool == null) return;
            float now = Time.time;

            if (def.Id == SkillIds.Decoy)
            {
                int range = Mathf.Max(0, def.RangeCells);
                for (int dz = -range; dz <= range; dz++)
                {
                    for (int dx = -range; dx <= range; dx++)
                    {
                        var cell = new Vector2Int(group.AnchorCell.x + dx, group.AnchorCell.y + dz);
                        if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                        _coverage[cell] = 1;
                        _cellsOrder.Add(cell);
                    }
                }
                BuildHighlights(pool);
                return;
            }

            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (!SkillEligibilityService.IsMemberEligible(group, member, def, now)) continue;
                int minRange, maxRange;
                SkillRangeMath.ComputeMemberRanges(member, def, out minRange, out maxRange);
                if (maxRange <= 0) continue;
                var origin = member.Data.GridPosition;
                for (int dz = -maxRange; dz <= maxRange; dz++)
                {
                    for (int dx = -maxRange; dx <= maxRange; dx++)
                    {
                        int dist = SkillRangeMath.Chebyshev(origin,
                            new Vector2Int(origin.x + dx, origin.y + dz));
                        if (dist < minRange || dist > maxRange) continue;
                        var cell = new Vector2Int(origin.x + dx, origin.y + dz);
                        if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) continue;
                        int overlap;
                        if (_coverage.TryGetValue(cell, out overlap)) _coverage[cell] = overlap + 1;
                        else { _coverage[cell] = 1; _cellsOrder.Add(cell); }
                    }
                }
            }

            BuildHighlights(pool);
        }

        void BuildHighlights(UiPool pool)
        {
            for (int i = 0; i < _cellsOrder.Count; i++)
            {
                var cell = _cellsOrder[i];
                int overlap = _coverage[cell];
                var ui = pool.Get(UiPoolType.SkillRangeHighlight, GridWorld(cell),
                    Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.92f, 0.92f, 1f));
                if (ui == null) continue;
                var sr = ui.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float t = Mathf.Clamp01(overlap / 3f);
                    var c = sr.color;
                    c.a = 0.18f + 0.22f * t; // overlap cells read brighter
                    sr.color = c;
                    sr.sortingOrder = 82;
                }
                _cells.Add(ui);
            }
        }

        public void Hide()
        {
            var pool = UiPool.Instance;
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i] != null && pool != null) pool.Release(_cells[i]);
            _cells.Clear();
            _coverage.Clear();
            _cellsOrder.Clear();
            ReleaseCursor();
        }

        /// <summary>Shows/moves/hides the hover cursor. Pass null to hide.</summary>
        public void SetHover(Vector2Int? cell)
        {
            if (!cell.HasValue || !_coverage.ContainsKey(cell.Value))
            {
                ReleaseCursor();
                return;
            }
            EnsureCursor(cell.Value);
        }

        void EnsureCursor(Vector2Int cell)
        {
            if (_cursor == null)
            {
                var pool = UiPool.Instance;
                if (pool == null) return;
                _cursor = pool.Get(UiPoolType.SkillRangeCursor, GridWorld(cell),
                    Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            }
            if (_cursor == null) return;
            if (_hoverActive && _hoverCell == cell) return;
            var sr = _cursor.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 86;
            var t = _cursor.transform;
            t.position = GridWorld(cell);
            t.rotation = Quaternion.Euler(-90f, 0f, 0f);
            t.localScale = Vector3.one;
            _hoverCell = cell;
            _hoverActive = true;
        }

        void ReleaseCursor()
        {
            if (_cursor == null) return;
            var pool = UiPool.Instance;
            if (pool != null) pool.Release(_cursor);
            _cursor = null;
            _hoverActive = false;
        }

        void Update()
        {
            if (_cursor == null) return;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            _cursor.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        static Vector3 GridWorld(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return new Vector3(cell.x, 0.03f, cell.y);
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell)) + 0.03f;
            return p;
        }
    }
}
