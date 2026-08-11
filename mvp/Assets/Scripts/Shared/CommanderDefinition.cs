using System.Collections.Generic;

namespace Mvp.Shared
{
    /// <summary>Static definition of a commander (selectable on CommanderSelectScene).</summary>
    public sealed class CommanderDefinition
    {
        public string Id;
        public string DisplayName;
        public int MaxHealth;
        public int CurrentHealth;
        public List<string> Traits = new List<string>();
        public List<StartingUnitEntry> StartingUnits = new List<StartingUnitEntry>();
    }
}
