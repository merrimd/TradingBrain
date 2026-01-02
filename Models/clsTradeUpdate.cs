using dto.endpoint.confirms;
using IGModels;
using IGWebApiClient;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class TradeSubUpdate : LsTradeSubscriptionData
    {
        public string id { get; set; } = "";
        public DateTime timestamp { get; set; } = DateTime.MinValue;
        public string statusVal { get; set; } = "";
        public string directionVal { get; set; } = "";
        public string dealStatusVal { get; set; } = "";
        public string updateType { get; set; } = "";
        public string reasonDescription { get; set; } = "";
        public TradeSubUpdate()
        {
            id = Guid.NewGuid().ToString();
            timestamp = DateTime.MinValue;
            statusVal = "";
            directionVal = "";
            dealStatusVal = "";
            updateType = "";
            reasonDescription = "";

        }
        public async Task<bool> SaveDocument(Database the_db)
        {
            bool ret = true;

            Container container = the_db.GetContainer("TradingBrainSubUpdates");

            try
            {
                ItemResponse<TradeSubUpdate> SaveResponse = await container.UpsertItemAsync<TradeSubUpdate>(this);
                //clsCommonFunctions.AddStatusMessage("Trade db record updated", "INFO");
            }
            catch (CosmosException de)
            {
                Console.WriteLine(de.ToString());
                var log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TradeSubUpdate/SaveDocument";

                await log.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                var log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TradeSubUpdate/SaveDocument";

                await log.Save();
            }



            return ret;
        }
        public async Task<bool> Add(Database? the_db)
        {
            bool ret = true;
            if (the_db != null)
            {
                if (string.IsNullOrEmpty(this.id))
                {
                    this.id = System.Guid.NewGuid().ToString();
                }

                try
                {

                    // DatabaseResponse db = await the_db.ReadAsync();
                    Container container = the_db.GetContainer("TradingBrainSubUpdates");


                    this.id = System.Guid.NewGuid().ToString();
                    ItemResponse<TradeSubUpdate> SaveResponse = await container.CreateItemAsync<TradeSubUpdate>(this, new PartitionKey(this.id));
                    //clsCommonFunctions.AddStatusMessage("Trade db record updated", "INFO");
                }
                catch (CosmosException de)
                {
                    Console.WriteLine(de.ToString());
                    var log = new Log();
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "TradeSubUpdate/Add";

                    await log.Save();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    var log = new Log();
                    log.Log_Message = e.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "TradeSubUpdate/Add";

                    await log.Save();
                }
            }
            return ret;



        }
    }

    public class clsTradeUpdate

    {
        public string status { get; set; } = "";
        ///<Summary>
        ///</Summary>
        public string reason { get; set; } = "";
        ///<Summary>
        ///Deal status
        ///</Summary>
        public string dealStatus { get; set; } = "";
        ///<Summary>
        ///Instrument epic identifier
        ///</Summary>
        public string epic { get; set; } = "";
        ///<Summary>
        ///Instrument expiry
        ///</Summary>
        public string expiry { get; set; } = "";
        ///<Summary>
        ///Deal reference
        ///</Summary>
        public string dealReference { get; set; } = "";
        ///<Summary>
        ///Deal identifier
        ///</Summary>
        public string dealId { get; set; } = "";
        ///<Summary>
        ///List of affected deals
        ///</Summary>
        public List<AffectedDeal> affectedDeals { get; set; }
        ///<Summary>
        ///Level
        ///</Summary>
        public decimal level { get; set; } = new decimal();
        ///<Summary>
        ///Size
        ///</Summary>
        public decimal size { get; set; } = new decimal();
        ///<Summary>
        ///Direction
        ///</Summary>
        public string direction { get; set; } = "";
        ///<Summary>
        ///Stop level
        ///</Summary>
        public decimal stopLevel { get; set; } = new decimal();
        ///<Summary>
        ///Limit level
        ///</Summary>
        public decimal limitLevel { get; set; } = new decimal();
        ///<Summary>
        ///Stop distance
        ///</Summary>
        public decimal stopDistance { get; set; } = new decimal();
        ///<Summary>
        ///Limit distance
        ///</Summary>
        public decimal limitDistance { get; set; } = new decimal();     
        ///<Summary>
        ///True if guaranteed stop
        ///</Summary>
        public bool guaranteedStop { get; set; } = false;
        public DateTime lastUpdated { get; set; } = DateTime.MinValue;
        public string accountId { get; set; } = "";
        public string channel { get; set; } = "";

        public string modelRunID { get; set; } = "";

        public clsTradeUpdate()
        {
            this.status = "";
            this.reason = "";
            this.dealStatus = "";
            this.epic = "";
            this.expiry = "";
            this.dealReference = "";
            this.dealId = "";
            this.affectedDeals = new List<AffectedDeal>();
            this.level = 0;
            this.size = 0;
            this.direction = "";
            this.stopLevel = 0;
            this.limitLevel = 0;
            this.stopDistance = 0;
            this.limitDistance = 0;
            this.guaranteedStop = false;
            this.lastUpdated = DateTime.MinValue;
            this.modelRunID = "";
            this.accountId = "";
            this.channel = "";
        }
        public async Task<bool> SaveDocument(Container container)
        {
            bool ret = true;


            try
            {
                ItemResponse<clsTradeUpdate> SaveResponse = await container.UpsertItemAsync<clsTradeUpdate>(this);
            }
            catch (CosmosException de)
            {
                CommonFunctions.AddStatusMessage("error:" + de.ToString(), "ERROR");
            }
            catch (Exception e)
            {
                CommonFunctions.AddStatusMessage("error:" + e.Message, "ERROR");
            }

            //clsCommonFunctions.AddStatusMessage("Trade db record updated", "INFO");

            return ret;
        }

    }
}
