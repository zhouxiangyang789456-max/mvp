using System.Collections.Generic;
using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Static registry of the MVP building archetypes (see 建筑与兵工厂系统设计文档 §13).
    /// Values match the §18.2 demo defaults; later can move to ScriptableObject.
    /// </summary>
    public static class BuildingCatalog
    {
        public const string ArmoryProductionCatalogId = "armory_basic";

        static readonly Dictionary<string, BuildingDefinition> _definitions;

        static BuildingCatalog()
        {
            _definitions = new Dictionary<string, BuildingDefinition>
            {
                {
                    "building_house",
                    new BuildingDefinition
                    {
                        Id = "building_house",
                        DisplayName = "楼房",
                        Type = BuildingType.House,
                        Footprint = new Vector2Int(1, 1),
                        CaptureRequiredSeconds = 5f,
                        GoldIncomeAmount = 500,
                        GoldIncomeInterval = 10f,
                        ProductionQueueSize = 0,
                        SpawnRetryInterval = 0.5f
                    }
                },
                {
                    "building_armory",
                    new BuildingDefinition
                    {
                        Id = "building_armory",
                        DisplayName = "兵工厂",
                        Type = BuildingType.Armory,
                        Footprint = new Vector2Int(1, 1),
                        CaptureRequiredSeconds = 7f,
                        GoldIncomeAmount = 0,
                        GoldIncomeInterval = 0f,
                        ProductionCatalogId = ArmoryProductionCatalogId,
                        ProductionQueueSize = 1,
                        SpawnRetryInterval = 0.5f
                    }
                }
            };
        }

        public static BuildingDefinition Get(string id)
        {
            BuildingDefinition def;
            return !string.IsNullOrEmpty(id) && _definitions.TryGetValue(id, out def) ? def : null;
        }

        public static BuildingDefinition Get(BuildingType type)
        {
            foreach (var kv in _definitions)
                if (kv.Value.Type == type) return kv.Value;
            return null;
        }
    }
}
