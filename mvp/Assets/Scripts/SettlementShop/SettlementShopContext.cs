namespace Mvp.SettlementShop
{
    public static class SettlementShopContext
    {
        public static SettlementShopOpenArgs PendingOpenArgs;

        public static SettlementShopOpenArgs ConsumeOrCreateDefault()
        {
            var args = PendingOpenArgs;
            PendingOpenArgs = null;
            if (args != null) return args;
            var fallback = new SettlementShopOpenArgs
            {
                SessionId = "settlement_debug",
                RewardGrantId = "reward_debug",
                RandomSeed = 1729,
                RewardGold = 10
            };
            var commanders = Mvp.CommanderSelect.CommanderCatalog.GetAll();
            if (commanders.Count > 0) fallback.ActiveCommanderIds.Add(commanders[0].Id);
            UnityEngine.Debug.LogWarning("[SettlementShop] Missing open context; using first commander as debug fallback.");
            return fallback;
        }
    }
}
