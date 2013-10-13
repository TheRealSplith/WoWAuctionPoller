using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoWAuctionPoller.Test
{
    [TestClass]
    public class WoWAuctionPollerTest
    {
        [TestMethod]
        public void AuctionMetaDataConnection()
        {
            WoWAuctionPoller poller = new WoWAuctionPoller()
            {
                BaseAPI = "http://us.battle.net/api/wow",
            };

            poller.QueryAuctions("bonechewer", new System.Collections.Generic.List<String>() { "neutral" });
        }
    }
}
