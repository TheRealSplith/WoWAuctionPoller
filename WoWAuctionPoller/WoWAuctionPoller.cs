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
        public String BaseAPI { get; set; }
        public void QueryAuctions(String Realm, IEnumerable<String> Factions)
        {
            // Web client is used for big download
            var web = new System.Net.WebClient();

            String AuctionJSON = String.Empty; // This holds JSON result of auctions
            Int64 TimeStamp = 0; // This holds the time stamp
            Int32 MaxAttempts = 3; // How many tries to get data before we give up
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
                    if (count < MaxAttempts)
                        count++;
                    else
                        throw ex;
                }
            }
            var AuctionData = JObject.Parse(AuctionJSON);

            WoWAuctionContext context = new WoWAuctionContext();
            // Create new AH if necessary
            foreach (var Faction in Factions)
            {
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

                // For each auction
                foreach (var item in ((JArray)AuctionData[Faction]["auctions"]))
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
                    if (!context.Items.Any(i => i.ID == newAuction.ItemID))
                    {
                        var newItem = context.Items.Create();
                        var ItemUrl = String.Format("{0}/item/{1}", BaseAPI, newAuction.ItemID);
                        String newItemJSON = String.Empty;

                        MaxAttempts = 3; // How many tries to get data before we give up
                        count = 1; // Which attempt is this?
                        while (newItemJSON == String.Empty)
                        {
                            try
                            {
                                // Get item data
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

                        Debug.WriteLine(String.Format("New Item:{0}", newItem.ID));
                        context.Items.Add(newItem);
                        context.SaveChanges();
                    }
                    Debug.WriteLine(String.Format("New Auction:{0}", newAuction.AucID));
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
        public Int32 ID {get;set;}
        public String Name {get;set;}
    }
}
