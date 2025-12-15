using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using TradingBrain.Models;
namespace TradingBrain
{
    //public class WorkItem
    //{
    //    public int Id { get; set; }
    //    public string Data { get; set; } = "";

    //    public List<tbEpics> items { get; set; } = new List<tbEpics>();
    //    public string region { get; set; } = "test";
    //    public MainApp mainApp { get; set; }
    //}

    public class WorkChannel
    {
        public Channel<MainApp> channel { get; }
            = Channel.CreateUnbounded<MainApp>();
    }
}
