namespace Mvp.Shared
{
    /// <summary>One kind of starting unit and how many the commander brings.</summary>
    public sealed class StartingUnitEntry
    {
        public UnitType UnitType;
        /// <summary>Number of occupied formation/grid slots.</summary>
        public int Count;
        /// <summary>Visual soldiers/vehicles represented by each occupied slot.</summary>
        public int MembersPerSlot = 1;

        public StartingUnitEntry() { }

        public StartingUnitEntry(UnitType unitType, int count)
            : this(unitType, count, 1)
        {
        }

        public StartingUnitEntry(UnitType unitType, int count, int membersPerSlot)
        {
            UnitType = unitType;
            Count = count;
            MembersPerSlot = membersPerSlot;
        }
    }
}
