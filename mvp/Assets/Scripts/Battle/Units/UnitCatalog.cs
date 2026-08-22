using System.Collections.Generic;
using Mvp.Shared;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Static registry of the two MVP unit archetypes (步兵 / 坦克) per
    /// 战斗页面开发文档. Values match the spec exactly; later this can move
    /// to ScriptableObject assets without touching callers.
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
                        MoveSpeed = 1.25f,
                        VisionRange = 2,
                        AttackRange = 2,
                        AttackPower = 8,
                        AttackCooldown = 1.0f,
                        CanCaptureCity = true
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
                        MoveSpeed = 1.8f,
                        VisionRange = 3,
                        AttackRange = 4,
                        AttackPower = 25,
                        AttackCooldown = 1.5f,
                        CanCaptureCity = false
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
