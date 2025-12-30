using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class EpicList
    {
        public string Epic { get; set; }
        public string last_ig_updatetime { get; set; }
        public DateTime last_ig_dt { get; set; }
        public int counter { get; set; }

        public EpicList()
        {
            this.Epic = "";
            this.last_ig_updatetime = "";
            this.counter = 0;
            this.last_ig_dt = DateTime.MinValue;
        }
    }
}
