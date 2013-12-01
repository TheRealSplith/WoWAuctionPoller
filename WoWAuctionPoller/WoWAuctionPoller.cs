using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

namespace WoWAuctionPoller
{
    public class WoWAuctionContext : DbContext
    {
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<AuctionHouse> AuctionHouse { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuctionHouse>()
                .HasMany(ah => ah.Auctions)
                .WithOptional()
                .HasForeignKey(a => a.AuctionHouseID);

            modelBuilder.Entity<Auction>()
                .HasOptional(a => a.MyAuctionHouse)
                .WithMany()
                .HasForeignKey(a => a.AuctionHouseID);

            modelBuilder.Entity<Auction>()
                .HasOptional(a => a.MyItem)
                .WithMany()
                .HasForeignKey(a => a.ItemID);

            modelBuilder.Entity<Item>().Property(i => i.ID)
                .HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);
        }
    }
    public class WoWAuctionPoller
    {
        public static DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0);
        public String BaseAPI { get; set; }
        public void QueryAuctions(String Realm, IEnumerable<String> Factions)
        {
            // Web client is used for big download
            var web = new System.Net.WebClient();

            String AuctionJSON = String.Empty; // This holds JSON result of auctions
            Int64 TimeStamp = 0; // This holds the time stamp
            Int32 MaxAttempts = 30; // How many tries to get data before we give up
            Int32 count = 1; // Which attempt is this?
            while (AuctionJSON == String.Empty)
            {
                try
                {
                    var MetaAuctionJSON = web.DownloadString(
                        String.Format("{0}/auction/data/{1}", BaseAPI, Realm));
                    var MetaAuctionData = JObject.Parse(MetaAuctionJSON);
                    var AuctionURL = (String)MetaAuctionData["files"][0]["url"];
                    TimeStamp = (Int64)MetaAuctionData["files"][0]["lastModified"];

                    // Get Auction Data and parse it
                    AuctionJSON = web.DownloadString(AuctionURL);
                }
                catch (Exception ex)
                {
                    // Burn a little time, let blizzard get it together
                    System.Threading.Thread.Sleep(50);
                    if (count < MaxAttempts)
                        count++;
                    else
                        throw ex;
                }
            }

            // Create new AH if necessary
            Parallel.ForEach(Factions, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (fac) =>
                {
                    JObject AuctionData;
                    lock (AuctionJSON)
                    {
                        AuctionData = JObject.Parse(AuctionJSON);
                    }
                    ParseData(Realm, fac, TimeStamp, AuctionData);
                }
            );
        }

        private void ParseData(String realm, String faction, Int64 TimeStamp, JObject auctionData)
        {
            using (var context = new WoWAuctionContext())
            {
                // Pure performance!
                context.Configuration.AutoDetectChangesEnabled = false;

                AuctionHouse auctionHouse;
                if (context.AuctionHouse.Any(ah => ah.Faction == faction && ah.Realm == realm))
                    auctionHouse = context.AuctionHouse.First(ah => ah.Faction == faction && ah.Realm == realm);
                else
                {
                    auctionHouse = context.AuctionHouse.Create();
                    auctionHouse.Realm = realm;
                    auctionHouse.Faction = faction;
                    context.AuctionHouse.Add(auctionHouse);

                    context.SaveChanges();
                }
                foreach (var item in ((JArray)auctionData[faction]["auctions"]))
                {
                    try
                    {
                        // Create auction
                        var newAuction = context.Auctions.Create();
                        newAuction.AucID = (Int64)item["auc"];
                        newAuction.ItemID = (Int32)item["item"];
                        newAuction.Quanity = (Int32)item["quantity"];
                        newAuction.Bid = (Int64)item["bid"];
                        newAuction.Buyout = (Int64)item["buyout"];
                        newAuction.MyAuctionHouse = auctionHouse;
                        newAuction.TimeStamp = UNIX_EPOCH.AddMilliseconds(TimeStamp);

                        // Create Item if necessary
                        if (!context.Items.Any(i => i.ID == newAuction.ItemID))
                        {
                            var newItem = context.Items.Create();

                            this.PollItem(newAuction.ItemID.Value, ref newItem);

                            context.Items.Add(newItem);
                            context.SaveChanges();
                        }
                        Debug.WriteLine(String.Format("New Auction:{0}", newAuction.AucID));
                        context.Auctions.Add(newAuction);
                    }
                    catch (Exception ex)
                    {
                        // Log Error
                        EventLog.WriteEntry(
                            WoWAuctionService.SOURCE,
                            String.Format("Message:{0}\nStack:{1}", ex.Message, ex.StackTrace),
                            EventLogEntryType.Warning
                        );
                    }
                }
                context.SaveChanges();
            }
        }

        public void PollItem(Int32 itemID, ref Item newItem)
        {
            var ItemUrl = String.Format("{0}/item/{1}", this.BaseAPI, itemID);
            String newItemJSON = String.Empty;

            Int32 MaxAttempts = 3; // How many tries to get data before we give up
            Int32 count = 1; // Which attempt is this?
            while (newItemJSON == String.Empty)
            {
                try
                {
                    // Get item data
                    WebClient web = new WebClient();
                    newItemJSON = web.DownloadString(ItemUrl);
                }
                catch (Exception ex)
                {
                    if (count < MaxAttempts)
                        count++;
                    else
                        throw ex;
                }
            }

            var newItemData = JObject.Parse(newItemJSON);

            newItem.ID = (Int32)newItemData["id"];
            newItem.Name = (String)newItemData["name"];
            newItem.Description = (String)newItemData["description"];
            newItem.ItemLevel = (Int32)newItemData["itemLevel"];
            newItem.Stackable = (Int32)newItemData["stackable"];
            newItem.VendorBuyPrice = (Int64)newItemData["buyPrice"];
            newItem.VendorSellPrice = (Int64)newItemData["sellPrice"];
            newItem.ItemClass = (Int32)newItemData["itemClass"];
            newItem.ItemSubClass = (Int32)newItemData["itemSubClass"];
            newItem.Quality = (Int32)newItemData["quality"];
            newItem.RequiredSkillRank = (Int32)newItemData["requiredSkillRank"];
        }
    }

    public class Auction
    {
        [Key]
        public Int32 ID { get; set; }
        public Int64 AucID { get; set; }
        public DateTime TimeStamp { get; set; }
        public Int32? ItemID { get; set; }
        public virtual Item MyItem { get; set; }
        public Int32? AuctionHouseID { get; set; }
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
        public virtual IList<Auction> Auctions { get; set; }
    }

    public class Item
    {
        [Key]
        public Int32 ID { get; set; }
        public String Name { get; set; }
        public String Description { get; set; }
        public Int32 ItemLevel { get; set; }
        public Int32 Stackable { get; set; }
        public Int64 VendorBuyPrice { get; set; }
        public Int64 VendorSellPrice { get; set; }
        public Int32 ItemClass { get; set; }
        public Int32 ItemSubClass { get; set; }
        public Int32 Quality { get; set; }
        public Int32 RequiredSkillRank { get; set; }
    }
}
