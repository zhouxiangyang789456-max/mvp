namespace Mvp.Battle.Outcome
{
    public static class BattleSimulationState
    {
        public static bool IsFrozen { get; private set; }
        public static void Freeze() { IsFrozen = true; }
        public static void Reset() { IsFrozen = false; }
    }
}
