using System.Collections.Generic;
using Mvp.Battle.Formation;

namespace Mvp.Shared
{
    /// <summary>Immutable-at-battle-start copy of the selected commander roster.</summary>
    public sealed class ExpeditionRosterSnapshot
    {
        public readonly List<ExpeditionCommanderEntry> Commanders =
            new List<ExpeditionCommanderEntry>();

        public bool IsEmpty { get { return Commanders.Count == 0; } }
    }

    public sealed class ExpeditionCommanderEntry
    {
        public string CommanderId;
        public int RosterIndex;
        public int StartingHealth;
        public FormationType InitialFormation;
        public readonly List<StartingUnitEntry> StartingUnits =
            new List<StartingUnitEntry>();

        public static ExpeditionCommanderEntry FromDefinition(
            CommanderDefinition definition, int rosterIndex)
        {
            var entry = new ExpeditionCommanderEntry
            {
                CommanderId = definition.Id,
                RosterIndex = rosterIndex,
                StartingHealth = definition.CurrentHealth,
                InitialFormation = FormationType.Square
            };
            for (int i = 0; i < definition.StartingUnits.Count; i++)
            {
                var source = definition.StartingUnits[i];
                entry.StartingUnits.Add(new StartingUnitEntry(
                    source.UnitType, source.Count, source.MembersPerSlot));
            }
            return entry;
        }
    }
}
