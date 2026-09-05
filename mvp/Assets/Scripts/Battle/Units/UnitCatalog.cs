using System.Collections.Generic;
using Mvp.Shared;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Static registry of the MVP unit archetypes (see 建筑与兵工厂系统设计文档 §7.2).
    /// Values match the spec exactly; tuning happens through the config in §18.
    /// Later this can move to ScriptableObject assets without touching callers.
    /// </summary>
    public static class UnitCatalog
    {
        static readonly Dictionary<UnitType, UnitDefinition> _definitions;

        static UnitCatalog()
        {
            _definitions = new Dictionary<UnitType, UnitDefinition>
            {
                {
                    UnitType.Infantry,
                    new UnitDefinition
                    {
                        Type = UnitType.Infantry,
                        DisplayName = "步兵",
                        Cost = 1000,
                        MaxHealth = 100,
                        MoveSpeed = 0.417f,
                        VisionRange = 2,
                        AttackRangeMax = 2,
                        AttackPower = 8,
                        AttackCooldown = 1.0f,
                        Tags = UnitTag.Infantry | UnitTag.CanCaptureBuilding,
                        ProductionSeconds = 4f,
                        CounterDamageMultiplier = 1f
                    }
                },
                {
                    UnitType.MachineGunner,
                    new UnitDefinition
                    {
                        Type = UnitType.MachineGunner,
                        DisplayName = "机枪兵",
                        Cost = 3000,
                        MaxHealth = 90,
                        MoveSpeed = 0.4f,
                        VisionRange = 2,
                        AttackRangeMax = 2,
                        AttackPower = 12,
                        AttackCooldown = 1.0f,
                        Tags = UnitTag.Infantry | UnitTag.CanCaptureBuilding,
                        ProductionSeconds = 6f,
                        CounterTargets = new[] { UnitType.Infantry, UnitType.Scout },
                        CounterDamageMultiplier = 1.5f
                    }
                },
                {
                    UnitType.Scout,
                    new UnitDefinition
                    {
                        Type = UnitType.Scout,
                        DisplayName = "侦察兵",
                        Cost = 2000,
                        MaxHealth = 60,
                        MoveSpeed = 0.467f,
                        VisionRange = 5,
                        AttackRangeMax = 1,
                        AttackPower = 5,
                        AttackCooldown = 1.0f,
                        Tags = UnitTag.Infantry | UnitTag.Scout | UnitTag.CanCaptureBuilding,
                        ProductionSeconds = 5f,
                        CounterDamageMultiplier = 1f
                    }
                },
                {
                    UnitType.ScoutCar,
                    new UnitDefinition
                    {
                        Type = UnitType.ScoutCar,
                        DisplayName = "侦察车",
                        Cost = 4000,
                        MaxHealth = 120,
                        MoveSpeed = 0.8f,
                        VisionRange = 6,
                        AttackRangeMax = 1,
                        AttackPower = 8,
                        AttackCooldown = 1.0f,
                        Tags = UnitTag.Vehicle | UnitTag.Scout,
                        ProductionSeconds = 7f,
                        CounterDamageMultiplier = 1f
                    }
                },
                {
                    UnitType.Tank,
                    new UnitDefinition
                    {
                        Type = UnitType.Tank,
                        DisplayName = "坦克",
                        Cost = 7000,
                        MaxHealth = 220,
                        MoveSpeed = 0.6f,
                        VisionRange = 3,
                        AttackRangeMax = 4,
                        AttackPower = 25,
                        AttackCooldown = 1.5f,
                        Tags = UnitTag.Vehicle | UnitTag.CloseMechanical | UnitTag.Tank,
                        ProductionSeconds = 10f,
                        CounterTargets = new[] { UnitType.Infantry, UnitType.MachineGunner, UnitType.ScoutCar },
                        CounterDamageMultiplier = 1.25f
                    }
                },
                {
                    UnitType.HeavyTank,
                    new UnitDefinition
                    {
                        Type = UnitType.HeavyTank,
                        DisplayName = "大坦克",
                        Cost = 12000,
                        MaxHealth = 450,
                        MoveSpeed = 0.4f,
                        VisionRange = 3,
                        AttackRangeMax = 3,
                        AttackPower = 50,
                        AttackCooldown = 1.8f,
                        Tags = UnitTag.Vehicle | UnitTag.CloseMechanical | UnitTag.Tank,
                        ProductionSeconds = 14f,
                        CounterTargets = new[] { UnitType.Tank },
                        CounterDamageMultiplier = 1.5f
                    }
                },
                {
                    UnitType.SelfPropelledArtillery,
                    new UnitDefinition
                    {
                        Type = UnitType.SelfPropelledArtillery,
                        DisplayName = "自行火炮",
                        Cost = 16000,
                        MaxHealth = 140,
                        MoveSpeed = 0.333f,
                        VisionRange = 4,
                        AttackRangeMin = 3,
                        AttackRangeMax = 8,
                        AttackPower = 60,
                        AttackCooldown = 4.0f,
                        AreaRadius = 1f,
                        Tags = UnitTag.Vehicle | UnitTag.LongRangeMechanical | UnitTag.Ranged,
                        ProductionSeconds = 16f,
                        CounterTargets = new[] { UnitType.Tank, UnitType.HeavyTank },
                        CounterDamageMultiplier = 1.5f
                    }
                },
                {
                    UnitType.RocketArtillery,
                    new UnitDefinition
                    {
                        Type = UnitType.RocketArtillery,
                        DisplayName = "火箭炮车",
                        Cost = 18000,
                        MaxHealth = 120,
                        MoveSpeed = 0.367f,
                        VisionRange = 4,
                        AttackRangeMin = 4,
                        AttackRangeMax = 9,
                        AttackPower = 45,
                        AttackCooldown = 5.0f,
                        AreaRadius = 1.5f,
                        Tags = UnitTag.Vehicle | UnitTag.LongRangeMechanical | UnitTag.Ranged,
                        ProductionSeconds = 18f,
                        CounterTargets = new[] { UnitType.Infantry, UnitType.MachineGunner, UnitType.Scout },
                        CounterDamageMultiplier = 1.25f
                    }
                }
            };
        }

        public static UnitDefinition Get(UnitType type)
        {
            UnitDefinition def;
            return _definitions.TryGetValue(type, out def) ? def : null;
        }
    }
}
