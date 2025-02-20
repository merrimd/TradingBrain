using dto.endpoint.watchlists.retrieve;
using TradingBrain.Common;
using IGWebApiClient;
using IGWebApiClient.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lightstreamer.DotNet.Client;
using com.lightstreamer.client;
using System.Linq;
using dto.endpoint.auth.session.v2;
using dto.endpoint.application.operation;
using Microsoft.Azure.Cosmos;
using TradingBrain.Models;
using IGModels;
using static IGModels.ModellingModels.GetModelClass;
using IGCandleCreator.Models;
using System.Reflection.Metadata;
using IGModels.ModellingModels;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using Microsoft.Extensions.Azure;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using MimeKit;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Connections.Client;
using Newtonsoft.Json.Linq;
using dto.endpoint.search;
using System.Runtime.InteropServices;
using dto.endpoint.confirms;
using dto.endpoint.positions.create.otc.v1;
using System.Diagnostics.Eventing.Reader;
using dto.endpoint.positions.close.v1;
using dto.endpoint.positions.edit.v1;
using dto.endpoint.type;
using dto.endpoint.positions.get.otc.v1;
using Azure.Core.GeoJson;
using System.Xml.Xsl;
using System.Drawing;
using System.Runtime.CompilerServices;
using static DotNetty.Common.ThreadLocalPool;
using static System.Runtime.InteropServices.JavaScript.JSType;
using dto.endpoint.workingorders.create.v1;
using dto.endpoint.workingorders.delete.v1;
using dto.endpoint.workingorders.get.v1;
using log4net.Core;
using Azure.Storage;
using static TradingBrain.Models.clsCommonFunctions;



//using System.ComponentModel;

namespace TradingBrain.Models
{
    public   class MainApp
    {
        public StatusMessage currentStatus;
        public  IgRestApiClient? igRestApiClient;


        //public static IGStreamingApiClient? igStreamApiClient;
        //private SubscribedTableKey? _watchlistL1PricesSubscribedTableKey;
        //private SubscribedTableKey? _chartSubscribedTableKey;
        //private SubscribedTableKey? _tradeSubscribedTableKey;
        //private SubscribedTableKey _tradeSubscriptionStk;
        //private MarketDetailsTableListerner? _l1PricesSubscription;
        //private ChartTickTableListerner? _l1ChartSubscription;
        //private ChartCandleTableListerner? _chartSubscription;
        //private TradeSubscription _tradeSubscription;






        //public delegate void LightstreamerUpdateDelegate(int item, ItemUpdate values);
        //public delegate void LightstreamerStatusChangedDelegate(int cStatus, string status);
        public delegate void StopDelegate();


        public static bool LoggedIn { get; set; }
        public ObservableCollection<IgPublicApiData.AccountModel>? Accounts { get; set; }
        public static string? CurrentAccountId;
        public Database? the_db;
        public Database? the_app_db;

        public Microsoft.Azure.Cosmos.Container? the_container;
        public Microsoft.Azure.Cosmos.Container? the_chart_container;
        public Microsoft.Azure.Cosmos.Container? minute_container;
        public Microsoft.Azure.Cosmos.Container? TicksContainer;
        public Microsoft.Azure.Cosmos.Container? trade_container;
        public List<clsEpicList> EpicList;
        public string epicName;
        //public ObservableCollection<IgPublicApiData.ChartModel> ChartMarketData { get; set; }
        //public ObservableCollection<IgPublicApiData.TradeSubscriptionModel> TradeSubscriptions { get; set; }
        private long _lngTickCount;

        public clsChartUpdate currentTick { get; set; }
        public clsTradeUpdate currentTrade { get; set; }
        public clsTradeUpdate suppTrade { get; set; }
        public clsCandleUpdate currentCandle { get; set; }
        static System.Timers.Timer ti;
        public TradingBrainSettings tb;
        public Dictionary<string, string> TradeErrors = new Dictionary<string, string>();

        public List<requestedTrade> requestedTrades { get; set; }
        //public enum TradeSubscriptionType
        //{
        //    Opu = 0,
        //    Wou = 1,
        //    Confirm = 2
        //}
        //public ModelRequest? requestData { get; set; }
        public string modelID { get; set; }

        public GetModelClass model { get; set; }
        public ModelVars modelVar { get; set; }

        public HubConnection hubConnection { get; set; }
        private bool FirstConfirmUpdate = true;

        public TBStreamingClient tbClient;
        private bool isDirty = false;

        private string pushServerUrl;
        public string forceT;
        private string forceTransport = "no";
        public ConversationContext context;
        TradingBrainSettings firstTB;
        public int latestHour = 0;
        public bool marketOpen = false;
        public bool paused { get; set; }
        public bool pausedAfterNGL { get; set; }
        public string igAccountId { get; set; }

        public bool lastTradeDeleted { get; set; }
        public double lastTradeValue { get; set; }
        public double lastTradeSuppValue { get; set; }
        public bool lastTradeMaxQuantity { get; set; }
        public bool retryOrder { get; set; }
        public int retryOrderLimit { get; set; }
        public int retryOrderCount { get; set; }
        //public double currentSpread { get; set; }
        //public delegate void LightstreamerChartUpdateDelegate(int item, ItemUpdate values);
        public ModelVars setInitialModelVar()
        {
            //firstTB = await clsCommonFunctions.GetTradingBrainSettings(this.the_db, this.epicName);
                Task<TradingBrainSettings> tb = Task.Run<TradingBrainSettings>(async () => await clsCommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId));
                //return tb.Result;

            return tb.Result.lastRunVars;

        }

