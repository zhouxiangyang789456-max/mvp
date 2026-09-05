using UnityEngine;

namespace Mvp.Shared
{
    /// <summary>
    /// Static definition of a building archetype (see 建筑与兵工厂系统设计文档 §13).
    /// Buildings cannot be attacked or destroyed; the only interaction is capture.
    /// </summary>
    public sealed class BuildingDefinition
    {
        /// <summary>Stable in-config id, e.g. "building_house".</summary>
        public string Id;
        public string DisplayName;
        public BuildingType Type;

        /// <summary>Occupied grid footprint (width × height in grid cells).</summary>
        public Vector2Int Footprint;

        /// <summary>Seconds of capture progress required to flip ownership (§5.2).</summary>
        public float CaptureRequiredSeconds;

        /// <summary>Gold granted per income tick once owned; 0 disables income.</summary>
        public int GoldIncomeAmount;

        /// <summary>Seconds between gold income ticks.</summary>
        public float GoldIncomeInterval;

        /// <summary>Key into <c>ProductionCatalog</c>; null/empty means no production.</summary>
        public string ProductionCatalogId;

        /// <summary>Number of parallel production queue slots (MVP = 1).</summary>
        public int ProductionQueueSize;

        /// <summary>Retry interval when the spawn exit cell is blocked (§10.2).</summary>
        public float SpawnRetryInterval;

        /// <summary>Derived: whether this building produces gold once owned.</summary>
        public bool CanProduceGold
        {
            get { return GoldIncomeAmount > 0 && GoldIncomeInterval > 0f; }
        }

        /// <summary>Derived: whether this building can produce units once owned.</summary>
        public bool CanProduceUnits
        {
            get { return !string.IsNullOrEmpty(ProductionCatalogId) && ProductionQueueSize > 0; }
        }
    }
}
