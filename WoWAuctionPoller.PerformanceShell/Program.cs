using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace WoWAuctionPoller.PerformanceShell
{
    class Program
    {
        static void Main(string[] args)
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
