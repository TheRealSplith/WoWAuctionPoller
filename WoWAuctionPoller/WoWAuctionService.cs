using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Configuration;

namespace WoWAuctionPoller
{
    public partial class WoWAuctionService : ServiceBase
    {
        Timer timer;
        WoWAuctionPoller poller;

        public static String SOURCE = "WoWAuctionPoller";
        public static String LOG = "Application";
        public WoWAuctionService()
        {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 4 * 60 * 60 * 1000;

            poller = new WoWAuctionPoller()
            {
                BaseAPI = "http://us.battle.net/api/wow",
            };
            // Then we set the timer for more updates
            timer.Elapsed += timer_Elapsed;
            EventLog.Source = "WoWAuctionPoller";

            // Build event log here, so I don't have to dont have to check all the time
            if (!EventLog.SourceExists(SOURCE))
                EventLog.CreateEventSource(SOURCE, LOG);
        }

        protected override void OnStart(string[] args)
        {
            timer.Start();
            // Initial check when service starts
            timer_Elapsed(null, null); // Everything is loaded from Config
        }

        /// <summary>
        /// This runs through the poller operation which collects data from the WoW auction API, parses it into .net objects
        /// and then adds it to database. Exceptions are caught, but recorded in event viewer, as well as diagnostics.
        /// </summary>
        /// <param name="sender">Totally Junk</param>
        /// <param name="e">Even Junkier</param>
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                // Inform the routine is starting so we know the state
                EventLog.WriteEntry(
                    SOURCE,
                    "Data poll starting",
                    EventLogEntryType.Information
                );
                // Actually poll data
                sw.Start();
                poller.QueryAuctions(ConfigurationManager.AppSettings["server"], ConfigurationManager.AppSettings["factions"].Split(','));
                sw.Stop();

                // Record runtime for diagnostic purposes
                EventLog.WriteEntry(
                    SOURCE,
                    String.Format("Minutes:{0}", sw.Elapsed.TotalMinutes),
                    EventLogEntryType.Information
                );
            }
            catch(Exception ex)
            {
                sw.Stop();

                // Log Error
                EventLog.WriteEntry(
                    SOURCE,
                    String.Format("Message:{0}\nStack:{1}", ex.Message, ex.StackTrace),
                    EventLogEntryType.Error
                );
            }
            finally
            {
                sw.Reset();
            }
        }

        protected override void OnStop()
        {
            timer.Stop();
        }
    }
}
