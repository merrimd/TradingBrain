using IGModels;
using IGWebApiClient.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NLog;
using Org.BouncyCastle.Tsp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{

    public class UpdateMessage
    {
        public string updateType { get; set; }
        public string updateData { get; set; }
        public string itemName { get; set; }
        public UpdateMessage()
        {
            updateData = "";
            itemName = "";
            updateType = "";
        }
    }

}