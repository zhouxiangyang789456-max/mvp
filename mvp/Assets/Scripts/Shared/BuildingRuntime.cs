using UnityEngine;

namespace Mvp.Shared
{
    /// <summary>
    /// Runtime instance of one building on the battle map (see 建筑与兵工厂系统设计文档 §14).
    /// Pure data: the registry owns spawning/placement and the capture/economy controllers
    /// own the progress/income logic. Deliberately not a MonoBehaviour — the footprint
    /// occupies multiple grid cells and is represented by a BuildingView placed by the
    /// registry.
    /// </summary>
    public sealed class BuildingRuntime
    {
        /// <summary>Stable in-config id, e.g. "building_house".</summary>
        public string DefinitionId;
        /// <summary>Unique per-battle instance id assigned by <c>BuildingRegistry</c>.</summary>
        public int InstanceId;
        /// <summary>Resolved definition from <c>BuildingCatalog</c> (never null once registered).</summary>
        public BuildingDefinition Definition;
        public BuildingType Type;

        /// <summary>Grid footprint (width × height in cells), e.g. 2x2.</summary>
        public Vector2Int Footprint;

        /// <summary>Bottom-left cell of the footprint (footprint spans AnchorCell..AnchorCell+Footprint-1).</summary>
        public Vector2Int AnchorCell;

        /// <summary>Current owner. Neutral buildings are capturable; owned buildings flip to the capturing side.</summary>
        public BuildingOwner Owner = BuildingOwner.Neutral;

        /// <summary>Accumulated capture progress in seconds for each side (§5.2).</summary>
        public float CaptureProgressPlayer;
        public float CaptureProgressEnemy;

        /// <summary>True when both sides have capture-capable units adjacent (progress paused).</summary>
        public bool Contested;

        /// <summary>True once owned (Owner != Neutral); the visual reflects this via team colour.</summary>
        public bool IsOperational;

        /// <summary>Per-building income accumulator (seconds since last gold tick).</summary>
        public float GoldIncomeTimer;

        public string DisplayName
        {
            get { return Definition != null ? Definition.DisplayName : DefinitionId; }
        }

        /// <summary>Whether this building produces gold once owned.</summary>
        public bool CanProduceGold
        {
            get { return Definition != null && Definition.CanProduceGold; }
        }

        /// <summary>Whether this building can produce units once owned (armory; stage C).</summary>
        public bool CanProduceUnits
        {
            get { return Definition != null && Definition.CanProduceUnits; }
        }

        /// <summary>Seconds of progress required to flip ownership (§5.2 / §18.2).</summary>
        public float CaptureRequiredSeconds
        {
            get { return Definition != null ? Definition.CaptureRequiredSeconds : 0f; }
        }

        public bool IsPlayerOwned { get { return Owner == BuildingOwner.Player; } }
        public bool IsEnemyOwned { get { return Owner == BuildingOwner.Enemy; } }
        public bool IsNeutral { get { return Owner == BuildingOwner.Neutral; } }

        /// <summary>
        /// Re-derives operational state from ownership. Called after any ownership change.
        /// </summary>
        public void RefreshOperational()
        {
            IsOperational = Owner != BuildingOwner.Neutral;
        }
    }
}
