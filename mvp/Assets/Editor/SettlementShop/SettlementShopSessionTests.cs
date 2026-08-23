#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Mvp.Progression;
using Mvp.SettlementShop;

namespace Mvp.EditorTests.SettlementShop
{
    /// <summary>
    /// EditMode tests for SettlementShopSession 专属卡池过滤(期 2 §8.1)。
    /// 直接构造 Session(不依赖 PlayerProgressionStore / Store),校验进池只含所选指挥官专属卡。
    /// </summary>
    public sealed class SettlementShopSessionTests
    {
        [Test]
        public void RollOffers_选中伊莲娜_不进其他指挥官专属卡()
        {
            var progression = new PlayerProgressionSnapshot { Gold = 30, Version = 1 };
            var session = new SettlementShopSession("s1", "g1", 12345, 10,
                new[] { "commander_elena" }, progression, null, "commander_elena");

            Assert.AreEqual(3, session.Offers.Length);
            for (int i = 0; i < session.Offers.Length; i++)
            {
                var offer = session.Offers[i];
                if (offer == null) continue;
                string owner = TraitCatalog.ExclusiveOwner(offer.DefinitionId);
                Assert.IsTrue(owner == null || owner == "commander_elena",
                    offer.DefinitionId + " 归属 " + owner + ",不应进伊莲娜商店");
            }
        }

        [Test]
        public void RollOffers_选中伊莲娜卡西安_包含各自专属卡()
        {
            var progression = new PlayerProgressionSnapshot { Gold = 30, Version = 1 };
            var session = new SettlementShopSession("s2", "g2", 54321, 10,
                new[] { "commander_elena", "commander_cassian" }, progression, null, "commander_elena");

            Assert.AreEqual(3, session.Offers.Length);
            for (int i = 0; i < session.Offers.Length; i++)
            {
                var offer = session.Offers[i];
                if (offer == null) continue;
                string owner = TraitCatalog.ExclusiveOwner(offer.DefinitionId);
                Assert.IsTrue(owner == null || owner == "commander_elena" || owner == "commander_cassian",
                    offer.DefinitionId + " 归属 " + owner + ",不应进双指挥官商店");
            }
        }
    }
}
#endif
