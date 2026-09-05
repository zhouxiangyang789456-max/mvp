namespace Mvp.Shared
{
    /// <summary>
    /// Static definition of a unit archetype (see 建筑与兵工厂系统设计文档 §13).
    /// Values are extended for the armory production and extended unit types.
    /// </summary>
    public sealed class UnitDefinition
    {
        public UnitType Type;
        public string DisplayName;
        public int Cost;
        public int MaxHealth;
        public float MoveSpeed;
        public int VisionRange;
        /// <summary>Maximum attack range in grid cells.</summary>
        public float AttackRangeMax;
        /// <summary>Minimum attack range; 0 means no minimum (no blind spot).</summary>
        public float AttackRangeMin;
        public int AttackPower;
        public float AttackCooldown;
        /// <summary>Area damage radius in grid cells; 0 means single target.</summary>
        public float AreaRadius;
        /// <summary>Composite classification tags (replaces the old CanCaptureCity bool).</summary>
        public UnitTag Tags;
        /// <summary>Production time in seconds at an armory.</summary>
        public float ProductionSeconds;
        /// <summary>Unit types this unit deals bonus damage to.</summary>
        public UnitType[] CounterTargets;
        /// <summary>Damage multiplier when the target type is in CounterTargets (default 1).</summary>
        public float CounterDamageMultiplier = 1f;
    }
}
