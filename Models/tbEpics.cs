using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class tbEpics
    {
        public string epic { get; set; }
        public string resolution { get; set; }
        public string strategy { get; set; }
        public tbEpics()
        {
            epic = "";
            resolution = "";
            strategy = "";
        }
        public tbEpics(string input)
        {
            resolution = "";
            strategy = "";
            List<string> tmp = input.Split("|").ToList();
            epic = tmp[0];
            if (tmp.Count == 2)
            {
                strategy = tmp[1];
                resolution = "";
            }
            else
            {
                if (tmp.Count == 3)
                {
                    strategy = tmp[1];
                    resolution = tmp[2];
                }
            }
        }
    }
}
