using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.SqlClient;

namespace WoWAuctionPoller.PerformanceShell
{
    class Program
    {
        static void Main(string[] args)
        {
            //pollerTest(args);
            GetMissingItems(args);
        }

        private static void GetMissingItems(string[] args)
        {
            using (SqlConnection sqlConn = new SqlConnection("Server=SPLITHPC;Database=master;Trusted_Connection=SSPI;"))
            {
                sqlConn.Open();
                SqlCommand sqlComm = sqlConn.CreateCommand();

                sqlComm.CommandText =
    @"SELECT i.ID
  FROM [WoWAuctionData].[dbo].[Items] i
  WHERE i.ID NOT IN (SELECT ID FROM [WoWAuctionDataTest].[dbo].[Items])";

                WoWAuctionContext wac = new WoWAuctionContext();
                var reader = sqlComm.ExecuteReader();

                while (reader.Read())
                {
                    Int32 itemID = (Int32)reader.GetValue(0);

                    var poller = new WoWAuctionPoller()
                    {
                        BaseAPI = "http://us.battle.net/api/wow",
                    };

                    var currentItem = wac.Items.Create();
                    poller.PollItem(itemID, ref currentItem);

                    wac.Items.Add(currentItem);
                }

                wac.SaveChanges();
            }
        }

        private static void pollerTest (string[] args)
        {
            WoWAuctionPoller poller = new WoWAuctionPoller()
            {
                BaseAPI = "http://us.battle.net/api/wow",
            };

            Debug.WriteLine("-- Starting Exerices --");

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            poller.QueryAuctions(ConfigurationManager.AppSettings["server"], ConfigurationManager.AppSettings["factions"].Split(','));
            stopwatch.Stop();
            Debug.WriteLine(
                String.Format("Operation took Hour:{0}, Minute:{1}, Second:{2}",
                stopwatch.Elapsed.Hours,
                stopwatch.Elapsed.Minutes,
                stopwatch.Elapsed.Seconds
            ));
        }
    }
}
