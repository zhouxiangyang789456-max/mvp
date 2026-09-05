using System;
using Mvp.Battle.Commanders;
using Mvp.Battle.UI;
using Mvp.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mvp.Battle.Outcome
{
    public sealed class BattleOutcomeController : MonoBehaviour
    {
        const float CandidateDelay = 0.35f;
        public static BattleOutcomeController Instance { get; private set; }
        public BattleOutcomeState State { get; private set; }
        public BattleResultSnapshot Result { get; private set; }
        public event Action<BattleResultSnapshot> BattleResolved;

        int _initialPlayers;
        int _initialEnemies;
        int _initialPlayerUnits;
        int _initialEnemyUnits;
        int _resolutionTick;
        float _candidateTimer;
        BattleOutcome _candidate;
        CommanderGroupRegistry _registry;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            State = BattleOutcomeState.Registering;
            BattleSimulationState.Reset();
        }

        void Start()
        {
            _registry = CommanderGroupRegistry.Instance;
            if (_registry != null) _registry.GroupDefeated += OnGroupDefeated;
        }

        void OnEnable() { BattleTickService.SlowTick += OnSlowTick; }
        void OnDisable() { BattleTickService.SlowTick -= OnSlowTick; }

        void OnDestroy()
        {
            if (_registry != null) _registry.GroupDefeated -= OnGroupDefeated;
            if (Instance == this) Instance = null;
        }

        public void NotifyInitialSpawnCompleted()
        {
            if (State != BattleOutcomeState.Registering) return;
            _registry = CommanderGroupRegistry.Instance;
            CountInitialForces();
            if (_initialPlayers <= 0 || _initialEnemies <= 0)
            {
                Debug.LogError("[BattleOutcome] Cannot arm: both teams require at least one group.");
                return;
            }
            State = BattleOutcomeState.Armed;
        }

        public void NotifyCombatStarted()
        {
            if (State != BattleOutcomeState.Armed) return;
            State = BattleOutcomeState.Running;
            Evaluate();
        }

        public bool TryForceVictory()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (State == BattleOutcomeState.Registering || State == BattleOutcomeState.Resolved ||
                State == BattleOutcomeState.Presenting || State == BattleOutcomeState.Transitioning ||
                State == BattleOutcomeState.Aborted)
                return false;
            ResolveOnce(BattleOutcome.Victory);
            return Result != null && Result.Outcome == BattleOutcome.Victory;
#else
            return false;
#endif
        }

        public bool ResolveObjective(BattleOutcome outcome)
        {
            if (outcome != BattleOutcome.Victory && outcome != BattleOutcome.Defeat)
                return false;
            if (State != BattleOutcomeState.Running && State != BattleOutcomeState.Candidate)
                return false;
            ResolveOnce(outcome);
            return Result != null && Result.Outcome == outcome;
        }

        void OnGroupDefeated(CommanderGroupRuntime group)
        {
            if (State == BattleOutcomeState.Running || State == BattleOutcomeState.Candidate)
                Evaluate();
        }

        void OnSlowTick()
        {
            if (State != BattleOutcomeState.Running && State != BattleOutcomeState.Candidate) return;
            _resolutionTick++;
            if (State == BattleOutcomeState.Candidate)
            {
                _candidateTimer -= 0.3f;
                var current = DetermineOutcome(out _, out _);
                if (current == BattleOutcome.None) { State = BattleOutcomeState.Running; _candidate = BattleOutcome.None; return; }
                if (current != _candidate) { _candidate = current; _candidateTimer = CandidateDelay; return; }
                if (_candidateTimer <= 0f) ResolveOnce(current);
                return;
            }
            Evaluate();
        }

        void Evaluate()
        {
            int players, enemies;
            var outcome = DetermineOutcome(out players, out enemies);
            if (outcome == BattleOutcome.None) return;
            _candidate = outcome;
            _candidateTimer = CandidateDelay;
            State = BattleOutcomeState.Candidate;
        }

        BattleOutcome DetermineOutcome(out int players, out int enemies)
        {
            players = enemies = 0;
            if (BattlePhaseState.Current != BattlePhase.Combat || _registry == null) return BattleOutcome.None;
            for (int i = 0; i < _registry.Groups.Count; i++)
            {
                var group = _registry.Groups[i];
                if (group == null || group.IsDefeated || group.IsExtracted) continue;
                if (group.Team == TeamId.Player) players++; else if (group.Team == TeamId.Enemy) enemies++;
            }
            var extraction = ExtractionObjectiveController.Instance;
            if (extraction != null && extraction.IsEnabled)
            {
                if (players == 0 && extraction.ExtractedCount == 0)
                    return BattleOutcome.Defeat;
                return BattleOutcome.None;
            }
            if (players == 0 && enemies == 0) return BattleOutcome.Draw;
            if (enemies == 0 && players > 0) return BattleOutcome.Victory;
            if (players == 0 && enemies > 0) return BattleOutcome.Defeat;
            return BattleOutcome.None;
        }

        void ResolveOnce(BattleOutcome outcome)
        {
            if (State == BattleOutcomeState.Resolved || State == BattleOutcomeState.Presenting ||
                State == BattleOutcomeState.Transitioning || State == BattleOutcomeState.Aborted) return;
            int players, enemies;
            DetermineOutcome(out players, out enemies);
            string battleId = SceneManager.GetActiveScene().name + "_" + DateTime.UtcNow.Ticks;
            Result = new BattleResultSnapshot
            {
                BattleId = battleId,
                LevelId = SceneManager.GetActiveScene().name,
                Outcome = outcome,
                ResolutionTick = _resolutionTick,
                InitialPlayerGroups = _initialPlayers,
                InitialEnemyGroups = _initialEnemies,
                InitialPlayerUnits = _initialPlayerUnits,
                InitialEnemyUnits = _initialEnemyUnits,
                SurvivingPlayerGroups = players,
                SurvivingEnemyGroups = enemies,
                PlayerUnitsLost = _initialPlayerUnits - CountAliveUnits(TeamId.Player),
                EnemyUnitsDefeated = _initialEnemyUnits - CountAliveUnits(TeamId.Enemy),
                RewardGold = outcome == BattleOutcome.Victory ? 10 : 0,
                RewardGrantId = battleId + "_reward",
                ShopRandomSeed = battleId.GetHashCode()
            };
            var extraction = ExtractionObjectiveController.Instance;
            if (extraction != null && extraction.IsEnabled)
            {
                Result.ObjectiveType = BattleObjectiveType.TimedExtraction;
                Result.TimeLimitSeconds = extraction.TimeLimitSeconds;
                Result.RemainingSeconds = extraction.RemainingSeconds;
                Result.RequiredExtractionCount = extraction.RequiredCount;
                Result.ExtractedUnitCount = extraction.ExtractedCount;
                Result.ExtractionCompleted = extraction.IsComplete;
            }
            else
            {
                Result.ObjectiveType = BattleObjectiveType.Elimination;
            }
            State = BattleOutcomeState.Resolved;
            BattleSimulationState.Freeze();
            if (BattleResolved != null) BattleResolved(Result);
            State = BattleOutcomeState.Presenting;
            BattleResultController.Show(Result, this);
        }

        void CountInitialForces()
        {
            _initialPlayers = _initialEnemies = _initialPlayerUnits = _initialEnemyUnits = 0;
            if (_registry == null) return;
            for (int i = 0; i < _registry.Groups.Count; i++)
            {
                var group = _registry.Groups[i];
                if (group.Team == TeamId.Player) { _initialPlayers++; _initialPlayerUnits += group.AliveMemberCount; }
                else if (group.Team == TeamId.Enemy) { _initialEnemies++; _initialEnemyUnits += group.AliveMemberCount; }
            }
        }

        int CountAliveUnits(TeamId team)
        {
            int count = 0;
            if (_registry == null) return count;
            for (int i = 0; i < _registry.Groups.Count; i++)
                if (_registry.Groups[i].Team == team) count += _registry.Groups[i].AliveMemberCount;
            return count;
        }

        public void BeginTransition() { if (State == BattleOutcomeState.Presenting) State = BattleOutcomeState.Transitioning; }
    }
}
