using System.Collections.Generic;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;
using Mvp.Battle.UI;
using Mvp.Battle.Units;
using Mvp.Shared;
using UnityEngine;

namespace Mvp.Battle.Outcome
{
    /// <summary>
    /// Timed, grid-based extraction objective. It deliberately does not use the
    /// imported portal package's Rigidbody/Collider transition system.
    /// </summary>
    public sealed class ExtractionObjectiveController : MonoBehaviour
    {
        public static ExtractionObjectiveController Instance { get; private set; }

        const float DefaultTimeLimit = 180f;
        const float DefaultOpeningDelay = 1f;

        readonly List<UnitView> _scan = new List<UnitView>();
        PortalExtractionZone _zone;
        ExtractionHud _hud;
        bool _started;
        bool _resolved;
        float _openingRemaining;
        float _openingDelay = DefaultOpeningDelay;

        public bool IsEnabled { get; private set; }
        public bool IsActive { get { return _started && _openingRemaining <= 0f && !_resolved; } }
        public bool IsStarted { get { return _started; } }
        public bool IsOpening { get { return _started && _openingRemaining > 0f; } }
        public bool Resolved { get { return _resolved; } }
        public bool IsComplete { get; private set; }
        public float TimeLimitSeconds { get; private set; } = DefaultTimeLimit;
        public float RemainingSeconds { get; private set; }
        public int RequiredCount { get; private set; }
        public int ExtractedCount { get; private set; }
        public Vector2Int PortalAnchor { get { return _zone != null ? _zone.Anchor : default(Vector2Int); } }
        /// <summary>World-space centre of the extraction zone, used to pull units in.</summary>
        public Vector3 PortalWorldCenter { get { return _zone != null ? _zone.transform.position : Vector3.zero; } }
        /// <summary>True once every enemy group is defeated/extracted (作战继续，仅提示).</summary>
        public bool EnemiesCleared
        {
            get
            {
                var registry = CommanderGroupRegistry.Instance;
                if (registry == null) return false;
                for (int i = 0; i < registry.Groups.Count; i++)
                {
                    var g = registry.Groups[i];
                    if (g == null || g.Team != TeamId.Enemy) continue;
                    if (!g.IsDefeated && !g.IsExtracted) return false;
                }
                return true;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void InitializeAfterSpawn()
        {
            if (IsEnabled) return;
            var grid = BattleGridController.Instance;
            var registry = CommanderGroupRegistry.Instance;
            if (grid == null || registry == null) return;

            RequiredCount = CountActivePlayerUnits(registry);
            if (RequiredCount <= 0) return;

            // Generated maps carry a seed-stable portal; a null portal on a generated
            // map means the level is an Elimination objective, so extraction stays off.
            var generated = BattleMapContext.LastGeneratedData;
            if (generated != null)
            {
                PortalSpawnData spawnData = generated.Portal;
                if (spawnData == null) return;
                TimeLimitSeconds = Mathf.Max(1, spawnData.TimeLimitSeconds);
                _openingDelay = Mathf.Max(0f, spawnData.OpeningDelaySeconds);
                _zone = PortalExtractionZone.Create(transform, grid, spawnData.AnchorCell,
                    spawnData.Width, spawnData.Height);
                IsEnabled = _zone != null;
            }
            else
            {
                // Hand-authored TestMap has no generated portal: run the legacy runtime
                // scan so the objective stays testable by directly opening BattleScene.
                Vector2Int anchor;
                if (!TryChoosePortalAnchor(grid, registry, out anchor))
                {
                    Debug.LogError("[Extraction] No legal portal zone could be placed.");
                    return;
                }
                _zone = PortalExtractionZone.Create(transform, grid, anchor, 2, 2);
                IsEnabled = _zone != null;
            }

            RemainingSeconds = TimeLimitSeconds;
            if (IsEnabled && _hud == null) _hud = ExtractionHud.Show(transform);
            Debug.Log("[Extraction] Portal ready at " + (_zone != null ? _zone.Anchor.ToString() : "?") +
                ", required=" + RequiredCount + ", limit=" + TimeLimitSeconds + "s");
        }

        void Update()
        {
            if (!IsEnabled || _resolved || BattleSimulationState.IsFrozen) return;
            if (!_started)
            {
                if (BattlePhaseState.Current != BattlePhase.Combat) return;
                _started = true;
                _openingRemaining = _openingDelay;
                _zone.SetOpen(false);
            }

            if (_openingRemaining > 0f)
            {
                _openingRemaining -= Time.deltaTime;
                if (_openingRemaining <= 0f) _zone.SetOpen(true);
                return;
            }

            // Entry is processed before timeout so a unit arriving on the zero
            // frame receives the deterministic success priority from the rules.
            ExtractUnitsInsideZone();
            if (EvaluateCompletion()) return;

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
            if (RemainingSeconds <= 0f)
                Resolve(BattleOutcome.Defeat, "撤离时间耗尽");
        }

        void ExtractUnitsInsideZone()
        {
            var selection = UnitSelectionController.Instance;
            if (selection == null || _zone == null) return;
            _scan.Clear();
            for (int i = 0; i < selection.Units.Count; i++)
            {
                var unit = selection.Units[i];
                if (unit == null || unit.Data == null || unit.Data.Team != TeamId.Player ||
                    unit.Data.State == UnitState.Dead || unit.Data.ExitState != UnitExitState.Active)
                    continue;
                if (_zone.Contains(unit.Data.GridPosition)) _scan.Add(unit);
            }

            for (int i = 0; i < _scan.Count; i++)
            {
                var unit = _scan[i];
                if (unit == null || unit.Data == null) continue;
                ExtractedCount++;
                _zone.PlayEntryPulse(unit.transform.position);
                unit.Extract();
            }
        }

        bool EvaluateCompletion()
        {
            var registry = CommanderGroupRegistry.Instance;
            int active = registry != null ? CountActivePlayerUnits(registry) : 0;
            if (active > 0) return false;

            if (ExtractedCount > 0)
            {
                IsComplete = true;
                Resolve(BattleOutcome.Victory, "全员撤离完成");
            }
            else
            {
                Resolve(BattleOutcome.Defeat, "玩家部队已被消灭");
            }
            return true;
        }

        void Resolve(BattleOutcome outcome, string statusText)
        {
            if (_resolved) return;
            _resolved = true;
            if (_zone != null) _zone.SetCompleted(outcome == BattleOutcome.Victory);
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus(statusText);
            var outcomeController = BattleOutcomeController.Instance;
            if (outcomeController != null) outcomeController.ResolveObjective(outcome);
        }

        static int CountActivePlayerUnits(CommanderGroupRegistry registry)
        {
            int count = 0;
            for (int g = 0; g < registry.Groups.Count; g++)
            {
                var group = registry.Groups[g];
                if (group == null || group.Team != TeamId.Player || group.IsExtracted) continue;
                for (int i = 0; i < group.Members.Count; i++)
                {
                    var unit = group.Members[i];
                    if (unit != null && unit.Data != null && unit.Data.State != UnitState.Dead &&
                        unit.Data.ExitState == UnitExitState.Active)
                        count++;
                }
            }
            return count;
        }

        static bool TryChoosePortalAnchor(BattleGridController grid,
            CommanderGroupRegistry registry, out Vector2Int best)
        {
            best = default(Vector2Int);
            int bestScore = int.MinValue;
            var pathfinder = new PathfindingService(grid);
            var path = new List<Vector2Int>();
            for (int z = 0; z < grid.Height - 1; z++)
            for (int x = 0; x < grid.Width - 1; x++)
            {
                var anchor = new Vector2Int(x, z);
                if (!IsLegalZone(grid, anchor)) continue;
                if (!IsReachableFromAllPlayerGroups(registry, pathfinder, path, anchor))
                    continue;
                int nearest = int.MaxValue;
                for (int g = 0; g < registry.Groups.Count; g++)
                {
                    var group = registry.Groups[g];
                    if (group == null || group.Team != TeamId.Player) continue;
                    for (int i = 0; i < group.Members.Count; i++)
                    {
                        var unit = group.Members[i];
                        if (unit == null || unit.Data == null) continue;
                        int distance = Mathf.Abs(unit.Data.GridPosition.x - x) +
                            Mathf.Abs(unit.Data.GridPosition.y - z);
                        nearest = Mathf.Min(nearest, distance);
                    }
                }
                if (nearest > bestScore)
                {
                    bestScore = nearest;
                    best = anchor;
                }
            }
            return bestScore != int.MinValue;
        }

        static bool IsReachableFromAllPlayerGroups(CommanderGroupRegistry registry,
            PathfindingService pathfinder, List<Vector2Int> path, Vector2Int anchor)
        {
            for (int g = 0; g < registry.Groups.Count; g++)
            {
                var group = registry.Groups[g];
                if (group == null || group.Team != TeamId.Player || group.Members.Count == 0)
                    continue;
                UnitView representative = null;
                for (int i = 0; i < group.Members.Count; i++)
                {
                    if (group.Members[i] != null && group.Members[i].Data != null)
                    {
                        representative = group.Members[i];
                        break;
                    }
                }
                if (representative == null) continue;
                bool reachable = false;
                for (int dz = 0; dz < 2 && !reachable; dz++)
                for (int dx = 0; dx < 2 && !reachable; dx++)
                    reachable = pathfinder.FindPath(representative.Data.GridPosition,
                        anchor + new Vector2Int(dx, dz), path, false);
                if (!reachable) return false;
            }
            return true;
        }

        static bool IsLegalZone(BattleGridController grid, Vector2Int anchor)
        {
            for (int dz = 0; dz < 2; dz++)
            for (int dx = 0; dx < 2; dx++)
            {
                var cell = anchor + new Vector2Int(dx, dz);
                if (!grid.InBounds(cell) || !grid.IsWalkable(cell) ||
                    grid.IsBlocked(cell) || grid.IsOccupied(cell)) return false;
            }
            return true;
        }
    }
}
