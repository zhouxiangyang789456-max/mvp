using System.Collections.Generic;

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
}
