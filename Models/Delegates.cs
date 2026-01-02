using com.lightstreamer.client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class Delegates
    {
        public delegate void LightstreamerUpdateDelegate(int item, ItemUpdate values);
        public delegate void LightstreamerStatusChangedDelegate(int cStatus, string status);
    }
}