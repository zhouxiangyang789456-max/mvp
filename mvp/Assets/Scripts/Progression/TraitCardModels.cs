using System;
using System.Collections.Generic;

namespace Mvp.Progression
{
    public enum TraitRarity { Common, Rare, Epic, Legendary }
    public enum TraitStackPolicy { UniquePerCommander, Stackable }
    public enum TraitCardLocation { Inventory, Equipped, Sold }

    [Serializable]
    public sealed class TraitCardDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string IconAssetId;
        public TraitRarity Rarity;
        public int BuyPrice;
        public int SellPrice;
        public TraitStackPolicy StackPolicy;
        public List<TraitEffect> Effects = new List<TraitEffect>();
        public List<string> Tags = new List<string>();
    }

    [Serializable]
    public sealed class TraitCardInstance
    {
        public string InstanceId;
        public string DefinitionId;
        public TraitCardLocation Location;
        public string EquippedCommanderId;
        public int EquippedSlotIndex = -1;

        public TraitCardInstance Clone()
        {
            return (TraitCardInstance)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CommanderLoadoutSnapshot
    {
        public string CommanderId;
        public readonly string[] TraitCardInstanceIds = new string[4];

        public CommanderLoadoutSnapshot Clone()
        {
            var clone = new CommanderLoadoutSnapshot { CommanderId = CommanderId };
            Array.Copy(TraitCardInstanceIds, clone.TraitCardInstanceIds, 4);
            return clone;
        }
    }

    [Serializable]
    public sealed class PlayerProgressionSnapshot
    {
        public int Version;
        public int Gold;
        public readonly List<TraitCardInstance> TraitCards = new List<TraitCardInstance>();
        public readonly List<CommanderLoadoutSnapshot> CommanderLoadouts =
            new List<CommanderLoadoutSnapshot>();

        public PlayerProgressionSnapshot Clone()
        {
            var clone = new PlayerProgressionSnapshot { Version = Version, Gold = Gold };
            for (int i = 0; i < TraitCards.Count; i++) clone.TraitCards.Add(TraitCards[i].Clone());
            for (int i = 0; i < CommanderLoadouts.Count; i++)
                clone.CommanderLoadouts.Add(CommanderLoadouts[i].Clone());
            return clone;
        }
    }
}
