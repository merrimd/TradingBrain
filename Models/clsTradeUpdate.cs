using dto.endpoint.confirms;
using IGModels;
using IGWebApiClient;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class clsTradeUpdate

    {
        public string status { get; set; }
        ///<Summary>
        ///</Summary>
        public string reason { get; set; }
        ///<Summary>
        ///Deal status
        ///</Summary>
        public string dealStatus { get; set; }
        ///<Summary>
        ///Instrument epic identifier
        ///</Summary>
        public string epic { get; set; }
        ///<Summary>
        ///Instrument expiry
        ///</Summary>
        public string expiry { get; set; }
        ///<Summary>
        ///Deal reference
        ///</Summary>
        public string dealReference { get; set; }
        ///<Summary>
        ///Deal identifier
        ///</Summary>
        public string dealId { get; set; }
        ///<Summary>
        ///List of affected deals
        ///</Summary>
        public List<AffectedDeal> affectedDeals { get; set; }
        ///<Summary>
        ///Level
        ///</Summary>
        public decimal? level { get; set; }
        ///<Summary>
        ///Size
        ///</Summary>
        public decimal? size { get; set; }
        ///<Summary>
        ///Direction
        ///</Summary>
        public string direction { get; set; }
        ///<Summary>
        ///Stop level
        ///</Summary>
        public decimal? stopLevel { get; set; }
        ///<Summary>
        ///Limit level
        ///</Summary>
        public decimal? limitLevel { get; set; }
        ///<Summary>
        ///Stop distance
        ///</Summary>
        public decimal? stopDistance { get; set; }
        ///<Summary>
        ///Limit distance
        ///</Summary>
        public decimal? limitDistance { get; set; }
        ///<Summary>
        ///True if guaranteed stop
        ///</Summary>
        public bool? guaranteedStop { get; set; }
        public DateTime lastUpdated { get; set; }

    public string modelRunID { get; set; }

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
                Console.WriteLine("error:" + de.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("error:" + e.Message);
            }

            Console.WriteLine("Done");

            return ret;
        }

        //public async Task<bool> Add(Database the_db, Container container)
        //{
        //    bool ret = true;
        //    if (string.IsNullOrEmpty(this.id))
        //    {
        //        this.id = System.Guid.NewGuid().ToString();
        //    }

        //    this.SyncStatus = 0;
        //    bool blnLoop = true;
        //    while (blnLoop)
        //    {
        //        try
        //        {

        //            if (the_db != null)
        //            {
        //                // DatabaseResponse db = await the_db.ReadAsync();
        //                //Container container = the_db.GetContainer("Candles");

        //                bool exist = await DoesThisTickExist(the_db, container);
        //                if (!exist)
        //                {
        //                    this.id = System.Guid.NewGuid().ToString();
        //                    ItemResponse<clsChartUpdate> SaveResponse = await container.CreateItemAsync<clsChartUpdate>(this, new PartitionKey(this.id));
        //                }
        //                blnLoop = false;
        //            }
        //        }
        //        catch (CosmosException de)
        //        {
        //            Console.WriteLine(de.ToString());
        //            var log = new Log();
        //            log.Log_Message = de.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "clsChartUpdate/Add";
        //            log.Epic = this.Epic;
        //            await log.Save();
        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine(e.ToString());
        //            var log = new Log();
        //            log.Log_Message = e.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "clsChartUpdate/Add";
        //            log.Epic = this.Epic;
        //            await log.Save();
        //        }
        //    }


        //    return (ret);
        //}
        //public async Task<bool> DoesThisTickExist(Database the_db, Container container)
        //{
        //    bool ret = false;

        //    try
        //    {
        //        if (the_db != null)
        //        {


        //            var parameterizedQuery = new QueryDefinition(
        //                query: "SELECT * FROM c WHERE c.Epic= @epicName and c.UTM = @UTM"
        //            )
        //            .WithParameter("@epicName", this.Epic)
        //            .WithParameter("@UTM", this.UTM);

        //            using FeedIterator<clsChartUpdate> filteredFeed = container.GetItemQueryIterator<clsChartUpdate>(
        //                queryDefinition: parameterizedQuery
        //            );

        //            while (filteredFeed.HasMoreResults)
        //            {
        //                FeedResponse<clsChartUpdate> response = await filteredFeed.ReadNextAsync();

        //                // Iterate query results
        //                foreach (clsChartUpdate item in response)
        //                {
        //                    if (item.Epic == this.Epic && this.Bid == item.Bid)
        //                    {
        //                        ret = true;
        //                    }

        //                }
        //            }
        //        }

        //    }
        //    catch (CosmosException de)
        //    {
        //        Console.WriteLine(de.ToString());
        //        var log = new Log();
        //        log.Log_Message = de.ToString();
        //        log.Log_Type = "Error";
        //        log.Log_App = "clsChartUpdate/DoesThisTickExist";
        //        log.Epic = this.Epic;
        //        await log.Save();
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e.ToString());
        //        var log = new Log();
        //        log.Log_Message = e.ToString();
        //        log.Log_Type = "Error";
        //        log.Log_App = "clsChartUpdate/DoesThisTickExist";
        //        log.Epic = this.Epic;
        //        await log.Save();
        //    }
        //    return ret;
        //}

    }
}
