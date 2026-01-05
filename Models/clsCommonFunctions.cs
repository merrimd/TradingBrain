using IGCandleCreator.Models;
using IGModels;
using IGModels.ModellingModels;
using Lightstreamer.DotNet.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NLog;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static TradingBrain.Models.CommonFunctions;

namespace TradingBrain.Models
{
    public static class CommonFunctions
    {
        public static void AddStatusMessage(string message, string level = "INFO")
        {
            Logger tbLog = LogManager.GetCurrentClassLogger();
            //Console.WriteLine(message);
            switch (level)
            {

                case "ERROR":
                    tbLog.Error(message);
                    break;

                case "DEBUG":
                    tbLog.Debug(message);
                    break;

                case "WARNING":
                    tbLog.Warn(message);
                    break;

                default:
                    tbLog.Info(message);
                    break;

            }


        }
        public static void AddStatusMessage(string message, string level, string logName)
        {
            //Logger tbLog = LogManager.GetLogger(logName);
            Logger tbLog = LogManager.GetCurrentClassLogger();
            //Console.WriteLine(message);
            switch (level)
            {
                case "ERROR":
                    tbLog.Error(message);
                    break;

                case "DEBUG":
                    tbLog.Debug(message);
                    break;

                case "WARNING":
                    tbLog.Warn(message);
                    break;

                default:
                    tbLog.Info(message);
                    break;

            }


        }
        public static async Task<Database?> Get_Database()
        {
            Database? ret = null;
            try
            {
                DatabaseParams db_params = new();

                CosmosClient cosmosClient = new(db_params.EndpointUri, db_params.DBPrimaryKey, new CosmosClientOptions() { ApplicationName = "IGFunctions" });

                ret = cosmosClient.GetDatabase(db_params.DBName);
            }
            catch (CosmosException de)
            {
                // DON'T create a Log here - it causes infinite recursion in tests
                System.Diagnostics.Debug.WriteLine($"CosmosException in Get_Database: {de}");
            }
            catch (Exception e)
            {
                // DON'T create a Log here - it causes infinite recursion in tests
                System.Diagnostics.Debug.WriteLine($"Exception in Get_Database: {e}");
            }

            return ret;
        }
        public static async Task<Database?> Get_App_Database()
        {
            Database? ret = null;
            try
            {
                DatabaseParams db_params = new();

                CosmosClient cosmosClient = new(db_params.EndpointUri, db_params.DBPrimaryKey, new CosmosClientOptions() { ApplicationName = "IGFunctions" });

                ret =  cosmosClient.GetDatabase(db_params.DBNameApp);
            }
            catch (CosmosException de)
            {
                // DON'T create a Log here - it causes infinite recursion in tests
                System.Diagnostics.Debug.WriteLine($"CosmosException in Get_App_Database: {de}");
            }
            catch (Exception e)
            {
                // DON'T create a Log here - it causes infinite recursion in tests
                System.Diagnostics.Debug.WriteLine($"Exception in Get_App_Database: {e}");
            }

            return ret;
        }
        public static void DiscardTask(this Task ignored)
        {
        }

        public static string EncryptText(string ClearText, string Key, string IV)
        {
            byte[] bytKey = Encoding.UTF8.GetBytes(Key);
            byte[] bytIV = Encoding.UTF8.GetBytes(IV);
            string ret = EncryptAES(ClearText, bytKey, bytIV);
            return ret;
        }

