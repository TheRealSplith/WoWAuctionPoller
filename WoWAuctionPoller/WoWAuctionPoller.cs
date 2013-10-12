using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WoWAuctionPoller
{
    public class WoWAuctionContext : DbContext
    {
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<AuctionHouse> AuctionHouse { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
    public class WoWAuctionPoller
    {
        public String BaseAPI { get; set; }
        public String connString { get; set; }
        public void QueryAuctions(String Realm, String Faction)
        {
            // Get URL and time stamp
            var web = new System.Net.WebClient();
            var MetaAuctionJSON = web.DownloadString(
                String.Format("{0}/auction/data/{1}", BaseAPI, Realm));
            var MetaAuctionData = JObject.Parse(MetaAuctionJSON);
            var AuctionURL = (String)MetaAuctionData["files"]["url"];
            var TimeStamp = (Int64)MetaAuctionData["files"]["lastModified"];

            WoWAuctionContext context = new WoWAuctionContext();
            // Create new AH if possible
            AuctionHouse auctionHouse;
            if (context.AuctionHouse.Any(ah => ah.Faction == Faction && ah.Realm == Realm))
                auctionHouse = context.AuctionHouse.First(ah => ah.Faction == Faction && ah.Realm == Realm);
            else
            {
                auctionHouse = context.AuctionHouse.Create();
                auctionHouse.Realm = Realm;
                auctionHouse.Faction = Faction;
                context.AuctionHouse.Add(auctionHouse);

                context.SaveChanges();
            }

            // Get Auction Data and parse it
            var AuctionJSON = web.DownloadString(AuctionURL);
            var AuctionData = JObject.Parse(AuctionJSON);
            // For each auction
            foreach (var item in ((JArray)AuctionData[Faction]["auctions"]).Children<JObject>())
            {
                if (!context.Auctions.Any(a => a.AucID == (Int64)item["auc"]))
                {
                    // Create auction
                    var newAuction = context.Auctions.Create();
                    newAuction.AucID = (Int64)item["auc"];
                    newAuction.ItemID = (Int32)item["item"];
                    newAuction.Quanity = (Int32)item["quantity"];
                    newAuction.Bid = (Int64)item["bid"];
                    newAuction.Buyout = (Int64)item["buyout"];
                    newAuction.MyAuctionHouse = auctionHouse;
                    newAuction.TimeStamp = TimeStamp;

                    // Create Item if necessary
                    if (!context.Items.Any(i => i.ItemID == newAuction.ItemID))
                    {
                        var newItem = context.Items.Create();
                        var newItemJSON = web.DownloadString(
                            String.Format("{0}/item/{1}", BaseAPI, newAuction.ItemID));
                    }

                    context.Auctions.Add(newAuction);
                }
            }

            context.SaveChanges();
        }
    }

    public class Auction
    {
        [Key]
        public Int32 ID { get; set; }
        public Int64 AucID { get; set; }
        public Int64 TimeStamp { get; set; }
        public Int32 ItemID { get; set; }
        public virtual Item MyItem { get; set; }
        public Int32 AuctionHouseID { get; set; }
        public virtual AuctionHouse MyAuctionHouse { get; set; }
        public Int64 Bid { get; set; }
        public Int64 Buyout { get; set; }
        public Int32 Quanity { get; set;}
    }

    public class AuctionHouse
    {
        public Int32 ID { get; set; }
        public String Realm { get; set; }
        public String Faction { get; set; }
        public IList<Auction> Auctions { get; set; }
    }

    public class Item
    {
        [Key]
        public Int32 ID {get;set;}
        public Int32 ItemID {get;set;}
        public String Name {get;set;}
    }
}