        public    MainApp(Database db,Database appDb, Container container, Container chart_container, string epic, Container _minute_container, Container _TicksContainer, Container _trade_container)
        {
            try
            {

                tbClient = null;
                forceT = forceTransport;
                currentStatus = new StatusMessage();
                requestedTrades = new List<requestedTrade>();
                ////////////////////////////////////
                // Get account id from app config //
                ////////////////////////////////////
                string region = IGModels.clsCommonFunctions.Get_AppSetting("region");
                igAccountId = IGModels.clsCommonFunctions.Get_AppSetting("accountId." + region);

                retryOrderCount = 0;
                retryOrder = false;
                retryOrderLimit = 10;


                //igAccountId = tbClient.client.connectionDetails.User;
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "MainApp", "TB Started - " + igAccountId, appDb);

                lastTradeDeleted = false;
                lastTradeSuppValue = 0;
                lastTradeValue = 0;
                paused = false;
                pausedAfterNGL = false;
                epicName = epic;
                //Test the messaging
                setupTradeErrors();
                setupMessaging();
                //currentTrade = new clsTradeUpdate();
                currentTick = new clsChartUpdate();
                currentCandle = new clsCandleUpdate();
                LoggedIn = false;
                Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();
                //TradeSubscriptions = new ObservableCollection<IgPublicApiData.TradeSubscriptionModel>();
                CurrentAccountId = "";
                _lngTickCount = 0;
                the_db = db;
                the_app_db = appDb;
                the_container = container;
                the_chart_container = chart_container;
                minute_container = _minute_container;
                TicksContainer = _TicksContainer;
                trade_container = _trade_container;
                this.EpicList = new List<clsEpicList>();
                //this.ChartMarketData = new ObservableCollection<IgPublicApiData.ChartModel>();

                tb = new TradingBrainSettings();
                model = new GetModelClass();
                model.the_db = db;
                model.the_app_db = appDb;
                modelVar = new ModelVars();
                //modelVar.optimizeRun = false;
                modelVar = setInitialModelVar().DeepCopy();
               model.modelVar = modelVar;
                model.exchangeClosedDates =  IGModels.clsCommonFunctions.GetExchangeClosedDates(epicName, the_app_db).Result;
                // set the region we are in

                model.region = region;
                if (model.region == "")
                {
                    model.region = IGModels.clsCommonFunctions.Get_AppSetting("environment");
                    if (model.region == "demo") { model.region = "test"; }
                    if (model.region == "") { model.region = "live"; }
                }
                latestHour = DateTime.UtcNow.Hour;

                //TradingBrainSettings firstTB =  clsCommonFunctions.GetTradingBrainSettings(this.the_db, this.epicName);

                model.index = 30;
                model.doLongs = true;
                model.doShorts = true;
                model.logModel = true;

                model.thisModel = new modelInstance();
                //thisModel.ii = iteration;
                modelID = System.Guid.NewGuid().ToString();

                model.modelLogs.modelRunID = modelID;
                model.modelLogs.modelRunDate = DateTime.UtcNow;
                model.TBRun = true;
                marketOpen =  IGModels.clsCommonFunctions.IsTradingOpen(model.modelLogs.modelRunDate, model.exchangeClosedDates).Result;   //IGModels.clsCommonFunctions.IsTradingOpen(model.modelLogs.modelRunDate);
                clsCommonFunctions.AddStatusMessage($"Market open = {marketOpen}", "INFO");

                clsCommonFunctions.AddStatusMessage(  "Model Run ID = " + modelID, "INFO");
        
                currentStatus.startDate = model.modelLogs.modelRunDate;
                currentStatus.modelRunID = modelID;
                currentStatus.status = "running";
                currentStatus.epicName = this.epicName;
                currentStatus.doSuppTrades = model.doSuppTrades;

                object v = ConfigurationManager.GetSection("appSettings");
                if (v != null)
                {
                    NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;

                    if (igWebApiConnectionConfig != null)
                    {
                        //string region = igWebApiConnectionConfig["region"] ?? "test";
                        //igAccountId = igWebApiConnectionConfig["accountId." + region] ?? "";
                        string env = igWebApiConnectionConfig["environment"] ?? "DEMO";
                        clsCommonFunctions.AddStatusMessage(env, "INFO");
                        SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();

                        igRestApiClient = new IgRestApiClient(env, smartDispatcher);

                        // keep this bit
                        string[] epics = { epic };
                        this.EpicList = clsCommonFunctions.GetEpicList(epics);

                        // Start the lightstreamer bits in a new thread

                        clsCommonFunctions.AddStatusMessage("Starting lightstreamer in a new thread", "INFO");
                        Thread t = new Thread(new ThreadStart(StartLightstreamer));
                        t.Start();


                        //Broadcast message that we are running
                        currentStatus.status = "running";
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db);


                        //Console.ReadLine();
                        //StartTimer
                        ti = new System.Timers.Timer();
                        ti.AutoReset = false;
                        ti.Elapsed += new System.Timers.ElapsedEventHandler(RunCode);
                        ti.Interval = GetInterval();
                        ti.Start();
                    }

                }





            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "MainApp";
                log.Epic = this.epicName;
                log.Save();
            }
        }

        private void StartLightstreamer()
        {

            tbClient = new TBStreamingClient(
                pushServerUrl,
                forceT,
                this,
                new Delegates.LightstreamerUpdateDelegate(OnLightstreamerUpdate),
                new Delegates.LightstreamerStatusChangedDelegate(OnLightstreamerStatusChanged));


            tbClient.Start();
            
        }
        public void OnLightstreamerUpdate(int item, ItemUpdate values)
        {
            //dataGridview.SuspendLayout();

            if (this.isDirty)
            {
                this.isDirty = false;
                // CleanGrid();
            }


        }

        public void OnLightstreamerStatusChanged(int cStatus, string status)
        {
            //statusLabel.Text = status;
            var a = 1;


        }

        public async Task<string> PlaceOrder(string direction, double quantity, double stopLoss, string accountId, decimal dealPrice)
        {
            string ret = "";
            try
            {
                bool newsession = false;
                clsCommonFunctions.AddStatusMessage($"Placing new order = direction = {direction}, quantity = {quantity}, stopLoss = {stopLoss}, dealPrice = {dealPrice}, accountId = {accountId}", "INFO");
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "PlaceOrder", "Placing order - direction = " + direction + ", quantity = " + quantity + ", stopLoss = " + stopLoss + ", accountID = " + accountId, the_app_db);

                dto.endpoint.workingorders.create.v2.CreateWorkingOrderRequest pos = new dto.endpoint.workingorders.create.v2.CreateWorkingOrderRequest();
          
                pos.epic = this.epicName;
                pos.expiry = "DFB";
                if (direction.ToUpper() == "LONG" || direction.ToUpper() == "BUY")
                {
                    pos.direction = "BUY";
                }
                else
                {
                    pos.direction = "SELL";
                }
                pos.size = decimal.Round((decimal)quantity, 2, MidpointRounding.AwayFromZero);
                pos.timeInForce = "GOOD_TILL_CANCELLED";
                pos.type = "STOP";
                pos.guaranteedStop = false;
                pos.stopDistance = Convert.ToDecimal(stopLoss);
                pos.level = dealPrice;
                pos.currencyCode = "GBP";

                IgResponse<CreateWorkingOrderResponse> req = await igRestApiClient.createWorkingOrderV2(pos);
                if (req != null)
                {
                    ret = req.Response.dealReference;
                    clsCommonFunctions.AddStatusMessage("Place order - " + direction + " - Status: " + req.StatusCode + " - account = " + accountId + " - deal reference = " + req.Response.dealReference, "INFO");
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "PlaceOrder", "Place order - " + direction + " - Status: " + req.StatusCode + " - AccountId: " + accountId + " - deal reference = " + req.Response.dealReference, the_app_db);
                    if (req.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    tbClient.ConnectToRest();
                    req = await igRestApiClient.createWorkingOrderV2(pos);
                    if (req != null)
                    {
                        ret = req.Response.dealReference;
                        clsCommonFunctions.AddStatusMessage("Place order - " + direction + " - Status: " + req.StatusCode + " - account = " + accountId + " - deal reference = " + req.Response.dealReference, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "PlaceOrder", "Place order - " + direction + " - Status: " + req.StatusCode + " - AccountId: " + accountId + " - deal reference = " + req.Response.dealReference, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "PlaceOrder";
                await log.Save();
            }

            return ret;
        }
        public async void DeleteOrder(string direction, double quantity, string dealID)
        {
            try
            {
                bool newsession = false;
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "DeleteOrder", "Deleting order", the_app_db);

                // create new pos which is basically a blank object
                dto.endpoint.workingorders.delete.v1.DeleteWorkingOrderRequest pos = new dto.endpoint.workingorders.delete.v1.DeleteWorkingOrderRequest();


                IgResponse<DeleteWorkingOrderResponse> ret = await igRestApiClient.deleteWorkingOrder(dealID,pos);

                if (ret != null)
                {
                    clsCommonFunctions.AddStatusMessage("Delete order - " + direction + " - Status: " + ret.StatusCode, "INFO");
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "DeleteOrder", "Delete order - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    tbClient.ConnectToRest();
                    ret = await igRestApiClient.deleteWorkingOrder(dealID, pos);
                    if (ret != null)
                    {
                        clsCommonFunctions.AddStatusMessage("Delete order - " + direction + " - Status: " + ret.StatusCode, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "DeleteOrder", "Delete order - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    }
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "CloseDeal";
                await log.Save();
            }
            //IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);
        }
        public async Task<string> PlaceDeal(string direction, double quantity, double stopLoss, string accountId, bool setStopLevel = false)
        {
            string ret = "";
            try
            {
                
                bool newsession = false;
                clsCommonFunctions.AddStatusMessage($"Placing new deal = direction = {direction}, quantity = {quantity}, stopLoss = {stopLoss}, accountId = {accountId} ", "INFO");
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info",  "PlaceDeal", "Placing deal - direction = " + direction + ", quantity = " + quantity + ", stopLoss = " + stopLoss + ", accountID = " + accountId, the_app_db);

                dto.endpoint.positions.create.otc.v1.CreatePositionRequest pos = new dto.endpoint.positions.create.otc.v1.CreatePositionRequest();
                //pos.epic = "IX.D.NASDAQ.CASH.IP";
                pos.epic = this.epicName;
                pos.expiry = "DFB";
                if (direction == "long")
                {
                    pos.direction = "BUY";
                }
                else
                {
                    pos.direction = "SELL";
                }
                pos.size = decimal.Round((decimal)quantity, 2, MidpointRounding.AwayFromZero);
                pos.orderType = "MARKET";
                pos.guaranteedStop = false;
                if (setStopLevel)
                {
                    pos.stopLevel = Convert.ToDecimal(stopLoss);
                }
                else
                {
                    pos.stopDistance = Convert.ToDecimal(stopLoss);
                }
                
                //pos.limitDistance = 50;
                pos.forceOpen = true;
                pos.currencyCode = "GBP";


                //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);

                IgResponse<CreatePositionResponse> resp = await igRestApiClient.createPositionV1(pos);
                if (resp != null)
                {
                    ret = resp.Response.dealReference;
                    clsCommonFunctions.AddStatusMessage("Place deal - " + direction + " - Status: " + resp.StatusCode + " - account = " + accountId + " - deal reference = " + resp.Response.dealReference, "INFO" );
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "PlaceDeal", "Place deal - " + direction + " - Status: " + resp.StatusCode + " - AccountId: " + accountId, the_app_db);
                    if (resp.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    tbClient.ConnectToRest();
                    resp = await igRestApiClient.createPositionV1(pos);
                    if (ret != null)
                    {
                        ret = resp.Response.dealReference;
                        clsCommonFunctions.AddStatusMessage("Place deal - " + direction + " - Status: " + resp.StatusCode + " - account = " + accountId + " - deal reference = " + resp.Response.dealReference, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "PlaceDeal", "Place deal - " + direction + " - Status: " + resp.StatusCode + " - AccountId: " + accountId, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "PlaceDeal";
                await log.Save();
            }
            return ret;
        }
        public async void CloseDeal(string direction, double quantity, string dealID)
        {
            try
            {
                bool newsession = false;
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Closing deal", the_app_db);
                dto.endpoint.positions.close.v1.ClosePositionRequest pos = new dto.endpoint.positions.close.v1.ClosePositionRequest();

                if (direction == "long")
                {
                    pos.direction = "SELL";
                }
                else
                {
                    pos.direction = "BUY";
                }
                pos.size = decimal.Round((decimal)quantity, 2, MidpointRounding.AwayFromZero);
                pos.orderType = "MARKET";
                pos.dealId = dealID;
                //pos.guaranteedStop = true;
                //pos.stopDistance = Convert.ToDecimal(stopLoss);
                //pos.limitDistance = 50;
                //pos.forceOpen = true;
                //pos.currencyCode = "GBP";


                //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);
                IgResponse<ClosePositionResponse> ret = await igRestApiClient.closePosition(pos);

                if (ret != null)
                {
                    clsCommonFunctions.AddStatusMessage("Close deal - " + direction + " - Status: " + ret.StatusCode, "INFO");
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Close deal - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    tbClient.ConnectToRest();
                    ret = await igRestApiClient.closePosition(pos);
                    if (ret != null)
                    {
                        clsCommonFunctions.AddStatusMessage("Close deal - " + direction + " - Status: " + ret.StatusCode, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Close deal - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    }
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "CloseDeal";
                await log.Save();
            }
            //IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);
        }
        public async void EditDeal(double stopLoss, string dealID)
        {
            try
            {
                clsCommonFunctions.AddStatusMessage("Editing deal. StopLoss = " + stopLoss + " - dealId = " + dealID, "INFO");
                bool newsession = false;
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "EditDeal", "Editing deal - " + dealID, the_app_db);
                dto.endpoint.positions.edit.v1.EditPositionRequest pos = new dto.endpoint.positions.edit.v1.EditPositionRequest();

                pos.stopLevel = Convert.ToDecimal(stopLoss);
               

                IgResponse<EditPositionResponse> ret = await igRestApiClient.editPositionV1(dealID, pos);

                if (ret != null)
                {
                    clsCommonFunctions.AddStatusMessage("Edit deal - Status: " + ret.StatusCode, "INFO");
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "EditDeal", "Edit deal - Status: " + ret.StatusCode, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }
                if (newsession)
                {
                    clsCommonFunctions.AddStatusMessage("Trying to reconnect to REST", "INFO");
                    tbClient.ConnectToRest();
                    ret = await igRestApiClient.editPositionV1(dealID, pos);
                    if (ret != null)
                    {
                        clsCommonFunctions.AddStatusMessage("Edit deal - Status: " + ret.StatusCode, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "EditDeal", "Edit deal - Status: " + ret.StatusCode, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "EditDeal";
                await log.Save();
            }
        }
        public async void EditOrder(decimal orderLevel, double stopDistance, string dealID)
        {
            
            try
            {
                clsCommonFunctions.AddStatusMessage($"Editing order. orderLevel = {orderLevel}, stopDistance = {stopDistance} - dealId = {dealID}", "INFO");
                bool newsession = false;
                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "Editorder", "Editing order - " + dealID, the_app_db);
                dto.endpoint.workingorders.edit.v1.EditWorkingOrderRequest pos = new dto.endpoint.workingorders.edit.v1.EditWorkingOrderRequest();
                pos.stopDistance = Convert.ToDecimal(stopDistance);
                pos.level = orderLevel;
                pos.timeInForce = "GOOD_TILL_CANCELLED";
                pos.type = "STOP";

                IgResponse<dto.endpoint.workingorders.edit.v1.EditWorkingOrderResponse> ret = await igRestApiClient.editWorkingOrderV1(dealID, pos);

                if (ret != null)
                {
                    clsCommonFunctions.AddStatusMessage("Edit order - Status: " + ret.StatusCode, "INFO");
                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "EditOrder", "Edit order - Status: " + ret.StatusCode, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }
                if (newsession)
                {
                    clsCommonFunctions.AddStatusMessage("Trying to reconnect to REST", "INFO");
                    tbClient.ConnectToRest();
                    ret = await igRestApiClient.editWorkingOrderV1(dealID, pos);
                    if (ret != null)
                    {
                        clsCommonFunctions.AddStatusMessage("Edit order - Status: " + ret.StatusCode, "INFO");
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "EditOrder", "Edit order - Status: " + ret.StatusCode, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "EditDeal";
                await log.Save();
            }
        }

        double GetInterval()
        {
            DateTime now = DateTime.Now;
            
            // testOffset will move it to 10 seconds past the minute to ensure it doesn't interfere with live
            int testOffset = 0;
            if (model.region == "test")
            {
                testOffset = 5;
            }
            return ((now.Second > 30 ? 120 : 60) - now.Second + testOffset) * 1000 - now.Millisecond;
        }

        async void RunCode(object sender, System.Timers.ElapsedEventArgs e)
        {
       
            bool liveMode = true;
            bool marketOpen = false;

            DateTime dtNow = DateTime.UtcNow;
            DateTime _startTime;

            // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
            int seconds = dtNow.Second;
            if (seconds < 59)
            {
                _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
            }
            else
            {
                _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0);
            }


            DateTime _endTime = _startTime;


            if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
            {
                // Check if the market is currently open. If it is not then skip till next time.
                //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
                marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates).Result;
                if (marketOpen)
                {
                    tbClient.FirstConfirmUpdate = false;
                    string param = "";

                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Running code");
                    clsCommonFunctions.AddStatusMessage( " ------------------", "INFO");
                    clsCommonFunctions.AddStatusMessage(  " - Run Started ", "INFO");
                    clsCommonFunctions.AddStatusMessage( " ------------------", "INFO");
                    clsCommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG");
                    var watch = new System.Diagnostics.Stopwatch();
                    var bigWatch = new System.Diagnostics.Stopwatch();
                    bigWatch.Start();
                    try
                    {
                        //watch.Start();
  

                        this.tb = await clsCommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId);

                        clsCommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG");


                        // If the trade has just been deleted then sort out the CFL

                        if (lastTradeDeleted)
                        {
                            try
                            {
                                clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG");
                                double nettPosition = lastTradeValue + lastTradeSuppValue;
                                clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG");

                                if (nettPosition <= 0)
                                {
                                    model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
                                }
                                else
                                {
                                    model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
                                    if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
                                    model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
                                }

                                tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
                                tb.lastRunVars.currentGain = model.modelVar.currentGain;

                                // check to see if the trade just finished lost at max quantity, if so then we need to reset the vars
                                clsCommonFunctions.AddStatusMessage($"checking if reset required - lastTradeMaxQuantity = {lastTradeMaxQuantity}", "DEBUG");
                                if (lastTradeMaxQuantity)
                                {
                                    clsCommonFunctions.AddStatusMessage($"old lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG");
                                    tb.lastRunVars.currentGain = Math.Max(tb.lastRunVars.currentGain - model.modelVar.carriedForwardLoss,0);
                                    tb.lastRunVars.carriedForwardLoss = 0;
                                    tb.lastRunVars.quantity = tb.lastRunVars.minQuantity;
                                    tb.lastRunVars.counter = 0;
                                    tb.lastRunVars.maxQuantity = tb.lastRunVars.minQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    model.modelVar.currentGain = tb.lastRunVars.currentGain;
                                    model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
                                    model.modelVar.quantity = tb.lastRunVars.quantity;
                                    model.modelVar.counter = tb.lastRunVars.counter;
                                    model.modelVar.maxQuantity = tb.lastRunVars.maxQuantity;

                                    clsCommonFunctions.AddStatusMessage($"new lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG");
                                }
                                await tb.SaveDocument(the_app_db);

                                clsCommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG");
                            }

                            catch(Exception ex)
                            {
                                clsCommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR"); 
                            }

                            lastTradeDeleted = false;
                            lastTradeValue = 0;
                            lastTradeSuppValue = 0;
                            lastTradeMaxQuantity = false;
                        }
                        //watch.Stop();
                        //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - GetSettings - Time taken = " + watch.ElapsedMilliseconds);

                        model.doSuppTrades = tb.doSuppTrades;
                        clsCommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG");
                        model.thisModel.inputs = this.tb.runDetails.inputs.DeepCopy();
                        model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        model.thisModel.matchProTrend = false;
                        model.modelVar.counterVar = model.thisModel.counterVar;
                        //model.modelVar = tb.lastRunVars;

                        model.startTime = dtNow;
                        model.modelRunID = modelID;
                        if (model.modelVar.quantity == 0)
                        {
                            //model.modelVar.baseQuantity = tb.runDetails.quantity;
                            //model.modelVar.startingQuantity = tb.runDetails.quantity;
                            //model.modelVar.startingQuantity = tb.lastRunVars.startingQuantity;

                            model.modelVar.minQuantity = tb.runDetails.quantity;
                            model.modelVar.quantity = tb.runDetails.quantity;
                        }

                        //model.counterVar = tb.runDetails.counterVar;
                        currentStatus.inputs = tb.runDetails.inputs;
                        currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        //currentStatus.quantity = model.modelVar.quantity;
                        currentStatus.quantity = tb.lastRunVars.minQuantity;


                        // Here is where I can force it to run for a full day, rather than just for the current minute. Just to test like.

                        if (Environment.GetCommandLineArgs().Length >= 3)
                        {
                            param = Environment.GetCommandLineArgs()[2];
                        }
                        if (param == "DEBUG")
                        {
                            _startTime = new DateTime(2024, 11, 11, 16, 12, 00);
                            _endTime = new DateTime(2024, 11, 12, 14, 30, 00);
                            liveMode = false;
                        }
                        modelInstanceInputs thisInput = new modelInstanceInputs();
                        //while (_startTime <= _endTime)
                        //{
                            bigWatch.Restart();


                            /////////////////////////////////////////////////////////
                            // using the candle time determine which inputs to use //
                            /////////////////////////////////////////////////////////
                            //thisInput = IGModels.clsCommonFunctions.GetInputs(tb.runDetails.inputs, _startTime);

                            double thisSpread =Math.Round( Math.Abs((double)currentTick.Offer - (double)currentTick.Bid),1);
                            clsCommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
                            thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadv2(tb.runDetails.inputs, thisSpread);

                            //Create the current candle
                            // only create a new min record if we are in live
                            // 
                            // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
                            // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
                            
                            bool createMinRecord = liveMode;
                            if (model.region == "test") { createMinRecord = false; }
                            model.candles.currentCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime, epicName, minute_container, TicksContainer, false, createMinRecord,the_app_db,model.exchangeClosedDates);

                            // Check to see if we have prev and prev2 candles already. If not (i.e. first run) then go get them.
                            if (model.candles.prevCandle.candleStart == DateTime.MinValue)
                            {
                                model.candles.prevCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-1), epicName, minute_container, TicksContainer, false, false,the_app_db,model.exchangeClosedDates);

                            }
                            if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
                            {
                                model.candles.prevCandle2 = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-2), epicName, minute_container, TicksContainer, false, false,the_app_db,model.exchangeClosedDates);
                            }
                           
                              DateTime getStartDate = await model.getPrevMAStartDate(model.candles.currentCandle.candleStart);

                            IG_Epic epic = new IG_Epic(epicName);
                            clsMinuteCandle prevMa = await Get_MinuteCandle(the_db, minute_container, epic, getStartDate);
                            model.candles.prevMACandle.mA30MinTypicalLongClose = prevMa.MovingAverages30Min[thisInput.var3 - 1].movingAverage.Close;
                            model.candles.prevMACandle.mA30MinTypicalShortClose = prevMa.MovingAverages30Min[thisInput.var13 - 1].movingAverage.Close;

                            clsCommonFunctions.AddStatusMessage($"values before run         - buyLong={ model.buyLong}, buyShort={model.buyShort}, sellLong={ model.sellLong}, sellShort={ model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onSuppTrade = {model.onSuppTrade}, onMarket={ model.onMarket}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"values before  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");

                            clsCommonFunctions.AddStatusMessage($"currentCandle.ma30MinTypicalLongClose:{model.candles.currentCandle.mA30MinTypicalLongClose} currentCandle.ma30MinTypicalLongClose:{model.candles.currentCandle.mA30MinTypicalShortClose}", "DEBUG");

                            clsCommonFunctions.AddStatusMessage($"prevCandle.ma30MinTypicalLongClose:{model.candles.prevCandle.mA30MinTypicalLongClose} prevCandle.ma30MinTypicalLongClose:{model.candles.prevCandle.mA30MinTypicalShortClose}", "DEBUG");


                            clsCommonFunctions.AddStatusMessage($"prevCandle2.ma30MinTypicalLongClose:{model.candles.prevCandle2.mA30MinTypicalLongClose} prevCandle2.ma30MinTypicalLongClose:{model.candles.prevCandle2.mA30MinTypicalShortClose}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"prevMACandle.ma30MinTypicalLongClose:{model.candles.prevMACandle.mA30MinTypicalLongClose} prevMACandle.ma30MinTypicalLongClose:{model.candles.prevMACandle.mA30MinTypicalShortClose}", "DEBUG");


                        // Check if we are still on market but were unable to set up the order
                        if (model.onMarket && retryOrder)
                        {
                            if (model.thisModel.currentTrade.attachedOrder != null)
                            {
                                clsCommonFunctions.OrderValues orderValues = new clsCommonFunctions.OrderValues();
                                string newOrderDirection = "";
                                if (currentTrade.direction.ToUpper() == "LONG" || currentTrade.direction.ToUpper() == "BUY")
                                {
                                    newOrderDirection = "BUY";
                                }
                                else { newOrderDirection = "SELL"; }

                                orderValues.SetOrderValues(newOrderDirection, this);
                                clsCommonFunctions.AddStatusMessage($"Retrying placing new order - direction:{newOrderDirection}, stopDistance:{orderValues.stopDistance}, level:{orderValues.level}", "INFO");
                                requestedTrade reqTrade = new requestedTrade();
                                reqTrade.dealType = "ORDER";
                                reqTrade.dealReference = await PlaceOrder(newOrderDirection, orderValues.quantity, orderValues.stopDistance, igAccountId, orderValues.level);
                                requestedTrades.Add(reqTrade);
                            }
                        }


                            //model.RunProTrendCodeV2(model.candles);
                            model.RunProTrendCodeV3(model.candles);

                            clsCommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onSuppTrade = {model.onSuppTrade}, onMarket={model.onMarket}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");


                            clsCommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG"); 
                            clsCommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG"); 
                            clsCommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}","DEBUG");
                            clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG");
                            clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG");


                            if (this.currentTrade != null) { clsCommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO"); }
                            if (this.suppTrade != null) { clsCommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO"); }

                            //model.sellShort = true;

                            if (param != "DEBUG")
                            {

                                double targetVar = thisInput.targetVarInput / 100 + 1;
                                double targetVarShort = thisInput.targetVarInputShort / 100 + 1;

                                //////////////////////////////////////////////////////////////////////////////////////////////
                                // Check for changes to stop limit that would mean the current trade has to end immediately //
                                //////////////////////////////////////////////////////////////////////////////////////////////

                                double currentStop = 0;
                                double newStop = 0;
                                double currentPrice = 0;

                                if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
                                {
                                     currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
                                     newStop =   IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)  ;
                                     currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

                                    clsCommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG");
                                    clsCommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG");


                                    if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
                                    {
                                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + "+ currentPrice + " is now lower than the new stop.", the_app_db);
                                        model.sellLong = true;
                                    }

                                }
                                if (model.shortOnMarket && model.modelVar.breakEvenVar == 0)
                                {
                                    currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue));
                                    newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
                                    currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

                                    clsCommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG");
                                    clsCommonFunctions.AddStatusMessage($"[LONG] Current stop > newStop = {currentStop > newStop},  currentPrice > newStop = {currentPrice > newStop}, currentPrice < currentStop {currentPrice < currentStop}  ", "DEBUG");


                                    if (currentStop > newStop && currentPrice > newStop && currentPrice < currentStop)
                                    {
                                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "buying short because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now higher than the new stop.", the_app_db);
                                        model.buyShort = true;
                                    }
                                }


                                // Check if target price has changed when an attached order is set but the supp trade hasn't started yet. //

                                if (model.onMarket && this.currentTrade != null && model.onSuppTrade == false && model.thisModel.currentTrade.attachedOrder != null)
                                {
                                    OrderValues orderValues = new OrderValues();
                                    string direction = "";
                                    if (model.longOnmarket)
                                    {
                                        direction = "buy";
                                    }
                                    if (model.shortOnMarket)
                                    {
                                        direction = "sell";
                                    }
                                    orderValues.SetOrderValues(direction, this);
                                    clsCommonFunctions.AddStatusMessage($"Checking order level {orderValues.level} = attached order level {model.thisModel.currentTrade.attachedOrder.orderLevel}", "DEBUG");
                                    if (orderValues.level != model.thisModel.currentTrade.attachedOrder.orderLevel)
                                    {
                                        // get stuff done :- specifically get the order values 
                                        

                                        if (orderValues.stopDistance > 0 && orderValues.level > 0)
                                        {
                                            EditOrder(orderValues.level, orderValues.stopDistance, model.thisModel.currentTrade.attachedOrder.dealId);
                                        }
                                        

                                    }
                                }


                                //////////////////////////////////////////////////////////////////

                                if (model.buyLong && this.currentTrade == null)
                                {
                                    clsCommonFunctions.AddStatusMessage("BuyLong activated", "INFO");
                                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
                                    model.stopLossVar = (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
                                    requestedTrade reqTrade = new requestedTrade();
                                    reqTrade.dealType = "POSITION";                                    
                                    reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar,this.igAccountId);
                                    requestedTrades.Add(reqTrade);
                                }
                                else
                                {
                                    if (model.sellLong)
                                    {
                                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
                                        clsCommonFunctions.AddStatusMessage("SellLong activated", "INFO");
                                        CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);

                                        if (model.doSuppTrades && model.onSuppTrade)
                                        {
                                            TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "SellLong Supplementary", the_app_db);
                                            clsCommonFunctions.AddStatusMessage("SellLong Supplementary activated", "INFO");
                                            CloseDeal("long", (double)this.suppTrade.size, this.suppTrade.dealId);
                                        }
                                    }
                                }

                                if (model.sellShort && this.currentTrade == null  )
                                {
                                    clsCommonFunctions.AddStatusMessage("SellShort activated", "INFO");
                                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);
                                    model.stopLossVar = (double)thisInput.var5 * Math.Abs((double)targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
                                    requestedTrade reqTrade = new requestedTrade();
                                    reqTrade.dealType = "POSITION";
                                    reqTrade.dealReference = await PlaceDeal("short", model.modelVar.quantity, model.stopLossVar, this.igAccountId);
                                    requestedTrades.Add(reqTrade);
                                }
                                else
                                {
                                    if (model.buyShort)
                                    {
                                        clsCommonFunctions.AddStatusMessage("BuyShort activated", "INFO");
                                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyShort)", the_app_db);
                                        CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId);

                                        if (model.doSuppTrades && model.onSuppTrade)
                                        {
                                            clsCommonFunctions.AddStatusMessage("BuyShort Supplementary activated", "INFO");
                                            TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyShort Supplementary", the_app_db);
                                            CloseDeal("short", (double)this.suppTrade.size, this.suppTrade.dealId);
                                        }
                                    }
                                }


                                if (model.longOnmarket)
                                {
                                    clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG");

                                    if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                    {



                                        //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                        decimal? currentStopLevel = this.currentTrade.stopLevel;
                                        if (model.modelVar.breakEvenVar == 1)
                                        {
        

                                            this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                            clsCommonFunctions.AddStatusMessage($"EditLong activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                            TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal - BREAKEVEN", the_app_db);
                                            EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId);

                                            //If on a supp trade then set that trades sl to be the same as the current trade
                                            if (model.onSuppTrade && this.suppTrade != null)
                                            {
                                                clsCommonFunctions.AddStatusMessage($"EditLong SUPP activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Long Deal BREAKEVEN", the_app_db);
                                                EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId);
                                            }
                                        }
                                        else

                                        {

                                            if (model.onSuppTrade)
                                            {
                                                // on a supp trade so make the current trade have the same stop loss value so they close at the same time.
                                                //this.currentTrade.stopLevel = model.suppStopLossVar;
                                                this.model.thisModel.currentTrade.stopLossValue = (double)this.suppTrade.stopLevel;
                                                this.currentTrade.stopLevel = this.suppTrade.stopLevel;

                                                clsCommonFunctions.AddStatusMessage($"EditLong Long SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Long Deal ", the_app_db);
                                                EditDeal((double)this.currentTrade.stopLevel, this.currentTrade.dealId);
                                            }
                                            else
                                            {
                                                this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
                                                clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
                                                EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId);
                                            }
                                        }

                                    }
                                }
                                if (model.shortOnMarket)
                                {
                                    clsCommonFunctions.AddStatusMessage($"[SHORT] Check if sellPrice ({model.thisModel.currentTrade.sellPrice}) + stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG");

                                    if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                    {
                                        clsCommonFunctions.AddStatusMessage("EditShort activated", "INFO");
                                        TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal", the_app_db);


                                        //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                        decimal? currentStopLevel = this.currentTrade.stopLevel;

                                        if (model.modelVar.breakEvenVar == 1)
                                        {
                                 
                                            this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
                                            clsCommonFunctions.AddStatusMessage($"EditShort activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                            TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal - BREAKEVEN", the_app_db);
                                            EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId);

                                            //If on a supp trade then set that trades sl to be the same as the current trade
                                            if (model.onSuppTrade && this.suppTrade != null)
                                            {
                                                clsCommonFunctions.AddStatusMessage($"EditShort SUPP activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Short SUPP Deal - BREAKEVEN", the_app_db);
                                                EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId);
                                            }
                                        }
                                        else
                                        {
                                            if (model.onSuppTrade)
                                            {
                                                // on a supp trade so make the current trade have the same stop loss value so they close at the same time.
                                                //this.currentTrade.stopLevel = model.suppStopLossVar;
                                                this.model.thisModel.currentTrade.stopLossValue = (double)this.suppTrade.stopLevel;
                                                this.currentTrade.stopLevel = this.suppTrade.stopLevel;

                                                clsCommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
                                                EditDeal((double)this.currentTrade.stopLevel, this.currentTrade.dealId);
                                            }
                                            else
                                            {
                                                this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                                clsCommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO");
                                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
                                                EditDeal((double)model.thisModel.currentTrade.sellPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId);
                                            }
                                        }

                                    }
                                }


                            }
                            try
                            {
                                if (model.thisModel.currentTrade != null)
                                {
                                    if (model.thisModel.currentTrade.targetPrice != 0)
                                    {
                                        await model.thisModel.currentTrade.SaveDocument(this.trade_container);
                                    };
                                }
                                if (model.thisModel.suppTrade != null)
                                {
                                    if (model.thisModel.suppTrade.targetPrice != 0)
                                    {
                                        await model.thisModel.suppTrade.SaveDocument(this.trade_container);
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                Log log = new Log(the_app_db);
                                log.Log_Message = ex.ToString();
                                log.Log_Type = "Error";
                                log.Log_App = "RunCode";
                                await log.Save();

                            }

                            //reset any deal variables that could have been placed by the RunCode
                            model.buyLong = false;
                            model.buyShort = false;
                            model.sellLong = false;
                            model.sellShort = false;
                            model.buyLongSupp = false;
                            model.buyShortSupp = false;
                            model.sellLongSupp = false;
                            model.sellShortSupp = false;

                            if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
                            {

                            }


                            if (model.modelLogs.logs.Count() > 0)
                            {
                                ModelLog log = new ModelLog();
                                log = model.modelLogs.logs[0];
                                log.modelRunID = modelID;
                                log.runDate = _startTime;
                                log.id = System.Guid.NewGuid().ToString();
                                if (model.onMarket)
                                {
                                    currentStatus.onMarket = true;
                                    if (model.longOnmarket)
                                    {
                                        currentStatus.tradeType = "Long";
                                    }
                                    if (model.shortOnMarket)
                                    {
                                        currentStatus.tradeType = "Short";
                                    }
                                    currentStatus.target = model.thisModel.currentTrade.targetPrice;
                                    currentStatus.count = model.thisModel.currentTrade.count;
                           
                                }
                                else
                                {
                                    currentStatus.onMarket = false;
                                    currentStatus.tradeType = "";
                                }

                                currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
                                currentStatus.accountId = this.igAccountId;
                                currentStatus.startingQuantity = modelVar.startingQuantity;
                                currentStatus.minQuantity = modelVar.minQuantity;
                                currentStatus.maxQuantity = modelVar.maxQuantity;
                                currentStatus.gainMultiplier = modelVar.gainMultiplier;
                                currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
                                currentStatus.currentGain = modelVar.currentGain;
                                currentStatus.baseQuantity = modelVar.baseQuantity;
                                currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
                                currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
                            currentStatus.doSuppTrades = model.doSuppTrades;
                                //currentStatus.epicName = this.epicName;
                                //send log to the website
                                model.modelLogs.logs[0].epicName = this.epicName;
                                clsCommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]),the_app_db);
                                clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                                //save log to the database
                                Container logContainer = the_app_db.GetContainer("ModelLogs");
                                await log.SaveDocument(logContainer);
                                model.modelLogs.logs = new List<ModelLog>();

                            }
                            model.candles.prevCandle2 = model.candles.prevCandle.DeepCopy();
                            model.candles.prevCandle = model.candles.currentCandle.DeepCopy();
                            _startTime = _startTime.AddMinutes(1);
                            bigWatch.Stop();
                            //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);
                        



                    }
                    catch (Exception ex)
                    {
                        Log log = new Log(the_app_db);
                        log.Log_Message = ex.ToString();
                        log.Log_Type = "Error";
                        log.Log_App = "RunCode";
                        log.Save();
                    }

                    bigWatch.Stop();
                    clsCommonFunctions.AddStatusMessage( "Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO");

                    // call the accounts api each hour just so we ensure the tokens don't expire
                    //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
                }
                else
                {
                    clsCommonFunctions.AddStatusMessage("Trading not currently open", "INFO");
                }
            }
            else
            {
                clsCommonFunctions.AddStatusMessage("Trading brain paused...", "INFO");
                pausedAfterNGL = false;
            }

            if (latestHour != DateTime.UtcNow.Hour)
            {
                clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO");
                try
                {
                    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await igRestApiClient.accountBalance();
                    if (ret != null)
                    {
                        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO");
                    }
                    latestHour = DateTime.UtcNow.Hour;
                }
                catch (Exception ex)
                {
                    Log log = new Log(the_app_db);
                    log.Log_Message = ex.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "RunCode";
                    log.Save();
                }
                
            }

            if (liveMode)
            {
                ti.Interval = GetInterval();
                ti.Start();
            }

        }

        public async Task<int> WaitForChanges()
        {
            int ret = 1;
            try
            {
                DateTime dtStart = DateTime.UtcNow;
                DateTime dtMax = dtStart.AddSeconds(20);

                while (DateTime.UtcNow < dtMax || 1 == 1)
                {
                    System.Threading.Thread.Sleep(1000);
                    DateTime dtNow = DateTime.UtcNow;
                    //clsCommonFunctions.AddStatusMessage(dtNow.ToString("o") + " Sleeping....");
                }

                // Unsubscriber. Commented out for now but may need to add later.

                //UnsubscribeFromWatchlistInstruments();
                //UnsubscribefromTradeSubscription();
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "WaitForChanges";
                await log.Save();
            }
            return ret;

        }
        public async void setupMessaging()
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
            clsCommonFunctions.AddStatusMessage("Starting messaging - TradingBrain-" + this.epicName + "-" + this.igAccountId, "INFO");
            hubConnection = new HubConnectionBuilder()
                .WithUrl(url, (HttpConnectionOptions options) => options.Headers.Add("userid", "TradingBrain-" + this.epicName + "-" + this.igAccountId))
                 .WithAutomaticReconnect()
                .Build();
     
            hubConnection.Closed += async (error) =>
            {
                clsCommonFunctions.AddStatusMessage("Messaging connection errored - " + error.ToString(), "ERROR");
                 clsCommonFunctions.SaveLog("Error", "Message Connection", error.ToString(), this.the_app_db);
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await hubConnection.StartAsync();
            };


            hubConnection.On<string>("newMessage", (message) =>

            {
                message obj = JsonConvert.DeserializeObject<message>(message);
                switch (obj.messageType)
                {
                    case "Ping":
                        //clientMessage msg = JsonConvert.DeserializeObject<clientMessage>(obj.messageValue);
                        PingMessage msg = new PingMessage();
                        msg.epicName = this.epicName;

                        clsCommonFunctions.SendMessage(obj.messageValue, "Ping", JsonConvert.SerializeObject(msg),the_app_db);
                        break;

                    case "Status":
                        clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                        break;

                    case "Pause":
                        //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                        clsCommonFunctions.AddStatusMessage("Pause request received", "INFO");
                        
                        paused = true;
                        pausedAfterNGL = false;
                        if (model.onMarket)
                        {
                            currentStatus.status = "deferred pause (after current trade)";
                        }
                        else
                        {
                            currentStatus.status = "paused";
                        }
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                        break;

                    case "PauseAfterNGL":
                        clsCommonFunctions.AddStatusMessage("PauseAfterNGL request received", "INFO");
                        // Pause TB once CFL = 0
                        paused = true;
                        pausedAfterNGL = true;
                        currentStatus.status = "deferred pause (after nightingale success)";
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                        break;

                    case "Stop":
                        // Stop TB (pause) and close any open trades
                        clsCommonFunctions.AddStatusMessage("Stop request received", "INFO");
                        paused = true;
                        pausedAfterNGL = false;
                        if (model.longOnmarket)
                        {
                            TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "SellLong - Immediate stop activated", the_app_db);
                            clsCommonFunctions.AddStatusMessage("SellLong activated", "INFO");
                            CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
                            if (model.onSuppTrade)
                            {
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "SellLong supplementary- Immediate stop activated", the_app_db);
                                clsCommonFunctions.AddStatusMessage("SellLong supplementary activated", "INFO");
                                CloseDeal("long", (double)this.suppTrade.size, this.suppTrade.dealId);
                            }
                        }
                        else
                        {
                            if (model.shortOnMarket)
                            {
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyShort - Immediate stop activated", the_app_db);
                                clsCommonFunctions.AddStatusMessage("BuyShort activated", "INFO");
                                CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId);
                                if (model.onSuppTrade)
                                {
                                    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyShort supplementary - Immediate stop activated", the_app_db);
                                    clsCommonFunctions.AddStatusMessage("BuyShort supplementary activated", "INFO");
                                    CloseDeal("short", (double)this.suppTrade.size, this.suppTrade.dealId);
                                }
                            }
                        }
                        
                        currentStatus.status = "paused";
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                        break;

                    case "Resume":
                        //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                        clsCommonFunctions.AddStatusMessage("Resume request received", "INFO");
                        paused = false;
                        pausedAfterNGL = false;
                        currentStatus.status = "running";
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus),the_app_db);
                        break;

                    case "ChangeQuantity":
                        //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                        clsCommonFunctions.AddStatusMessage("ChangeQuantity request received", "INFO");
                        if (obj.messageValue != "")
                        {
                            ModelVars newVars = new ModelVars();
                            newVars = JsonConvert.DeserializeObject<ModelVars>(obj.messageValue);

                            if (newVars.baseQuantity > 0)
                            {
                                clsCommonFunctions.AddStatusMessage("New baseQuantity to use = " + newVars.baseQuantity, "INFO");
                                tb.lastRunVars.baseQuantity = newVars.baseQuantity;
                                tb.lastRunVars.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                tb.lastRunVars.minQuantity = newVars.baseQuantity;
                                model.modelVar.baseQuantity = newVars.baseQuantity;
                                model.modelVar.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier; ;
                                model.modelVar.minQuantity = newVars.baseQuantity;
                                currentStatus.quantity = newVars.baseQuantity;
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("New baseQuantity is 0", "ERROR");
                            }

                            if (newVars.maxQuantityMultiplier > 0)
                            {
                                clsCommonFunctions.AddStatusMessage("New maxQuantityMultiplier to use = " + newVars.maxQuantityMultiplier, "INFO");
                                tb.lastRunVars.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                                model.modelVar.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                                currentStatus.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("New maxQuantityMultiplier is 0", "ERROR");
                            }

                            //if (newVars.carriedForwardLoss > 0)
                            //{
                                clsCommonFunctions.AddStatusMessage("New carriedForwardLoss to use = " + newVars.carriedForwardLoss, "INFO");
                                tb.lastRunVars.carriedForwardLoss = newVars.carriedForwardLoss;
                                model.modelVar.carriedForwardLoss = newVars.carriedForwardLoss;
                                currentStatus.carriedForwardLoss = newVars.carriedForwardLoss;
                            //}
                            //else
                            //{
                            //    clsCommonFunctions.AddStatusMessage("New carriedForwardLoss is 0", "ERROR");
                            //}

                            //if (newVars.currentGain > 0)
                            //{
                                clsCommonFunctions.AddStatusMessage("New currentGain to use = " + newVars.currentGain, "INFO");
                                tb.lastRunVars.currentGain = newVars.currentGain;
                                model.modelVar.currentGain = newVars.currentGain;
                                currentStatus.currentGain = newVars.currentGain;
                            //}
                            //else
                            //{
                            //    clsCommonFunctions.AddStatusMessage("New currentGain is 0", "ERROR");
                            //}

                            if (newVars.gainMultiplier > 0)
                            {
                                clsCommonFunctions.AddStatusMessage("New gainMultiplier to use = " + newVars.gainMultiplier, "INFO");
                                tb.lastRunVars.gainMultiplier = newVars.gainMultiplier;
                                model.modelVar.gainMultiplier = newVars.gainMultiplier;
                                currentStatus.gainMultiplier = newVars.gainMultiplier;
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("New gainMultiplier is 0", "ERROR");
                            }

                            if (newVars.suppQuantityMultiplier > 0)
                            {
                                clsCommonFunctions.AddStatusMessage("New suppQuantityMultiplier to use = " + newVars.suppQuantityMultiplier, "INFO");
                                tb.lastRunVars.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                                model.modelVar.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                                currentStatus.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("New gainMultiplier is 0", "ERROR");
                            }

                            if (newVars.suppStopPercentage > 0)
                            {
                                clsCommonFunctions.AddStatusMessage("New suppStopPercentage to use = " + newVars.suppStopPercentage, "INFO");
                                tb.lastRunVars.suppStopPercentage = newVars.suppStopPercentage;
                                model.modelVar.suppStopPercentage = newVars.suppStopPercentage;
                                currentStatus.suppStopPercentage = newVars.suppStopPercentage;
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("New gainMultiplier is 0", "ERROR");
                            }

                            clsCommonFunctions.AddStatusMessage("New doSuppTrades to use = " + newVars.doSuppTradesVar, "INFO");
                            model.doSuppTrades = newVars.doSuppTradesVar;
                            currentStatus.doSuppTrades = newVars.doSuppTradesVar;
                            tb.doSuppTrades = newVars.doSuppTradesVar;

                            // Save the last run vars into the TB settings table
                            Task<bool> res = tb.SaveDocument(the_app_db);
                            clsCommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus), the_app_db);
                            clsCommonFunctions.AddStatusMessage("New values saved", "ERROR");
                        }
                        //var newValString = obj.messageValue;
                        //double newVal = 0;
                        //bool convRes = double.TryParse(newValString, out newVal);
                        //if (convRes)
                        //{
                        //    clsCommonFunctions.AddStatusMessage("New quantity to use = " + newVal, "INFO");
                        //    tb.lastRunVars.baseQuantity = newVal;
                        //    tb.lastRunVars.maxQuantity = newVal * tb.lastRunVars.maxQuantityMultiplier;
                        //    //tb.lastRunVars.startingQuantity = newVal ;
                        //    tb.lastRunVars.minQuantity = newVal;

                        //    model.modelVar.baseQuantity = newVal;
                        //    model.modelVar.maxQuantity = newVal * tb.lastRunVars.maxQuantityMultiplier; ;
                        //    //model.modelVar.startingQuantity = newVal;
                        //    model.modelVar.minQuantity = newVal;

                        //    currentStatus.quantity = newVal;
                        //    // Save the last run vars into the TB settings table
                        //    Task<bool> res = tb.SaveDocument(the_app_db);
                        //    clsCommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus),the_app_db);

                        //}
                        //else
                        //{
                        //    clsCommonFunctions.AddStatusMessage("New quantity cant be used - " + newValString, "ERROR");
                        //}


                        break;

                    case "Kill":  
                        currentStatus.status = "closed";
                        clsCommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db);
                 
                        // Close Console app
                        System.Environment.Exit(1);
                         
                        break;
                }
                //var newMessage = $"{message}";
                //clsCommonFunctions.AddStatusMessage(newMessage);

            });

            try
            {

                await hubConnection.StartAsync();
                clsCommonFunctions.AddStatusMessage("Connection started", "INFO");
                clsCommonFunctions.SaveLog("Info", "Message Connection", "Messaging started", this.the_app_db);
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "setupMessaging";
                await log.Save();
            }

            hubConnection.Reconnecting += error =>
            {
                Debug.Assert(hubConnection.State == HubConnectionState.Reconnecting);

                // Notify users the connection was lost and the client is reconnecting.
                // Start queuing or dropping messages.
                clsCommonFunctions.AddStatusMessage($"Messaging connection lost, retrying - {error.ToString()}", "ERROR");
                return Task.CompletedTask;
            };
            hubConnection.Reconnected += connectionId =>
            {
                Debug.Assert(hubConnection.State == HubConnectionState.Connected);
                clsCommonFunctions.AddStatusMessage("Messaging connection reconnected", "INFO");
                // Notify users the connection was reestablished.
                // Start dequeuing messages queued while reconnecting if any.

                return Task.CompletedTask;
            };

        }
        public async Task<int> CheckStuff()
        {
            int ret = 1;

            return ret;
        }



        public static async Task<ModelMinuteCandle> CreateLiveCandle(Database the_db, int minAvgIndex, int min30AvgIndex, int minAvgIndexShort, int min30AvgIndexShort, DateTime dtDate, string epicName, Container minute_container, Container TicksContainer, bool MAonly, bool createMinRecord,Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
        {
            //var watch = new System.Diagnostics.Stopwatch();
            ModelMinuteCandle retCandle = new ModelMinuteCandle();

            try
            {
                DateTime minStart = dtDate.AddMinutes(-1 * (minAvgIndex - 1));
                DateTime minEnd = dtDate.AddSeconds(59);
                DateTime minStartShort = dtDate.AddMinutes(-1 * (minAvgIndexShort - 1));
                DateTime minEndShort = dtDate.AddSeconds(59);

                var bigWatch = new System.Diagnostics.Stopwatch();
                //AvgDates min30Dates = new AvgDates();
                //min30Dates.GetAvgDates(dtDate, (min30AvgIndex-1));

                //AvgDates min30DatesShort = new AvgDates();
                //min30DatesShort.GetAvgDates(dtDate, (min30AvgIndexShort-1));

                string ret = "";

                retCandle.epicName = epicName;
                retCandle.candleStart = dtDate;

                //watch.Start();
                if (!MAonly)
                {
                    CandleData data = await IGModels.clsCommonFunctions.GetCandleDataV2(the_db, TicksContainer, epicName, dtDate, minEnd);

                    // may need to add the candle here
                    if (createMinRecord)
                    {

                        //bigWatch.Start();

                        // Check if there was any tick data (might not have been collected). If not, then create one.
                        if (data.Open == 0)
                        {
                            clsCommonFunctions.AddStatusMessage( "TickData not found. Creating ...", "INFO");
                            // Get the previous tick and save it as the next tick
                            TickData tick = await IGModels.clsCommonFunctions.GetTickData(the_db, TicksContainer, epicName, dtDate);
                            tick.UTM = dtDate;
                            tick.id = System.Guid.NewGuid().ToString();
                            bool sv = await tick.Save(the_db, TicksContainer);
                            clsCommonFunctions.AddStatusMessage( "New tick created. ", "INFO");
                            //Now we can get the candle data as it should now find the tick created above
                            data = await IGModels.clsCommonFunctions.GetCandleDataV2(the_db, TicksContainer, epicName, dtDate, minEnd);
                        }

                        clsMinuteCandle minuteCandle = new clsMinuteCandle();
                        minuteCandle.id = System.Guid.NewGuid().ToString();
                        minuteCandle.Epic = epicName;
                        minuteCandle.CandleStart = dtDate;
                        minuteCandle.candleData = data;
                        bool b = await minuteCandle.SaveDocument(minute_container);
                        //bigWatch.Stop();
                        //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Created minute candle - Time taken = " + bigWatch.ElapsedMilliseconds);
                    }

                    CandleMovingAverage minuteAvg = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, minStart, minEnd);
                    CandleMovingAverage minuteAvgShort = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, minStartShort, minEndShort);
                    retCandle.candleData = data;
                    retCandle.mATypicalLongTypical = minuteAvg.Typical;
                    retCandle.mATypicalShortTypical = minuteAvgShort.Typical;

                }

                IG_Epic epic = new IG_Epic();
                epic.Epic = epicName;





                


                //CandleMovingAverage min30Avg = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, min30Dates.start, min30Dates.end);
                //CandleMovingAverage min30AvgShort = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, min30DatesShort.start, min30DatesShort.end);
                //bigWatch.Restart();
                //CandleMovingAverage min30Avg = await Get_MinuteMovingAverageNum30(the_db, minute_container, epic, retCandle.candleStart,  min30AvgIndex);
                CandleMovingAverage min30Avg = await Get_MinuteMovingAverageNum30v1(the_db, minute_container, epic, retCandle.candleStart, min30AvgIndex, the_app_db,exchangeClosedDates);
                //bigWatch.Stop();
                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - min30Avg - Time taken = " + bigWatch.ElapsedMilliseconds);
                //bigWatch.Restart();
                //            CandleMovingAverage min30AvgShort = await Get_MinuteMovingAverageNum30(the_db, minute_container, epic, retCandle.candleStart,  min30AvgIndexShort );
                CandleMovingAverage min30AvgShort = await Get_MinuteMovingAverageNum30v1(the_db, minute_container, epic, retCandle.candleStart, min30AvgIndexShort, the_app_db,exchangeClosedDates);
                
                // bigWatch.Stop();
                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - min30AvgShort - Time taken = " + bigWatch.ElapsedMilliseconds);

                retCandle.mA30MinTypicalShortClose = min30AvgShort.Close;
                retCandle.mA30MinTypicalLongClose = min30Avg.Close;
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "CreateLiveCandle";
                await log.Save();
            }
            return retCandle;
        }

        public class AvgDates
        {
            public DateTime start { get; set; }
            public DateTime end { get; set; }
            public AvgDates()
            {
                start = DateTime.MinValue;
                end = DateTime.MinValue;
            }
            public void GetAvgDates(DateTime now, int i)
            {
                try
                {
                    AvgDates ret = new AvgDates();
                    int mins = now.Minute;
                    int addMins = 0;

                    if (i == 1)
                    {
                        start = now;
                    }
                    else
                    {

                        if (mins == 29 || mins == 59)
                        {
                            addMins = 0;
                            start = now.AddMinutes(-((i - 1) * 30));
                        }
                        else
                        {
                            if (mins < 29)
                            {
                                addMins = 59 - mins;
                            }
                            else
                            {
                                addMins = mins - 29;
                            }
                            start = now.AddMinutes(-(addMins + ((i - 2) * 30)));
                        }
                    }
                    end = now.AddSeconds(59);
                }
                catch (Exception e)
                {
                    Log log = new Log();
                    log.Log_Message = e.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "GetAvgDates";
                    log.Save();
                }
                //clsCommonFunctions.AddStatusMessage(i + " ---- Now : " + now + " Start : " + ret.start + " End : " + ret.end);

            }
        }




        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num,Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
        {

            CandleMovingAverage ret = new CandleMovingAverage();
            List<clsMinuteCandle> resp = new List<clsMinuteCandle>();

            //string epicName = epic.Epic;
            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");

                // Start the loop to get [num] number of candles into the object
                DateTime currentStart = CandleStart;
                DateTime getStartDate = currentStart;
                bool weekendDetected = false;
                for (int i = 0; i <= num - 1; i++)
                {

                    bool blnFound = false;

                    //int numChances = 0;

                    // Get the candle for the required date. If it does not exist, keep trying a minute less until one is found.
                    while (!blnFound)
                    {
                        // Sort out the start date if it now falls during the weekend. This is so we can get the averages of candles created surrounding a weekend
                        //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate) && !weekendDetected)
                        if(!await IGModels.clsCommonFunctions.IsTradingOpen(getStartDate,exchangeClosedDates))
                        {
                            int daysToSubtract = 0;
                            // get the current day and then work out how many days to remove to make it friday at 21:00
                            if (getStartDate.DayOfWeek == DayOfWeek.Sunday)
                            {
                                daysToSubtract = -2;
                            }
                            else
                            {
                                if (getStartDate.DayOfWeek == DayOfWeek.Saturday)
                                {
                                    daysToSubtract = -1;
                                }
                            }
                            getStartDate = getStartDate.AddDays(daysToSubtract);
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, 20, getStartDate.Minute, 0);
                            weekendDetected = true;
                        }
                        else
                        {
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate))
                            if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates).Result)
                            {
                                weekendDetected = false;
                            }
                        }

                        clsMinuteCandle item = await Get_MinuteCandle(the_db, container, epic, getStartDate);

                        if (item.CandleStart != DateTime.MinValue)
                        {
                            blnFound = true;
                            // if this is the first one then we need to add an extra min inorder to ensure we get 30 candles back each time.
                            //if (i == 0) { currentStart = currentStart.AddMinutes(1); }

                            // Actually, what we need to do is move the time so the subsequent candles are either 29 mins or 59 mins past the hour
                            if (i == 0)
                            {
                                int mm = getStartDate.Minute;
                                int hh = getStartDate.Hour;
                                if (mm <= 29) { mm = 29; } else { mm = 59; }
                                //currentStart = new DateTime(currentStart.Year, currentStart.Month, currentStart.Day, hh, mm, currentStart.Second);
                                getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, hh, mm, getStartDate.Second);

                            }
                            // Now we have the correct starting point we can just remove 30 mins each time
                            //currentStart = currentStart.AddMinutes(-30);
                            getStartDate = getStartDate.AddMinutes(-30);
                            resp.Add(item);

                        }
                        else
                        {
                            getStartDate = getStartDate.AddMinutes(-1);
                            if (getStartDate < new DateTime(2024, 10, 07, 10, 05, 00) && epic.Epic == "IX.D.NIKKEI.DAILY.IP")
                            {
                                blnFound = true;
                            }

                        }
                    }
                }

                //int idx = 1;
                //foreach (clsMinuteCandle item in resp)
                //{
                CandleMovingAverage newMA = new CandleMovingAverage();
                //newMA.sequence = idx;
                newMA.StartDate = resp[0].candleData.StartDate;
                newMA.EndDate = resp[resp.Count - 1].candleData.EndDate;
                newMA.Close = resp.Select(x => x.candleData.Close).Average();
                newMA.High = resp.Select(x => x.candleData.High).Average();
                newMA.Low = resp.Select(x => x.candleData.Low).Average();
                newMA.Open = resp.Select(x => x.candleData.Open).Average();
                newMA.Typical = resp.Select(x => x.candleData.Typical).Average();

                if (newMA.Close != 0)
                {

                    ret = newMA;
                }

                //idx++;
                // }
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Get_MinuteMovingAverageNum30";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get_MinuteMovingAverageNum30";
                await log.Save();
            }

            return (ret);
        }

        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30v1(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num,Database the_app_db,List<ExchangeClosedItem> exchangeClosedDates)
        {

            CandleMovingAverage ret = new CandleMovingAverage();
            List<clsMinuteCandle> resp = new List<clsMinuteCandle>();
            //List<clsMinuteCandle> resp = new List<clsMinuteCandle>();

            List<string> lstDates = new List<string>();

            //string epicName = epic.Epic;
            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");

                // Start the loop to get [num] number of candles into the object
                DateTime currentStart = CandleStart;
                DateTime getStartDate = currentStart;
                bool weekendDetected = false;
                for (int i = 0; i <= num - 1; i++)
                {

                    bool blnFound = false;

                    //int numChances = 0;

                    // Get the candle for the required date. If it does not exist, keep trying a minute less until one is found.
                    while (!blnFound)
                    {

                        // Sort out the start date if it now falls during the weekend. This is so we can get the averages of candles created surrounding a weekend
                        //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate) && !weekendDetected)
                            if (!await IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates))
                            {
                            int daysToSubtract = 0;
                            // get the current day and then work out how many days to remove to make it friday at 21:00
                            if (getStartDate.DayOfWeek == DayOfWeek.Sunday)
                            {
                                daysToSubtract = -2;
                            }
                            else
                            {
                                if (getStartDate.DayOfWeek == DayOfWeek.Saturday)
                                {
                                    daysToSubtract = -1;
                                }
                            }
                            getStartDate = getStartDate.AddDays(daysToSubtract);
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, 20, getStartDate.Minute, 0);
                            weekendDetected = true;
                        }
                        else
                        {
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate))
                            if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates).Result)
                            {
                                weekendDetected = false;
                            }
                        }

                        //clsMinuteCandle item = await Get_MinuteCandle(the_db, container, epic, getStartDate);
                        lstDates.Add(getStartDate.ToString("yyyy-MM-dd") + "T" + getStartDate.ToString("HH:mm:ss"));
                        //if (item.CandleStart != DateTime.MinValue)
                        //{
                        blnFound = true;
                        // if this is the first one then we need to add an extra min inorder to ensure we get 30 candles back each time.
                        //if (i == 0) { currentStart = currentStart.AddMinutes(1); }

                        // Actually, what we need to do is move the time so the subsequent candles are either 29 mins or 59 mins past the hour
                        if (i == 0)
                        {
                            int mm = getStartDate.Minute;
                            int hh = getStartDate.Hour;
                            if (mm <= 29) { mm = 29; } else { mm = 59; }
                            //currentStart = new DateTime(currentStart.Year, currentStart.Month, currentStart.Day, hh, mm, currentStart.Second);
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, hh, mm, getStartDate.Second);

                        }
                        // Now we have the correct starting point we can just remove 30 mins each time
                        //currentStart = currentStart.AddMinutes(-30);


                        getStartDate = getStartDate.AddMinutes(-30);
                        // resp.Add(item);

                        //}
                        //else
                        //{
                        //    getStartDate = getStartDate.AddMinutes(-1);
                        //    if (getStartDate < new DateTime(2024, 10, 07, 10, 05, 00) && epic.Epic == "IX.D.NIKKEI.DAILY.IP")
                        //    {
                        //        blnFound = true;
                        //    }

                        //}

                        //Now query the DB to get the averages of them all.

                    }
                }

                clsMinuteCandle item = new clsMinuteCandle();
                string epicName = epic.Epic;
                try
                {
                    //Container container = the_db.GetContainer("MinuteCandle");

                    string qry = "SELECT  avg(c.candleData.Typical) as Typical, avg(c.candleData.Open) as Open, avg(c.candleData.High) as High, avg(c.candleData.Low) as Low, avg(c.candleData.Close) as Close FROM c  ";


                    int i = 0;
                    foreach (string dt in lstDates)
                    {
                        if (i == 0)
                        {
                            qry += " where ";
                        }
                        else
                        {
                            qry += " or ";
                        }
                        qry += " c.CandleStart = '" + dt.ToString() + "' ";
                        i++;
                    }
                    var parameterizedQuery = new QueryDefinition(
                        query: qry
                    );

                    using FeedIterator<CandleMovingAverage> filteredFeed = container.GetItemQueryIterator<CandleMovingAverage>(
                        queryDefinition: parameterizedQuery
                    );

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<CandleMovingAverage> response = await filteredFeed.ReadNextAsync();

                        // Iterate query results
                        foreach (CandleMovingAverage candle in response)
                        {
                            ret = candle;
                        }
                    }

                    //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

                }
                catch (CosmosException de)
                {
                    if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                    {
                        Log log = new Log(the_app_db);
                        log.Log_Message = de.ToString();
                        log.Log_Type = "Error";
                        log.Log_App = "Get_MinuteMovingAverageNum30v1";
                        await log.Save();
                    }

                }
                catch (Exception e)
                {
                    Log log = new Log(the_app_db);
                    log.Log_Message = e.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Get_MinuteMovingAverageNum30v1";
                    await log.Save();
                }

                //int idx = 1;
                //foreach (clsMinuteCandle item in resp)
                //{
                //CandleMovingAverage newMA = new CandleMovingAverage();
                ////newMA.sequence = idx;
                //newMA.StartDate = resp[0].candleData.StartDate;
                //newMA.EndDate = resp[resp.Count - 1].candleData.EndDate;
                //newMA.Close = resp.Select(x => x.candleData.Close).Average();
                //newMA.High = resp.Select(x => x.candleData.High).Average();
                //newMA.Low = resp.Select(x => x.candleData.Low).Average();
                //newMA.Open = resp.Select(x => x.candleData.Open).Average();
                //newMA.Typical = resp.Select(x => x.candleData.Typical).Average();

                //if (newMA.Close != 0)
                //{

                //    ret = newMA;
                //}

                //idx++;
                // }
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Get_MinuteMovingAverageNum30v1";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get_MinuteMovingAverageNum30v1";
                await log.Save();
            }

            return (ret);
        }


        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30v2(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num, Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
        {

            CandleMovingAverage ret = new CandleMovingAverage();
            List<clsMinuteCandle> resp = new List<clsMinuteCandle>();

            //string epicName = epic.Epic;
            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");

                // Start the loop to get [num] number of candles into the object
                DateTime currentStart = CandleStart;
                DateTime getStartDate = currentStart;
                bool weekendDetected = false;
                for (int i = 0; i <= num - 1; i++)
                {

                    bool blnFound = false;

                    //int numChances = 0;

                    // Get the candle for the required date. If it does not exist, keep trying a minute less until one is found.
                    while (!blnFound)
                    {
                        // Sort out the start date if it now falls during the weekend. This is so we can get the averages of candles created surrounding a weekend
                        //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate) && !weekendDetected)
                        if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates).Result && !weekendDetected)
                        {
                            int daysToSubtract = 0;
                            // get the current day and then work out how many days to remove to make it friday at 21:00
                            if (getStartDate.DayOfWeek == DayOfWeek.Sunday)
                            {
                                daysToSubtract = -2;
                            }
                            else
                            {
                                if (getStartDate.DayOfWeek == DayOfWeek.Saturday)
                                {
                                    daysToSubtract = -1;
                                }
                            }
                            getStartDate = getStartDate.AddDays(daysToSubtract);
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, 20, getStartDate.Minute, 0);
                            weekendDetected = true;
                        }
                        else
                        {
                            if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates).Result)
                            {
                                weekendDetected = false;
                            }
                        }

                        clsMinuteCandle item = await Get_MinuteCandle(the_db, container, epic, getStartDate);

                        if (item.CandleStart != DateTime.MinValue)
                        {
                            blnFound = true;
                            // if this is the first one then we need to add an extra min inorder to ensure we get 30 candles back each time.
                            //if (i == 0) { currentStart = currentStart.AddMinutes(1); }

                            // Actually, what we need to do is move the time so the subsequent candles are either 29 mins or 59 mins past the hour
                            if (i == 0)
                            {
                                int mm = getStartDate.Minute;
                                int hh = getStartDate.Hour;
                                if (mm <= 29) { mm = 29; } else { mm = 59; }
                                //currentStart = new DateTime(currentStart.Year, currentStart.Month, currentStart.Day, hh, mm, currentStart.Second);
                                getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, hh, mm, getStartDate.Second);

                            }
                            // Now we have the correct starting point we can just remove 30 mins each time
                            //currentStart = currentStart.AddMinutes(-30);
                            getStartDate = getStartDate.AddMinutes(-30);
                            resp.Add(item);

                        }
                        else
                        {
                            getStartDate = getStartDate.AddMinutes(-1);
                            if (getStartDate < new DateTime(2024, 10, 07, 10, 05, 00) && epic.Epic == "IX.D.NIKKEI.DAILY.IP")
                            {
                                blnFound = true;
                            }

                        }
                    }
                }

                //int idx = 1;
                //foreach (clsMinuteCandle item in resp)
                //{
                CandleMovingAverage newMA = new CandleMovingAverage();
                //newMA.sequence = idx;
                newMA.StartDate = resp[0].candleData.StartDate;
                newMA.EndDate = resp[resp.Count - 1].candleData.EndDate;
                newMA.Close = resp.Select(x => x.candleData.Close).Average();
                newMA.High = resp.Select(x => x.candleData.High).Average();
                newMA.Low = resp.Select(x => x.candleData.Low).Average();
                newMA.Open = resp.Select(x => x.candleData.Open).Average();
                newMA.Typical = resp.Select(x => x.candleData.Typical).Average();

                if (newMA.Close != 0)
                {

                    ret = newMA;
                }

                //idx++;
                // }
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Get_MinuteMovingAverageNum30v2";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get_MinuteMovingAverageNum30v2";
                await log.Save();
            }

            return (ret);
        }

        public static async Task<clsMinuteCandle> Get_MinuteCandle(Database the_db, Container container, IG_Epic epic, DateTime CandleStart)
        {

            clsMinuteCandle ret = new clsMinuteCandle();
            string epicName = epic.Epic;
            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM  c WHERE c.Epic= @epicName and c.CandleStart=@CandleStart "
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@CandleStart", CandleStart);

                using FeedIterator<clsMinuteCandle> filteredFeed = container.GetItemQueryIterator<clsMinuteCandle>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<clsMinuteCandle> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (clsMinuteCandle item in response)
                    {
                        ret = item;
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "Get_MinuteCandle";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Get_MinuiteCandle";
                await log.Save();
            }

            return (ret);
        }

        public async void GetPositions()
        {

            //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);
            try
            {


                IgResponse<PositionsResponse> ret = await igRestApiClient.getOTCOpenPositionsV1();

                foreach (OpenPosition obj in ret.Response.positions)
                {
                    if (obj.market.epic == this.epicName)
                    {
                        OpenPositionData tsm = obj.position;

                        //First see if we already have this deal in our database.
                        tradeItem thisTrade = await GetTradeFromDB(tsm.dealId);

                        // Check to see if it is not a supplementary trade first.
                        if (!thisTrade.isSuppTrade) { 
                            if (thisTrade.tbDealId != "")
                            {
                                    this.model.thisModel.currentTrade = thisTrade;
                                this.currentTrade = new clsTradeUpdate();
                                this.currentTrade.epic = this.epicName;
                                    this.currentTrade.dealId = tsm.dealId;
                                    this.currentTrade.lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate);
                                    this.currentTrade.level = Convert.ToDecimal(tsm.openLevel);
                                    this.currentTrade.stopLevel = Convert.ToDecimal(tsm.stopLevel);
                                    this.currentTrade.size = Convert.ToDecimal(tsm.dealSize);
                                    this.currentTrade.direction = tsm.direction;
                                    this.model.thisModel.currentTrade.stopLossValue = (double)this.model.stopPrice;

                                    if (tsm.direction == "BUY")
                                    {
                                        this.model.stopPrice = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                        this.model.stopPriceOld = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                        this.model.longOnmarket = true;
                                        this.model.buyShort = false;
                                        this.model.shortOnMarket = false;
                                    }
                                    else
                                    {
                                        this.model.stopPrice = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                        this.model.stopPriceOld = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                        this.model.shortOnMarket = true;
                                        this.model.buyLong = false;
                                        this.model.longOnmarket = false;
                                    }

                                    this.model.thisModel.currentTrade.stopLossValue = (double)this.model.stopPrice;

                                    this.model.onMarket = true;
                                
                                }


                        }
                        else
                        {
                            if (thisTrade.tbDealId != "")
                            {
                                // This is a supplementary trade.
                                this.model.thisModel.suppTrade = thisTrade;
                                this.suppTrade = new clsTradeUpdate();
                                this.suppTrade.epic = this.epicName;
                                this.suppTrade.dealId = tsm.dealId;
                                this.suppTrade.lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate);
                                this.suppTrade.level = Convert.ToDecimal(tsm.openLevel);
                                this.suppTrade.stopLevel = Convert.ToDecimal(tsm.stopLevel);
                                this.suppTrade.size = Convert.ToDecimal(tsm.dealSize);
                                this.suppTrade.direction = tsm.direction;
                                this.model.thisModel.suppTrade.stopLossValue = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                this.model.onSuppTrade = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetPositions";
                log.Epic = this.epicName;
                log.Save();
            }
            //return ret;
        }
        public async void GetOrders()
        {

            //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);
            try
            {


                IgResponse<dto.endpoint.workingorders.get.v2.WorkingOrdersResponse> ret = await igRestApiClient.workingOrdersV2();

                foreach (dto.endpoint.workingorders.get.v2.WorkingOrder obj in ret.Response.workingOrders)
                {
                    if (obj.workingOrderData.epic == this.epicName)
                    {
                        dto.endpoint.workingorders.get.v2.WorkingOrderData tsm = obj.workingOrderData;

                        //First see if we already have this deal in our database.
                        orderItem thisOrder = await GetOrderFromDB(tsm.dealId);

                        // Check to see if it is not a supplementary trade first.
    
                            if (thisOrder.dealId != "")
                            {
                                //this.model.thisModel.currentTrade = thisTrade;
                                //this.currentTrade = new clsTradeUpdate();
                                //this.currentTrade.epic = this.epicName;
                                //this.currentTrade.dealId = tsm.dealId;
                                //this.currentTrade.lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate);
                                //this.currentTrade.level = Convert.ToDecimal(tsm.openLevel);
                                //this.currentTrade.stopLevel = Convert.ToDecimal(tsm.stopLevel);
                                //this.currentTrade.size = Convert.ToDecimal(tsm.dealSize);
                                //this.currentTrade.direction = tsm.direction;
                                //this.model.thisModel.currentTrade.stopLossValue = (double)this.model.stopPrice;

                                //if (tsm.direction == "BUY")
                                //{
                                //    this.model.stopPrice = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                //    this.model.stopPriceOld = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                //    this.model.longOnmarket = true;
                                //    this.model.buyShort = false;
                                //    this.model.shortOnMarket = false;
                                //}
                                //else
                                //{
                                //    this.model.stopPrice = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                //    this.model.stopPriceOld = (double)this.currentTrade.stopLevel - (double)this.currentTrade.level;
                                //    this.model.shortOnMarket = true;
                                //    this.model.buyLong = false;
                                //    this.model.longOnmarket = false;
                                //}

                                //this.model.thisModel.currentTrade.stopLossValue = (double)this.model.stopPrice;

                                //this.model.onMarket = true;

                            }


                        }

                    }
                
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetPositions";
                log.Epic = this.epicName;
                log.Save();
            }
            //return ret;
        }
        public void setupTradeErrors()
        {
            this.TradeErrors.Add("", "");
            this.TradeErrors.Add("ACCOUNT_NOT_ENABLED_TO_TRADING", "The account is not enabled to trade");
            this.TradeErrors.Add("ATTACHED_ORDER_LEVEL_ERROR", "The level of the attached stop or limit is not valid");
            this.TradeErrors.Add("ATTACHED_ORDER_TRAILING_STOP_ERROR", "The trailing stop value is invalid");
            this.TradeErrors.Add("CANNOT_CHANGE_STOP_TYPE", "Cannot change the stop type.");
            this.TradeErrors.Add("CANNOT_REMOVE_STOP", "Cannot remove the stop.");
            this.TradeErrors.Add("CLOSING_ONLY_TRADES_ACCEPTED_ON_THIS_MARKET", "We are not taking opening deals on a Controlled Risk basis on this market");
            this.TradeErrors.Add("CLOSINGS_ONLY_ACCOUNT", "You are currently restricted from opening any new positions on your account.");
            this.TradeErrors.Add("CONFLICTING_ORDER", "Resubmitted request does not match the original order.");
            this.TradeErrors.Add("CONTACT_SUPPORT_INSTRUMENT_ERROR", "Instrument has an error - check the order's currency is the instrument's currency (see the market's details); otherwise please contact support.");
            this.TradeErrors.Add("CR_SPACING", "Sorry we are unable to process this order. The stop or limit level you have requested is not a valid trading level in the underlying market.");
            this.TradeErrors.Add("DUPLICATE_ORDER_ERROR", "The order has been rejected as it is a duplicate of a previously issued order");
            this.TradeErrors.Add("EXCHANGE_MANUAL_OVERRIDE", "Exchange check failed. Please call in for assistance.");
            this.TradeErrors.Add("EXPIRY_LESS_THAN_SPRINT_MARKET_MIN_EXPIRY", "Order expiry is less than the sprint market's minimum expiry. Check the sprint market's market details for the allowable expiries.");
            this.TradeErrors.Add("FINANCE_REPEAT_DEALING", "The total size of deals placed on this market in a short period has exceeded our limits. Please wait before attempting to open further positions on this market.");
            this.TradeErrors.Add("FORCE_OPEN_ON_SAME_MARKET_DIFFERENT_CURRENCY", "Ability to force open in different currencies on same market not allowed");
            this.TradeErrors.Add("GENERAL_ERROR", "an error has occurred but no detailed information is available. Check transaction history or contact support for further information");
            this.TradeErrors.Add("GOOD_TILL_DATE_IN_THE_PAST", "The working order has been set to expire on a past date");
            this.TradeErrors.Add("INSTRUMENT_NOT_FOUND", "The requested market was not found");
            this.TradeErrors.Add("INSTRUMENT_NOT_TRADEABLE_IN_THIS_CURRENCY", "Instrument not tradeable in this currency.");
            this.TradeErrors.Add("INSUFFICIENT_FUNDS", "The account has not enough funds available for the requested trade");
            this.TradeErrors.Add("LEVEL_TOLERANCE_ERROR", "The market level has moved and has been rejected");
            this.TradeErrors.Add("LIMIT_ORDER_WRONG_SIDE_OF_MARKET", "The deal has been rejected because the limit level is inconsistent with current market price given the direction.");
            this.TradeErrors.Add("MANUAL_ORDER_TIMEOUT", "The manual order timeout limit has been reached");
            this.TradeErrors.Add("MARGIN_ERROR", "Order declined during margin checks Check available funds.");
            this.TradeErrors.Add("MARKET_CLOSED", "The market is currently closed");
            this.TradeErrors.Add("MARKET_CLOSED_WITH_EDITS", "The market is currently closed with edits");
            this.TradeErrors.Add("MARKET_CLOSING", "The epic is due to expire shortly, client should deal in the next available contract.");
            this.TradeErrors.Add("MARKET_NOT_BORROWABLE", "The market does not allow opening shorting positions");
            this.TradeErrors.Add("MARKET_OFFLINE", "The market is currently offline");
            this.TradeErrors.Add("MARKET_ORDERS_NOT_ALLOWED_ON_INSTRUMENT", "The epic does not support 'Market' order type");
            this.TradeErrors.Add("MARKET_PHONE_ONLY", "The market can only be traded over the phone");
            this.TradeErrors.Add("MARKET_ROLLED", "The market has been rolled to the next period");
            this.TradeErrors.Add("MARKET_UNAVAILABLE_TO_CLIENT", "The requested market is not allowed to this account");
            this.TradeErrors.Add("MAX_AUTO_SIZE_EXCEEDED", "The order size exceeds the instrument's maximum configured value for auto-hedging the exposure of a deal");
            this.TradeErrors.Add("MINIMUM_ORDER_SIZE_ERROR", "The order size is too small");
            this.TradeErrors.Add("MOVE_AWAY_ONLY_LIMIT", "The limit level you have requested is closer to the market level than the existing stop. When the market is closed you can only move the limit order further away from the current market level.");
            this.TradeErrors.Add("MOVE_AWAY_ONLY_STOP", "The stop level you have requested is closer to the market level than the existing stop level. When the market is closed you can only move the stop level further away from the current market level");
            this.TradeErrors.Add("MOVE_AWAY_ONLY_TRIGGER_LEVEL", "The order level you have requested is moving closer to the market level than the exisiting order level. When the market is closed you can only move the order further away from the current market level.");
            this.TradeErrors.Add("NCR_POSITIONS_ON_CR_ACCOUNT", "You are not permitted to open a non-controlled risk position on this account.");
            this.TradeErrors.Add("OPPOSING_DIRECTION_ORDERS_NOT_ALLOWED", "Opening CR position in opposite direction to existing CR position not allowed.");
            this.TradeErrors.Add("OPPOSING_POSITIONS_NOT_ALLOWED", "The deal has been rejected to avoid having long and short open positions on the same market or having long and short open positions and working orders on the same epic");
            this.TradeErrors.Add("ORDER_DECLINED", "Order declined; please contact Support");
            this.TradeErrors.Add("ORDER_LOCKED", "The order is locked and cannot be edited by the user");
            this.TradeErrors.Add("ORDER_NOT_FOUND", "The order has not been found");
            this.TradeErrors.Add("ORDER_SIZE_CANNOT_BE_FILLED", "The order size cannot be filled at this price at the moment.");
            this.TradeErrors.Add("OVER_NORMAL_MARKET_SIZE", "The total position size at this stop level is greater than the size allowed on this market. Please reduce the size of the order.");
            this.TradeErrors.Add("PARTIALY_CLOSED_POSITION_NOT_DELETED", "Position cannot be deleted as it has been partially closed.");
            this.TradeErrors.Add("POSITION_ALREADY_EXISTS_IN_OPPOSITE_DIRECTION", "The deal has been rejected because of an existing position. Either set the 'force open' to be true or cancel opposing position");
            this.TradeErrors.Add("POSITION_NOT_AVAILABLE_TO_CANCEL", "Position cannot be cancelled. Check transaction history or contact support for further information.");
            this.TradeErrors.Add("POSITION_NOT_AVAILABLE_TO_CLOSE", "Cannot close this position. Either the position no longer exists, or the size available to close is less than the size specified.");
            this.TradeErrors.Add("POSITION_NOT_FOUND", "The position has not been found");
            this.TradeErrors.Add("REJECT_CFD_ORDER_ON_SPREADBET_ACCOUNT", "Invalid attempt to submit a CFD trade on a spreadbet account");
            this.TradeErrors.Add("REJECT_SPREADBET_ORDER_ON_CFD_ACCOUNT", "Invalid attempt to submit a spreadbet trade on a CFD account");
            this.TradeErrors.Add("SIZE_INCREMENT", "Order size is not an increment of the value specified for the market.");
            this.TradeErrors.Add("SPRINT_MARKET_EXPIRY_AFTER_MARKET_CLOSE", "The expiry of the position would have fallen after the closing time of the market");
            this.TradeErrors.Add("STOP_OR_LIMIT_NOT_ALLOWED", "The market does not allow stop or limit attached orders");
            this.TradeErrors.Add("STOP_REQUIRED_ERROR", "The order requires a stop");
            this.TradeErrors.Add("STRIKE_LEVEL_TOLERANCE", "The submitted strike level is invalid");
            this.TradeErrors.Add("SUCCESS", "The operation completed successfully");
            this.TradeErrors.Add("TRAILING_STOP_NOT_ALLOWED", "The market or the account do not allow for trailing stops");
            this.TradeErrors.Add("UNKNOWN", "The operation resulted in an unknown result condition. Check transaction history or contact support for further information");
            this.TradeErrors.Add("WRONG_SIDE_OF_MARKET", "The requested operation has been attempted on the wrong direction");
        }
        public async Task<tradeItem> GetTradeFromDB(string dealID)
        {
            tradeItem ret = new tradeItem();

            try
            {
                Microsoft.Azure.Cosmos.Container container = the_app_db.GetContainer("TradingBrainTrades");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM  c WHERE  c.tbDealId=@DealID "
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@DealID", dealID);

                using FeedIterator<tradeItem> filteredFeed = container.GetItemQueryIterator<tradeItem>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<tradeItem> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (tradeItem item in response)
                    {
                        if (item.tbDealId == dealID)
                        {
                            ret = item;
                        }
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new TradingBrain.Models.Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "GetTradeFromDB";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetTradeFromDB";
                await log.Save();
            }

            return ret;

        }
        public async Task<tradeItem> GetTradeFromDBByOrder(string dealID)
        {
            tradeItem ret = new tradeItem();

            try
            {
                Microsoft.Azure.Cosmos.Container container = the_app_db.GetContainer("TradingBrainTrades");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM  c WHERE  c.suppOrderId=@DealID "
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@DealID", dealID);

                using FeedIterator<tradeItem> filteredFeed = container.GetItemQueryIterator<tradeItem>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<tradeItem> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (tradeItem item in response)
                    {
                        //if (item.tbDealId == dealID)
                        //{
                            ret = item;
                        //}
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new TradingBrain.Models.Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "GetTradeFromDB";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetTradeFromDB";
                await log.Save();
            }

            return ret;

        }
        public async Task<orderItem> GetOrderFromDB(string dealID)
        {
            orderItem ret = new orderItem();

            try
            {
                Microsoft.Azure.Cosmos.Container container = the_app_db.GetContainer("TradingBrainOrders");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM  c WHERE  c.tbDealId=@DealID "
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@DealID", dealID);

                using FeedIterator<orderItem> filteredFeed = container.GetItemQueryIterator<orderItem>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<orderItem> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (orderItem item in response)
                    {
                        if (item.dealId == dealID)
                        {
                            ret = item;
                        }
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new TradingBrain.Models.Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "GetOrderFromDB";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetOrderFromDB";
                await log.Save();
            }

            return ret;

        }
        public async void GetTradeFromDBSync(string dealID)
        {
            tradeItem ret = new tradeItem();

            try
            {
                Microsoft.Azure.Cosmos.Container container = the_app_db.GetContainer("TradingBrainTrades");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM  c WHERE  c.tbDealId=@DealID "
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@DealID", dealID);

                using FeedIterator<tradeItem> filteredFeed = container.GetItemQueryIterator<tradeItem>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<tradeItem> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (tradeItem item in response)
                    {
                        if (item.tbDealId == dealID)
                        {
                            ret = item;
                        }
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new TradingBrain.Models.Log(the_app_db);
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "GetTradeFromDB";
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "GetTradeFromDB";
                await log.Save();
            }
            this.model.thisModel.currentTrade = ret;
            //return ret;

        }
        //public tradeItem GetTradeFromDBSync(string dealID)
        //{
        //    Task<tradeItem> task = Task.Run<tradeItem>(async () => await GetTradeFromDB(dealID));
        //    return task.Result;

        //}

        
    }

}
