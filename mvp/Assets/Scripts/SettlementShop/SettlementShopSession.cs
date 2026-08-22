using System;
using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.SettlementShop
{
    public sealed class SettlementShopSession
    {
        public string SessionId { get; }
        public string RewardGrantId { get; }
        public int RandomSeed { get; }
        public int BaseProgressionVersion { get; }
        public int DirtyVersion { get; private set; }
        public int RefreshCount { get; private set; }
        public int Gold { get; private set; }
        public int RewardGold { get; }
        public ShopSessionState State { get; private set; }
        public readonly ShopOffer[] Offers = new ShopOffer[3];

        readonly Dictionary<string, TraitCardInstance> _cards =
            new Dictionary<string, TraitCardInstance>();
        readonly Dictionary<string, CommanderLoadoutSnapshot> _loadouts =
            new Dictionary<string, CommanderLoadoutSnapshot>();
        readonly List<string> _inventoryIds = new List<string>();
        readonly List<string> _activeCommanderIds = new List<string>();
        readonly HashSet<string> _activeCommanderIdSet = new HashSet<string>();
        readonly TraitOfferRollContext _rollContext;
        int _nextInstanceOrdinal;

        public IReadOnlyList<string> ActiveCommanderIds => _activeCommanderIds;

        public void GetOwnedCardIds(List<string> output)
        {
            if (output == null) return;
            output.Clear();
            foreach (var pair in _cards)
                if (pair.Value.Location == TraitCardLocation.Inventory ||
                    pair.Value.Location == TraitCardLocation.Equipped &&
                    IsActiveCommander(pair.Value.EquippedCommanderId))
                    output.Add(pair.Key);
            output.Sort(StringComparer.Ordinal);
        }

        public SettlementShopSession(string sessionId, string rewardGrantId, int seed,
            int rewardGold, IEnumerable<string> activeCommanderIds,
            PlayerProgressionSnapshot progression,
            TraitOfferRollContext rollContext = null)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            _rollContext = rollContext ?? new TraitOfferRollContext();
            SessionId = sessionId;
            RewardGrantId = rewardGrantId;
            RandomSeed = seed;
            BaseProgressionVersion = progression.Version;
            RewardGold = Math.Max(0, rewardGold);
            Gold = progression.Gold + RewardGold;
            if (activeCommanderIds != null)
            {
                foreach (var commanderId in activeCommanderIds)
                {
                    if (string.IsNullOrEmpty(commanderId) || !_activeCommanderIdSet.Add(commanderId))
                        continue;
                    _activeCommanderIds.Add(commanderId);
                }
            }
            for (int i = 0; i < progression.TraitCards.Count; i++)
            {
                var card = progression.TraitCards[i].Clone();
                _cards[card.InstanceId] = card;
                if (card.Location == TraitCardLocation.Inventory) _inventoryIds.Add(card.InstanceId);
            }
            for (int i = 0; i < progression.CommanderLoadouts.Count; i++)
            {
                var loadout = progression.CommanderLoadouts[i].Clone();
                _loadouts[loadout.CommanderId] = loadout;
            }
            State = ShopSessionState.Ready;
            RollOffers();
        }

        public TraitCardInstance GetCard(string instanceId)
        {
            TraitCardInstance card;
            return _cards.TryGetValue(instanceId, out card) ? card : null;
        }

        public CommanderLoadoutSnapshot GetLoadout(string commanderId)
        {
            CommanderLoadoutSnapshot loadout;
            return _loadouts.TryGetValue(commanderId, out loadout) ? loadout : null;
        }

        public ShopOperationResult Buy(int offerIndex, ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            if (offerIndex < 0 || offerIndex >= Offers.Length || Offers[offerIndex] == null)
                return ShopOperationResult.NotFound;
            var offer = Offers[offerIndex];
            if (offer.Purchased) return ShopOperationResult.AlreadyPurchased;
            if (Gold < offer.Price) return ShopOperationResult.InsufficientGold;

            Gold -= offer.Price;
            offer.Purchased = true;
            var card = new TraitCardInstance
            {
                InstanceId = SessionId + "_card_" + (++_nextInstanceOrdinal),
                DefinitionId = offer.DefinitionId,
                Location = TraitCardLocation.Inventory
            };
            _cards.Add(card.InstanceId, card);
            _inventoryIds.Add(card.InstanceId);
            MarkChanged(changes, card.InstanceId, null, offerIndex, true, true);
            return ShopOperationResult.Success;
        }

        public ShopOperationResult Refresh(int price, ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            if (price < 0 || Gold < price) return ShopOperationResult.InsufficientGold;
            Gold -= price;
            RefreshCount++;
            RollOffers();
            changes.Clear();
            changes.GoldChanged = true;
            for (int i = 0; i < Offers.Length; i++) changes.ChangedOfferIndices.Add(i);
            DirtyVersion++;
            return ShopOperationResult.Success;
        }

        public ShopOperationResult Equip(string instanceId, string commanderId, int slot,
            ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            if (!IsActiveCommander(commanderId)) return ShopOperationResult.InvalidOwnership;
            var card = GetCard(instanceId);
            var loadout = GetLoadout(commanderId);
            if (card == null || loadout == null) return ShopOperationResult.NotFound;
            if (card.Location != TraitCardLocation.Inventory) return ShopOperationResult.InvalidOwnership;
            if (slot < 0 || slot >= 4) return ShopOperationResult.InvalidSlot;
            var definition = TraitCatalog.Get(card.DefinitionId);
            if (definition == null) return ShopOperationResult.NotFound;
            if (definition.StackPolicy == TraitStackPolicy.UniquePerCommander &&
                HasDefinitionEquipped(loadout, card.DefinitionId, slot))
                return ShopOperationResult.DuplicateTrait;

            string replacedId = loadout.TraitCardInstanceIds[slot];
            if (!string.IsNullOrEmpty(replacedId))
            {
                var replaced = GetCard(replacedId);
                if (replaced != null)
                {
                    replaced.Location = TraitCardLocation.Inventory;
                    replaced.EquippedCommanderId = null;
                    replaced.EquippedSlotIndex = -1;
                    _inventoryIds.Add(replaced.InstanceId);
                    changes.ChangedInstanceIds.Add(replaced.InstanceId);
                }
            }

            _inventoryIds.Remove(instanceId);
            card.Location = TraitCardLocation.Equipped;
            card.EquippedCommanderId = commanderId;
            card.EquippedSlotIndex = slot;
            loadout.TraitCardInstanceIds[slot] = instanceId;
            MarkChanged(changes, instanceId, commanderId, -1, false, true);
            return ShopOperationResult.Success;
        }

        public ShopOperationResult Unequip(string commanderId, int slot, ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            if (!IsActiveCommander(commanderId)) return ShopOperationResult.InvalidOwnership;
            var loadout = GetLoadout(commanderId);
            if (loadout == null) return ShopOperationResult.NotFound;
            if (slot < 0 || slot >= 4) return ShopOperationResult.InvalidSlot;
            string instanceId = loadout.TraitCardInstanceIds[slot];
            var card = GetCard(instanceId);
            if (card == null || card.Location != TraitCardLocation.Equipped)
                return ShopOperationResult.InvalidOwnership;
            loadout.TraitCardInstanceIds[slot] = null;
            card.Location = TraitCardLocation.Inventory;
            card.EquippedCommanderId = null;
            card.EquippedSlotIndex = -1;
            _inventoryIds.Add(instanceId);
            MarkChanged(changes, instanceId, commanderId, -1, false, true);
            return ShopOperationResult.Success;
        }

        public ShopOperationResult MoveEquippedCard(string instanceId, string sourceCommanderId,
            int sourceSlot, string targetCommanderId, int targetSlot, ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            if (!IsActiveCommander(sourceCommanderId) || !IsActiveCommander(targetCommanderId))
                return ShopOperationResult.InvalidOwnership;
            if (sourceSlot < 0 || sourceSlot >= 4 || targetSlot < 0 || targetSlot >= 4)
                return ShopOperationResult.InvalidSlot;

            var sourceLoadout = GetLoadout(sourceCommanderId);
            var targetLoadout = GetLoadout(targetCommanderId);
            var card = GetCard(instanceId);
            if (sourceLoadout == null || targetLoadout == null || card == null)
                return ShopOperationResult.NotFound;
            if (sourceLoadout.TraitCardInstanceIds[sourceSlot] != instanceId ||
                card.Location != TraitCardLocation.Equipped ||
                card.EquippedCommanderId != sourceCommanderId ||
                card.EquippedSlotIndex != sourceSlot)
                return ShopOperationResult.InvalidOwnership;
            if (sourceCommanderId == targetCommanderId && sourceSlot == targetSlot)
                return ShopOperationResult.Success;

            var definition = TraitCatalog.Get(card.DefinitionId);
            if (definition == null) return ShopOperationResult.NotFound;
            if (definition.StackPolicy == TraitStackPolicy.UniquePerCommander &&
                HasDefinitionEquippedExcept(targetLoadout, card.DefinitionId, instanceId, targetSlot))
                return ShopOperationResult.DuplicateTrait;

            string replacedId = targetLoadout.TraitCardInstanceIds[targetSlot];
            var replaced = string.IsNullOrEmpty(replacedId) ? null : GetCard(replacedId);
            if (!string.IsNullOrEmpty(replacedId) && replaced == null)
                return ShopOperationResult.InvalidOwnership;

            sourceLoadout.TraitCardInstanceIds[sourceSlot] = null;
            if (replaced != null)
            {
                replaced.Location = TraitCardLocation.Inventory;
                replaced.EquippedCommanderId = null;
                replaced.EquippedSlotIndex = -1;
                _inventoryIds.Add(replaced.InstanceId);
            }
            targetLoadout.TraitCardInstanceIds[targetSlot] = instanceId;
            card.EquippedCommanderId = targetCommanderId;
            card.EquippedSlotIndex = targetSlot;

            changes.Clear();
            changes.InventoryOrderDirty = replaced != null;
            changes.ChangedInstanceIds.Add(instanceId);
            if (replaced != null) changes.ChangedInstanceIds.Add(replaced.InstanceId);
            changes.ChangedCommanderIds.Add(sourceCommanderId);
            if (targetCommanderId != sourceCommanderId)
                changes.ChangedCommanderIds.Add(targetCommanderId);
            DirtyVersion++;
            return ShopOperationResult.Success;
        }

        public ShopOperationResult Sell(string instanceId, ShopChangeSet changes)
        {
            if (!CanWrite()) return ShopOperationResult.InvalidState;
            var card = GetCard(instanceId);
            if (card == null) return ShopOperationResult.NotFound;
            if (card.Location != TraitCardLocation.Inventory) return ShopOperationResult.InvalidOwnership;
            var definition = TraitCatalog.Get(card.DefinitionId);
            if (definition == null) return ShopOperationResult.NotFound;
            Gold += definition.SellPrice;
            _inventoryIds.Remove(instanceId);
            card.Location = TraitCardLocation.Sold;
            MarkChanged(changes, instanceId, null, -1, true, true);
            return ShopOperationResult.Success;
        }

        public bool TryBuildCommitSnapshot(out PlayerProgressionSnapshot snapshot)
        {
            snapshot = null;
            if (!CanWrite()) return false;
            RepairIndexes();
            string integrityError;
            if (!ValidateIntegrity(out integrityError))
            {
                UnityEngine.Debug.LogError("[SettlementShop] Commit integrity failed: " + integrityError);
                return false;
            }
            State = ShopSessionState.Committing;
            snapshot = new PlayerProgressionSnapshot { Version = BaseProgressionVersion, Gold = Gold };
            foreach (var pair in _cards)
                if (pair.Value.Location != TraitCardLocation.Sold)
                    snapshot.TraitCards.Add(pair.Value.Clone());
            foreach (var pair in _loadouts) snapshot.CommanderLoadouts.Add(pair.Value.Clone());
            return true;
        }

        public void FinishCommit(bool success)
        {
            State = success ? ShopSessionState.Committed : ShopSessionState.Ready;
        }

        public void Suspend()
        {
            if (State == ShopSessionState.Ready) State = ShopSessionState.Suspended;
        }

        public void Resume()
        {
            if (State == ShopSessionState.Suspended) State = ShopSessionState.Ready;
        }

        bool CanWrite() => State == ShopSessionState.Ready;
        public bool IsActiveCommander(string commanderId) =>
            !string.IsNullOrEmpty(commanderId) && _activeCommanderIdSet.Contains(commanderId);

        void RollOffers()
        {
            _rollContext.RefreshCount = RefreshCount;
            RecomputeOwnedTags();
            var rolled = TraitShopDirector.Roll(RandomSeed + RefreshCount * 7919,
                _rollContext, TraitCatalog.Definitions, Offers.Length);
            for (int i = 0; i < Offers.Length; i++)
            {
                var definition = i < rolled.Length ? rolled[i] : null;
                Offers[i] = definition == null ? null : new ShopOffer
                {
                    DefinitionId = definition.Id,
                    Price = definition.BuyPrice,
                    Purchased = false
                };
            }
        }

        void RecomputeOwnedTags()
        {
            _rollContext.OwnedTraitTags.Clear();
            foreach (var pair in _cards)
            {
                var card = pair.Value;
                if (card == null ||
                    (card.Location != TraitCardLocation.Inventory &&
                     card.Location != TraitCardLocation.Equipped))
                    continue;
                var def = TraitCatalog.Get(card.DefinitionId);
                if (def == null || def.Tags == null) continue;
                for (int t = 0; t < def.Tags.Count; t++)
                    if (!string.IsNullOrEmpty(def.Tags[t]))
                        _rollContext.OwnedTraitTags.Add(def.Tags[t]);
            }
        }

        bool HasDefinitionEquipped(CommanderLoadoutSnapshot loadout, string definitionId,
            int ignoredSlot)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == ignoredSlot) continue;
                var card = GetCard(loadout.TraitCardInstanceIds[i]);
                if (card != null && card.DefinitionId == definitionId) return true;
            }
            return false;
        }

        bool HasDefinitionEquippedExcept(CommanderLoadoutSnapshot loadout, string definitionId,
            string ignoredInstanceId, int ignoredSlot)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == ignoredSlot) continue;
                string instanceId = loadout.TraitCardInstanceIds[i];
                if (instanceId == ignoredInstanceId) continue;
                var equipped = GetCard(instanceId);
                if (equipped != null && equipped.DefinitionId == definitionId) return true;
            }
            return false;
        }

        void RepairIndexes()
        {
            _inventoryIds.Clear();
            foreach (var pair in _cards)
            {
                var card = pair.Value;
                if (card.Location == TraitCardLocation.Inventory)
                {
                    card.EquippedCommanderId = null;
                    card.EquippedSlotIndex = -1;
                    _inventoryIds.Add(card.InstanceId);
                }
            }
        }

        bool ValidateIntegrity(out string error)
        {
            error = null;
            if (Gold < 0) { error = "negative gold"; return false; }
            var equipped = new HashSet<string>();
            foreach (var pair in _loadouts)
            for (int slot = 0; slot < 4; slot++)
            {
                string id = pair.Value.TraitCardInstanceIds[slot];
                if (string.IsNullOrEmpty(id)) continue;
                var card = GetCard(id);
                if (card == null) { error = "missing equipped card " + id; return false; }
                if (!equipped.Add(id)) { error = "card equipped more than once " + id; return false; }
                if (card.Location != TraitCardLocation.Equipped ||
                    card.EquippedCommanderId != pair.Key || card.EquippedSlotIndex != slot)
                { error = "equipped ownership mismatch " + id; return false; }
            }
            for (int i = 0; i < _inventoryIds.Count; i++)
            {
                var card = GetCard(_inventoryIds[i]);
                if (card == null || card.Location != TraitCardLocation.Inventory)
                { error = "invalid inventory card " + _inventoryIds[i]; return false; }
            }
            return true;
        }

        void MarkChanged(ShopChangeSet changes, string instanceId, string commanderId,
            int offerIndex, bool gold, bool orderDirty)
        {
            changes.Clear();
            changes.GoldChanged = gold;
            changes.InventoryOrderDirty = orderDirty;
            if (!string.IsNullOrEmpty(instanceId)) changes.ChangedInstanceIds.Add(instanceId);
            if (!string.IsNullOrEmpty(commanderId)) changes.ChangedCommanderIds.Add(commanderId);
            if (offerIndex >= 0) changes.ChangedOfferIndices.Add(offerIndex);
            DirtyVersion++;
        }
    }
}
