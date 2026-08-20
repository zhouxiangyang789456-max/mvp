using System;
using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.SettlementShop
{
    public enum ShopSessionState { Ready, Committing, Suspended, Committed }
    public enum ShopOperationResult
    {
        Success, InvalidState, VersionConflict, NotFound, InsufficientGold,
        InvalidOwnership, InvalidSlot, DuplicateTrait, AlreadyPurchased, CommitFailed
    }

    [Serializable]
    public sealed class ShopOffer
    {
        public string DefinitionId;
        public int Price;
        public bool Purchased;
    }

    public sealed class ShopChangeSet
    {
        public bool GoldChanged;
        public bool InventoryOrderDirty;
        public readonly List<int> ChangedOfferIndices = new List<int>(3);
        public readonly List<string> ChangedInstanceIds = new List<string>(4);
        public readonly List<string> ChangedCommanderIds = new List<string>(2);

        public void Clear()
        {
            GoldChanged = InventoryOrderDirty = false;
            ChangedOfferIndices.Clear();
            ChangedInstanceIds.Clear();
            ChangedCommanderIds.Clear();
        }
    }
}