        public static string DecryptText(string EncryptedText, string Key, string IV)
        {
            byte[] bytKey = Encoding.UTF8.GetBytes(Key);
            byte[] bytIV = Encoding.UTF8.GetBytes(IV);
            string ret = DecryptAES(EncryptedText, bytKey, bytIV);
            return ret;
        }
        public static string EncryptAES(string ClearText, Byte[] KeyBytes, Byte[] IVBytes)
        {
            string StrRet = "";

            try
            {
                string tempText = HttpUtility.UrlEncode(ClearText);

                Byte[] SourceBytes = System.Text.Encoding.UTF8.GetBytes(tempText);
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.BlockSize = 128;
                    aes.KeySize = 256;
                    aes.Key = KeyBytes;
                    aes.IV = IVBytes;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        using (MemoryStream ms = new())
                        {
                            using (CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write))
                            {
                                using (StreamWriter swEncrypt = new(cs))
                                {
                                    swEncrypt.Write(ClearText);
                                }

                                StrRet = Convert.ToBase64String(ms.ToArray());
                            }

                        }
                    }
                    ;
                }
            }
            catch (Exception ex)
            {
                // Don't try to log to database - it causes issues in tests
                System.Diagnostics.Debug.WriteLine($"EncryptAES failed: {ex}");
            }

            return StrRet;
        }

        public static string DecryptAES(string base64Cipher, Byte[] KeyBytes, Byte[] IVBytes)
        {
            string StrRet = "";
            try
            {
                Byte[] cipherBytes = Convert.FromBase64String(base64Cipher.Replace(' ', '+'));
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.BlockSize = 128;
                    aes.KeySize = 256;
                    aes.Key = KeyBytes;
                    aes.IV = IVBytes;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream msDecrypt = new(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new(csDecrypt))
                            {
                                StrRet = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't try to log to database - it causes issues in tests
                System.Diagnostics.Debug.WriteLine($"DecryptAES failed: {ex}");
            }

            return StrRet;
        }
        public static async Task<Log> LogData(Database the_db, string Log_type, string Log_action, string Log_Message, string Log_App, string Reference_ID, string Reference_sub_id, HttpRequest req)
        {
            var ret = new Log(the_db);
            try
            {

                ret.Log_Type = Log_type;
                ret.Log_Action = Log_action;
                ret.Log_App = Log_App;
                ret.Log_Message = Log_Message;
                ret.IPAddress = GetClientIp(req);
                ret.Reference_id = Reference_ID;
                await ret.Save();

            }
            catch (Exception ex)
            {
                ret.Log_Message = ex.ToString();
            }

            return ret;
        }
        public static async Task<Log> LogData(Database the_db, string Log_type, string Log_action, string Log_Message, string Log_App, string Reference_ID, string Reference_sub_id)
        {
            var ret = new Log(the_db);
            try
            {

                ret.Log_Type = Log_type;
                ret.Log_Action = Log_action;
                ret.Log_App = Log_App;
                ret.Log_Message = Log_Message;
                ret.IPAddress = "n/a";
                ret.Reference_id = Reference_ID;
                await ret.Save();

            }
            catch (Exception ex)
            {
                ret.Log_Message = ex.ToString();
            }

            return ret;
        }

        public static string GetClientIp(HttpRequest req)
        {
            if (req == null || req.HttpContext == null || req.HttpContext.Connection == null || req.HttpContext.Connection.RemoteIpAddress == null)
            {
                return "unknown";
            }
            String RemoteIP = req.HttpContext.Connection.RemoteIpAddress.ToString() ?? ""; //  + ":" + req.HttpContext.Connection.RemotePort.ToString();

            return RemoteIP;
        }
        public static Boolean Is_Dangerous(string value)
        {
            // validate for cross site scripting (XSS)
            Boolean blnRet = false;

            string strBlocklist = "|<|>|&amp|&#|javascript|&lt|&gt|varchar|exec(|0x0|0x1|0x2|0x3|0x4|0x5|0x6|0x7|0x8|0x9|";
            string strCheckValue = "|" + value + "|";
            string[] arrBlockList = strBlocklist.Split('|');
            string strThisItem;
            for (int i = 0; i < arrBlockList.Length; i++)
            {
                strThisItem = arrBlockList[i];
                if (!string.IsNullOrEmpty(strThisItem))
                {
                    if (strCheckValue.Contains(strThisItem))
                    {
                        blnRet = true;
                    }
                }
            }
            return (blnRet);
        }


        public static string GetKey()
        {
            string ret = "";
            string basedata = "3ogh3490gh8340938bg0bgn0328vn253ng08nv28nb098n8934394bgn938bn893nb93nv893nv938vn39vn3890tnvdehbveiovn439780nv4bn49p834nbp34b9peuife7fmj39frmjgf84jgf9fu489";
            int iPos = 0;

            for (int iCount = 0; iCount < 32; iCount++)
            {
                ret += basedata.Substring(iPos, 1);
                iPos += 2;

            }

            return ret;
        }

        public static string GetRandomString(int length)
        {

            string ret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(length));

            if (ret.Length > length)
            {
                ret = ret.Substring(0, length);
            }

            return ret;
        }

        public static string GetIv()
        {
            string ret = "";
            string basedata = "bn25894nb8924nb240bn034nb34308g90gj390gj33nb80n45n48hn894n4ng948gn894hg894g84gn894gn4";
            int iPos = 0;

            for (int iCount = 0; iCount < 16; iCount++)
            {
                ret += basedata.Substring(iPos, 1);
                iPos += 2;
            }

            return ret;

        }
        public static string EncryptText(string ClearText)
        {
            byte[] bytKey = Encoding.UTF8.GetBytes(GetKey());
            byte[] bytIV = Encoding.UTF8.GetBytes(GetIv());
            string ret = EncryptAES(ClearText, bytKey, bytIV);
            return ret;
        }

        public static string DecryptText(string ClearText)
        {
            byte[] bytKey = Encoding.UTF8.GetBytes(GetKey());
            byte[] bytIV = Encoding.UTF8.GetBytes(GetIv());
            string ret = DecryptAES(ClearText, bytKey, bytIV);
            return ret;
        }

        public static async Task<IG_Epic> Get_IG_Epic(Database? the_db, string epicName)
        {
            // find the data
            IG_Epic epic = new();
            try
            {
                if (the_db != null)
                {
                    Container container = the_db.GetContainer("Epics");

                    var parameterizedQuery = new QueryDefinition(
                        query: "SELECT * FROM Epics c WHERE c.Epic= @epicName"
                    )
                    .WithParameter("@epicName", epicName);

                    using FeedIterator<IG_Epic> filteredFeed = container.GetItemQueryIterator<IG_Epic>(
                        queryDefinition: parameterizedQuery
                    );

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<IG_Epic> response = await filteredFeed.ReadNextAsync();

                        // Iterate query results
                        foreach (IG_Epic item in response)
                        {
                            epic = item;
                        }
                    }

                    //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);
                }
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new(the_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Epic/Get";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();
            }

            return (epic);
        }

        public static List<EpicList> GetEpicList(string[] epics)
        {
            List<EpicList> ret = new();

            foreach (string item in epics)
            {
                EpicList o = new();
                o.Epic = item;
                o.counter = 0;
                o.last_ig_updatetime = "";
                ret.Add(o);
            }

            return ret;
        }
        public static async Task<List<IG_Epic>> Get_IG_Epics(Database the_db)
        {
            // find the data
            List<IG_Epic> epics = new();
            try
            {
                Container container = the_db.GetContainer("Epics");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM Epics c WHERE c.enabled='Y'"
                );


                using FeedIterator<IG_Epic> filteredFeed = container.GetItemQueryIterator<IG_Epic>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<IG_Epic> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (IG_Epic item in response)
                    {
                        epics.Add(item);
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                Log log = new(the_db);
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();


            }
            catch (Exception e)
            {
                Log log = new(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();
            }

            return (epics);
        }
        //public static async Task<TradingBrainSettings> GetTradingBrainSettings(Database the_db, string epicName)
        //{
        //    // find the data
        //    var ret = new TradingBrainSettings();
        //    try
        //    {
        //        Container container = the_db.GetContainer("TradingBrainSettings");

        //        var parameterizedQuery = new QueryDefinition(
        //            query: "SELECT top 1 * FROM c WHERE c.epicName = @epicname  Order by c.timestamp DESC"

        //        //query: "SELECT M.modelLogs FROM c JOIN (SELECT VALUE m FROM m IN c.runVars WHERE m.var1 = @Var1) as m WHERE c.modelRunID = @ModelRunID  Order by c.runVars.modelLogs.seqNo ASC"
        //        ).WithParameter("@epicname", epicName);

        //        using FeedIterator<TradingBrainSettings> filteredFeed = container.GetItemQueryIterator<TradingBrainSettings>(
        //            queryDefinition: parameterizedQuery
        //        );


        //        while (filteredFeed.HasMoreResults)
        //        {
        //            FeedResponse<TradingBrainSettings> response = await filteredFeed.ReadNextAsync();
        //            var logs = response.Resource;
        //            // Iterate query results
        //            foreach (var item in logs)
        //            {
        //                ret = item;
        //            }
        //        }


        //    }
        //    catch (CosmosException de)
        //    {
        //        Log log = new Log();
        //        log.Log_Message = de.ToString();
        //        log.Log_Type = "Error";
        //        log.Log_App = "Get_Stats/Get";
        //        await log.Save();


        //    }
        //    catch (Exception e)
        //    {
        //        Log log = new Log();
        //        log.Log_Message = e.ToString();
        //        log.Log_Type = "Error";
        //        log.Log_App = "Get_Stats/Get";
        //        await log.Save();
        //    }

        //    return (ret);
        //}

        public static async Task<List<Candle>> Get_IG_Candles(Database the_db, string epicName)
        {
            // find the data
            List<Candle> candles = new();
            try
            {
                Container container = the_db.GetContainer("Candles");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM Candles c where c.Epic=@epicName and c.SyncStatus = 0 order by snapshotTimeUTC DESC"
                )
                .WithParameter("@epicName", epicName);

                using FeedIterator<Candle> filteredFeed = container.GetItemQueryIterator<Candle>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<Candle> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (Candle item in response)
                    {
                        candles.Add(item);
                    }
                }



            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new(the_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Epic/Get";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();
            }

            return (candles);
        }



        public static async Task<Settings> Get_Settings(Database the_db)
        {
            // find the data
            Settings settings = new();
            try
            {
                Container container = the_db.GetContainer("Settings");

                //string sql = "Select TOP 1 * from c where IS_DEFINED(c.ErrorList)";
                string sql = "Select TOP 1 * from c ";
                QueryDefinition query = new(sql);

                FeedIterator<Settings> queryA = container.GetItemQueryIterator<Settings>(query, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });


                foreach (Settings s in await queryA.ReadNextAsync())
                {
                    settings = s;

                }

            }
            catch (CosmosException de)
            {
                Log log = new(the_db);
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get Settings";
                await log.Save();

            }
            catch (Exception e)
            {
                Log log = new(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get Settings";
                await log.Save();
            }

            return (settings);
        }

        public static async void SendBroadcast(string messageType, string messageValue)
        {
            HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(3);

            try
            {
                string url = "";
                url = Environment.GetEnvironmentVariable("MessagingEndPoint") ?? "";
                if (url == "")
                {
                    var igWebApiConnectionConfig = ConfigurationManager.GetSection("appSettings") as NameValueCollection;
                    if (igWebApiConnectionConfig != null)
                    {
                        if (igWebApiConnectionConfig.Count > 0)
                        {
                            url = igWebApiConnectionConfig["MessagingEndPoint"] ?? "";
                        }
                    }
                }
                IGModels.ModellingModels.message newMsg = new();
                newMsg.messageType = messageType;
                newMsg.messageValue = messageValue;


                url = url + "/broadcast";

                string msg = JsonConvert.SerializeObject(newMsg);
                HttpContent content = new StringContent(msg, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                string results = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException)
            {
                //Could have been caused by cancellation or timeout if you used one.  
                //If that was the case, rethrow.  
                //cancellationToken.ThrowIfCancellationRequested();  

                //HttpClient throws TaskCanceledException when the request times out. That's dumb.  
                //Throw TimeoutException instead and say how long we waited.  
                string time;
                if (client.Timeout.TotalHours > 1)
                {
                    time = $"{client.Timeout.TotalHours:N1} hours";
                }
                else if (client.Timeout.TotalMinutes > 1)
                {
                    time = $"{client.Timeout.TotalMinutes:N1} minutes";
                }
                else if (client.Timeout.TotalSeconds > 1)
                {
                    time = $"{client.Timeout.TotalSeconds:N1} seconds";
                }
                else
                {
                    time = $"{client.Timeout.TotalMilliseconds:N0} milliseconds";
                }
                CommonFunctions.AddStatusMessage("Message timed out.");
                //throw new TimeoutException($"No response after waiting {time}.");
            }
            catch (Exception e)
            {
                AddStatusMessage("Error sending broadcast message: " + e.ToString(), "ERROR");
                //Log log = new Log(the_app_db);
                //log.Log_Message = e.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "SendMessage";
                //await log.Save();
            }

        }
        public static async void SendMessage(string userid, string messageType, string messageValue, Database? the_app_db)
        {
            try
            {
                if (the_app_db != null)
                {
                    string url = "";
                    url = Environment.GetEnvironmentVariable("MessagingEndPoint") ?? "";
                    if (url == "")
                    {
                        var igWebApiConnectionConfig = ConfigurationManager.GetSection("appSettings") as NameValueCollection;
                        if (igWebApiConnectionConfig != null && igWebApiConnectionConfig.Count > 0)
                        {
                            url = igWebApiConnectionConfig["MessagingEndPoint"] ?? "";
                        }
                    }
                    IGModels.ModellingModels.message newMsg = new();
                    newMsg.messageType = messageType;
                    newMsg.messageValue = messageValue;

                    HttpClient client = new();
                    url = url + "/sendmessage?userid=" + userid;

                    string msg = JsonConvert.SerializeObject(newMsg);
                    HttpContent content = new StringContent(msg, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = client.PostAsync(url, content).Result;
                    string results = response.Content.ReadAsStringAsync().Result;
                }
            }
            catch (Exception e)
            {
                // Don't try to log to database - it causes issues in tests
                System.Diagnostics.Debug.WriteLine($"SendMessage failed: {e}");
            }
        }
        public static async void SaveLog(string logType, string logApp, string logMessage, Database? the_db)
        {
            if (the_db != null)
            {
                Log log = new(the_db);
                log.Log_Message = logMessage;
                log.Log_Type = logType;
                log.Log_App = logApp;
                await log.Save();
            }
        }

        public static async Task<TradingBrainSettings> GetTradingBrainSettings(Database? the_db, string epicName, string accountId, string strategy = "SMA", string resolution = "")
        {
            // find the data
            var ret = new TradingBrainSettings();


            string logName = epicName + "." + strategy;
            if (strategy == "RSI" ||
                strategy == "REI" ||
                strategy == "RSI-ATR" ||
                strategy == "RSI-CUML" ||
                strategy == "CASEYC" ||
                 strategy == "VWAP" ||
                strategy == "CASEYCSHORT" ||
                strategy == "CASEYCEQUITIES")
            {
                logName += "_" + resolution;
            }
            if (strategy == "SMA")
            {
                resolution = "";
            }

            try
            {
                if (the_db == null) { throw new InvalidOperationException("Database is null"); }
                Container container = the_db.GetContainer("TradingBrainSettings");
                Container container_opt = the_db.GetContainer("OptimizeRunData");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT top 1 * FROM c WHERE c.epicName = @epicname AND (c.accountId = @accountId or c.accountId = '') AND c.strategy = @strategy AND c.resolution = @resolution Order by c.timestamp DESC"

                //query: "SELECT M.modelLogs FROM c JOIN (SELECT VALUE m FROM m IN c.runVars WHERE m.var1 = @Var1) as m WHERE c.modelRunID = @ModelRunID  Order by c.runVars.modelLogs.seqNo ASC"
                ).WithParameter("@epicname", epicName)
                .WithParameter("@strategy", strategy)
                .WithParameter("@resolution", resolution)
                .WithParameter("@accountId", accountId);

                using FeedIterator<TradingBrainSettings> filteredFeed = container.GetItemQueryIterator<TradingBrainSettings>(
                    queryDefinition: parameterizedQuery
                );


                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<TradingBrainSettings> response = await filteredFeed.ReadNextAsync();
                    var logs = response.Resource;
                    // Iterate query results
                    foreach (var item in logs)
                    {
                        ret = item;
                    }
                }

                if (ret.runDetails.getLatestVars)
                {



                    CommonFunctions.AddStatusMessage("Getting latest vars (if necessary)", "INFO", logName);



                    // now get the latest optimized data
                    OptimizeRunData opt = new();
                    opt = await OptimizeRunData.GetOptimizeRunDataLatest(the_db, container_opt, epicName, ret.runDetails.numCandles, strategy, resolution);
                    if (opt.inputs.Count > 0)
                    {
                        ret.runDetails.inputs = opt.inputs;
                        await ret.SaveDocument(the_db);
                    }
                    if (opt.inputs_RSI.Count > 0)
                    {
                        ret.runDetails.inputs_RSI = opt.inputs_RSI;
                        if (opt.averageWinValue > 0)
                        {
                            ret.lastRunVars.seedAvgWinningTrade = opt.averageWinValue;
                            CommonFunctions.AddStatusMessage($"seedAvgWinningTrade taken from opt - {opt.averageWinValue}", "INFO", logName);
                        }
                        await ret.SaveDocument(the_db);
                    }
                }

            }
            catch (CosmosException de)
            {
                Log log = new(the_db);  // <-- No database passed!
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetTradingBrainSettings";
                await log.Save();  // <-- This tries to call Get_App_Database()
            }
            catch (Exception e)
            {
                Log log = new(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetTradingBrainSettings";
                await log.Save();
            }

            return (ret);
        }
        public class OrderValues
        {
            public double quantity { get; set; }
            public double stopDistance { get; set; }
            public decimal level { get; set; }
            public decimal targetPrice { get; set; }

            public OrderValues()
            {
                quantity = 0;
                stopDistance = 0;
                level = 0;
                targetPrice = 0;
            }

            //public void SetOrderValues(string direction, MainApp thisApp)
            //{
            //    try
            //    {

            //        if (thisApp.model == null)
            //        {
            //            throw new InvalidOperationException("thisApp.model is null");
            //        }

            //        double quantity = Math.Min(thisApp.model.thisModel.currentTrade.quantity * thisApp.model.modelVar.suppQuantityMultiplier, thisApp.model.modelVar.maxQuantity);
            //        modelInstanceInputs thisInput = IGModels.clsCommonFunctions.GetInputsFromSpread(thisApp.model.thisModel.inputs, thisApp.model.candles.currentCandle.candleData);
            //        double targetVar = thisInput.targetVarInput / 100 + 1;
            //        double targetVarShort = thisInput.targetVarInputShort / 100 + 1;
            //        decimal targetPrice = 0;
            //        double suppStopPercentage = thisApp.model.modelVar.suppStopPercentage;
            //        if (suppStopPercentage == 0) { suppStopPercentage = 1; }

            //        // Now set up the order for the supp trade

            //        //Calculate the open level and stop limits
            //        decimal dealPrice = 0;
            //        double targetUnits = 0;
            //        double suppTargetUnits = 0;
            //        decimal newLevel = 0;
            //        double newStop = 0;

            //        if (direction.ToUpper() == "BUY")
            //        {
            //            dealPrice = thisApp.model.thisModel.currentTrade.buyPrice;
            //            targetPrice = thisApp.model.thisModel.currentTrade.buyPrice + Math.Abs((decimal)targetVar * (decimal)thisApp.model.candles.currentCandle.mATypicalLongTypical - (decimal)thisApp.model.candles.currentCandle.mATypicalLongTypical);
            //            targetUnits = (double)(targetPrice - dealPrice);
            //            suppTargetUnits = targetUnits * 0.9;
            //            newLevel = dealPrice + (decimal)suppTargetUnits;
            //        }
            //        else
            //        {
            //            dealPrice = thisApp.model.thisModel.currentTrade.sellPrice;
            //            targetPrice = thisApp.model.thisModel.currentTrade.sellPrice - Math.Abs((decimal)targetVarShort * (decimal)thisApp.model.candles.currentCandle.mATypicalShortTypical - (decimal)thisApp.model.candles.currentCandle.mATypicalShortTypical);
            //            targetUnits = (double)(dealPrice - targetPrice);
            //            suppTargetUnits = targetUnits * 0.9;
            //            newLevel = dealPrice - (decimal)suppTargetUnits;
            //        }

            //        newStop = targetUnits - (targetUnits * suppStopPercentage) - (targetUnits - suppTargetUnits);

            //        this.quantity = quantity;
            //        this.stopDistance = Math.Round(newStop, 1);
            //        this.level = Math.Round(newLevel, 1);
            //        this.targetPrice = Math.Round(targetPrice, 1);

            //        if (thisApp.model.doSuppTrades)
            //        {
            //            CommonFunctions.AddStatusMessage($"order values - dealPrice={this.level}, targetPrice= {targetPrice}, targetUnits = {targetUnits}, suppTargetUnits = {suppTargetUnits}, newLevel = {newLevel}, stop level {newStop}, suppStopPercentage {suppStopPercentage}", "DEBUG");
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        CommonFunctions.AddStatusMessage("Error calculating order values: " + ex.ToString(), "ERROR");
            //    }
            //}
        }

    }
}