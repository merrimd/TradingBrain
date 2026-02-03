#pragma warning disable S125
#pragma warning disable S1244
#pragma warning disable S1764
#pragma warning disable S2589
#pragma warning disable S3776
#pragma warning disable S107

using Azure.Core.GeoJson;
using Azure.Storage;
using com.lightstreamer.client;
using DotNetty.Handlers.Tls;
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
using System.Reflection;
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
        public StatusMessage? currentStatus { get; set; }
        public delegate void StopDelegate();
        public Database? the_db { get; set; }
        public Database? the_app_db { get; set; }
        public Microsoft.Azure.Cosmos.Container? the_container { get; set; }
        public Microsoft.Azure.Cosmos.Container? the_chart_container { get; set; }
        public Microsoft.Azure.Cosmos.Container? minute_container { get; set; }
        public Microsoft.Azure.Cosmos.Container? candles_RSI_container { get; set; }
        public Microsoft.Azure.Cosmos.Container? TicksContainer { get; set; }
        public Microsoft.Azure.Cosmos.Container? trade_container { get; set; }

        public string epicName { get; set; } = "";

        // private long _lngTickCount;

        public ChartUpdate currentTick { get; set; } = new ChartUpdate();
        public clsTradeUpdate? currentTrade { get; set; }
        public clsTradeUpdate? currentSIMLTrade { get; set; }
        public clsTradeUpdate? currentSIMSTrade { get; set; }
        public clsTradeUpdate? currentGRIDLTrade { get; set; }
        public clsTradeUpdate? currentGRIDSTrade { get; set; }
        public clsTradeUpdate suppTrade { get; set; } = new clsTradeUpdate();
        public CandleUpdate currentCandle { get; set; } = new CandleUpdate();

        public TradingBrainSettings? tb { get; set; } = new TradingBrainSettings();
        public Dictionary<string, string> TradeErrors { get; set; } = [];

        public List<requestedTrade> requestedTrades { get; set; } = [];
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
        public System.Timers.Timer ti = new();
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

        public modQuote lastCandle = new();
        public List<modQuote> candleList = [];

        public IGContainer _igContainer = new();
        public IGContainer? _igContainer2 = new();
        const int MAX_WAIT_FOR_CLOSE_TIME = 60;
        public int closeAttemptCount = 0;

        public int testCount { get; set; } = 0;
        public decimal gridMinuteSMA { get; set; } = 0;

        public TradingBrainSettings setInitialModelVar()
        {
            //firstTB = await clsCommonFunctions.GetTradingBrainSettings(this.the_db, this.epicName);
            try
            {
                if (the_app_db == null)
                {
                    throw new InvalidOperationException("Application database is null in setInitialModelVar");
                }
                Task<TradingBrainSettings> tbTask = Task.Run<TradingBrainSettings>(async () => await CommonFunctions.GetTradingBrainSettings(this.the_app_db, this.epicName, this.igAccountId, this.strategy, this.resolution));
                //return tb.Result;
                return tbTask.Result;
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
                this.candleList = [];

                IG_Epic epicObj = CommonFunctions.Get_IG_Epic(appDb, epic).Result;
                this.futures = epicObj.futures;

                bolliID = "";
                gridLID = "";
                gridSID = "";
                //tbClient = null;
                //forceT = forceTransport;
                currentStatus = new StatusMessage();
                requestedTrades = [];
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

                Task.Run(() => setupTradeErrors());
                Task.Run(() => setupMessaging());

                currentTick = new ChartUpdate();
                currentCandle = new CandleUpdate();
                currentTrade = new clsTradeUpdate();
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
                    if (accumValues != null && accumValues.accumQuantity > 0)
                    {
                        modelVar.calcAvgWinningTrade = accumValues.accumProfit / accumValues.accumQuantity;
                        currentStatus.calcAvgWinningTrade = modelVar.calcAvgWinningTrade;
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

                IGContainer? _igContainerToUse = null;
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

                dto.endpoint.positions.create.otc.v2.CreatePositionRequest pos = new()
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
                    await Task.Run(() => _igContainerToUse.tbClient.ConnectToRest());
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
                Log log = new(the_app_db)
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

                IGContainer _igContainerToUse = new();
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
                dto.endpoint.positions.close.v1.ClosePositionRequest pos = new();

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
                    await Task.Run(() => _igContainerToUse.tbClient.ConnectToRest());
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
                Log log = new(the_app_db)
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

                IGContainer _igContainerToUse = new();
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
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in CloseDealEpic"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in CloseDealEpic"); }
                List<tradeItem> trades = [];

                bool newsession = false;
                //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Closing deal", the_app_db);
                dto.endpoint.positions.close.v1.ClosePositionRequest pos = new()
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
                        trades = await model.thisModel.gridLTrades.DeepCopyAsync();
                    }
                    else
                    {
                        pos.direction = "BUY";
                        trades = await model.thisModel.gridSTrades.DeepCopyAsync();
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
                        _ = _igContainerToUse.tbClient.ConnectToRest();
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
                Log log = new(the_app_db)
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
        public async Task<string> CloseDealEpicPartial(string direction, string epic, string accountId)
        {
            string dealRef = "";

            try
            {
                // closes all trades for the epic
                if (_igContainer == null) { throw new InvalidOperationException("IG Container is null in CloseDealEpicPartial"); }
                if (_igContainer2 == null && strategy == "GRID") { throw new InvalidOperationException("IG Container2 is null in CloseDealEpicPartial"); }

                IGContainer _igContainerToUse = new();
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
                    CommonFunctions.AddStatusMessage($"CloseDealEpicPartial - Account ID {accountId} not found", "ERROR");
                }
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in CloseDealEpicPartial"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in CloseDealEpicPartial"); }
                List<tradeItem> trades = [];

                bool newsession = false;
                //TradingBrain.Models.clsCommonFunctions.SaveLog("Info", "CloseDeal", "Closing deal", the_app_db);
                dto.endpoint.positions.close.v1.ClosePositionRequest pos = new()
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
                        trades = await model.thisModel.gridLTradesToClose.DeepCopyAsync();
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
                        CommonFunctions.AddStatusMessage($"Close Partial epic {epic} - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");

                        if (ret.StatusCode.ToString() == "Unauthorized")
                        {
                            newsession = true;
                        }
                    }

                    if (newsession)
                    {
                        _ = _igContainerToUse.tbClient.ConnectToRest();
                        ret = await _igContainerToUse.igRestApiClient.closePosition(pos);
                        if (ret != null)
                        {
                            dealRef = ret.Response.dealReference;
                            CommonFunctions.AddStatusMessage($"Close Partial epic {epic} - {direction} - Status: {ret.StatusCode} deal reference {dealRef} ", "INFO");
                        }
                    }
                    Thread.Sleep(200);
                }


            }
            catch (Exception e)
            {
                Log log = new(the_app_db)
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

                IGContainer _igContainerToUse = new();
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
                if (_igContainerToUse.igRestApiClient == null) { throw new InvalidOperationException("IG Rest API Client is null in EditDeal"); }
                if (_igContainerToUse.tbClient == null) { throw new InvalidOperationException("tbClient is null in EditDeal"); }

                CommonFunctions.AddStatusMessage("Editing deal. StopLoss = " + stopLoss + " - dealId = " + dealID, "INFO");
                bool newsession = false;
                TradingBrain.Models.CommonFunctions.SaveLog("Info", "EditDeal", "Editing deal - " + dealID, the_app_db);
                dto.endpoint.positions.edit.v2.EditPositionRequest pos = new()
                {
                    stopLevel = Convert.ToDecimal(stopLoss)
                };
                //this.model.modelVar.breakEvenVar
                if (this.strategy == "SMA2")
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
                    _ = _igContainerToUse.tbClient.ConnectToRest();
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
                Log log = new(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "EditDeal"
                };
                await log.Save();
            }
            return dealRef;
        }
        public async Task<RunRet> RunCodeV5(object? sender, System.Timers.ElapsedEventArgs e)
        {

            //bool liveMode = true;
            marketOpen = false;

            DateTime dtNow = DateTime.UtcNow;
            DateTime _startTime;
            resolution = "MINUTE";


            RunRet taskRet = new();
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
                    _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0, DateTimeKind.Utc).AddMinutes(-1);
                }
                else
                {
                    _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0, DateTimeKind.Utc);
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

                        model.thisModel.inputs = await this.tb.runDetails.inputs.DeepCopyAsync();
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
                            _startTime = new DateTime(2024, 11, 11, 16, 12, 00, DateTimeKind.Utc);
                            _endTime = new DateTime(2024, 11, 12, 14, 30, 00, DateTimeKind.Utc);
                            //liveMode = false;
                        }

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
                        modelInstanceInputs? thisInput = tb.runDetails.inputs.FirstOrDefault(t => t.spread == thisSpread) ?? null;

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
                            LOepic? thisEpic = _igContainer.PriceEpicList.FirstOrDefault(x => x.name == epicName) ?? throw new InvalidOperationException("Epic not found in PriceEpicList");
                            DateTime tickStart = _startTime;
                            DateTime tickeEnd = _startTime.AddMinutes(1).AddMilliseconds(-1);
                            List<tick> ticks = thisEpic.ticks.Where(t => t.UTM >= tickStart && t.UTM <= tickeEnd).ToList();

                            tbPrice thisPrice = new();

                            dto.endpoint.prices.v2.Price lowPrice = new();
                            dto.endpoint.prices.v2.Price highPrice = new();
                            dto.endpoint.prices.v2.Price openPrice = new();
                            dto.endpoint.prices.v2.Price closePrice = new();

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
                            thisPrice.typicalPrice ??= new dto.endpoint.prices.v2.Price();
                            thisPrice.startDate = tickStart;
                            thisPrice.endDate = tickeEnd;
                            thisPrice.openPrice = openPrice;
                            thisPrice.closePrice = closePrice;
                            thisPrice.highPrice = highPrice;
                            thisPrice.lowPrice = lowPrice;
                            thisPrice.typicalPrice.bid = (highPrice.bid + lowPrice.bid + closePrice.bid) / 3;
                            thisPrice.typicalPrice.ask = (highPrice.ask + lowPrice.ask + closePrice.ask) / 3;

                            List<double> closePrices = ticks.Select(x => (double)(x.bid + x.offer) / 2).ToList();
                            StandardDeviation sd = new(closePrices);
                            thisPrice.stdDev = sd.Value;

                            //StandardDeviation sd = new StandardDeviation((IEnumerable<double>)ticks.Select(x => (x.bid + x.offer) / 2).ToList());
                            //thisPrice.stdDev = sd.Value;

                            modQuote thisCandle = new()
                            {
                                Date = tickStart,
                                Close = (thisPrice.closePrice.bid ?? 0 + thisPrice.closePrice.ask ?? 0) / 2,
                                Open = (thisPrice.openPrice.bid ?? 0 + thisPrice.openPrice.ask ?? 0) / 2,
                                High = (thisPrice.highPrice.bid ?? 0 + thisPrice.highPrice.ask ?? 0) / 2,
                                Low = (thisPrice.lowPrice.bid ?? 0 + thisPrice.lowPrice.ask ?? 0) / 2,
                                Typical = (thisPrice.typicalPrice.bid ?? 0 + thisPrice.typicalPrice.ask ?? 0) / 2,
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


                            List<modQuote> rsiQuotes = [];
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

                                        modQuote thisQuote = new()
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

                                    List<modQuote> sma30Quotes = indCandles.Where(s => s.Date <= mq.Date).Where(s => s.Date == mq.Date || s.Date.Minute == 29 || s.Date.Minute == 59).ToList();

                                    for (int sma30 = 1; sma30 <= 50; sma30 += 1)
                                    {
                                        //if (indIndex >= sma30 - 1)
                                        //{
                                        modQuote thisQuote = new()
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
                            model.candles.currentCandle.FirstBid = (thisPrice.openPrice.bid ?? 0);
                            model.candles.currentCandle.FirstOffer = (thisPrice.openPrice.ask ?? 0);


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
                                sL = thisInput.var4 * Math.Abs(targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
                                stdDevSL = sL / stdDev;
                            }
                            else
                            {
                                if (model.sellShort)
                                {
                                    sL = thisInput.var5 * Math.Abs(targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
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
                                model.stopLossVar = thisInput.var4 * Math.Abs(targetVar * (double)model.candles.currentCandle.mATypicalLongTypical - (double)model.candles.currentCandle.mATypicalLongTypical);
                                requestedTrade reqTrade = new()
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
                                    _ = await CloseDeal("long", (double)this.currentTrade.size, this.currentTrade.dealId, this.igAccountId);
                                }
                            }

                            if (model.sellShort && this.currentTrade == null)
                            {
                                CommonFunctions.AddStatusMessage("SellShort activated", "INFO", logName);
                                TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);
                                model.stopLossVar = thisInput.var5 * Math.Abs(targetVarShort * (double)model.candles.currentCandle.mATypicalShortTypical - (double)model.candles.currentCandle.mATypicalShortTypical);
                                requestedTrade reqTrade = new()
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

                                if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.buyPrice - Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP(model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                {



                                    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                    decimal? currentStopLevel = this.currentTrade.stopLevel;
                                    if (model.modelVar.breakEvenVar == 1)
                                    {


                                        this.currentTrade.stopLevel = model.thisModel.currentTrade.buyPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditLong activated BREAKEVEN set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal - BREAKEVEN", the_app_db);
                                        string dealRef = await EditDeal((double)model.thisModel.currentTrade.buyPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);

                                    }
                                    else

                                    {
                                        this.currentTrade.stopLevel = model.thisModel.currentTrade.buyPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditLong Long activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Long Deal ", the_app_db);
                                        _ = await EditDeal((double)model.thisModel.currentTrade.buyPrice - model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);

                                    }

                                }
                            }
                            if (model.shortOnMarket && currentTrade != null && model.thisModel.currentTrade != null)
                            {
                                CommonFunctions.AddStatusMessage($"[SHORT] Check if sellPrice ({model.thisModel.currentTrade.sellPrice}) + stoplossvalue ({Math.Abs(model.thisModel.currentTrade.stopLossValue)}) ({(double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)}) = currentTrade.stoplevel ({this.currentTrade.stopLevel}) - BreakEvenVar = {model.modelVar.breakEvenVar}", "DEBUG", logName);

                                if ((IGModels.clsCommonFunctions.Dbl2DP((double)model.thisModel.currentTrade.sellPrice + Math.Abs(model.thisModel.currentTrade.stopLossValue)) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)) && (IGModels.clsCommonFunctions.Dbl2DP(model.thisModel.currentTrade.stopLossValue) != IGModels.clsCommonFunctions.Dbl2DP((double)this.currentTrade.stopLevel)))
                                {
                                    CommonFunctions.AddStatusMessage("EditShort activated", "INFO", logName);
                                    TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit Short Deal", the_app_db);


                                    //EditDeal(Math.Abs( model.thisModel.currentTrade.stopLossValue), this.currentTrade.dealId);
                                    decimal? currentStopLevel = this.currentTrade.stopLevel;

                                    if (model.modelVar.breakEvenVar == 1)
                                    {

                                        this.currentTrade.stopLevel = model.thisModel.currentTrade.sellPrice - (decimal)model.thisModel.currentTrade.stopLossValue;
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
                                            _ = await EditDeal((double)model.thisModel.currentTrade.sellPrice - model.thisModel.currentTrade.stopLossValue, this.suppTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);
                                        }
                                    }
                                    else
                                    {

                                        this.currentTrade.stopLevel = model.thisModel.currentTrade.sellPrice + (decimal)model.thisModel.currentTrade.stopLossValue;
                                        CommonFunctions.AddStatusMessage($"EditShort SUPP activated set - Current stop value = {currentStopLevel}, new stop value = {this.currentTrade.stopLevel}", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "Edit SUPP Short Deal ", the_app_db);
                                        _ = await EditDeal((double)model.thisModel.currentTrade.sellPrice + model.thisModel.currentTrade.stopLossValue, this.currentTrade.dealId, model.thisModel.currentTrade.stopLossValue, this.igAccountId);


                                    }

                                }
                            }


                            try
                            {
                                if (model.thisModel.currentTrade != null && model.thisModel.currentTrade.targetPrice != 0)
                                {
                                    await model.thisModel.currentTrade.SaveDocument(this.trade_container);
                                }
                                if (model.thisModel.suppTrade != null && model.thisModel.suppTrade.targetPrice != 0)
                                {
                                    await model.thisModel.suppTrade.SaveDocument(this.trade_container);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log log = new(the_app_db)
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

                            if (model.modelLogs.logs.Count > 0)
                            {
                                ModelLog log = model.modelLogs.logs[0];
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
                                    model.modelLogs.logs = [];
                                }
                            }
                        }
                        model.candles.prevCandle2 = await model.candles.prevCandle.DeepCopyAsync();
                        model.candles.prevCandle = await model.candles.currentCandle.DeepCopyAsync();
                        //_startTime = _startTime.AddMinutes(1);
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
                Log log = new(the_app_db)
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
            RunRet taskRet = new();
            this.logName = IGModels.clsCommonFunctions.GetLogName(this.epicName, strategy, resolution);
            ScopeContext.PushProperty("app", "TRADINGBRAIN/");
            ScopeContext.PushProperty("epic", this.epicName + "/");
            ScopeContext.PushProperty("strategy", strategy + "/");
            ScopeContext.PushProperty("resolution", resolution + "/");
            //bool liveMode = true;
            marketOpen = false;

            DateTime dtNow = DateTime.UtcNow;
            DateTime _startTime;
            resolution = "SECOND";

            //int numCandlesToStart = 20;

            // Sometimes the timer that runs the RunCode will actually start at :59.xxx rather than at :00.000. This then means the minute candle is incorrect.
            //int seconds = dtNow.Second;
            //if (seconds < 59)
            //{
            _startTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, dtNow.Second, 0, DateTimeKind.Utc);
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


                    CommonFunctions.AddStatusMessage(" ---------------------------------", "INFO", logName);
                    CommonFunctions.AddStatusMessage($" - Run Started {epicName}", "INFO", logName);
                    //CommonFunctions.AddStatusMessage($" - Epic       - {epicName}", "INFO", logName);
                    //CommonFunctions.AddStatusMessage($" - Strategy   - {strategy}", "INFO", logName);
                    //CommonFunctions.AddStatusMessage($" - Resolution - {resolution}", "INFO", logName);
                    CommonFunctions.AddStatusMessage(" ---------------------------------", "INFO", logName);
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
                        model.thisModel.inputs_RSI = await this.tb.runDetails.inputs_RSI.DeepCopyAsync();
                        model.thisModel.counterVar = Math.Max(this.tb.runDetails.counterVar, 1000);
                        model.thisModel.matchProTrend = false;
                        model.modelVar.maxDropFlag = this.tb.lastRunVars.maxDropFlag;
                        if (model.modelVar.maxDropFlag == 1) { currentStatus.status = "MaxDropFlagSet"; }
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

                        modelInstanceInputs_RSI? thisInput = tb.runDetails.inputs_RSI.FirstOrDefault(t => t.spread == 0);
                        if (thisInput == null)
                        {
                            CommonFunctions.AddStatusMessage($"No inputs found for spread = 0", "ERROR", logName);
                        }

                        else
                        {
                            CommonFunctions.AddStatusMessage("Getting current candle data");

                            bool gotCandle = false;

                            //Get the last tick from the list of ticks
                            LOepic? thisEpic = _igContainer.PriceEpicList.FirstOrDefault(x => x.name == epicName) ?? throw new InvalidOperationException("Epic not found in PriceEpicList");
                            DateTime tickStart = _startTime.AddSeconds(-1);
                            DateTime tickeEnd = _startTime.AddSeconds(1).AddMilliseconds(-1);
                            if (thisEpic.ticks == null)
                            {
                                AddStatusMessage("thisEpic.ticks is null - creating new list", "WARNING", logName);

                                thisEpic.ticks = new List<tick>();
                                string msg = thisEpic.Equals(null) ? "thisEpic is null" : "thisEpic.ticks was null";

                                Log log = new(the_app_db)
                                {
                                    Log_Message = this.epicName + " - " + msg,
                                    Epic = this.epicName,
                                    Log_Type = "Error",
                                    Log_App = "RunCode"
                                };
                                await log.Save();
                            }
                            List<tick> ticks = thisEpic.ticks.Where(t => t.UTM >= tickStart && t.UTM <= tickeEnd).ToList();
                            modQuote thisCandle = new();

                            //clsCommonFunctions.AddStatusMessage($"Ticks - {thisEpic.ticks.Count}");
                            //clsCommonFunctions.AddStatusMessage($"Ticks in period {tickStart} to {tickeEnd} - Last Tick = {thisEpic.ticks.Last().UTM}");
                            if (ticks != null && ticks.Count > 0)
                            {
                                tbPrice thisPrice = new();

                                dto.endpoint.prices.v2.Price lowPrice = new();
                                dto.endpoint.prices.v2.Price highPrice = new();
                                dto.endpoint.prices.v2.Price openPrice = new();
                                dto.endpoint.prices.v2.Price closePrice = new();

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
                                thisPrice.typicalPrice ??= new dto.endpoint.prices.v2.Price();
                                thisPrice.startDate = tickStart;
                                thisPrice.endDate = tickeEnd;
                                thisPrice.openPrice = openPrice;
                                thisPrice.closePrice = closePrice;
                                thisPrice.highPrice = highPrice;
                                thisPrice.lowPrice = lowPrice;
                                thisPrice.typicalPrice.bid = (highPrice.bid + lowPrice.bid + closePrice.bid) / 3;
                                thisPrice.typicalPrice.ask = (highPrice.ask + lowPrice.ask + closePrice.ask) / 3;

                                List<double> closePrices = ticks.Select(x => (double)(x.bid + x.offer) / 2).ToList();
                                StandardDeviation sd = new(closePrices);
                                thisPrice.stdDev = sd.Value;


                                thisCandle.Date = tickStart;
                                thisCandle.Close = ((thisPrice.closePrice.bid ?? 0) + (thisPrice.closePrice.ask ?? 0)) / 2;
                                thisCandle.Open = ((thisPrice.openPrice.bid ?? 0) + (thisPrice.openPrice.ask ?? 0)) / 2;
                                thisCandle.High = ((thisPrice.highPrice.bid ?? 0) + (thisPrice.highPrice.ask ?? 0)) / 2;
                                thisCandle.Low = ((thisPrice.lowPrice.bid ?? 0) + (thisPrice.lowPrice.ask ?? 0)) / 2;
                                thisCandle.Typical = ((thisPrice.typicalPrice.bid ?? 0) + (thisPrice.typicalPrice.ask ?? 0)) / 2;
                                thisCandle.stdDev = sd.Value;
                                thisCandle.spread = (double)((thisPrice.closePrice.ask ?? 0) - (thisPrice.closePrice.bid ?? 0));
                                //this.lastCandle = thisCandle.DeepCopy();

                                //Remove the first candle and add this one to ensure we keep a rolling set of candles
                                this.candleList.RemoveAt(0);
                                this.candleList.Add(await thisCandle.DeepCopyAsync());


                                AddStatusMessage($"New Tick :{thisPrice.startDate} - {thisPrice.endDate}: Typical:{Math.Round(thisPrice.typicalPrice.bid ?? 0, 2)} / {Math.Round(thisPrice.typicalPrice.ask ?? 0, 2)} ");
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

                                //AddStatusMessage($"Candle Data : {thisCandle.Date} - {thisCandle.Open}");
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
                                CommonFunctions.AddStatusMessage($"{thisCandle.Date} - close price = {Math.Round(thisCandle.Close, 2)}", "DEBUG", logName);
                                if (closeAttemptCount > 0) CommonFunctions.AddStatusMessage($"Current closeAttemptCount = {closeAttemptCount}", "DEBUG", logName);

                                model.candles.currentCandle.grid_long_sma = Convert.ToDouble(candleList.TakeLast((int)thisInput.var4).Average(s => s.Close));
                                if (thisInput.svar4 > 0)
                                {
                                    model.candles.currentCandle.grid_short_sma = Convert.ToDouble(candleList.TakeLast((int)thisInput.svar4).Average(s => s.Close));
                                }
                                model.candles.currentCandle.grid_prev_long_sma = Convert.ToDouble(candleList.SkipLast(1).TakeLast((int)thisInput.var4).Average(s => s.Close));
                                if (thisInput.svar4 > 0)
                                {
                                    model.candles.currentCandle.grid_prev_short_sma = Convert.ToDouble(candleList.SkipLast(1).TakeLast((int)thisInput.svar4).Average(s => s.Close));
                                }
                                if (dtNow.Second == 0 || gridMinuteSMA == 0)
                                {
                                    CommonFunctions.AddStatusMessage($"Getting minute sma");
                                    gridMinuteSMA = await Get_Minute_SMA(the_db, this.epicName, (int)thisInput.var8);
                                }
                                model.candles.currentCandle.grid_minute_sma = gridMinuteSMA;

                                CommonFunctions.AddStatusMessage($"Minute sma = {model.candles.currentCandle.grid_minute_sma}");
                                //CommonFunctions.AddStatusMessage($"Current : SMA Long = {model.candles.currentCandle.grid_long_sma}, Short = {model.candles.currentCandle.grid_short_sma}", "DEBUG", logName);
                                //CommonFunctions.AddStatusMessage($"Prev : SMA Long = {model.candles.currentCandle.grid_prev_long_sma}, Short = {model.candles.currentCandle.grid_prev_short_sma}", "DEBUG", logName);


                                if (model.doShorts) CommonFunctions.AddStatusMessage($"current short sma = {model.candles.currentCandle.grid_short_sma}, prev sma = {model.candles.currentCandle.grid_prev_short_sma} : is current > prev - {model.candles.currentCandle.grid_short_sma > model.candles.currentCandle.grid_prev_short_sma}", "DEBUG", logName);
                                CommonFunctions.AddStatusMessage($"current long sma = {Math.Round(model.candles.currentCandle.grid_long_sma, 2)}, prev sma = {Math.Round(model.candles.currentCandle.grid_prev_long_sma, 2)} : is current < prev - {model.candles.currentCandle.grid_long_sma < model.candles.currentCandle.grid_prev_long_sma}", "DEBUG", logName);

                                if (closeAttemptCount == 0)
                                {

                                    if (model.thisModel.gridLTrades.Count > 0)
                                    {
                                        CommonFunctions.AddStatusMessage($"Long bollid = {this.gridLID}", "DEBUG");
                                        CommonFunctions.AddStatusMessage($"Long quantites = {Math.Round(model.thisModel.gridLTrades.Sum(x => x.quantity), 2)}, position price = {Math.Round(model.thisModel.gridLTrades.Average(x => x.buyPrice), 2)}, last price = {Math.Round(model.thisModel.gridLTrades.Last().buyPrice, 2)} ", "DEBUG", logName);
                                    }

                                    if (model.thisModel.gridSTrades.Count > 0)
                                    {
                                        CommonFunctions.AddStatusMessage($"Short bollid = {this.gridSID}", "DEBUG");
                                        CommonFunctions.AddStatusMessage($"Short quantites = {model.thisModel.gridSTrades.Sum(x => x.quantity)}, position price = {model.thisModel.gridSTrades.Average(x => x.sellPrice)}, last price = {model.thisModel.gridSTrades.Last().sellPrice} ", "DEBUG", logName);
                                    }

                                    CommonFunctions.AddStatusMessage($"Spread = {thisCandle.spread},   Long Start GridSize = {thisInput.var0} Long current gridsize = {Math.Round(model.modelVar.currentGridSize, 2)}", "DEBUG", logName);


                                    // Check to make sure a) var6 has a value other than 0 and b) that the close price now is greater than (1-var6) * close from  3600 candles ago

                                    if (thisInput.var6 > 0 && modelVar.maxDropFlag == 0)
                                    {
                                        modQuote? candle3600Ago = candleList.OrderByDescending(c => c.Date).Skip(3600).FirstOrDefault();
                                        if (candle3600Ago != null)
                                        {
                                            double thresholdPrice = (double)candle3600Ago.Close * (1 - thisInput.var6);
                                            CommonFunctions.AddStatusMessage($"MaxDropFlag - Close price 3600 seconds ago = {Math.Round(candle3600Ago.Close, 2)}, threshold price = {Math.Round(thresholdPrice, 2)}, current price = {thisCandle.Close}", "DEBUG", logName);
                                            if ((double)thisCandle.Close < thresholdPrice)
                                            {
                                                CommonFunctions.AddStatusMessage("MaxDropFlag matched", "DEBUG", logName);
                                                tb.lastRunVars.maxDropFlag = 1;
                                                modelVar.maxDropFlag = 1;
                                                _ = await tb.SaveDocument(this.the_app_db);
                                                CommonFunctions.SendBroadcast("MaxDropFlagSet", this.epicName);
                                                currentStatus.status = "MaxDropFlagSet";

                                                string region = Environment.GetEnvironmentVariable("Region") ?? "";
                                                if (region == "")
                                                {
                                                    region = IGModels.clsCommonFunctions.Get_AppSetting("region");
                                                }
                                                CommonFunctions.AddStatusMessage("MaxDropFlag region = " + region, "DEBUG", logName);
                                                if (region.ToUpper() == "LIVE" )
                                                {
                                                    CommonFunctions.AddStatusMessage("Sending email ", "DEBUG", logName);
                                                    clsEmail obj = new clsEmail();
                                                    List<recip> recips = new List<recip>();
                                                    recips.Add(new recip("Mike Ward", "n525fd@gmail.com"));
                                                    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                    string subject = "MAX DROP FLAG SET - " + this.epicName;
                                                    string text = "The MAX DROP FLAG has been set on the " + this.epicName + " epic</br></br>";
                                                    text += "This will mean that no more trades will be purchased until this flag has been cleared in the IGMonitor website.</br></br>";
                                                    text += "<a href='https://igmonitor.azurewebsites.net/?strategy=GRID'>IGMonitor Website</a></br></br>";
                                                    obj.sendEmail(recips, subject, text);
                                                    CommonFunctions.AddStatusMessage("Email sent to  " + recips.ToList().ToString(), "DEBUG", logName);
                                                }
                                            }
                                            else
                                            {
                                                CommonFunctions.AddStatusMessage("MaxDropFlag NOT matched", "DEBUG", logName);
                                            }
                                        }
                                        else
                                        {
                                            CommonFunctions.AddStatusMessage("MaxDropFlag - could not find candle from 3600 seconds ago", "DEBUG", logName);
                                        }
                                    }
                                    else
                                    {
                                        if (thisInput.var6 == 0)
                                        {
                                            CommonFunctions.AddStatusMessage("MaxDropFlag skipped as var6 = 0", "DEBUG", logName);
                                        }
                                        else
                                        {
                                            CommonFunctions.AddStatusMessage("MaxDropFlag CURRENTLY SET", "DEBUG", logName);
                                        }
                                    }


                                    ///////////////////////////////////////////////////////
                                    // Run the model to determine if we are to buy or sell
                                    ////////////////////////////////////////////////////////
                                    model.RunProTrendCodeGRID(model.candles);
                                    //if (this.testCount < 50)
                                    //{
                                    //    model.buyLong = true;
                                    //    this.testCount += 1;
                                    //    model.modelVar.quantity = 1;
                                    //}
                                    //else
                                    //{
                                    //    clsCommonFunctions.AddStatusMessage("Test loop finished");
                                    //    model.buyLong = false;
                                    //}

                                    CommonFunctions.AddStatusMessage($"values after  run  - buyLong={model.buyLong}, buyShort={model.buyShort}, sellLong={model.sellLong}, sellShort={model.sellShort}, shortOnMarket={model.shortOnMarket}, longOnmarket={model.longOnmarket}, onMarket={model.onMarket}", "DEBUG", logName);
                                    if (model.pauseTB)
                                    {
                                        this.paused = true;
                                        CommonFunctions.AddStatusMessage("Trading Brain paused by model", "INFO", logName);
                                    }

                                    if (this.currentGRIDLTrade != null) { CommonFunctions.AddStatusMessage($"Current Long dealid = {this.currentGRIDLTrade.dealId}", "INFO", logName); }
                                    if (this.currentGRIDSTrade != null) { CommonFunctions.AddStatusMessage($"Current Short dealid = {this.currentGRIDSTrade.dealId}", "INFO", logName); }

                                    List<tradeItem> openLTrades = await model.thisModel.gridLTrades.DeepCopyAsync();
                                    List<tradeItem> openSTrades = await model.thisModel.gridSTrades.DeepCopyAsync();

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
                                        CommonFunctions.AddStatusMessage($"GRID Long Trades - Num Trades : {openLTrades.Count}, Sum quantity : {Math.Round(openLTrades.Sum(x => x.quantity), 2)}");

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

                                        requestedTrade reqTrade = new()
                                        {
                                            dealType = "POSITION",
                                            dealReference = await PlaceDeal("long", model.modelVar.quantity, 0, this._igContainer.creds.igAccountId)
                                        };
                                        requestedTrades.Add(reqTrade);
                                    }
                                    else
                                    {
                                        if (model.sellLong)
                                        {
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLong", the_app_db);
                                            CommonFunctions.AddStatusMessage("SellLong activated", "INFO", logName);
                                            closeAttemptCount = 1;
                                            _ = await CloseDealEpic("long", openLTrades.Sum(x => x.quantity), this.epicName, this._igContainer.creds.igAccountId);
                                        }
                                        if (model.sellLongPartial && model.thisModel.gridLTradesToClose.Count > 0)
                                        {
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellLongPartial", the_app_db);
                                            CommonFunctions.AddStatusMessage("SellLongPartial activated", "INFO", logName);
                                            closeAttemptCount = 1;
                                            _ = await CloseDealEpicPartial("long", this.epicName, this._igContainer.creds.igAccountId);
                                        }
                                    }



                                    if (model.sellShort)
                                    {
                                        CommonFunctions.AddStatusMessage("SellShort activated", "INFO", logName);
                                        TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "SellShort", the_app_db);

                                        requestedTrade reqTrade = new()
                                        {
                                            dealType = "POSITION",
                                            dealReference = await PlaceDeal("short", model.modelVar.quantity, 0, this._igContainer2.creds.igAccountId)
                                        };
                                        requestedTrades.Add(reqTrade);

                                    }
                                    else
                                    {
                                        if (model.buyShort)
                                        {
                                            TradingBrain.Models.CommonFunctions.SaveLog("Info", "RunCode", "BuyShort", the_app_db);
                                            CommonFunctions.AddStatusMessage("BuyShort activated", "INFO", logName);
                                            closeAttemptCount = 1;
                                            _ = await CloseDealEpic("short", openSTrades.Sum(x => x.quantity), this.epicName, this._igContainer2.creds.igAccountId);
                                        }
                                    }



                                    //reset any deal variables that could have been placed by the RunCode
                                    model.buyLong = false;
                                    model.buyShort = false;
                                    model.sellLong = false;
                                    model.sellLongPartial = false;
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
                                        _ = await CloseDealEpic("long", model.thisModel.gridLTrades.Sum(x => x.quantity), this.epicName, this._igContainer.creds.igAccountId);
                                        closeAttemptCount = 1;
                                    }
                                    if (closeAttemptCount >= 40 && model.thisModel.closedGridSTrades.Count > 0)
                                    {
                                        _ = await CloseDealEpic("short", model.thisModel.gridSTrades.Sum(x => x.quantity), this.epicName, this._igContainer2.creds.igAccountId);
                                    }
                                }

                                if (model.modelLogs.logs.Count > 0)
                                {
                                    ModelLog log = model.modelLogs.logs[0];
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
                                    currentStatus.strategyProfit = modelVar.strategyProfit;
                                    currentStatus.maxStrategyProfit = modelVar.maxStrategyProfit;
                                    currentStatus.deltaProfit = modelVar.deltaProfit;


                                    //send log to the website
                                    model.modelLogs.logs[0].epicName = this.epicName;
                                    if (the_app_db != null)
                                    {
                                        //Task taskA = Task.Run(() => CommonFunctions.SendBroadcast("Log", JsonConvert.SerializeObject(model.modelLogs.logs[0])));
                                        if (DateTime.UtcNow.Second % 15 == 0 || DateTime.UtcNow.Second == 0)
                                        {
                                            Task taskB = Task.Run(() => CommonFunctions.SendBroadcast("Status", JsonConvert.SerializeObject(currentStatus)));
                                        }
                                        //save log to the database
                                        //Container logContainer = the_app_db.GetContainer("ModelLogs");
                                        //await log.SaveDocument(logContainer);
                                        model.modelLogs.logs = [];
                                    }
                                }
                                //}
                            }
                            else
                            {
                                CommonFunctions.AddStatusMessage($"No candle formed for second starting at {_startTime} - current candle list at {this.candleList.Count}", "DEBUG", logName);
                            }

                            //_startTime = _startTime.AddSeconds(1);


                        }

                    }
                    catch (Exception ex)
                    {
                        AddStatusMessage($"Error - {ex.ToString()}", "ERROR");
                        Log log = new(the_app_db)
                        {
                            Log_Message = ex.ToString(),
                            Log_Type = "Error",
                            Epic = this.epicName,
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
        public async Task<DateTime> getPrevMAStartDate(DateTime candleStartDate, string epic)
        {
            DateTime getStartDate = DateTime.MinValue;
            if (model != null && model.candles != null && model.candles.currentCandle != null)
            {
                //getStartDate = model.candles.currentCandle.candleStart;


                int mm = model.candles.currentCandle.candleStart.Minute;
                int hh = model.candles.currentCandle.candleStart.Hour;
                if (mm <= 29) { mm = 29; } else { mm = 59; }
                getStartDate = new DateTime(model.candles.currentCandle.candleStart.Year, model.candles.currentCandle.candleStart.Month, model.candles.currentCandle.candleStart.Day, model.candles.currentCandle.candleStart.Hour, mm, model.candles.currentCandle.candleStart.Second, DateTimeKind.Utc).AddMinutes(-30);

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
                    if (getStartDate < new DateTime(2024, 11, 3, 0, 0, 0, DateTimeKind.Utc) && getStartDate > new DateTime(2024, 10, 28, 0, 0, 0, DateTimeKind.Utc))
                    {
                        isDaylight = true;
                    }
                    //bool isDaylight = TimeZoneInfo.Local.IsDaylightSavingTime(dtCurrentDate);
                    if (!isDaylight)
                    {
                        time1 = 21;

                    }

                    getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, time1, getStartDate.Minute, 0, DateTimeKind.Utc);

                }
            }
            return getStartDate;
        }
        public RunRet iGUpdate(UpdateMessage msg)
        {
            RunRet taskRet = new();

            switch (msg.updateType)
            {
                case "UPDATE":
                    AddStatusMessage($"Update Message: {msg.itemName} - {msg.updateData}", "INFO");
                    Task.Run(() => OpuUpdate(msg.updateData, msg.itemName));
                    break;
                case "CONFIRM":
                    AddStatusMessage($"Confirm Message: {msg.itemName} - {msg.updateData}", "INFO");
                    Task.Run(() => ConfirmUpdate(msg.updateData, msg.itemName));
                    break;

            }
            return taskRet;
        }
        public async Task OpuUpdate(string inputData, string itemName)
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
                        if (tsm.Reason != null && tsm.Reason != "")
                        {
                            tradeSubUpdate.reasonDescription = this.TradeErrors[tsm.Reason ?? ""];
                        }

                        if (tsm.Epic == this.epicName)
                        {
                            if (tsm.Status == "UPDATED" && currentTrade != null && this.model.thisModel.currentTrade != null)
                            {
                                clsTradeUpdate thisTrade;
                                tradeItem thisModelTrade;
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
                                            this.tb.lastRunVars = await this.model.modelVar.DeepCopyAsync();
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

                                    if (this.strategy == "GRID")
                                    {
                                        //Check for long first
                                        tradeItem? matchTrade = this.model.thisModel.gridLTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);
                                        //then check for short
                                        matchTrade ??= this.model.thisModel.gridSTrades.FirstOrDefault(t => t.tbDealId == tsm.DealId);

                                        if (matchTrade != null)
                                        {
                                            CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                            //clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                            CommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + matchTrade.tbDealId, "INFO");
                                            DateTime dtNow = DateTime.UtcNow;
                                            tradeItem dbTrade = matchTrade; // await this.GetTradeFromDB(tsm.DealId, this.strategy, this.resolution);
                                            dbTrade.candleSold = null;
                                            dbTrade.candleBought = null;

                                            clsTradeUpdate cTrade = new()
                                            {
                                                lastUpdated = dtNow,
                                                status = tsm.Status,
                                                dealStatus = tsm.DealStatus,
                                                level = Convert.ToDecimal(tsm.Level),
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
                                                dbTrade.tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * cTrade.size;

                                                this.model.sellLong = false;
                                                this.model.buyLong = false;



                                                if (this.model.modelLogs.logs.Count >= 1)
                                                {
                                                    this.model.modelLogs.logs[0].tradeType = "Long";
                                                    this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                    this.model.modelLogs.logs[0].quantity = dbTrade.quantity;
                                                    this.model.modelLogs.logs[0].tradePrice = dbTrade.sellPrice;
                                                    this.model.modelLogs.logs[0].tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * cTrade.size;
                                                }



                                            }
                                            else
                                            {
                                                dbTrade.buyPrice = Convert.ToDecimal(cTrade.level);
                                                dbTrade.purchaseDate = dtNow;
                                                dbTrade.tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * cTrade.size;

                                                this.model.sellShort = false;
                                                this.model.buyShort = false;
                                                this.model.shortOnMarket = false;

                                                if (this.model.modelLogs.logs.Count >= 1)
                                                {
                                                    this.model.modelLogs.logs[0].tradeType = "Short";
                                                    this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                    this.model.modelLogs.logs[0].quantity = dbTrade.quantity;
                                                    this.model.modelLogs.logs[0].tradePrice = dbTrade.buyPrice;
                                                    this.model.modelLogs.logs[0].tradeValue = (dbTrade.sellPrice - dbTrade.buyPrice) * cTrade.size;
                                                }


                                            }

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


                                            this.model.modelVar.numCandlesOnMarket = 0;

                                            // update the strategy profit and max strategy profit after each sell.
                                            this.model.modelVar.strategyProfit += dbTrade.tradeValue;
                                            this.model.modelVar.deltaProfit += dbTrade.tradeValue;

                                            if (dbTrade.tradeValue > 0)
                                            {
                                                this.model.modelVar.maxStrategyProfit = Math.Max(this.model.modelVar.maxStrategyProfit, this.model.modelVar.deltaProfit);
                                            }
                                            //if (this.model.modelVar.strategyProfit > this.model.modelVar.maxStrategyProfit) { this.model.modelVar.maxStrategyProfit = this.model.modelVar.strategyProfit; }
                                            //this.tb.lastRunVars = await this.model.modelVar.DeepCopyAsync();
         

                                            await tradeSubUpdate.Add(this.the_app_db);

                                            this.model.thisModel.currentTrade = null;
                                            this.currentTrade = null;

                                            if (matchTrade != null)
                                            {
                                                if (tsm.Direction == "BUY")
                                                {
                                                    bool closingPartial = false;
                                                    this.model.thisModel.closedGridLTrades.Add(matchTrade);
                                                    this.model.thisModel.gridLTrades.Remove(matchTrade);
                                                    if (this.model.thisModel.gridLTradesToClose.Count > 0) { 
                                                        this.model.thisModel.gridLTradesToClose.Remove(matchTrade);
                                                        closingPartial = true; 
                                                    }
                                                    this.model.modelVar.carriedForwardLoss = Math.Max(this.model.modelVar.carriedForwardLoss - (double)matchTrade.tradeValue, 0);

                                                    CommonFunctions.AddStatusMessage($"closedGridLTrades  = {this.model.thisModel.closedGridLTrades.Count} : gridLTrades = {this.model.thisModel.gridLTrades.Count} : gridLTradesToClose = {this.model.thisModel.gridLTradesToClose.Count}  ", "DEBUG");
                                                    CommonFunctions.SendBroadcast("SellingLongGrid", matchTrade.BOLLI_ID + "|" + matchTrade.epic);

                                                    if (this.model.thisModel.gridLTrades.Count == 0 || closingPartial && this.model.thisModel.gridLTradesToClose.Count == 0)
                                                    {
                                                        CommonFunctions.AddStatusMessage($"closeAttemptCount going from {this.closeAttemptCount} to 0", "DEBUG");
                                                        this.closeAttemptCount = 0;
                                                        //CommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(this.model.thisModel.closedGridLTrades));
                                                        CommonFunctions.SendBroadcast("SoldLongGrid", matchTrade.BOLLI_ID + "|" + matchTrade.epic);

                                                        // calc strategyprofit

                                                        //this.model.thisModel.closingGridLTrade = false;
                                                        
                                                        if (!closingPartial)
                                                        {
                                                            decimal thisEventValue = this.model.thisModel.closedGridLTrades.Sum(x => x.tradeValue);
                                                            if (thisEventValue < 0)
                                                            {
                                                                this.model.modelVar.deltaProfit = thisEventValue;
                                                                this.model.modelVar.maxStrategyProfit = 0;
                                                                clsCommonFunctions.AddStatusMessage($"Resetting maxStrategyProfit to 0 due to loss of {thisEventValue} - deltaProfit set to {thisEventValue}", "INFO");
                                                            }
                                                            this.model.thisModel.currentGRIDLTrade = null;
                                                            this.currentGRIDLTrade = null;
                                                            this.model.onMarket = false;
                                                            this.model.longOnmarket = false;
                                                        }

                                                        this.model.thisModel.closedGridLTrades.Clear();
                                                        this.model.sellLongPartial = false;
                                                        // Save the tb settings to include the strategyprofit values.



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
                                                    CommonFunctions.SendBroadcast("BuyingShortGrid", matchTrade.BOLLI_ID + "|" + matchTrade.epic);
                                                    if (this.model.thisModel.gridSTrades.Count == 0)
                                                    {
                                                        CommonFunctions.AddStatusMessage($"closeAttemptCount going from {this.closeAttemptCount} to 0", "DEBUG");
                                                        this.closeAttemptCount = 0;
                                                        //CommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(this.model.thisModel.closedGridSTrades));

                                                        CommonFunctions.SendBroadcast("BuyShortGrid", matchTrade.BOLLI_ID + "|" + matchTrade.epic);
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
                                            this.tb.lastRunVars = await this.model.modelVar.DeepCopyAsync();
                                            // Save the trading brain settings to take into account all that has happened
                                            _ = this.tb.SaveDocument(this.the_app_db);

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
                                        clsTradeUpdate? thisTrade;
                                        tradeItem? thisModelTrade;

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
                                                        thisModelTrade.tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * thisTrade.size;

                                                        this.model.sellLong = false;
                                                        this.model.sellLongPartial = false;
                                                        this.model.buyLong = false;
                                                        this.model.longOnmarket = false;

                                                        if (this.model.modelLogs.logs.Count >= 1)
                                                        {
                                                            this.model.modelLogs.logs[0].tradeType = "Long";
                                                            this.model.modelLogs.logs[0].tradeAction = "Sell";
                                                            this.model.modelLogs.logs[0].quantity = thisModelTrade.quantity;
                                                            this.model.modelLogs.logs[0].tradePrice = thisModelTrade.sellPrice;
                                                            this.model.modelLogs.logs[0].tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * thisTrade.size;
                                                        }
                                                        CommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(thisModelTrade));
                                                    }
                                                    else
                                                    {
                                                        CommonFunctions.AddStatusMessage("deleting sell", "INFO");
                                                        thisModelTrade.buyPrice = Convert.ToDecimal(thisTrade.level);
                                                        thisModelTrade.purchaseDate = dtNow;
                                                        thisModelTrade.tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * thisTrade.size;
                                                        this.model.buyShort = false;
                                                        this.model.sellShort = false;
                                                        this.model.shortOnMarket = false;
                                                        if (this.model.modelLogs.logs.Count >= 1)
                                                        {
                                                            this.model.modelLogs.logs[0].tradeType = "Short";
                                                            this.model.modelLogs.logs[0].tradeAction = "Buy";
                                                            this.model.modelLogs.logs[0].tradePrice = thisModelTrade.buyPrice;
                                                            this.model.modelLogs.logs[0].tradeValue = (thisModelTrade.sellPrice - thisModelTrade.buyPrice) * thisTrade.size;
                                                        }
                                                        CommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(thisModelTrade));
                                                    }
                                                    this.model.sellLong = false;
                                                    this.model.sellLongPartial = false;
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
                                                        model.modelVar.carriedForwardLoss = model.modelVar.carriedForwardLoss + Math.Abs(nettPosition);
                                                        model.modelVar.quantityMultiplier = 1;
                                                    }
                                                    else
                                                    {
                                                        double newGain = Math.Max(nettPosition - model.modelVar.carriedForwardLoss, 0);
                                                        CommonFunctions.AddStatusMessage($"newGain  Max({nettPosition} - {model.modelVar.carriedForwardLoss} , 0 ) =  {newGain}", "DEBUG", logName);
                                                        model.modelVar.carriedForwardLoss = Math.Max(model.modelVar.carriedForwardLoss - Math.Abs(nettPosition), 0);

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

                                                    this.tb.lastRunVars = await this.model.modelVar.DeepCopyAsync();
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
                                                //thisModelTrade = null;
                                                //thisTrade = null;
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
                                    if (requestedTrades.FirstOrDefault(x => x.dealReference == tsm.DealReference) != null)
                                    {
                                        osDealRef = tsm.DealReference;
                                    }
                                    else
                                    {
                                        CommonFunctions.AddStatusMessage($"No matching requested trade found for DealRef: {tsm.DealReference}", "WARNING");
                                    }
                                    if (tsm.DealReference == osDealRef || this.strategy == "GRID" && osDealRef == "")
                                    {
                                        CommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                        CommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, this.the_app_db);
                                        this.newDealReference = "";

                                        //Not on market so this must be a new current trade
                                        DateTime thisDate = DateTime.UtcNow;


                                        clsTradeUpdate thisTrade = new()
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

                                        tradeItem thisModelTrade = new()
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

                                            if (this.strategy != "GRID")
                                            {
                                                            CommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(thisModelTrade));
                                            }
                                        }
                                        else
                                        {
                                            thisModelTrade.longShort = "Short";
                                            thisModelTrade.sellPrice = thisTrade.level;
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
                                            if (this.strategy != "GRID")
                                            {
                                                     CommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(thisModelTrade));
                                            }
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

                                        requestedTrades.RemoveAll(x => x.dealReference == tsm.DealReference);

                                        if (strategy == "GRID")
                                        {
                                            if (tsm.Direction == "BUY")
                                            {
                                                this.currentGRIDLTrade = await thisTrade.DeepCopyAsync();
                                                this.model.thisModel.currentGRIDLTrade = await thisModelTrade.DeepCopyAsync();
                                                this.model.thisModel.gridLTrades.Add(await thisModelTrade.DeepCopyAsync());
                                                CommonFunctions.AddStatusMessage($"Current long trade set in model - DealID: {this.model.thisModel.currentGRIDLTrade.tbDealId}, DealRef: {this.model.thisModel.currentGRIDLTrade.tbDealReference}", "INFO", logName);
                                                CommonFunctions.AddStatusMessage($"Current long trade set in local - DealID: {this.currentGRIDLTrade.dealId}, DealRef: {this.currentGRIDLTrade.dealReference}", "INFO", logName);

                                                CommonFunctions.SendBroadcast("BuyLongGrid", thisModelTrade.BOLLI_ID + "|" + thisModelTrade.epic);
     
                                            }
                                            else
                                            {
                                                this.currentGRIDSTrade = await thisTrade.DeepCopyAsync();
                                                this.model.thisModel.currentGRIDSTrade = await thisModelTrade.DeepCopyAsync();
                                                this.model.thisModel.gridSTrades.Add(await thisModelTrade.DeepCopyAsync());
                                                CommonFunctions.AddStatusMessage($"Current short trade set in model - DealID: {this.model.thisModel.currentGRIDSTrade.tbDealId}, DealRef: {this.model.thisModel.currentGRIDSTrade.tbDealReference}", "INFO", logName);
                                                CommonFunctions.AddStatusMessage($"Current short trade set in local - DealID: {this.currentGRIDSTrade.dealId}, DealRef: {this.currentGRIDSTrade.dealReference}", "INFO", logName);

                                                CommonFunctions.SendBroadcast("SellShortGrid", thisModelTrade.BOLLI_ID + "|" + thisModelTrade.epic);
     
                                            }
                                            this.tb.lastRunVars = await this.model.modelVar.DeepCopyAsync();
                                            _ = this.tb.SaveDocument(this.the_app_db);
                                        }
                                        else
                                        {
                                            this.currentTrade = await thisTrade.DeepCopyAsync();
                                            this.model.thisModel.currentTrade = await thisModelTrade.DeepCopyAsync();
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
        public async Task ConfirmUpdate(string inputData, string itemName)
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
                        if (tsm.Reason != null && tsm.Reason != "")
                        {
                            tradeSubUpdate.reasonDescription = this.TradeErrors[tsm.Reason ?? ""];
                        }

                        tradeSubUpdate.updateType = tsm.TradeType;

                        if (tsm.Epic == this.epicName)
                        {

                            CommonFunctions.AddStatusMessage($"CONFIRM - deal reference = {tsm.DealReference},   deal status = {tsm.Status}");

                            await tradeSubUpdate.Add(this.the_app_db);

                            // If this is a deletion, then update the trade record (previously updated from the OPU message) with the corect closing price. This is because IG changed the OPU message to return only the opening price!!
                            if (tsm.Status == "CLOSED" && tsm.Reason == "SUCCESS")
                            {
                                // empty
                            }


                            if (tsm.Status == "OPEN" && tsm.Reason == "SUCCESS")
                            {
                                // trade/order opened successfully
                                CommonFunctions.AddStatusMessage($"CONFIRM - successful", "INFO");
                            }

                            if (tsm.Status == null && tsm.Reason != "SUCCESS")
                            {
                                // trade/order not successful (could be update or open or delete)

                                CommonFunctions.AddStatusMessage($"CONFIRM - failed - - {tsm.Reason} - {this.TradeErrors[tsm.Reason ?? ""]}", "INFO");


                                if (this.model != null)
                                {
                                    CommonFunctions.AddStatusMessage($"CONFIRM - Resetting values due to  failure", "INFO");
                                    this.model.sellShort = false;
                                    this.model.sellLong = false;
                                    this.model.sellLongPartial = false;
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
                    //DateTime dtNow = DateTime.UtcNow;
                    //clsCommonFunctions.AddStatusMessage(dtNow.ToString("o") + " Sleeping....");
                }

                // Unsubscriber. Commented out for now but may need to add later.

                //UnsubscribeFromWatchlistInstruments();
                //UnsubscribefromTradeSubscription();
            }
            catch (Exception e)
            {
                Log log = new(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "WaitForChanges"
                };
                await log.Save();
            }
            return ret;

        }
        public async Task setupMessaging()
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
                    if (igWebApiConnectionConfig != null && igWebApiConnectionConfig.Count > 0)
                    {
                        url = igWebApiConnectionConfig["MessagingEndPoint"] ?? "";
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
                    //Console.WriteLine("message reveived " + DateTime.UtcNow.ToString() + " - " + obj.messageType);
                    switch (obj.messageType)
                    {
                        case "Ping":
                            //clientMessage msg = JsonConvert.DeserializeObject<clientMessage>(obj.messageValue);
                            PingMessage msg = new()
                            {
                                epicName = this.epicName
                            };

                            CommonFunctions.SendMessage(obj.messageValue, "Ping", JsonConvert.SerializeObject(msg), the_app_db);
                            break;

                        case "Status":
                            if (obj.messageValue.Contains(currentStatus.epicName))
                            {
                                //AddStatusMessage($"status recd - {currentStatus.epicName}-{currentStatus.strategy} - {obj.messageValue}", "INFO");
                                CommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus), the_app_db);
                                //AddStatusMessage($"status sent - {currentStatus.epicName}-{currentStatus.strategy} - {obj.messageValue}", "INFO");
                            }
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

                            if (model != null && currentStatus != null)
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

                        case "ClearMaxDrop":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("ClearMaxDrop request received", "INFO", logName);
                            paused = false;
                            pausedAfterNGL = false;

                            this.tb.lastRunVars.maxDropFlag = 0;
                            _ = await this.tb.SaveDocument(the_app_db);

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
                                ModelVarsChange? newVars = JsonConvert.DeserializeObject<ModelVarsChange>(obj.messageValue) ?? new ModelVarsChange();

                                if (newVars.baseQuantity > 0)
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity to use = " + newVars.baseQuantity, "INFO", logName);
                                    tb.lastRunVars.baseQuantity = newVars.baseQuantity;
                                    tb.lastRunVars.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    tb.lastRunVars.minQuantity = newVars.baseQuantity;
                                    model.modelVar.baseQuantity = newVars.baseQuantity;
                                    model.modelVar.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
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

                                // Save the last run vars into the TB settings table
                                Task<bool> res = tb.SaveDocument(the_app_db);
                                Task taskF = Task.Run(() => CommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus)));
                                CommonFunctions.AddStatusMessage("New values saved", "INFO", logName);
                            }
                            break;

                        case "ChangeQuantityRSI":
                            //clsCommonFunctions.SendMessage(obj.messageValue, "Status", JsonConvert.SerializeObject(currentStatus));
                            CommonFunctions.AddStatusMessage("ChangeQuantityRSI request received", "INFO", logName);
                            if (obj.messageValue != "" && tb != null && model != null && currentStatus != null)
                            {
                                ModelVarsChange? newVars = JsonConvert.DeserializeObject<ModelVarsChange>(obj.messageValue) ?? new ModelVarsChange();

                                if (newVars.baseQuantity > 0)
                                {
                                    CommonFunctions.AddStatusMessage("New baseQuantity to use = " + newVars.baseQuantity, "INFO", logName);
                                    tb.lastRunVars.baseQuantity = newVars.baseQuantity;
                                    tb.lastRunVars.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    tb.lastRunVars.minQuantity = newVars.baseQuantity;
                                    model.modelVar.baseQuantity = newVars.baseQuantity;
                                    model.modelVar.maxQuantity = newVars.baseQuantity * tb.lastRunVars.maxQuantityMultiplier;
                                    model.modelVar.minQuantity = newVars.baseQuantity;
                                    currentStatus.quantity = newVars.baseQuantity;
                                    currentStatus.baseQuantity = newVars.baseQuantity;
                                    currentStatus.minQuantity = newVars.baseQuantity;
                                    if (this.strategy == "GRID")
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

                                CommonFunctions.AddStatusMessage("New strategyProfit to use = " + newVars.strategyProfit, "INFO", logName);
                                tb.lastRunVars.strategyProfit = newVars.strategyProfit;
                                model.modelVar.strategyProfit = newVars.strategyProfit;
                                currentStatus.strategyProfit = newVars.strategyProfit;

                                CommonFunctions.AddStatusMessage("New maxStrategyProfit to use = " + newVars.maxStrategyProfit, "INFO", logName);
                                tb.lastRunVars.maxStrategyProfit = newVars.maxStrategyProfit;
                                model.modelVar.maxStrategyProfit = newVars.maxStrategyProfit;
                                currentStatus.maxStrategyProfit = newVars.maxStrategyProfit;

                                CommonFunctions.AddStatusMessage("New deltaProfit to use = " + newVars.deltaProfit, "INFO", logName);
                                tb.lastRunVars.deltaProfit = newVars.deltaProfit;
                                model.modelVar.deltaProfit = newVars.deltaProfit;
                                currentStatus.deltaProfit = newVars.deltaProfit;

                                if (this.strategy == "GRID" && (newVars.var0 > 0 || newVars.var1 > 0 || newVars.var2 > 0 || newVars.var3 > 0 || newVars.var4 > 0 || newVars.var5 > 0 || newVars.var6 > 0 || newVars.var7 > 0 || newVars.var8 > 0))
                                {
                                    // Get the input settings from the last run optimzerundata

                                    OptimizeRunData optData = await IGModels.clsCommonFunctions.GetLatestOptimizeRunData_RSI(the_app_db, this.epicName, 0, this.strategy, this.resolution);

                                    if (newVars.var0 > 0)
                                    {
                                        CommonFunctions.AddStatusMessage("New var0 to use = " + newVars.var0, "INFO", logName);
                                        tb.runDetails.inputs_RSI[0].var0 = newVars.var0;
                                    }
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
                                    if (newVars.var7 > 0)
                                    {
                                        CommonFunctions.AddStatusMessage("New var7 to use = " + newVars.var7, "INFO", logName);
                                        tb.runDetails.inputs_RSI[0].var7 = newVars.var7;
                                    }
                                    if (newVars.var8 > 0)
                                    {
                                        CommonFunctions.AddStatusMessage("New var8 to use = " + newVars.var8, "INFO", logName);
                                        tb.runDetails.inputs_RSI[0].var8 = newVars.var8;
                                    }
                                    Container optContainer = the_app_db.GetContainer("OptimizeRunData");
                                    optData.inputs_RSI = await tb.runDetails.inputs_RSI.DeepCopyAsync();
                                    await optData.SaveDocument(the_app_db, optContainer);

                                }
                            }

                            // Save the last run vars into the TB settings table
                            if (the_app_db != null && tb != null)
                            {
                                await tb.SaveDocument(the_app_db);
                                CommonFunctions.SendBroadcast("QuantityChanged", JsonConvert.SerializeObject(currentStatus));
                                CommonFunctions.AddStatusMessage("New values saved", "INFO", logName);
                            }



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
                    Log log = new(the_app_db)
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
                Log log = new(the_app_db)
                {
                    Log_Message = ex.ToString(),
                    Log_Type = "Error",
                    Log_App = "setupMessaging"
                };
                await log.Save();
            }
        }
        public static async Task<CandleMovingAverage> Get_MinuteMovingAverageNum30v1(Database the_db, Container container, IG_Epic epic, DateTime CandleStart, int num, Database the_app_db, List<ExchangeClosedItem> exchangeClosedDates)
        {

            CandleMovingAverage ret = new();
            List<clsMinuteCandle> resp = [];
            //List<clsMinuteCandle> resp = new List<clsMinuteCandle>();

            List<string> lstDates = [];

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
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, 20, getStartDate.Minute, 0, DateTimeKind.Utc);
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
                            getStartDate = new DateTime(getStartDate.Year, getStartDate.Month, getStartDate.Day, hh, mm, getStartDate.Second, DateTimeKind.Utc);

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

                clsMinuteCandle item = new();
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
                        Log log = new(the_app_db)
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
                    Log log = new(the_app_db)
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
                    Log log = new(the_app_db)
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
                Log log = new(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuteMovingAverageNum30v1"
                };
                await log.Save();
            }

            return (ret);
        }
        public static async Task<clsMinuteCandle> Get_MinuteCandle(Database the_db, Container container, IG_Epic epic, DateTime CandleStart)
        {

            clsMinuteCandle ret = new();
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
                    Log log = new(the_db)
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
                Log log = new(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_MinuiteCandle"
                };
                await log.Save();
            }

            return (ret);
        }
        public static async Task<decimal> Get_Minute_SMA(Database the_db, string epic, int numCandles)
        {

            decimal ret = 0;
 
            List<TypicalValue> lstPrices = new();

            try
            {
                Container container = the_db.GetContainer("Candles_RSI");

                var parameterizedQuery = new QueryDefinition(
                    query: "SELECT top @numCandles c.closePrice.ask as 'ask', c.closePrice.bid as 'bid', (c.closePrice.ask + c.closePrice.bid) / 2 as 'typicalPrice' FROM  c WHERE c.epic= @epicName AND c.resolution = 'MINUTE' ORDER BY c.startDate DESC "
                )
                .WithParameter("@epicName", epic)
                .WithParameter("@numCandles", numCandles);


                using FeedIterator<TypicalValue> filteredFeed = container.GetItemQueryIterator<TypicalValue>(
                    queryDefinition: parameterizedQuery
                );

                while (filteredFeed.HasMoreResults)
                {
                    FeedResponse<TypicalValue> response = await filteredFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (TypicalValue item in response)
                    {
                        lstPrices.Add(item);
                    }
                }

                if (lstPrices.Count > 0)
                {
                    ret = Math.Round(lstPrices.Average(x => x.bid), 2);
                }
                //epic = await container.ReadItemAsync<IG_Epic>(id, new PartitionKey(id), null, default);

            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new(the_db)
                    {
                        Log_Message = de.ToString(),
                        Log_Type = "Error",
                        Log_App = "Get_Minute_SMA"
                    };
                    await log.Save();
                }

            }
            catch (Exception e)
            {
                Log log = new(the_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "Get_Minute_SMA"
                };
                await log.Save();
            }

            return (ret);
        }
        public class TypicalValue
        {
            public decimal bid { get; set; }
            public decimal ask { get; set; }
            public decimal typicalPrice { get; set; }
            public TypicalValue()
            {
                bid = 0;
                ask = 0;
                typicalPrice = 0;
            }
            public TypicalValue(decimal _bid, decimal _ask)
            {
                bid = _bid;
                ask = _ask;
                typicalPrice = (bid + ask) / 2;
            }
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
                    Log log = new(the_db)
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
                Log log = new(the_db)
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
                                    this.model.thisModel.currentGRIDLTrade.stopLossValue = this.model.stopPrice;

                                    if (tsm.direction == "BUY")
                                    {
                                        this.model.longOnmarket = true;
                                        this.model.buyShort = false;
                                    }
                                    else
                                    {
                                        this.model.shortOnMarket = true;
                                        this.model.buyLong = false;
                                    }

                                    this.model.thisModel.currentGRIDLTrade.stopLossValue = this.model.stopPrice;

                                    this.model.onMarket = true;


                                    tradeItem? thisTde = this.model.thisModel.gridLTrades.Find(x => x.tbDealId == this.model.thisModel.currentGRIDLTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.gridLTrades.Add(thisTrade);
                                    }

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
                                    }
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
                                    this.model.thisModel.currentGRIDSTrade.stopLossValue = this.model.stopPrice;

                                    if (tsm.direction == "BUY")
                                    {
                                        this.model.longOnmarket = true;
                                        this.model.buyShort = false;
                                    }
                                    else
                                    {
                                        this.model.shortOnMarket = true;
                                        this.model.buyLong = false;
                                    }

                                    this.model.thisModel.currentGRIDSTrade.stopLossValue = this.model.stopPrice;

                                    this.model.onMarket = true;


                                    tradeItem? thisTde = this.model.thisModel.gridSTrades.Find(x => x.tbDealId == this.model.thisModel.currentGRIDSTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.gridSTrades.Add(thisTrade);
                                    }
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
                                    }
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
                                    this.model.thisModel.currentTrade.stopLossValue = this.model.stopPrice;

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

                                    this.model.thisModel.currentTrade.stopLossValue = this.model.stopPrice;

                                    this.model.onMarket = true;


                                    tradeItem? thisTde = this.model.thisModel.bolliTrades.Find(x => x.tbDealId == this.model.thisModel.currentTrade.tbDealId);
                                    if (thisTde == null)
                                    {
                                        this.model.thisModel.bolliTrades.Add(thisTrade);
                                    }
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
                                        this.model.thisModel.currentTrade.stopLossValue = this.model.stopPrice;

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

                                        this.model.thisModel.currentTrade.stopLossValue = this.model.stopPrice;

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
            tradeItem ret = new();

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
            }
            catch (CosmosException de)
            {
                if (de.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Log log = new(the_app_db)
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
                Log log = new(the_app_db)
                {
                    Log_Message = e.ToString(),
                    Log_Type = "Error",
                    Log_App = "GetTradeFromDB"
                };
                await log.Save();
            }

            return ret;

        }
    }


}