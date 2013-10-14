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
        public WoWAuctionService()
        {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 60 * 60 * 1000;
        }

        protected override void OnStart(string[] args)
        {
            poller = new WoWAuctionPoller()
            {
                BaseAPI = "http://us.battle.net/api/wow",
            };
            timer.Elapsed += timer_Elapsed;
            timer.Start();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            poller.QueryAuctions(ConfigurationManager.AppSettings["server"], ConfigurationManager.AppSettings["factions"].Split(','));
        }

        protected override void OnStop()
        {
        }
    }
}
