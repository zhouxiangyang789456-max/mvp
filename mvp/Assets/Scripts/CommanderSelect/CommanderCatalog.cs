using System.Collections.Generic;
using Mvp.Shared;

namespace Mvp.CommanderSelect
{
    /// <summary>
    /// Source of commander definitions. First version only 伊莲娜 is playable;
    /// the remaining card slots are placeholders.
    /// </summary>
    public static class CommanderCatalog
    {
        static readonly CommanderDefinition[] All =
        {
            BuildElena()
        };

        public static IReadOnlyList<CommanderDefinition> GetAll()
        {
            return All;
        }

        static CommanderDefinition BuildElena()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_elena",
                DisplayName = "伊莲娜",
                MaxHealth = 100,
                CurrentHealth = 86
            };
            c.Traits.Add("勇敢");
            c.Traits.Add("谨慎");
            c.Traits.Add("鼓舞");
            c.Traits.Add("坚韧");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            return c;
        }
    }
}
