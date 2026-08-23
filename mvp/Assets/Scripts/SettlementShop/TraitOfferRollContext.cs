using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.SettlementShop
{
    /// <summary>
    /// Battle-performance context handed to TraitShopDirector so offer candidates
    /// are lightly driven by how the run went (loss ratio, owned tags, refreshes).
    /// </summary>
    public sealed class TraitOfferRollContext
    {
        public int LevelIndex = 1;
        public bool HasBattleResult;
        public int InitialPlayerUnits;
        public int PlayerUnitsLost;
        public int SurvivingPlayerGroups;
        public int InitialEnemyGroups;
        public int SurvivingEnemyGroups;
        public int RefreshCount;
        public readonly List<string> OwnedTraitTags = new List<string>();

        /// <summary>流派成型判定 DTO(由组合根注入;Director 据此补强主流派,不依赖分析器)。</summary>
        public BuildAffinitySummary Affinity;

        /// <summary>指挥官方向补强(§8.3);组合根注入,Director 只读。</summary>
        public CommanderAffinityOverride CommanderAffinity;

        public float LossRatio
        {
            get
            {
                return HasBattleResult && InitialPlayerUnits > 0
                    ? PlayerUnitsLost / (float)InitialPlayerUnits
                    : 0f;
            }
        }
    }

    /// <summary>
    /// 指挥官方向补强 DTO(§8.3):主/副方向的流派公式标签表。由组合根(Session)从
    /// CommanderCatalog + TraitBuildAnalyzer.Archetypes 构建一次,局内不变。Director 只读。
    /// </summary>
    public sealed class CommanderAffinityOverride
    {
        public string CommanderId;

        /// <summary>主方向公式标签(AffinityArchetypeIds[0])。</summary>
        public readonly List<string> MainTags = new List<string>();

        /// <summary>副方向公式标签(AffinityArchetypeIds[1])。</summary>
        public readonly List<string> SubTags = new List<string>();
    }
}
