using System.Collections.Generic;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Static registry of production catalogs: maps a catalog id to the unit
    /// types an armory can produce (see 建筑与兵工厂系统设计文档 §13).
    /// The armory UI reads from here; never hard-code the list in UI.
    /// </summary>
    public static class ProductionCatalog
    {
        static readonly Dictionary<string, UnitType[]> _catalogs;

        static ProductionCatalog()
        {
            _catalogs = new Dictionary<string, UnitType[]>
            {
                {
                    BuildingCatalog.ArmoryProductionCatalogId,
                    new[]
                    {
                        UnitType.Infantry,
                        UnitType.MachineGunner,
                        UnitType.Scout,
                        UnitType.ScoutCar,
                        UnitType.Tank,
                        UnitType.HeavyTank,
                        UnitType.SelfPropelledArtillery,
                        UnitType.RocketArtillery
                    }
                }
            };
        }

        /// <summary>Returns the producible unit types for a catalog id (never null).</summary>
        public static UnitType[] GetUnits(string catalogId)
        {
            UnitType[] units;
            return !string.IsNullOrEmpty(catalogId) && _catalogs.TryGetValue(catalogId, out units)
                ? units : new UnitType[0];
        }
    }
}
