using System;
using UnityEngine;
using Mvp.Battle.Buildings;
using Mvp.Shared;
using Mvp.Battle.Outcome;

namespace Mvp.Battle.Economy
{
    /// <summary>
    /// Battle-side economy: per-side gold plus per-building gold income (阶段B, §18.2).
    /// Owned buildings that <see cref="BuildingRuntime.CanProduceGold"/> grant gold every
    /// <see cref="BuildingDefinition.GoldIncomeInterval"/> seconds via per-building timers.
    /// GoldChanged fires after every mutation so the UI can stay in sync.
    /// </summary>
    public sealed class BattleEconomyController : MonoBehaviour
    {
        public static BattleEconomyController Instance { get; private set; }

        /// <summary>Starting gold per side (§18.2).</summary>
        public const int InitialGold = 5000;

        // SlowTick cadence (BattleTickService default = 0.3s).
        const float SlowTickInterval = 0.3f;

        public event Action<TeamId, int> GoldChanged;

        public int PlayerGold { get; private set; }
        public int EnemyGold { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            PlayerGold = InitialGold;
            EnemyGold = InitialGold;
        }

        void OnEnable()
        {
            BattleTickService.SlowTick += OnSlowTick;
        }

        void OnDisable()
        {
            BattleTickService.SlowTick -= OnSlowTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnSlowTick()
        {
            if (BattleSimulationState.IsFrozen) return;
            var registry = BuildingRegistry.Instance;
            if (registry == null) return;
            for (int i = 0; i < registry.AllBuildings.Count; i++)
            {
                var building = registry.AllBuildings[i];
                if (building == null || !building.CanProduceGold) continue;
                if (building.Owner != BuildingOwner.Player && building.Owner != BuildingOwner.Enemy) continue;
                if (building.Definition == null) continue;
                float interval = building.Definition.GoldIncomeInterval;
                int amount = building.Definition.GoldIncomeAmount;
                if (interval <= 0f || amount <= 0) continue;

                building.GoldIncomeTimer += SlowTickInterval;
                if (building.GoldIncomeTimer < interval) continue;
                building.GoldIncomeTimer -= interval;
                AddGold(building.Owner == BuildingOwner.Player ? TeamId.Player : TeamId.Enemy, amount);
            }
        }

        /// <summary>Atomically deducts gold; returns false when insufficient funds.</summary>
        public bool TrySpend(TeamId team, int amount)
        {
            if (amount < 0) return false;
            int gold = team == TeamId.Player ? PlayerGold : EnemyGold;
            if (gold < amount) return false;
            SetGold(team, gold - amount);
            return true;
        }

        public void AddGold(TeamId team, int amount)
        {
            if (amount <= 0) return;
            SetGold(team, (team == TeamId.Player ? PlayerGold : EnemyGold) + amount);
        }

        public int GetGold(TeamId team)
        {
            return team == TeamId.Player ? PlayerGold : EnemyGold;
        }

        void SetGold(TeamId team, int value)
        {
            if (team == TeamId.Player) PlayerGold = value;
            else EnemyGold = value;
            if (GoldChanged != null) GoldChanged(team, value);
        }
    }
}
