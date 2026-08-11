namespace Mvp.Shared
{
    /// <summary>One kind of starting unit and how many the commander brings.</summary>
    public sealed class StartingUnitEntry
    {
        public UnitType UnitType;
        public int Count;

        public StartingUnitEntry() { }

        public StartingUnitEntry(UnitType unitType, int count)
        {
            UnitType = unitType;
            Count = count;
        }
    }
}
