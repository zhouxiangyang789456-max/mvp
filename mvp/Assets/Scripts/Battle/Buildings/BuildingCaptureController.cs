using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Battle.Outcome;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Drives capture progress for every building (阶段B, 设计文档 §5.2). On each
    /// MediumTick it counts alive capture-capable units standing on the building's
    /// adjacent ring (max 4 per side), pauses progress while both sides are present,
    /// and flips ownership once a side reaches <see cref="BuildingRuntime.CaptureRequiredSeconds"/>.
    /// </summary>
    public sealed class BuildingCaptureController : MonoBehaviour
    {
        public static BuildingCaptureController Instance { get; private set; }

        // MediumTick cadence (BattleTickService default = 0.1s).
        const float TickInterval = 0.1f;
        // Max capture-capable units counted per side (§5.2).
        const int MaxCounted = 4;

        readonly List<Vector2Int> _ring = new List<Vector2Int>();
        readonly List<UnitView> _captureUnits = new List<UnitView>();
        readonly List<BuildingRuntime> _progressChanged = new List<BuildingRuntime>();
        readonly List<UnitView> _contributingPlayer = new List<UnitView>();
        readonly List<UnitView> _contributingEnemy = new List<UnitView>();
        // Units actively capturing (last tick / this tick) for state-change detection.
        readonly List<UnitView> _capturingUnits = new List<UnitView>();
        readonly List<UnitView> _nextCapturing = new List<UnitView>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.MediumTick += OnMediumTick;
        }

        void OnDisable()
        {
            BattleTickService.MediumTick -= OnMediumTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnMediumTick()
        {
            if (BattleSimulationState.IsFrozen) return;
            var registry = BuildingRegistry.Instance;
            var selection = UnitSelectionController.Instance;
            if (registry == null || selection == null) return;
            if (registry.AllBuildings.Count == 0)
            {
                StopCapturingAll();
                return;
            }

            // Snapshot alive capture-capable units once; shared across all buildings.
            _captureUnits.Clear();
            var allUnits = selection.Units;
            for (int i = 0; i < allUnits.Count; i++)
            {
                var u = allUnits[i];
                if (u == null || u.Data == null || u.Data.State == UnitState.Dead) continue;
                if (u.Data.Definition == null) continue;
                if ((u.Data.Definition.Tags & UnitTag.CanCaptureBuilding) == 0) continue;
                _captureUnits.Add(u);
            }

            _nextCapturing.Clear();
            _progressChanged.Clear();
            for (int i = 0; i < registry.AllBuildings.Count; i++)
            {
                var building = registry.AllBuildings[i];
                if (building == null) continue;

                BuildAdjacentRing(building, _ring);

                _contributingPlayer.Clear();
                _contributingEnemy.Clear();
                int playerCount = 0;
                int enemyCount = 0;
                for (int j = 0; j < _captureUnits.Count; j++)
                {
                    var unit = _captureUnits[j];
                    if (unit == null || unit.Data == null) continue;
                    if (!_ring.Contains(unit.Data.GridPosition)) continue;
                    if (unit.Data.Team == TeamId.Player)
                    {
                        if (playerCount < MaxCounted)
                        {
                            playerCount++;
                            _contributingPlayer.Add(unit);
                        }
                    }
                    else if (unit.Data.Team == TeamId.Enemy)
                    {
                        if (enemyCount < MaxCounted)
                        {
                            enemyCount++;
                            _contributingEnemy.Add(unit);
                        }
                    }
                }

                bool contested = playerCount > 0 && enemyCount > 0;
                registry.SetContested(building, contested);
                if (contested) continue; // 双方争夺：进度暂停 (§5.2)

                float required = building.CaptureRequiredSeconds;
                if (required <= 0f) continue;

                bool alreadyPlayer = building.Owner == BuildingOwner.Player;
                bool alreadyEnemy = building.Owner == BuildingOwner.Enemy;
                if (playerCount > 0 && !alreadyPlayer)
                {
                    building.CaptureProgressPlayer += TickInterval * Multiplier(playerCount);
                    AddToNext(_contributingPlayer);
                }
                if (enemyCount > 0 && !alreadyEnemy)
                {
                    building.CaptureProgressEnemy += TickInterval * Multiplier(enemyCount);
                    AddToNext(_contributingEnemy);
                }

                bool flipped = false;
                if (building.CaptureProgressPlayer >= required)
                {
                    registry.SetOwner(building, BuildingOwner.Player);
                    flipped = true;
                }
                else if (building.CaptureProgressEnemy >= required)
                {
                    registry.SetOwner(building, BuildingOwner.Enemy);
                    flipped = true;
                }

                // SetOwner resets both progress accumulators and raised the ownership event.
                if (!flipped) _progressChanged.Add(building);
            }

            // Apply capture visual/state changes only on real deltas (no per-tick flicker).
            for (int i = 0; i < _nextCapturing.Count; i++)
            {
                var u = _nextCapturing[i];
                if (u == null || u.Data == null) continue;
                if (!_capturingUnits.Contains(u))
                {
                    u.SetCapturing(true);
                    if (u.Data.State == UnitState.Idle || u.Data.State == UnitState.Deploying)
                        u.Data.State = UnitState.Capturing;
                }
            }
            for (int i = 0; i < _capturingUnits.Count; i++)
            {
                var u = _capturingUnits[i];
                if (u == null) continue;
                if (!_nextCapturing.Contains(u))
                {
                    u.SetCapturing(false);
                    if (u.Data != null && u.Data.State == UnitState.Capturing)
                        u.Data.State = UnitState.Idle;
                }
            }
            _capturingUnits.Clear();
            _capturingUnits.AddRange(_nextCapturing);

            for (int i = 0; i < _progressChanged.Count; i++)
                registry.NotifyCaptureProgressChanged(_progressChanged[i]);
        }

        /// <summary>Adds units to the "capturing this tick" set, deduped.</summary>
        void AddToNext(List<UnitView> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null) continue;
                if (!_nextCapturing.Contains(u)) _nextCapturing.Add(u);
            }
        }

        /// <summary>Ends the capture state/flag for every unit currently capturing.</summary>
        void StopCapturingAll()
        {
            for (int i = 0; i < _capturingUnits.Count; i++)
            {
                var u = _capturingUnits[i];
                if (u == null) continue;
                u.SetCapturing(false);
                if (u.Data != null && u.Data.State == UnitState.Capturing)
                    u.Data.State = UnitState.Idle;
            }
            _capturingUnits.Clear();
        }

        /// <summary>1 unit → 1.0×, 2 → 1.5×, 3+ → 2.0× (§5.2).</summary>
        static float Multiplier(int count)
        {
            if (count >= 3) return 2.0f;
            if (count == 2) return 1.5f;
            return 1.0f;
        }

        /// <summary>
        /// The 8-neighbour ring around the footprint ("建筑旁"): all cells at Chebyshev
        /// distance 1 from any footprint cell but outside the footprint (12 cells for 2x2).
        /// Shared with <c>CommanderGroupCommandController</c> for capture move dispatch.
        /// </summary>
        public static void BuildAdjacentRing(BuildingRuntime building, List<Vector2Int> output)
        {
            output.Clear();
            int minX = building.AnchorCell.x - 1;
            int maxX = building.AnchorCell.x + building.Footprint.x;
            int minY = building.AnchorCell.y - 1;
            int maxY = building.AnchorCell.y + building.Footprint.y;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    bool onRing = x == minX || x == maxX || y == minY || y == maxY;
                    if (!onRing) continue;
                    output.Add(new Vector2Int(x, y));
                }
            }
        }
    }
}
