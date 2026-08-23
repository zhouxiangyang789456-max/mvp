using System;
using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.SettlementShop
{
    public sealed class SettlementShopOpenArgs
    {
        public string SessionId;
        public string RewardGrantId;
        public int RandomSeed;
        public int RewardGold = 10;
        public TraitOfferRollContext RollContext;
        public readonly List<string> ActiveCommanderIds = new List<string>();

        /// <summary>所选指挥官 Id(§8.3 方向补强 DTO 来源);null 时不启用方向补强。</summary>
        public string SelectedCommanderId;
    }

    public sealed class SettlementShopTransactionService
    {
        static readonly Dictionary<string, SettlementShopSession> SuspendedSessions =
            new Dictionary<string, SettlementShopSession>();
        static readonly HashSet<string> CommittedSessions = new HashSet<string>();

        readonly ShopChangeSet _changes = new ShopChangeSet();
        public SettlementShopSession Session { get; private set; }
        public event Action<ShopChangeSet, int> Changed;

        public bool Open(SettlementShopOpenArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.SessionId)) return false;
            if (CommittedSessions.Contains(args.SessionId)) return false;
            SettlementShopSession restored;
            if (SuspendedSessions.TryGetValue(args.SessionId, out restored))
            {
                Session = restored;
                Session.Resume();
            }
            else
            {
                Session = new SettlementShopSession(args.SessionId, args.RewardGrantId,
                    args.RandomSeed, args.RewardGold, args.ActiveCommanderIds,
                    PlayerProgressionStore.Current, args.RollContext, args.SelectedCommanderId);
                SuspendedSessions.Add(args.SessionId, Session);
            }
            NotifyAll();
            return true;
        }

        public ShopOperationResult Buy(int offerIndex) => Run(c => Session.Buy(offerIndex, c));
        public ShopOperationResult Refresh(int price) => Run(c => Session.Refresh(price, c));
        public ShopOperationResult Equip(string cardId, string commanderId, int slot) =>
            Run(c => Session.Equip(cardId, commanderId, slot, c));
        public ShopOperationResult Unequip(string commanderId, int slot) =>
            Run(c => Session.Unequip(commanderId, slot, c));
        public ShopOperationResult MoveEquippedCard(string cardId, string sourceCommanderId,
            int sourceSlot, string targetCommanderId, int targetSlot) =>
            Run(c => Session.MoveEquippedCard(cardId, sourceCommanderId, sourceSlot,
                targetCommanderId, targetSlot, c));
        public ShopOperationResult Sell(string cardId) => Run(c => Session.Sell(cardId, c));

        public ShopOperationResult Commit()
        {
            if (Session == null) return ShopOperationResult.InvalidState;
            PlayerProgressionSnapshot snapshot;
            if (!Session.TryBuildCommitSnapshot(out snapshot)) return ShopOperationResult.CommitFailed;
            bool success = PlayerProgressionStore.TryCommit(Session.BaseProgressionVersion, snapshot);
            Session.FinishCommit(success);
            if (!success) return ShopOperationResult.VersionConflict;
            SuspendedSessions.Remove(Session.SessionId);
            CommittedSessions.Add(Session.SessionId);
            NotifyAll();
            return ShopOperationResult.Success;
        }

        public void Suspend()
        {
            if (Session != null) Session.Suspend();
        }

        ShopOperationResult Run(Func<ShopChangeSet, ShopOperationResult> operation)
        {
            if (Session == null) return ShopOperationResult.InvalidState;
            var result = operation(_changes);
            if (result == ShopOperationResult.Success)
                Changed?.Invoke(_changes, Session.DirtyVersion);
            return result;
        }

        void NotifyAll()
        {
            _changes.Clear();
            _changes.GoldChanged = true;
            _changes.InventoryOrderDirty = true;
            for (int i = 0; i < 3; i++) _changes.ChangedOfferIndices.Add(i);
            Changed?.Invoke(_changes, Session == null ? 0 : Session.DirtyVersion);
        }
    }
}
