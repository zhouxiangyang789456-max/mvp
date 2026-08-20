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
            BuildElena(),
            BuildCassian()
        };

        public static IReadOnlyList<CommanderDefinition> GetAll()
        {
            return All;
        }

        public static CommanderDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        static CommanderDefinition BuildElena()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_elena",
                DisplayName = "伊莲娜",
                MaxHealth = 100,
                CurrentHealth = 86,
                PortraitAssetId = "Battle/UI/CommanderPortraits/elena_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/elena_map_marker"
            };
            c.Traits.Add("勇敢");
            c.Traits.Add("谨慎");
            c.Traits.Add("鼓舞");
            c.Traits.Add("坚韧");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            return c;
        }

        static CommanderDefinition BuildCassian()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_cassian",
                DisplayName = "卡西安",
                MaxHealth = 120,
                CurrentHealth = 112,
                PortraitAssetId = "Battle/UI/CommanderPortraits/cassian_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/cassian_map_marker"
            };
            c.Traits.Add("沉着");
            c.Traits.Add("守备");
            c.Traits.Add("纪律");
            c.Traits.Add("坚守");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 3));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            return c;
        }
    }
}
