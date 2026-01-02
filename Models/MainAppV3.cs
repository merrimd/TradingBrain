using Azure.Core.GeoJson;
using Azure.Storage;
using com.lightstreamer.client;
using dto.endpoint.accountactivity.transaction;
using dto.endpoint.application.operation;
using dto.endpoint.auth.session.v2;
using dto.endpoint.confirms;
using dto.endpoint.positions.close.v1;
using dto.endpoint.positions.create.otc.v1;
using dto.endpoint.positions.edit.v1;
using dto.endpoint.positions.get.otc.v1;
using dto.endpoint.search;
using dto.endpoint.type;
using dto.endpoint.watchlists.retrieve;
using dto.endpoint.workingorders.create.v1;
using dto.endpoint.workingorders.delete.v1;
using dto.endpoint.workingorders.get.v1;
using IGCandleCreator.Models;
using IGModels;
using IGModels.ModellingModels;
using IGModels.RSI_Models;
using IGWebApiClient;
using IGWebApiClient.Common;
using IGWebApiClient.Models;
using Lightstreamer.DotNet.Client;
using log4net.Core;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Identity.Client;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Pqc.Crypto.Saber;
//using PInvoke;
using Skender.Stock.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Xsl;
using TradingBrain.Models;
using static DotNetty.Common.ThreadLocalPool;
using static IGModels.ModellingModels.GetModelClass;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TradingBrain.Models.CommonFunctions;
using Timer = System.Timers.Timer;


namespace TradingBrain.Models
{
    public class RunRet
    {
        public string ret { get; set; }
        public RunRet()
        {
            ret = "OK";
        }
    }
    public class MainApp
    {
        //public event EventHandler igUpdate;
        public StatusMessage? currentStatus;
        public delegate void StopDelegate();
        public Database? the_db;
        public Database? the_app_db;
        public Microsoft.Azure.Cosmos.Container? the_container;
        public Microsoft.Azure.Cosmos.Container? the_chart_container;
        public Microsoft.Azure.Cosmos.Container? minute_container;
        public Microsoft.Azure.Cosmos.Container? candles_RSI_container;
        public Microsoft.Azure.Cosmos.Container? TicksContainer;
        public Microsoft.Azure.Cosmos.Container? trade_container;

        public string epicName = "";

        // private long _lngTickCount;

        public ChartUpdate currentTick { get; set; } = new ChartUpdate();
        public clsTradeUpdate? currentTrade { get; set; }
        public clsTradeUpdate? currentSIMLTrade { get; set; }
        public clsTradeUpdate? currentSIMSTrade { get; set; }
        public clsTradeUpdate? currentGRIDLTrade { get; set; }
        public clsTradeUpdate? currentGRIDSTrade { get; set; }
        public clsTradeUpdate suppTrade { get; set; } = new clsTradeUpdate();
        public CandleUpdate currentCandle { get; set; } = new CandleUpdate();

        public TradingBrainSettings? tb = new TradingBrainSettings();
        public Dictionary<string, string> TradeErrors = new Dictionary<string, string>();

        public List<requestedTrade> requestedTrades { get; set; } = new List<requestedTrade>();
        public string modelID { get; set; } = "";
        public string bolliID { get; set; } = "";
        public string gridLID { get; set; } = "";
        public string gridSID { get; set; } = "";

        public GetModelClass? model { get; set; }
        public ModelVars? modelVar { get; set; }

        public HubConnection? hubConnection { get; set; }
        //private bool FirstConfirmUpdate = true;
        //TradingBrainSettings firstTB;
        public int latestHour = 0;
        public System.Timers.Timer ti = new System.Timers.Timer();
        public bool marketOpen = false;
        public bool paused { get; set; } = false;
        public bool pausedAfterNGL { get; set; } = false;
        public string igAccountId { get; set; } = "";

        public bool lastTradeDeleted { get; set; } = false;
        public double lastTradeValue { get; set; } = 0;
        public double lastTradeSuppValue { get; set; } = 0;
        public bool lastTradeMaxQuantity { get; set; } = false;
        public bool retryOrder { get; set; } = false;
        public int retryOrderLimit { get; set; } = 0;
        public int retryOrderCount { get; set; } = 0;
        public string strategy { get; set; } = "";
        public string resolution { get; set; } = "";
        public bool futures { get; set; } = false;
        public string newDealReference { get; set; } = "";
        public string newSIMLDealReference { get; set; } = "";
        public string newSIMSDealReference { get; set; } = "";
        public string newGRIDLDealReference { get; set; } = "";
        public string newGRIDSDealReference { get; set; } = "";
        public modQuote lastCandle = new modQuote();
        public List<modQuote> candleList = new List<modQuote>();

        public IGContainer _igContainer = new IGContainer();
        public IGContainer? _igContainer2 = new IGContainer();
        const int MAX_WAIT_FOR_CLOSE_TIME = 60;
        public int closeAttemptCount = 0;

        public TradingBrainSettings setInitialModelVar()
        {
            //firstTB = await clsCommonFunctions.GetTradingBrainSettings(this.the_db, this.epicName);
            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("Application database is null in setInitialModelVar");
                }
                Task<TradingBrainSettings> tb = Task.Run<TradingBrainSettings>(async () => await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution));
                //return tb.Result;
                return tb.Result;
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "setInitialModelVar",
                    Epic = this.epicName
                };
                _ = log.Save();
            }
            return new TradingBrainSettings();

        }
        public string logName { get; set; } = "";
        public MainApp(Database? db, Database? appDb, string epic, IGContainer? igContainer, IGContainer? igContainer2, string strategy = "SMA", string resolution = "")
        {
            try
            {
                if (igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in MainApp");
                }
                if (igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in MainApp");
                }

                igAccountId = "";
                currentStatus = new StatusMessage();
                epicName = epic;
                tb = new TradingBrainSettings();


                the_db = db;
                the_app_db = appDb;
                if (the_db == null)
                {
                    throw new InvalidOperationException("Database is null in MainApp");
                }
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("Application Database is null in MainApp");
                }

                candles_RSI_container = the_db.GetContainer("Candles_RSI");
                the_container = the_db.GetContainer("CandleUpdate");
                the_chart_container = the_db.GetContainer("CandleTicks");
                TicksContainer = the_db.GetContainer("CandleTicks");
                trade_container = the_app_db.GetContainer("TradingBrainTrades");
                minute_container = the_db.GetContainer("Candles_RSI");
                //SetupDB(pms.epic);


                this.logName = IGModels.clsCommonFunctions.GetLogName(epic, strategy, resolution);
                ScopeContext.PushProperty("app", "TRADINGBRAIN/");
                ScopeContext.PushProperty("epic", epic + "/");
                ScopeContext.PushProperty("strategy", strategy + "/");
                ScopeContext.PushProperty("resolution", resolution + "/");

                this._igContainer = igContainer;
                this._igContainer2 = igContainer2;
                this.ti = new System.Timers.Timer();
                this.strategy = strategy;
                this.resolution = resolution;
                this.newDealReference = "";
                this.newSIMLDealReference = "";
                this.newSIMSDealReference = "";
                this.newGRIDLDealReference = "";
                this.newGRIDSDealReference = "";
                this.candleList = new List<modQuote>();

                IG_Epic epicObj = CommonFunctions.Get_IG_Epic(appDb, epic).Result;
                this.futures = epicObj.futures;

                bolliID = "";
                gridLID = "";
                gridSID = "";
                //tbClient = null;
                //forceT = forceTransport;
                currentStatus = new StatusMessage();
                requestedTrades = new List<requestedTrade>();
                ////////////////////////////////////
                // Get account id from app config //
                ////////////////////////////////////
                string region = IGModels.clsCommonFunctions.Get_AppSetting("region");
                //igAccountId = IGModels.clsCommonFunctions.Get_AppSetting("accountId." + region);
                igAccountId = igContainer.creds.igAccountId;

                retryOrderCount = 0;
                retryOrder = false;
                retryOrderLimit = 10;

                TradingBrain.Models.CommonFunctions.SaveLog("Info", "MainApp", "TB Started - " + igAccountId, appDb);

                lastTradeDeleted = false;
                lastTradeSuppValue = 0;
                lastTradeValue = 0;
                paused = false;
                pausedAfterNGL = false;
                epicName = epic;

                setupTradeErrors();
                setupMessaging();

                currentTick = new ChartUpdate();
                currentCandle = new CandleUpdate();
                currentTrade = new clsTradeUpdate();
                //_lngTickCount = 0;

                //the_container = container;
                //the_chart_container = chart_container;
                //minute_container = _minute_container;
                //TicksContainer = _TicksContainer;
                //trade_container = _trade_container;
                //candles_RSI_container = _candles_RSI_container;


                tb = new TradingBrainSettings();
                model = new GetModelClass
                {
                    the_db = db,
                    the_app_db = appDb
                };
                tb = setInitialModelVar().DeepCopy() ?? new TradingBrainSettings();
                modelVar = new ModelVars();
                modelVar = tb.lastRunVars.DeepCopy() ?? new ModelVars();
                model.modelVar = modelVar;
                model.exchangeClosedDates = IGModels.clsCommonFunctions.GetExchangeClosedDates(epicName, the_app_db).Result;
                // set the region we are in

                model.region = region;
                if (model.region == "")
                {
                    model.region = IGModels.clsCommonFunctions.Get_AppSetting("environment");
                    if (model.region == "demo") { model.region = "test"; }
                    if (model.region == "") { model.region = "live"; }
                }
                latestHour = DateTime.UtcNow.Hour;

                model.index = 30;
                model.logModel = true;
                model.thisModel = new modelInstance();
                modelID = System.Guid.NewGuid().ToString();

                model.modelLogs.modelRunID = modelID;
                model.modelLogs.modelRunDate = DateTime.UtcNow;
                model.TBRun = true;

                // move this to the beginning of run code.

                marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(model.modelLogs.modelRunDate, model.exchangeClosedDates, epicName, futures).Result;   //IGModels.clsCommonFunctions.IsTradingOpen(model.modelLogs.modelRunDate);
                CommonFunctions.AddStatusMessage($"Market open = {marketOpen}", "INFO", logName);

                CommonFunctions.AddStatusMessage("Model Run ID = " + modelID, "INFO", logName);

                currentStatus.calcAvgWinningTrade = tb.lastRunVars.calcAvgWinningTrade;

                //Work out the calculated average winning trade value
                if (this.strategy == "CASEYC" && tb.lastRunVars.calcAvgWinningTrade == 0)
                {
                    AccumulatedValues accumValues = IGModels.clsCommonFunctions.GetAccumulatedValues(this.the_app_db, this.epicName, this.strategy, this.resolution).Result;
                    if (accumValues != null)
                    {
                        if (accumValues.accumQuantity > 0)
                        {
                            modelVar.calcAvgWinningTrade = accumValues.accumProfit / accumValues.accumQuantity;
                            currentStatus.calcAvgWinningTrade = modelVar.calcAvgWinningTrade;
                        }
                    }
                }
                //TradingBrainSettings thisTB = clsCommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution).Result;
                currentStatus.startDate = model.modelLogs.modelRunDate;
                currentStatus.modelRunID = modelID;
                currentStatus.status = "running";
                currentStatus.epicName = this.epicName;
                currentStatus.doSuppTrades = model.doSuppTrades;
                currentStatus.doLongs = model.doLongs;
                currentStatus.doShorts = model.doShorts;
                currentStatus.countervar = tb.lastRunVars.counterVar;
                currentStatus.quantity = tb.lastRunVars.quantity;
                currentStatus.baseQuantity = tb.lastRunVars.baseQuantity;
                currentStatus.maxQuantity = tb.lastRunVars.maxQuantity;
                currentStatus.minQuantity = tb.lastRunVars.minQuantity;
                currentStatus.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
                currentStatus.gainMultiplier = tb.lastRunVars.gainMultiplier;
                currentStatus.suppQuantityMultiplier = tb.lastRunVars.suppQuantityMultiplier;
                currentStatus.suppStopPercentage = tb.lastRunVars.suppStopPercentage;
                currentStatus.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
                currentStatus.currentGain = tb.lastRunVars.currentGain;
                currentStatus.startingQuantity = tb.lastRunVars.startingQuantity;
                currentStatus.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
                currentStatus.seedAvgWinningTrade = tb.lastRunVars.seedAvgWinningTrade;
                //currentStatus.calcAvgWinningTrade = tb.lastRunVars.calcAvgWinningTrade;


                if (this.strategy == "SMA" || this.strategy == "SMA2")
                {
                    currentStatus.inputs = tb.runDetails.inputs;
                }
                else if (this.strategy == "RSI" ||
                    this.strategy == "RSI-ATR" ||
                    this.strategy == "RSI-CUML" ||
                    this.strategy == "CASEYC" ||
                    this.strategy == "VWAP" ||
                    this.strategy == "CASEYCSHORT" ||
                    this.strategy == "CASEYCEQUITIES")
                {
                    currentStatus.inputs_RSI = tb.runDetails.inputs_RSI;
                    currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
                }
                else if (this.strategy == "REI")
                {
                    currentStatus.inputs_REI = tb.runDetails.inputs_REI;
                    currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
                }

                //Broadcast message that we are running
                currentStatus.status = "running";
                currentStatus.strategy = this.strategy;
                currentStatus.resolution = this.resolution;
                Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));

                //AddStatusMessage($"Security token = {_igContainer.context.xSecurityToken}", "INFO");

                //Getting indCandles for GRID
                if (strategy == "GRID")
                {
                    AddStatusMessage($"Getting SMA candles ", "INFO");
                    this.candleList = RSI_LoadPrices.GetPriceDataSMAGRID(the_db, epicName, "SECOND", strategy, 5001).Result;
                    AddStatusMessage($"Number of SMA candles retrieved = {this.candleList.Count}", "INFO");
                }


                bool ret = GetPositions().Result;

                // set up timer here perhaps


            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "MainApp",
                    Epic = this.epicName
                };
                _ = log.Save();
            }
        }

        //public void onIgUpdate()
        //{
        //    EventHandler handler = igUpdate;
        //    if (null != handler) handler(this, EventArgs.Empty);
        //}

        public async Task<string> PlaceOrder(string direction, double quantity, double stopLoss, string accountId, decimal dealPrice)
        {
            string ret = "";
            bool newsession = false;
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in PlaceOrder");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in PlaceOrder");
                }
                //Get IG container based on account ID
                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"PlaceOrder - Account ID {accountId} not found", "ERROR");
                    return ret;
                }

                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in PlaceOrder"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in PlaceOrder"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in PlaceOrder"); }

                CommonFunctions.AddStatusMessage($"Placing new order = direction = {direction}, quantity = {quantity}, stopLoss = {stopLoss}, dealPrice = {dealPrice}, accountId = {accountId}", "INFO");
                TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceOrder", "Placing order - direction = " + direction + ", quantity = " + quantity + ", stopLoss = " + stopLoss + ", accountID = " + accountId, the_app_db);

                dto.endpoint.workingorders.create.v2.CreateWorkingOrderRequest pos = new dto.endpoint.workingorders.create.v2.CreateWorkingOrderRequest
                {
                    epic = this.epicName,
                    expiry = "DFB"
                };
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

                IgResponse<CreateWorkingOrderResponse> req = await _igContainerToUse.igRestApiClient.createWorkingOrderV2(pos);
                if (req != null)
                {
                    ret = req.Response.dealReference;
                    CommonFunctions.AddStatusMessage("Place order - " + direction + " - Status: " + req.StatusCode + " - account = " + accountId + " - deal reference = " + req.Response.dealReference, "INFO");
                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceOrder", "Place order - " + direction + " - Status: " + req.StatusCode + " - AccountId: " + accountId + " - deal reference = " + req.Response.dealReference, the_app_db);
                    if (req.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    _igContainerToUse.tbClient.ConnectToRest();
                    req = await _igContainerToUse.igRestApiClient.createWorkingOrderV2(pos);
                    if (req != null)
                    {
                        ret = req.Response.dealReference;
                        CommonFunctions.AddStatusMessage("Place order - " + direction + " - Status: " + req.StatusCode + " - account = " + accountId + " - deal reference = " + req.Response.dealReference, "INFO");
                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceOrder", "Place order - " + direction + " - Status: " + req.StatusCode + " - AccountId: " + accountId + " - deal reference = " + req.Response.dealReference, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "PlaceOrder"
                };
                await log.Save();
            }

            return ret;
        }
        public async Task DeleteOrder(string direction, double quantity, string dealID, string accountId)
        {
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in DeleteOrder");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in DeleteOrder");
                }

                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"PlaceOrder - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in DeleteOrder"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in DeleteOrder"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in DeleteOrder"); }

                bool newsession = false;
                TradingBrain.Models.CommonFunctions.SaveLog("Info", "DeleteOrder", "Deleting order", the_app_db);

                // create new pos which is basically a blank object
                dto.endpoint.workingorders.delete.v1.DeleteWorkingOrderRequest pos = new dto.endpoint.workingorders.delete.v1.DeleteWorkingOrderRequest();


                IgResponse<DeleteWorkingOrderResponse> ret = await _igContainerToUse.igRestApiClient.deleteWorkingOrder(dealID, pos);

                if (ret != null)
                {
                    CommonFunctions.AddStatusMessage("Delete order - " + direction + " - Status: " + ret.StatusCode, "INFO");
                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "DeleteOrder", "Delete order - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    _igContainerToUse.tbClient.ConnectToRest();
                    ret = await _igContainerToUse.igRestApiClient.deleteWorkingOrder(dealID, pos);
                    if (ret != null)
                    {
                        CommonFunctions.AddStatusMessage("Delete order - " + direction + " - Status: " + ret.StatusCode, "INFO");
                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "DeleteOrder", "Delete order - " + direction + " - Status: " + ret.StatusCode, the_app_db);
                    }
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "CloseDeal"
                };
                await log.Save();
            }
            //IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);
        }
        // public async Task<string> PlaceDeal(string direction, double quantity, double stopLoss, string accountId, bool setStopLevel = false)
        public async Task<string> PlaceDeal(string direction, double quantity, double stopLoss, string accountId, double target = 0)
        {
            string ret = string.Empty;
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in PlaceDeal");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in PlaceDeal");
                }

                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"PlaceOrder - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in PlaceDeal"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in PlaceDeal"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in PlaceDeal"); }

                bool newsession = false;
                CommonFunctions.AddStatusMessage($"Placing new deal = direction = {direction}, quantity = {quantity}, stopLoss = {stopLoss}, target = {target}, accountId = {accountId} ", "INFO");
                TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceDeal", "Placing deal - direction = " + direction + ", quantity = " + quantity + ", stopLoss = " + stopLoss + ", target = " + target + ", accountID = " + accountId, the_app_db);

                dto.endpoint.positions.create.otc.v2.CreatePositionRequest pos = new dto.endpoint.positions.create.otc.v2.CreatePositionRequest
                {
                    //pos.epic = "IX.D.NASDAQ.CASH.IP";
                    epic = this.epicName,
                    expiry = "DFB"
                };
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

                if (this.strategy == "SMA2" ||
                    this.strategy == "RSI" ||
                    this.strategy == "REI" ||
                    this.strategy == "RSI-ATR") // || 
                                                //this.strategy == "RSI-CUML" || 
                                                //this.strategy == "CASEYC")
                {
                    // If this is the SMA2 strategy, then we need to set the stop as a trailing stop (a stop that rises when the trade rises)
                    pos.trailingStop = true;
                    pos.trailingStopIncrement = 1;
                }
                if (stopLoss != 0)
                {
                    pos.stopDistance = Convert.ToDecimal(stopLoss);
                }
                if (target > 0)
                {
                    pos.limitDistance = Convert.ToDecimal(target);
                }

                pos.forceOpen = true;
                pos.currencyCode = "GBP";


                //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);
                if (this.strategy == "GRID")
                {

                    // for SMI we need to save the deal reference for both long and short trades
                    if (direction == "long")
                    {

                        this.newGRIDLDealReference = "";
                    }
                    else
                    {
                        this.newGRIDSDealReference = "";
                    }
                }
                IgResponse<CreatePositionResponse> resp = await _igContainerToUse.igRestApiClient.createPositionV2(pos);
                //clsCommonFunctions.AddStatusMessage("Here a", "ERROR");
                if (resp != null && resp.Response != null && !string.IsNullOrEmpty(resp.Response.dealReference))
                {
                    //clsCommonFunctions.AddStatusMessage("Here b", "ERROR");
                    if (this.strategy == "GRID")
                    {
                        //clsCommonFunctions.AddStatusMessage("Here c", "ERROR");
                        // for SMI we need to save the deal reference for both long and short trades
                        if (direction == "long")
                        {
                            //clsCommonFunctions.AddStatusMessage("Here d", "ERROR");
                            this.newGRIDLDealReference = resp.Response.dealReference;
                        }
                        else
                        {
                            this.newGRIDSDealReference = resp.Response.dealReference;
                        }
                    }
                    else
                    {
                        this.newDealReference = resp.Response.dealReference;
                    }
                    //clsCommonFunctions.AddStatusMessage("Here e", "ERROR");
                    ret = resp.Response.dealReference;
                    CommonFunctions.AddStatusMessage("Place deal - " + direction + " - Status: " + resp.StatusCode + " - account = " + accountId + " - deal reference = " + resp.Response.dealReference, "INFO");
                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceDeal", "Place deal - " + direction + " - Status: " + resp.StatusCode + " - AccountId: " + accountId, the_app_db);
                    if (resp.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }
                else
                {
                    CommonFunctions.AddStatusMessage("Place deal response is null so setting the GRIDL ref to blank", "ERROR");
                    if (this.strategy == "GRID")
                    {
                        // for SMI we need to save the deal reference for both long and short trades
                        if (direction == "long")
                        {
                            CommonFunctions.AddStatusMessage($"Setting new GRIDL deal reference to BLANK (existing one is {this.newGRIDLDealReference}", "INFO");
                            this.newGRIDLDealReference = "";
                        }
                        else
                        {
                            CommonFunctions.AddStatusMessage($"Setting new GRIDS deal reference to BLANK (existing one is {this.newGRIDLDealReference}", "INFO");
                            this.newGRIDSDealReference = "";
                        }
                    }
                }

                if (newsession)
                {
                    _igContainerToUse.tbClient.ConnectToRest();
                    resp = await _igContainerToUse.igRestApiClient.createPositionV2(pos);
                    if (resp != null && resp.Response != null && !string.IsNullOrEmpty(resp.Response.dealReference))
                    {
                        ret = resp.Response.dealReference;
                        CommonFunctions.AddStatusMessage("Place deal - " + direction + " - Status: " + resp.StatusCode + " - account = " + accountId + " - deal reference = " + resp.Response.dealReference, "INFO");
                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "PlaceDeal", "Place deal - " + direction + " - Status: " + resp.StatusCode + " - AccountId: " + accountId, the_app_db);
                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "PlaceDeal"
                };
                await log.Save();
            }
            return ret ?? string.Empty;
        }
        public async Task<string> CloseDeal(string direction, double quantity, string dealID, string accountId)
        {
            string dealRef = "";

            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in CloseDeal");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in CloseDeal");
                }

                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"CloseDeal - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in CloseDeal"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in CloseDeal"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in CloseDeal"); }
                bool newsession = false;
                //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Closing deal", the_app_db);
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
                IgResponse<ClosePositionResponse> ret = await _igContainerToUse.igRestApiClient.closePosition(pos);

                if (ret != null)
                {
                    dealRef = ret.Response.dealReference;
                    CommonFunctions.AddStatusMessage($"Close deal - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");
                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Close deal - " + direction + " - Status: " + ret.StatusCode + " - Deal ref: " + dealRef, the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }

                if (newsession)
                {
                    _igContainerToUse.tbClient.ConnectToRest();
                    ret = await _igContainerToUse.igRestApiClient.closePosition(pos);
                    if (ret != null)
                    {
                        dealRef = ret.Response.dealReference;
                        CommonFunctions.AddStatusMessage($"Close deal - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");
                        //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Close deal - " + direction + " - Status: " + ret.StatusCode + " - Deal ref: " + dealRef, the_app_db);
                    }
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "CloseDeal"
                };
                await log.Save();
            }

            return dealRef;

            //IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);
        }
        public async Task<string> CloseDealEpic(string direction, double quantity, string epic, string accountId)
        {
            string dealRef = "";

            try
            {
                // closes all trades for the epic
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in CloseDealEpic");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in CloseDealEpic");
                }

                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"CloseDealEpic - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in CloseDealEpic"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in CloseDealEpic"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in CloseDealEpic"); }
                List<tradeItem> trades = new List<tradeItem>();

                bool newsession = false;
                //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Closing deal", the_app_db);
                dto.endpoint.positions.close.v1.ClosePositionRequest pos = new dto.endpoint.positions.close.v1.ClosePositionRequest
                {
                    orderType = "MARKET",
                    //pos.epic = epic;
                    expiry = "DFB"
                };

                if (model != null)
                {
                    if (direction == "long")
                    {
                        pos.direction = "SELL";
                        trades = model.thisModel.gridLTrades.DeepCopy();
                    }
                    else
                    {
                        pos.direction = "BUY";
                        trades = model.thisModel.gridSTrades.DeepCopy();
                    }
                }

                foreach (tradeItem trade in trades)
                {
                    pos.dealId = trade.tbDealId;
                    pos.size = decimal.Round((decimal)trade.quantity, 2, MidpointRounding.AwayFromZero);
                    IgResponse<ClosePositionResponse> ret = await _igContainerToUse.igRestApiClient.closePosition(pos);

                    if (ret != null && ret.Response != null)
                    {
                        dealRef = ret.Response.dealReference;
                        CommonFunctions.AddStatusMessage($"Close epic {epic} - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");

                        if (ret.StatusCode.ToString() == "Unauthorized")
                        {
                            newsession = true;
                        }
                    }

                    if (newsession)
                    {
                        _igContainerToUse.tbClient.ConnectToRest();
                        ret = await _igContainerToUse.igRestApiClient.closePosition(pos);
                        if (ret != null)
                        {
                            dealRef = ret.Response.dealReference;
                            CommonFunctions.AddStatusMessage($"Close epic {epic} - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");
                        }
                    }
                    Thread.Sleep(200);
                }


            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "CloseDeal"
                };
                await log.Save();
            }

            return dealRef;

            //IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);
        }
        public async Task<string> EditDeal(double stopLoss, string dealID, double stopLossVar, string accountId)
        {
            string dealRef = "";
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container is null in EditDeal");
                }
                if (_igContainer2 == null && strategy == "GRID")
                {
                    throw new InvalidOperationException("IG Container2 is null in EditDeal");
                }

                IGContainer _igContainerToUse = new IGContainer();
                if (_igContainer.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer;
                }
                else if (strategy == "GRID" && _igContainer2 != null && _igContainer2.creds.igAccountId == accountId)
                {
                    _igContainerToUse = _igContainer2;
                }
                else
                {
                    // account ID not found
                    CommonFunctions.AddStatusMessage($"EditDeal - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse == null) { throw new InvalidOperationException("IG Container to use is null in EditDeal"); }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in EditDeal"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in EditDeal"); }

                CommonFunctions.AddStatusMessage("Editing deal. StopLoss = " + stopLoss + " - dealId = " + dealID, "INFO");
                bool newsession = false;
                TradingBrain.Models.CommonFunctions.SaveLog("Info", "EditDeal", "Editing deal - " + dealID, the_app_db);
                dto.endpoint.positions.edit.v2.EditPositionRequest pos = new dto.endpoint.positions.edit.v2.EditPositionRequest
                {
                    stopLevel = Convert.ToDecimal(stopLoss)
                };
                //this.model.modelVar.breakEvenVar
                if (this.strategy == "SMA2" || this.strategy == "SMA2")
                {
                    // Still keep the trailing stop even when it hits BEven
                    //if (this.model.modelVar.breakEvenVar == 1)
                    //{
                    //    // SMA2 - if break even has been reached, then turn off trailing stop
                    //    pos.trailingStop = false;
                    //    pos.trailingStopDistance = null;
                    //    pos.trailingStopIncrement = null;
                    //}
                    //else
                    //{
                    // SMA2 - set the new trailing stop values
                    pos.trailingStop = true;
                    pos.trailingStopDistance = (decimal)stopLossVar;
                    pos.trailingStopIncrement = 1;
                    //}
                }

                IgResponse<EditPositionResponse> ret = await _igContainerToUse.igRestApiClient.editPositionV2(dealID, pos);

                if (ret != null)
                {
                    //dealRef = ret.Response.dealReference;

                    CommonFunctions.AddStatusMessage($"Edit deal - Status: {ret.StatusCode} = stopLevel = {pos.stopLevel}, trailingStopDistance = {pos.trailingStopDistance}, trailingStopIncrement = {pos.trailingStopIncrement}, dealRef: {dealRef} ", "INFO");
                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "EditDeal", $"Edit deal - Status: {ret.StatusCode} = stopLevel = {pos.stopLevel}, trailingStopDistance = {pos.trailingStopDistance}, trailingStopIncrement = {pos.trailingStopIncrement}, dealRef: {dealRef}", the_app_db);
                    if (ret.StatusCode.ToString() == "Unauthorized")
                    {
                        newsession = true;
                    }
                }
                if (newsession)
                {
                    CommonFunctions.AddStatusMessage("Trying to reconnect to REST", "INFO");
                    _igContainerToUse.tbClient.ConnectToRest();
                    ret = await _igContainerToUse.igRestApiClient.editPositionV2(dealID, pos);
                    if (ret != null)
                    {
                        //dealRef = ret.Response.dealReference;
                        CommonFunctions.AddStatusMessage($"Edit deal - Status: {ret.StatusCode} = stopLevel = {pos.stopLevel}, trailingStopDistance = {pos.trailingStopDistance}, trailingStopIncrement = {pos.trailingStopIncrement}, dealRef: {dealRef} ", "INFO");
                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "EditDeal", $"Edit deal - Status: {ret.StatusCode} = stopLevel = {pos.stopLevel}, trailingStopDistance = {pos.trailingStopDistance}, trailingStopIncrement = {pos.trailingStopIncrement}, dealRef: {dealRef}", the_app_db);

                    }
                }
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "EditDeal"
                };
                await log.Save();
            }
            return dealRef;
        }


        //public async Task<RunRet> RunCode(object sender, System.Timers.ElapsedEventArgs e)
        // {
        //     RunRet taskRet = new RunRet();
        //     this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //     //MappedDiagnosticsLogicalContext.Set("jobId", this.logName);
        //     ScopeContext.PushProperty("jobId", this.logName);
        //     bool liveMode = true;
        //     bool marketOpen = false;

        //     DateTime dtNow = DateTime.UtcNow;
        //     DateTime _startTime;

        //     // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //     int seconds = dtNow.Second;
        //     if (seconds < 59)
        //     {
        //         _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //     }
        //     else
        //     {
        //         _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0);
        //     }


        //     DateTime _endTime = _startTime;


        //     if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //     {
        //         // Check if the market is currently open. If it is not then skip till next time.
        //         //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
        //         marketOpen = await IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates,this.epicName);
        //         if (marketOpen)
        //         {
        //             _igContainer.tbClient.FirstConfirmUpdate = false;
        //             string param = "";

        //             //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Running code");
        //             CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
        //             CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //             CommonFunctions.AddStatusMessage($" - Epic       - {epicName}", "INFO", logName);
        //             CommonFunctions.AddStatusMessage($" - Strategy   - {strategy}", "INFO", logName);
        //             CommonFunctions.AddStatusMessage($" - Resolution - {resolution}", "INFO", logName);

        //             CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
        //             CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //             //var watch = new System.Diagnostics.Stopwatch();
        //             //var bigWatch = new System.Diagnostics.Stopwatch();
        //             //bigWatch.Start();
        //             try
        //             {
        //                 //watch.Start();


        //                 this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy);

        //                 CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);


        //                 // If the trade has just been deleted then sort out the CFL

        //                 if (lastTradeDeleted)
        //                 {
        //                     try
        //                     {
        //                         //clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                         double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                         //clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                         if (nettPosition <= 0)
        //                         {
        //                             //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                         }
        //                         else
        //                         {
        //                             //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                             //if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                             //model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                         }

        //                         //tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                         //tb.lastRunVars.currentGain = model.modelVar.currentGain;

        //                         // check to see if the trade just finished lost at max quantity, if so then we need to reset the vars

        //                         // Removed for new plan to allow it to stay at max quantity until it gets out of its pickle

        //                         //clsCommonFunctions.AddStatusMessage($"checking if reset required - lastTradeMaxQuantity = {lastTradeMaxQuantity}", "DEBUG");
        //                         //if (lastTradeMaxQuantity)
        //                         //{
        //                         //    clsCommonFunctions.AddStatusMessage($"old lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG");
        //                         //    tb.lastRunVars.currentGain = Math.Max(tb.lastRunVars.currentGain - model.modelVar.carriedForwardLoss, 0);
        //                         //    tb.lastRunVars.carriedForwardLoss = 0;
        //                         //    tb.lastRunVars.quantity = tb.lastRunVars.minQuantity;
        //                         //    tb.lastRunVars.counter = 0;
        //                         //    tb.lastRunVars.maxQuantity = tb.lastRunVars.minQuantity * tb.lastRunVars.maxQuantityMultiplier;
        //                         //    model.modelVar.currentGain = tb.lastRunVars.currentGain;
        //                         //    model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                         //    model.modelVar.quantity = tb.lastRunVars.quantity;
        //                         //    model.modelVar.counter = tb.lastRunVars.counter;
        //                         //    model.modelVar.maxQuantity = tb.lastRunVars.maxQuantity;

        //                         //    clsCommonFunctions.AddStatusMessage($"new lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG");
        //                         //}



        //                         //await tb.SaveDocument(the_app_db);

        //                         CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                     }

        //                     catch (Exception ex)
        //                     {
        //                         CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                     }

        //                     lastTradeDeleted = false;
        //                     lastTradeValue = 0;
        //                     lastTradeSuppValue = 0;
        //                     lastTradeMaxQuantity = false;
        //                 }
        //                 //watch.Stop();
        //                 //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - GetSettings - Time taken = " + watch.ElapsedMilliseconds);

        //                 //Determine if we are to do long and or short trades
        //                 model.doLongs = tb.doLongs;
        //                 model.doShorts = tb.doShorts;
        //                 model.doSuppTrades = tb.doSuppTrades;
        //                 //tb.lastRunVars.doLongsVar = tb.doLongs;
        //                 //tb.lastRunVars.doShortsVar = tb.doShorts;
        //                 //tb.lastRunVars.doSuppTradesVar = tb.doSuppTrades;

        //                 CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                 CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                 CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);

        //                 model.thisModel.inputs = this.tb.runDetails.inputs.DeepCopy();
        //                 model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                 model.thisModel.matchProTrend = false;
        //                 model.modelVar.counterVar = model.thisModel.counterVar;
        //                 //model.modelVar = tb.lastRunVars;

        //                 model.startTime = dtNow;
        //                 model.modelRunID = modelID;
        //                 if (model.modelVar.quantity == 0)
        //                 {
        //                     //model.modelVar.baseQuantity = tb.runDetails.quantity;
        //                     //model.modelVar.startingQuantity = tb.runDetails.quantity;
        //                     //model.modelVar.startingQuantity = tb.lastRunVars.startingQuantity;

        //                     model.modelVar.minQuantity = tb.runDetails.quantity;
        //                     model.modelVar.quantity = tb.runDetails.quantity;
        //                 }

        //                 //model.counterVar = tb.runDetails.counterVar;
        //                 currentStatus.inputs = tb.runDetails.inputs;
        //                 currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                 //currentStatus.quantity = model.modelVar.quantity;
        //                 currentStatus.quantity = tb.lastRunVars.minQuantity;


        //                 // Here is where I can force it to run for a full day, rather than just for the current minute. Just to test like.

        //                 if (Environment.GetCommandLineArgs().Length >= 3)
        //                 {
        //                     param = Environment.GetCommandLineArgs()[2];
        //                 }
        //                 if (param == "DEBUG")
        //                 {
        //                     _startTime = new DateTime(2024, 11, 11, 16, 12, 00);
        //                     _endTime = new DateTime(2024, 11, 12, 14, 30, 00);
        //                     liveMode = false;
        //                 }

        //                 //while (_startTime <= _endTime)
        //                 //{
        //                 //bigWatch.Restart();


        //                 /////////////////////////////////////////////////////////
        //                 // using the candle time determine which inputs to use //
        //                 /////////////////////////////////////////////////////////
        //                 //thisInput = IGModels.clsCommonFunctions.GetInputs(tb.runDetails.inputs, _startTime);

        //                 // Get the last candle so we can get the spread

        //                 //clsMinuteCandle thisCandle = await Get_MinuteCandle(the_app_db, minute_container, epicName, _endTime);
        //                 CommonFunctions.AddStatusMessage($"Checking Spread ", "INFO", logName);
        //                 double thisSpread = await Get_SpreadFromLastCandle(the_app_db, minute_container, _endTime);

        //                 CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO", logName);
        //                 //thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadv2(tb.runDetails.inputs, thisSpread);
        //                 modelInstanceInputs  thisInput = tb.runDetails.inputs.FirstOrDefault(t => t.spread == thisSpread);
        //                 if (thisInput == null)
        //                 {
        //                     CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}, trying spread 0" , "INFO", logName);
        //                     thisInput = tb.runDetails.inputs.FirstOrDefault(t => t.spread == 0);
        //                 }
        //                 //clsCommonFunctions.AddStatusMessage($"Checking A ", "INFO", logName);
        //                 if (thisInput == null)
        //                 {
        //                     CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                 }
        //                 else
        //                 {
        //                     //Create the current candle
        //                     // only create a new min record if we are in live
        //                     // 
        //                     // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                     // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);

        //                     bool createMinRecord = liveMode;
        //                     if (model.region == "test" || strategy == "SMA2") { createMinRecord = false; }
        //                     //clsCommonFunctions.AddStatusMessage($"Checking B ", "INFO", logName);
        //                     model.candles.currentCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime, epicName, minute_container, TicksContainer, false, createMinRecord, the_app_db, model.exchangeClosedDates);
        //                     //clsCommonFunctions.AddStatusMessage($"Checking C ", "INFO", logName);

        //                     // Check to see if we have prev and prev2 candles already. If not (i.e. first run) then go get them.
        //                     //clsCommonFunctions.AddStatusMessage($"Checking D ", "INFO", logName);
        //                     if (model.candles.prevCandle.candleStart == DateTime.MinValue)
        //                     {
        //                         model.candles.prevCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-1), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);

        //                     }
        //                     //clsCommonFunctions.AddStatusMessage($"Checking E ", "INFO", logName);
        //                     if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
        //                     {
        //                         model.candles.prevCandle2 = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-2), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);
        //                     }
        //                     //clsCommonFunctions.AddStatusMessage($"Checking F ", "INFO", logName);
        //                     DateTime getStartDate = await model.getPrevMAStartDate(model.candles.currentCandle.candleStart,model.candles.currentCandle.epicName);
        //                     //clsCommonFunctions.AddStatusMessage($"Checking G ", "INFO", logName);
        //                     IG_Epic epic = new IG_Epic(epicName);
        //                     clsMinuteCandle prevMa = await Get_MinuteCandle(the_db, minute_container, epic, getStartDate);
        //                     model.candles.prevMACandle.mA30MinTypicalLongClose = prevMa.MovingAverages30Min[thisInput.var3 - 1].movingAverage.Close;
        //                     model.candles.prevMACandle.mA30MinTypicalShortClose = prevMa.MovingAverages30Min[thisInput.var13 - 1].movingAverage.Close;
        //                     //clsCommonFunctions.AddStatusMessage($"Checking H ", "INFO", logName);
        //                     CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);
        //                     //clsCommonFunctions.AddStatusMessage($"values before  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");

        //                     CommonFunctions.AddStatusMessage($"currentCandle.ma30MinTypicalLongClose:{model.candles.currentCandle.mA30MinTypicalLongClose} currentCandle.ma30MinTypicalLongClose:{model.candles.currentCandle.mA30MinTypicalShortClose}", "DEBUG");
        //                     CommonFunctions.AddStatusMessage($"prevCandle.ma30MinTypicalLongClose:{model.candles.prevCandle.mA30MinTypicalLongClose} prevCandle.ma30MinTypicalLongClose:{model.candles.prevCandle.mA30MinTypicalShortClose}", "DEBUG");
        //                     CommonFunctions.AddStatusMessage($"prevCandle2.ma30MinTypicalLongClose:{model.candles.prevCandle2.mA30MinTypicalLongClose} prevCandle2.ma30MinTypicalLongClose:{model.candles.prevCandle2.mA30MinTypicalShortClose}", "DEBUG");
        //                     CommonFunctions.AddStatusMessage($"prevMACandle.ma30MinTypicalLongClose:{model.candles.prevMACandle.mA30MinTypicalLongClose} prevMACandle.ma30MinTypicalLongClose:{model.candles.prevMACandle.mA30MinTypicalShortClose}", "DEBUG");




        //                     // Check if we are still on market but were unable to set up the order
        //                     //if (model.onMarket && retryOrder)
        //                     //{
        //                     //    if (model.thisModel.currentTrade.attachedOrder != null)
        //                     //    {
        //                     //        clsCommonFunctions.OrderValues orderValues = new clsCommonFunctions.OrderValues();
        //                     //        string newOrderDirection = "";
        //                     //        if (currentTrade.direction.ToUpper() == "LONG" || currentTrade.direction.ToUpper() == "BUY")
        //                     //        {
        //                     //            newOrderDirection = "BUY";
        //                     //        }
        //                     //        else { newOrderDirection = "SELL"; }

        //                     //        orderValues.SetOrderValues(newOrderDirection, this);
        //                     //        clsCommonFunctions.AddStatusMessage($"Retrying placing new order - direction:{newOrderDirection}, stopDistance:{orderValues.stopDistance}, level:{orderValues.level}", "INFO", logName);
        //                     //        requestedTrade reqTrade = new requestedTrade();
        //                     //        reqTrade.dealType = "ORDER";
        //                     //        reqTrade.dealReference = await PlaceOrder(newOrderDirection, orderValues.quantity, orderValues.stopDistance, igAccountId, orderValues.level);
        //                     //        requestedTrades.Add(reqTrade);
        //                     //    }
        //                     //}


        //                     //model.RunProTrendCodeV2(model.candles);
        //                     model.RunProTrendCodeV3(model.candles);

        //                     CommonFunctions.AddStatusMessage($"values after  run         - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);
        //                     //clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");
        //                     CommonFunctions.AddStatusMessage($"Current standard deviation - {model.candles.currentCandle.candleData.StdDev}", "DEBUG", logName);

        //                     CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                     CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                     //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG");
        //                     //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG");


        //                     if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                     if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                     //model.sellShort = true;

        //                     if (param != "DEBUG")
        //                     {
        //                         string thisDealRef = "";
        //                         //string dealType = "";
        //                         //bool dealSent = false;

        //                         double targetVar = thisInput.targetVarInput / 100 + 1;
        //                         double targetVarShort = thisInput.targetVarInputShort / 100 + 1;

        //                         //////////////////////////////////////////////////////////////////////////////////////////////
        //                         // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                         //////////////////////////////////////////////////////////////////////////////////////////////

        //                         double currentStop = 0;
        //                         double newStop = 0;
        //                         double currentPrice = 0;

        //                         if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                         {
        //                             currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                             newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                             currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

        //                             CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                             CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                             if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                             {
        //                                 TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                 model.sellLong = true;
        //                             }

        //                         }
        //                         if (model.shortOnMarket && model.modelVar.breakEvenVar == 0)
        //                         {
        //                             currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                             newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                             currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

        //                             CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                             CommonFunctions.AddStatusMessage($"[LONG] Current stop > newStop = {currentStop > newStop},  currentPrice > newStop = {currentPrice > newStop}, currentPrice < currentStop {currentPrice < currentStop}  ", "DEBUG", logName);


        //                             if (currentStop > newStop && currentPrice > newStop && currentPrice < currentStop)
        //                             {
        //                                 TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "buying short because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now higher than the new stop.", the_app_db);
        //                                 model.buyShort = true;
        //                             }
        //                         }


        //                         // Check if target price has changed when an attached order is set but the supp trade hasn't started yet. //

        //                         //if (model.onMarket && this.currentTrade != null && model.onSuppTrade == false && model.thisModel.currentTrade.attachedOrder != null)
        //                         //{
        //                         //    OrderValues orderValues = new OrderValues();
        //                         //    string direction = "";
        //                         //    if (model.longOnmarket)
        //                         //    {
        //                         //        direction = "buy";
        //                         //    }
        //                         //    if (model.shortOnMarket)
        //                         //    {
        //                         //        direction = "sell";
        //                         //    }
        //                         //    orderValues.SetOrderValues(direction, this);
        //                         //    clsCommonFunctions.AddStatusMessage($"Checking order level {orderValues.level} = attached order level {model.thisModel.currentTrade.attachedOrder.orderLevel}", "DEBUG", logName);
        //                         //    if (orderValues.level != model.thisModel.currentTrade.attachedOrder.orderLevel)
        //                         //    {
        //                         //        // get stuff done :- specifically get the order values 


        //                         //        if (orderValues.stopDistance > 0 && orderValues.level > 0)
        //                         //        {
        //                         //            EditOrder(orderValues.level, orderValues.stopDistance, model.thisModel.currentTrade.attachedOrder.dealId);
        //                         //        }


        //                         //    }
        //                         //}


        //                         //////////////////////////////////////////////////////////////////

        //                         // Check first that the stop loss is more than 4x the current std dev

        //                         double stdDev = model.candles.currentCandle.candleData.StdDev;
        //                         double sL = 0;
        //                         double stdDevSL = 0;
        //                         if (model.buyLong)
        //                         {
        //                             sL = (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
        //                             stdDevSL = sL / stdDev;
        //                         }
        //                         else
        //                         {
        //                             if (model.sellShort)
        //                             {
        //                                 sL = (double)thisInput.var5 * Math.Abs((double)targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
        //                                 stdDevSL = sL / stdDev;
        //                             }
        //                         }
        //                         //bool stdDevOK = true;
        //                         if (model.buyLong || model.sellShort)
        //                         {
        //                             if (stdDevSL < 4)
        //                             {
        //                                 //stdDevOK = false;
        //                                 CommonFunctions.AddStatusMessage($"Stop loss ({sL}) is not more than 4x the current std dev ({stdDev}) - stdDevSL = {stdDevSL}", "ERROR", logName);
        //                                 model.buyLong = false;
        //                                 model.sellShort = false;
        //                             }
        //                             else
        //                             {
        //                                 CommonFunctions.AddStatusMessage($"Stop loss ({sL}) is more than 4x the current std dev ({stdDev}) - stdDevSL = {stdDevSL}", "INFO", logName);
        //                             }
        //                         }


        //                         if (model.buyLong && this.currentTrade == null)
        //                         {
        //                             CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                             TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                             model.stopLossVar = (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
        //                             requestedTrade reqTrade = new requestedTrade();
        //                             reqTrade.dealType = "POSITION";
        //                             reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId);
        //                             requestedTrades.Add(reqTrade);
        //                             //if (reqTrade.dealReference != "")
        //                             //{
        //                             //    //dealSent = true;
        //                             //    thisDealRef = reqTrade.dealReference;
        //                             //    //dealType = "PlaceDeal";
        //                             //}

        //                         }
        //                         else
        //                         {
        //                             if (model.sellLong)
        //                             {
        //                                 TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                 CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
        //                                 //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                 string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId,this.igAccountId);
        //                                 //if (dealRef != "")
        //                                 //{
        //                                 //    //dealSent = true;
        //                                 //    thisDealRef = dealRef;
        //                                 //    //dealType = "PlaceDeal";
        //                                 //}

        //                                 if (model.doSuppTrades && model.onSuppTrade)
        //                                 {
        //                                     TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong Supplementary", the_app_db);
        //                                     CommonFunctions.AddStatusMessage("SellLong Supplementary activated", "INFO", logName);
        //                                     dealRef = await CloseDeal("long", (double)this.suppTrade.size, this.suppTrade.dealId, this.igAccountId);
        //                                     //if (dealRef != "")
        //                                     //{
        //                                     //    //dealSent = true;
        //                                     //    thisDealRef = dealRef;
        //                                     //    //dealType = "PlaceDeal";
        //                                     //}
        //                                 }
        //                             }
        //                         }

        //                         if (model.sellShort && this.currentTrade == null)
        //                         {
        //                             CommonFunctions.AddStatusMessage("SellShort activated", "INFO", logName);
        //                             TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);
        //                             model.stopLossVar = (double)thisInput.var5 * Math.Abs((double)targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
        //                             requestedTrade reqTrade = new requestedTrade();
        //                             reqTrade.dealType = "POSITION";
        //                             reqTrade.dealReference = await PlaceDeal("short", model.modelVar.quantity, model.stopLossVar, this.igAccountId);
        //                             requestedTrades.Add(reqTrade);
        //                             //if (reqTrade.dealReference != "")
        //                             //{
        //                             //    //dealSent = true;
        //                             //    thisDealRef = reqTrade.dealReference;
        //                             //    //dealType = "PlaceDeal";
        //                             //}
        //                         }
        //                         else
        //                         {
        //                             if (model.buyShort)
        //                             {
        //                                 CommonFunctions.AddStatusMessage("BuyShort activated", "INFO", logName);
        //                                 TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort)", the_app_db);
        //                                 //CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                 string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                 //if (dealRef != "")
        //                                 //{
        //                                 //    //dealSent = true;
        //                                 //    thisDealRef = dealRef;
        //                                 //   // dealType = "PlaceDeal";
        //                                 //}

        //                                 if (model.doSuppTrades && model.onSuppTrade)
        //                                 {
        //                                     CommonFunctions.AddStatusMessage("BuyShort Supplementary activated", "INFO", logName);
        //                                     TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort Supplementary", the_app_db);
        //                                     string dealRefSupp = await CloseDeal("short", (double)this.suppTrade.size, this.suppTrade.dealId, this.igAccountId);
        //                                 }
        //                             }
        //                         }


        //                         if (model.longOnmarket)
        //                         {
        //                             CommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                             if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                             {



        //                                 //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                 decimal? currentStopLevel = this.currentTrade.stopLevel;
        //                                 if (model.modelVar.breakEvenVar == 1)
        //                                 {


        //                                     this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                     CommonFunctions.AddStatusMessage($"EditLong activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                     TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal - BREAKEVEN", the_app_db);
        //                                     string dealRef = await EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId,  model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     //if (dealRef != "")
        //                                     //{
        //                                     //    //dealSent = true;
        //                                     //    thisDealRef = dealRef;
        //                                     //    //dealType = "PlaceDeal";
        //                                     //}

        //                                     //If on a supp trade then set that trades sl to be the same as the current trade
        //                                     if (model.onSuppTrade && this.suppTrade != null)
        //                                     {
        //                                         CommonFunctions.AddStatusMessage($"EditLong SUPP activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Long Deal BREAKEVEN", the_app_db);
        //                                         string dealRefSupp = await EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     }
        //                                 }
        //                                 else

        //                                 {

        //                                     if (model.onSuppTrade)
        //                                     {
        //                                         // on a supp trade so make the current trade have the same stop loss value so they close at the same time.
        //                                         //this.currentTrade.stopLevel = model.suppStopLossVar;
        //                                         this.model.thisModel.currentTrade.stopLossValue = (double)this.suppTrade.stopLevel;
        //                                         this.currentTrade.stopLevel = this.suppTrade.stopLevel;

        //                                         CommonFunctions.AddStatusMessage($"EditLong Long SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Long Deal ", the_app_db);
        //                                         string dealRefSupp = await EditDeal((double)this.currentTrade.stopLevel, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     }
        //                                     else
        //                                     {
        //                                         this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                         CommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                         string dealRef = await EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                         //if (dealRef != "")
        //                                         //{
        //                                         //    //dealSent = true;
        //                                         //    thisDealRef = dealRef;
        //                                         //    //dealType = "PlaceDeal";
        //                                         //}
        //                                     }
        //                                 }

        //                             }
        //                         }
        //                         if (model.shortOnMarket)
        //                         {
        //                             CommonFunctions.AddStatusMessage($"[SHORT] Check if sellPrice ({model.thisModel.currentTrade.sellPrice}) + stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                             if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                             {
        //                                 CommonFunctions.AddStatusMessage("EditShort activated", "INFO", logName);
        //                                 TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal", the_app_db);


        //                                 //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                 decimal? currentStopLevel = this.currentTrade.stopLevel;

        //                                 if (model.modelVar.breakEvenVar == 1)
        //                                 {

        //                                     this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                     CommonFunctions.AddStatusMessage($"EditShort activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                     TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal - BREAKEVEN", the_app_db);
        //                                     string dealRef = await EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     //if (dealRef != "")
        //                                     //{
        //                                     //    //dealSent = true;
        //                                     //    thisDealRef = dealRef;
        //                                     //    //dealType = "PlaceDeal";
        //                                     //}
        //                                     //If on a supp trade then set that trades sl to be the same as the current trade
        //                                     if (model.onSuppTrade && this.suppTrade != null)
        //                                     {
        //                                         CommonFunctions.AddStatusMessage($"EditShort SUPP activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short SUPP Deal - BREAKEVEN", the_app_db);
        //                                         string dealRefSupp = await EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     }
        //                                 }
        //                                 else
        //                                 {
        //                                     if (model.onSuppTrade)
        //                                     {
        //                                         // on a supp trade so make the current trade have the same stop loss value so they close at the same time.
        //                                         //this.currentTrade.stopLevel = model.suppStopLossVar;
        //                                         this.model.thisModel.currentTrade.stopLossValue = (double)this.suppTrade.stopLevel;
        //                                         this.currentTrade.stopLevel = this.suppTrade.stopLevel;

        //                                         CommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
        //                                         string dealRefSupp = await EditDeal((double)this.currentTrade.stopLevel, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                     }
        //                                     else
        //                                     {
        //                                         this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                         CommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                         TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
        //                                         string dealRef = await EditDeal((double)model.thisModel.currentTrade.sellPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
        //                                         //if (dealRef != "")
        //                                         //{
        //                                         //    //dealSent = true;
        //                                         //    thisDealRef = dealRef;
        //                                         //   // dealType = "PlaceDeal";
        //                                         //}
        //                                     }
        //                                 }

        //                             }
        //                         }


        //                     }
        //                     try
        //                     {
        //                         if (model.thisModel.currentTrade != null)
        //                         {
        //                             if (model.thisModel.currentTrade.targetPrice != 0)
        //                             {
        //                                 await model.thisModel.currentTrade.SaveDocument(this.trade_container);
        //                             }
        //                             ;
        //                         }
        //                         if (model.thisModel.suppTrade != null)
        //                         {
        //                             if (model.thisModel.suppTrade.targetPrice != 0)
        //                             {
        //                                 await model.thisModel.suppTrade.SaveDocument(this.trade_container);
        //                             }
        //                             ;
        //                         }
        //                     }
        //                     catch (Exception ex)
        //                     {
        //                         Log log = new Log(the_app_db);
        //                         log.Log_Message = ex.ToString();
        //                         log.Log_Type = "Error";
        //                         log.Log_App = "RunCode";
        //                         await log.Save();

        //                     }

        //                     //reset any deal variables that could have been placed by the RunCode
        //                     model.buyLong = false;
        //                     model.buyShort = false;
        //                     model.sellLong = false;
        //                     model.sellShort = false;
        //                     model.buyLongSupp = false;
        //                     model.buyShortSupp = false;
        //                     model.sellLongSupp = false;
        //                     model.sellShortSupp = false;

        //                     if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
        //                     {

        //                     }


        //                     if (model.modelLogs.logs.Count() > 0)
        //                     {
        //                         ModelLog log = new ModelLog();
        //                         log = model.modelLogs.logs[0];
        //                         log.modelRunID = modelID;
        //                         log.runDate = _startTime;
        //                         log.id = System.Guid.NewGuid().ToString();
        //                         if (model.onMarket)
        //                         {
        //                             currentStatus.onMarket = true;
        //                             if (model.longOnmarket)
        //                             {
        //                                 currentStatus.tradeType = "Long";
        //                             }
        //                             if (model.shortOnMarket)
        //                             {
        //                                 currentStatus.tradeType = "Short";
        //                             }
        //                             currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                             currentStatus.count = model.thisModel.currentTrade.count;

        //                         }
        //                         else
        //                         {
        //                             currentStatus.onMarket = false;
        //                             currentStatus.tradeType = "";
        //                         }

        //                         currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                         currentStatus.accountId = this.igAccountId;
        //                         currentStatus.startingQuantity = modelVar.startingQuantity;
        //                         currentStatus.minQuantity = modelVar.minQuantity;
        //                         currentStatus.maxQuantity = modelVar.maxQuantity;
        //                         currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                         currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                         currentStatus.currentGain = modelVar.currentGain;
        //                         currentStatus.baseQuantity = modelVar.baseQuantity;
        //                         currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                         currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                         currentStatus.doSuppTrades = model.doSuppTrades;
        //                         currentStatus.doShorts = model.doShorts;
        //                         currentStatus.doLongs = model.doLongs;
        //                         //if(strategy == "RSI")
        //                         //{
        //                         currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                         //}
        //                         //currentStatus.epicName = this.epicName;
        //                         //send log to the website
        //                         model.modelLogs.logs[0].epicName = this.epicName;
        //                         Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                         Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                         //save log to the database
        //                         Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                         await log.SaveDocument(logContainer);
        //                         model.modelLogs.logs = new List<ModelLog>();

        //                     }
        //                 }
        //                 model.candles.prevCandle2 = model.candles.prevCandle.DeepCopy();
        //                 model.candles.prevCandle = model.candles.currentCandle.DeepCopy();
        //                 _startTime = _startTime.AddMinutes(1);
        //                 //bigWatch.Stop();
        //                 //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //             }
        //             catch (Exception ex)
        //             {
        //                 Log log = new Log(the_app_db);
        //                 log.Log_Message = ex.ToString();
        //                 log.Log_Type = "Error";
        //                 log.Log_App = "RunCode";
        //                 await log.Save();
        //             }

        //             //bigWatch.Stop();
        //             //clsCommonFunctions.AddStatusMessage( "Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //             CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);
        //             // call the accounts api each hour just so we ensure the tokens don't expire
        //             //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //         }
        //         else
        //         {
        //             CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //         }
        //     }
        //     else
        //     {
        //         CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //         pausedAfterNGL = false;
        //     }

        //     if (latestHour != DateTime.UtcNow.Hour)
        //     {
        //         //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //         //try
        //         //{
        //         //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //         //    if (ret != null)
        //         //    {
        //         //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //         //    }
        //             latestHour = DateTime.UtcNow.Hour;
        //         //}
        //         //catch (Exception ex)
        //         //{
        //         //    Log log = new Log(the_app_db);
        //         //    log.Log_Message = ex.ToString();
        //         //    log.Log_Type = "Error";
        //         //    log.Log_App = "RunCode";
        //         //    await log.Save();
        //         //}

        //     }

        //     //if (liveMode)
        //     //{
        //     //    ti.Interval = GetInterval();
        //     //    ti.Start();
        //     //}
        //     return taskRet;
        // }
        public async Task<RunRet> RunCodeV5(object? sender, System.Timers.ElapsedEventArgs e)
        {

            //bool liveMode = true;
            bool marketOpen = false;

            DateTime dtNow = DateTime.UtcNow;
            DateTime _startTime;
            resolution = "MINUTE";


            RunRet taskRet = new RunRet();
            this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
            ScopeContext.PushProperty("app", "TRADINGBRAIN/");
            ScopeContext.PushProperty("epic", this.epicName + "/");
            ScopeContext.PushProperty("strategy", strategy + "/");
            ScopeContext.PushProperty("resolution", resolution + "/");
            try
            {

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

                if (model == null) { throw new InvalidOperationException("Model is null in RunCodeV5"); }
                if (_igContainer == null || _igContainer.tbClient == null) { throw new InvalidOperationException("IG Container is null in RunCodeV5"); }
                if (modelVar == null) { throw new InvalidOperationException("ModelVars is null in RunCodeV5"); }
                if (currentStatus == null) { throw new InvalidOperationException("CurrentStatus is null in RunCodeV5"); }


                if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
                {
                    // Check if the market is currently open. If it is not then skip till next time.
                    //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
                    marketOpen = await IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName);
                    if (marketOpen)
                    {
                        _igContainer.tbClient.FirstConfirmUpdate = false;
                        string param = "";

                        //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Running code");
                        CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
                        CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
                        CommonFunctions.AddStatusMessage($" - Epic       - {epicName}", "INFO", logName);
                        CommonFunctions.AddStatusMessage($" - Strategy   - {strategy}", "INFO", logName);
                        CommonFunctions.AddStatusMessage($" - Resolution - {resolution}", "INFO", logName);

                        CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
                        CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
                        //var watch = new System.Diagnostics.Stopwatch();
                        //var bigWatch = new System.Diagnostics.Stopwatch();
                        //bigWatch.Start();

                        //watch.Start();


                        this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy);

                        if (this.tb == null || this.tb.runDetails == null || this.tb.runDetails.inputs == null || this.tb.lastRunVars == null)
                        {
                            throw new InvalidOperationException("Trading Brain settings not found");
                        }
                        CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);


                        // If the trade has just been deleted then sort out the CFL

                        if (lastTradeDeleted)
                        {
                            try
                            {
                                double nettPosition = lastTradeValue + lastTradeSuppValue;
                                CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
                            }

                            catch (Exception ex)
                            {
                                CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
                            }

                            lastTradeDeleted = false;
                            lastTradeValue = 0;
                            lastTradeSuppValue = 0;
                            lastTradeMaxQuantity = false;
                        }
                        //watch.Stop();
                        //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - GetSettings - Time taken = " + watch.ElapsedMilliseconds);

                        //Determine if we are to do long and or short trades
                        model.doLongs = tb.doLongs;
                        model.doShorts = tb.doShorts;
                        model.doSuppTrades = tb.doSuppTrades;
                        //tb.lastRunVars.doLongsVar = tb.doLongs;
                        //tb.lastRunVars.doShortsVar = tb.doShorts;
                        //tb.lastRunVars.doSuppTradesVar = tb.doSuppTrades;

                        CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
                        CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
                        CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);

                        model.thisModel.inputs = this.tb.runDetails.inputs.DeepCopy();
                        model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        model.thisModel.matchProTrend = false;
                        model.modelVar.counterVar = model.thisModel.counterVar;
                        //model.modelVar = tb.lastRunVars;

                        model.startTime = dtNow;
                        model.modelRunID = modelID;
                        if (model.modelVar.quantity == 0)
                        {
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
                            //liveMode = false;
                        }
                        modelInstanceInputs? thisInput = new modelInstanceInputs();
                        //while (_startTime <= _endTime)
                        //{
                        //bigWatch.Restart();


                        /////////////////////////////////////////////////////////
                        // using the candle time determine which inputs to use //
                        /////////////////////////////////////////////////////////
                        //thisInput = IGModels.clsCommonFunctions.GetInputs(tb.runDetails.inputs, _startTime);

                        // Get the last candle so we can get the spread
                        CommonFunctions.AddStatusMessage($"Checking Spread ", "INFO", logName);
                        //double thisSpread = await Get_SpreadFromLastCandle(the_app_db, minute_container, _endTime);
                        double thisSpread = await Get_SpreadFromLastCandleRSI(the_db, candles_RSI_container, _endTime, resolution, epicName);

                        CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO", logName);
                        thisInput = tb.runDetails.inputs.FirstOrDefault(t => t.spread == thisSpread) ?? null;

                        if (thisInput == null)
                        {
                            CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}, trying spread 0", "INFO", logName);
                            thisInput = tb.runDetails.inputs.FirstOrDefault(t => t.spread == 0);
                        }
                        if (thisInput == null)
                        {
                            CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
                        }
                        else
                        {
                            CommonFunctions.AddStatusMessage("Getting current candle data");
                            //Get the last tick from the list of ticks
                            LOepic? thisEpic = _igContainer.PriceEpicList.Where(x => x.name == epicName).FirstOrDefault();
                            if (thisEpic == null) { throw new InvalidOperationException("Epic not found in PriceEpicList"); }
                            DateTime tickStart = _startTime;
                            DateTime tickeEnd = _startTime.AddMinutes(1).AddMilliseconds(-1);
                            List<tick> ticks = new List<tick>();
                            ticks = thisEpic.ticks.Where(t => t.UTM >= tickStart && t.UTM <= tickeEnd).ToList();

                            tbPrice thisPrice = new tbPrice();

                            dto.endpoint.prices.v2.Price lowPrice = new dto.endpoint.prices.v2.Price();
                            dto.endpoint.prices.v2.Price highPrice = new dto.endpoint.prices.v2.Price();
                            dto.endpoint.prices.v2.Price openPrice = new dto.endpoint.prices.v2.Price();
                            dto.endpoint.prices.v2.Price closePrice = new dto.endpoint.prices.v2.Price();

                            highPrice.bid = ticks.Max(x => x.bid);
                            highPrice.ask = ticks.Max(x => x.offer);
                            lowPrice.bid = ticks.Min(x => x.bid);
                            lowPrice.ask = ticks.Min(x => x.offer);

                            tick? thisOpenTick = ticks.OrderBy(x => x.UTM).FirstOrDefault();
                            if (thisOpenTick != null)
                            {
                                openPrice.bid = thisOpenTick.bid;
                                openPrice.ask = thisOpenTick.offer;
                            }
                            //openPrice.bid = ticks.OrderBy(x => x.UTM).FirstOrDefault().bid;
                            //openPrice.ask = ticks.OrderBy(x => x.UTM).FirstOrDefault().offer;

                            tick? thisCloseTick = ticks.OrderByDescending(x => x.UTM).FirstOrDefault();
                            if (thisCloseTick != null)
                            {
                                closePrice.bid = thisCloseTick.bid;
                                closePrice.ask = thisCloseTick.offer;
                            }
                            //closePrice.bid = ticks.OrderByDescending(x => x.UTM).FirstOrDefault().bid;
                            //closePrice.ask  = ticks.OrderByDescending(x => x.UTM).FirstOrDefault().offer;
                            if (thisPrice.typicalPrice == null) { thisPrice.typicalPrice = new dto.endpoint.prices.v2.Price(); }
                            thisPrice.startDate = tickStart;
                            thisPrice.endDate = tickeEnd;
                            thisPrice.openPrice = openPrice;
                            thisPrice.closePrice = closePrice;
                            thisPrice.highPrice = highPrice;
                            thisPrice.lowPrice = lowPrice;
                            thisPrice.typicalPrice.bid = (highPrice.bid + lowPrice.bid + closePrice.bid) / 3;
                            thisPrice.typicalPrice.ask = (highPrice.ask + lowPrice.ask + closePrice.ask) / 3;

                            List<double> closePrices = new List<double>();
                            closePrices = ticks.Select(x => (double)(x.bid + x.offer) / 2).ToList();
                            StandardDeviation sd = new StandardDeviation(closePrices);
                            thisPrice.stdDev = sd.Value;

                            //StandardDeviation sd = new StandardDeviation((IEnumerable<double>)ticks.Select(x => (x.bid + x.offer) / 2).ToList());
                            //thisPrice.stdDev = sd.Value;

                            modQuote thisCandle = new modQuote
                            {
                                Date = tickStart,
                                Close = (decimal)(thisPrice.closePrice.bid ?? 0 + thisPrice.closePrice.ask ?? 0) / 2,
                                Open = (decimal)(thisPrice.openPrice.bid ?? 0 + thisPrice.openPrice.ask ?? 0) / 2,
                                High = (decimal)(thisPrice.highPrice.bid ?? 0 + thisPrice.highPrice.ask ?? 0) / 2,
                                Low = (decimal)(thisPrice.lowPrice.bid ?? 0 + thisPrice.lowPrice.ask ?? 0) / 2,
                                Typical = (decimal)(thisPrice.typicalPrice.bid ?? 0 + thisPrice.typicalPrice.ask ?? 0) / 2,
                                stdDev = sd.Value
                            };

                            AddStatusMessage($"New Tick :{thisPrice.startDate} - {thisPrice.endDate}");
                            AddStatusMessage($"   Open: {thisPrice.openPrice.bid} / {thisPrice.openPrice.ask} ");
                            AddStatusMessage($"   High: {thisPrice.highPrice.bid} / {thisPrice.highPrice.ask} ");
                            AddStatusMessage($"   Low:  {thisPrice.lowPrice.bid} / {thisPrice.lowPrice.ask} ");
                            AddStatusMessage($"   Close:{thisPrice.closePrice.bid} / {thisPrice.closePrice.ask} ");
                            AddStatusMessage($"   Typical:{thisPrice.typicalPrice.bid} / {thisPrice.typicalPrice.ask} ");

                            foreach (tick item in ticks) thisEpic.ticks.Remove(item);

                            model.quotes = new ModelQuotes();

                            AddStatusMessage("Getting rsi quotes from DB.......");


                            List<modQuote> rsiQuotes = new List<modQuote>();
                            List<modQuote> indCandles = await RSI_LoadPrices.GetPriceDataSMA(the_db, epicName, "MINUTE", 0, _startTime, _endTime, strategy, true, 50);

                            indCandles.Add(thisCandle);

                            AddStatusMessage("Getting Moving Averages quotes from indCandles.......");
                            int intI = 0;
                            //List<modQuote> newCandles = new List<modQuote>();
                            foreach (modQuote mq in indCandles)
                            {
                                if (intI >= 1500)
                                {
                                    int indIndex2 = indCandles.BinarySearch(new modQuote { Date = mq.Date }, new QuoteComparer());
                                    for (int sma = 1; sma <= 100; sma += 1)
                                    {

                                        modQuote thisQuote = new modQuote
                                        {
                                            Date = mq.Date,
                                            High = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.High),
                                            Low = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Low),
                                            Close = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Close),
                                            Open = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Open),
                                            Typical = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Typical),
                                            index = sma
                                        };

                                        mq.smaQuotes.Add(thisQuote);


                                    }

                                    List<modQuote> sma30Quotes = new List<modQuote>();

                                    sma30Quotes = indCandles.Where(s => s.Date <= mq.Date).Where(s => s.Date == mq.Date || s.Date.Minute == 29 || s.Date.Minute == 59).ToList();

                                    for (int sma30 = 1; sma30 <= 50; sma30 += 1)
                                    {
                                        //if (indIndex >= sma30 - 1)
                                        //{
                                        modQuote thisQuote = new modQuote
                                        {
                                            Date = mq.Date,
                                            High = sma30Quotes.TakeLast(sma30).Average(s => s.High),
                                            Low = sma30Quotes.TakeLast(sma30).Average(s => s.Low),
                                            Close = sma30Quotes.TakeLast(sma30).Average(s => s.Close),
                                            Open = sma30Quotes.TakeLast(sma30).Average(s => s.Open),
                                            Typical = sma30Quotes.TakeLast(sma30).Average(s => s.Typical),
                                            index = sma30
                                        };

                                        mq.sma30Quotes.Add(thisQuote);


                                    }
                                    //newCandles.Add(mq);
                                }

                                intI++;
                            }



                            int newVar1 = thisInput.var1 - 1;
                            int newVar2 = thisInput.var2 - 1;
                            int newVar3 = thisInput.var3 - 1;
                            int newVar13 = thisInput.var13 - 1;

                            model.candles.spread = thisSpread;


                            int indIndex = indCandles.BinarySearch(new modQuote { Date = thisPrice.startDate }, new QuoteComparer());
                            int numNewCandles = indCandles.Count;

                            model.candles.currentCandle = new ModelMinuteCandle
                            {
                                epicName = epicName,
                                candleStart = thisPrice.startDate,
                                thisQuote = indCandles[numNewCandles - 1]
                            };
                            model.candles.currentCandle.mATypicalLongTypical = model.candles.currentCandle.thisQuote.smaQuotes[newVar1].Typical;// newCandles.TakeLast( thisInput.var1).Average(s => s.Typical);
                            model.candles.currentCandle.mATypicalShortTypical = model.candles.currentCandle.thisQuote.smaQuotes[newVar2].Typical;// newCandles.TakeLast( thisInput.var2).Average(s => s.Typical);
                            model.candles.currentCandle.mA30MinTypicalLongClose = model.candles.currentCandle.thisQuote.sma30Quotes[newVar3].Close; //sma30Quotes.TakeLast(thisInput.var3).Average(s => s.Close);
                            model.candles.currentCandle.mA30MinTypicalShortClose = model.candles.currentCandle.thisQuote.sma30Quotes[newVar13].Close; //sma30Quotes.TakeLast(thisInput.var13).Average(s => s.Close);
                            model.candles.currentCandle.FirstBid = (decimal)(thisPrice.openPrice.bid ?? 0);
                            model.candles.currentCandle.FirstOffer = (decimal)(thisPrice.openPrice.ask ?? 0);


                            model.candles.prevCandle = new ModelMinuteCandle
                            {
                                candleStart = indCandles[numNewCandles - 2].Date,
                                thisQuote = indCandles[numNewCandles - 2]
                            };
                            model.candles.prevCandle.mATypicalLongTypical = model.candles.prevCandle.thisQuote.smaQuotes[newVar1].Typical;// newCandles.TakeLast( thisInput.var1).Average(s => s.Typical);
                            model.candles.prevCandle.mATypicalShortTypical = model.candles.prevCandle.thisQuote.smaQuotes[newVar2].Typical;// newCandles.TakeLast( thisInput.var2).Average(s => s.Typical);
                            model.candles.prevCandle.mA30MinTypicalLongClose = model.candles.prevCandle.thisQuote.sma30Quotes[newVar3].Close; //sma30Quotes.TakeLast(thisInput.var3).Average(s => s.Close);
                            model.candles.prevCandle.mA30MinTypicalShortClose = model.candles.prevCandle.thisQuote.sma30Quotes[newVar13].Close; //sma30Quotes.TakeLast(thisInput.var13).Average(s => s.Close);

                            model.candles.prevCandle2 = new ModelMinuteCandle
                            {
                                candleStart = indCandles[numNewCandles - 3].Date,
                                thisQuote = indCandles[numNewCandles - 3]
                            };
                            model.candles.prevCandle2.mATypicalLongTypical = model.candles.prevCandle2.thisQuote.smaQuotes[newVar1].Typical;// newCandles.TakeLast( thisInput.var1).Average(s => s.Typical);
                            model.candles.prevCandle2.mATypicalShortTypical = model.candles.prevCandle2.thisQuote.smaQuotes[newVar2].Typical;// newCandles.TakeLast( thisInput.var2).Average(s => s.Typical);
                            model.candles.prevCandle2.mA30MinTypicalLongClose = model.candles.prevCandle2.thisQuote.sma30Quotes[newVar3].Close; //sma30Quotes.TakeLast(thisInput.var3).Average(s => s.Close);
                            model.candles.prevCandle2.mA30MinTypicalShortClose = model.candles.prevCandle2.thisQuote.sma30Quotes[newVar13].Close; //sma30Quotes.TakeLast(thisInput.var13).Average(s => s.Close);

                            DateTime getStartDate = await getPrevMAStartDate(model.candles.currentCandle.candleStart, model.candles.currentCandle.epicName);
                            int candleIndex = indCandles.BinarySearch(new modQuote { Date = getStartDate }, new QuoteComparer());

                            if (candleIndex >= 0)
                            {
                                model.candles.prevMACandle = new ModelMinuteCandle
                                {
                                    candleStart = indCandles[candleIndex].Date,
                                    thisQuote = indCandles[candleIndex]
                                };
                                model.candles.prevMACandle.mATypicalLongTypical = model.candles.prevMACandle.thisQuote.smaQuotes[newVar1].Typical;// newCandles.TakeLast( thisInput.var1).Average(s => s.Typical);
                                model.candles.prevMACandle.mATypicalShortTypical = model.candles.prevMACandle.thisQuote.smaQuotes[newVar2].Typical;// newCandles.TakeLast( thisInput.var2).Average(s => s.Typical);
                                model.candles.prevMACandle.mA30MinTypicalLongClose = model.candles.prevMACandle.thisQuote.sma30Quotes[newVar3].Close; //sma30Quotes.TakeLast(thisInput.var3).Average(s => s.Close);
                                model.candles.prevMACandle.mA30MinTypicalShortClose = model.candles.prevMACandle.thisQuote.sma30Quotes[newVar13].Close; //sma30Quotes.TakeLast(thisInput.var13).Average(s => s.Close);
                            }
                            else
                            {
                                model.candles.prevMACandle = null;
                            }

                            CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);


                            CommonFunctions.AddStatusMessage($"longLTTValue:{model.candles.currentCandle.mA30MinTypicalLongClose} ", "DEBUG");
                            CommonFunctions.AddStatusMessage($"shortLTTValue:{model.candles.currentCandle.mA30MinTypicalShortClose}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"longLTTPrevValue:{model.candles.prevCandle.mA30MinTypicalLongClose}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"shortLTTPrevValue:{model.candles.prevCandle.mA30MinTypicalShortClose}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"shortMAT:{model.candles.currentCandle.mATypicalShortTypical}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"shortPrevMAT:{model.candles.prevCandle.mATypicalShortTypical}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"longMAT:{model.candles.currentCandle.mATypicalLongTypical}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"longPrevMAT:{model.candles.prevCandle.mATypicalLongTypical}", "DEBUG");

                            CommonFunctions.AddStatusMessage($"");

                            CommonFunctions.AddStatusMessage($"prevCandle2.ma30MinTypicalLongClose:{model.candles.prevCandle2.mA30MinTypicalLongClose}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"prevCandle2.mA30MinTypicalShortClose:{model.candles.prevCandle2.mA30MinTypicalShortClose}", "DEBUG");


                            if (model.candles.prevMACandle != null)
                            {
                                CommonFunctions.AddStatusMessage($"prevMACandle.ma30MinTypicalLongClose:{model.candles.prevMACandle.mA30MinTypicalLongClose}", "DEBUG");
                                CommonFunctions.AddStatusMessage($"prevMACandle.mA30MinTypicalShortClose:{model.candles.prevMACandle.mA30MinTypicalShortClose}", "DEBUG");

                            }
                            else
                            {
                                CommonFunctions.AddStatusMessage($"prevMaCandle is null. getStartDate = {getStartDate}");
                            }
                            //model.RunProTrendCodeV2(model.candles);
                            //model.RunProTrendCodeV3(model.candles);
                            model.RunProTrendCodeV5(model.candles);



                            CommonFunctions.AddStatusMessage($"values after  run         - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);
                            //clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");
                            CommonFunctions.AddStatusMessage($"Current standard deviation - {model.candles.currentCandle.thisQuote.stdDev}", "DEBUG", logName);

                            CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
                            CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
                            //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG");
                            //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG");


                            if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
                            if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

                            //model.sellShort = true;

                            //string thisDealRef = "";
                            //string dealType = "";
                            //bool dealSent = false;

                            double targetVar = thisInput.targetVarInput / 100 + 1;
                            double targetVarShort = thisInput.targetVarInputShort / 100 + 1;

                            //////////////////////////////////////////////////////////////////////////////////////////////
                            // Check for changes to stop limit that would mean the current trade has to end immediately //
                            //////////////////////////////////////////////////////////////////////////////////////////////

                            double currentStop = 0;
                            double newStop = 0;
                            double currentPrice = 0;

                            if (model.longOnmarket && model.modelVar.breakEvenVar == 0 && currentTrade != null && model.thisModel.currentTrade != null)
                            {
                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)currentTrade.stopLevel);
                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
                                CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


                                if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
                                {
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
                                    model.sellLong = true;
                                }

                            }
                            if (model.shortOnMarket && model.modelVar.breakEvenVar == 0 && currentTrade != null && model.thisModel.currentTrade != null)
                            {
                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue));
                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.candles.currentCandle.candleData.Close);

                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
                                CommonFunctions.AddStatusMessage($"[LONG] Current stop > newStop = {currentStop > newStop},  currentPrice > newStop = {currentPrice > newStop}, currentPrice < currentStop {currentPrice < currentStop}  ", "DEBUG", logName);


                                if (currentStop > newStop && currentPrice > newStop && currentPrice < currentStop)
                                {
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "buying short because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now higher than the new stop.", the_app_db);
                                    model.buyShort = true;
                                }
                            }


                            // Check if target price has changed when an attached order is set but the supp trade hasn't started yet. //

                            //if (model.onMarket && this.currentTrade != null && model.onSuppTrade == false && model.thisModel.currentTrade.attachedOrder != null)
                            //{
                            //    OrderValues orderValues = new OrderValues();
                            //    string direction = "";
                            //    if (model.longOnmarket)
                            //    {
                            //        direction = "buy";
                            //    }
                            //    if (model.shortOnMarket)
                            //    {
                            //        direction = "sell";
                            //    }
                            //    orderValues.SetOrderValues(direction, this);
                            //    clsCommonFunctions.AddStatusMessage($"Checking order level {orderValues.level} = attached order level {model.thisModel.currentTrade.attachedOrder.orderLevel}", "DEBUG", logName);
                            //    if (orderValues.level != model.thisModel.currentTrade.attachedOrder.orderLevel)
                            //    {
                            //        // get stuff done :- specifically get the order values 


                            //        if (orderValues.stopDistance > 0 && orderValues.level > 0)
                            //        {
                            //            EditOrder(orderValues.level, orderValues.stopDistance, model.thisModel.currentTrade.attachedOrder.dealId);
                            //        }


                            //    }
                            //}


                            //////////////////////////////////////////////////////////////////

                            // Check first that the stop loss is more than 4x the current std dev

                            double stdDev = model.candles.currentCandle.thisQuote.stdDev;
                            double sL = 0;
                            double stdDevSL = 0;
                            if (model.buyLong)
                            {
                                sL = (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
                                stdDevSL = sL / stdDev;
                            }
                            else
                            {
                                if (model.sellShort)
                                {
                                    sL = (double)thisInput.var5 * Math.Abs((double)targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
                                    stdDevSL = sL / stdDev;
                                }
                            }
                            //bool stdDevOK = true;
                            if (model.buyLong || model.sellShort)
                            {
                                if (stdDevSL < 4)
                                {
                                    //stdDevOK = false;
                                    CommonFunctions.AddStatusMessage($"Stop loss ({sL}) is not more than 4x the current std dev ({stdDev}) - stdDevSL = {stdDevSL}", "ERROR", logName);
                                    model.buyLong = false;
                                    model.sellShort = false;
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage($"Stop loss ({sL}) is more than 4x the current std dev ({stdDev}) - stdDevSL = {stdDevSL}", "INFO", logName);
                                }
                            }


                            if (model.buyLong && this.currentTrade == null)
                            {
                                CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
                                model.stopLossVar = (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
                                requestedTrade reqTrade = new requestedTrade
                                {
                                    dealType = "POSITION",
                                    dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId)
                                };
                                requestedTrades.Add(reqTrade);
                                //if (reqTrade.dealReference != "")
                                //{
                                //    dealSent = true;
                                //    thisDealRef = reqTrade.dealReference;
                                //    dealType = "PlaceDeal";
                                //}

                            }
                            else
                            {
                                if (model.sellLong && this.currentTrade != null)
                                {
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
                                    string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
                                }
                            }

                            if (model.sellShort && this.currentTrade == null)
                            {
                                CommonFunctions.AddStatusMessage("SellShort activated", "INFO", logName);
                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);
                                model.stopLossVar = (double)thisInput.var5 * Math.Abs((double)targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
                                requestedTrade reqTrade = new requestedTrade
                                {
                                    dealType = "POSITION",
                                    dealReference = await PlaceDeal("short", model.modelVar.quantity, model.stopLossVar, this.igAccountId)
                                };
                                requestedTrades.Add(reqTrade);
                                //if (reqTrade.dealReference != "")
                                //{
                                //    dealSent = true;
                                //    thisDealRef = reqTrade.dealReference;
                                //    dealType = "PlaceDeal";
                                //}
                            }
                            else
                            {
                                if (model.buyShort && currentTrade != null)
                                {
                                    CommonFunctions.AddStatusMessage("BuyShort activated", "INFO", logName);
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort)", the_app_db);
                                    string dealRef = await CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
                                }
                            }


                            if (model.longOnmarket && currentTrade != null && model.thisModel.currentTrade != null)
                            {
                                CommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

                                if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                {



                                    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                    decimal? currentStopLevel = this.currentTrade.stopLevel;
                                    if (model.modelVar.breakEvenVar == 1)
                                    {


                                        this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditLong activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal - BREAKEVEN", the_app_db);
                                        string dealRef = await EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);

                                    }
                                    else

                                    {
                                        this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
                                        string dealRef = await EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);

                                    }

                                }
                            }
                            if (model.shortOnMarket && currentTrade != null && model.thisModel.currentTrade != null)
                            {
                                CommonFunctions.AddStatusMessage($"[SHORT] Check if sellPrice ({model.thisModel.currentTrade.sellPrice}) + stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

                                if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                {
                                    CommonFunctions.AddStatusMessage("EditShort activated", "INFO", logName);
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal", the_app_db);


                                    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                    decimal? currentStopLevel = this.currentTrade.stopLevel;

                                    if (model.modelVar.breakEvenVar == 1)
                                    {

                                        this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditShort activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal - BREAKEVEN", the_app_db);
                                        string dealRef = await EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
                                        //if (dealRef != "")
                                        //{
                                        //    dealSent = true;
                                        //    thisDealRef = dealRef;
                                        //    dealType = "PlaceDeal";
                                        //}
                                        //If on a supp trade then set that trades sl to be the same as the current trade
                                        if (model.onSuppTrade && this.suppTrade != null)
                                        {
                                            CommonFunctions.AddStatusMessage($"EditShort SUPP activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short SUPP Deal - BREAKEVEN", the_app_db);
                                            string dealRefSupp = await EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
                                        }
                                    }
                                    else
                                    {

                                        this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.sellPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
                                        string dealRef = await EditDeal((double)model.thisModel.currentTrade.sellPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);


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
                                    }
                                    ;
                                }
                                if (model.thisModel.suppTrade != null)
                                {
                                    if (model.thisModel.suppTrade.targetPrice != 0)
                                    {
                                        await model.thisModel.suppTrade.SaveDocument(this.trade_container);
                                    }
                                    ;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log log = new Log(the_app_db)
                                {
                                    Log_Message = ex.ToString(),
                                    Log_Type = "Error",
                                    Log_App = "RunCode"
                                };
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

                            if (model.modelLogs.logs.Count() > 0)
                            {
                                ModelLog log = new ModelLog();
                                log = model.modelLogs.logs[0];
                                log.modelRunID = modelID;
                                log.runDate = _startTime;
                                log.id = System.Guid.NewGuid().ToString();
                                if (model.onMarket && model.thisModel.currentTrade != null)
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
                                currentStatus.doShorts = model.doShorts;
                                currentStatus.doLongs = model.doLongs;
                                //if(strategy == "RSI")
                                //{
                                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
                                //}
                                //currentStatus.epicName = this.epicName;
                                //send log to the website
                                model.modelLogs.logs[0].epicName = this.epicName;
                                if (the_app_db != null)
                                {
                                    Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0])));
                                    Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                                    //save log to the database
                                    Container logContainer = the_app_db.GetContainer("ModelLogs");
                                    await log.SaveDocument(logContainer);
                                    model.modelLogs.logs = new List<ModelLog>();
                                }
                            }
                        }
                        model.candles.prevCandle2 = model.candles.prevCandle.DeepCopy();
                        model.candles.prevCandle = model.candles.currentCandle.DeepCopy();
                        _startTime = _startTime.AddMinutes(1);
                        //bigWatch.Stop();
                        //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);






                        //bigWatch.Stop();
                        //clsCommonFunctions.AddStatusMessage( "Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
                        CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);
                        // call the accounts api each hour just so we ensure the tokens don't expire
                        //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
                    }
                    else
                    {
                        CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
                    }
                }
                else
                {
                    CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
                    pausedAfterNGL = false;
                }

                if (latestHour != DateTime.UtcNow.Hour)
                {
                    //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
                    //try
                    //{
                    //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
                    //    if (ret != null)
                    //    {
                    //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
                    //    }
                    latestHour = DateTime.UtcNow.Hour;
                    //}
                    //catch (Exception ex)
                    //{
                    //    Log log = new Log(the_app_db);
                    //    log.Log_Message = ex.ToString();
                    //    log.Log_Type = "Error";
                    //    log.Log_App = "RunCode";
                    //    await log.Save();
                    //}

                }


            }
            catch (Exception ex)
            {
                AddStatusMessage($"Error - {ex.ToString()}", "ERROR");
                Log log = new Log(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "RunCode"
                };
                await log.Save();
            }

            return taskRet;
        }

        public async Task<RunRet> RunCode_GRID(object sender, System.Timers.ElapsedEventArgs e)
        {
            RunRet taskRet = new RunRet();
            this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
            ScopeContext.PushProperty("app", "TRADINGBRAIN/");
            ScopeContext.PushProperty("epic", this.epicName + "/");
            ScopeContext.PushProperty("strategy", strategy + "/");
            ScopeContext.PushProperty("resolution", resolution + "/");
            //bool liveMode = true;
            bool marketOpen = false;

            DateTime dtNow = DateTime.UtcNow;
            DateTime _startTime;
            resolution = "SECOND";

            //int numCandlesToStart = 20;

            // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
            //int seconds = dtNow.Second;
            //if (seconds < 59)
            //{
            _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, dtNow.Second, 0);
            //}
            //else
            //{
            //    _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0);
            //}


            DateTime _endTime = _startTime;

            // Check to see if we have a list of candles to work with, if not, go get some

            if (model == null) { throw new InvalidOperationException("Model is null in RunCode_GRID"); }
            if (_igContainer == null || _igContainer.tbClient == null) { throw new InvalidOperationException("IG Container is null in RunCode_GRID"); }
            if (_igContainer2 == null || _igContainer2.tbClient == null) { throw new InvalidOperationException("IG Container2 is null in RunCode_GRID"); }
            if (modelVar == null) { throw new InvalidOperationException("ModelVars is null in RunCode_GRID"); }
            if (currentStatus == null) { throw new InvalidOperationException("CurrentStatus is null in RunCode_GRID"); }

            if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
            {
                // Check if the market is currently open. If it is not then skip till next time.
                marketOpen = await IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName);
                if (marketOpen)
                {
                    // All candles loaded so lets crack on.
                    _igContainer.tbClient.FirstConfirmUpdate = false;
                    _igContainer2.tbClient.FirstConfirmUpdate = false;
                    //string param = "";


                    CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
                    CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
                    CommonFunctions.AddStatusMessage($" - Epic       - {epicName}", "INFO", logName);
                    CommonFunctions.AddStatusMessage($" - Strategy   - {strategy}", "INFO", logName);
                    CommonFunctions.AddStatusMessage($" - Resolution - {resolution}", "INFO", logName);
                    CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
                    CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);

                    try
                    {
                        this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

                        if (this.tb == null || this.tb.runDetails == null || this.tb.runDetails.inputs == null || this.tb.lastRunVars == null)
                        {
                            throw new InvalidOperationException("Trading Brain settings not found");
                        }

                        CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

                        // If the trade has just been deleted then sort out the CFL
                        if (lastTradeDeleted)
                        {
                            try
                            {
                                double nettPosition = lastTradeValue + lastTradeSuppValue;
                                CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
                            }

                            catch (Exception ex)
                            {
                                CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
                            }

                            lastTradeDeleted = false;
                            lastTradeValue = 0;
                            lastTradeSuppValue = 0;
                            lastTradeMaxQuantity = false;
                        }


                        //Determine if we are to do long and or short trades
                        model.doLongs = tb.doLongs;
                        model.doShorts = tb.doShorts;
                        model.doSuppTrades = tb.doSuppTrades;
                        // clsCommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
                        // clsCommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
                        // clsCommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
                        model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
                        model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        model.thisModel.matchProTrend = false;
                        model.modelVar.counterVar = model.thisModel.counterVar;
                        model.startTime = dtNow;
                        model.modelRunID = modelID;
                        model.modelVar.startingQuantity = tb.lastRunVars.startingQuantity;
                        model.modelVar.baseQuantity = tb.lastRunVars.baseQuantity;
                        model.modelVar.quantity = tb.lastRunVars.quantity;
                        model.modelVar.minQuantity = tb.lastRunVars.minQuantity;

                        //if (model.modelVar.quantity == 0)
                        //{
                        //    model.modelVar.minQuantity = tb.lastRunVars.quantity;
                        //    model.modelVar.quantity = tb.lastRunVars.quantity;
                        //}

                        currentStatus.inputs = tb.runDetails.inputs;
                        currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        currentStatus.quantity = tb.lastRunVars.minQuantity;

                        modelInstanceInputs_RSI? thisInput = new modelInstanceInputs_RSI();

                        thisInput = tb.runDetails.inputs_RSI.FirstOrDefault(t => t.spread == 0);
                        if (thisInput == null)
                        {
                            CommonFunctions.AddStatusMessage($"No inputs found for spread = 0", "ERROR", logName);
                        }

                        else
                        {
                            CommonFunctions.AddStatusMessage("Getting current candle data");

                            bool gotCandle = false;

                            //Get the last tick from the list of ticks
                            LOepic? thisEpic = _igContainer.PriceEpicList.Where(x => x.name == epicName).FirstOrDefault();
                            if (thisEpic == null) { throw new InvalidOperationException("Epic not found in PriceEpicList"); }

                            DateTime tickStart = _startTime.AddSeconds(-1);
                            DateTime tickeEnd = _startTime.AddSeconds(1).AddMilliseconds(-1);
                            List<tick> ticks = new List<tick>();
                            modQuote thisCandle = new modQuote();
                            ticks = thisEpic.ticks.Where(t => t.UTM >= tickStart && t.UTM <= tickeEnd).ToList();
                            //clsCommonFunctions.AddStatusMessage($"Ticks - {thisEpic.ticks.Count}");
                            //clsCommonFunctions.AddStatusMessage($"Ticks in period {tickStart} to {tickeEnd} - Last Tick = {thisEpic.ticks.Last().UTM}");
                            if (ticks != null && ticks.Count > 0)
                            {
                                tbPrice thisPrice = new tbPrice();

                                dto.endpoint.prices.v2.Price lowPrice = new dto.endpoint.prices.v2.Price();
                                dto.endpoint.prices.v2.Price highPrice = new dto.endpoint.prices.v2.Price();
                                dto.endpoint.prices.v2.Price openPrice = new dto.endpoint.prices.v2.Price();
                                dto.endpoint.prices.v2.Price closePrice = new dto.endpoint.prices.v2.Price();

                                highPrice.bid = ticks.Max(x => x.bid);
                                highPrice.ask = ticks.Max(x => x.offer);
                                lowPrice.bid = ticks.Min(x => x.bid);
                                lowPrice.ask = ticks.Min(x => x.offer);

                                tick? thisOpenTick = ticks.OrderBy(x => x.UTM).FirstOrDefault();
                                if (thisOpenTick != null)
                                {
                                    openPrice.bid = thisOpenTick.bid;
                                    openPrice.ask = thisOpenTick.offer;
                                }

                                tick? thisCloseTick = ticks.OrderByDescending(x => x.UTM).FirstOrDefault();
                                if (thisCloseTick != null)
                                {
                                    closePrice.bid = thisCloseTick.bid;
                                    closePrice.ask = thisCloseTick.offer;
                                }
                                if (thisPrice.typicalPrice == null) { thisPrice.typicalPrice = new dto.endpoint.prices.v2.Price(); }
                                thisPrice.startDate = tickStart;
                                thisPrice.endDate = tickeEnd;
                                thisPrice.openPrice = openPrice;
                                thisPrice.closePrice = closePrice;
                                thisPrice.highPrice = highPrice;
                                thisPrice.lowPrice = lowPrice;
                                thisPrice.typicalPrice.bid = (highPrice.bid + lowPrice.bid + closePrice.bid) / 3;
                                thisPrice.typicalPrice.ask = (highPrice.ask + lowPrice.ask + closePrice.ask) / 3;

                                List<double> closePrices = new List<double>();
                                closePrices = ticks.Select(x => (double)(x.bid + x.offer) / 2).ToList();
                                StandardDeviation sd = new StandardDeviation(closePrices);
                                thisPrice.stdDev = sd.Value;


                                thisCandle.Date = tickStart;
                                thisCandle.Close = (decimal)(thisPrice.closePrice.bid ?? 0 + thisPrice.closePrice.ask ?? 0) / 2;
                                thisCandle.Open = (decimal)(thisPrice.openPrice.bid ?? 0 + thisPrice.openPrice.ask ?? 0) / 2;
                                thisCandle.High = (decimal)(thisPrice.highPrice.bid ?? 0 + thisPrice.highPrice.ask ?? 0) / 2;
                                thisCandle.Low = (decimal)(thisPrice.lowPrice.bid ?? 0 + thisPrice.lowPrice.ask ?? 0) / 2;
                                thisCandle.Typical = (decimal)(thisPrice.typicalPrice.bid ?? 0 + thisPrice.typicalPrice.ask ?? 0) / 2;
                                thisCandle.stdDev = sd.Value;
                                thisCandle.spread = (double)(thisPrice.closePrice.ask ?? 0 - thisPrice.closePrice.bid ?? 0);
                                //this.lastCandle = thisCandle.DeepCopy();

                                //Remove the first candle and add this one to ensure we keep a rolling set of candles
                                this.candleList.RemoveAt(0);
                                this.candleList.Add(thisCandle.DeepCopy());


                                AddStatusMessage($"New Tick :{thisPrice.startDate} - {thisPrice.endDate}: Typical:{thisPrice.typicalPrice.bid} / {thisPrice.typicalPrice.ask} ");
                                //AddStatusMessage($"   Open: {thisPrice.openPrice.bid} / {thisPrice.openPrice.ask} ");
                                //AddStatusMessage($"   High: {thisPrice.highPrice.bid} / {thisPrice.highPrice.ask} ");
                                //AddStatusMessage($"   Low:  {thisPrice.lowPrice.bid} / {thisPrice.lowPrice.ask} ");
                                //AddStatusMessage($"   Close:{thisPrice.closePrice.bid} / {thisPrice.closePrice.ask} ");
                                //AddStatusMessage($"   Typical:{thisPrice.typicalPrice.bid} / {thisPrice.typicalPrice.ask} ");

                                //this.candleList.Add(thisCandle.DeepCopy());
                                gotCandle = true;
                                foreach (tick item in ticks) thisEpic.ticks.Remove(item);
                            }

                            if (gotCandle && model.candles.currentCandle != null)
                            {
                                //this.candleList.Add(thisCandle.DeepCopy());

                                AddStatusMessage($"Candle Data : {thisCandle.Date} - {thisCandle.Open}");
                                model.quotes = new ModelQuotes();

                                //if (this.candleList == null || this.candleList.Count < numCandlesToStart)
                                //{
                                //    clsCommonFunctions.AddStatusMessage($"Candle list at {this.candleList.Count}, waiting for ({numCandlesToStart - this.candleList.Count} more candles...", "INFO", logName);
                                //}
                                //else
                                //{

                                //Remove the first item in the list to maintain a rolling set of candles
                                //if (this.candleList.Count > numCandlesToStart * 5)
                                //{
                                //    this.candleList.RemoveAt(0);
                                //}

                                //clsCommonFunctions.AddStatusMessage($"Candle list at {this.candleList.Count}");
                                //AddStatusMessage("Getting rsi quotes from DB.......");


                                //thisCandle.atr= this.candleList.GetAtr((int)thisInput.var3).LastOrDefault().Atr ?? 0;
                                //clsCommonFunctions.AddStatusMessage($"ATR= {thisCandle.atr}");

                                model.candles.currentGRIDCandle = thisCandle;
                                CommonFunctions.AddStatusMessage($"Current close price = {thisCandle.Close}", "DEBUG", logName);
                                CommonFunctions.AddStatusMessage($"Current closeAttemptCount = {closeAttemptCount}", "DEBUG", logName);

                                model.candles.currentCandle.grid_long_sma = Convert.ToDouble(candleList.TakeLast((int)thisInput.var4).Average(s => s.Close));
                                model.candles.currentCandle.grid_short_sma = Convert.ToDouble(candleList.TakeLast((int)thisInput.svar4).Average(s => s.Close));

                                model.candles.currentCandle.grid_prev_long_sma = Convert.ToDouble(candleList.SkipLast(1).TakeLast((int)thisInput.var4).Average(s => s.Close));
                                model.candles.currentCandle.grid_prev_short_sma = Convert.ToDouble(candleList.SkipLast(1).TakeLast((int)thisInput.svar4).Average(s => s.Close));

                                CommonFunctions.AddStatusMessage($"Current : SMA Long = {model.candles.currentCandle.grid_long_sma}, Short = {model.candles.currentCandle.grid_short_sma}", "DEBUG", logName);
                                CommonFunctions.AddStatusMessage($"Prev : SMA Long = {model.candles.currentCandle.grid_prev_long_sma}, Short = {model.candles.currentCandle.grid_prev_short_sma}", "DEBUG", logName);

                                if (closeAttemptCount == 0)
                                {


                                    if (model.thisModel.gridLTrades.Count > 0)
                                    {
                                        CommonFunctions.AddStatusMessage($"Long bollid = {this.gridLID}", "DEBUG");
                                        CommonFunctions.AddStatusMessage($"Long quantites = {model.thisModel.gridLTrades.Sum(x => x.quantity)}, position price = {model.thisModel.gridLTrades.Average(x => x.buyPrice)}, last price = {model.thisModel.gridLTrades.Last().buyPrice} ", "DEBUG", logName);

                                    }

                                    if (model.thisModel.gridSTrades.Count > 0)
                                    {
                                        CommonFunctions.AddStatusMessage($"Short bollid = {this.gridSID}", "DEBUG");
                                        CommonFunctions.AddStatusMessage($"Short quantites = {model.thisModel.gridSTrades.Sum(x => x.quantity)}, position price = {model.thisModel.gridSTrades.Average(x => x.sellPrice)}, last price = {model.thisModel.gridSTrades.Last().sellPrice} ", "DEBUG", logName);

                                    }

                                    CommonFunctions.AddStatusMessage($"Spread = {thisCandle.spread}, Short GridSize = {thisInput.svar0}, Long GridSize = {thisInput.var0}", "DEBUG", logName);


                                    model.RunProTrendCodeGRID(model.candles);



                                    //if (model.thisModel.gridLTrades.Count < 100 && dtNow.Second % 2 == 0)
                                    //{
                                    //    model.modelVar.quantity = 2.5;
                                    //    model.buyLong = true;
                                    //}
                                    //else
                                    //{
                                    //    if (model.thisModel.gridLTrades.Count >= 100 && dtNow.Second % 2 == 0)
                                    //    {
                                    //        model.sellLong = true;
                                    //    }
                                    //    else
                                    //    {
                                    //        model.sellLong = false;
                                    //        model.buyLong = false;
                                    //    }
                                    //}
                                    CommonFunctions.AddStatusMessage($"values after  run  - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);


                                    //string thisDealRef = "";
                                    //string dealType = "";
                                    //bool dealSent = false;

                                    if (this.currentGRIDLTrade != null) { CommonFunctions.AddStatusMessage($"Current Long dealid = {this.currentGRIDLTrade.dealId}", "INFO", logName); }
                                    if (this.currentGRIDSTrade != null) { CommonFunctions.AddStatusMessage($"Current Short dealid = {this.currentGRIDSTrade.dealId}", "INFO", logName); }

                                    List<tradeItem> openLTrades = model.thisModel.gridLTrades.DeepCopy();
                                    List<tradeItem> openSTrades = model.thisModel.gridSTrades.DeepCopy();

                                    bool sellingLongs = false;
                                    bool buyingShorts = false;

                                    if (model.sellLong)
                                    {
                                        sellingLongs = true;
                                    }
                                    if (model.sellShort)
                                    {
                                        buyingShorts = true;
                                    }

                                    //double currentPrice = 0;
                                    if (openLTrades.Count > 0 && sellingLongs == false)
                                    {
                                        CommonFunctions.AddStatusMessage($"GRID Long Trades - Num Trades : {openLTrades.Count}, Sum quantity : {openLTrades.Sum(x => x.quantity)}");

                                        //foreach (tradeItem ti in openLTrades)
                                        //{
                                        //    clsCommonFunctions.AddStatusMessage($"Trade id : {ti.tbDealId}, started: {ti.tradeStarted}, BuyPrice: {ti.buyPrice}, Quantity: {ti.quantity}");

                                        //}

                                    }

                                    if (openSTrades.Count > 0 && buyingShorts == false)
                                    {
                                        CommonFunctions.AddStatusMessage($"GRID Short Trades - Num Trades : {openSTrades.Count}, Sum quantity : {openSTrades.Sum(x => x.quantity)}");

                                        //foreach (tradeItem ti in openSTrades)
                                        //{
                                        //    clsCommonFunctions.AddStatusMessage($"Trade id : {ti.tbDealId}, started: {ti.tradeStarted}, SellPrice: {ti.sellPrice}, Quantity: {ti.quantity}");

                                        //}

                                    }

                                    if (model.buyLong)
                                    {
                                        CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);

                                        requestedTrade reqTrade = new requestedTrade
                                        {
                                            dealType = "POSITION",
                                            dealReference = await PlaceDeal("long", model.modelVar.quantity, 0, this._igContainer.creds.igAccountId)
                                        };
                                        requestedTrades.Add(reqTrade);
                                        //if (reqTrade.dealReference != "")
                                        //{
                                        //    //dealSent = true;
                                        //    thisDealRef = reqTrade.dealReference;
                                        //    //dealType = "PlaceDeal";
                                        //}

                                    }
                                    else
                                    {
                                        if (model.sellLong)
                                        {
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
                                            CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
                                            closeAttemptCount = 1;
                                            string dealRef = await CloseDealEpic("long", (double)openLTrades.Sum(x => x.quantity), this.epicName, this._igContainer.creds.igAccountId);
                                        }
                                    }



                                    if (model.sellShort)
                                    {
                                        CommonFunctions.AddStatusMessage("SellShort activated", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);

                                        requestedTrade reqTrade = new requestedTrade
                                        {
                                            dealType = "POSITION",
                                            dealReference = await PlaceDeal("short", model.modelVar.quantity, 0, this._igContainer2.creds.igAccountId)
                                        };
                                        requestedTrades.Add(reqTrade);
                                        //if (reqTrade.dealReference != "")
                                        //{
                                        //    //dealSent = true;
                                        //    thisDealRef = reqTrade.dealReference;
                                        //    //dealType = "PlaceDeal";
                                        //}

                                    }
                                    else
                                    {
                                        if (model.buyShort)
                                        {
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort", the_app_db);
                                            CommonFunctions.AddStatusMessage("BuyShort activated", "INFO", logName);
                                            closeAttemptCount = 1;
                                            string dealRef = await CloseDealEpic("short", (double)openSTrades.Sum(x => x.quantity), this.epicName, this._igContainer2.creds.igAccountId);
                                        }
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

                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage($"Waiting for close to finish. Attempt {closeAttemptCount} of {MAX_WAIT_FOR_CLOSE_TIME}", "INFO", logName);
                                    //if (model.thisModel.closingGridLTrade)
                                    //{
                                    //    clsCommonFunctions.AddStatusMessage("Currently closing long trades, no new trades will be initiated", "INFO", logName);
                                    //}
                                    //else
                                    //{
                                    //    if (model.thisModel.closingGridSTrade)
                                    //    {
                                    //        clsCommonFunctions.AddStatusMessage("Currently closing short trades, no new trades will be initiated", "INFO", logName);
                                    //    }
                                    //}
                                    closeAttemptCount += 1;
                                    if (closeAttemptCount == MAX_WAIT_FOR_CLOSE_TIME)
                                    {
                                        CommonFunctions.AddStatusMessage("Max wait time for close reached, resetting close flags", "DEBUG", logName);
                                        model.thisModel.closedGridLTrades.Clear();
                                        model.thisModel.closedGridSTrades.Clear();
                                        closeAttemptCount = 0;
                                    }

                                    //If there are still some trades after 45 seconds, try again
                                    if (closeAttemptCount >= 40 && model.thisModel.closedGridLTrades.Count > 0)
                                    {
                                        CommonFunctions.AddStatusMessage($"still have {model.thisModel.gridLTrades.Count} trades to close.....trying again");
                                        foreach (tradeItem trade in model.thisModel.gridLTrades)
                                        {
                                            CommonFunctions.AddStatusMessage($"trade {trade.tbDealId} not closed yet", "DEBUG");
                                        }
                                        string dealRef = await CloseDealEpic("long", (double)model.thisModel.gridLTrades.Sum(x => x.quantity), this.epicName, this._igContainer.creds.igAccountId);
                                        closeAttemptCount = 1;
                                    }
                                    if (closeAttemptCount >= 40 && model.thisModel.closedGridSTrades.Count > 0)
                                    {
                                        string dealRef = await CloseDealEpic("short", (double)model.thisModel.gridSTrades.Sum(x => x.quantity), this.epicName, this._igContainer2.creds.igAccountId);
                                    }
                                }

                                if (model.modelLogs.logs.Count > 0)
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
                                        //currentStatus.target = model.thisModel.currentGRIDLTrade.targetPrice;
                                        //currentStatus.count = model.thisModel.currentGRIDLTrade.count;

                                    }
                                    else
                                    {
                                        currentStatus.onMarket = false;
                                        currentStatus.tradeType = "";
                                    }

                                    //if (model.candles.currentCandle.bolli_avg > model.candles.currentCandle.bolli_avgPrev && model.onMarket)
                                    //{
                                    //    currentStatus.lTT = 1;
                                    //}
                                    //else
                                    //{
                                    //    currentStatus.lTT = 0;
                                    //}

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
                                    currentStatus.doShorts = model.doShorts;
                                    currentStatus.doLongs = model.doLongs;

                                    //send log to the website
                                    model.modelLogs.logs[0].epicName = this.epicName;
                                    if (the_app_db != null)
                                    {
                                        Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0])));
                                        Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                                        //save log to the database
                                        Container logContainer = the_app_db.GetContainer("ModelLogs");
                                        await log.SaveDocument(logContainer);
                                        model.modelLogs.logs = new List<ModelLog>();
                                    }
                                }
                                //}
                            }
                            else
                            {
                                CommonFunctions.AddStatusMessage($"No candle formed for second starting at {_startTime} - current candle list at {this.candleList.Count}", "DEBUG", logName);
                            }

                            _startTime = _startTime.AddSeconds(1);


                        }

                    }
                    catch (Exception ex)
                    {
                        AddStatusMessage($"Error - {ex.ToString()}", "ERROR");
                        Log log = new Log(the_app_db)
                        {
                            Log_Message = ex.ToString(),
                            Log_Type = "Error",
                            Log_App = "RunCode"
                        };
                        await log.Save();
                    }

                    CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);


                }
                else
                {
                    CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
                }
            }
            else
            {
                CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
                pausedAfterNGL = false;
            }




            return taskRet;
        }
        //public async Task<RunRet> RunCode_BOLLI(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    //bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;
        //    resolution = "MINUTE";

        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    if (seconds < 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0);
        //    }


        //    DateTime _endTime = _startTime;


        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.
        //        //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
        //        marketOpen = await IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName);
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($" - Epic       - {epicName}", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($" - Strategy   - {strategy}", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($" - Resolution - {resolution}", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ----------------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);

        //            try
        //            {
        //                //watch.Start();


        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);
        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL
        //                //if (lastTradeDeleted)
        //                //{
        //                //    try
        //                //    {
        //                //        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                //        clsCommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                //    }

        //                //    catch (Exception ex)
        //                //    {
        //                //        clsCommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                //    }

        //                //    lastTradeDeleted = false;
        //                //    lastTradeValue = 0;
        //                //    lastTradeSuppValue = 0;
        //                //    lastTradeMaxQuantity = false;
        //                //}

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                //model.doShorts = tb.doShorts;
        //                //model.doSuppTrades = tb.doSuppTrades;

        //                //clsCommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                //clsCommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                //clsCommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                //model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                //model.modelVar.counterVar = model.thisModel.counterVar;
        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI;
        //                //currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////

        //                // Get the last candle so we can get the spread
        //                CommonFunctions.AddStatusMessage($"Checking Spread ", "INFO", logName);
        //                //double thisSpread = await Get_SpreadFromLastCandle(the_app_db, minute_container, _endTime);
        //                double thisSpread = await Get_SpreadFromLastCandleRSI(the_db, candles_RSI_container, _endTime, resolution, epicName);

        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO", logName);
        //                thisInput = tb.runDetails.inputs_RSI.FirstOrDefault(t => t.spread == thisSpread);

        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}, trying spread 0", "INFO", logName);
        //                    thisInput = tb.runDetails.inputs_RSI.FirstOrDefault(t => t.spread == 0);
        //                }
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    CommonFunctions.AddStatusMessage("Getting current candle data");
        //                    //Get the last tick from the list of ticks
        //                    LOepic thisEpic = _igContainer.PriceEpicList.Where(x => x.name == epicName).FirstOrDefault();
        //                    DateTime tickStart = _startTime;
        //                    DateTime tickeEnd = _startTime.AddMinutes(1).AddMilliseconds(-1);
        //                    List<tick> ticks = new List<tick>();
        //                    ticks = thisEpic.ticks.Where(t => t.UTM >= tickStart && t.UTM <= tickeEnd).ToList();

        //                    tbPrice thisPrice = new tbPrice();

        //                    dto.endpoint.prices.v2.Price lowPrice = new dto.endpoint.prices.v2.Price();
        //                    dto.endpoint.prices.v2.Price highPrice = new dto.endpoint.prices.v2.Price();
        //                    dto.endpoint.prices.v2.Price openPrice = new dto.endpoint.prices.v2.Price();
        //                    dto.endpoint.prices.v2.Price closePrice = new dto.endpoint.prices.v2.Price();

        //                    highPrice.bid = ticks.Max(x => x.bid);
        //                    highPrice.ask = ticks.Max(x => x.offer);
        //                    lowPrice.bid = ticks.Min(x => x.bid);
        //                    lowPrice.ask = ticks.Min(x => x.offer);
        //                    openPrice.bid = ticks.OrderBy(x => x.UTM).FirstOrDefault().bid;
        //                    openPrice.ask = ticks.OrderBy(x => x.UTM).FirstOrDefault().offer;
        //                    closePrice.bid = ticks.OrderByDescending(x => x.UTM).FirstOrDefault().bid;
        //                    closePrice.ask = ticks.OrderByDescending(x => x.UTM).FirstOrDefault().offer;

        //                    thisPrice.startDate = tickStart;
        //                    thisPrice.endDate = tickeEnd;
        //                    thisPrice.openPrice = openPrice;
        //                    thisPrice.closePrice = closePrice;
        //                    thisPrice.highPrice = highPrice;
        //                    thisPrice.lowPrice = lowPrice;
        //                    thisPrice.typicalPrice.bid = (highPrice.bid + lowPrice.bid + closePrice.bid) / 3;
        //                    thisPrice.typicalPrice.ask = (highPrice.ask + lowPrice.ask + closePrice.ask) / 3;

        //                    List<double> closePrices = new List<double>();
        //                    closePrices = ticks.Select(x => (double)(x.bid + x.offer) / 2).ToList();
        //                    StandardDeviation sd = new StandardDeviation(closePrices);
        //                    thisPrice.stdDev = sd.Value;

        //                    modQuote thisCandle = new modQuote();
        //                    thisCandle.Date = tickStart;
        //                    thisCandle.Close = (decimal)(thisPrice.closePrice.bid + thisPrice.closePrice.ask) / 2;
        //                    thisCandle.Open = (decimal)(thisPrice.openPrice.bid + thisPrice.openPrice.ask) / 2;
        //                    thisCandle.High = (decimal)(thisPrice.highPrice.bid + thisPrice.highPrice.ask) / 2;
        //                    thisCandle.Low = (decimal)(thisPrice.lowPrice.bid + thisPrice.lowPrice.ask) / 2;
        //                    thisCandle.Typical = (decimal)(thisPrice.typicalPrice.bid + thisPrice.typicalPrice.ask) / 2;
        //                    thisCandle.stdDev = sd.Value;

        //                    AddStatusMessage($"New Tick :{thisPrice.startDate} - {thisPrice.endDate}");
        //                    AddStatusMessage($"   Open: {thisPrice.openPrice.bid} / {thisPrice.openPrice.ask} ");
        //                    AddStatusMessage($"   High: {thisPrice.highPrice.bid} / {thisPrice.highPrice.ask} ");
        //                    AddStatusMessage($"   Low:  {thisPrice.lowPrice.bid} / {thisPrice.lowPrice.ask} ");
        //                    AddStatusMessage($"   Close:{thisPrice.closePrice.bid} / {thisPrice.closePrice.ask} ");
        //                    AddStatusMessage($"   Typical:{thisPrice.typicalPrice.bid} / {thisPrice.typicalPrice.ask} ");

        //                    foreach (tick item in ticks) thisEpic.ticks.Remove(item);

        //                    model.quotes = new ModelQuotes();

        //                    AddStatusMessage("Getting rsi quotes from DB.......");
        //                    List<modQuote> rsiQuotes = new List<modQuote>();
        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceDataSMA(the_db, epicName, "MINUTE", 0, _startTime, _endTime, strategy, true, 50);
        //                    List<modQuote> indCandles5Mins = await RSI_LoadPrices.GetPriceDataSMA5Mins(the_db, epicName, "MINUTE_5", 0, _startTime, _endTime, strategy, true, 5000);

        //                    indCandles.Add(thisCandle);

        //                    model.candles.spread = thisSpread;


        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = thisPrice.startDate }, new QuoteComparer());
        //                    int numNewCandles = indCandles.Count;




        //                    model.candles.currentCandle = new ModelMinuteCandle();
        //                    model.candles.currentCandle.epicName = epicName;
        //                    model.candles.currentCandle.candleStart = thisPrice.startDate;
        //                    model.candles.currentCandle.thisQuote = indCandles[numNewCandles - 1];

        //                    model.candles.currentCandle.bolli_avg = Convert.ToDouble(indCandles5Mins.TakeLast((int)thisInput.var1).Average(s => s.Close));
        //                    model.candles.currentCandle.bolli_avgPrev = Convert.ToDouble(indCandles5Mins.SkipLast((int)thisInput.var4).TakeLast((int)thisInput.var1).Average(s => s.Close));
        //                    model.candles.currentCandle.bolli_mid = Convert.ToDouble(indCandles.TakeLast((int)thisInput.var2).Average(s => s.Close));

        //                    model.candles.currentCandle.bolli_sigma = Convert.ToDouble(indCandles.GetStdDev((int)thisInput.var2).LastOrDefault().StdDev);
        //                    model.candles.currentCandle.bolli_upper = model.candles.currentCandle.bolli_mid + thisInput.var3 * model.candles.currentCandle.bolli_sigma;
        //                    model.candles.currentCandle.bolli_lower = model.candles.currentCandle.bolli_mid - thisInput.var3 * model.candles.currentCandle.bolli_sigma;

        //                    model.candles.currentCandle.FirstBid = (decimal)thisPrice.openPrice.bid;
        //                    model.candles.currentCandle.FirstOffer = (decimal)thisPrice.openPrice.ask;

        //                    model.candles.prevCandle = new ModelMinuteCandle();
        //                    model.candles.prevCandle.candleStart = indCandles[numNewCandles - 2].Date;
        //                    model.candles.prevCandle.thisQuote = indCandles[numNewCandles - 2];
        //                    model.candles.prevCandle.epicName = epicName;


        //                    CommonFunctions.AddStatusMessage($"SumQuantites in bolliTrades = {model.thisModel.bolliTrades.Sum(x => x.quantity)}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"quantity in modelVar = {model.modelVar.quantity}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"var5  = {thisInput.var5}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"Is this close ({model.candles.currentCandle.thisQuote.Close}) > prev close ({model.candles.prevCandle.thisQuote.Close}) = {model.candles.currentCandle.thisQuote.Close > model.candles.prevCandle.thisQuote.Close}", "DEBUG", logName); 

        //                    CommonFunctions.AddStatusMessage($"check quantites = is sumquantites ({model.thisModel.bolliTrades.Sum(x => x.quantity)}) <= quantity ({model.modelVar.quantity}) * var5 {thisInput.var5}"  , "DEBUG", logName);  

        //                    if (model.thisModel.bolliTrades.Sum(x => x.quantity) <= model.modelVar.quantity * thisInput.var5)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"OK to do another trade", "DEBUG", logName);
        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Max trades reached", "DEBUG", logName);
        //                    }

        //                    CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong}, sellLong={model.sellLong}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);


        //                    CommonFunctions.AddStatusMessage($"bolli_avg    :{model.candles.currentCandle.bolli_avg} ", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"bolli_avgPrev:{model.candles.currentCandle.bolli_avgPrev}", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"bolli_mid    :{model.candles.currentCandle.bolli_mid}", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"bolli_sigma  :{model.candles.currentCandle.bolli_sigma}", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"bolli_upper  :{model.candles.currentCandle.bolli_upper}", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"bolli_lower  :{model.candles.currentCandle.bolli_lower}", "DEBUG");

        //                    model.RunProTrendCodeBOLLI(model.candles);

        //                    CommonFunctions.AddStatusMessage($"values after  run         - buyLong={model.buyLong}, sellLong={model.sellLong}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);
        //                    //clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");
        //                    CommonFunctions.AddStatusMessage($"Current standard deviation - {model.candles.currentCandle.thisQuote.stdDev}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                    CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);

        //                    CommonFunctions.AddStatusMessage($"current bolliID = {this.bolliID}", "DEBUG", logName);

        //                    // Check for stop
        //                    if (model.onMarket || model.thisModel.bolliTrades.Count > 0)
        //                    {
        //                        //clsCommonFunctions.AddStatusMessage($"checking - (positionPrice - (double)candles.currentCandle.thisQuote.Close) * sumQuantities > (thisInput.var0 * modelVar.quantity)");
        //                        CommonFunctions.AddStatusMessage("Checking Stop ......");
        //                        CommonFunctions.AddStatusMessage($"positionPrice = {model.thisModel.bolliTrades.Average(s => s.buyPrice)}, candles.currentCandle.thisQuote.Close = {(double)model.candles.currentCandle.thisQuote.Close}, sumQuantities = {model.thisModel.bolliTrades.Sum(x => x.quantity)}, thisInput.var0 = {thisInput.var0}, modelVar.quantity = {model.modelVar.quantity}");
        //                        CommonFunctions.AddStatusMessage($"current position = positionPrice ({model.thisModel.bolliTrades.Average(s => s.buyPrice)}) - close ({model.candles.currentCandle.thisQuote.Close}) * sumQuantites ({model.thisModel.bolliTrades.Sum(x => x.quantity)} = {((double)model.thisModel.bolliTrades.Average(s => s.buyPrice) - (double)model.candles.currentCandle.thisQuote.Close) * model.thisModel.bolliTrades.Sum(x => x.quantity)}");
        //                        CommonFunctions.AddStatusMessage($"stop threshold = thisInput.var0 ({thisInput.var0}) * modelVar.quantity ({model.modelVar.quantity}) = {thisInput.var0 * model.modelVar.quantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"calculation = {((double)model.thisModel.bolliTrades.Average(s => s.buyPrice) - (double)model.candles.currentCandle.thisQuote.Close) * model.thisModel.bolliTrades.Sum(x => x.quantity)} > {thisInput.var0 * model.modelVar.quantity} = {((double)model.thisModel.bolliTrades.Average(s => s.buyPrice) - (double)model.candles.currentCandle.thisQuote.Close) * model.thisModel.bolliTrades.Sum(x => x.quantity) > (thisInput.var0 * model.modelVar.quantity)}", "DEBUG", logName);
        //                    }
        //                        //clsCommonFunctions.AddStatusMessage("Force buy");
        //                    //model.buyLong = true;
        //                    model.sellLong = false;
        //                    //model.buyLong = false;
        //                    //model.sellLong = true;


        //                    if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                    //if (this.suppTrade != null) { clsCommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }
        //                    List<tradeItem> openTrades = model.thisModel.bolliTrades.DeepCopy();
        //                    //model.sellShort = true;
        //                    bool sellingLongs = false;
        //                    if (model.sellLong)
        //                    {
        //                        sellingLongs = true;
        //                    }
        //                        if (param != "DEBUG")
        //                    {
        //                        //string thisDealRef = "";
        //                        //string dealType = "";
        //                        //bool dealSent = false;


        //                        //////////////////////////////////////////////////////////////////////////////////////////////
        //                        // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                        //////////////////////////////////////////////////////////////////////////////////////////////

        //                        //double currentPrice = 0;
        //                        if (openTrades.Count > 0 && sellingLongs == false)
        //                        {
        //                            CommonFunctions.AddStatusMessage($"BOLLI Trades ");
        //                            CommonFunctions.AddStatusMessage($"Num Trades - {openTrades.Count}");
        //                            CommonFunctions.AddStatusMessage($"Sum Quantity - {openTrades.Sum(x => x.quantity)}");

        //                            foreach (tradeItem ti in openTrades)
        //                            {
        //                                CommonFunctions.AddStatusMessage($"Trade id : {ti.tbDealId}, started: {ti.tradeStarted}, BuyPrice: {ti.buyPrice}, Quantity: {ti.quantity}");

        //                            }
        //                            CommonFunctions.AddStatusMessage("");
        //                        }

        //                        if (model.buyLong )
        //                        {
        //                            CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);

        //                            requestedTrade reqTrade = new requestedTrade();
        //                            reqTrade.dealType = "POSITION";
        //                            reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, 0, this.igAccountId);
        //                            requestedTrades.Add(reqTrade);
        //                            //if (reqTrade.dealReference != "")
        //                            //{
        //                            //    //dealSent = true;
        //                            //    thisDealRef = reqTrade.dealReference;
        //                            //    //dealType = "PlaceDeal";
        //                            //}

        //                        }
        //                        else
        //                        {
        //                            if (model.sellLong)
        //                            {

        //                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
        //                                //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);

        //                                string dealRef = await CloseDealEpic("long", (double)openTrades.Sum(x => x.quantity), this.epicName,this.igAccountId);
        //                                //foreach (tradeItem trade in openTrades)
        //                                //{
        //                                //    string dealRef = await CloseDeal("long", (double)trade.quantity, trade.tbDealId);
        //                                //    if (dealRef != "")
        //                                //    {
        //                                //        dealSent = true;
        //                                //        thisDealRef = dealRef;
        //                                //        dealType = "PlaceDeal";
        //                                //    }
        //                                //}
        //                                //string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                //if (dealRef != "")
        //                                //{
        //                                //    dealSent = true;
        //                                //    thisDealRef = dealRef;
        //                                //    dealType = "PlaceDeal";
        //                                //}

        //                            }
        //                        }
        //                    }




        //                    //reset any deal variables that could have been placed by the RunCode
        //                    model.buyLong = false;
        //                    model.buyShort = false;
        //                    model.sellLong = false;
        //                    model.sellShort = false;
        //                    model.buyLongSupp = false;
        //                    model.buyShortSupp = false;
        //                    model.sellLongSupp = false;
        //                    model.sellShortSupp = false;



        //                    //if (model.modelLogs.logs.Count() > 0)
        //                    //{
        //                    //    ModelLog log = new ModelLog();
        //                    //    log = model.modelLogs.logs[0];
        //                    //    log.modelRunID = modelID;
        //                    //    log.runDate = _startTime;
        //                    //    log.id = System.Guid.NewGuid().ToString();
        //                        if (model.onMarket)
        //                        {
        //                            currentStatus.onMarket = true;
        //                            if (model.longOnmarket)
        //                            {
        //                                currentStatus.tradeType = "Long";
        //                            }
        //                            if (model.shortOnMarket)
        //                            {
        //                                currentStatus.tradeType = "Short";
        //                            }
        //                            //currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                            //currentStatus.count = model.thisModel.currentTrade.count;

        //                        }
        //                        else
        //                        {
        //                            currentStatus.onMarket = false;
        //                            currentStatus.tradeType = "";
        //                        }

        //                    if (model.candles.currentCandle.bolli_avg > model.candles.currentCandle.bolli_avgPrev && model.onMarket)
        //                    {
        //                        currentStatus.lTT = 1;
        //                    }
        //                    else
        //                    {
        //                        currentStatus.lTT = 0;
        //                    }



        //                        currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                        currentStatus.accountId = this.igAccountId;
        //                        currentStatus.startingQuantity = modelVar.startingQuantity;
        //                        currentStatus.minQuantity = modelVar.minQuantity;
        //                        currentStatus.maxQuantity = modelVar.maxQuantity;
        //                        currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                        currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                        currentStatus.currentGain = modelVar.currentGain;
        //                        currentStatus.baseQuantity = modelVar.baseQuantity;
        //                        currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                        currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                        currentStatus.doSuppTrades = model.doSuppTrades;
        //                        currentStatus.doShorts = model.doShorts;
        //                        currentStatus.doLongs = model.doLongs;
        //                        //if(strategy == "RSI")
        //                        //{
        //                        currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                        //}
        //                        //currentStatus.epicName = this.epicName;
        //                        //send log to the website
        //                        //model.modelLogs.logs[0].epicName = this.epicName;
        //                        //Task taskA = Task.Run(() => clsCommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                        Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                        //save log to the database
        //                        //Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                        //await log.SaveDocument(logContainer);
        //                        model.modelLogs.logs = new List<ModelLog>();

        //                    //}
        //                }
        //                _startTime = _startTime.AddMinutes(1);


        //            }
        //            catch (Exception ex)
        //            {
        //                AddStatusMessage($"Error - {ex.ToString()}", "ERROR");
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode";
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage( "Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);
        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        //try
        //        //{
        //        //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //        //    if (ret != null)
        //        //    {
        //        //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //        //    }
        //        latestHour = DateTime.UtcNow.Hour;
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    Log log = new Log(the_app_db);
        //        //    log.Log_Message = ex.ToString();
        //        //    log.Log_Type = "Error";
        //        //    log.Log_App = "RunCode";
        //        //    await log.Save();
        //        //}

        //    }

        //    //if (liveMode)
        //    //{
        //    //    ti.Interval = GetInterval();
        //    //    ti.Start();
        //    //}
        //    return taskRet;
        //}
        public async Task<DateTime> getPrevMAStartDate(DateTime candleStartDate, string epic)
        {
            DateTime getStartDate = DateTime.MinValue;
            if (model != null && model.candles != null && model.candles.currentCandle != null)
            {
                getStartDate = model.candles.currentCandle.candleStart;


                int mm = model.candles.currentCandle.candleStart.Minute;
                int hh = model.candles.currentCandle.candleStart.Hour;
                if (mm <= 29) { mm = 29; } else { mm = 59; }
                getStartDate = new DateTime(model.candles.currentCandle.candleStart.Year, model.candles.currentCandle.candleStart.Month, model.candles.currentCandle.candleStart.Day, model.candles.currentCandle.candleStart.Hour, mm, model.candles.currentCandle.candleStart.Second);
                getStartDate = getStartDate.AddMinutes(-30);

                //if (!await IGModels.clsCommonFunctions.IsTradingOpen(candles.currentCandle.candleStart, this.exchangeClosedDates))
                if (!await IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, model.exchangeClosedDates, epic))
                //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate))
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

                    int time1 = 20;


                    bool isDaylight = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time").IsDaylightSavingTime(getStartDate);


                    //Hack because the US stay on DST for an extra week for some reason.
                    if (getStartDate < new DateTime(2024, 11, 3, 0, 0, 0) && getStartDate > new DateTime(2024, 10, 28, 0, 0, 0))
                    {
                        isDaylight = true;
                    }
                    //bool isDaylight = TimeZoneInfo.Local.IsDaylightSavingTime(dtCurrentDate);
                    if (!isDaylight)
                    {
                        time1 = 21;

                    }

                    getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, time1, getStartDate.Minute, 0);

                }
            }
            return getStartDate;
        }
        public RunRet iGUpdate(UpdateMessage msg)
        {
            RunRet taskRet = new RunRet();

            switch (msg.updateType)
            {
                case "UPDATE":
                    AddStatusMessage($"Update Message: {msg.itemName} - {msg.updateData}", "INFO");
                    OpuUpdate(msg.updateData, msg.itemName);
                    break;
                case "CONFIRM":
                    AddStatusMessage($"Confirm Message: {msg.itemName} - {msg.updateData}", "INFO");
                    ConfirmUpdate(msg.updateData, msg.itemName);
                    break;

            }
            return taskRet;
        }
        public async void OpuUpdate(string inputData, string itemName)
        {
            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                if (this.the_app_db == null || this.the_db == null)
                {
                    throw new InvalidOperationException("DBs are null in OPUUpdate");
                }

                this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
                //MappedDiagnosticsLogicalContext.Set("jobId", this.logName);
                ScopeContext.PushProperty("jobId", this.logName);
                //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
                TradeSubUpdate? tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                if (tradeSubUpdate != null && this.model != null)
                {
                    tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString() ?? "";
                    tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString() ?? "";
                    tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString() ?? "";
                    tradeSubUpdate.updateType = "OPU";
                    if (tradeSubUpdate.epic == this.epicName)
                    {
                        tradeSubUpdate.date = tradeSubUpdate.timestamp;
                        tsm.Channel = tradeSubUpdate.channel;
                        tsm.DealId = tradeSubUpdate.dealId;
                        tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
                        tsm.DealReference = tradeSubUpdate.dealReference;
                        tsm.DealStatus = tradeSubUpdate.dealStatus.ToString() ?? "";
                        tsm.Direction = tradeSubUpdate.direction.ToString() ?? "";
                        tsm.ItemName = itemName;
                        tsm.Epic = tradeSubUpdate.epic;
                        tsm.Expiry = tradeSubUpdate.expiry;
                        tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
                        tsm.Level = tradeSubUpdate.level;
                        tsm.Limitlevel = tradeSubUpdate.limitLevel;
                        tsm.Size = tradeSubUpdate.size;
                        tsm.Status = tradeSubUpdate.status.ToString() ?? "";
                        tsm.StopLevel = tradeSubUpdate.stopLevel;
                        tsm.Reason = tradeSubUpdate.reason;
                        tsm.date = tradeSubUpdate.timestamp;
                        tsm.StopDistance = tradeSubUpdate.stopDistance;

                        tsm.TradeType = "OPU";
                        if (tsm.Reason != null)
                        {
                            if (tsm.Reason != "")
                            {
                                tradeSubUpdate.reasonDescription = this.TradeErrors[tsm.Reason ?? ""];
                            }
                        }

                        if (tsm.Epic == this.epicName)
                        {
                            if (tsm.Status == "UPDATED" && currentTrade != null && this.model.thisModel.currentTrade != null)
                            {
                                clsTradeUpdate thisTrade = new clsTradeUpdate();
                                tradeItem thisModelTrade = new tradeItem();
                                if (this.strategy == "GRID")
                                {
                                    try
                                    {
                                        if (this.currentGRIDLTrade != null && this.model.thisModel.currentGRIDLTrade != null && this.currentGRIDLTrade.dealId == tsm.DealId)
                                        {
                                            thisTrade = this.currentGRIDLTrade;
                                            thisModelTrade = this.model.thisModel.currentGRIDLTrade;
                                        }
                                        else if (this.currentGRIDSTrade != null && this.model.thisModel.currentGRIDSTrade != null && this.currentGRIDSTrade.dealId == tsm.DealId)
                                        {
                                            thisTrade = this.currentGRIDSTrade;
                                            thisModelTrade = this.model.thisModel.currentGRIDSTrade;
                                        }
                                        else
                                        {
                                            CommonFunctions.AddStatusMessage("Trade update received for unknown deal id - " + tsm.DealId, "WARNING");
                                            return;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        CommonFunctions.AddStatusMessage("Trade update received for unknown deal id  - " + e.ToString(), "WARNING");
                                        return;
                                    }
                                }
                                else
                                {
                                    thisTrade = this.currentTrade;
                                    thisModelTrade = this.model.thisModel.currentTrade;
                                }
                                // Deal has been updated, so save the new data and move on.
                                if (tsm.DealStatus == "ACCEPTED" && this.currentTrade != null && thisModelTrade != null)
                                {

                                    //Only update if it is the current trade or is the supplementary trade that is affected (in case we have 2 trades running at the same time)
                                    if (tsm.DealId == thisTrade.dealId)
                                    {

                                        CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                        CommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                        CommonFunctions.AddStatusMessage("Updating  - " + tsm.DealId + " - Current Deal = " + this.currentTrade.dealId, "INFO");
                                        await this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);

                                        thisTrade.dealReference = tsm.DealReference;
                                        thisTrade.dealId = tsm.DealId;
                                        thisTrade.lastUpdated = tsm.date;
                                        thisTrade.status = tsm.Status;
                                        thisTrade.dealStatus = tsm.DealStatus;
                                        thisTrade.level = Convert.ToDecimal(tsm.Level);
                                        thisTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                        thisTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                        thisTrade.size = Convert.ToDecimal(tsm.Size);
                                        thisTrade.direction = tsm.Direction;

                                        thisModelTrade.candleSold = null;
                                        thisModelTrade.candleBought = null;
                                        thisModelTrade.tbDealId = tsm.DealId;
                                        thisModelTrade.tbDealReference = tsm.DealReference;
                                        thisModelTrade.tbDealStatus = tsm.DealStatus;
                                        thisModelTrade.timestamp = DateTime.UtcNow;
                                        thisModelTrade.tbReason = tsm.Status;
                                        if (this.epicName != "") { thisModelTrade.epic = this.epicName; }
                                        if (this.modelID != "") { thisModelTrade.modelRunID = this.modelID; }
                                        thisModelTrade.quantity = Convert.ToDouble(this.currentTrade.size);
                                        thisModelTrade.stopLossValue = Math.Abs(Convert.ToDouble(this.currentTrade.level) - Convert.ToDouble(this.currentTrade.stopLevel));

                                        this.model.stopPriceOld = Math.Abs(this.model.stopPrice);
                                        this.model.stopPrice = Math.Abs(thisModelTrade.stopLossValue);

                                        _ = thisModelTrade.SaveDocument(this.trade_container);

                                        // Save the TBAudit
                                        IGModels.clsCommonFunctions.SaveTradeAudit(this.the_app_db, thisModelTrade, (double)thisTrade.level, tsm.TradeType);

                                        // Save the last run vars into the TB settings table

                                        if (this.tb != null)
                                        {
                                            this.tb.lastRunVars = this.model.modelVar.DeepCopy();
                                            _ = this.tb.SaveDocument(this.the_app_db);
                                        }
                                        CommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(thisModelTrade));

                                        await tradeSubUpdate.Add(this.the_app_db);

                                        //this.model.stopPriceOld = this.model.stopPrice;
                                    }
                                    //else
                                    //{
                                    //if (this.suppTrade != null)
                                    //{
                                    //    if (tsm.DealId == this.suppTrade.dealId)
                                    //    {
                                    //        clsCommonFunctions.AddStatusMessage("Updating supp trade  - " + tsm.DealId + " - Current Deal = " + this.suppTrade.dealId, "INFO");
                                    //        this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);
                                    //        this.model.thisModel.suppTrade.candleSold = null;
                                    //        this.model.thisModel.suppTrade.candleBought = null;
                                    //        this.suppTrade.dealReference = tsm.DealReference;
                                    //        this.suppTrade.dealId = tsm.DealId;
                                    //        this.suppTrade.lastUpdated = tsm.date;
                                    //        this.suppTrade.status = tsm.Status;
                                    //        this.suppTrade.dealStatus = tsm.DealStatus;
                                    //        this.suppTrade.level = Convert.ToDecimal(tsm.Level);
                                    //        this.suppTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                    //        this.suppTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                    //        this.suppTrade.size = Convert.ToDecimal(tsm.Size);
                                    //        this.suppTrade.direction = tsm.Direction;
                                    //        //this.model.thisModel.currentTrade = new tradeItem();

                                    //        this.model.thisModel.suppTrade.tbDealId = tsm.DealId;
                                    //        this.model.thisModel.suppTrade.tbDealReference = tsm.DealReference;
                                    //        this.model.thisModel.suppTrade.tbDealStatus = tsm.DealStatus;
                                    //        this.model.thisModel.suppTrade.timestamp = DateTime.UtcNow;
                                    //        this.model.thisModel.suppTrade.tbReason = tsm.Status;
                                    //        if (this.epicName != "") { this.model.thisModel.suppTrade.epic = this.epicName; }
                                    //        if (this.modelID != "") { this.model.thisModel.suppTrade.modelRunID = this.modelID; }
                                    //        this.model.thisModel.suppTrade.quantity = Convert.ToDouble(this.suppTrade.size);
                                    //        this.model.thisModel.suppTrade.stopLossValue = Math.Abs(Convert.ToDouble(this.suppTrade.level) - Convert.ToDouble(this.suppTrade.stopLevel));
                                    //        //this.model.stopPriceOld = Math.Abs(this.model.stopPrice);
                                    //        //this.model.stopPrice = Math.Abs(this.model.thisModel.currentTrade.stopLossValue);

                                    //        this.model.thisModel.suppTrade.SaveDocument(this.trade_container);

                                    //        // Save the last run vars into the TB settings table
                                    //        //this.tb.lastRunVars = this.model.modelVar.DeepCopy();
                                    //        //await this.tb.SaveDocument(this.the_app_db);

                                    //        clsCommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(this.model.thisModel.suppTrade), this.the_app_db);
                                    //        //this.model.stopPriceOld = this.model.stopPrice;
                                    //    }
                                    //}
                                    //}
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("UPDATE failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], "ERROR");
                                    TradingBrain.Models.CommonFunctions.SaveLog("Error", "UpdateTs", "UPDATE failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], this.the_app_db);
                                    await tradeSubUpdate.Add(this.the_app_db);
                                }

                                //tradeSubUpdate.Add(this.the_app_db);
                            }
                            else if (tsm.Status == "DELETED")
                            {
                                if (tsm.DealStatus == "ACCEPTED")
                                {

                                    // Deal has been closed (either by the software or by the stop being met).
                                    //Only delete if it is the current trade, or it is a supplementary trade that is affected (in case we have 2 trades running at the same time)

                                    //IgResponse<ConfirmsResponse> ret = await _igContainer.igRestApiClient.retrieveConfirm("G2QBWF9EFPET28R");
                                    bool deleteTrade = false;
                                    if (this.strategy == "BOLLI")
                                    {
                                        tradeItem? matchTrade = this.model.thisModel.bolliTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                        if (matchTrade != null)
                                        {
                                            CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                            //clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                            CommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + matchTrade.tbDealId, "INFO");
                                            DateTime dtNow = DateTime.UtcNow;
                                            tradeItem dbTrade = matchTrade; // await this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);
                                            dbTrade.candleSold = null;
                                            dbTrade.candleBought = null;

                                            clsTradeUpdate cTrade = new clsTradeUpdate
                                            {
                                                lastUpdated = dtNow,
                                                status = tsm.Status,
                                                dealStatus = tsm.DealStatus,
                                                level = Convert.ToDecimal(tsm.Level),
                                                //cTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                                //cTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                                channel = tsm.Channel
                                            };

                                            if (Convert.ToDecimal(tsm.Size) > 0)
                                            {
                                                cTrade.size = Convert.ToDecimal(tsm.Size);
                                            }
                                            cTrade.direction = tsm.Direction;

                                            dbTrade.channel = tsm.Channel;

                                            this.model.stopPriceOld = 0;// this.model.stopPrice;

                                            dbTrade.tradeEnded = dtNow;
                                            //clsCommonFunctions.AddStatusMessage("tsm.Direction = " + tsm.Direction, "INFO");
                                            if (tsm.Direction == "BUY")
                                            {
                                                //clsCommonFunctions.AddStatusMessage("deleting buy", "INFO");
                                                dbTrade.sellPrice = Convert.ToDecimal(cTrade.level);
                                                dbTrade.sellDate = dtNow;
                                                dbTrade.tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;

                                                this.model.sellLong = false;
                                                this.model.buyLong = false;
                                                this.model.longOnmarket = false;

                                                if (this.model.modelLogs.logs.Count >= 1)
                                                {
                                                    this.model.modelLogs.logs[0].tradeType = "Long";
                                                    this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                    this.model.modelLogs.logs[0].quantity = dbTrade.quantity;
                                                    this.model.modelLogs.logs[0].tradePrice = dbTrade.sellPrice;
                                                    this.model.modelLogs.logs[0].tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;
                                                }
                                                CommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(dbTrade));
                                            }

                                            this.model.sellLong = false;
                                            this.model.buyLong = false;
                                            this.model.longOnmarket = false;
                                            this.model.buyShort = false;
                                            this.model.sellShort = false;
                                            this.model.shortOnMarket = false;


                                            this.model.modelVar.strategyProfit += dbTrade.tradeValue;
                                            this.model.modelVar.numCandlesOnMarket = 0;
                                            // set the trade values in the next run of the code rather than right away so we can aggregate trades and supp trades if needs be
                                            this.lastTradeDeleted = true;


                                            // Save tbAudit
                                            IGModels.clsCommonFunctions.SaveTradeAudit(this.the_app_db, dbTrade, (double)cTrade.level, tsm.TradeType);



                                            dbTrade.units = dbTrade.sellPrice - dbTrade.buyPrice;
                                            dbTrade.tbDealStatus = tsm.DealStatus;
                                            dbTrade.tradeValue = dbTrade.units * Convert.ToDecimal(dbTrade.quantity);
                                            dbTrade.timestamp = DateTime.UtcNow;
                                            dbTrade.candleSold = null;
                                            dbTrade.candleBought = null;
                                            if (this.epicName != "") { dbTrade.epic = this.epicName; }
                                            if (this.modelID != "") { dbTrade.modelRunID = this.modelID; }
                                            dbTrade.tbReason = tsm.Status;

                                            this.lastTradeValue = (double)dbTrade.tradeValue;

                                            _ = dbTrade.SaveDocument(this.trade_container);


                                            await tradeSubUpdate.Add(this.the_app_db);

                                            //Send email
                                            try
                                            {
                                                //string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                                //if (region == "LIVE")
                                                //{
                                                //    clsEmail obj = new clsEmail();
                                                //    List<recip> recips = new List<recip>();
                                                //    //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                //    string subject = "TRADE ENDED - " + this.currentTrade.epic;
                                                //    string text = "The trade has ended in the " + region + " environment</br></br>";
                                                //    text += "<ul>";
                                                //    text += "<li>Trade ID : " + this.currentTrade.dealId + "</li>";
                                                //    text += "<li>Epic : " + this.currentTrade.epic + "</li>";
                                                //    text += "<li>Date : " + this.currentTrade.lastUpdated + "</li>";
                                                //    text += "<li>Type : " + this.model.thisModel.currentTrade.longShort + "</li>";
                                                //    text += "<li>Trade value : " + this.model.thisModel.currentTrade.tradeValue + "</li>";
                                                //    text += "<li>Size : " + this.currentTrade.size + "</li>";
                                                //    text += "<li>Price : " + this.currentTrade.level + "</li>";
                                                //    text += "<li>Stop Level : " + this.currentTrade.stopLevel + "</li>";
                                                //    text += "<li>NG count : " + this.modelVar.counter + "</li>";
                                                //    text += "</ul>";
                                                //    obj.sendEmail(recips, subject, text);
                                                //}
                                            }
                                            catch (Exception ex)
                                            {
                                                var log = new TradingBrain.Models.Log(this.the_app_db)
                                                {
                                                    Log_Message = ex.ToString(),
                                                    Log_Type = "Error",
                                                    Log_App = "UpdateTsOPU",
                                                    Epic = ""
                                                };
                                                await log.Save();
                                            }
                                            this.model.thisModel.currentTrade = null;
                                            this.currentTrade = null;
                                            this.model.onMarket = false;



                                            if (matchTrade != null)
                                            {
                                                this.model.thisModel.bolliTrades.Remove(matchTrade);
                                            }
                                            // keep on market until the last bolli trade is removed
                                            if (this.model.thisModel.bolliTrades.Count > 0)
                                            {
                                                this.model.onMarket = true;
                                            }
                                            else
                                            {
                                                // Clear down the bollid    
                                                this.bolliID = "";
                                            }


                                        }
                                    }
                                    else
                                    {
                                        if (this.strategy == "GRID")
                                        {
                                            //Check for long first
                                            tradeItem? matchTrade = this.model.thisModel.gridLTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                            if (matchTrade == null)
                                            {
                                                //then check for short
                                                matchTrade = this.model.thisModel.gridSTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                            }

                                            if (matchTrade != null)
                                            {
                                                CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                                //clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                                CommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + matchTrade.tbDealId, "INFO");
                                                DateTime dtNow = DateTime.UtcNow;
                                                tradeItem dbTrade = matchTrade; // await this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);
                                                dbTrade.candleSold = null;
                                                dbTrade.candleBought = null;

                                                clsTradeUpdate cTrade = new clsTradeUpdate
                                                {
                                                    lastUpdated = dtNow,
                                                    status = tsm.Status,
                                                    dealStatus = tsm.DealStatus,
                                                    level = Convert.ToDecimal(tsm.Level),
                                                    //cTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                                    //cTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                                    channel = tsm.Channel
                                                };

                                                if (Convert.ToDecimal(tsm.Size) > 0)
                                                {
                                                    cTrade.size = Convert.ToDecimal(tsm.Size);
                                                }
                                                cTrade.direction = tsm.Direction;

                                                dbTrade.channel = tsm.Channel;

                                                this.model.stopPriceOld = 0;// this.model.stopPrice;

                                                dbTrade.tradeEnded = dtNow;
                                                //clsCommonFunctions.AddStatusMessage("tsm.Direction = " + tsm.Direction, "INFO");
                                                if (tsm.Direction == "BUY")
                                                {
                                                    //clsCommonFunctions.AddStatusMessage("deleting buy", "INFO");
                                                    dbTrade.sellPrice = Convert.ToDecimal(cTrade.level);
                                                    dbTrade.sellDate = dtNow;
                                                    dbTrade.tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;

                                                    this.model.sellLong = false;
                                                    this.model.buyLong = false;
                                                    this.model.longOnmarket = false;

                                                    if (this.model.modelLogs.logs.Count >= 1)
                                                    {
                                                        this.model.modelLogs.logs[0].tradeType = "Long";
                                                        this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                        this.model.modelLogs.logs[0].quantity = dbTrade.quantity;
                                                        this.model.modelLogs.logs[0].tradePrice = dbTrade.sellPrice;
                                                        this.model.modelLogs.logs[0].tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;
                                                    }



                                                }
                                                else
                                                {
                                                    dbTrade.buyPrice = Convert.ToDecimal(cTrade.level);
                                                    dbTrade.purchaseDate = dtNow;
                                                    dbTrade.tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;

                                                    this.model.sellShort = false;
                                                    this.model.buyShort = false;
                                                    this.model.shortOnMarket = false;

                                                    if (this.model.modelLogs.logs.Count >= 1)
                                                    {
                                                        this.model.modelLogs.logs[0].tradeType = "Short";
                                                        this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                        this.model.modelLogs.logs[0].quantity = dbTrade.quantity;
                                                        this.model.modelLogs.logs[0].tradePrice = dbTrade.buyPrice;
                                                        this.model.modelLogs.logs[0].tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * (decimal)cTrade.size;
                                                    }


                                                }

                                                //this.model.sellLong = false;
                                                //this.model.buyLong = false;
                                                //this.model.longOnmarket = false;
                                                //this.model.buyShort = false;
                                                //this.model.sellShort = false;
                                                //this.model.shortOnMarket = false;


                                                this.model.modelVar.strategyProfit += dbTrade.tradeValue;
                                                this.model.modelVar.numCandlesOnMarket = 0;
                                                // set the trade values in the next run of the code rather than right away so we can aggregate trades and supp trades if needs be
                                                this.lastTradeDeleted = true;


                                                // Save tbAudit
                                                IGModels.clsCommonFunctions.SaveTradeAudit(this.the_app_db, dbTrade, (double)cTrade.level, tsm.TradeType);



                                                dbTrade.units = dbTrade.sellPrice - dbTrade.buyPrice;
                                                dbTrade.tbDealStatus = tsm.DealStatus;
                                                dbTrade.tradeValue = dbTrade.units * Convert.ToDecimal(dbTrade.quantity);
                                                dbTrade.timestamp = DateTime.UtcNow;
                                                dbTrade.candleSold = null;
                                                dbTrade.candleBought = null;
                                                if (this.epicName != "") { dbTrade.epic = this.epicName; }
                                                if (this.modelID != "") { dbTrade.modelRunID = this.modelID; }
                                                dbTrade.tbReason = tsm.Status;

                                                this.lastTradeValue = (double)dbTrade.tradeValue;

                                                _ = dbTrade.SaveDocument(this.trade_container);


                                                await tradeSubUpdate.Add(this.the_app_db);

                                                //Send email
                                                try
                                                {
                                                    //string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                                    //if (region == "LIVE")
                                                    //{
                                                    //    clsEmail obj = new clsEmail();
                                                    //    List<recip> recips = new List<recip>();
                                                    //    //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                    //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                    //    string subject = "TRADE ENDED - " + this.currentTrade.epic;
                                                    //    string text = "The trade has ended in the " + region + " environment</br></br>";
                                                    //    text += "<ul>";
                                                    //    text += "<li>Trade ID : " + this.currentTrade.dealId + "</li>";
                                                    //    text += "<li>Epic : " + this.currentTrade.epic + "</li>";
                                                    //    text += "<li>Date : " + this.currentTrade.lastUpdated + "</li>";
                                                    //    text += "<li>Type : " + this.model.thisModel.currentTrade.longShort + "</li>";
                                                    //    text += "<li>Trade value : " + this.model.thisModel.currentTrade.tradeValue + "</li>";
                                                    //    text += "<li>Size : " + this.currentTrade.size + "</li>";
                                                    //    text += "<li>Price : " + this.currentTrade.level + "</li>";
                                                    //    text += "<li>Stop Level : " + this.currentTrade.stopLevel + "</li>";
                                                    //    text += "<li>NG count : " + this.modelVar.counter + "</li>";
                                                    //    text += "</ul>";
                                                    //    obj.sendEmail(recips, subject, text);
                                                    //}
                                                }
                                                catch (Exception ex)
                                                {
                                                    var log = new TradingBrain.Models.Log(this.the_app_db)
                                                    {
                                                        Log_Message = ex.ToString(),
                                                        Log_Type = "Error",
                                                        Log_App = "UpdateTsOPU",
                                                        Epic = ""
                                                    };
                                                    await log.Save();
                                                }
                                                this.model.thisModel.currentTrade = null;
                                                this.currentTrade = null;
                                                //if (tsm.Direction == "BUY")
                                                //{
                                                //    this.model.thisModel.currentGRIDLTrade = null;
                                                //    this.currentGRIDLTrade = null;
                                                //}
                                                //else
                                                //{
                                                //    this.model.thisModel.currentGRIDSTrade = null;
                                                //    this.currentGRIDSTrade = null;
                                                //}






                                                if (matchTrade != null)
                                                {
                                                    if (tsm.Direction == "BUY")
                                                    {

                                                        this.model.thisModel.closedGridLTrades.Add(matchTrade);
                                                        this.model.thisModel.gridLTrades.Remove(matchTrade);

                                                        CommonFunctions.AddStatusMessage($"closedGridLTrades  = {this.model.thisModel.closedGridLTrades.Count} : gridLTrades = {this.model.thisModel.gridLTrades.Count} ", "DEBUG");

                                                        if (this.model.thisModel.gridLTrades.Count == 0)
                                                        {
                                                            CommonFunctions.AddStatusMessage($"closeAttemptCount going from {this.closeAttemptCount} to 0", "DEBUG");
                                                            this.closeAttemptCount = 0;
                                                            CommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(this.model.thisModel.closedGridLTrades));
                                                            //this.model.thisModel.closingGridLTrade = false;
                                                            this.model.thisModel.closedGridLTrades.Clear();
                                                            this.model.thisModel.currentGRIDLTrade = null;
                                                            this.currentGRIDLTrade = null;
                                                            this.model.onMarket = false;
                                                        }
                                                        //else
                                                        //{
                                                        //    //this.model.thisModel.closingGridLTrade = true;
                                                        //} 
                                                    }
                                                    else
                                                    {
                                                        this.model.thisModel.closedGridSTrades.Add(matchTrade);
                                                        this.model.thisModel.gridSTrades.Remove(matchTrade);

                                                        CommonFunctions.AddStatusMessage($"closedGridSTrades  = {this.model.thisModel.closedGridSTrades.Count} : gridSTrades = {this.model.thisModel.gridSTrades.Count} ", "DEBUG");

                                                        if (this.model.thisModel.gridSTrades.Count == 0)
                                                        {
                                                            CommonFunctions.AddStatusMessage($"closeAttemptCount going from {this.closeAttemptCount} to 0", "DEBUG");
                                                            this.closeAttemptCount = 0;
                                                            CommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(this.model.thisModel.closedGridSTrades));
                                                            //this.model.thisModel.closingGridSTrade = false;
                                                            this.model.thisModel.closedGridSTrades.Clear();
                                                            this.model.thisModel.currentGRIDSTrade = null;
                                                            this.currentGRIDSTrade = null;
                                                            this.model.onMarket = false;
                                                        }
                                                        //else
                                                        //{
                                                        //    //this.model.thisModel.closingGridLTrade = true;
                                                        //}

                                                    }
                                                }

                                                // keep on market until the last grid trade is removed
                                                if (tsm.Direction == "BUY")
                                                {
                                                    if (this.model.thisModel.gridLTrades.Count > 0)
                                                    {
                                                        this.model.onMarket = true;
                                                    }
                                                    else
                                                    {
                                                        // Clear down the bollid    
                                                        this.gridLID = "";
                                                    }
                                                }
                                                else
                                                {
                                                    if (this.model.thisModel.gridSTrades.Count > 0)
                                                    {
                                                        this.model.onMarket = true;
                                                    }
                                                    else
                                                    {
                                                        // Clear down the bollid    
                                                        this.gridSID = "";
                                                    }
                                                }


                                            }
                                        }
                                        else
                                        {
                                            clsTradeUpdate? thisTrade = new clsTradeUpdate();
                                            tradeItem? thisModelTrade = new tradeItem();
                                            //if (this.strategy == "GRID")
                                            //{
                                            //    if (this.currentGRIDLTrade.dealId == tsm.DealId)
                                            //    {
                                            //        thisTrade = this.currentGRIDLTrade;
                                            //        thisModelTrade = this.model.thisModel.currentGRIDLTrade;
                                            //    }
                                            //    else if (this.currentGRIDSTrade.dealId == tsm.DealId)
                                            //    {
                                            //        thisTrade = this.currentGRIDSTrade;
                                            //        thisModelTrade = this.model.thisModel.currentGRIDSTrade;
                                            //    }
                                            //    else
                                            //    {
                                            //        clsCommonFunctions.AddStatusMessage("Trade update received for unknown deal id - " + tsm.DealId, "WARNING");
                                            //        return;
                                            //    }
                                            //}
                                            //else
                                            //{
                                            if (this.currentTrade != null && this.model.thisModel.currentTrade != null && _igContainer != null && _igContainer.igRestApiClient != null && this.tb != null)
                                            {
                                                thisTrade = this.currentTrade;
                                                thisModelTrade = this.model.thisModel.currentTrade;
                                                //}



                                                if (tsm.DealId == thisTrade.dealId)
                                                {
                                                    deleteTrade = true;
                                                }

                                                if (thisTrade != null)
                                                {
                                                    if (deleteTrade)
                                                    {
                                                        CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                                        CommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                                        CommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + thisTrade.dealId, "INFO");
                                                        DateTime dtNow = DateTime.UtcNow;
                                                        await this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);

                                                        thisTrade.lastUpdated = dtNow;
                                                        thisTrade.status = tsm.Status;
                                                        thisTrade.dealStatus = tsm.DealStatus;
                                                        thisTrade.level = Convert.ToDecimal(tsm.Level);
                                                        thisTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                                        thisTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                                        thisTrade.channel = tsm.Channel;

                                                        if (Convert.ToDecimal(tsm.Size) > 0)
                                                        {
                                                            thisTrade.size = Convert.ToDecimal(tsm.Size);
                                                        }
                                                        thisTrade.direction = tsm.Direction;

                                                        thisModelTrade.candleSold = null;
                                                        thisModelTrade.candleBought = null;
                                                        thisModelTrade.channel = tsm.Channel;

                                                        // Buy price = level so need to get data from the API
                                                        if ((tsm.Direction == "BUY" && thisModelTrade.buyPrice == thisTrade.level) || (tsm.Direction == "SELL" && thisModelTrade.sellPrice == thisTrade.level))
                                                        {
                                                            AddStatusMessage($"Sorting closing price for deal {thisTrade.dealId}", "DEBUG");
                                                            try
                                                            {
                                                                IgResponse<dto.endpoint.accountactivity.activity.ActivityHistoryResponse> historyRet = await _igContainer.igRestApiClient.lastActivityPeriod("3600000");
                                                                if (historyRet.Response.activities != null)
                                                                {
                                                                    if (historyRet.Response.activities.Count >= 1)
                                                                    {
                                                                        bool histFound = false;
                                                                        foreach (dto.endpoint.accountactivity.activity.Activity activity in historyRet.Response.activities)
                                                                        {
                                                                            if (activity.dealId == this.currentTrade.dealId && activity.result.Contains("Position/s closed:"))
                                                                            {
                                                                                this.currentTrade.level = Convert.ToDecimal(activity.level);
                                                                                histFound = true;
                                                                                AddStatusMessage($"History - found price ({this.currentTrade.level}) for deal {this.currentTrade.dealId}", "DEBUG");
                                                                            }
                                                                        }
                                                                        if (!histFound)
                                                                        {
                                                                            AddStatusMessage($"History - activity not found for deal {this.currentTrade.dealId}", "DEBUG");
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        AddStatusMessage("History Response activities = 0", "DEBUG");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    AddStatusMessage("History Response is null", "DEBUG");
                                                                }
                                                            }
                                                            catch (Exception apiex)
                                                            {
                                                                var log = new TradingBrain.Models.Log(this.the_app_db)
                                                                {
                                                                    Log_Message = apiex.ToString(),
                                                                    Log_Type = "Error",
                                                                    Log_App = "OPUUpdate",
                                                                    Epic = ""
                                                                };
                                                                await log.Save();
                                                                AddStatusMessage($"Getting history errored - {apiex.ToString()}", "ERROR");
                                                            }

                                                        }


                                                        //this.model.thisModel.currentTrade = new tradeItem();
                                                        //this.model.thisModel.currentTrade.quantity = Convert.ToDouble(this.currentTrade.size);
                                                        //this.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(this.currentTrade.level) - Convert.ToDouble(this.currentTrade.stopLevel);
                                                        this.model.stopPrice = 0;// Math.Abs(this.model.thisModel.currentTrade.stopLossValue);
                                                        this.model.stopPriceOld = 0;// this.model.stopPrice;

                                                        thisModelTrade.tradeEnded = dtNow;
                                                        CommonFunctions.AddStatusMessage("tsm.Direction = " + tsm.Direction, "INFO");
                                                        if (tsm.Direction == "BUY")
                                                        {
                                                            CommonFunctions.AddStatusMessage("deleting buy", "INFO");
                                                            thisModelTrade.sellPrice = Convert.ToDecimal(thisTrade.level);
                                                            thisModelTrade.sellDate = dtNow;
                                                            thisModelTrade.tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * (decimal)thisTrade.size;

                                                            this.model.sellLong = false;
                                                            this.model.buyLong = false;
                                                            this.model.longOnmarket = false;

                                                            if (this.model.modelLogs.logs.Count >= 1)
                                                            {
                                                                this.model.modelLogs.logs[0].tradeType = "Long";
                                                                this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                                this.model.modelLogs.logs[0].quantity = thisModelTrade.quantity;
                                                                this.model.modelLogs.logs[0].tradePrice = thisModelTrade.sellPrice;
                                                                this.model.modelLogs.logs[0].tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * (decimal)thisTrade.size;
                                                            }
                                                            CommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(thisModelTrade));
                                                        }
                                                        else
                                                        {
                                                            CommonFunctions.AddStatusMessage("deleting sell", "INFO");
                                                            thisModelTrade.buyPrice = Convert.ToDecimal(thisTrade.level);
                                                            thisModelTrade.purchaseDate = dtNow;
                                                            thisModelTrade.tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * (decimal)thisTrade.size;
                                                            this.model.buyShort = false;
                                                            this.model.sellShort = false;
                                                            this.model.shortOnMarket = false;
                                                            if (this.model.modelLogs.logs.Count >= 1)
                                                            {
                                                                this.model.modelLogs.logs[0].tradeType = "Short";
                                                                this.model.modelLogs.logs[0].tradeAction = "Buy";
                                                                this.model.modelLogs.logs[0].tradePrice = thisModelTrade.buyPrice;
                                                                this.model.modelLogs.logs[0].tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * (decimal)thisTrade.size;
                                                            }
                                                            CommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(thisModelTrade));
                                                        }
                                                        this.model.sellLong = false;
                                                        this.model.buyLong = false;
                                                        this.model.longOnmarket = false;
                                                        this.model.buyShort = false;
                                                        this.model.sellShort = false;
                                                        this.model.shortOnMarket = false;


                                                        this.model.modelVar.strategyProfit += thisModelTrade.tradeValue;
                                                        this.model.modelVar.numCandlesOnMarket = 0;
                                                        // set the trade values in the next run of the code rather than right away so we can aggregate trades and supp trades if needs be
                                                        this.lastTradeDeleted = true;
                                                        this.lastTradeValue = (double)thisModelTrade.tradeValue;

                                                        //check if the last trade lost and was at max quantity. If so then we need to do a reset 
                                                        CommonFunctions.AddStatusMessage($"Check if reset required - quantity = {thisModelTrade.quantity}, maxQuantity = {this.model.modelVar.maxQuantity}, tradeValue = {thisModelTrade.tradeValue}", "DEBUG");
                                                        if ((thisModelTrade.quantity + 1) >= this.model.modelVar.maxQuantity && thisModelTrade.tradeValue < 0)
                                                        {
                                                            this.lastTradeMaxQuantity = true;
                                                            CommonFunctions.AddStatusMessage($"Do reset next run - lastTradeMaxQuantity = {this.lastTradeMaxQuantity}", "DEBUG");
                                                        }
                                                        //if (this.model.thisModel.currentTrade.tradeValue <= 0)
                                                        //{
                                                        //    this.model.modelVar.carriedForwardLoss = this.model.modelVar.carriedForwardLoss + (double)Math.Abs(this.model.thisModel.currentTrade.tradeValue);
                                                        //}
                                                        //else
                                                        //{
                                                        //    this.model.modelVar.carriedForwardLoss = this.model.modelVar.carriedForwardLoss - (double)Math.Abs(this.model.thisModel.currentTrade.tradeValue);
                                                        //    if (this.model.modelVar.carriedForwardLoss < 0) { this.model.modelVar.carriedForwardLoss = 0; }
                                                        //    this.model.modelVar.currentGain += Math.Max((double)this.model.thisModel.currentTrade.tradeValue - this.model.modelVar.carriedForwardLoss, 0);
                                                        //}

                                                        if (this.model.modelVar.strategyProfit > this.model.modelVar.maxStrategyProfit) { this.model.modelVar.maxStrategyProfit = this.model.modelVar.strategyProfit; }

                                                        // Save tbAudit
                                                        IGModels.clsCommonFunctions.SaveTradeAudit(this.the_app_db, thisModelTrade, (double)thisTrade.level, tsm.TradeType);



                                                        thisModelTrade.units = thisModelTrade.sellPrice - thisModelTrade.buyPrice;
                                                        thisModelTrade.tbDealStatus = tsm.DealStatus;

                                                        thisModelTrade.timestamp = DateTime.UtcNow;
                                                        thisModelTrade.candleSold = null;
                                                        thisModelTrade.candleBought = null;
                                                        if (this.epicName != "") { thisModelTrade.epic = this.epicName; }
                                                        if (this.modelID != "") { thisModelTrade.modelRunID = this.modelID; }
                                                        thisModelTrade.tbReason = tsm.Status;
                                                        this.model.thisModel.modelTrades.Add(thisModelTrade);
                                                        this.model.modelVar.numCandlesOnMarket = 0;
                                                        thisModelTrade.numCandlesOnMarket = this.model.modelVar.numCandlesOnMarket;

                                                        // Save the last run vars into the TB settings table
                                                        //Figure out any CFL so we can update the able.
                                                        CommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {this.tb.lastRunVars.carriedForwardLoss}, original currentGain = {this.tb.lastRunVars.currentGain}", "DEBUG", logName);
                                                        double nettPosition = lastTradeValue + lastTradeSuppValue;
                                                        CommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

                                                        if (nettPosition <= 0)
                                                        {
                                                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
                                                            model.modelVar.quantityMultiplier = 1;
                                                        }
                                                        else
                                                        {
                                                            double newGain = Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
                                                            CommonFunctions.AddStatusMessage($"newGain  Max({nettPosition} - {model.modelVar.carriedForwardLoss} , 0 ) =  {newGain}", "DEBUG", logName);
                                                            model.modelVar.carriedForwardLoss = Math.Max(model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition), 0);

                                                            if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }

                                                            model.modelVar.currentGain += newGain;

                                                            if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
                                                        }
                                                        CommonFunctions.AddStatusMessage($"new CarriedForwardLoss  = {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
                                                        CommonFunctions.AddStatusMessage($"new currentGain  = {model.modelVar.currentGain}", "DEBUG", logName);

                                                        tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
                                                        tb.lastRunVars.currentGain = model.modelVar.currentGain;
                                                        tb.lastRunVars.numCandlesOnMarket = 0;
                                                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;

                                                        this.tb.lastRunVars = this.model.modelVar.DeepCopy();
                                                        _ = this.tb.SaveDocument(this.the_app_db);

                                                        CommonFunctions.AddStatusMessage("Saving trade", "INFO");
                                                        _ = thisModelTrade.SaveDocument(this.trade_container);
                                                        CommonFunctions.AddStatusMessage("Trade saved", "INFO");


                                                        await tradeSubUpdate.Add(this.the_app_db);

                                                        //if (this.model.thisModel.currentTrade.attachedOrder != null)
                                                        //{
                                                        //    // Close any open orders
                                                        //    if (this.model.thisModel.currentTrade.attachedOrder.dealId != "")
                                                        //    {
                                                        //        clsCommonFunctions.AddStatusMessage($"Deleting order (if exists) {this.model.thisModel.currentTrade.attachedOrder.dealId}", "INFO");
                                                        //        this.DeleteOrder(this.model.thisModel.currentTrade.attachedOrder.direction, this.model.thisModel.currentTrade.attachedOrder.orderSize, this.model.thisModel.currentTrade.attachedOrder.dealId);
                                                        //        clsCommonFunctions.AddStatusMessage("Order deleted", "INFO");
                                                        //    }
                                                        //}
                                                        //// Close supp trade if it is still running
                                                        //if (this.model.onSuppTrade)
                                                        //{
                                                        //    clsCommonFunctions.AddStatusMessage($"Closing supp trade (if exists) {this.model.thisModel.suppTrade.tbDealId}", "INFO");
                                                        //    this.CloseDeal(this.model.thisModel.suppTrade.longShort.ToLower(), this.model.thisModel.suppTrade.quantity, this.model.thisModel.suppTrade.tbDealId);
                                                        //    clsCommonFunctions.AddStatusMessage("Supp trade deleted", "INFO");
                                                        //}

                                                    }

                                                    //Send email
                                                    try
                                                    {
                                                        //string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                                        //if (region == "LIVE")
                                                        //{
                                                        //    clsEmail obj = new clsEmail();
                                                        //    List<recip> recips = new List<recip>();
                                                        //    //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                        //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                        //    string subject = "TRADE ENDED - " + this.currentTrade.epic;
                                                        //    string text = "The trade has ended in the " + region + " environment</br></br>";
                                                        //    text += "<ul>";
                                                        //    text += "<li>Trade ID : " + this.currentTrade.dealId + "</li>";
                                                        //    text += "<li>Epic : " + this.currentTrade.epic + "</li>";
                                                        //    text += "<li>Date : " + this.currentTrade.lastUpdated + "</li>";
                                                        //    text += "<li>Type : " + this.model.thisModel.currentTrade.longShort + "</li>";
                                                        //    text += "<li>Trade value : " + this.model.thisModel.currentTrade.tradeValue + "</li>";
                                                        //    text += "<li>Size : " + this.currentTrade.size + "</li>";
                                                        //    text += "<li>Price : " + this.currentTrade.level + "</li>";
                                                        //    text += "<li>Stop Level : " + this.currentTrade.stopLevel + "</li>";
                                                        //    text += "<li>NG count : " + this.modelVar.counter + "</li>";
                                                        //    text += "</ul>";
                                                        //    obj.sendEmail(recips, subject, text);
                                                        //}
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var log = new TradingBrain.Models.Log(this.the_app_db)
                                                        {
                                                            Log_Message = ex.ToString(),
                                                            Log_Type = "Error",
                                                            Log_App = "UpdateTsOPU",
                                                            Epic = ""
                                                        };
                                                        await log.Save();
                                                    }
                                                    thisModelTrade = null;
                                                    thisTrade = null;
                                                    if (this.strategy == "GRID")
                                                    {
                                                        //GRID Trade
                                                        if (tsm.Direction == "BUY")
                                                        {
                                                            tradeItem? gridLTrade = this.model.thisModel.gridLTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                                            if (gridLTrade != null)
                                                            {
                                                                this.model.thisModel.gridLTrades.Remove(gridLTrade);
                                                            }
                                                            // keep on market until the last bolli trade is removed
                                                            if (this.model.thisModel.gridLTrades.Count > 0)
                                                            {
                                                                this.model.onMarket = true;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            tradeItem? gridSTrade = this.model.thisModel.gridSTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                                            if (gridSTrade != null)
                                                            {
                                                                this.model.thisModel.gridSTrades.Remove(gridSTrade);
                                                            }
                                                            // keep on market until the last bolli trade is removed
                                                            if (this.model.thisModel.gridSTrades.Count > 0)
                                                            {
                                                                this.model.onMarket = true;
                                                            }
                                                        }

                                                    }
                                                    else
                                                    {
                                                        if (this.strategy == "BOLLI")
                                                        {
                                                            //BOLLI trade
                                                            tradeItem? bolliTrade = this.model.thisModel.bolliTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                                            if (bolliTrade != null)
                                                            {
                                                                this.model.thisModel.bolliTrades.Remove(bolliTrade);
                                                            }
                                                            // keep on market until the last bolli trade is removed
                                                            if (this.model.thisModel.bolliTrades.Count > 0)
                                                            {
                                                                this.model.onMarket = true;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            //Normal SMA trade
                                                            this.currentTrade = null;
                                                            this.model.thisModel.currentTrade = null;
                                                            this.model.onMarket = false;
                                                        }

                                                    }

                                                }
                                            }


                                        }
                                    }

                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("DELETED failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], "ERROR");
                                    TradingBrain.Models.CommonFunctions.SaveLog("Error", "UpdateTs", "DELETED failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], this.the_app_db);
                                    await tradeSubUpdate.Add(this.the_app_db);
                                }
                                //tradeSubUpdate.Add(this.the_app_db);

                            }
                            else if (tsm.Status == "OPEN")
                            {
                                if (tsm.DealStatus == "ACCEPTED")
                                {

                                    string orderDealId = tsm.DealId;
                                    string osDealRef = "";
                                    string accountId = "";
                                    if (this.strategy == "GRID")
                                    {
                                        if (this._igContainer2 == null) { throw new InvalidOperationException("IG Container 2 is null for GRID strategy"); }
                                        if (tsm.Direction == "BUY")
                                        {
                                            osDealRef = this.newGRIDLDealReference;
                                            accountId = this._igContainer.creds.igAccountId;
                                        }
                                        else
                                        {
                                            osDealRef = this.newGRIDSDealReference;
                                            accountId = this._igContainer2.creds.igAccountId;
                                        }
                                    }
                                    else
                                    {
                                        osDealRef = this.newDealReference;
                                        accountId = this._igContainer.creds.igAccountId;
                                    }
                                    CommonFunctions.AddStatusMessage($"Processing OPEN trade update for DealRef: {tsm.DealReference}, saved deal ref = {osDealRef}", "INFO");
                                    // Check the deal id with the deal reference from the Place Deal call to ensure we are dealing with the correct trade
                                    if (tsm.DealReference == osDealRef || this.strategy == "GRID" && osDealRef == "")
                                    {
                                        CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                        CommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                        this.newDealReference = "";

                                        //Not on market so this must be a new current trade
                                        DateTime thisDate = DateTime.UtcNow;


                                        clsTradeUpdate thisTrade = new clsTradeUpdate
                                        {
                                            epic = tsm.Epic,
                                            dealReference = tsm.DealReference,
                                            dealId = tsm.DealId,
                                            lastUpdated = thisDate,
                                            status = tsm.Status,
                                            dealStatus = tsm.DealStatus,
                                            level = Convert.ToDecimal(tsm.Level),
                                            stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel)),
                                            stopDistance = Convert.ToDecimal(tsm.StopDistance),
                                            size = Convert.ToDecimal(tsm.Size),
                                            direction = tsm.Direction,
                                            accountId = this.igAccountId,
                                            channel = tsm.Channel
                                        };

                                        tradeItem thisModelTrade = new tradeItem
                                        {
                                            //this.model.thisModel.currentTrade = new tradeItem();
                                            quantity = Convert.ToDouble(thisTrade.size),
                                            stopLossValue = Convert.ToDouble(thisTrade.stopLevel),
                                            tbDealId = tsm.DealId,
                                            tbDealReference = tsm.DealReference,
                                            tbDealStatus = tsm.DealStatus,
                                            tbReason = tsm.Status
                                        };
                                        this.model.stopPrice = thisModelTrade.stopLossValue;
                                        this.model.stopPriceOld = this.model.stopPrice;
                                        thisModelTrade.tradeStarted = thisDate;// new DateTime(thisDate.Year, thisDate.Month, thisDate.Day, thisDate.Hour, thisDate.Minute, thisDate.Second);
                                        thisModelTrade.modelRunID = this.modelID;
                                        thisModelTrade.epic = this.epicName;
                                        thisModelTrade.timestamp = DateTime.UtcNow;
                                        thisModelTrade.channel = tsm.Channel;
                                        thisModelTrade.accountId = accountId;

                                        // Set the bolliID
                                        if (strategy == "BOLLI")
                                        {
                                            if (this.bolliID == "")
                                            {
                                                this.bolliID = System.Guid.NewGuid().ToString();
                                            }
                                            thisModelTrade.BOLLI_ID = this.bolliID;
                                        }
                                        if (strategy == "GRID")
                                        {
                                            if (tsm.Direction == "BUY")
                                            {
                                                if (this.gridLID == "")
                                                {
                                                    this.gridLID = System.Guid.NewGuid().ToString();
                                                }
                                                thisModelTrade.BOLLI_ID = this.gridLID;
                                            }
                                            else
                                            {
                                                if (this.gridSID == "")
                                                {
                                                    this.gridSID = System.Guid.NewGuid().ToString();
                                                }
                                                thisModelTrade.BOLLI_ID = this.gridSID;
                                            }
                                        }
                                        // set the target

                                        if (tsm.Direction == "BUY")
                                        {
                                            thisModelTrade.longShort = "Long";
                                            thisModelTrade.buyPrice = Convert.ToDecimal(thisTrade.level);
                                            thisModelTrade.purchaseDate = thisDate;

                                            //this.model.sellLong = false;
                                            this.model.buyLong = false;
                                            this.model.longOnmarket = true;
                                            // this.model.buyShort = false;
                                            //this.model.shortOnMarket = false;
                                            if (this.model.modelLogs.logs.Count >= 1)
                                            {
                                                this.model.modelLogs.logs[0].tradeType = "Long";
                                                this.model.modelLogs.logs[0].tradeAction = "Buy";
                                                this.model.modelLogs.logs[0].quantity = thisModelTrade.quantity;
                                                this.model.modelLogs.logs[0].tradePrice = thisModelTrade.buyPrice;
                                            }
                                            CommonFunctions.SendBroadcast("BuyLong", JsonConvert.SerializeObject(thisModelTrade));
                                        }
                                        else
                                        {
                                            thisModelTrade.longShort = "Short";
                                            thisModelTrade.sellPrice = (decimal)thisTrade.level;
                                            thisModelTrade.sellDate = thisDate;
                                            thisModelTrade.modelRunID = this.modelID;
                                            this.model.sellShort = false;
                                            this.model.shortOnMarket = true;
                                            //this.model.buyLong = false;
                                            //this.model.longOnmarket = false;
                                            if (this.model.modelLogs.logs.Count >= 1)
                                            {
                                                this.model.modelLogs.logs[0].tradeType = "Short";
                                                this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                this.model.modelLogs.logs[0].quantity = thisModelTrade.quantity;
                                                this.model.modelLogs.logs[0].tradePrice = thisModelTrade.sellPrice;
                                            }
                                            CommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(thisModelTrade));
                                        }
                                        this.model.onMarket = true;

                                        if (this.strategy == "RSI" ||
                                            this.strategy == "REI" ||
                                            this.strategy == "RSI-ATR" ||
                                            this.strategy == "RSI-CUML" ||
                                            this.strategy == "CASEYC" ||
                                            this.strategy == "VWAP" ||
                                            this.strategy == "CASEYCSHORT" ||
                                            this.strategy == "CASEYCEQUITIES")
                                        {
                                            thisTrade.limitLevel = Convert.ToDecimal(tsm.Limitlevel);
                                            thisModelTrade.targetPrice = Convert.ToDecimal(tsm.Limitlevel);
                                        }

                                        // Save this trade in the database
                                        thisModelTrade.candleSold = null;
                                        thisModelTrade.candleBought = null;
                                        if (this.modelVar != null) { thisModelTrade.count = this.modelVar.counter; }
                                        thisModelTrade.strategy = this.strategy;
                                        thisModelTrade.resolution = this.resolution;

                                        _ = thisModelTrade.Add(this.the_app_db, this.trade_container);

                                        // Save tbAudit
                                        IGModels.clsCommonFunctions.SaveTradeAudit(this.the_app_db, thisModelTrade, (double)thisTrade.level, tsm.TradeType);


                                        await tradeSubUpdate.Add(this.the_app_db);


                                        if (strategy == "GRID")
                                        {
                                            if (tsm.Direction == "BUY")
                                            {
                                                this.currentGRIDLTrade = thisTrade.DeepCopy();
                                                this.model.thisModel.currentGRIDLTrade = thisModelTrade.DeepCopy();
                                                this.model.thisModel.gridLTrades.Add(thisModelTrade.DeepCopy());
                                                CommonFunctions.AddStatusMessage($"Current long trade set in model - DealID: {this.model.thisModel.currentGRIDLTrade.tbDealId}, DealRef: {this.model.thisModel.currentGRIDLTrade.tbDealReference}", "INFO", logName);
                                                CommonFunctions.AddStatusMessage($"Current long trade set in local - DealID: {this.currentGRIDLTrade.dealId}, DealRef: {this.currentGRIDLTrade.dealReference}", "INFO", logName);
                                            }
                                            else
                                            {
                                                this.currentGRIDSTrade = thisTrade.DeepCopy();
                                                this.model.thisModel.currentGRIDSTrade = thisModelTrade.DeepCopy();
                                                this.model.thisModel.gridSTrades.Add(thisModelTrade.DeepCopy());
                                                CommonFunctions.AddStatusMessage($"Current short trade set in model - DealID: {this.model.thisModel.currentGRIDSTrade.tbDealId}, DealRef: {this.model.thisModel.currentGRIDSTrade.tbDealReference}", "INFO", logName);
                                                CommonFunctions.AddStatusMessage($"Current short trade set in local - DealID: {this.currentGRIDSTrade.dealId}, DealRef: {this.currentGRIDSTrade.dealReference}", "INFO", logName);
                                            }
                                        }
                                        else
                                        {
                                            this.currentTrade = thisTrade.DeepCopy();
                                            this.model.thisModel.currentTrade = thisModelTrade.DeepCopy();
                                            CommonFunctions.AddStatusMessage($"Current trade set in model - DealID: {this.model.thisModel.currentTrade.tbDealId}, DealRef: {this.model.thisModel.currentTrade.tbDealReference}", "INFO", logName);
                                            CommonFunctions.AddStatusMessage($"Current trade set in local - DealID: {this.currentTrade.dealId}, DealRef: {this.currentTrade.dealReference}", "INFO", logName);
                                        }
                                        //}



                                        //Send email
                                        string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                        try
                                        {
                                            //if (region == "LIVE")
                                            //{

                                            //    clsEmail obj = new clsEmail();
                                            //    List<recip> recips = new List<recip>();
                                            //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                            //    string subject = "NEW TRADE STARTED - " + thisTrade.epic;
                                            //    string text = "A new trade has started in the " + region + " environment</br></br>";
                                            //    text += "<ul>";
                                            //    text += "<li>Trade ID : " + thisTrade.dealId + "</li>";
                                            //    text += "<li>Epic : " + thisTrade.epic + "</li>";
                                            //    text += "<li>Date : " + thisTrade.lastUpdated + "</li>";
                                            //    text += "<li>Type : " + thisModelTrade.longShort + "</li>";
                                            //    text += "<li>Size : " + thisTrade.size + "</li>";
                                            //    text += "<li>Price : " + thisTrade.level + "</li>";
                                            //    text += "<li>Stop Level : " + thisTrade.stopLevel + "</li>";
                                            //    text += "<li>NG count : " + this.modelVar.counter + "</li>";
                                            //    text += "</ul>";

                                            //    obj.sendEmail(recips, subject, text);
                                            //}
                                        }
                                        catch (Exception ex)
                                        {
                                            var log = new TradingBrain.Models.Log(this.the_app_db)
                                            {
                                                Log_Message = ex.ToString(),
                                                Log_Type = "Error",
                                                Log_App = "UpdateTsOPU",
                                                Epic = ""
                                            };
                                            await log.Save();
                                        }

                                    }
                                    else
                                    {
                                        CommonFunctions.AddStatusMessage($"Unable to process trade update for DealRef: {tsm.DealReference}, saved deal ref = {osDealRef}", "INFO");
                                    }
                                    //}
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("OPEN failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], "ERROR");
                                    TradingBrain.Models.CommonFunctions.SaveLog("Error", "UpdateTs", "DELETED failed - " + tsm.Reason + " - " + this.TradeErrors[tsm.Reason ?? ""], this.the_app_db);
                                    await tradeSubUpdate.Add(this.the_app_db);
                                }
                                //tradeSubUpdate.Add(this.the_app_db);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CommonFunctions.AddStatusMessage("Error in ConfirmUpdate: " + ex.ToString(), "ERROR");
            }
        }

        public async void ConfirmUpdate(string inputData, string itemName)
        {
            var tsm = new IgPublicApiData.TradeSubscriptionModel();
            this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
            //MappedDiagnosticsLogicalContext.Set("jobId", this.logName);
            ScopeContext.PushProperty("jobId", this.logName);
            try
            {
                if (this.the_app_db == null)
                {
                    throw new InvalidOperationException("Database not set in ConfirmUpdate");
                }

                TradeSubUpdate? tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                if (tradeSubUpdate != null)
                {
                    tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString() ?? "";
                    tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString() ?? "";
                    tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString() ?? "";
                    tradeSubUpdate.updateType = "CONFIRM";

                    if (tradeSubUpdate.epic == this.epicName)
                    {
                        tsm.Channel = tradeSubUpdate.channel;
                        tsm.DealId = tradeSubUpdate.dealId;
                        tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
                        tsm.DealReference = tradeSubUpdate.dealReference;
                        tsm.DealStatus = tradeSubUpdate.dealStatus.ToString() ?? "";
                        tsm.Direction = tradeSubUpdate.direction.ToString() ?? "";
                        tsm.ItemName = itemName;
                        tsm.Epic = tradeSubUpdate.epic;
                        tsm.Expiry = tradeSubUpdate.expiry;
                        tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
                        tsm.Level = tradeSubUpdate.level;
                        tsm.Limitlevel = tradeSubUpdate.limitLevel;
                        tsm.Size = tradeSubUpdate.size;
                        tsm.Status = tradeSubUpdate.status.ToString() ?? "";
                        tsm.StopLevel = tradeSubUpdate.stopLevel;
                        tsm.Reason = tradeSubUpdate.reason;
                        tsm.date = tradeSubUpdate.date;
                        tsm.StopDistance = tradeSubUpdate.stopDistance;
                        tsm.TradeType = "CONFIRM";

                        tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString() ?? "";
                        tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString() ?? "";
                        tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString() ?? "";
                        if (tsm.Reason != null)
                        {
                            if (tsm.Reason != "")
                            {
                                tradeSubUpdate.reasonDescription = this.TradeErrors[tsm.Reason ?? ""];
                            }
                        }

                        tradeSubUpdate.updateType = tsm.TradeType;

                        if (tsm.Epic == this.epicName)
                        {

                            // Find this trade from the list of requested trades to tie in with the requested type (position or order)
                            //requestedTrade reqTrade = new requestedTrade();
                            //reqTrade = this.requestedTrades.Where(i => i.dealReference == tsm.DealReference).FirstOrDefault();

                            //if (reqTrade != null)
                            //{
                            CommonFunctions.AddStatusMessage($"CONFIRM - deal reference = {tsm.DealReference},   deal status = {tsm.Status}");

                            await tradeSubUpdate.Add(this.the_app_db);

                            // If this is a deletion, then update the trade record (previously updated from the OPU message) with the corect closing price. This is because IG changed the OPU message to return only the opening price!!
                            if (tsm.Status == "CLOSED" && tsm.Reason == "SUCCESS")
                            {
                                // wait 2 seconds just to ensure the OPU updating is finished.
                                // await Task.Delay(TimeSpan.FromSeconds(2));

                                // tradeItem thisTrade = await GetTradeFromDB(tsm.DealId);

                                // if (thisTrade.longShort == "Long")
                                // {
                                //     thisTrade.sellPrice = Convert.ToDecimal(tsm.Level);
                                //     thisTrade.units = thisTrade.sellPrice - thisTrade.buyPrice;
                                //     thisTrade.tradeValue = thisTrade.units * Convert.ToDecimal(thisTrade.quantity);
                                // }
                                // else
                                // {
                                //     thisTrade.buyPrice = Convert.ToDecimal(tsm.Level);
                                //     thisTrade.units = thisTrade.buyPrice - thisTrade.sellPrice;
                                //     thisTrade.tradeValue = thisTrade.units * Convert.ToDecimal(thisTrade.quantity);
                                // }
                                //await thisTrade.SaveDocument(this.trade_container);
                            }
                            //reqTrade.dealStatus = tsm.DealStatus;




                            if (tsm.Status == "OPEN" && tsm.Reason == "SUCCESS")
                            {
                                // trade/order opened successfully
                                CommonFunctions.AddStatusMessage($"CONFIRM - successful", "INFO");
                            }

                            //if (reqTrade.dealType == "ORDER" && reqTrade.dealStatus == "REJECTED")
                            //{

                            //    clsCommonFunctions.AddStatusMessage($"ORDER REJECTED -  {tsm.Reason} - {this.TradeErrors[tsm.Reason ?? ""]} : retryCount = {this.retryOrderCount}, retryOrderLimit = {this.retryOrderLimit}");
                            //    // Order has been rejected, possibly because the market is moving too fast. Try again next time.
                            //    if (this.retryOrderCount < this.retryOrderLimit)
                            //    {
                            //        this.retryOrder = true;
                            //        this.retryOrderCount += 1;
                            //        clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry set for next run");

                            //    }
                            //    else
                            //    {
                            //        clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry limit hit. Just forget about it.");
                            //        this.retryOrder = false;
                            //        this.retryOrderCount = 0;
                            //    }
                            //}

                            if (tsm.Status == null && tsm.Reason != "SUCCESS")
                            {
                                // trade/order not successful (could be update or open or delete)

                                CommonFunctions.AddStatusMessage($"CONFIRM - failed - - {tsm.Reason} - {this.TradeErrors[tsm.Reason ?? ""]}", "INFO");


                                if (this.model != null)
                                {
                                    CommonFunctions.AddStatusMessage($"CONFIRM - Resetting values due to  failure", "INFO");
                                    this.model.sellShort = false;
                                    this.model.sellLong = false;
                                    this.model.buyShort = false;
                                    this.model.shortOnMarket = false;
                                    this.model.buyLong = false;
                                    this.model.longOnmarket = false;
                                    this.model.onMarket = false;
                                }

                            }
                            //}

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(this.the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "UpdateTsCONFIRM",
                    Epic = ""
                };
                await log.Save();
            }
        }
        //public async Task<RunRet> RunCode_RSI(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    if (seconds <= 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes((-min) + 1);
        //        //_startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0);

        //    }

        //    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, 04, 0, 0) ;

        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);


        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    //paused = true;


        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.
        //        //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates,this.epicName).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Running code");
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            //var watch = new System.Diagnostics.Stopwatch();
        //            //var bigWatch = new System.Diagnostics.Stopwatch();
        //            //bigWatch.Start();
        //            try
        //            {
        //                //watch.Start();


        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);


        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        CommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        CommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                        }
        //                        else
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                        }

        //                        tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        //tb.lastRunVars.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                        // check to see if the trade just finished lost at max quantity, if so then we need to reset the vars
        //                        //clsCommonFunctions.AddStatusMessage($"checking if reset required - lastTradeMaxQuantity = {lastTradeMaxQuantity}", "DEBUG", logName);
        //                        //if (lastTradeMaxQuantity)
        //                        //{
        //                        //    clsCommonFunctions.AddStatusMessage($"old lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG", logName);
        //                        //    tb.lastRunVars.currentGain = Math.Max(tb.lastRunVars.currentGain - model.modelVar.carriedForwardLoss, 0);
        //                        //    tb.lastRunVars.carriedForwardLoss = 0;
        //                        //    tb.lastRunVars.quantity = tb.lastRunVars.minQuantity;
        //                        //    tb.lastRunVars.counter = 0;
        //                        //    tb.lastRunVars.maxQuantity = tb.lastRunVars.minQuantity * tb.lastRunVars.maxQuantityMultiplier;
        //                        //    model.modelVar.currentGain = tb.lastRunVars.currentGain;
        //                        //    model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                        //    model.modelVar.quantity = tb.lastRunVars.quantity;
        //                        //    model.modelVar.counter = tb.lastRunVars.counter;
        //                        //    model.modelVar.maxQuantity = tb.lastRunVars.maxQuantity;

        //                        //    clsCommonFunctions.AddStatusMessage($"new lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG", logName);
        //                        //}
        //                        //await tb.SaveDocument(the_app_db);

        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }
        //                //watch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - GetSettings - Time taken = " + watch.ElapsedMilliseconds);

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;
        //                model.nightingaleOn = true;
        //                //tb.lastRunVars.doLongsVar = tb.doLongs;
        //                //tb.lastRunVars.doShortsVar = tb.doShorts;
        //                //tb.lastRunVars.doSuppTradesVar = tb.doSuppTrades;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution,epicName);
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);



        //                    //modelInstanceInputs_RSI thisInput = clsCommonFunctions.GetInputsFromSpread_RSI(thisModel.inputs_RSI, thisCandle);

        //                    //Console.WriteLine(DateTime.Now.ToString("G") + "Getting rsi quotes from DB.......");
        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();
        //                    //DateTime startTime = DateTime.MinValue;
        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceData(the_db, epicName, resolution, resMod, _startTime, _endTime,strategy, true);



        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());



        //                    model.quotes.rsiCandleLow = indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var1).LastOrDefault().Rsi ?? 0;
        //                    model.quotes.rsiCandleHigh = indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var3).LastOrDefault().Rsi ?? 0;
        //                    model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                    int idx = (indIndex) - (int)thisInput.var7;
        //                    model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];


        //                    //model.candles.currentCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime, epicName, minute_container, TicksContainer, false, createMinRecord, the_app_db, model.exchangeClosedDates);

        //                    //// Check to see if we have prev and prev2 candles already. If not (i.e. first run) then go get them.
        //                    //if (model.candles.prevCandle.candleStart == DateTime.MinValue)
        //                    //{
        //                    //    model.candles.prevCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-1), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);

        //                    //}
        //                    //if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
        //                    //{
        //                    //    model.candles.prevCandle2 = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-2), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);
        //                    //}

        //                    //DateTime getStartDate = await model.getPrevMAStartDate(model.candles.currentCandle.candleStart);

        //                    //IG_Epic epic = new IG_Epic(epicName);
        //                    //clsMinuteCandle prevMa = await Get_MinuteCandle(the_db, minute_container, epic, getStartDate);
        //                    //model.candles.prevMACandle.mA30MinTypicalLongClose = prevMa.MovingAverages30Min[thisInput.var3 - 1].movingAverage.Close;
        //                    //model.candles.prevMACandle.mA30MinTypicalShortClose = prevMa.MovingAverages30Min[thisInput.var13 - 1].movingAverage.Close;


        //                    // Check if we should be adding trades at this hour
        //                    bool doTrade = true;
        //                    int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                    hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                    if (tradeHour != null)
        //                    {
        //                        doTrade = tradeHour.trade;
        //                    }

        //                    if (model.onMarket || (!model.onMarket && doTrade))
        //                    {


        //                        CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");


        //                        //model.RunProTrendCodeV2(model.candles);
        //                        model.RunProTrendCodeRSIV1(model.quotes);

        //                        CommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, sellLong={model.sellLong},  longOnmarket={model.longOnmarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                        //clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");

        //                        CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);

        //                        if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                        if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                        //model.sellShort = true;

        //                        if (param != "DEBUG")
        //                        {

        //                            //string thisDealRef = "";
        //                            //string dealType = "";
        //                            //bool dealSent = false;
        //                            //////////////////////////////////////////////////////////////////////////////////////////////
        //                            // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                            //////////////////////////////////////////////////////////////////////////////////////////////

        //                            double currentStop = 0;
        //                            double newStop = 0;
        //                            double currentPrice = 0;

        //                            if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                            {
        //                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                                if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                    model.sellLong = true;
        //                                }

        //                            }

        //                            //////////////////////////////////////////////////////////////////

        //                            if (model.buyLong && this.currentTrade == null)
        //                            {
        //                                CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                requestedTrade reqTrade = new requestedTrade();
        //                                reqTrade.dealType = "POSITION";
        //                                reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId, thisInput.profitTarget);
        //                                requestedTrades.Add(reqTrade);
        //                                //if (reqTrade.dealReference != "")
        //                                //{
        //                                //    //dealSent = true;
        //                                //    thisDealRef = reqTrade.dealReference;
        //                                //    //dealType = "PlaceDeal";
        //                                //}
        //                            }
        //                            else
        //                            {
        //                                if (model.sellLong)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                    string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                    //if (dealRef != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = dealRef;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                            }


        //                            if (model.longOnmarket)
        //                            {

        //                                //Don't touch the stop level as it should be done by trailing stops instead

        //                                //Also, if we put this back in, the edit deal function is not sending the limit level (target) so it is being overwritten.


        //                                //clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                                //if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                                //{



        //                                //    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                //    decimal? currentStopLevel = this.currentTrade.stopLevel;


        //                                //    this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                //    clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                //    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                //    EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue);



        //                                //}
        //                            }



        //                        }
        //                        try
        //                        {
        //                            if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.purchaseDate != DateTime.MinValue)
        //                            {
        //                                model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                            }

        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Log log = new Log(the_app_db);
        //                            log.Log_Message = ex.ToString();
        //                            log.Log_Type = "Error";
        //                            log.Log_App = "RunCode";
        //                            await log.Save();

        //                        }

        //                        //reset any deal variables that could have been placed by the RunCode
        //                        model.buyLong = false;
        //                        model.sellLong = false;


        //                        if (model.modelLogs.logs.Count() > 0)
        //                        {
        //                            ModelLog log = new ModelLog();
        //                            log = model.modelLogs.logs[0];
        //                            log.modelRunID = modelID;
        //                            log.runDate = _startTime;
        //                            log.id = System.Guid.NewGuid().ToString();
        //                            if (model.onMarket)
        //                            {
        //                                currentStatus.onMarket = true;

        //                                currentStatus.tradeType = "Long";


        //                                currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                currentStatus.count = model.thisModel.currentTrade.count;

        //                            }
        //                            else
        //                            {
        //                                currentStatus.onMarket = false;
        //                                currentStatus.tradeType = "";
        //                            }

        //                            currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                            currentStatus.accountId = this.igAccountId;
        //                            currentStatus.startingQuantity = modelVar.startingQuantity;
        //                            currentStatus.minQuantity = modelVar.minQuantity;
        //                            currentStatus.maxQuantity = modelVar.maxQuantity;
        //                            currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                            currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                            currentStatus.currentGain = modelVar.currentGain;
        //                            currentStatus.baseQuantity = modelVar.baseQuantity;
        //                            currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                            currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                            currentStatus.doSuppTrades = model.doSuppTrades;
        //                            currentStatus.doShorts = model.doShorts;
        //                            currentStatus.doLongs = model.doLongs;
        //                            currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            currentStatus.strategy = this.strategy;
        //                            currentStatus.resolution = this.resolution;
        //                            currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                            //currentStatus.epicName = this.epicName;
        //                            //send log to the website
        //                            model.modelLogs.logs[0].epicName = this.epicName;
        //                            Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                            Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                            //save log to the database
        //                            Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                            await log.SaveDocument(logContainer);
        //                            model.modelLogs.logs = new List<ModelLog>();

        //                        }


        //                        // save the run details to ensure all picked up
        //                        tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                        await tb.SaveDocument(the_app_db);

        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode";
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        CommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        try
        //        {
        //            IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //            if (ret != null)
        //            {
        //                CommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //            }
        //            latestHour = DateTime.UtcNow.Hour;
        //        }
        //        catch (Exception ex)
        //        {
        //            Log log = new Log(the_app_db);
        //            log.Log_Message = ex.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "RunCode";
        //            await log.Save();
        //        }

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_RSI_ATR(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    if (seconds <= 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes((-min) + 1);

        //    }

        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates,this.epicName).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        CommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        CommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                        }
        //                        else
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                        }

        //                        tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;

        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;
        //                model.nightingaleOn = true;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution,epicName);
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);

        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceData(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true);

        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());

        //                    model.quotes.rsiCandleLow = indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var1).LastOrDefault().Rsi ?? 0;
        //                    model.quotes.rsiCandleHigh = indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var3).LastOrDefault().Rsi ?? 0;
        //                    model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.atrCandleLow = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var10).LastOrDefault().Atr ?? 0;
        //                    model.quotes.atrCandleHigh = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var12).LastOrDefault().Atr ?? 0;

        //                    int idx = (indIndex) - (int)thisInput.var7;
        //                    model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];

        //                    // Check if we should be adding trades at this hour
        //                    bool doTrade = true;
        //                    int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                    hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                    if (tradeHour != null)
        //                    {
        //                        doTrade = tradeHour.trade;
        //                    }

        //                    if (model.onMarket || (!model.onMarket && doTrade))
        //                    {


        //                        CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                        // run the actual code 
        //                        model.RunProTrendCodeRSIATR(model.quotes);

        //                        CommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, sellLong={model.sellLong},  longOnmarket={model.longOnmarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                        CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);

        //                        if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                        if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                        //model.sellShort = true;

        //                        if (param != "DEBUG")
        //                        {

        //                            //string thisDealRef = "";
        //                            //string dealType = "";
        //                            //bool dealSent = false;

        //                            //////////////////////////////////////////////////////////////////////////////////////////////
        //                            // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                            //////////////////////////////////////////////////////////////////////////////////////////////

        //                            double currentStop = 0;
        //                            double newStop = 0;
        //                            double currentPrice = 0;

        //                            if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                            {
        //                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                                if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                    model.sellLong = true;
        //                                }

        //                            }

        //                            //////////////////////////////////////////////////////////////////

        //                            if (model.buyLong && this.currentTrade == null)
        //                            {
        //                                CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                requestedTrade reqTrade = new requestedTrade();
        //                                reqTrade.dealType = "POSITION";
        //                                reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId, thisInput.profitTarget);
        //                                requestedTrades.Add(reqTrade);

        //                                //if (reqTrade.dealReference != "")
        //                                //{
        //                                //    //dealSent = true;
        //                                //    thisDealRef = reqTrade.dealReference;
        //                                //    //dealType = "PlaceDeal";
        //                                //}
        //                            }
        //                            else
        //                            {
        //                                if (model.sellLong)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                    //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                    string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                    //if (dealRef != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = dealRef;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}

        //                                }
        //                            }


        //                            if (model.longOnmarket)
        //                            {

        //                                //Don't touch the stop level as it should be done by trailing stops instead

        //                                //Also, if we put this back in, the edit deal function is not sending the limit level (target) so it is being overwritten.


        //                                //clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                                //if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                                //{



        //                                //    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                //    decimal? currentStopLevel = this.currentTrade.stopLevel;


        //                                //    this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                //    clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                //    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                //    EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue);



        //                                //}
        //                            }



        //                        }
        //                        try
        //                        {
        //                            if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.purchaseDate != DateTime.MinValue)
        //                            {
        //                                model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                            }

        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Log log = new Log(the_app_db);
        //                            log.Log_Message = ex.ToString();
        //                            log.Log_Type = "Error";
        //                            log.Log_App = "RunCode";
        //                            await log.Save();

        //                        }

        //                        //reset any deal variables that could have been placed by the RunCode
        //                        model.buyLong = false;
        //                        model.sellLong = false;


        //                        if (model.modelLogs.logs.Count() > 0)
        //                        {
        //                            ModelLog log = new ModelLog();
        //                            log = model.modelLogs.logs[0];
        //                            log.modelRunID = modelID;
        //                            log.runDate = _startTime;
        //                            log.id = System.Guid.NewGuid().ToString();
        //                            if (model.onMarket)
        //                            {
        //                                currentStatus.onMarket = true;

        //                                currentStatus.tradeType = "Long";


        //                                currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                currentStatus.count = model.thisModel.currentTrade.count;

        //                            }
        //                            else
        //                            {
        //                                currentStatus.onMarket = false;
        //                                currentStatus.tradeType = "";
        //                            }

        //                            currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                            currentStatus.accountId = this.igAccountId;
        //                            currentStatus.startingQuantity = modelVar.startingQuantity;
        //                            currentStatus.minQuantity = modelVar.minQuantity;
        //                            currentStatus.maxQuantity = modelVar.maxQuantity;
        //                            currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                            currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                            currentStatus.currentGain = modelVar.currentGain;
        //                            currentStatus.baseQuantity = modelVar.baseQuantity;
        //                            currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                            currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                            currentStatus.doSuppTrades = model.doSuppTrades;
        //                            currentStatus.doShorts = model.doShorts;
        //                            currentStatus.doLongs = model.doLongs;
        //                            currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            currentStatus.strategy = this.strategy;
        //                            currentStatus.resolution = this.resolution;
        //                            currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                            //currentStatus.epicName = this.epicName;
        //                            //send log to the website
        //                            model.modelLogs.logs[0].epicName = this.epicName;
        //                            Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                            Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                            //save log to the database
        //                            Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                            await log.SaveDocument(logContainer);
        //                            model.modelLogs.logs = new List<ModelLog>();

        //                        }


        //                        // save the run details to ensure all picked up
        //                        tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                        await tb.SaveDocument(the_app_db);

        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode";
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        CommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        try
        //        {
        //            IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //            if (ret != null)
        //            {
        //                CommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //            }
        //            latestHour = DateTime.UtcNow.Hour;
        //        }
        //        catch (Exception ex)
        //        {
        //            Log log = new Log(the_app_db);
        //            log.Log_Message = ex.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "RunCode";
        //            await log.Save();
        //        }

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_RSI_CUML(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    if (seconds <= 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes((-min) + 1);

        //    }

        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName, model.futures).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        CommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        CommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                            model.modelVar.quantityMultiplier = 1;
        //                        }
        //                        else
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                            if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
        //                        }

        //                        tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;

        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;

        //                // turn off nightingale
        //                model.nightingaleOn = false;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0, 3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                    thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);

        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceData(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true);

        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());

        //                    //model.quotes.rsiCandleLow = indCandles.Take(indIndex + 1).GetRsi(thisInput.var1).LastOrDefault();
        //                    //model.quotes.rsiCandleHigh = indCandles.Take(indIndex + 1).GetRsi(thisInput.var3).LastOrDefault();
        //                    model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.atrCandleLow = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var10).LastOrDefault().Atr ?? 0;
        //                    model.quotes.atrCandleHigh = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var12).LastOrDefault().Atr ?? 0;

        //                    //model.quotes.rsiCandleHigh = new RsiResult(_startTime);
        //                    //model.quotes.rsiCandleLow = new RsiResult(_startTime);
        //                    model.quotes.rsiCandleLow = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var10).TakeLast(3).Average(s => s.Rsi);
        //                    model.quotes.rsiCandleHigh = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var12).TakeLast(3).Average(s => s.Rsi);

        //                    int idx = (indIndex) - (int)thisInput.var7;
        //                    model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];

        //                    // Check if we should be adding trades at this hour
        //                    bool doTrade = true;
        //                    int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                    hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                    if (tradeHour != null)
        //                    {
        //                        doTrade = tradeHour.trade;
        //                    }

        //                    if (model.onMarket || (!model.onMarket && doTrade))
        //                    {


        //                        CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                        // run the actual code 
        //                        model.RunProTrendCodeRSICUML(model.quotes);

        //                        CommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, sellLong={model.sellLong},  longOnmarket={model.longOnmarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                        CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"quantityMultiplier - {model.modelVar.quantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                        //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                        //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);

        //                        if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                        if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                        //model.sellShort = true;

        //                        if (param != "DEBUG")
        //                        {

        //                            //string thisDealRef = "";
        //                            //string dealType = "";
        //                           // bool dealSent = false;

        //                            //////////////////////////////////////////////////////////////////////////////////////////////
        //                            // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                            //////////////////////////////////////////////////////////////////////////////////////////////

        //                            double currentStop = 0;
        //                            double newStop = 0;
        //                            double currentPrice = 0;

        //                            if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                            {
        //                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                                if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                    model.sellLong = true;
        //                                }

        //                            }

        //                            //////////////////////////////////////////////////////////////////

        //                            if (model.buyLong && this.currentTrade == null)
        //                            {
        //                                CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                requestedTrade reqTrade = new requestedTrade();
        //                                reqTrade.dealType = "POSITION";
        //                                reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId, thisInput.profitTarget);
        //                                requestedTrades.Add(reqTrade);

        //                                //if (reqTrade.dealReference != "")
        //                                //{
        //                                //    //dealSent = true;
        //                                //    thisDealRef = reqTrade.dealReference;
        //                                //   // dealType = "PlaceDeal";
        //                                //}
        //                            }
        //                            else
        //                            {
        //                                if (model.sellLong)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                    //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                    string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                    //if (dealRef != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = dealRef;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}

        //                                }
        //                            }


        //                            if (model.longOnmarket)
        //                            {

        //                                //Don't touch the stop level as it should be done by trailing stops instead

        //                                //Also, if we put this back in, the edit deal function is not sending the limit level (target) so it is being overwritten.


        //                                //clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                                //if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                                //{



        //                                //    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                //    decimal? currentStopLevel = this.currentTrade.stopLevel;


        //                                //    this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                //    clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                //    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                //    EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue);



        //                                //}
        //                            }



        //                        }
        //                        try
        //                        {
        //                            if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.purchaseDate != DateTime.MinValue)
        //                            {
        //                                model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                            }

        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Log log = new Log(the_app_db);
        //                            log.Log_Message = ex.ToString();
        //                            log.Log_Type = "Error";
        //                            log.Log_App = "RunCode";
        //                            await log.Save();

        //                        }

        //                        //reset any deal variables that could have been placed by the RunCode
        //                        model.buyLong = false;
        //                        model.sellLong = false;


        //                        if (model.modelLogs.logs.Count() > 0)
        //                        {
        //                            ModelLog log = new ModelLog();
        //                            log = model.modelLogs.logs[0];
        //                            log.modelRunID = modelID;
        //                            log.runDate = _startTime;
        //                            log.id = System.Guid.NewGuid().ToString();
        //                            if (model.onMarket)
        //                            {
        //                                currentStatus.onMarket = true;

        //                                currentStatus.tradeType = "Long";


        //                                currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                currentStatus.count = model.thisModel.currentTrade.count;

        //                            }
        //                            else
        //                            {
        //                                currentStatus.onMarket = false;
        //                                currentStatus.tradeType = "";
        //                            }

        //                            currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                            currentStatus.accountId = this.igAccountId;
        //                            currentStatus.startingQuantity = modelVar.startingQuantity;
        //                            currentStatus.minQuantity = modelVar.minQuantity;
        //                            currentStatus.maxQuantity = modelVar.maxQuantity;
        //                            currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                            currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                            currentStatus.currentGain = modelVar.currentGain;
        //                            currentStatus.baseQuantity = modelVar.baseQuantity;
        //                            currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                            currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                            currentStatus.doSuppTrades = model.doSuppTrades;
        //                            currentStatus.doShorts = model.doShorts;
        //                            currentStatus.doLongs = model.doLongs;
        //                            currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            currentStatus.strategy = this.strategy;
        //                            currentStatus.resolution = this.resolution;
        //                            currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                            //currentStatus.epicName = this.epicName;
        //                            //send log to the website
        //                            model.modelLogs.logs[0].epicName = this.epicName;
        //                            Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                            Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                            //save log to the database
        //                            Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                            await log.SaveDocument(logContainer);
        //                            model.modelLogs.logs = new List<ModelLog>();

        //                        }


        //                        // save the run details to ensure all picked up
        //                        tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                        await tb.SaveDocument(the_app_db);

        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode";
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        CommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        try
        //        {
        //            IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //            if (ret != null)
        //            {
        //                CommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //            }
        //            latestHour = DateTime.UtcNow.Hour;
        //        }
        //        catch (Exception ex)
        //        {
        //            Log log = new Log(the_app_db);
        //            log.Log_Message = ex.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "RunCode";
        //            await log.Save();
        //        }

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_VWAP(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///

        //    AddStatusMessage($"Security token = {_igContainer.context.xSecurityToken}", "INFO");
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    //
        //    //_igContainer.tbClient.ConnectToRest();

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    int minutes = dtNow.Minute;
        //    if (seconds == 59 && minutes == 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour + 1, 0, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0).AddMinutes(-min);
        //    }


        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName, this.futures).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        //clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        //clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                            //model.modelVar.quantityMultiplier = 1;
        //                        }
        //                        else
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            //if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            //model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                            //if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
        //                        }

        //                        //tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        //tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;
        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;

        //                // turn on nightingale
        //                model.nightingaleOn = true;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                model.modelVar.baseQuantity = tb.lastRunVars.baseQuantity;
        //                model.modelVar.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                model.modelVar.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                model.modelVar.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                //model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                //modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;

        //                model.modelVar.winningBetMultiple = tb.lastRunVars.winningBetMultiple;
        //                model.modelVar.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                model.thisModel.strategy = strategy;
        //                model.thisModel.resolution = resolution;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;
        //                currentStatus.baseQuantity = tb.lastRunVars.baseQuantity;
        //                currentStatus.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                currentStatus.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                currentStatus.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                currentStatus.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                currentStatus.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                currentStatus.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                currentStatus.winningBetMultiple = tb.lastRunVars.winningBetMultiple;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0, 3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                    thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);

        //                    CommonFunctions.AddStatusMessage("Getting VWAP Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceDataVWAP(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true);


        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());

        //                    if (indIndex >= 0)
        //                    {

        //                        //model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev(thisInput.var6).LastOrDefault();
        //                        //model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault();
        //                        //model.quotes.atrCandleLow = indCandles.Take(indIndex + 1).GetAtr(thisInput.var10).LastOrDefault();
        //                        //model.quotes.atrCandleHigh = indCandles.Take(indIndex + 1).GetAtr(thisInput.var12).LastOrDefault();

        //                        //model.quotes.rsiCandleHigh = new RsiResult(_startTime);
        //                        //model.quotes.rsiCandleLow = new RsiResult(_startTime);
        //                        //model.quotes.rsiCandleLow.Rsi = (double)indCandles.Take(indIndex + 1).GetRsi(thisInput.var10).TakeLast(3).Average(s => s.Rsi);
        //                        //model.quotes.rsiCandleHigh.Rsi = (double)indCandles.Take(indIndex + 1).GetRsi(thisInput.var12).TakeLast(3).Average(s => s.Rsi);

        //                        //model.quotes.caseyC = (double)indCandles.GetRange(indIndex + 1 - thisInput.var1, thisInput.var1).Average(s => s.cRank[thisInput.var0 - 1].cRank);
        //                        //model.quotes.caseyCExit = (double)indCandles.GetRange(indIndex + 1 - thisInput.var3, thisInput.var3).Average(s => s.cRank[thisInput.var0 - 1].cRank);

        //                        model.quotes.rollingVWAP = indCandles[indIndex].vwapValues.FirstOrDefault(o => o.idx == (int)thisInput.var0).rollingVWAP;
        //                        model.quotes.stdDevVWAP = indCandles[indIndex].vwapValues.FirstOrDefault(o => o.idx == (int)thisInput.var0).stdDev;

        //                        //model.quotes.rollingVWAP = indCandles[indIndex].rollingVWAP;
        //                        //model.quotes.stdDevVWAP = indCandles[indIndex].stdDevVWAP;

        //                        //model.quotes.avgClose = (double)indCandles.GetRange(indIndex + 1 - thisInput.var3, thisInput.var3).Average(s => s.Close);
        //                        //model.quotes.prevAvgClose = (double)indCandles.GetRange(indIndex  - thisInput.var3, thisInput.var3).Average(s => s.Close);

        //                        model.quotes.avgClose = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.var3, (int)thisInput.var3).Average(s => s.Close);
        //                        model.quotes.prevAvgClose = (double)indCandles.GetRange(indIndex - (int)thisInput.var3, (int)thisInput.var3).Average(s => s.Close);

        //                        int trendFlag = 0;
        //                        if (model.quotes.avgClose > model.quotes.prevAvgClose)
        //                        {
        //                            trendFlag = 1;
        //                        }

        //                        CommonFunctions.AddStatusMessage($"rollingVWAP = {model.quotes.rollingVWAP}, stdDevVWAP = {model.quotes.stdDevVWAP}");
        //                        CommonFunctions.AddStatusMessage($"avgClose = {model.quotes.avgClose}, prevAvgClose = {model.quotes.prevAvgClose}");
        //                        CommonFunctions.AddStatusMessage($"trendFlag = {trendFlag} ");

        //                        //int idx = (indIndex) - thisInput.var7;
        //                        //model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev(thisInput.var6).ToList()[idx]; //stdDevResults[idx];

        //                        // Check if we should be adding trades at this hour
        //                        bool doTrade = true;
        //                        int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                        hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                        if (tradeHour != null)
        //                        {
        //                            doTrade = tradeHour.trade;
        //                        }

        //                        if (model.onMarket || (!model.onMarket && doTrade))
        //                        {


        //                            CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow.Rsi} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh.Rsi}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow.Atr} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh.Atr}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle.StdDev} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle.StdDev}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle.StdDev}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                            // run the actual code 
        //                            model.RunProTrendCodeVWAP(model.quotes);

        //                            CommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, sellLong={model.sellLong},  longOnmarket={model.longOnmarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                            CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"quantityMultiplier - {model.modelVar.quantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"WinningBetMultiple - {model.modelVar.winningBetMultiple}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxBetMultiple - {model.modelVar.maxBetMultiple}", "DEBUG", logName);


        //                            if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                            if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                            //model.sellShort = true;

        //                            if (param != "DEBUG")
        //                            {

        //                                //string thisDealRef = "";
        //                                //string dealType = "";
        //                                //bool dealSent = false;

        //                                //////////////////////////////////////////////////////////////////////////////////////////////
        //                                // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                                //////////////////////////////////////////////////////////////////////////////////////////////

        //                                double currentStop = 0;
        //                                double newStop = 0;
        //                                double currentPrice = 0;

        //                                if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                                {
        //                                    currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                    newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                    currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                    CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                    CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                                    if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                        model.sellLong = true;
        //                                    }

        //                                }

        //                                //////////////////////////////////////////////////////////////////

        //                                if (model.buyLong && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                    //model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
        //                                    model.stopLossVar = (double)model.quotes.currentCandle.closePrice.ask * (thisInput.stopLoss / 100);
        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId, (double)model.quotes.currentCandle.openPrice.ask * (thisInput.profitTarget / 100));
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.sellLong)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }


        //                                if (model.longOnmarket)
        //                                {

        //                                    //Don't touch the stop level as it should be done by trailing stops instead

        //                                    //Also, if we put this back in, the edit deal function is not sending the limit level (target) so it is being overwritten.


        //                                    //clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                                    //if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                                    //{



        //                                    //    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                    //    decimal? currentStopLevel = this.currentTrade.stopLevel;


        //                                    //    this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                    //    clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                    //    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                    //    EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue);



        //                                    //}
        //                                }



        //                            }
        //                            try
        //                            {
        //                                if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.purchaseDate != DateTime.MinValue)
        //                                {
        //                                    model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                    await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                                }

        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                Log log = new Log(the_app_db);
        //                                log.Log_Message = ex.ToString();
        //                                log.Log_Type = "Error";
        //                                log.Log_App = "RunCode";
        //                                await log.Save();

        //                            }

        //                            //reset any deal variables that could have been placed by the RunCode
        //                            model.buyLong = false;
        //                            model.sellLong = false;


        //                            if (model.modelLogs.logs.Count() > 0)
        //                            {
        //                                ModelLog log = new ModelLog();
        //                                log = model.modelLogs.logs[0];
        //                                log.modelRunID = modelID;
        //                                log.runDate = _startTime;
        //                                log.id = System.Guid.NewGuid().ToString();
        //                                if (model.onMarket)
        //                                {
        //                                    currentStatus.onMarket = true;

        //                                    currentStatus.tradeType = "Long";


        //                                    currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                    currentStatus.count = model.thisModel.currentTrade.count;

        //                                }
        //                                else
        //                                {
        //                                    currentStatus.onMarket = false;
        //                                    currentStatus.tradeType = "";
        //                                }

        //                                currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                                currentStatus.accountId = this.igAccountId;
        //                                currentStatus.startingQuantity = modelVar.startingQuantity;
        //                                currentStatus.minQuantity = modelVar.minQuantity;
        //                                currentStatus.maxQuantity = modelVar.maxQuantity;
        //                                currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                                currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                                currentStatus.currentGain = modelVar.currentGain;
        //                                currentStatus.baseQuantity = modelVar.baseQuantity;
        //                                currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                                currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                                currentStatus.doSuppTrades = model.doSuppTrades;
        //                                currentStatus.doShorts = model.doShorts;
        //                                currentStatus.doLongs = model.doLongs;
        //                                currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                                currentStatus.strategy = this.strategy;
        //                                currentStatus.resolution = this.resolution;
        //                                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                                //currentStatus.epicName = this.epicName;
        //                                //send log to the website
        //                                model.modelLogs.logs[0].epicName = this.epicName;
        //                                Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                                Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                                //save log to the database
        //                                Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                                await log.SaveDocument(logContainer);
        //                                model.modelLogs.logs = new List<ModelLog>();

        //                            }


        //                            // save the run details to ensure all picked up
        //                            tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;

        //                            TradingBrainSettings newTB = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);
        //                            newTB.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            await newTB.SaveDocument(the_app_db);


        //                        }
        //                        else
        //                        {
        //                            CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"No candle found for {_startTime}. ", "INFO");
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode_CASEY";
        //                log.Epic = this.epicName + "-" + this.resolution;
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        //try
        //        //{
        //        //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //        //    if (ret != null)
        //        //    {
        //        //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);

        //        //        if (ret.StatusCode.ToString() == "Forbidden")
        //        //        {
        //        //            // re connect to API

        //        //        }
        //        //    }



        //        latestHour = DateTime.UtcNow.Hour;
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    Log log = new Log(the_app_db);
        //        //    log.Log_Message = ex.ToString();
        //        //    log.Log_Type = "Error";
        //        //    log.Log_App = "RunCode";
        //        //    await log.Save();
        //        //}

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_CASEYC(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///

        //    AddStatusMessage($"Security token = {_igContainer.context.xSecurityToken}", "INFO");
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    MappedDiagnosticsLogicalContext.Set("jobId", this.logName);



        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    //
        //    //_igContainer.tbClient.ConnectToRest();

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    int minutes = dtNow.Minute;
        //    if (seconds == 59 && minutes == 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour + 1, 0, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0).AddMinutes(-min);
        //    }


        //        DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName, this.futures).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        //clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        //clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                            //model.modelVar.quantityMultiplier = 1;
        //                        }
        //                        else
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            //if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            //model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                            //if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
        //                        }

        //                        //tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        //tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;
        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;

        //                // turn on nightingale
        //                model.nightingaleOn = true;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                model.modelVar.baseQuantity = tb.lastRunVars.baseQuantity;
        //                model.modelVar.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                model.modelVar.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                model.modelVar.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                //model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                //modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;

        //                model.modelVar.winningBetMultiple = tb.lastRunVars.winningBetMultiple;
        //                model.modelVar.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                model.thisModel.strategy = strategy;
        //                model.thisModel.resolution = resolution;
        //                model.thisModel.epicName = epicName;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;
        //                currentStatus.baseQuantity = tb.lastRunVars.baseQuantity;
        //                currentStatus.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                currentStatus.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                currentStatus.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                currentStatus.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                currentStatus.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                currentStatus.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                currentStatus.winningBetMultiple = tb.lastRunVars.winningBetMultiple;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0, 3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                     thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);

        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceDataCASEYC(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true);


        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());

        //                    if (indIndex >= 0)
        //                    {
        //                        //model.quotes.rsiCandleLow = indCandles.Take(indIndex + 1).GetRsi(thisInput.var1).LastOrDefault();
        //                        //model.quotes.rsiCandleHigh = indCandles.Take(indIndex + 1).GetRsi(thisInput.var3).LastOrDefault();
        //                        model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                        model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                        model.quotes.atrCandleLow = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var10).LastOrDefault().Atr ?? 0;
        //                        model.quotes.atrCandleHigh = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var12).LastOrDefault().Atr ?? 0;

        //                        //model.quotes.rsiCandleHigh = new RsiResult(_startTime);
        //                        //model.quotes.rsiCandleLow = new RsiResult(_startTime);
        //                        //model.quotes.rsiCandleLow.Rsi = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var10).TakeLast(3).Average(s => s.Rsi);
        //                        //model.quotes.rsiCandleHigh.Rsi = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var12).TakeLast(3).Average(s => s.Rsi);

        //                        model.quotes.caseyC = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.var1, (int)thisInput.var1).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);
        //                        model.quotes.caseyCExit = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.var3, (int)thisInput.var3).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);

        //                        model.quotes.caseyCShort = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.svar1, (int)thisInput.svar1).Average(s => s.cRank[(int)thisInput.svar0 - 1].cRank);
        //                        model.quotes.caseyCExitShort = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.svar3, (int)thisInput.svar3).Average(s => s.cRank[(int)thisInput.svar0 - 1].cRank);

        //                        //model.quotes.caseyCAverage = await RSI_LoadPrices.GetCASEYCAverageClose(the_db, epicName, resolution, resMod,200,  _endTime);



        //                        int idx = (indIndex) - (int)thisInput.var7;
        //                        model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];
        //                        model.quotes.prevStdDevCandleShort = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.svar6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];

        //                        model.quotes.ema = (double)indCandles.Take(indIndex + 1).GetEma((int)thisInput.var14).LastOrDefault().Ema;
        //                        model.quotes.prevEma = (double)indCandles.Take(indIndex).GetEma((int)thisInput.var14).LastOrDefault().Ema;


        //                        // Check if we should be adding trades at this hour
        //                        bool doTrade = true;
        //                        int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                        hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                        if (tradeHour != null)
        //                        {
        //                            doTrade = tradeHour.trade;
        //                        }

        //                        if (model.onMarket || (!model.onMarket && doTrade && model.quotes.prevEma > 0))
        //                        {


        //                            CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           buyShort={model.buyShort},  sellShort={model.sellShort}, shortOnmarket={model.shortOnMarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           onMarket={model.onMarket}");
        //                            // clsCommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow.Rsi} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh.Rsi}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}, modelQuotes.prevStdDevCandleShort {model.quotes.prevStdDevCandleShort}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.caseyC = {model.quotes.caseyC}, modelQuotes.caseyCExit = {model.quotes.caseyCExit}");
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.caseyCShort = {model.quotes.caseyCShort}, modelQuotes.caseyCExitShort = {model.quotes.caseyCExitShort}");
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.ema:{model.quotes.ema} modelQuotes.prevEma:{model.quotes.prevEma}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                            // run the actual code 
        //                            model.RunProTrendCodeCASEYC(model.quotes);


        //                            CommonFunctions.AddStatusMessage($"values after run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           buyShort={model.buyShort},  sellShort={model.sellShort}, shortOnmarket={model.shortOnMarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           onMarket={model.onMarket}");
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                            CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"quantityMultiplier - {model.modelVar.quantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"WinningBetMultiple - {model.modelVar.winningBetMultiple}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxBetMultiple - {model.modelVar.maxBetMultiple}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"calcAvgWinningTrade - {model.modelVar.calcAvgWinningTrade}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"seedAvgWinningTrade - {model.modelVar.seedAvgWinningTrade}", "DEBUG", logName);


        //                            if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                            if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                            //model.sellShort = true;

        //                            if (param != "DEBUG")
        //                            {

        //                                //string thisDealRef = "";
        //                                //string dealType = "";
        //                                //bool dealSent = false;

        //                                //////////////////////////////////////////////////////////////////////////////////////////////
        //                                // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                                //////////////////////////////////////////////////////////////////////////////////////////////

        //                                //double currentStop = 0;
        //                                //double newStop = 0;
        //                                //double currentPrice = 0;



        //                                //////////////////////////////////////////////////////////////////

        //                                if (model.buyLong && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                    //model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, 0, this.igAccountId, 0);
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.sellLong)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }



        //                                if (model.sellShort && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("sellShort activated", "INFO", logName);
        //                                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                    //model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("short", model.modelVar.quantity, 0, this.igAccountId, 0);
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.buyShort)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("buyShort activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }


        //                            }
        //                            try
        //                            {
        //                                if (model.thisModel.currentTrade != null && (model.thisModel.currentTrade.purchaseDate != DateTime.MinValue || model.thisModel.currentTrade.sellDate != DateTime.MinValue ))
        //                                {
        //                                    model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                    await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                                }


        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                CommonFunctions.AddStatusMessage(ex.ToString(), "ERROR");
        //                                Log log = new Log(the_app_db);
        //                                log.Log_Message = ex.ToString();
        //                                log.Log_Type = "Error";
        //                                log.Log_App = "RunCode";
        //                                await log.Save();

        //                            }

        //                            //reset any deal variables that could have been placed by the RunCode
        //                            model.buyLong = false;
        //                            model.sellLong = false;
        //                            model.buyShort = false;
        //                            model.sellShort = false;


        //                            if (model.modelLogs.logs.Count() > 0)
        //                            {
        //                                ModelLog log = new ModelLog();
        //                                log = model.modelLogs.logs[0];
        //                                log.modelRunID = modelID;
        //                                log.runDate = _startTime;
        //                                log.id = System.Guid.NewGuid().ToString();
        //                                if (model.onMarket)
        //                                {
        //                                    currentStatus.onMarket = true;



        //                                    currentStatus.tradeType = model.thisModel.currentTrade.longShort;


        //                                    currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                    currentStatus.count = model.thisModel.currentTrade.count;

        //                                }
        //                                else
        //                                {
        //                                    currentStatus.onMarket = false;
        //                                    currentStatus.tradeType = "";
        //                                }

        //                                currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                                currentStatus.accountId = this.igAccountId;
        //                                currentStatus.startingQuantity = modelVar.startingQuantity;
        //                                currentStatus.minQuantity = modelVar.minQuantity;
        //                                currentStatus.maxQuantity = modelVar.maxQuantity;
        //                                currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                                currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                                currentStatus.currentGain = modelVar.currentGain;
        //                                currentStatus.baseQuantity = modelVar.baseQuantity;
        //                                currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                                currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                                currentStatus.doSuppTrades = model.doSuppTrades;
        //                                currentStatus.doShorts = model.doShorts;
        //                                currentStatus.doLongs = model.doLongs;
        //                                currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                                currentStatus.strategy = this.strategy;
        //                                currentStatus.resolution = this.resolution;
        //                                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                                //currentStatus.epicName = this.epicName;
        //                                //send log to the website
        //                                model.modelLogs.logs[0].epicName = this.epicName;
        //                                Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                                Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                                //save log to the database
        //                                Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                                await log.SaveDocument(logContainer);
        //                                model.modelLogs.logs = new List<ModelLog>();

        //                            }


        //                            // save the run details to ensure all picked up
        //                            tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;

        //                            TradingBrainSettings newTB = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);
        //                            newTB.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            newTB.lastRunVars.calcAvgWinningTrade = modelVar.calcAvgWinningTrade;
        //                            await newTB.SaveDocument(the_app_db);


        //                        }
        //                        else
        //                        {
        //                            CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"No candle found for {_startTime}. ","INFO");
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                CommonFunctions.AddStatusMessage(ex.ToString(), "ERROR");
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode_CASEY";
        //                log.Epic = this.epicName + "-" + this.resolution;
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        //try
        //        //{
        //        //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //        //    if (ret != null)
        //        //    {
        //        //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);

        //        //        if (ret.StatusCode.ToString() == "Forbidden")
        //        //        {
        //        //            // re connect to API

        //        //        }
        //        //    }



        //            latestHour = DateTime.UtcNow.Hour;
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    Log log = new Log(the_app_db);
        //        //    log.Log_Message = ex.ToString();
        //        //    log.Log_Type = "Error";
        //        //    log.Log_App = "RunCode";
        //        //    await log.Save();
        //        //}

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_CASEYCv2(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///

        //    AddStatusMessage($"Security token = {_igContainer.context.xSecurityToken}", "INFO");
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    //
        //    //_igContainer.tbClient.ConnectToRest();

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    int minutes = dtNow.Minute;
        //    if (seconds == 59 && minutes == 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour + 1, 0, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0).AddMinutes(-min);
        //    }


        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName, this.futures).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        //clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        //clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                            //model.modelVar.quantityMultiplier = 1;
        //                        }
        //                        else
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            //if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            //model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                            //if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
        //                        }

        //                        //tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        //tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;
        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;

        //                // turn on nightingale
        //                model.nightingaleOn = true;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                model.modelVar.baseQuantity = tb.lastRunVars.baseQuantity;
        //                model.modelVar.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                model.modelVar.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                model.modelVar.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                //model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                //modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;

        //                model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;

        //                model.modelVar.winningBetMultiple = tb.lastRunVars.winningBetMultiple;
        //                model.modelVar.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                model.thisModel.strategy = strategy;
        //                model.thisModel.resolution = resolution;
        //                model.thisModel.epicName = epicName;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;
        //                currentStatus.baseQuantity = tb.lastRunVars.baseQuantity;
        //                currentStatus.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                currentStatus.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                currentStatus.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                currentStatus.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                currentStatus.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                currentStatus.maxBetMultiple = tb.lastRunVars.maxBetMultiple;
        //                currentStatus.winningBetMultiple = tb.lastRunVars.winningBetMultiple;

        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0, 3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                    thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();

        //                    tbPrice dbPrice = new tbPrice();
        //                    dbPrice = obj.GetLastPriceByEndDate(the_db, minute_container, epicName, resolution, _endTime).Result;

        //                    if (dbPrice != null || dbPrice.endDate == _endTime)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Current candles taken from DB using end date {_endTime} : ", "INFO", logName);
        //                        model.quotes.currentCandle = dbPrice;
        //                    }
        //                    else {
        //                        CommonFunctions.AddStatusMessage("Getting current candles from API", "INFO", logName);
        //                        model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);
        //                    }

        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote>? indCandles = await RSI_LoadPrices.GetPriceDataCASEYC(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true)  ;

        //                    if (indCandles == null)
        //                    {
        //                        CommonFunctions.AddStatusMessage("No candles found from DB (indCandles = null)", "INFO", logName);
        //                        throw new InvalidOperationException(param + " - No candles found from DB (indCandles = null)");
        //                    }

        //                    AddStatusMessage("Getting Moving Averages quotes from indCandles.......");
        //                    int intI = 0;
        //                    //List<modQuote> newCandles = new List<modQuote>();
        //                    foreach (modQuote mq in indCandles)
        //                    {
        //                        if (intI >= 200)
        //                        {
        //                            int indIndex2 = indCandles.BinarySearch(new modQuote { Date = mq.Date }, new QuoteComparer());
        //                            for (int sma = 1; sma <= 200; sma += 1)
        //                            {

        //                                modQuote thisQuote = new modQuote();
        //                                thisQuote.Date = mq.Date;
        //                                thisQuote.High = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.High);
        //                                thisQuote.Low = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Low);
        //                                thisQuote.Close = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Close);
        //                                thisQuote.Open = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Open);
        //                                thisQuote.Typical = indCandles.GetRange(indIndex2 + 1 - sma, sma).Average(s => s.Typical);

        //                                //thisQuote.High = indCandles.TakeLast(  sma).Average(s => s.High);
        //                                //thisQuote.Low = indCandles.TakeLast(sma).Average(s => s.Low);
        //                                //thisQuote.Close = indCandles.TakeLast(sma).Average(s => s.Close);
        //                                //thisQuote.Open = indCandles.TakeLast(sma).Average(s => s.Open);
        //                                //thisQuote.Typical = indCandles.TakeLast(sma).Average(s => s.Typical);

        //                                thisQuote.index = sma;

        //                                mq.smaQuotes.Add(thisQuote);


        //                            }


        //                            //newCandles.Add(mq);
        //                        }

        //                        intI++;
        //                    }
        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());
        //                    // List<modQuote> seededList = indCandles.TakeLast(500).ToList();
        //                    if (indIndex >= 0  )
        //                    {

        //                        var stdDevResult = indCandles.GetStdDev(30).LastOrDefault();
        //                        model.quotes.stdDevLongCandle = stdDevResult != null && stdDevResult.StdDev != null ? stdDevResult.StdDev.Value : 0;

        //                        var atrCandleLowResult = indCandles.GetAtr((int)thisInput.var10).LastOrDefault();
        //                        model.quotes.atrCandleLow = atrCandleLowResult != null && atrCandleLowResult.Atr != null ? atrCandleLowResult.Atr.Value : 0;

        //                        var atrCandleHighResult = indCandles.GetAtr((int)thisInput.var12).LastOrDefault();
        //                        model.quotes.atrCandleHigh = atrCandleHighResult != null && atrCandleHighResult.Atr != null ? atrCandleHighResult.Atr.Value : 0;

        //                        var atrCandleLowShortResult = indCandles.GetAtr((int)thisInput.svar10).LastOrDefault();
        //                        model.quotes.atrCandleLowShort = atrCandleLowShortResult != null && atrCandleLowShortResult.Atr != null ? atrCandleLowShortResult.Atr.Value : 0;

        //                        var atrCandleHighShortResult = indCandles.GetAtr((int)thisInput.svar12).LastOrDefault();
        //                        model.quotes.atrCandleHighShort = atrCandleHighShortResult != null && atrCandleHighShortResult != null && atrCandleHighShortResult.Atr != null ? atrCandleHighShortResult.Atr.Value : 0;

        //                        model.quotes.caseyC = (double)indCandles.TakeLast( (int)thisInput.var1).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);
        //                        model.quotes.caseyCExit = (double)indCandles.TakeLast( (int)thisInput.var3).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);
        //                        model.quotes.caseyCShort = (double)indCandles.TakeLast( (int)thisInput.svar1).Average(s => s.cRank[(int)thisInput.svar0 - 1].cRank);
        //                        model.quotes.caseyCExitShort = (double)indCandles.TakeLast( (int)thisInput.svar3).Average(s => s.cRank[(int)thisInput.svar0 - 1].cRank);

        //                        model.quotes.sma = (double)indCandles.Last().smaQuotes[(int)thisInput.var14 - 1].Close;
        //                        model.quotes.prevSma = (double)indCandles.TakeLast(2).ToList()[0].smaQuotes[(int)thisInput.var14 - 1].Close;

        //                        int idx = 500 - (int)thisInput.var7 -1;
        //                        model.quotes.prevStdDevCandle = indCandles.GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0;  

        //                        idx = 500 - (int)thisInput.svar7 -1;
        //                        model.quotes.prevStdDevCandleShort = indCandles.GetStdDev((int)thisInput.svar6).ToList()[idx].StdDev ?? 0;  

        //                        // Check if we should be adding trades at this hour
        //                        bool doTrade = true;
        //                        //int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                        //hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                        //if (tradeHour != null)
        //                        //{
        //                        //    doTrade = tradeHour.trade;
        //                        //}

        //                        if (model.onMarket || (!model.onMarket && doTrade ))
        //                        {


        //                            CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           buyShort={model.buyShort},  sellShort={model.sellShort}, shortOnmarket={model.shortOnMarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           onMarket={model.onMarket}");



        //                            CommonFunctions.AddStatusMessage($"atrCandleLow:...........{model.quotes.atrCandleLow}");
        //                            CommonFunctions.AddStatusMessage($"atrCandleHigh:..........{model.quotes.atrCandleHigh}");
        //                            CommonFunctions.AddStatusMessage($"atrCandleLowShort:......{model.quotes.atrCandleLowShort}");
        //                            CommonFunctions.AddStatusMessage($"atrCandleHighShort:.....{model.quotes.atrCandleHighShort}");
        //                            CommonFunctions.AddStatusMessage($"caseyC:.................{model.quotes.caseyC}");
        //                            CommonFunctions.AddStatusMessage($"caseyCExit:.............{model.quotes.caseyCExit}");
        //                            CommonFunctions.AddStatusMessage($"caseyCShort:............{model.quotes.caseyCShort}");
        //                            CommonFunctions.AddStatusMessage($"caseyCExitShort:........{model.quotes.caseyCExitShort}");
        //                            CommonFunctions.AddStatusMessage($"sma:....................{model.quotes.sma}");
        //                            CommonFunctions.AddStatusMessage($"prevSma:................{model.quotes.prevSma}");
        //                            CommonFunctions.AddStatusMessage($"stdDevCandle:...........{model.quotes.stdDevCandle}");
        //                            CommonFunctions.AddStatusMessage($"stdDevLongCandle:.......{model.quotes.stdDevLongCandle}");
        //                            CommonFunctions.AddStatusMessage($"stdDevCandleShort:......{model.quotes.stdDevCandleShort}");
        //                            CommonFunctions.AddStatusMessage($"prevStdDevCandle:.......{model.quotes.prevStdDevCandle}");
        //                            CommonFunctions.AddStatusMessage($"prevStdDevCandleShort:..{model.quotes.prevStdDevCandleShort}");


        //                            // clsCommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow.Rsi} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh.Rsi}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow.Atr} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh.Atr}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle.StdDev} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle.StdDev}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle.StdDev}, modelQuotes.prevStdDevCandleShort {model.quotes.prevStdDevCandleShort.StdDev}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.caseyC = {model.quotes.caseyC}, modelQuotes.caseyCExit = {model.quotes.caseyCExit}");
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.caseyCShort = {model.quotes.caseyCShort}, modelQuotes.caseyCExitShort = {model.quotes.caseyCExitShort}");
        //                            //clsCommonFunctions.AddStatusMessage($"modelQuotes.ema:{model.quotes.ema} modelQuotes.prevEma:{model.quotes.prevEma}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                            // run the actual code 
        //                            model.RunProTrendCodeCASEYCv2(model.quotes);


        //                            CommonFunctions.AddStatusMessage($"values after run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           buyShort={model.buyShort},  sellShort={model.sellShort}, shortOnmarket={model.shortOnMarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"           onMarket={model.onMarket}");
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                            CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"quantityMultiplier - {model.modelVar.quantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"WinningBetMultiple - {model.modelVar.winningBetMultiple}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxBetMultiple - {model.modelVar.maxBetMultiple}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"calcAvgWinningTrade - {model.modelVar.calcAvgWinningTrade}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"seedAvgWinningTrade - {model.modelVar.seedAvgWinningTrade}", "DEBUG", logName);


        //                            if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                            if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                            //model.sellShort = true;

        //                            if (param != "DEBUG")
        //                            {

        //                                //string thisDealRef = "";
        //                                //string dealType = "";
        //                                //bool dealSent = false;

        //                                //////////////////////////////////////////////////////////////////////////////////////////////
        //                                // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                                //////////////////////////////////////////////////////////////////////////////////////////////

        //                                //double currentStop = 0;
        //                                //double newStop = 0;
        //                                //double currentPrice = 0;



        //                                //////////////////////////////////////////////////////////////////

        //                                if (model.buyLong && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                    //model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, 0, this.igAccountId, 0);
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.sellLong)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }



        //                                if (model.sellShort && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("sellShort activated", "INFO", logName);
        //                                    //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                    //model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("short", model.modelVar.quantity, 0, this.igAccountId, 0);
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.buyShort)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("buyShort activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }


        //                            }
        //                            try
        //                            {
        //                                if (model.thisModel.currentTrade != null && (model.thisModel.currentTrade.purchaseDate != DateTime.MinValue || model.thisModel.currentTrade.sellDate != DateTime.MinValue))
        //                                {
        //                                    model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                    await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                                }


        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                CommonFunctions.AddStatusMessage(ex.ToString(), "ERROR");
        //                                Log log = new Log(the_app_db);
        //                                log.Log_Message = ex.ToString();
        //                                log.Log_Type = "Error";
        //                                log.Log_App = "RunCode";
        //                                await log.Save();

        //                            }

        //                            //reset any deal variables that could have been placed by the RunCode
        //                            model.buyLong = false;
        //                            model.sellLong = false;
        //                            model.buyShort = false;
        //                            model.sellShort = false;


        //                            if (model.modelLogs.logs.Count() > 0)
        //                            {
        //                                ModelLog log = new ModelLog();
        //                                log = model.modelLogs.logs[0];
        //                                log.modelRunID = modelID;
        //                                log.runDate = _startTime;
        //                                log.id = System.Guid.NewGuid().ToString();
        //                                if (model.onMarket)
        //                                {
        //                                    currentStatus.onMarket = true;



        //                                    currentStatus.tradeType = model.thisModel.currentTrade.longShort;


        //                                    currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                    currentStatus.count = model.thisModel.currentTrade.count;

        //                                }
        //                                else
        //                                {
        //                                    currentStatus.onMarket = false;
        //                                    currentStatus.tradeType = "";
        //                                }

        //                                currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                                currentStatus.accountId = this.igAccountId;
        //                                currentStatus.startingQuantity = modelVar.startingQuantity;
        //                                currentStatus.minQuantity = modelVar.minQuantity;
        //                                currentStatus.maxQuantity = modelVar.maxQuantity;
        //                                currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                                currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                                currentStatus.currentGain = modelVar.currentGain;
        //                                currentStatus.baseQuantity = modelVar.baseQuantity;
        //                                currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                                currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                                currentStatus.doSuppTrades = model.doSuppTrades;
        //                                currentStatus.doShorts = model.doShorts;
        //                                currentStatus.doLongs = model.doLongs;
        //                                currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                                currentStatus.strategy = this.strategy;
        //                                currentStatus.resolution = this.resolution;
        //                                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                                //currentStatus.epicName = this.epicName;
        //                                //send log to the website
        //                                model.modelLogs.logs[0].epicName = this.epicName;
        //                                Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                                Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                                //save log to the database
        //                                Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                                await log.SaveDocument(logContainer);
        //                                model.modelLogs.logs = new List<ModelLog>();

        //                            }


        //                            // save the run details to ensure all picked up
        //                            tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;

        //                            TradingBrainSettings newTB = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);
        //                            newTB.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            newTB.lastRunVars.calcAvgWinningTrade = modelVar.calcAvgWinningTrade;
        //                            await newTB.SaveDocument(the_app_db);


        //                        }
        //                        else
        //                        {
        //                            //clsCommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"No candle found for {_startTime}. ", "INFO");
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                CommonFunctions.AddStatusMessage(ex.ToString(), "ERROR");
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode_CASEY";
        //                log.Epic = this.epicName + "-" + this.resolution;
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        //try
        //        //{
        //        //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //        //    if (ret != null)
        //        //    {
        //        //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);

        //        //        if (ret.StatusCode.ToString() == "Forbidden")
        //        //        {
        //        //            // re connect to API

        //        //        }
        //        //    }



        //        latestHour = DateTime.UtcNow.Hour;
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    Log log = new Log(the_app_db);
        //        //    log.Log_Message = ex.ToString();
        //        //    log.Log_Type = "Error";
        //        //    log.Log_App = "RunCode";
        //        //    await log.Save();
        //        //}

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_CASEYCSHORT(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///

        //    AddStatusMessage($"Security token = {_igContainer.context.xSecurityToken}", "INFO");
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    //
        //    //_igContainer.tbClient.ConnectToRest();

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    int minutes = dtNow.Minute;
        //    if (seconds == 59 && minutes == 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour + 1, 0, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0).AddMinutes(-min);
        //    }


        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);

        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.

        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName, this.futures).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            try
        //            {
        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);

        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        //clsCommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        //clsCommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                            model.modelVar.quantityMultiplier = 1;
        //                        }
        //                        else
        //                        {
        //                            //model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            //if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            //model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                            if (model.modelVar.quantityMultiplier == 1 && model.modelVar.carriedForwardLoss == 0) { model.modelVar.quantityMultiplier = 2; }
        //                        }

        //                        //tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        //tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        tb.lastRunVars.quantityMultiplier = model.modelVar.quantityMultiplier;
        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;

        //                // turn off nightingale
        //                model.nightingaleOn = false;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_RSI = this.tb.runDetails.inputs_RSI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                model.modelVar.baseQuantity = tb.lastRunVars.baseQuantity;
        //                model.modelVar.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                model.modelVar.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                model.modelVar.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                //model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                //modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                model.modelVar.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;
        //                currentStatus.baseQuantity = tb.lastRunVars.baseQuantity;
        //                currentStatus.gainMultiplier = tb.lastRunVars.gainMultiplier;
        //                currentStatus.maxQuantityMultiplier = tb.lastRunVars.maxQuantityMultiplier;
        //                currentStatus.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                currentStatus.quantityMultiplier = tb.lastRunVars.quantityMultiplier;
        //                currentStatus.numCandlesOnMarket = tb.lastRunVars.numCandlesOnMarket;
        //                modelInstanceInputs_RSI thisInput = new modelInstanceInputs_RSI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0, 3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                    thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }
        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadRSIv2(tb.runDetails.inputs_RSI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);

        //                    CommonFunctions.AddStatusMessage("Getting RSI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();

        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceDataCASEYC(the_db, epicName, resolution, resMod, _startTime, _endTime, strategy, true);


        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());

        //                    if (indIndex >= 0)
        //                    {
        //                        //model.quotes.rsiCandleLow = indCandles.Take(indIndex + 1).GetRsi(thisInput.var1).LastOrDefault();
        //                        //model.quotes.rsiCandleHigh = indCandles.Take(indIndex + 1).GetRsi(thisInput.var3).LastOrDefault();
        //                        model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                        model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                        model.quotes.atrCandleLow = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var10).LastOrDefault().Atr ?? 0;
        //                        model.quotes.atrCandleHigh = indCandles.Take(indIndex + 1).GetAtr((int)thisInput.var12).LastOrDefault().Atr ?? 0;

        //                        //model.quotes.rsiCandleHigh = new RsiResult(_startTime);
        //                        //model.quotes.rsiCandleLow = new RsiResult(_startTime);
        //                        model.quotes.rsiCandleLow = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var10).TakeLast(3).Average(s => s.Rsi);
        //                        model.quotes.rsiCandleHigh = (double)indCandles.Take(indIndex + 1).GetRsi((int)thisInput.var12).TakeLast(3).Average(s => s.Rsi);

        //                        model.quotes.caseyC = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.var1, (int)thisInput.var1).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);
        //                        model.quotes.caseyCExit = (double)indCandles.GetRange(indIndex + 1 - (int)thisInput.var3, (int)thisInput.var3).Average(s => s.cRank[(int)thisInput.var0 - 1].cRank);
        //                        //model.quotes.caseyCAverage = await RSI_LoadPrices.GetCASEYCAverageClose(the_db, epicName, resolution, resMod,200,  _endTime);

        //                        CommonFunctions.AddStatusMessage($"caseyC = {model.quotes.caseyC}, caseyCExit = {model.quotes.caseyCExit}, caseyAverage = {model.quotes.caseyCAverage}");

        //                        int idx = (indIndex) - (int)thisInput.var7;
        //                        model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev((int)thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];

        //                        // Check if we should be adding trades at this hour
        //                        bool doTrade = true;
        //                        int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                        hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                        if (tradeHour != null)
        //                        {
        //                            doTrade = tradeHour.trade;
        //                        }

        //                        if (model.onMarket || (!model.onMarket && doTrade))
        //                        {


        //                            CommonFunctions.AddStatusMessage($"values before run         - buyShort={model.buyShort},  sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.rsiCandleLow:{model.quotes.rsiCandleLow} modelQuotes.rsiCandleHigh:{model.quotes.rsiCandleHigh}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.atrCandleLow:{model.quotes.atrCandleLow} modelQuotes.atrCandleHigh:{model.quotes.atrCandleHigh}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");

        //                            // run the actual code 
        //                            model.RunProTrendCodeCASEYCSHORT(model.quotes);

        //                            CommonFunctions.AddStatusMessage($"values after  run        - buyShort={model.buyShort}, sellShort={model.sellShort},  shortOnMarket={model.shortOnMarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");
        //                            CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"quantityMultiplier - {model.modelVar.quantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                            //clsCommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                            CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);

        //                            if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                            if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                            //model.sellShort = true;

        //                            if (param != "DEBUG")
        //                            {

        //                                //string thisDealRef = "";
        //                               // string dealType = "";
        //                               // bool dealSent = false;

        //                                //////////////////////////////////////////////////////////////////////////////////////////////
        //                                // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                                //////////////////////////////////////////////////////////////////////////////////////////////

        //                                double currentStop = 0;
        //                                double newStop = 0;
        //                                double currentPrice = 0;

        //                                if (model.shortOnMarket && model.modelVar.breakEvenVar == 0)
        //                                {
        //                                    currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                    newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                    currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                    CommonFunctions.AddStatusMessage($"[SHORT] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                    CommonFunctions.AddStatusMessage($"[SHORT] Current stop > newStop = {currentStop > newStop},  currentPrice > newStop = {currentPrice > newStop}, currentPrice < currentStop {currentPrice < currentStop}  ", "DEBUG", logName);


        //                                    if (currentStop > newStop && currentPrice > newStop && currentPrice < currentStop)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling short because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now higher than the new stop.", the_app_db);
        //                                        model.buyShort = true;
        //                                    }

        //                                }

        //                                //////////////////////////////////////////////////////////////////

        //                                if (model.sellShort && this.currentTrade == null)
        //                                {
        //                                    CommonFunctions.AddStatusMessage("sellShort activated", "INFO", logName);
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);
        //                                    model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                    requestedTrade reqTrade = new requestedTrade();
        //                                    reqTrade.dealType = "POSITION";
        //                                    reqTrade.dealReference = await PlaceDeal("short", model.modelVar.quantity, model.stopLossVar, this.igAccountId, thisInput.profitTarget);
        //                                    requestedTrades.Add(reqTrade);

        //                                    //if (reqTrade.dealReference != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = reqTrade.dealReference;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}
        //                                }
        //                                else
        //                                {
        //                                    if (model.buyShort)
        //                                    {
        //                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "buyShort", the_app_db);
        //                                        CommonFunctions.AddStatusMessage("buyShort activated", "INFO");
        //                                        //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                        string dealRef = await CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                        //if (dealRef != "")
        //                                        //{
        //                                        //    //dealSent = true;
        //                                        //    thisDealRef = dealRef;
        //                                        //    //dealType = "PlaceDeal";
        //                                        //}

        //                                    }
        //                                }


        //                                if (model.shortOnMarket)
        //                                {

        //                                }



        //                            }
        //                            try
        //                            {
        //                                if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.purchaseDate != DateTime.MinValue)
        //                                {
        //                                    model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                    await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                                }

        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                Log log = new Log(the_app_db);
        //                                log.Log_Message = ex.ToString();
        //                                log.Log_Type = "Error";
        //                                log.Log_App = "RunCode";
        //                                await log.Save();

        //                            }

        //                            //reset any deal variables that could have been placed by the RunCode
        //                            model.buyShort = false;
        //                            model.sellShort = false;


        //                            if (model.modelLogs.logs.Count() > 0)
        //                            {
        //                                ModelLog log = new ModelLog();
        //                                log = model.modelLogs.logs[0];
        //                                log.modelRunID = modelID;
        //                                log.runDate = _startTime;
        //                                log.id = System.Guid.NewGuid().ToString();
        //                                if (model.onMarket)
        //                                {
        //                                    currentStatus.onMarket = true;

        //                                    currentStatus.tradeType = "Short";


        //                                    currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                    currentStatus.count = model.thisModel.currentTrade.count;

        //                                }
        //                                else
        //                                {
        //                                    currentStatus.onMarket = false;
        //                                    currentStatus.tradeType = "";
        //                                }

        //                                currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                                currentStatus.accountId = this.igAccountId;
        //                                currentStatus.startingQuantity = modelVar.startingQuantity;
        //                                currentStatus.minQuantity = modelVar.minQuantity;
        //                                currentStatus.maxQuantity = modelVar.maxQuantity;
        //                                currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                                currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                                currentStatus.currentGain = modelVar.currentGain;
        //                                currentStatus.baseQuantity = modelVar.baseQuantity;
        //                                currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                                currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                                currentStatus.doSuppTrades = model.doSuppTrades;
        //                                currentStatus.doShorts = model.doShorts;
        //                                currentStatus.doLongs = model.doLongs;
        //                                currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                                currentStatus.strategy = this.strategy;
        //                                currentStatus.resolution = this.resolution;
        //                                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                                //currentStatus.epicName = this.epicName;
        //                                //send log to the website
        //                                model.modelLogs.logs[0].epicName = this.epicName;
        //                                Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                                Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                                //save log to the database
        //                                Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                                await log.SaveDocument(logContainer);
        //                                model.modelLogs.logs = new List<ModelLog>();

        //                            }


        //                            // save the run details to ensure all picked up
        //                            tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;

        //                            TradingBrainSettings newTB = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);
        //                            newTB.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            await newTB.SaveDocument(the_app_db);


        //                        }
        //                        else
        //                        {
        //                            CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"No candle found for {_startTime}. ", "INFO");
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db);
        //                log.Log_Message = ex.ToString();
        //                log.Log_Type = "Error";
        //                log.Log_App = "RunCode_CASEYCSHORT";
        //                log.Epic = this.epicName + "-" + this.resolution;
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        //clsCommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        //try
        //        //{
        //        //    IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //        //    if (ret != null)
        //        //    {
        //        //        clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);

        //        //        if (ret.StatusCode.ToString() == "Forbidden")
        //        //        {
        //        //            // re connect to API

        //        //        }
        //        //    }



        //        latestHour = DateTime.UtcNow.Hour;
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    Log log = new Log(the_app_db);
        //        //    log.Log_Message = ex.ToString();
        //        //    log.Log_Type = "Error";
        //        //    log.Log_App = "RunCode";
        //        //    await log.Save();
        //        //}

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}
        //public async Task<RunRet> RunCode_REI(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    ///////////////////////////////
        //    // Run the RSI strategy code //
        //    ///////////////////////////////
        //    ///
        //    RunRet taskRet = new RunRet();
        //    this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
        //    ScopeContext.PushProperty("app", "TRADINGBRAIN/");
        //    ScopeContext.PushProperty("epic", this.epicName + "/");
        //    ScopeContext.PushProperty("strategy", strategy + "/");
        //    ScopeContext.PushProperty("resolution", resolution + "/");
        //    int resMod = 0;

        //    //bool liveMode = true;
        //    bool marketOpen = false;

        //    DateTime dtNow = DateTime.UtcNow;
        //    DateTime _startTime;

        //    int min = RSI_LoadPrices.GetMinsFromResolution(this.resolution).Result;
        //    // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
        //    int seconds = dtNow.Second;
        //    if (seconds <= 59)
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-min);
        //    }
        //    else
        //    {
        //        _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes((-min) + 1);
        //        //_startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, 0, 0);

        //    }

        //    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, 04, 0, 0) ;

        //    DateTime _endTime = _startTime.AddMinutes(min).AddMilliseconds(-1);


        //    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
        //    {
        //        int i = 0;
        //        i = Convert.ToInt16(resolution.Split("_")[1].ToString());
        //        resMod = _startTime.Hour % i;
        //    }

        //    //paused = true;


        //    if (!paused || paused && model.onMarket || paused && pausedAfterNGL && modelVar.carriedForwardLoss > 0)
        //    {
        //        // Check if the market is currently open. If it is not then skip till next time.
        //        //marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow);
        //        marketOpen = IGModels.clsCommonFunctions.IsTradingOpen(dtNow, model.exchangeClosedDates, this.epicName).Result;
        //        if (marketOpen)
        //        {
        //            _igContainer.tbClient.FirstConfirmUpdate = false;
        //            string param = "";

        //            //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Running code");
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Run Started ", "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Strategy   :- " + this.strategy, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Resolution :- " + this.resolution, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Account ID :- " + this.igAccountId, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" - Epic       :- " + this.epicName, "INFO", logName);
        //            CommonFunctions.AddStatusMessage(" ------------------", "INFO", logName);
        //            CommonFunctions.AddStatusMessage($"Start Time = {_startTime}", "DEBUG", logName);
        //            CommonFunctions.AddStatusMessage($"resMod = {resMod}", "DEBUG", logName);

        //            //var watch = new System.Diagnostics.Stopwatch();
        //            //var bigWatch = new System.Diagnostics.Stopwatch();
        //            //bigWatch.Start();
        //            try
        //            {
        //                //watch.Start();


        //                this.tb = await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution);

        //                CommonFunctions.AddStatusMessage($"lastTradeDeleted  = {lastTradeDeleted}", "DEBUG", logName);


        //                // If the trade has just been deleted then sort out the CFL

        //                if (lastTradeDeleted)
        //                {
        //                    try
        //                    {
        //                        CommonFunctions.AddStatusMessage($"original carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, original currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                        double nettPosition = lastTradeValue + lastTradeSuppValue;
        //                        CommonFunctions.AddStatusMessage($"lastTradeValue  = {lastTradeValue}, lastTradeSuppValue = {lastTradeSuppValue}, nett position = {nettPosition}", "DEBUG", logName);

        //                        if (nettPosition <= 0)
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + (double)Math.Abs(nettPosition);
        //                        }
        //                        else
        //                        {
        //                            model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss - (double)Math.Abs(nettPosition);
        //                            if (model.modelVar.carriedForwardLoss < 0) { model.modelVar.carriedForwardLoss = 0; }
        //                            model.modelVar.currentGain += Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
        //                        }

        //                        tb.lastRunVars.carriedForwardLoss = model.modelVar.carriedForwardLoss;
        //                        tb.lastRunVars.currentGain = model.modelVar.currentGain;
        //                        tb.lastRunVars.numCandlesOnMarket = 0;
        //                        //tb.lastRunVars.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                        // check to see if the trade just finished lost at max quantity, if so then we need to reset the vars
        //                        //clsCommonFunctions.AddStatusMessage($"checking if reset required - lastTradeMaxQuantity = {lastTradeMaxQuantity}", "DEBUG", logName);
        //                        //if (lastTradeMaxQuantity)
        //                        //{
        //                        //    clsCommonFunctions.AddStatusMessage($"old lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG", logName);
        //                        //    tb.lastRunVars.currentGain = Math.Max(tb.lastRunVars.currentGain - model.modelVar.carriedForwardLoss, 0);
        //                        //    tb.lastRunVars.carriedForwardLoss = 0;
        //                        //    tb.lastRunVars.quantity = tb.lastRunVars.minQuantity;
        //                        //    tb.lastRunVars.counter = 0;
        //                        //    tb.lastRunVars.maxQuantity = tb.lastRunVars.minQuantity * tb.lastRunVars.maxQuantityMultiplier;
        //                        //    model.modelVar.currentGain = tb.lastRunVars.currentGain;
        //                        //    model.modelVar.carriedForwardLoss = tb.lastRunVars.carriedForwardLoss;
        //                        //    model.modelVar.quantity = tb.lastRunVars.quantity;
        //                        //    model.modelVar.counter = tb.lastRunVars.counter;
        //                        //    model.modelVar.maxQuantity = tb.lastRunVars.maxQuantity;

        //                        //    clsCommonFunctions.AddStatusMessage($"new lastRunVars - currentGain = {tb.lastRunVars.currentGain}, carriedForwardLoss = {tb.lastRunVars.carriedForwardLoss}, quantity = {tb.lastRunVars.quantity}, counter = {tb.lastRunVars.counter}, maxQuantity={tb.lastRunVars.maxQuantity}", "DEBUG", logName);
        //                        //}
        //                        //await tb.SaveDocument(the_app_db);

        //                        CommonFunctions.AddStatusMessage($"new carriedForwardLoss  = {tb.lastRunVars.carriedForwardLoss}, new currentGain = {tb.lastRunVars.currentGain}", "DEBUG", logName);
        //                    }

        //                    catch (Exception ex)
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Sorting new CFL failed - {ex.ToString()}", "ERROR", logName);
        //                    }

        //                    lastTradeDeleted = false;
        //                    lastTradeValue = 0;
        //                    lastTradeSuppValue = 0;
        //                    lastTradeMaxQuantity = false;
        //                }
        //                //watch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - GetSettings - Time taken = " + watch.ElapsedMilliseconds);

        //                //Determine if we are to do long and or short trades
        //                model.doLongs = tb.doLongs;
        //                model.doShorts = tb.doShorts;
        //                model.doSuppTrades = tb.doSuppTrades;
        //                model.nightingaleOn = true;
        //                //tb.lastRunVars.doLongsVar = tb.doLongs;
        //                //tb.lastRunVars.doShortsVar = tb.doShorts;
        //                //tb.lastRunVars.doSuppTradesVar = tb.doSuppTrades;

        //                CommonFunctions.AddStatusMessage($"Do Supplementary trades = {model.doSuppTrades}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Long trades = {model.doLongs}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"Do Short trades = {model.doShorts}", "DEBUG", logName);
        //                CommonFunctions.AddStatusMessage($"nightingaleOn= {model.nightingaleOn}", "DEBUG", logName);

        //                model.thisModel.inputs_REI = this.tb.runDetails.inputs_REI.DeepCopy();
        //                model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                model.thisModel.matchProTrend = false;
        //                model.modelVar.counterVar = model.thisModel.counterVar;
        //                //model.modelVar = tb.lastRunVars;

        //                model.startTime = dtNow;
        //                model.modelRunID = modelID;
        //                if (model.modelVar.quantity == 0)
        //                {
        //                    model.modelVar.minQuantity = tb.runDetails.quantity;
        //                    model.modelVar.quantity = tb.runDetails.quantity;
        //                }

        //                //model.counterVar = tb.runDetails.counterVar;
        //                currentStatus.inputs_REI = tb.runDetails.inputs_REI.DeepCopy();
        //                currentStatus.countervar = Math.Max(this.tb.runDetails.counterVar, 1000);
        //                //currentStatus.quantity = model.modelVar.quantity;
        //                currentStatus.quantity = tb.lastRunVars.minQuantity;
        //                currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                currentStatus.strategy = this.strategy;
        //                currentStatus.resolution = this.resolution;

        //                modelInstanceInputs_REI thisInput = new modelInstanceInputs_REI();

        //                //bigWatch.Restart();


        //                /////////////////////////////////////////////////////////
        //                // using the candle time determine which inputs to use //
        //                /////////////////////////////////////////////////////////

        //                //double thisSpread = Math.Round(Math.Abs((double)currentTick.Offer - (double)currentTick.Bid), 1);
        //                double thisSpread = 0;
        //                if (this.epicName.Substring(0,3) == "IX." || this.epicName.Substring(0, 3) == "CS.")
        //                {
        //                    thisSpread = await Get_SpreadFromLastCandleRSI(the_db, minute_container, _endTime, resolution, epicName);
        //                }

        //                CommonFunctions.AddStatusMessage($"Spread = {thisSpread}", "INFO");
        //                thisInput = IGModels.clsCommonFunctions.GetInputsFromSpreadREI(tb.runDetails.inputs_REI, thisSpread);
        //                if (thisInput == null)
        //                {
        //                    CommonFunctions.AddStatusMessage($"No inputs found for spread = {thisSpread}", "ERROR", logName);
        //                }
        //                else
        //                {
        //                    //Create the current candle
        //                    // only create a new min record if we are in live
        //                    // 
        //                    // reset the start time to be now to ensure we are in the correct minute (sometimes the timer will run the code at 59.99 rather than at 00.00
        //                    // _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);
        //                    //ModelQuotes modelQuotes = new ModelQuotes(); rubb

        //                    model.quotes = new ModelQuotes();

        //                    bool createMinRecord = false; // liveMode;
        //                    if (model.region == "test") { createMinRecord = false; }

        //                    // Don't create a new candle for HOUR_2, HOUR_3 or HOUR_4 as it would have been created when HOUR was sorted.
        //                    // This means that for these candles, we need to run TB a little bit later than the HOUR candle to ensure all candles are created.
        //                    if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4") { createMinRecord = false; }

        //                    RSI_LoadPrices obj = new RSI_LoadPrices();
        //                    model.quotes.currentCandle = obj.LoadPrices(the_db, minute_container, epicName, resolution, _endTime, createMinRecord, _igContainer.igRestApiClient);



        //                    //modelInstanceInputs_RSI thisInput = clsCommonFunctions.GetInputsFromSpread_RSI(thisModel.inputs_RSI, thisCandle);

        //                    //Console.WriteLine(DateTime.Now.ToString("G") + "Getting rsi quotes from DB.......");
        //                    CommonFunctions.AddStatusMessage("Getting REI Quotes from DB", "INFO", logName);
        //                    List<modQuote> rsiQuotes = new List<modQuote>();
        //                    //DateTime startTime = DateTime.MinValue;
        //                    List<modQuote> indCandles = await RSI_LoadPrices.GetPriceData(the_db, epicName, resolution, resMod, _startTime, _endTime,strategy, true);



        //                    int indIndex = indCandles.BinarySearch(new modQuote { Date = _startTime }, new QuoteComparer());


        //                    model.quotes.rei = indCandles.Take(indIndex + 1).LastOrDefault().rei;
        //                    model.quotes.stdDevCandle = indCandles.Take(indIndex + 1).GetStdDev(thisInput.var6).LastOrDefault().StdDev ?? 0;
        //                    model.quotes.stdDevLongCandle = indCandles.Take(indIndex + 1).GetStdDev(30).LastOrDefault().StdDev ?? 0;
        //                    int idx = (indIndex) - thisInput.var7;
        //                    model.quotes.prevStdDevCandle = indCandles.Take(indIndex + 1).GetStdDev(thisInput.var6).ToList()[idx].StdDev ?? 0; //stdDevResults[idx];


        //                    //model.candles.currentCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime, epicName, minute_container, TicksContainer, false, createMinRecord, the_app_db, model.exchangeClosedDates);

        //                    //// Check to see if we have prev and prev2 candles already. If not (i.e. first run) then go get them.
        //                    //if (model.candles.prevCandle.candleStart == DateTime.MinValue)
        //                    //{
        //                    //    model.candles.prevCandle = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-1), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);

        //                    //}
        //                    //if (model.candles.prevCandle2.candleStart == DateTime.MinValue)
        //                    //{
        //                    //    model.candles.prevCandle2 = await CreateLiveCandle(the_db, thisInput.var1, thisInput.var3, thisInput.var2, thisInput.var13, _startTime.AddMinutes(-2), epicName, minute_container, TicksContainer, false, false, the_app_db, model.exchangeClosedDates);
        //                    //}

        //                    //DateTime getStartDate = await model.getPrevMAStartDate(model.candles.currentCandle.candleStart);

        //                    //IG_Epic epic = new IG_Epic(epicName);
        //                    //clsMinuteCandle prevMa = await Get_MinuteCandle(the_db, minute_container, epic, getStartDate);
        //                    //model.candles.prevMACandle.mA30MinTypicalLongClose = prevMa.MovingAverages30Min[thisInput.var3 - 1].movingAverage.Close;
        //                    //model.candles.prevMACandle.mA30MinTypicalShortClose = prevMa.MovingAverages30Min[thisInput.var13 - 1].movingAverage.Close;


        //                    // Check if we should be adding trades at this hour
        //                    bool doTrade = true;
        //                    int currentHour = model.quotes.currentCandle.endDate.AddMinutes(1).Hour;
        //                    hourToTrade tradeHour = modelVar.hoursToTrade.FirstOrDefault(o => o.hour == currentHour);
        //                    if (tradeHour != null)
        //                    {
        //                        doTrade = tradeHour.trade;
        //                    }

        //                    if (model.onMarket || (!model.onMarket && doTrade))
        //                    {


        //                        CommonFunctions.AddStatusMessage($"values before run         - buyLong={model.buyLong},  sellLong={model.sellLong}, longOnmarket={model.longOnmarket},   onMarket={model.onMarket}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.rei:{model.quotes.rei} ", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"modelQuotes.stdDevCandle:{model.quotes.stdDevCandle} modelQuotes.stdDevLongCandle:{model.quotes.stdDevLongCandle}  modelQuotes.prevStdDevCandle {model.quotes.prevStdDevCandle}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {model.modelVar.numCandlesOnMarket}", "INFO");


        //                        //model.RunProTrendCodeV2(model.candles);
        //                        model.RunProTrendCodeREI(model.quotes);

        //                        CommonFunctions.AddStatusMessage($"values after  run        - buyLong={model.buyLong}, sellLong={model.sellLong},  longOnmarket={model.longOnmarket},  onMarket={model.onMarket}", "DEBUG", logName);
        //                        //clsCommonFunctions.AddStatusMessage($"values after  run ctd... - doSuppTrades={model.doSuppTrades}, onSuppTrade={model.onSuppTrade}", "DEBUG");
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket: {modelVar.numCandlesOnMarket}", "INFO");

        //                        CommonFunctions.AddStatusMessage($"Model vars - ", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"baseQuantity - {model.modelVar.baseQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"startingQuantity - {model.modelVar.startingQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"currentGain - {model.modelVar.currentGain}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"gainMultiplier - {model.modelVar.gainMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantityMultiplier - {model.modelVar.maxQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"maxQuantity - {model.modelVar.maxQuantity}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"carriedForwardloss - {model.modelVar.carriedForwardLoss}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppQuantityMultiplier - {model.modelVar.suppQuantityMultiplier}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"suppStopPercentage - {model.modelVar.suppStopPercentage}", "DEBUG", logName);
        //                        CommonFunctions.AddStatusMessage($"numCandlesOnMarket - {model.modelVar.numCandlesOnMarket}", "DEBUG", logName);

        //                        if (this.currentTrade != null) { CommonFunctions.AddStatusMessage(" current dealid = " + this.currentTrade.dealId, "INFO", logName); }
        //                        if (this.suppTrade != null) { CommonFunctions.AddStatusMessage(" current supp dealid = " + this.suppTrade.dealId, "INFO", logName); }

        //                        //model.sellShort = true;

        //                        if (param != "DEBUG")
        //                        {

        //                            //string thisDealRef = "";
        //                            //string dealType = "";
        //                            //bool dealSent = false;

        //                            //////////////////////////////////////////////////////////////////////////////////////////////
        //                            // Check for changes to stop limit that would mean the current trade has to end immediately //
        //                            //////////////////////////////////////////////////////////////////////////////////////////////

        //                            double currentStop = 0;
        //                            double newStop = 0;
        //                            double currentPrice = 0;

        //                            if (model.longOnmarket && model.modelVar.breakEvenVar == 0)
        //                            {
        //                                currentStop = IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue));
        //                                newStop = IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel);
        //                                currentPrice = IGModels.clsCommonFunctions.Dbl2DP((double)model.quotes.currentCandle.closePrice.ask);

        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop {currentStop} - newStop  {newStop} - CurrentPrice {currentPrice}  ", "DEBUG", logName);
        //                                CommonFunctions.AddStatusMessage($"[LONG] Current stop < newStop = {currentStop < newStop},  currentPrice < newStop = {currentPrice < newStop}, currentPrice > currentStop {currentPrice > currentStop}  ", "DEBUG", logName);


        //                                if (currentStop < newStop && currentPrice < newStop && currentPrice > currentStop)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Selling long because the original stop price : " + currentStop + " has changed to : " + newStop + " and the current price : + " + currentPrice + " is now lower than the new stop.", the_app_db);
        //                                    model.sellLong = true;
        //                                }

        //                            }

        //                            //////////////////////////////////////////////////////////////////

        //                            if (model.buyLong && this.currentTrade == null)
        //                            {
        //                                CommonFunctions.AddStatusMessage("BuyLong activated", "INFO", logName);
        //                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyLong", the_app_db);
        //                                model.stopLossVar = thisInput.stopLoss;// (double)thisInput.var4 * Math.Abs((double)targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);

        //                                requestedTrade reqTrade = new requestedTrade();
        //                                reqTrade.dealType = "POSITION";
        //                                reqTrade.dealReference = await PlaceDeal("long", model.modelVar.quantity, model.stopLossVar, this.igAccountId, thisInput.profitTarget);
        //                                requestedTrades.Add(reqTrade);


        //                                //if (reqTrade.dealReference != "")
        //                                //{
        //                                //    //dealSent = true;
        //                                //    thisDealRef = reqTrade.dealReference;
        //                                //    //dealType = "PlaceDeal";
        //                                //}
        //                            }
        //                            else
        //                            {
        //                                if (model.sellLong)
        //                                {
        //                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
        //                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO");
        //                                    //CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId);
        //                                    string dealRef = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
        //                                    //if (dealRef != "")
        //                                    //{
        //                                    //    //dealSent = true;
        //                                    //    thisDealRef = dealRef;
        //                                    //    //dealType = "PlaceDeal";
        //                                    //}

        //                                }
        //                            }


        //                            if (model.longOnmarket)
        //                            {

        //                                //Don't touch the stop level as it should be done by trailing stops instead

        //                                //Also, if we put this back in, the edit deal function is not sending the limit level (target) so it is being overwritten.


        //                                //clsCommonFunctions.AddStatusMessage($"[LONG] Check if buyprice ({model.thisModel.currentTrade.buyPrice}) - stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

        //                                //if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
        //                                //{



        //                                //    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
        //                                //    decimal? currentStopLevel = this.currentTrade.stopLevel;


        //                                //    this.currentTrade.stopLevel = (decimal)model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
        //                                //    clsCommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
        //                                //    TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
        //                                //    EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue);



        //                                //}
        //                            }



        //                        }
        //                        try
        //                        {
        //                            if (model.thisModel.currentTrade != null)
        //                            {
        //                                model.thisModel.currentTrade.numCandlesOnMarket = model.modelVar.numCandlesOnMarket;

        //                                await model.thisModel.currentTrade.SaveDocument(this.trade_container);

        //                            }

        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Log log = new Log(the_app_db);
        //                            log.Log_Message = ex.ToString();
        //                            log.Log_Type = "Error";
        //                            log.Log_App = "RunCode";
        //                            await log.Save();

        //                        }

        //                        //reset any deal variables that could have been placed by the RunCode
        //                        model.buyLong = false;
        //                        model.sellLong = false;


        //                        if (model.modelLogs.logs.Count() > 0)
        //                        {
        //                            ModelLog log = new ModelLog();
        //                            log = model.modelLogs.logs[0];
        //                            log.modelRunID = modelID;
        //                            log.runDate = _startTime;
        //                            log.id = System.Guid.NewGuid().ToString();
        //                            if (model.onMarket)
        //                            {
        //                                currentStatus.onMarket = true;

        //                                currentStatus.tradeType = "Long";


        //                                currentStatus.target = model.thisModel.currentTrade.targetPrice;
        //                                currentStatus.count = model.thisModel.currentTrade.count;

        //                            }
        //                            else
        //                            {
        //                                currentStatus.onMarket = false;
        //                                currentStatus.tradeType = "";
        //                            }

        //                            currentStatus.carriedForwardLoss = modelVar.carriedForwardLoss;
        //                            currentStatus.accountId = this.igAccountId;
        //                            currentStatus.startingQuantity = modelVar.startingQuantity;
        //                            currentStatus.minQuantity = modelVar.minQuantity;
        //                            currentStatus.maxQuantity = modelVar.maxQuantity;
        //                            currentStatus.gainMultiplier = modelVar.gainMultiplier;
        //                            currentStatus.maxQuantityMultiplier = modelVar.maxQuantityMultiplier;
        //                            currentStatus.currentGain = modelVar.currentGain;
        //                            currentStatus.baseQuantity = modelVar.baseQuantity;
        //                            currentStatus.suppQuantityMultiplier = modelVar.suppQuantityMultiplier;
        //                            currentStatus.suppStopPercentage = modelVar.suppStopPercentage;
        //                            currentStatus.doSuppTrades = model.doSuppTrades;
        //                            currentStatus.doShorts = model.doShorts;
        //                            currentStatus.doLongs = model.doLongs;
        //                            currentStatus.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                            currentStatus.strategy = this.strategy;
        //                            currentStatus.resolution = this.resolution;
        //                            currentStatus.hoursToTrade = tb.lastRunVars.hoursToTrade;
        //                            //currentStatus.epicName = this.epicName;
        //                            //send log to the website
        //                            model.modelLogs.logs[0].epicName = this.epicName;
        //                            Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0]), the_app_db));
        //                            Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus), the_app_db));
        //                            //save log to the database
        //                            Container logContainer = the_app_db.GetContainer("ModelLogs");
        //                            await log.SaveDocument(logContainer);
        //                            model.modelLogs.logs = new List<ModelLog>();

        //                        }


        //                        // save the run details to ensure all picked up
        //                        tb.lastRunVars.numCandlesOnMarket = modelVar.numCandlesOnMarket;
        //                        await tb.SaveDocument(the_app_db);

        //                    }
        //                    else
        //                    {
        //                        CommonFunctions.AddStatusMessage($"Not doing trades for hour {currentHour}", "INFO", logName);
        //                    }
        //                }
        //                _startTime = _startTime.AddMinutes(1);
        //                //bigWatch.Stop();
        //                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - Completed run - Time taken = " + bigWatch.ElapsedMilliseconds);




        //            }
        //            catch (Exception ex)
        //            {
        //                Log log = new Log(the_app_db)
        //                {
        //                    Log_Message = ex.ToString(),
        //                    Log_Type = "Error",
        //                    Log_App = "RunCode"
        //                };
        //                await log.Save();
        //            }

        //            //bigWatch.Stop();
        //            //clsCommonFunctions.AddStatusMessage("Completed run - Time taken = " + bigWatch.ElapsedMilliseconds, "INFO", logName);
        //            CommonFunctions.AddStatusMessage("Completed run ", "INFO", logName);

        //            // call the accounts api each hour just so we ensure the tokens don't expire
        //            //clsCommonFunctions.AddStatusMessage($"Current hour - {DateTime.UtcNow.Hour}, Last hour = {latestHour}", "INFO") ;
        //        }
        //        else
        //        {
        //            CommonFunctions.AddStatusMessage("Trading not currently open", "INFO", logName);
        //        }
        //    }
        //    else
        //    {
        //        CommonFunctions.AddStatusMessage("Trading brain paused...", "INFO", logName);
        //        pausedAfterNGL = false;
        //    }

        //    if (latestHour != DateTime.UtcNow.Hour)
        //    {
        //        CommonFunctions.AddStatusMessage("Hour has changed so call the AccountDetails API to ensure token doesn't expire", "INFO", logName);
        //        try
        //        {
        //            IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = await _igContainer.igRestApiClient.accountBalance();
        //            if (ret != null)
        //            {
        //                CommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO", logName);
        //            }
        //            latestHour = DateTime.UtcNow.Hour;
        //        }
        //        catch (Exception ex)
        //        {
        //            Log log = new Log(the_app_db);
        //            log.Log_Message = ex.ToString();
        //            log.Log_Type = "Error";
        //            log.Log_App = "RunCode";
        //            await log.Save();
        //        }

        //    }

        //    //if (liveMode)
        //    //{

        //    //    ti.Interval = GetIntervalWithResolution(this.resolution);
        //    //    ti.Start();
        //    //}

        //    return taskRet;
        //}

        public async Task<int> WaitForChanges()
        {
            int ret = 1;
            try
            {
                DateTime dtStart = DateTime.UtcNow;
                DateTime dtMax = dtStart.AddSeconds(20);

                while (DateTime.UtcNow < dtMax || 1 == 1)
                {
                    //System.Threading.Thread.Sleep(1000);
                    await Task.Delay(1000);
                    DateTime dtNow = DateTime.UtcNow;
                    //clsCommonFunctions.AddStatusMessage(dtNow.ToString("o") + " Sleeping....");
                }

                // Unsubscriber. Commented out for now but may need to add later.

                //UnsubscribeFromWatchlistInstruments();
                //UnsubscribefromTradeSubscription();
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "WaitForChanges"
                };
                await log.Save();
            }
            return ret;

        }
        public async void setupMessaging()
        {

            try
            {
                if (this.the_app_db == null)
                {
                    throw new InvalidOperationException("Database connection not set for messaging");
                }

                string strat = this.strategy.Replace("-", "_");
                string resol = "";
                if (this.resolution != "" && this.strategy != "SMA")
                {
                    resol = "-" + this.resolution;
                }
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
                CommonFunctions.AddStatusMessage("Starting messaging - TradingBrain-" + this.epicName + "-" + this.igAccountId + "-" + strat + resol, "INFO", logName);
                hubConnection = new HubConnectionBuilder()
                    .WithUrl(url, (HttpConnectionOptions options) => options.Headers.Add("userid", "TradingBrain-" + this.epicName + "-" + this.igAccountId + "-" + strat + resol))
                     .WithAutomaticReconnect()
                    .Build();

                hubConnection.Closed += async (error) =>
                {
                    if (error != null)
                    {
                        CommonFunctions.AddStatusMessage("Messaging connection errored - " + error.ToString(), "ERROR", logName);
                        CommonFunctions.SaveLog("Error", "Message Connection", error.ToString(), this.the_app_db);
                        await Task.Delay(new Random().Next(0, 5) * 1000);
                        await hubConnection.StartAsync();
                    }
                };


                hubConnection.On<string>("newMessage", async (message) =>

                {
                    message obj = JsonConvert.DeserializeObject<message>(message) ?? new message();
                    switch (obj.messageType)
                    {
                        case "Ping":
                            //clientMessage msg = JsonConvert.DeserializeObject<clientMessage>(obj.messageValue);
                            PingMessage msg = new PingMessage
                            {
                                epicName = this.epicName
                            };

                            CommonFunctions.SendMessage(obj.messageValue, "Ping", JsonConvert.SerializeObject(msg), the_app_db);
                            break;

                        case "Status":
                            //AddStatusMessage($"status sent - {currentStatus.epicName}-{currentStatus.strategy}", "INFO");
                            CommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus), the_app_db);
                            break;

                        case "Pause":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("Pause request received", "INFO", logName);

                            paused = true;
                            pausedAfterNGL = false;
                            if (currentStatus != null && model != null)
                            {
                                if (model.onMarket)
                                {
                                    currentStatus.status = "deferred pause (after current trade)";
                                }
                                else
                                {
                                    currentStatus.status = "paused";
                                }
                            }
                            Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                            break;

                        case "PauseAfterNGL":
                            CommonFunctions.AddStatusMessage("PauseAfterNGL request received", "INFO", logName);
                            // Pause TB once CFL = 0
                            paused = true;
                            pausedAfterNGL = true;
                            if (currentStatus != null)
                            {
                                currentStatus.status = "deferred pause (after nightingale success)";
                                Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                            }
                            break;

                        case "Stop":
                            // Stop TB (pause) and close any open trades
                            Task taskC = Task.Run(() => CommonFunctions.AddStatusMessage("Stop request received", "INFO", logName));
                            if (this.currentTrade == null)
                            {
                                throw new InvalidOperationException("No current trade to stop");
                            }
                            //if (this.currentTrade.size == null)
                            //{
                            //    throw new InvalidOperationException("No current trade size to stop");
                            //}
                            if (this.currentTrade != null && model != null && currentStatus != null)
                            {
                                paused = true;
                                pausedAfterNGL = false;
                                if (model.longOnmarket)
                                {
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong - Immediate stop activated", the_app_db);
                                    CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
                                    _ = CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);

                                }
                                else
                                {
                                    if (model.shortOnMarket)
                                    {
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort - Immediate stop activated", the_app_db);
                                        CommonFunctions.AddStatusMessage("BuyShort activated", "INFO", logName);
                                        _ = CloseDeal("short", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);

                                    }
                                }

                                currentStatus.status = "paused";
                                Task taskD = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                            }
                            break;

                        case "Resume":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("Resume request received", "INFO", logName);
                            paused = false;
                            pausedAfterNGL = false;
                            if (currentStatus != null)
                            {
                                currentStatus.status = "running";
                                Task taskE = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                            }
                            break;

                        case "ChangeQuantity":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("ChangeQuantity request received", "INFO", logName);
                            if (obj.messageValue != "" && currentStatus != null && model != null && tb != null)
                            {
                                ModelVarsChange? newVars = new ModelVarsChange();
                                newVars = JsonConvert.DeserializeObject<ModelVarsChange>(obj.messageValue) ?? new ModelVarsChange();

                                if (newVars.baseQuantity > 0)
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity to use = " + newVars.baseQuantity, "INFO", logName);
                                    tb.lastRunVars.baseQuantity = newVars.baseQuantity;
                                    tb.lastRunVars.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    tb.lastRunVars.minQuantity = newVars.baseQuantity;
                                    model.modelVar.baseQuantity = newVars.baseQuantity;
                                    model.modelVar.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier; ;
                                    model.modelVar.minQuantity = newVars.baseQuantity;
                                    currentStatus.quantity = newVars.baseQuantity;
                                    currentStatus.baseQuantity = newVars.baseQuantity;
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity is 0", "ERROR", logName);
                                }

                                if (newVars.maxQuantityMultiplier > 0)
                                {
                                    CommonFunctions.AddStatusMessage("New maxQuantityMultiplier to use = " + newVars.maxQuantityMultiplier, "INFO", logName);
                                    tb.lastRunVars.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                                    model.modelVar.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                                    currentStatus.maxQuantityMultiplier = newVars.maxQuantityMultiplier;
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("New maxQuantityMultiplier is 0", "ERROR", logName);
                                }

                                //if (newVars.carriedForwardLoss > 0)
                                //{
                                CommonFunctions.AddStatusMessage("New carriedForwardLoss to use = " + newVars.carriedForwardLoss, "INFO", logName);
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
                                CommonFunctions.AddStatusMessage("New currentGain to use = " + newVars.currentGain, "INFO", logName);
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
                                    CommonFunctions.AddStatusMessage("New gainMultiplier to use = " + newVars.gainMultiplier, "INFO", logName);
                                    tb.lastRunVars.gainMultiplier = newVars.gainMultiplier;
                                    model.modelVar.gainMultiplier = newVars.gainMultiplier;
                                    currentStatus.gainMultiplier = newVars.gainMultiplier;
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("New gainMultiplier is 0", "ERROR", logName);
                                }

                                CommonFunctions.AddStatusMessage("New seedAvgWinningTrade to use = " + newVars.seedAvgWinningTrade, "INFO", logName);
                                tb.lastRunVars.seedAvgWinningTrade = newVars.seedAvgWinningTrade;
                                model.modelVar.seedAvgWinningTrade = newVars.seedAvgWinningTrade;
                                currentStatus.seedAvgWinningTrade = newVars.seedAvgWinningTrade;



                                //if (newVars.suppQuantityMultiplier > 0)
                                //{
                                //    clsCommonFunctions.AddStatusMessage("New suppQuantityMultiplier to use = " + newVars.suppQuantityMultiplier, "INFO", logName);
                                //    tb.lastRunVars.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                                //    model.modelVar.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                                //    currentStatus.suppQuantityMultiplier = newVars.suppQuantityMultiplier;
                                //}
                                //else
                                //{
                                //    clsCommonFunctions.AddStatusMessage("New suppQuantityMultiplier is 0", "ERROR", logName);
                                //}

                                //if (newVars.suppStopPercentage > 0)
                                //{
                                //    clsCommonFunctions.AddStatusMessage("New suppStopPercentage to use = " + newVars.suppStopPercentage, "INFO", logName);
                                //    tb.lastRunVars.suppStopPercentage = newVars.suppStopPercentage;
                                //    model.modelVar.suppStopPercentage = newVars.suppStopPercentage;
                                //    currentStatus.suppStopPercentage = newVars.suppStopPercentage;
                                //}
                                //else
                                //{
                                //    clsCommonFunctions.AddStatusMessage("New suppStopPercentage is 0", "ERROR", logName);
                                //}

                                //clsCommonFunctions.AddStatusMessage("New doSuppTrades to use = " + newVars.doSuppTradesVar, "INFO", logName);

                                //model.doSuppTrades = newVars.doSuppTradesVar;
                                //currentStatus.doSuppTrades = newVars.doSuppTradesVar;
                                //tb.doSuppTrades = newVars.doSuppTradesVar;

                                //clsCommonFunctions.AddStatusMessage("New doLongs to use = " + newVars.doLongsVar, "INFO", logName);

                                //model.doLongs = newVars.doLongsVar;
                                //currentStatus.doLongs = newVars.doLongsVar;
                                //tb.doLongs = newVars.doLongsVar;

                                //clsCommonFunctions.AddStatusMessage("New doShorts to use = " + newVars.doShortsVar, "INFO", logName);

                                //model.doShorts = newVars.doShortsVar;
                                //currentStatus.doShorts = newVars.doShortsVar;
                                //tb.doShorts = newVars.doShortsVar;

                                // Save the last run vars into the TB settings table
                                Task<bool> res = tb.SaveDocument(the_app_db);
                                Task taskF = Task.Run(() => CommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus)));
                                CommonFunctions.AddStatusMessage("New values saved", "INFO", logName);
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
                        case "ChangeQuantityRSI":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("ChangeQuantityRSI request received", "INFO", logName);
                            if (obj.messageValue != "" && tb != null && model != null && currentStatus != null)
                            {
                                ModelVarsChange? newVars = new ModelVarsChange();
                                newVars = JsonConvert.DeserializeObject<ModelVarsChange>(obj.messageValue) ?? new ModelVarsChange();

                                if (newVars.baseQuantity > 0)
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity to use = " + newVars.baseQuantity, "INFO", logName);
                                    tb.lastRunVars.baseQuantity = newVars.baseQuantity;
                                    tb.lastRunVars.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    tb.lastRunVars.minQuantity = newVars.baseQuantity;
                                    model.modelVar.baseQuantity = newVars.baseQuantity;
                                    model.modelVar.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier; ;
                                    model.modelVar.minQuantity = newVars.baseQuantity;
                                    currentStatus.quantity = newVars.baseQuantity;
                                    currentStatus.baseQuantity = newVars.baseQuantity;
                                    currentStatus.minQuantity = newVars.baseQuantity;
                                    if (this.strategy == "BOLLI")
                                    {
                                        tb.lastRunVars.quantity = newVars.baseQuantity;
                                        model.modelVar.quantity = newVars.baseQuantity;
                                        tb.lastRunVars.startingQuantity = newVars.baseQuantity;
                                        model.modelVar.startingQuantity = newVars.baseQuantity;
                                        currentStatus.startingQuantity = newVars.baseQuantity;
                                    }
                                }
                                else
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity is 0", "ERROR", logName);
                                }

                                CommonFunctions.AddStatusMessage("New carriedForwardLoss to use = " + newVars.carriedForwardLoss, "INFO", logName);
                                tb.lastRunVars.carriedForwardLoss = newVars.carriedForwardLoss;
                                model.modelVar.carriedForwardLoss = newVars.carriedForwardLoss;
                                currentStatus.carriedForwardLoss = newVars.carriedForwardLoss;

                                CommonFunctions.AddStatusMessage("New currentGain to use = " + newVars.currentGain, "INFO", logName);
                                tb.lastRunVars.currentGain = newVars.currentGain;
                                model.modelVar.currentGain = newVars.currentGain;
                                currentStatus.currentGain = newVars.currentGain;

                                //if (newVars.hoursToTrade != null)
                                //{
                                //    if (newVars.hoursToTrade.Count == 24)
                                //    {
                                //        CommonFunctions.AddStatusMessage("New hoursToTrade to use -", "INFO", logName);
                                //        for (int i = 0; i < newVars.hoursToTrade.Count; i++)
                                //        {
                                //            CommonFunctions.AddStatusMessage("Hour " + i + " = " + newVars.hoursToTrade[i].trade, "INFO", logName);
                                //        }
                                //        tb.lastRunVars.hoursToTrade = newVars.hoursToTrade;
                                //        modelVar.hoursToTrade = newVars.hoursToTrade;
                                //        currentStatus.hoursToTrade = newVars.hoursToTrade;
                                //    }
                                //    else
                                //    {
                                //        CommonFunctions.AddStatusMessage("New hoursToTrade is less than 24", "ERROR", logName);
                                //    }
                                //}
                                //else
                                //{
                                //    CommonFunctions.AddStatusMessage("New hoursToTrade is null", "ERROR", logName);
                                //}

                                if (this.strategy == "BOLLI")
                                {
                                    if (newVars.var1 > 0 || newVars.var2 > 0 || newVars.var3 > 0 || newVars.var4 > 0 || newVars.var5 > 0 || newVars.var6 > 0)
                                    {
                                        // Get the input settings from the last run optimzerundata

                                        OptimizeRunData optData = await IGModels.clsCommonFunctions.GetLatestOptimizeRunData_RSI(the_app_db, this.epicName, 0, this.strategy, this.resolution);

                                        if (newVars.var1 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var1 to use = " + newVars.var1, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var1 = newVars.var1;
                                        }
                                        if (newVars.var2 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var2 to use = " + newVars.var2, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var2 = newVars.var2;
                                        }
                                        if (newVars.var3 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var3 to use = " + newVars.var3, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var3 = newVars.var3;
                                        }
                                        if (newVars.var4 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var4 to use = " + newVars.var4, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var4 = newVars.var4;
                                        }
                                        if (newVars.var5 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var5 to use = " + newVars.var5, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var5 = newVars.var5;
                                        }
                                        if (newVars.var6 > 0)
                                        {
                                            CommonFunctions.AddStatusMessage("New var6 to use = " + newVars.var6, "INFO", logName);
                                            tb.runDetails.inputs_RSI[0].var6 = newVars.var6;
                                        }

                                        Container optContainer = the_app_db.GetContainer("OptimizeRunData");
                                        optData.inputs_RSI = tb.runDetails.inputs_RSI.DeepCopy();
                                        await optData.SaveDocument(the_app_db, optContainer);

                                    }
                                }

                                // Save the last run vars into the TB settings table
                                if (the_app_db != null)
                                {
                                    Task<bool> res = tb.SaveDocument(the_app_db);
                                    CommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus));
                                    CommonFunctions.AddStatusMessage("New values saved", "INFO", logName);
                                }
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
                            if (the_app_db != null && currentStatus != null)
                            {
                                currentStatus.status = "closed";
                                Task taskG = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));

                                // Close Console app
                                System.Environment.Exit(1);
                            }
                            break;
                    }
                    //var newMessage = $"{message}";
                    //clsCommonFunctions.AddStatusMessage(newMessage);

                });

                try
                {

                    await hubConnection.StartAsync();
                    CommonFunctions.AddStatusMessage("Connection started", "INFO", logName);

                }
                catch (Exception e)
                {
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = e.ToString(),
                        Log_Type = "Error",
                        Log_App = "setupMessaging",
                        Epic = this.epicName + "-" + this.resolution
                    };
                    await log.Save();
                }

                hubConnection.Reconnecting += error =>
                {
                    Debug.Assert(hubConnection.State == HubConnectionState.Reconnecting);

                    // Notify users the connection was lost and the client is reconnecting.
                    // Start queuing or dropping messages.
                    string strErr = "";
                    if (error != null) { strErr = error.ToString(); }
                    CommonFunctions.AddStatusMessage($"Messaging connection lost, retrying - {strErr}", "ERROR", logName);
                    return Task.CompletedTask;
                };
                hubConnection.Reconnected += connectionId =>
                {
                    Debug.Assert(hubConnection.State == HubConnectionState.Connected);
                    CommonFunctions.AddStatusMessage("Messaging connection reconnected", "INFO", logName);
                    // Notify users the connection was reestablished.
                    // Start dequeuing messages queued while reconnecting if any.

                    return Task.CompletedTask;
                };
            }
            catch (Exception ex)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "setupMessaging"
                };
                await log.Save();
            }
        }

        public static async Task<ModelMinuteCandle> CreateLiveCandle(Database the_db, int minAvgIndex, int min30AvgIndex, int minAvgIndexShort, int min30AvgIndexShort, DateTime dtDate, string epicName, Container minute_container, Container TicksContainer, bool MAonly, bool createMinRecord, Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
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

                // string ret = "";

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
                            CommonFunctions.AddStatusMessage("TickData not found. Creating ...", "INFO");
                            // Get the previous tick and save it as the next tick
                            TickData tick = await IGModels.clsCommonFunctions.GetTickData(the_db, TicksContainer, epicName, dtDate);
                            tick.UTM = dtDate;
                            tick.id = System.Guid.NewGuid().ToString();
                            bool sv = await tick.Save(the_db, TicksContainer);
                            CommonFunctions.AddStatusMessage("New tick created. ", "INFO");
                            //Now we can get the candle data as it should now find the tick created above
                            data = await IGModels.clsCommonFunctions.GetCandleDataV2(the_db, TicksContainer, epicName, dtDate, minEnd);
                        }

                        clsMinuteCandle minuteCandle = new clsMinuteCandle
                        {
                            id = System.Guid.NewGuid().ToString(),
                            Epic = epicName,
                            CandleStart = dtDate,
                            candleData = data
                        };
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

                IG_Epic epic = new IG_Epic
                {
                    Epic = epicName
                };








                //CandleMovingAverage min30Avg = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, min30Dates.start, min30Dates.end);
                //CandleMovingAverage min30AvgShort = await IGModels.clsCommonFunctions.GetMovingAverageV2(the_db, minute_container, epicName, min30DatesShort.start, min30DatesShort.end);
                //bigWatch.Restart();
                //CandleMovingAverage min30Avg = await Get_MinuteMovingAverageNum30(the_db, minute_container, epic, retCandle.candleStart,  min30AvgIndex);
                CandleMovingAverage min30Avg = await Get_MinuteMovingAverageNum30v1(the_db, minute_container, epic, retCandle.candleStart, min30AvgIndex, the_app_db, exchangeClosedDates);
                //bigWatch.Stop();
                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - min30Avg - Time taken = " + bigWatch.ElapsedMilliseconds);
                //bigWatch.Restart();
                //            CandleMovingAverage min30AvgShort = await Get_MinuteMovingAverageNum30(the_db, minute_container, epic, retCandle.candleStart,  min30AvgIndexShort );
                CandleMovingAverage min30AvgShort = await Get_MinuteMovingAverageNum30v1(the_db, minute_container, epic, retCandle.candleStart, min30AvgIndexShort, the_app_db, exchangeClosedDates);

                // bigWatch.Stop();
                //clsCommonFunctions.AddStatusMessage(DateTime.Now.ToString("o") + " - min30AvgShort - Time taken = " + bigWatch.ElapsedMilliseconds);

                retCandle.mA30MinTypicalShortClose = min30AvgShort.Close;
                retCandle.mA30MinTypicalLongClose = min30Avg.Close;
            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "CreateLiveCandle"
                };
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
                    Log log = new Log
                    {
                        Log_Message = e.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetAvgDates"
                    };
                    _ = log.Save();
                }
                //clsCommonFunctions.AddStatusMessage(i + " ---- Now : " + now + " Start : " + ret.start + " End : " + ret.end);

            }
        }




        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num, Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
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
                //bool weekendDetected = false;
                for (int i = 0; i <= num - 1; i++)
                {

                    bool blnFound = false;

                    //int numChances = 0;

                    // Get the candle for the required date. If it does not exist, keep trying a minute less until one is found.
                    while (!blnFound)
                    {
                        // Sort out the start date if it now falls during the weekend. This is so we can get the averages of candles created surrounding a weekend
                        //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate) && !weekendDetected)
                        if (!await IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates, epic.Epic))
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
                            //weekendDetected = true;
                        }
                        else
                        {
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate))
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates,epic.Epic).Result)
                            //{
                            //    weekendDetected = false;
                            //}
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
                CandleMovingAverage newMA = new CandleMovingAverage
                {
                    //newMA.sequence = idx;
                    StartDate = resp[0].candleData.StartDate,
                    EndDate = resp[resp.Count - 1].candleData.EndDate,
                    Close = resp.Select(x => x.candleData.Close).Average(),
                    High = resp.Select(x => x.candleData.High).Average(),
                    Low = resp.Select(x => x.candleData.Low).Average(),
                    Open = resp.Select(x => x.candleData.Open).Average(),
                    Typical = resp.Select(x => x.candleData.Typical).Average()
                };

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
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteMovingAverageNum30"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuteMovingAverageNum30"
                };
                await log.Save();
            }

            return (ret);
        }

        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30v1(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num, Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
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
                // bool weekendDetected = false;
                for (int i = 0; i <= num - 1; i++)
                {

                    bool blnFound = false;

                    //int numChances = 0;

                    // Get the candle for the required date. If it does not exist, keep trying a minute less until one is found.
                    while (!blnFound)
                    {

                        // Sort out the start date if it now falls during the weekend. This is so we can get the averages of candles created surrounding a weekend
                        //if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate) && !weekendDetected)
                        if (!await IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates, epic.Epic))
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
                            //weekendDetected = true;
                        }
                        else
                        {
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate))
                            //if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates,epic.Epic).Result)
                            //{
                            //    weekendDetected = false;
                            //}
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
                        Log log = new Log(the_app_db)
                        {
                            Log_Message = de.ToString(),
                            Log_Type = "Error",
                            Log_App = "Get_MinuteMovingAverageNum30v1"
                        };
                        await log.Save();
                    }

                }
                catch (Exception e)
                {
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = e.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteMovingAverageNum30v1"
                    };
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
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteMovingAverageNum30v1"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuteMovingAverageNum30v1"
                };
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
                        if (!IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates, epic.Epic).Result && !weekendDetected)
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
                            if (IGModels.clsCommonFunctions.IsTradingOpen(getStartDate, exchangeClosedDates, epic.Epic).Result)
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
                CandleMovingAverage newMA = new CandleMovingAverage
                {
                    //newMA.sequence = idx;
                    StartDate = resp[0].candleData.StartDate,
                    EndDate = resp[resp.Count - 1].candleData.EndDate,
                    Close = resp.Select(x => x.candleData.Close).Average(),
                    High = resp.Select(x => x.candleData.High).Average(),
                    Low = resp.Select(x => x.candleData.Low).Average(),
                    Open = resp.Select(x => x.candleData.Open).Average(),
                    Typical = resp.Select(x => x.candleData.Typical).Average()
                };

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
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteMovingAverageNum30v2"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuteMovingAverageNum30v2"
                };
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
                    Log log = new Log(the_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteCandle"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuiteCandle"
                };
                await log.Save();
            }

            return (ret);
        }


        public static async Task<clsMinuteCandle> Get_MinuteCandle(Database the_db, Container container, string epicName, DateTime CandleStart)
        {

            clsMinuteCandle ret = new clsMinuteCandle();

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
                    Log log = new Log(the_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteCandle"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuiteCandle"
                };
                await log.Save();
            }

            return (ret);
        }
        public class SpreadValue
        {
            public double spread { get; set; }
            public SpreadValue()
            {
                spread = 0;
            }
            public SpreadValue(double _spread)
            {
                spread = _spread;
            }
        }
        public static async Task<double> Get_SpreadFromLastCandle(Database the_db, Container container, DateTime CandleStart)
        {

            double ret = 0;

            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT top 1 c.candleData.FirstOffer - c.candleData.FirstBid as spread FROM  c ORDER BY  c.CandleStart DESC "
                )
                .WithParameter("@CandleStart", CandleStart);

                using FeedIterator<SpreadValue> filteredFeed = container.GetItemQueryIterator<SpreadValue>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<SpreadValue> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (SpreadValue item in response)
                    {
                        ret = Math.Abs(Math.Round(item.spread, 1));
                    }
                }

                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteCandle"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuiteCandle"
                };
                await log.Save();
            }

            return (ret);
        }
        public static async Task<double> Get_SpreadFromLastCandleRSI(Database? the_db, Container? container, DateTime CandleStart, string resolution, string epicName)
        {

            double ret = 0;

            try
            {
                //Container container = the_db.GetContainer("MinuteCandle");
                if (the_db != null && container != null)
                {
                    var parameterizedQuery = new QueryDefinition(
                        query: "SELECT top 1 c.openPrice.ask - c.openPrice.bid as spread FROM  c WHERE (c.epic = @epic )  and  c.resolution = @resolution order by c.startDate DESC "
                    )
                        .WithParameter("@resolution", resolution)
                        .WithParameter("@epic", epicName)
                    .WithParameter("@CandleStart", CandleStart);

                    using FeedIterator<SpreadValue> filteredFeed = container.GetItemQueryIterator<SpreadValue>(
                        queryDefinition: parameterizedQuery
                    );

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<SpreadValue> response = await filteredFeed.ReadNextAsync();

                        // Iterate query results
                        foreach (SpreadValue item in response)
                        {
                            ret = Math.Abs(Math.Round(item.spread, 1));
                        }
                    }

                    //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);
                }
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new Log(the_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_MinuteCandle"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuiteCandle"
                };
                await log.Save();
            }

            return (ret);
        }
        public async Task<bool> GetPositions()
        {
            bool ret2 = true;
            //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG Container not set in GetPositions");
                }
                if (_igContainer.igRestApiClient == null)
                {
                    throw new InvalidOperationException("IG Rest API Client not set in GetPositions");
                }
                if (model == null)
                {
                    throw new InvalidOperationException("model not set in GetPositions");
                }
                if (this.strategy == "GRID")
                {
                    IgResponse<PositionsResponse> ret;
                    //Do longs first
                    try
                    {


                        ret = await _igContainer.igRestApiClient.getOTCOpenPositionsV1();

                        foreach (OpenPosition obj in ret.Response.positions)
                        {
                            if (obj.market.epic == this.epicName)
                            {
                                OpenPositionData tsm = obj.position;

                                //First see if we already have this deal in our database.
                                tradeItem thisTrade = await GetTradeFromDB(tsm.dealId, this.strategy, this.resolution);


                                if (thisTrade.tbDealId != "")
                                {

                                    if (this.gridLID == "")
                                    {

                                        this.gridLID = thisTrade.BOLLI_ID;
                                        //AddStatusMessage($"BOLLI IF found in DB   {this.bolliID}.");
                                    }



                                    //AddStatusMessage($"BOLLI Trade found in DB with DealID {tsm.dealId}.");
                                    //This trade is in the database already.
                                    this.model.thisModel.currentGRIDLTrade = thisTrade;
                                    this.currentTrade = new clsTradeUpdate
                                    {
                                        epic = this.epicName,
                                        dealId = tsm.dealId,
                                        lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate),
                                        level = Convert.ToDecimal(tsm.openLevel),
                                        stopLevel = Convert.ToDecimal(tsm.stopLevel),
                                        size = Convert.ToDecimal(tsm.dealSize),
                                        direction = tsm.direction
                                    };
                                    this.model.thisModel.currentGRIDLTrade.stopLossValue = (double)this.model.stopPrice;

                                    if (tsm.direction == "BUY")
                                    {
                                        //this.model.stopPrice = (double)this.currentGRIDLTrade.stopLevel - (double)this.currentGRIDLTrade.level;
                                        //this.model.stopPriceOld = (double)this.currentGRIDLTrade.stopLevel - (double)this.currentGRIDLTrade.level;
                                        this.model.longOnmarket = true;
                                        this.model.buyShort = false;
                                        //this.model.shortOnMarket = false;
                                    }
                                    else
                                    {
                                        //this.model.stopPrice = (double)this.currentGRIDLTrade.stopLevel - (double)this.currentGRIDLTrade.level;
                                        //this.model.stopPriceOld = (double)this.currentGRIDLTrade.stopLevel - (double)this.currentGRIDLTrade.level;
                                        this.model.shortOnMarket = true;
                                        this.model.buyLong = false;
                                        //this.model.longOnmarket = false;
                                    }

                                    this.model.thisModel.currentGRIDLTrade.stopLossValue = (double)this.model.stopPrice;

                                    this.model.onMarket = true;


                                    tradeItem? thisTde = this.model.thisModel.gridLTrades.Find(x => x.tbDealId == this.model.thisModel.currentGRIDLTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.gridLTrades.Add(thisTrade);
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} added to BOLLI trades list.");
                                    }
                                    else
                                    {
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} already exists in BOLLI trades list.");
                                    }

                                }
                                else
                                {
                                    // This trade is not in the database!
                                    //AddStatusMessage($"Trade with DealID {tsm.dealId} not found in DB for BOLLI strategy.");
                                }
                            }
                        }

                        //then do shorts
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.AddStatusMessage($"Error in Long GetPositions - {ex.ToString()}", "ERROR");
                    }
                    try
                    {

                        if (_igContainer2 == null)
                        {
                            throw new InvalidOperationException("IG Container not set in GetPositions");
                        }
                        if (_igContainer2.igRestApiClient == null)
                        {
                            throw new InvalidOperationException("IG Rest API Client not set in GetPositions");
                        }

                        ret = await _igContainer2.igRestApiClient.getOTCOpenPositionsV1();

                        foreach (OpenPosition obj in ret.Response.positions)
                        {
                            if (obj.market.epic == this.epicName)
                            {
                                OpenPositionData tsm = obj.position;

                                //First see if we already have this deal in our database.
                                tradeItem thisTrade = await GetTradeFromDB(tsm.dealId, this.strategy, this.resolution);


                                if (thisTrade.tbDealId != "")
                                {

                                    if (this.gridSID == "")
                                    {

                                        this.gridSID = thisTrade.BOLLI_ID;
                                        //AddStatusMessage($"BOLLI IF found in DB   {this.bolliID}.");
                                    }

                                    //AddStatusMessage($"BOLLI Trade found in DB with DealID {tsm.dealId}.");
                                    //This trade is in the database already.
                                    this.model.thisModel.currentGRIDSTrade = thisTrade;
                                    this.currentTrade = new clsTradeUpdate
                                    {
                                        epic = this.epicName,
                                        dealId = tsm.dealId,
                                        lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate),
                                        level = Convert.ToDecimal(tsm.openLevel),
                                        stopLevel = Convert.ToDecimal(tsm.stopLevel),
                                        size = Convert.ToDecimal(tsm.dealSize),
                                        direction = tsm.direction
                                    };
                                    this.model.thisModel.currentGRIDSTrade.stopLossValue = (double)this.model.stopPrice;

                                    if (tsm.direction == "BUY")
                                    {
                                        //this.model.stopPrice = (double)this.currentGRIDSTrade.stopLevel - (double)this.currentGRIDSTrade.level;
                                        //this.model.stopPriceOld = (double)this.currentGRIDSTrade.stopLevel - (double)this.currentGRIDSTrade.level;
                                        this.model.longOnmarket = true;
                                        this.model.buyShort = false;
                                        //this.model.shortOnMarket = false;
                                    }
                                    else
                                    {
                                        //this.model.stopPrice = (double)this.currentGRIDSTrade.stopLevel - (double)this.currentGRIDSTrade.level;
                                        //this.model.stopPriceOld = (double)this.currentGRIDSTrade.stopLevel - (double)this.currentGRIDSTrade.level;
                                        this.model.shortOnMarket = true;
                                        this.model.buyLong = false;
                                        //this.model.longOnmarket = false;
                                    }

                                    this.model.thisModel.currentGRIDSTrade.stopLossValue = (double)this.model.stopPrice;

                                    this.model.onMarket = true;


                                    tradeItem? thisTde = this.model.thisModel.gridSTrades.Find(x => x.tbDealId == this.model.thisModel.currentGRIDSTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.gridSTrades.Add(thisTrade);
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} added to BOLLI trades list.");
                                    }
                                    else
                                    {
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} already exists in BOLLI trades list.");
                                    }

                                }
                                else
                                {
                                    // This trade is not in the database!
                                    //AddStatusMessage($"Trade with DealID {tsm.dealId} not found in DB for BOLLI strategy.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.AddStatusMessage($"Error in Short GetPositions - {ex.ToString()}", "ERROR");
                    }
                }
                else
                {
                    IgResponse<PositionsResponse> ret = await _igContainer.igRestApiClient.getOTCOpenPositionsV1();


                    //AddStatusMessage($"{ret.Response.positions.Count} trades found in IG");
                    foreach (OpenPosition obj in ret.Response.positions)
                    {
                        if (obj.market.epic == this.epicName)
                        {
                            OpenPositionData tsm = obj.position;

                            //First see if we already have this deal in our database.
                            tradeItem thisTrade = await GetTradeFromDB(tsm.dealId, this.strategy, this.resolution);


                            if (this.strategy == "BOLLI")
                            {
                                if (thisTrade.tbDealId != "")
                                {
                                    if (this.bolliID == "")
                                    {

                                        this.bolliID = thisTrade.BOLLI_ID;
                                        //AddStatusMessage($"BOLLI IF found in DB   {this.bolliID}.");
                                    }
                                    //AddStatusMessage($"BOLLI Trade found in DB with DealID {tsm.dealId}.");
                                    //This trade is in the database already.
                                    this.model.thisModel.currentTrade = thisTrade;
                                    this.currentTrade = new clsTradeUpdate
                                    {
                                        epic = this.epicName,
                                        dealId = tsm.dealId,
                                        lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate),
                                        level = Convert.ToDecimal(tsm.openLevel),
                                        stopLevel = Convert.ToDecimal(tsm.stopLevel),
                                        size = Convert.ToDecimal(tsm.dealSize),
                                        direction = tsm.direction
                                    };
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


                                    tradeItem? thisTde = this.model.thisModel.bolliTrades.Find(x => x.tbDealId == this.model.thisModel.currentTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.bolliTrades.Add(thisTrade);
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} added to BOLLI trades list.");
                                    }
                                    else
                                    {
                                        //AddStatusMessage($"BOLLI Trade with DealID {tsm.dealId} already exists in BOLLI trades list.");
                                    }

                                }
                                else
                                {
                                    // This trade is not in the database!
                                    //AddStatusMessage($"Trade with DealID {tsm.dealId} not found in DB for BOLLI strategy.");
                                }
                            }
                            else
                            {


                                // Check to see if it is not a supplementary trade first.
                                if (!thisTrade.isSuppTrade)
                                {
                                    if (thisTrade.tbDealId != "")
                                    {
                                        this.model.thisModel.currentTrade = thisTrade;
                                        this.currentTrade = new clsTradeUpdate
                                        {
                                            epic = this.epicName,
                                            dealId = tsm.dealId,
                                            lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate),
                                            level = Convert.ToDecimal(tsm.openLevel),
                                            stopLevel = Convert.ToDecimal(tsm.stopLevel),
                                            size = Convert.ToDecimal(tsm.dealSize),
                                            direction = tsm.direction
                                        };
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

                                        if (this.strategy == "BOLLI")
                                        {
                                            tradeItem? thisTde = this.model.thisModel.bolliTrades.Find(x => x.tbDealId == this.model.thisModel.currentTrade.tbDealId);
                                            if (thisTde == null)
                                            {
                                                this.model.thisModel.bolliTrades.Add(thisTrade);
                                            }
                                        }
                                    }


                                }
                                else
                                {
                                    if (thisTrade.tbDealId != "" && currentTrade != null)
                                    {
                                        // This is a supplementary trade.
                                        this.model.thisModel.suppTrade = thisTrade;
                                        this.suppTrade = new clsTradeUpdate
                                        {
                                            epic = this.epicName,
                                            dealId = tsm.dealId,
                                            lastUpdated = IGModels.clsCommonFunctions.ConvertToIGDate(tsm.createdDate),
                                            level = Convert.ToDecimal(tsm.openLevel),
                                            stopLevel = Convert.ToDecimal(tsm.stopLevel),
                                            size = Convert.ToDecimal(tsm.dealSize),
                                            direction = tsm.direction
                                        };
                                        //if (this.currentTrade.stopLevel != null && this.currentTrade.level != null){
                                        this.model.thisModel.suppTrade.stopLossValue = (double)currentTrade.stopLevel - (double)this.currentTrade.level;
                                        //}
                                        this.model.onSuppTrade = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetPositions",
                    Epic = this.epicName
                };
                await log.Save();
            }
            return ret2;
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
        public async Task<tradeItem> GetTradeFromDB(string dealID, string strategy, string resolution = "")
        {
            tradeItem ret = new tradeItem();

            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("the_app_db is null.");
                }
                Microsoft.Azure.Cosmos.Container? container = the_app_db.GetContainer("TradingBrainTrades");
                string qry = "SELECT * FROM  c WHERE  c.tbDealId=@DealID AND c.strategy = @strategy AND c.resolution = @resolution ";
                if (strategy == "SMA")
                {
                    qry = "SELECT * FROM  c WHERE  c.tbDealId=@DealID AND (c.strategy = @strategy or c.strategy = '' or not is_defined(c.strategy))   ";
                }
                var parameterizedQuery = new QueryDefinition(
                    query: qry
                )
                .WithParameter("@epicName", epicName)
                .WithParameter("@DealID", dealID)
                .WithParameter("@strategy", strategy)
                .WithParameter("@resolution", resolution);

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
                    Log log = new TradingBrain.Models.Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetTradeFromDB"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetTradeFromDB"
                };
                await log.Save();
            }

            return ret;

        }
        public async Task<tradeItem> GetTradeFromDB(string dealID)
        {
            tradeItem ret = new tradeItem();

            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("the_app_db is null.");
                }
                Microsoft.Azure.Cosmos.Container? container = the_app_db.GetContainer("TradingBrainTrades");
                string qry = "SELECT * FROM  c WHERE  c.tbDealId=@DealID   ";

                var parameterizedQuery = new QueryDefinition(
                    query: qry
                )
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
                    Log log = new TradingBrain.Models.Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetTradeFromDB"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetTradeFromDB"
                };
                await log.Save();
            }

            return ret;

        }
        public async Task<tradeItem> GetTradeFromDBByOrder(string dealID)
        {
            tradeItem ret = new tradeItem();

            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("the_app_db is null.");
                }
                Microsoft.Azure.Cosmos.Container? container = the_app_db.GetContainer("TradingBrainTrades");

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
                    Log log = new TradingBrain.Models.Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetTradeFromDB"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetTradeFromDB"
                };
                await log.Save();
            }

            return ret;

        }
        public async Task<orderItem> GetOrderFromDB(string dealID)
        {
            orderItem ret = new orderItem();

            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("the_app_db is null.");
                }
                Microsoft.Azure.Cosmos.Container? container = the_app_db.GetContainer("TradingBrainOrders");

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
                    Log log = new TradingBrain.Models.Log(the_app_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetOrderFromDB"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new Log(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetOrderFromDB"
                };
                await log.Save();
            }

            return ret;

        }


        public async void GetTradeFromDBSync(string dealID)
        {
            tradeItem ret = new tradeItem();
            if (model != null)
            {
                try
                {
                    if (the_app_db == null)
                    {
                        throw new InvalidOperationException("the_app_db is null.");
                    }

                    Microsoft.Azure.Cosmos.Container? container = the_app_db.GetContainer("TradingBrainTrades");

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
                        Log log = new TradingBrain.Models.Log(the_app_db)
                        {
                            Log_Message = de.ToString(),
                            Log_Type = "Error",
                            Log_App = "GetTradeFromDB"
                        };
                        await log.Save();
                    }

                }
                catch (Exception e)
                {
                    Log log = new Log(the_app_db)
                    {
                        Log_Message = e.ToString(),
                        Log_Type = "Error",
                        Log_App = "GetTradeFromDB"
                    };
                    await log.Save();
                }

                this.model.thisModel.currentTrade = ret;
            }
            //return ret;

        }
        //public tradeItem GetTradeFromDBSync(string dealID)
        //{
        //    Task<tradeItem> task = Task.Run<tradeItem>(async () => await GetTradeFromDB(dealID));
        //    return task.Result;

        //}


    }

}