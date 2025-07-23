using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class clsChartUpdate
    {
        public string Epic { get; set; }
        public Decimal Bid { get; set; }
        public Decimal Offer { get; set; }
        public Decimal LTP { get; set; }
        public Decimal LTV { get; set; }
        public Decimal TTV { get; set; }
        public DateTime? UTM { get; set; }
        public Decimal DAY_OPEN_MID { get; set; }
        public Decimal DAY_NET_CHG_MID { get; set; }
        public Decimal DAY_PERC_CHG_MID { get; set; }
        public Decimal DAY_HIGH { get; set; }
        public Decimal DAY_LOW { get; set; }


        public DateTime snapshotTimeUTC { get; set; }
        public DateTime IGUpdateDateTime { get; set; }
        public int SyncStatus { get; set; }
        public string id { get; set; }




        public clsChartUpdate()
        {
            this.Epic = "";
            this.Bid = new Decimal();
            this.Offer = new Decimal();

            this.LTP = 0;
            this.LTV = 0;
            this.TTV = 0;
            this.UTM = DateTime.MinValue;
            this.DAY_OPEN_MID = 0;
            this.DAY_NET_CHG_MID = 0;
            this.DAY_PERC_CHG_MID = 0;
            this.DAY_HIGH = 0;
            this.DAY_LOW = 0;

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

                        bool exist = await DoesThisTickExist(the_db, container);
                        if (!exist)
                        {
                            this.id = System.Guid.NewGuid().ToString();
                            ItemResponse<clsChartUpdate> SaveResponse = await container.CreateItemAsync<clsChartUpdate>(this, new PartitionKey(this.id));
                        }
                        blnLoop = false;
                    }
                }
                catch (CosmosException de)
                {
                    clsCommonFunctions.AddStatusMessage(de.ToString(), "ERROR");
                    var log = new Log();
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "clsChartUpdate/Add";
                    log.Epic = this.Epic;
                    await log.Save();
                }
                catch (Exception e)
                {
                    clsCommonFunctions.AddStatusMessage(e.ToString(), "ERROR");
                    var log = new Log();
                    log.Log_Message = e.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "clsChartUpdate/Add";
                    log.Epic = this.Epic;
                    await log.Save();
                }
            }


            return (ret);
        }
        public async Task<bool> DoesThisTickExist(Database the_db, Container container)
        {
            bool ret = false;

            try
            {
                if (the_db != null)
                {


                    var parameterizedQuery = new QueryDefinition(
                        query: "SELECT * FROM c WHERE c.Epic= @epicName and c.UTM = @UTM"
                    )
                    .WithParameter("@epicName", this.Epic)
                    .WithParameter("@UTM", this.UTM);

                    using FeedIterator<clsChartUpdate> filteredFeed = container.GetItemQueryIterator<clsChartUpdate>(
                        queryDefinition: parameterizedQuery
                    );

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<clsChartUpdate> response = await filteredFeed.ReadNextAsync();

                        // Iterate query results
                        foreach (clsChartUpdate item in response)
                        {
                            if (item.Epic == this.Epic && this.Bid == item.Bid)
                            {
                                ret = true;
                            }

                        }
                    }
                }

            }
            catch (CosmosException de)
            {
                clsCommonFunctions.AddStatusMessage(de.ToString(), "ERROR");
                var log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error"; 
                log.Log_App = "clsChartUpdate/DoesThisTickExist";
                log.Epic = this.Epic;
                await log.Save();
            }
            catch (Exception e)
            {
                clsCommonFunctions.AddStatusMessage(e.ToString(), "ERROR");
                var log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "clsChartUpdate/DoesThisTickExist";
                log.Epic = this.Epic;
                await log.Save();
            }
            return ret;
        }

    }
    public class clsChartUpdateMini
    {
        public string Epic { get; set; }
        public Decimal Bid { get; set; }
        public Decimal Offer { get; set; }
        public DateTime? UTM { get; set; }


        public clsChartUpdateMini()
        {
            this.Epic = "";
            this.Bid = new Decimal();
            this.Offer = new Decimal();
            this.UTM = DateTime.MinValue;
        }
    }
}


