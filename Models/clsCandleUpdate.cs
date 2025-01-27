using IGModels;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class clsCandleUpdate
    {
        public string Epic { get; set; }
        public Decimal Bid { get; set; }
        public Decimal Change { get; set; }
        public Decimal ChangePct { get; set; }
        public Decimal High { get; set; }
        public Decimal Low { get; set; }
        public Decimal MarketDelay { get; set; }
        public string MarketState { get; set; }
        public Decimal MidOpen { get; set; }
        public Decimal Offer { get; set; }
        public string UpdateTime { get; set; }
        public DateTime snapshotTimeUTC { get; set; }
        public DateTime IGUpdateDateTime { get; set; }
        public int SyncStatus { get; set; }
        public string id { get; set; }


        public clsCandleUpdate()
        {
            this.Epic = "";
            this.Bid = new Decimal();
            this.Change = new Decimal();
            this.ChangePct = new Decimal();
            this.High = new Decimal();
            this.Low = new Decimal();
            this.MarketDelay = new Decimal();
            this.MarketState = "";
            this.MidOpen = new Decimal();
            this.Offer = new Decimal();
            this.UpdateTime = "";
            this.snapshotTimeUTC = DateTime.UtcNow;
            this.id = System.Guid.NewGuid().ToString();
            this.SyncStatus = 0;
            this.IGUpdateDateTime = DateTime.MinValue;
        }

        public async Task<bool> Add(Database the_db, Container container)
        {
            bool ret = true;
            if (string.IsNullOrEmpty(this.id))
            {
                this.id = System.Guid.NewGuid().ToString();
            }

            this.SyncStatus = 0;
            bool blnLoop = true;
            while (blnLoop)
            {
                try
                {

                    if (the_db != null)
                    {
                        // DatabaseResponse db = await the_db.ReadAsync();
                        //Container container = the_db.GetContainer("Candles");

                        ItemResponse<clsCandleUpdate> SaveResponse = await container.CreateItemAsync<clsCandleUpdate>(this, new PartitionKey(this.Epic));
                        blnLoop = false;
                    }
                }
                catch (CosmosException de)
                {
                    clsCommonFunctions.AddStatusMessage(de.ToString(),"ERROR");
                }
                catch (Exception e)
                {
                    clsCommonFunctions.AddStatusMessage(e.ToString(),"ERROR");
                }


            }
            return (ret);
        }


    }

}
