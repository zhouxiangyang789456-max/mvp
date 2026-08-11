namespace Mvp.Shared
{
    /// <summary>Static definition of a unit archetype.</summary>
    public sealed class UnitDefinition
    {
        public UnitType Type;
        public string DisplayName;
        public int Cost;
        public int MaxHealth;
        public float MoveSpeed;
        public int VisionRange;
        public float AttackRange;
        public int AttackPower;
        public float AttackCooldown;
        public bool CanCaptureCity;
    }
}
