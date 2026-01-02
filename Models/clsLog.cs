#nullable enable
using IGModels;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TradingBrain.Models.CommonFunctions;


namespace TradingBrain.Models
{
    public class Log
    {
        public string id { get; set; }
        public string Log_Type { get; set; }
        public string Log_Action { get; set; }
        public DateTime Log_Timestamp { get; set; }
        public string Log_Message { get; set; }
        public string Log_App { get; set; }
        public string Epic { get; set; }
        public string Reference_id { get; set; }
        public string Reference_sub_id { get; set; }
        public DateTime Reference_Expiry { get; set; }
        public string IPAddress { get; set; }

        private Database db;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Log()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            Setup();
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Log(Database? the_db)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            if (the_db != null)
            {
                this.db = the_db;
            }
            Setup();
        }

        private void Setup()
        {
            this.id = System.Guid.NewGuid().ToString();
            this.Log_Type = "";
            this.Log_Timestamp = DateTime.UtcNow;
            this.Log_Message = "";
            this.Log_App = "";
            this.Log_Action = "";
            this.Reference_id = "";
            this.Reference_sub_id = "";
            this.IPAddress = "";
            this.Reference_Expiry = DateTime.MaxValue;
            this.Epic = "";
        }
        public async Task<bool> Save()
        {
            bool ret = true;

            try
            {
                if (this.db == null)
                {
                    Database? the_db = await Get_App_Database();
                    if (the_db != null)
                    {
                        this.db = the_db;
                    }

                }

                if (db != null)
                {
                    Container container = db.GetContainer("TradingBrainLogs");

                    if (this.Log_Timestamp == DateTime.MinValue)
                    {
                        this.Log_Timestamp = DateTime.UtcNow;
                    }

                    await container.CreateItemAsync<Log>(this, new PartitionKey(this.Log_Type));

                }



            }
            catch (CosmosException de)
            {

                this.Log_Message = de.ToString();
                ret = false;
            }
            catch (Exception e)
            {
                this.Log_Message = e.ToString();
                ret = false;
            }


            return (ret);
        }
    }


}
#nullable disable