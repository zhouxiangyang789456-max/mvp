using System;
using Mvp.CommanderSelect;

namespace Mvp.Progression
{
    public static class PlayerProgressionStore
    {
        static PlayerProgressionSnapshot _current;
        public static PlayerProgressionSnapshot Current => _current ?? (_current = CreateInitial());

        public static bool TryCommit(int expectedVersion, PlayerProgressionSnapshot snapshot)
        {
            if (snapshot == null || Current.Version != expectedVersion) return false;
            var committed = snapshot.Clone();
            committed.Version = expectedVersion + 1;
            _current = committed;
            return true;
        }

        static PlayerProgressionSnapshot CreateInitial()
        {
            var snapshot = new PlayerProgressionSnapshot { Gold = 30, Version = 1 };
            var commanders = CommanderCatalog.GetAll();
            for (int c = 0; c < commanders.Count; c++)
            {
                var commander = commanders[c];
                var loadout = new CommanderLoadoutSnapshot { CommanderId = commander.Id };
                snapshot.CommanderLoadouts.Add(loadout);
                for (int slot = 0; slot < commander.Traits.Count && slot < 4; slot++)
                {
                    var definition = TraitCatalog.FindByDisplayName(commander.Traits[slot]);
                    if (definition == null) continue;
                    var card = new TraitCardInstance
                    {
                        InstanceId = "initial_" + commander.Id + "_" + slot,
                        DefinitionId = definition.Id,
                        Location = TraitCardLocation.Equipped,
                        EquippedCommanderId = commander.Id,
                        EquippedSlotIndex = slot
                    };
                    snapshot.TraitCards.Add(card);
                    loadout.TraitCardInstanceIds[slot] = card.InstanceId;
                }
            }
            return snapshot;
        }
    }
}
