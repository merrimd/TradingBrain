using IGCandleCreator.Models;
using IGModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace TradingBrain.Models
{
    public static class clsCommonFunctions
    {
        public static void AddStatusMessage(string message)
        {
            Console.WriteLine(message);
        }
        public static async Task<Database?> Get_Database()
        {
            Database? ret = null;
            try
            {
                DatabaseParams db_params = new DatabaseParams();

                CosmosClient cosmosClient = new CosmosClient(db_params.EndpointUri, db_params.DBPrimaryKey, new CosmosClientOptions() { ApplicationName = "IGFunctions" });

                ret = cosmosClient.GetDatabase(db_params.DBName);
            }
            catch (CosmosException de)
            {
                var log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Validate_Session";
                await log.Save();
            }
            catch (Exception e)
            {
                var log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Validate_Session";
                await log.Save();
            }

            return ret;
        }
        public static async Task<Database?> Get_App_Database()
        {
            Database? ret = null;
            try
            {
                DatabaseParams db_params = new DatabaseParams();

                CosmosClient cosmosClient = new CosmosClient(db_params.EndpointUri, db_params.DBPrimaryKey, new CosmosClientOptions() { ApplicationName = "IGFunctions" });

                ret = cosmosClient.GetDatabase(db_params.DBNameApp);
            }
            catch (CosmosException de)
            {
                var log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Validate_Session";
                await log.Save();
            }
            catch (Exception e)
            {
                var log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Validate_Session";
                await log.Save();
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
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                            {
                                using (StreamWriter swEncrypt = new StreamWriter(cs))
                                {
                                    swEncrypt.Write(ClearText);
                                }

                                StrRet = Convert.ToBase64String(ms.ToArray());
                            }

                        }
                    };
                }
            }
            catch (Exception ex)
            {
                var log = new Log();
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "CustomerLockFunctions";
                log.Save().DiscardTask();

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
            String RemoteIP = req.HttpContext.Connection.RemoteIpAddress.ToString(); //  + ":" + req.HttpContext.Connection.RemotePort.ToString();

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

        public static string DecryptAES(string base64Cipher, Byte[] KeyBytes, Byte[] IVBytes)
        {
            string StrRet = "";
            try
            {
                Byte[] cipherBytes = Convert.FromBase64String(base64Cipher.Replace(' ', '+'));
                //Byte[] cipherBytes2 = Convert.FromBase64String(base64Cipher);
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.BlockSize = 128;
                    aes.KeySize = 256;
                    aes.Key = KeyBytes;
                    aes.IV = IVBytes;


                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                StrRet = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var log = new Log();
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "CustomerLockFunctions";
                log.Save().DiscardTask();

            }

            return StrRet;
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

        public static async Task<IG_Epic> Get_IG_Epic(Database the_db, string epicName)
        {
            // find the data
            IG_Epic epic = new IG_Epic();
            try
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
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log();
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Epic/Get";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();
            }

            return (epic);
        }

        public static List<clsEpicList> GetEpicList(string[] epics)
        {
            List<clsEpicList> ret = new List<clsEpicList>();

            foreach (string item in epics)
            {
                clsEpicList o = new clsEpicList();
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
            List<IG_Epic> epics = new List<IG_Epic>();
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
                Log log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Epic/Get";
                await log.Save();


            }
            catch (Exception e)
            {
                Log log = new Log();
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
            List<Candle> candles = new List<Candle>();
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
                    Log log = new Log();
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Epic/Get";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log();
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
            Settings settings = new Settings();
            try
            {
                Container container = the_db.GetContainer("Settings");

                //string sql = "Select TOP 1 * from c where IS_DEFINED(c.ErrorList)";
                string sql = "Select TOP 1 * from c ";
                QueryDefinition query = new QueryDefinition(sql);

                FeedIterator<Settings> queryA = container.GetItemQueryIterator<Settings>(query, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });


                foreach (Settings s in await queryA.ReadNextAsync())
                {
                    settings = s;

                }

            }
            catch (CosmosException de)
            {
                Log log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get Settings";
                await log.Save();

            }
            catch (Exception e)
            {
                Log log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get Settings";
                await log.Save();
            }

            return (settings);
        }

        public static async void SendBroadcast(string messageType, string messageValue)
        {
            string url = "";
            var igWebApiConnectionConfig = ConfigurationManager.GetSection("appSettings") as NameValueCollection;
            if (igWebApiConnectionConfig != null)
            {
                if (igWebApiConnectionConfig.Count > 0)
                {
                    url = igWebApiConnectionConfig["MessagingEndPoint"] ?? "";
                }
            }

            IGModels.ModellingModels.message newMsg = new IGModels.ModellingModels.message();
            newMsg.messageType = messageType;
            newMsg.messageValue = messageValue;

            HttpClient client = new HttpClient();
            url = url + "/broadcast";

            string msg = JsonConvert.SerializeObject(newMsg);
            HttpContent content = new StringContent(msg, Encoding.UTF8, "application/json");

            HttpResponseMessage response = client.PostAsync(url, content).Result;
            string results = response.Content.ReadAsStringAsync().Result;
        }
        public static async void SendMessage(string userid,string messageType, string messageValue)
        {
            string url = "";
            var igWebApiConnectionConfig = ConfigurationManager.GetSection("appSettings") as NameValueCollection;
            if (igWebApiConnectionConfig != null)
            {
                if (igWebApiConnectionConfig.Count > 0)
                {
                    url = igWebApiConnectionConfig["MessagingEndPoint"] ?? "";
                }
            }

            IGModels.ModellingModels.message newMsg = new IGModels.ModellingModels.message();
            newMsg.messageType = messageType;
            newMsg.messageValue = messageValue;

            HttpClient client = new HttpClient();
            url = url + "/sendmessage?userid=" + userid;

            string msg = JsonConvert.SerializeObject(newMsg);
            HttpContent content = new StringContent(msg, Encoding.UTF8, "application/json");

            HttpResponseMessage response = client.PostAsync(url, content).Result;
            string results = response.Content.ReadAsStringAsync().Result;
        }
        public static async void SaveLog(string logType, string logApp, string logMessage)
        {
            Log log = new Log();
            log.Log_Message = logMessage;
            log.Log_Type = logType;
            log.Log_App = logApp;
            await log.Save();
        }
    }
}
