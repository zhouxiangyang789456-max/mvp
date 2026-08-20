using System.Collections.Generic;

namespace Mvp.Progression
{
    public static class TraitCatalog
    {
        static readonly TraitCardDefinition[] All =
        {
            Make("trait_brave", "勇敢", "生命低于 35% 时，攻击力提高 20%。", TraitRarity.Common, 5, 2),
            Make("trait_cautious", "谨慎", "首次受到攻击后，短时间降低所受伤害。", TraitRarity.Common, 5, 2),
            Make("trait_inspiring", "鼓舞", "编队成员生命高于 70% 时，提高移动速度。", TraitRarity.Rare, 7, 3),
            Make("trait_tenacious", "坚韧", "编队成员生命上限提高 10%。", TraitRarity.Rare, 7, 3),
            Make("trait_disciplined", "纪律", "保持完整阵型时，提高攻击速度。", TraitRarity.Epic, 9, 4),
            Make("trait_calm", "沉着", "受到攻击后短时间提高命中稳定性。", TraitRarity.Common, 5, 2),
            Make("trait_guard", "守备", "停止移动时降低所受远程伤害。", TraitRarity.Rare, 7, 3),
            Make("trait_hold", "坚守", "阵型未被打乱时提高防御力。", TraitRarity.Epic, 9, 4),
            Make("trait_reflection", "回光反照", "单位剩余 5% 生命时，攻击力提高 100%。", TraitRarity.Epic, 10, 5)
        };

        public static IReadOnlyList<TraitCardDefinition> Definitions => All;

        public static TraitCardDefinition Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static TraitCardDefinition FindByDisplayName(string name)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].DisplayName == name) return All[i];
            return null;
        }

        static TraitCardDefinition Make(string id, string name, string description,
            TraitRarity rarity, int buy, int sell)
        {
            return new TraitCardDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                IconAssetId = "Battle/UI/Traits/" + id,
                Rarity = rarity,
                BuyPrice = buy,
                SellPrice = sell,
                StackPolicy = TraitStackPolicy.UniquePerCommander
            };
        }
    }
}
