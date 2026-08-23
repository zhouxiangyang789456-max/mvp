using System.Collections.Generic;

namespace Mvp.Shared
{
    /// <summary>Static definition of a commander (selectable on CommanderSelectScene).</summary>
    public sealed class CommanderDefinition
    {
        public string Id;
        public string DisplayName;

        /// <summary>称号,如 "绯红猎犬"。</summary>
        public string Title;

        /// <summary>独占标签,只出现在该指挥官的专属卡上(frenzy / bulwark / lethality / scorch / mercenary / frost)。</summary>
        public string ExclusiveTag;

        /// <summary>本指挥官的 2 条独占流派 Id(商店方向补强与 UI 展示用)。</summary>
        public readonly List<string> AffinityArchetypeIds = new List<string>();

        /// <summary>该指挥官专属卡 Id(20 张),商店按选人过滤卡池。</summary>
        public readonly List<string> ExclusiveTraitIds = new List<string>();

        public int MaxHealth;
        public int CurrentHealth;
        public string PortraitAssetId;
        public string MapPortraitAssetId;
        public List<string> Traits = new List<string>();
        public List<StartingUnitEntry> StartingUnits = new List<StartingUnitEntry>();
    }
}
