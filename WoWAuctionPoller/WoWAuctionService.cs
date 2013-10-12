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

namespace WoWAuctionPoller
{
    public partial class WoWAuctionService : ServiceBase
    {
        Timer timer;
        public WoWAuctionService()
        {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 5 * 60 * 1000;
        }

        protected override void OnStart(string[] args)
        {
            timer.Start();

        }

        protected override void OnStop()
        {
        }
    }
}
