using IGModels;
using IGWebApiClient.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NLog;
using Org.BouncyCastle.Tsp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public interface IWorkerEvent { }
    public class StartEvent : IWorkerEvent { }
    public class StopEvent : IWorkerEvent { }



    public class EventParams
    {
        public string epic { get; set; }
        public string strategy { get; set; }
        public string resolution { get; set; }
        public IGContainer igContainer { get; set; }
        public EventParams()
        {
            epic = "";
            strategy = "";
            resolution = "";
            igContainer = new IGContainer();
        }
        public EventParams(string _epic, string _strategy, string _resolution, IGContainer _igContainer)
        {
            epic = _epic;
            strategy = _strategy;
            resolution = _resolution;   
            igContainer = _igContainer;
        }
    }
    public class updateMessage
    {
        public string updateType { get; set; }
        public string updateData { get; set; }
        public string itemName { get; set; }
        public updateMessage()
        {
            updateData = "";
            itemName = "";
            updateType = "";
        }
    }
    public class PrintOPUMessageEvent : IWorkerEvent
    {
        public updateMessage Message { get; }
        public PrintOPUMessageEvent(updateMessage message) => Message = message;
    }

    public class PrintCONFIRMMessageEvent : IWorkerEvent
    {
        public updateMessage Message { get; }
        public PrintCONFIRMMessageEvent(updateMessage message) => Message = message;
    }

    public class EventWorker
    {
        private readonly BlockingCollection<IWorkerEvent> _queue = new();
        public readonly Thread _thread;
        public MainApp _thisApp = null;
        public ILogger tbLog ;
        public string logName = "";
        public EventWorker(EventParams pams )
        {
            //_thread = new Thread(Run)
            _thread = new Thread(() => Run(pams))

            {
                IsBackground = true,
                Name = pams.epic + "|" + pams.strategy + "|" + pams.resolution
            };
             _thread.Start();
            logName = IGModels.clsCommonFunctions.GetLogName(pams.epic, pams.strategy, pams.resolution);
            //threads.Add(_thread);
        }
        private void Run(EventParams pams)
        {
            //Microsoft.Azure.Cosmos.Container? EpicContainer;
            //Microsoft.Azure.Cosmos.Container? hourly_container;
            //Microsoft.Azure.Cosmos.Container minute_container;
            //Microsoft.Azure.Cosmos.Container? TicksContainer;
            //Microsoft.Azure.Cosmos.Container? candlesContainer;
            //Microsoft.Azure.Cosmos.Container? trade_container;
            //Database? the_db;
            //Database? the_app_db;


            System.Timers.Timer t;

            //MainApp? app = null;
            
           EventParams pms = (EventParams)pams;
            //string nme = pms.strategy;
            //if (pms.strategy == "RSI")
            //{
            //    nme += "_" + pms.resolution;
            //}
            //string logName = this.logName; // clsCommonFunctions.GetLogName(pms.epic, pms.strategy, pms.resolution);
            this.tbLog = LogManager.GetLogger(this.logName);
            //string epic = "IX.D.NASDAQ.CASH.IP";
            //string strategy = "SMA";
            //string resolution = "";

            //// See if an epic has been passed in. if not then default to NASDAQ
            //if (Environment.GetCommandLineArgs().Length >= 2)
            //{
            //    epic = Environment.GetCommandLineArgs()[1].ToUpper();
            //}


            //// See if a strategy and resolution has been passed in, otherwise use default. //

            //if (Environment.GetCommandLineArgs().Length >= 4)
            //{
            //    strategy = Environment.GetCommandLineArgs()[2].ToUpper();
            //    resolution = Environment.GetCommandLineArgs()[3].ToUpper();
            //}

            // Set up the logging //

            //var config = new NLog.Config.LoggingConfiguration();

            //var filename = "DEBUG-" + DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + "-" + DateTime.UtcNow.Hour + ".txt";

            //string nme = pms.strategy;
            //if (pms.strategy == "RSI")
            //{
            //    nme += "_" + pms.resolution;
            //}
            //var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "c:/tblogs/App." + pms.epic + "." + nme + ".${shortdate}.txt", MaxArchiveDays = 31, KeepFileOpen = false, Layout = "${longdate} [${threadid}] |${level:uppercase=true}|${message}|${exception:format=toString}" };
            //var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            //logconsole.Layout = "${longdate} [${threadid}] |${level:uppercase=true}|${logger}|${message}|${exception:format=toString}";

            //config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            //config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            //NLog.LogManager.Configuration = config;


            //Logger tbLog = LogManager.GetCurrentClassLogger();
           // tbLog = LogManager.GetCurrentClassLogger();



            tbLog.Info("-------------------------------------------------------------");
            tbLog.Info($"-- TradingBrain started - strategy: {pms.strategy + " " + pms.resolution} : {pms.epic} --");
            tbLog.Info("-------------------------------------------------------------");

            tbLog.Info("Connecting to database....");

            Database? the_db = IGModels.clsCommonFunctions.Get_Database(pms.epic).Result;
            Database? the_app_db = IGModels.clsCommonFunctions.Get_App_Database(pms.epic).Result;


            if (the_db != null)
            {


                Container container = null;
                Container chart_container = null;
                Container trade_container = null;
                Container minute_container = null;
                Container TicksContainer = null;
                switch (pms.epic)
                {
                    case "IX.D.NIKKEI.DAILY.IP":
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks_NIKKEI");
                        TicksContainer = the_db.GetContainer("CandleTicks_NIKKEI");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");

                        break;

                    default:
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks");
                        TicksContainer = the_db.GetContainer("CandleTicks");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                        break;
                }

                //SetupDB(pms.epic);

                if (pms.strategy == "RSI" || 
                    pms.strategy == "REI" || 
                    pms.strategy == "RSI-ATR" ||
                    pms.strategy == "RSI-CUML" ||
                    pms.strategy == "CASEYC" ||
                    pms.strategy == "CASEYCSHORT" ||
                    pms.strategy == "CASEYCEQUITIES")
                {
                    minute_container = the_db.GetContainer("Candles_RSI");
                }
                else
                {
                    if (pms.epic == "IX.D.NIKKEI.DAILY.IP")
                    {
                        minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
                    }
                    else
                    {
                        minute_container = the_db.GetContainer("MinuteCandle");
                    }
                }
                    tbLog.Info("Initialising app");

                _thisApp = new MainApp(the_db, the_app_db, container, chart_container, pms.epic, minute_container, TicksContainer, trade_container, pms.igContainer, pms.strategy, pms.resolution);

                try
                {
                    foreach (var evt in _queue.GetConsumingEnumerable())
                    {
                        HandleEvent(evt);
                    }
                }
                catch (Exception ex)
                {
                    var a = 1;
                }

                if (_thisApp != null)
                {
                    tbLog.Info("Waiting for changes...");

                    int i = _thisApp.WaitForChanges().Result;
                }

                tbLog.Info("Exiting...");

                Environment.Exit(0);

                static void Exiting(Exception exception, MainApp app)
                {
                    //Put common cleanup code here (or at the end of the method)
                    if (app != null)
                    {
                        //app.UnsubscribeTidyUp();
                    }

                    if (exception == null)
                    {
                        clsCommonFunctions.AddStatusMessage("normal proc exit", "INFO");
                    }
                    else
                    {
                        clsCommonFunctions.AddStatusMessage("unhandled exception: " + exception.GetType().Name, "ERROR");
                    }
                }




                //static double GetInterval()
                //{
                //    DateTime now = DateTime.Now;
                //    //return ((60 - now.Second) * 1000 - now.Millisecond);
                //    return ((now.Second > 30 ? 120 : 60) - now.Second) * 1000 - now.Millisecond;
                //}

                //static void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
                //{
                //    //Console.WriteLine(DateTime.Now.ToString("o"));
                //    t.Interval = GetInterval();
                //    t.Start();
                //}


            }


        }
        public void PostEvent(IWorkerEvent evt)
        {
            _queue.Add(evt);
        }

        private void HandleEvent(IWorkerEvent evt)
        {
            switch (evt)
            {
                case StartEvent:
                    Console.WriteLine("StartEvent received.");
                    break;

                case StopEvent:
                    Console.WriteLine("StopEvent received.");
                    break;

                case PrintOPUMessageEvent msg:
                    Console.WriteLine($"Message: {msg.Message.itemName} - {msg.Message.updateData}");
                    OPUUpdate(msg.Message.updateData, msg.Message.itemName);
                    break;
                case PrintCONFIRMMessageEvent msg:
                    Console.WriteLine($"Message: {msg.Message.itemName} - {msg.Message.updateData}");
                    CONFIRMUpdate(msg.Message.updateData, msg.Message.itemName);
                    break;
                default:
                    Console.WriteLine("Unknown event type.");
                    break;
            }
        }

        public void Stop()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }

        public void currentTickData()
        {

        }
        public async void OPUUpdate(string inputData,string itemName)
        {
            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
                TradeSubUpdate tradeSubUpdate = (TradeSubUpdate)JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
                tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
                tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
                tradeSubUpdate.updateType = "OPU";
                if (tradeSubUpdate.epic == _thisApp.epicName)
                {
                    tradeSubUpdate.date = tradeSubUpdate.timestamp;
                    tsm.Channel = tradeSubUpdate.channel;
                    tsm.DealId = tradeSubUpdate.dealId;
                    tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
                    tsm.DealReference = tradeSubUpdate.dealReference;
                    tsm.DealStatus = tradeSubUpdate.dealStatus.ToString();
                    tsm.Direction = tradeSubUpdate.direction.ToString();
                    tsm.ItemName = itemName;
                    tsm.Epic = tradeSubUpdate.epic;
                    tsm.Expiry = tradeSubUpdate.expiry;
                    tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
                    tsm.Level = tradeSubUpdate.level;
                    tsm.Limitlevel = tradeSubUpdate.limitLevel;
                    tsm.Size = tradeSubUpdate.size;
                    tsm.Status = tradeSubUpdate.status.ToString();
                    tsm.StopLevel = tradeSubUpdate.stopLevel;
                    tsm.Reason = tradeSubUpdate.reason;
                    tsm.date = tradeSubUpdate.timestamp;
                    tsm.StopDistance = tradeSubUpdate.stopDistance;

                    tsm.TradeType = "OPU";
                    if (tsm.Reason != null)
                    {
                        if (tsm.Reason != "")
                        {
                            tradeSubUpdate.reasonDescription = _thisApp.TradeErrors[tsm.Reason];
                        }
                    }

                    if (tsm.Epic == _thisApp.epicName)
                    {
                        if (tsm.Status == "UPDATED")
                        {
                            // Deal has been updated, so save the new data and move on.
                            if (tsm.DealStatus == "ACCEPTED" && _thisApp.currentTrade != null)
                            {

                                //Only update if it is the current trade or is the supplementary trade that is affected (in case we have 2 trades running at the same time)
                                if (tsm.DealId == _thisApp.currentTrade.dealId)
                                {
                                    clsCommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                    clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);
                                    clsCommonFunctions.AddStatusMessage("Updating  - " + tsm.DealId + " - Current Deal = " + _thisApp.currentTrade.dealId, "INFO");
                                    await _thisApp.GetTradeFromDB(tsm.DealId, _thisApp.strategy, _thisApp.resolution);
                                    _thisApp.model.thisModel.currentTrade.candleSold = null;
                                    _thisApp.model.thisModel.currentTrade.candleBought = null;
                                    _thisApp.currentTrade.dealReference = tsm.DealReference;
                                    _thisApp.currentTrade.dealId = tsm.DealId;
                                    _thisApp.currentTrade.lastUpdated = tsm.date;
                                    _thisApp.currentTrade.status = tsm.Status;
                                    _thisApp.currentTrade.dealStatus = tsm.DealStatus;
                                    _thisApp.currentTrade.level = Convert.ToDecimal(tsm.Level);
                                    _thisApp.currentTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                    _thisApp.currentTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                    _thisApp.currentTrade.size = Convert.ToDecimal(tsm.Size);
                                    _thisApp.currentTrade.direction = tsm.Direction;
                                    //_thisApp.model.thisModel.currentTrade = new tradeItem();

                                    _thisApp.model.thisModel.currentTrade.tbDealId = tsm.DealId;
                                    _thisApp.model.thisModel.currentTrade.tbDealReference = tsm.DealReference;
                                    _thisApp.model.thisModel.currentTrade.tbDealStatus = tsm.DealStatus;
                                    _thisApp.model.thisModel.currentTrade.timestamp = DateTime.UtcNow;
                                    _thisApp.model.thisModel.currentTrade.tbReason = tsm.Status;
                                    if (_thisApp.epicName != "") { _thisApp.model.thisModel.currentTrade.epic = _thisApp.epicName; }
                                    if (_thisApp.modelID != "") { _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID; }
                                    _thisApp.model.thisModel.currentTrade.quantity = Convert.ToDouble(_thisApp.currentTrade.size);
                                    _thisApp.model.thisModel.currentTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.currentTrade.level) - Convert.ToDouble(_thisApp.currentTrade.stopLevel));
                                    _thisApp.model.stopPriceOld = Math.Abs(_thisApp.model.stopPrice);
                                    _thisApp.model.stopPrice = Math.Abs(_thisApp.model.thisModel.currentTrade.stopLossValue);

                                    _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);

                                    // Save the TBAudit
                                    IGModels.clsCommonFunctions.SaveTradeAudit(_thisApp.the_app_db, _thisApp.model.thisModel.currentTrade, (double)_thisApp.currentTrade.level, tsm.TradeType);

                                    // Save the last run vars into the TB settings table
                                    _thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                    _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                    clsCommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);

                                    await tradeSubUpdate.Add(_thisApp.the_app_db);

                                    //_thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                }
                                else
                                {
                                    if (_thisApp.suppTrade != null)
                                    {
                                        if (tsm.DealId == _thisApp.suppTrade.dealId)
                                        {
                                            clsCommonFunctions.AddStatusMessage("Updating supp trade  - " + tsm.DealId + " - Current Deal = " + _thisApp.suppTrade.dealId, "INFO");
                                            await _thisApp.GetTradeFromDB(tsm.DealId, _thisApp.strategy, _thisApp.resolution);
                                            _thisApp.model.thisModel.suppTrade.candleSold = null;
                                            _thisApp.model.thisModel.suppTrade.candleBought = null;
                                            _thisApp.suppTrade.dealReference = tsm.DealReference;
                                            _thisApp.suppTrade.dealId = tsm.DealId;
                                            _thisApp.suppTrade.lastUpdated = tsm.date;
                                            _thisApp.suppTrade.status = tsm.Status;
                                            _thisApp.suppTrade.dealStatus = tsm.DealStatus;
                                            _thisApp.suppTrade.level = Convert.ToDecimal(tsm.Level);
                                            _thisApp.suppTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                            _thisApp.suppTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                            _thisApp.suppTrade.size = Convert.ToDecimal(tsm.Size);
                                            _thisApp.suppTrade.direction = tsm.Direction;
                                            //_thisApp.model.thisModel.currentTrade = new tradeItem();

                                            _thisApp.model.thisModel.suppTrade.tbDealId = tsm.DealId;
                                            _thisApp.model.thisModel.suppTrade.tbDealReference = tsm.DealReference;
                                            _thisApp.model.thisModel.suppTrade.tbDealStatus = tsm.DealStatus;
                                            _thisApp.model.thisModel.suppTrade.timestamp = DateTime.UtcNow;
                                            _thisApp.model.thisModel.suppTrade.tbReason = tsm.Status;
                                            if (_thisApp.epicName != "") { _thisApp.model.thisModel.suppTrade.epic = _thisApp.epicName; }
                                            if (_thisApp.modelID != "") { _thisApp.model.thisModel.suppTrade.modelRunID = _thisApp.modelID; }
                                            _thisApp.model.thisModel.suppTrade.quantity = Convert.ToDouble(_thisApp.suppTrade.size);
                                            _thisApp.model.thisModel.suppTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.suppTrade.level) - Convert.ToDouble(_thisApp.suppTrade.stopLevel));
                                            //_thisApp.model.stopPriceOld = Math.Abs(_thisApp.model.stopPrice);
                                            //_thisApp.model.stopPrice = Math.Abs(_thisApp.model.thisModel.currentTrade.stopLossValue);

                                            _thisApp.model.thisModel.suppTrade.SaveDocument(_thisApp.trade_container);

                                            // Save the last run vars into the TB settings table
                                            //_thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                            //await _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                            clsCommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(_thisApp.model.thisModel.suppTrade), _thisApp.the_app_db);
                                            //_thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("UPDATE failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "UPDATE failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], _thisApp.the_app_db);
                                await tradeSubUpdate.Add(_thisApp.the_app_db);
                            }

                            //tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                        else if (tsm.Status == "DELETED")
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                // Deal has been closed (either by the software or by the stop being met).
                                //Only delete if it is the current trade, or it is a supplementary trade that is affected (in case we have 2 trades running at the same time)
                                if (_thisApp.currentTrade != null)
                                {
                                    if (tsm.DealId == _thisApp.currentTrade.dealId)
                                    {
                                        clsCommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                        clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);
                                        clsCommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + _thisApp.currentTrade.dealId, "INFO");
                                        DateTime dtNow = DateTime.UtcNow;
                                        await _thisApp.GetTradeFromDB(tsm.DealId, _thisApp.strategy, _thisApp.resolution);
                                        _thisApp.model.thisModel.currentTrade.candleSold = null;
                                        _thisApp.model.thisModel.currentTrade.candleBought = null;

                                        _thisApp.currentTrade.lastUpdated = dtNow;
                                        _thisApp.currentTrade.status = tsm.Status;
                                        _thisApp.currentTrade.dealStatus = tsm.DealStatus;
                                        _thisApp.currentTrade.level = Convert.ToDecimal(tsm.Level);
                                        _thisApp.currentTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                        _thisApp.currentTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                        _thisApp.currentTrade.channel = tsm.Channel;

                                        if (Convert.ToDecimal(tsm.Size) > 0)
                                        {
                                            _thisApp.currentTrade.size = Convert.ToDecimal(tsm.Size);
                                        }
                                        _thisApp.currentTrade.direction = tsm.Direction;

                                        _thisApp.model.thisModel.currentTrade.channel = tsm.Channel;
                                        //_thisApp.model.thisModel.currentTrade = new tradeItem();
                                        //_thisApp.model.thisModel.currentTrade.quantity = Convert.ToDouble(_thisApp.currentTrade.size);
                                        //_thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.level) - Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                        _thisApp.model.stopPrice = 0;// Math.Abs(_thisApp.model.thisModel.currentTrade.stopLossValue);
                                        _thisApp.model.stopPriceOld = 0;// _thisApp.model.stopPrice;

                                        _thisApp.model.thisModel.currentTrade.tradeEnded = dtNow;
                                        clsCommonFunctions.AddStatusMessage("tsm.Direction = " + tsm.Direction, "INFO");
                                        if (tsm.Direction == "BUY")
                                        {
                                            clsCommonFunctions.AddStatusMessage("deleting buy", "INFO");
                                            _thisApp.model.thisModel.currentTrade.sellPrice = Convert.ToDecimal(_thisApp.currentTrade.level);
                                            _thisApp.model.thisModel.currentTrade.sellDate = dtNow;
                                            _thisApp.model.thisModel.currentTrade.tradeValue = (_thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice) * (decimal)_thisApp.currentTrade.size;

                                            _thisApp.model.sellLong = false;
                                            _thisApp.model.buyLong = false;
                                            _thisApp.model.longOnmarket = false;

                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Long";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Sell";
                                                _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.currentTrade.quantity;
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.currentTrade.sellPrice;
                                                _thisApp.model.modelLogs.logs[0].tradeValue = (_thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice) * (decimal)_thisApp.currentTrade.size;
                                            }
                                            clsCommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);
                                        }
                                        else
                                        {
                                            clsCommonFunctions.AddStatusMessage("deleting sell", "INFO");
                                            _thisApp.model.thisModel.currentTrade.buyPrice = Convert.ToDecimal(_thisApp.currentTrade.level);
                                            _thisApp.model.thisModel.currentTrade.purchaseDate = dtNow;
                                            _thisApp.model.thisModel.currentTrade.tradeValue = (_thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice) * (decimal)_thisApp.currentTrade.size;
                                            _thisApp.model.buyShort = false;
                                            _thisApp.model.sellShort = false;
                                            _thisApp.model.shortOnMarket = false;
                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Short";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Buy";
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.currentTrade.buyPrice;
                                                _thisApp.model.modelLogs.logs[0].tradeValue = (_thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice) * (decimal)_thisApp.currentTrade.size;
                                            }
                                            clsCommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);
                                        }
                                        _thisApp.model.sellLong = false;
                                        _thisApp.model.buyLong = false;
                                        _thisApp.model.longOnmarket = false;
                                        _thisApp.model.buyShort = false;
                                        _thisApp.model.sellShort = false;
                                        _thisApp.model.shortOnMarket = false;


                                        _thisApp.model.modelVar.strategyProfit += _thisApp.model.thisModel.currentTrade.tradeValue;
                                        _thisApp.model.modelVar.numCandlesOnMarket = 0;
                                        // set the trade values in the next run of the code rather than right away so we can aggregate trades and supp trades if needs be
                                        _thisApp.lastTradeDeleted = true;
                                        _thisApp.lastTradeValue = (double)_thisApp.model.thisModel.currentTrade.tradeValue;

                                        //check if the last trade lost and was at max quantity. If so then we need to do a reset 
                                        clsCommonFunctions.AddStatusMessage($"Check if reset required - quantity = {_thisApp.model.thisModel.currentTrade.quantity}, maxQuantity = {_thisApp.model.modelVar.maxQuantity}, tradeValue = {_thisApp.model.thisModel.currentTrade.tradeValue}", "DEBUG");
                                        if ((_thisApp.model.thisModel.currentTrade.quantity + 1) >= _thisApp.model.modelVar.maxQuantity && _thisApp.model.thisModel.currentTrade.tradeValue < 0)
                                        {
                                            _thisApp.lastTradeMaxQuantity = true;
                                            clsCommonFunctions.AddStatusMessage($"Do reset next run - lastTradeMaxQuantity = {_thisApp.lastTradeMaxQuantity}", "DEBUG");
                                        }
                                        //if (_thisApp.model.thisModel.currentTrade.tradeValue <= 0)
                                        //{
                                        //    _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss + (double)Math.Abs(_thisApp.model.thisModel.currentTrade.tradeValue);
                                        //}
                                        //else
                                        //{
                                        //    _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss - (double)Math.Abs(_thisApp.model.thisModel.currentTrade.tradeValue);
                                        //    if (_thisApp.model.modelVar.carriedForwardLoss < 0) { _thisApp.model.modelVar.carriedForwardLoss = 0; }
                                        //    _thisApp.model.modelVar.currentGain += Math.Max((double)_thisApp.model.thisModel.currentTrade.tradeValue - _thisApp.model.modelVar.carriedForwardLoss, 0);
                                        //}

                                        if (_thisApp.model.modelVar.strategyProfit > _thisApp.model.modelVar.maxStrategyProfit) { _thisApp.model.modelVar.maxStrategyProfit = _thisApp.model.modelVar.strategyProfit; }

                                        // Save tbAudit
                                        IGModels.clsCommonFunctions.SaveTradeAudit(_thisApp.the_app_db, _thisApp.model.thisModel.currentTrade, (double)_thisApp.currentTrade.level, tsm.TradeType);

                                        // Save the last run vars into the TB settings table
                                        _thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                        _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                        _thisApp.model.thisModel.currentTrade.units = _thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice;
                                        _thisApp.model.thisModel.currentTrade.tbDealStatus = tsm.DealStatus;

                                        _thisApp.model.thisModel.currentTrade.timestamp = DateTime.UtcNow;
                                        _thisApp.model.thisModel.currentTrade.candleSold = null;
                                        _thisApp.model.thisModel.currentTrade.candleBought = null;
                                        if (_thisApp.epicName != "") { _thisApp.model.thisModel.currentTrade.epic = _thisApp.epicName; }
                                        if (_thisApp.modelID != "") { _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID; }
                                        _thisApp.model.thisModel.currentTrade.tbReason = tsm.Status;
                                        _thisApp.model.thisModel.modelTrades.Add(_thisApp.model.thisModel.currentTrade);
                                        _thisApp.model.modelVar.numCandlesOnMarket = 0;
                                        _thisApp.model.thisModel.currentTrade.numCandlesOnMarket = _thisApp.model.modelVar.numCandlesOnMarket;
                                        clsCommonFunctions.AddStatusMessage("Saving trade", "INFO");
                                        _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                        clsCommonFunctions.AddStatusMessage("Trade saved", "INFO");


                                        await tradeSubUpdate.Add(_thisApp.the_app_db);

                                        if (_thisApp.model.thisModel.currentTrade.attachedOrder != null)
                                        {
                                            // Close any open orders
                                            if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId != "")
                                            {
                                                clsCommonFunctions.AddStatusMessage($"Deleting order (if exists) {_thisApp.model.thisModel.currentTrade.attachedOrder.dealId}", "INFO");
                                                _thisApp.DeleteOrder(_thisApp.model.thisModel.currentTrade.attachedOrder.direction, _thisApp.model.thisModel.currentTrade.attachedOrder.orderSize, _thisApp.model.thisModel.currentTrade.attachedOrder.dealId);
                                                clsCommonFunctions.AddStatusMessage("Order deleted", "INFO");
                                            }
                                        }
                                        // Close supp trade if it is still running
                                        if (_thisApp.model.onSuppTrade)
                                        {
                                            clsCommonFunctions.AddStatusMessage($"Closing supp trade (if exists) {_thisApp.model.thisModel.suppTrade.tbDealId}", "INFO");
                                            _thisApp.CloseDeal(_thisApp.model.thisModel.suppTrade.longShort.ToLower(), _thisApp.model.thisModel.suppTrade.quantity, _thisApp.model.thisModel.suppTrade.tbDealId);
                                            clsCommonFunctions.AddStatusMessage("Supp trade deleted", "INFO");
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
                                            //    string subject = "TRADE ENDED - " + _thisApp.currentTrade.epic;
                                            //    string text = "The trade has ended in the " + region + " environment</br></br>";
                                            //    text += "<ul>";
                                            //    text += "<li>Trade ID : " + _thisApp.currentTrade.dealId + "</li>";
                                            //    text += "<li>Epic : " + _thisApp.currentTrade.epic + "</li>";
                                            //    text += "<li>Date : " + _thisApp.currentTrade.lastUpdated + "</li>";
                                            //    text += "<li>Type : " + _thisApp.model.thisModel.currentTrade.longShort + "</li>";
                                            //    text += "<li>Trade value : " + _thisApp.model.thisModel.currentTrade.tradeValue + "</li>";
                                            //    text += "<li>Size : " + _thisApp.currentTrade.size + "</li>";
                                            //    text += "<li>Price : " + _thisApp.currentTrade.level + "</li>";
                                            //    text += "<li>Stop Level : " + _thisApp.currentTrade.stopLevel + "</li>";
                                            //    text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                            //    text += "</ul>";
                                            //    obj.sendEmail(recips, subject, text);
                                            //}
                                        }
                                        catch (Exception ex)
                                        {
                                            var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                                            log.Log_Message = ex.ToString();
                                            log.Log_Type = "Error";
                                            log.Log_App = "UpdateTsOPU";
                                            log.Epic = "";
                                            await log.Save();
                                        }
                                        _thisApp.model.thisModel.currentTrade = null;
                                        _thisApp.currentTrade = null;
                                        _thisApp.model.onMarket = false;

                                    }
                                }

                                if (_thisApp.suppTrade != null)
                                {
                                    if (tsm.DealId == _thisApp.suppTrade.dealId)
                                    {
                                        clsCommonFunctions.AddStatusMessage("Deleting supp trade - " + tsm.DealId + " - Current Deal = " + _thisApp.suppTrade.dealId, "INFO");
                                        DateTime dtNow = DateTime.UtcNow;
                                        await _thisApp.GetTradeFromDB(tsm.DealId, _thisApp.strategy, _thisApp.resolution);

                                        _thisApp.suppTrade.lastUpdated = dtNow;
                                        _thisApp.suppTrade.status = tsm.Status;
                                        _thisApp.suppTrade.dealStatus = tsm.DealStatus;
                                        _thisApp.suppTrade.level = Convert.ToDecimal(tsm.Level);
                                        _thisApp.suppTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                        _thisApp.suppTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                        _thisApp.suppTrade.channel = tsm.Channel;
                                        if (Convert.ToDecimal(tsm.Size) > 0)
                                        {
                                            _thisApp.suppTrade.size = Convert.ToDecimal(tsm.Size);
                                        }
                                        _thisApp.suppTrade.direction = tsm.Direction;
                                        _thisApp.model.thisModel.suppTrade.channel = tsm.Channel;
                                        _thisApp.model.thisModel.suppTrade.tradeEnded = dtNow;
                                        clsCommonFunctions.AddStatusMessage("tsm.Direction = " + tsm.Direction, "INFO");
                                        if (tsm.Direction == "BUY")
                                        {
                                            clsCommonFunctions.AddStatusMessage("deleting buy", "INFO");
                                            _thisApp.model.thisModel.suppTrade.sellPrice = Convert.ToDecimal(_thisApp.suppTrade.level);
                                            _thisApp.model.thisModel.suppTrade.sellDate = dtNow;
                                            _thisApp.model.thisModel.suppTrade.tradeValue = (_thisApp.model.thisModel.suppTrade.sellPrice - _thisApp.model.thisModel.suppTrade.buyPrice) * (decimal)_thisApp.suppTrade.size;

                                            _thisApp.model.sellLong = false;
                                            _thisApp.model.buyLong = false;
                                            _thisApp.model.longOnmarket = false;
                                            _thisApp.model.onSuppTrade = false;

                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Long";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Sell";
                                                _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.suppTrade.quantity;
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.suppTrade.sellPrice;
                                                _thisApp.model.modelLogs.logs[0].tradeValue = (_thisApp.model.thisModel.suppTrade.sellPrice - _thisApp.model.thisModel.suppTrade.buyPrice) * (decimal)_thisApp.model.thisModel.suppTrade.quantity;
                                            }
                                            clsCommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.suppTrade), _thisApp.the_app_db);
                                        }
                                        else
                                        {
                                            clsCommonFunctions.AddStatusMessage("deleting sell", "INFO");
                                            _thisApp.model.thisModel.suppTrade.buyPrice = Convert.ToDecimal(_thisApp.suppTrade.level);
                                            _thisApp.model.thisModel.suppTrade.purchaseDate = dtNow;
                                            _thisApp.model.thisModel.suppTrade.tradeValue = (_thisApp.model.thisModel.suppTrade.sellPrice - _thisApp.model.thisModel.suppTrade.buyPrice) * (decimal)_thisApp.suppTrade.size;
                                            //_thisApp.model.buyShort = false;
                                            //_thisApp.model.sellShort = false;
                                            //_thisApp.model.shortOnMarket = false;
                                            _thisApp.model.onSuppTrade = false;

                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Short";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Buy";
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.suppTrade.buyPrice;
                                                _thisApp.model.modelLogs.logs[0].tradeValue = (_thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.suppTrade.buyPrice) * (decimal)_thisApp.suppTrade.size;
                                            }
                                            clsCommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.suppTrade), _thisApp.the_app_db);
                                        }
                                        //_thisApp.model.sellLong = false;
                                        //_thisApp.model.buyLong = false;
                                        //_thisApp.model.longOnmarket = false;
                                        //_thisApp.model.buyShort = false;
                                        //_thisApp.model.sellShort = false;
                                        //_thisApp.model.shortOnMarket = false;

                                        //_thisApp.model.thisModel.currentTrade.tradeValue = _thisApp.model.thisModel.currentTrade.buyPrice - _thisApp.model.thisModel.currentTrade.sellPrice;

                                        _thisApp.model.modelVar.strategyProfit += _thisApp.model.thisModel.suppTrade.tradeValue;


                                        // set the trade values in the next run of the code rather than right away so we can aggregate trades and supp trades if needs be
                                        _thisApp.lastTradeDeleted = true;
                                        _thisApp.lastTradeSuppValue = (double)_thisApp.model.thisModel.suppTrade.tradeValue;


                                        //if (_thisApp.model.thisModel.suppTrade.tradeValue <= 0)
                                        //{
                                        //    _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss + (double)Math.Abs(_thisApp.model.thisModel.suppTrade.tradeValue);
                                        //}
                                        //else
                                        //{

                                        //    _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss - (double)Math.Abs(_thisApp.model.thisModel.suppTrade.tradeValue);
                                        //    if (_thisApp.model.modelVar.carriedForwardLoss < 0) { _thisApp.model.modelVar.carriedForwardLoss = 0; }
                                        //    _thisApp.model.modelVar.currentGain += Math.Max((double)_thisApp.model.thisModel.suppTrade.tradeValue - _thisApp.model.modelVar.carriedForwardLoss, 0);
                                        //}


                                        if (_thisApp.model.modelVar.strategyProfit > _thisApp.model.modelVar.maxStrategyProfit) { _thisApp.model.modelVar.maxStrategyProfit = _thisApp.model.modelVar.strategyProfit; }

                                        // Save the last run vars into the TB settings table
                                        // Dont do this for suplementary trades
                                        //_thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                        //await _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                        _thisApp.model.thisModel.suppTrade.units = _thisApp.model.thisModel.suppTrade.sellPrice - _thisApp.model.thisModel.suppTrade.buyPrice;
                                        _thisApp.model.thisModel.suppTrade.tbDealStatus = tsm.DealStatus;

                                        _thisApp.model.thisModel.suppTrade.timestamp = DateTime.UtcNow;
                                        _thisApp.model.thisModel.suppTrade.candleSold = null;
                                        _thisApp.model.thisModel.suppTrade.candleBought = null;
                                        if (_thisApp.epicName != "") { _thisApp.model.thisModel.suppTrade.epic = _thisApp.epicName; }
                                        if (_thisApp.modelID != "") { _thisApp.model.thisModel.suppTrade.modelRunID = _thisApp.modelID; }
                                        _thisApp.model.thisModel.suppTrade.tbReason = tsm.Status;
                                        _thisApp.model.thisModel.modelTrades.Add(_thisApp.model.thisModel.suppTrade);

                                        clsCommonFunctions.AddStatusMessage("Saving supp trade", "INFO");
                                        _thisApp.model.thisModel.suppTrade.SaveDocument(_thisApp.trade_container);
                                        clsCommonFunctions.AddStatusMessage("Trade supp saved", "INFO");

                                        _thisApp.model.thisModel.suppTrade = null;
                                        _thisApp.suppTrade = null;
                                        _thisApp.model.onSuppTrade = false;

                                        //Send email
                                        try
                                        {
                                            string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                            if (region == "LIVE")
                                            {
                                                clsEmail obj = new clsEmail();
                                                List<recip> recips = new List<recip>();
                                                //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                string subject = "SUPPLEMENTARY TRADE ENDED - " + _thisApp.suppTrade.epic;
                                                string text = "The trade has ended in the " + region + " environment</br></br>";
                                                text += "<ul>";
                                                text += "<li>Trade ID : " + _thisApp.suppTrade.dealId + "</li>";
                                                text += "<li>Epic : " + _thisApp.suppTrade.epic + "</li>";
                                                text += "<li>Date : " + _thisApp.suppTrade.lastUpdated + "</li>";
                                                text += "<li>Type : " + _thisApp.model.thisModel.suppTrade.longShort + "</li>";
                                                text += "<li>Trade value : " + _thisApp.model.thisModel.suppTrade.tradeValue + "</li>";
                                                text += "<li>Size : " + _thisApp.suppTrade.size + "</li>";
                                                text += "<li>Price : " + _thisApp.suppTrade.level + "</li>";
                                                text += "<li>Stop Level : " + _thisApp.suppTrade.stopLevel + "</li>";
                                                text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                                text += "</ul>";
                                                obj.sendEmail(recips, subject, text);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                                            log.Log_Message = ex.ToString();
                                            log.Log_Type = "Error";
                                            log.Log_App = "UpdateTsOPU";
                                            log.Epic = "";
                                            await log.Save();
                                        }
                                        //_thisApp.model.thisModel.currentTrade = null;
                                        //_thisApp.currentTrade = null;

                                        //_thisApp.model.onMarket = false;



                                    }
                                }

                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("DELETED failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "DELETED failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], _thisApp.the_app_db);
                                await tradeSubUpdate.Add(_thisApp.the_app_db);
                            }
                            //tradeSubUpdate.Add(_thisApp.the_app_db);

                        }
                        else if (tsm.Status == "OPEN")
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {

                                string orderDealId = tsm.DealId;

                                // Check the deal id with the deal reference from the Place Deal call to ensure we are dealing with the correct trade
                                if (tsm.DealReference == _thisApp.newDealReference)
                                {
                                    clsCommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                    clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);
                                    _thisApp.newDealReference = "";
                                    // First see if this is a supplementary trade triggered from an order
                                    if (_thisApp.model.onMarket == true)
                                    {
                                        if (_thisApp.model.thisModel.currentTrade.attachedOrder != null)
                                        {
                                            if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId == orderDealId && _thisApp.model.onSuppTrade == false)
                                            {

                                                //New supplementary trade has been started from an order
                                                DateTime thisDate = DateTime.UtcNow;

                                                _thisApp.suppTrade = new clsTradeUpdate();
                                                _thisApp.suppTrade.epic = tsm.Epic;
                                                _thisApp.suppTrade.dealReference = tsm.DealReference;
                                                _thisApp.suppTrade.dealId = tsm.DealId;
                                                _thisApp.suppTrade.lastUpdated = thisDate;
                                                _thisApp.suppTrade.status = tsm.Status;
                                                _thisApp.suppTrade.dealStatus = tsm.DealStatus;
                                                _thisApp.suppTrade.level = Convert.ToDecimal(tsm.Level);
                                                _thisApp.suppTrade.stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel));
                                                _thisApp.suppTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                                _thisApp.suppTrade.size = Convert.ToDecimal(tsm.Size);
                                                _thisApp.suppTrade.direction = tsm.Direction;
                                                _thisApp.suppTrade.accountId = _thisApp.igAccountId;
                                                _thisApp.suppTrade.channel = tsm.Channel;
                                                _thisApp.model.thisModel.suppTrade = new tradeItem();
                                                _thisApp.model.thisModel.suppTrade.quantity = Convert.ToDouble(_thisApp.suppTrade.size);
                                                _thisApp.model.thisModel.suppTrade.stopLossValue = Convert.ToDouble(_thisApp.suppTrade.stopLevel);
                                                _thisApp.model.thisModel.suppTrade.tbDealId = tsm.DealId;
                                                _thisApp.model.thisModel.suppTrade.tbDealReference = tsm.DealReference;
                                                _thisApp.model.thisModel.suppTrade.tbDealStatus = tsm.DealStatus;
                                                _thisApp.model.thisModel.suppTrade.tbReason = tsm.Status;
                                                _thisApp.model.thisModel.suppTrade.tradeStarted = new DateTime(thisDate.Year, thisDate.Month, thisDate.Day, thisDate.Hour, thisDate.Minute, thisDate.Second);
                                                _thisApp.model.thisModel.suppTrade.modelRunID = _thisApp.modelID;
                                                _thisApp.model.thisModel.suppTrade.epic = _thisApp.epicName;
                                                _thisApp.model.thisModel.suppTrade.timestamp = thisDate;
                                                _thisApp.model.thisModel.suppTrade.accountId = _thisApp.igAccountId;
                                                _thisApp.model.thisModel.suppTrade.channel = tsm.Channel;

                                                if (tsm.Direction == "BUY")
                                                {
                                                    _thisApp.model.thisModel.suppTrade.longShort = "Long";
                                                    _thisApp.model.thisModel.suppTrade.buyPrice = Convert.ToDecimal(_thisApp.suppTrade.level);
                                                    _thisApp.model.thisModel.suppTrade.purchaseDate = thisDate;
                                                    _thisApp.model.onSuppTrade = true;
                                                    _thisApp.model.buyLongSupp = false;
                                                    if (_thisApp.model.modelLogs.logs.Count >= 1)
                                                    {
                                                        _thisApp.model.modelLogs.logs[0].tradeType = "Long";
                                                        _thisApp.model.modelLogs.logs[0].tradeAction = "Buy";
                                                        _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.suppTrade.quantity;
                                                        _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.suppTrade.buyPrice;
                                                    }
                                                    clsCommonFunctions.SendBroadcast("BuyLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.suppTrade), _thisApp.the_app_db);
                                                }
                                                else
                                                {
                                                    _thisApp.model.thisModel.suppTrade.longShort = "Short";
                                                    _thisApp.model.thisModel.suppTrade.sellPrice = (decimal)_thisApp.suppTrade.level;
                                                    _thisApp.model.thisModel.suppTrade.sellDate = thisDate;
                                                    _thisApp.model.thisModel.suppTrade.modelRunID = _thisApp.modelID;
                                                    _thisApp.model.onSuppTrade = true;
                                                    _thisApp.model.sellShortSupp = false;
                                                    if (_thisApp.model.modelLogs.logs.Count >= 1)
                                                    {
                                                        _thisApp.model.modelLogs.logs[0].tradeType = "Short";
                                                        _thisApp.model.modelLogs.logs[0].tradeAction = "Sell";
                                                        _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.suppTrade.quantity;
                                                        _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.suppTrade.sellPrice;
                                                    }
                                                    clsCommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.suppTrade), _thisApp.the_app_db);
                                                }

                                                _thisApp.model.thisModel.suppTrade.targetPrice = _thisApp.model.thisModel.currentTrade.targetPrice;
                                                // Save this trade in the database
                                                _thisApp.model.thisModel.suppTrade.candleSold = null;
                                                _thisApp.model.thisModel.suppTrade.candleBought = null;
                                                _thisApp.model.thisModel.suppTrade.isSuppTrade = true;
                                                _thisApp.model.thisModel.suppTrade.Add(_thisApp.the_app_db, _thisApp.trade_container);

                                                _thisApp.model.thisModel.currentTrade.hasSuppTrade = true;

                                                //Update the current trade to have the same stop loss as this one.

                                                _thisApp.currentTrade.stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel));

                                                if (tsm.Direction == "BUY")
                                                {
                                                    _thisApp.model.thisModel.currentTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.suppTrade.stopLevel) - (double)_thisApp.model.thisModel.currentTrade.buyPrice);
                                                    _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = (decimal)_thisApp.suppTrade.stopLevel;
                                                    _thisApp.EditDeal((double)_thisApp.suppTrade.stopLevel, _thisApp.model.thisModel.currentTrade.tbDealId, _thisApp.model.thisModel.currentTrade.stopLossValue);
                                                }
                                                else
                                                {
                                                    _thisApp.model.thisModel.currentTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.suppTrade.stopLevel) - (double)_thisApp.model.thisModel.currentTrade.sellPrice);
                                                    _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = (decimal)_thisApp.suppTrade.stopLevel;
                                                    _thisApp.EditDeal((double)_thisApp.suppTrade.stopLevel, _thisApp.model.thisModel.currentTrade.tbDealId, _thisApp.model.thisModel.currentTrade.stopLossValue);

                                                }
                                                _thisApp.model.stopPrice = _thisApp.model.thisModel.currentTrade.stopLossValue;
                                                _thisApp.model.stopPriceOld = _thisApp.model.stopPrice;

                                                //Send email

                                                try
                                                {


                                                    string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                                    if (region == "LIVE")
                                                    {

                                                        clsEmail obj = new clsEmail();
                                                        List<recip> recips = new List<recip>();
                                                        //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                        recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                        string subject = "SUPPLEMENTARY TRADE STARTED - " + _thisApp.suppTrade.epic;
                                                        string text = "A new supplementary trade has started in the " + region + " environment</br></br>";
                                                        text += "<ul>";
                                                        text += "<li>Trade ID : " + _thisApp.suppTrade.dealId + "</li>";
                                                        text += "<li>Epic : " + _thisApp.suppTrade.epic + "</li>";
                                                        text += "<li>Date : " + _thisApp.suppTrade.lastUpdated + "</li>";
                                                        text += "<li>Type : " + _thisApp.model.thisModel.suppTrade.longShort + "</li>";
                                                        text += "<li>Size : " + _thisApp.suppTrade.size + "</li>";
                                                        text += "<li>Price : " + _thisApp.suppTrade.level + "</li>";
                                                        text += "<li>Stop Level : " + _thisApp.suppTrade.stopLevel + "</li>";
                                                        text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                                        text += "</ul>";

                                                        obj.sendEmail(recips, subject, text);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                                                    log.Log_Message = ex.ToString();
                                                    log.Log_Type = "Error";
                                                    log.Log_App = "UpdateTsOPU";
                                                    log.Epic = "";
                                                    await log.Save();
                                                }
                                                //else
                                                //{
                                                //    clsEmail obj = new clsEmail();
                                                //    List<recip> recips = new List<recip>();
                                                //    //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                                //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                                //    string subject = "SUPPLEMENTARY TRADE STARTED - " + _thisApp.suppTrade.epic;
                                                //    string text = "A new supplementary trade has started in the " + region + " environment</br></br>";
                                                //    text += "<ul>";
                                                //    text += "<li>Trade ID : " + _thisApp.suppTrade.dealId + "</li>";
                                                //    text += "<li>Epic : " + _thisApp.suppTrade.epic + "</li>";
                                                //    text += "<li>Date : " + _thisApp.suppTrade.lastUpdated + "</li>";
                                                //    text += "<li>Type : " + _thisApp.model.thisModel.suppTrade.longShort + "</li>";
                                                //    text += "<li>Size : " + _thisApp.suppTrade.size + "</li>";
                                                //    text += "<li>Price : " + _thisApp.suppTrade.level + "</li>";
                                                //    text += "<li>Stop Level : " + _thisApp.suppTrade.stopLevel + "</li>";
                                                //    text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                                //    text += "</ul>";

                                                //    obj.sendEmail(recips, subject, text);
                                                //}
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Not on market so this must be a new current trade
                                        DateTime thisDate = DateTime.UtcNow;
                                        _thisApp.currentTrade = new clsTradeUpdate();
                                        _thisApp.currentTrade.epic = tsm.Epic;
                                        _thisApp.currentTrade.dealReference = tsm.DealReference;
                                        _thisApp.currentTrade.dealId = tsm.DealId;
                                        _thisApp.currentTrade.lastUpdated = thisDate;
                                        _thisApp.currentTrade.status = tsm.Status;
                                        _thisApp.currentTrade.dealStatus = tsm.DealStatus;
                                        _thisApp.currentTrade.level = Convert.ToDecimal(tsm.Level);
                                        _thisApp.currentTrade.stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel));
                                        _thisApp.currentTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                        _thisApp.currentTrade.size = Convert.ToDecimal(tsm.Size);
                                        _thisApp.currentTrade.direction = tsm.Direction;
                                        _thisApp.currentTrade.accountId = _thisApp.igAccountId;
                                        _thisApp.currentTrade.channel = tsm.Channel;

                                        _thisApp.model.thisModel.currentTrade = new tradeItem();
                                        _thisApp.model.thisModel.currentTrade.quantity = Convert.ToDouble(_thisApp.currentTrade.size);
                                        _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                        _thisApp.model.thisModel.currentTrade.tbDealId = tsm.DealId;
                                        _thisApp.model.thisModel.currentTrade.tbDealReference = tsm.DealReference;
                                        _thisApp.model.thisModel.currentTrade.tbDealStatus = tsm.DealStatus;
                                        _thisApp.model.thisModel.currentTrade.tbReason = tsm.Status;
                                        _thisApp.model.stopPrice = _thisApp.model.thisModel.currentTrade.stopLossValue;
                                        _thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                        _thisApp.model.thisModel.currentTrade.tradeStarted = thisDate;// new DateTime(thisDate.Year, thisDate.Month, thisDate.Day, thisDate.Hour, thisDate.Minute, thisDate.Second);
                                        _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID;
                                        _thisApp.model.thisModel.currentTrade.epic = _thisApp.epicName;
                                        _thisApp.model.thisModel.currentTrade.timestamp = DateTime.UtcNow;
                                        _thisApp.model.thisModel.currentTrade.accountId = _thisApp.igAccountId;
                                        _thisApp.model.thisModel.currentTrade.channel = tsm.Channel;


                                        // set the target

                                        if (tsm.Direction == "BUY")
                                        {
                                            _thisApp.model.thisModel.currentTrade.longShort = "Long";
                                            _thisApp.model.thisModel.currentTrade.buyPrice = Convert.ToDecimal(_thisApp.currentTrade.level);
                                            _thisApp.model.thisModel.currentTrade.purchaseDate = thisDate;

                                            _thisApp.model.sellLong = false;
                                            _thisApp.model.buyLong = false;
                                            _thisApp.model.longOnmarket = true;
                                            _thisApp.model.buyShort = false;
                                            _thisApp.model.shortOnMarket = false;
                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Long";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Buy";
                                                _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.currentTrade.quantity;
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.currentTrade.buyPrice;
                                            }
                                            clsCommonFunctions.SendBroadcast("BuyLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);
                                        }
                                        else
                                        {
                                            _thisApp.model.thisModel.currentTrade.longShort = "Short";
                                            _thisApp.model.thisModel.currentTrade.sellPrice = (decimal)_thisApp.currentTrade.level;
                                            _thisApp.model.thisModel.currentTrade.sellDate = thisDate;
                                            _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID;
                                            _thisApp.model.sellShort = false;
                                            _thisApp.model.shortOnMarket = true;
                                            _thisApp.model.buyLong = false;
                                            _thisApp.model.longOnmarket = false;
                                            if (_thisApp.model.modelLogs.logs.Count >= 1)
                                            {
                                                _thisApp.model.modelLogs.logs[0].tradeType = "Short";
                                                _thisApp.model.modelLogs.logs[0].tradeAction = "Sell";
                                                _thisApp.model.modelLogs.logs[0].quantity = _thisApp.model.thisModel.currentTrade.quantity;
                                                _thisApp.model.modelLogs.logs[0].tradePrice = _thisApp.model.thisModel.currentTrade.sellPrice;
                                            }
                                            clsCommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);
                                        }
                                        _thisApp.model.onMarket = true;

                                        if (_thisApp.strategy == "" || _thisApp.strategy == "SMA")
                                        {
                                            clsCommonFunctions.OrderValues orderValues = new clsCommonFunctions.OrderValues();

                                            orderValues.SetOrderValues(tsm.Direction, _thisApp);
                                            if (_thisApp.model.doSuppTrades)
                                            {
                                                clsCommonFunctions.AddStatusMessage($"Creating new order - direction:{tsm.Direction}, stopDistance:{orderValues.stopDistance}, level:{orderValues.level}", "INFO");
                                                requestedTrade reqTrade = new requestedTrade();
                                                reqTrade.dealType = "ORDER";
                                                reqTrade.dealReference = _thisApp.PlaceOrder(tsm.Direction, orderValues.quantity, orderValues.stopDistance, _thisApp.igAccountId, orderValues.level).Result;
                                                _thisApp.requestedTrades.Add(reqTrade);

                                            }

                                            _thisApp.model.thisModel.currentTrade.targetPrice = orderValues.targetPrice;
                                        }

                                        if (_thisApp.strategy == "RSI" || 
                                            _thisApp.strategy == "REI" || 
                                            _thisApp.strategy == "RSI-ATR" || 
                                            _thisApp.strategy == "RSI-CUML" || 
                                            _thisApp.strategy == "CASEYC" ||
                                            _thisApp.strategy == "CASEYCSHORT" ||
                                            _thisApp.strategy == "CASEYCEQUITIES")
                                        {
                                            _thisApp.currentTrade.limitLevel = Convert.ToDecimal(tsm.Limitlevel);
                                            _thisApp.model.thisModel.currentTrade.targetPrice = Convert.ToDecimal(tsm.Limitlevel);
                                        }

                                        // Save this trade in the database
                                        _thisApp.model.thisModel.currentTrade.candleSold = null;
                                        _thisApp.model.thisModel.currentTrade.candleBought = null;
                                        _thisApp.model.thisModel.currentTrade.count = _thisApp.modelVar.counter;
                                        _thisApp.model.thisModel.currentTrade.strategy = _thisApp.strategy;
                                        _thisApp.model.thisModel.currentTrade.resolution = _thisApp.resolution;

                                        _thisApp.model.thisModel.currentTrade.Add(_thisApp.the_app_db, _thisApp.trade_container);

                                        // Save tbAudit
                                        IGModels.clsCommonFunctions.SaveTradeAudit(_thisApp.the_app_db, _thisApp.model.thisModel.currentTrade, (double)_thisApp.currentTrade.level, tsm.TradeType);


                                        await tradeSubUpdate.Add(_thisApp.the_app_db);

                                        //Send email
                                        string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                        try
                                        {
                                            //if (region == "LIVE")
                                            //{

                                            //    clsEmail obj = new clsEmail();
                                            //    List<recip> recips = new List<recip>();
                                            //    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                            //    string subject = "NEW TRADE STARTED - " + _thisApp.currentTrade.epic;
                                            //    string text = "A new trade has started in the " + region + " environment</br></br>";
                                            //    text += "<ul>";
                                            //    text += "<li>Trade ID : " + _thisApp.currentTrade.dealId + "</li>";
                                            //    text += "<li>Epic : " + _thisApp.currentTrade.epic + "</li>";
                                            //    text += "<li>Date : " + _thisApp.currentTrade.lastUpdated + "</li>";
                                            //    text += "<li>Type : " + _thisApp.model.thisModel.currentTrade.longShort + "</li>";
                                            //    text += "<li>Size : " + _thisApp.currentTrade.size + "</li>";
                                            //    text += "<li>Price : " + _thisApp.currentTrade.level + "</li>";
                                            //    text += "<li>Stop Level : " + _thisApp.currentTrade.stopLevel + "</li>";
                                            //    text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                            //    text += "</ul>";

                                            //    obj.sendEmail(recips, subject, text);
                                            //}
                                        }
                                        catch (Exception ex)
                                        {
                                            var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                                            log.Log_Message = ex.ToString();
                                            log.Log_Type = "Error";
                                            log.Log_App = "UpdateTsOPU";
                                            log.Epic = "";
                                            await log.Save();
                                        }

                                    }
                                }
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("OPEN failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "DELETED failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], _thisApp.the_app_db);
                                await tradeSubUpdate.Add(_thisApp.the_app_db);
                            }
                            //tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                    }
                }
            }catch(Exception ex)
            {

            }
        }

        public async void CONFIRMUpdate( string inputData,string itemName)
        {
            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                TradeSubUpdate tradeSubUpdate = (TradeSubUpdate)JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
                tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
                tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
                tradeSubUpdate.updateType = "CONFIRM";

                if (tradeSubUpdate.epic == _thisApp.epicName)
                {
                    tsm.Channel = tradeSubUpdate.channel;
                    tsm.DealId = tradeSubUpdate.dealId;
                    tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
                    tsm.DealReference = tradeSubUpdate.dealReference;
                    tsm.DealStatus = tradeSubUpdate.dealStatus.ToString();
                    tsm.Direction = tradeSubUpdate.direction.ToString();
                    tsm.ItemName = itemName;
                    tsm.Epic = tradeSubUpdate.epic;
                    tsm.Expiry = tradeSubUpdate.expiry;
                    tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
                    tsm.Level = tradeSubUpdate.level;
                    tsm.Limitlevel = tradeSubUpdate.limitLevel;
                    tsm.Size = tradeSubUpdate.size;
                    tsm.Status = tradeSubUpdate.status.ToString();
                    tsm.StopLevel = tradeSubUpdate.stopLevel;
                    tsm.Reason = tradeSubUpdate.reason;
                    tsm.date = tradeSubUpdate.date;
                    tsm.StopDistance = tradeSubUpdate.stopDistance;
                    tsm.TradeType = "CONFIRM";

                    tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
                    tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
                    tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
                    if (tsm.Reason != null)
                    {
                        if (tsm.Reason != "")
                        {
                            tradeSubUpdate.reasonDescription = _thisApp.TradeErrors[tsm.Reason];
                        }
                    }

                    tradeSubUpdate.updateType = tsm.TradeType;

                    if (tsm.Epic == _thisApp.epicName)
                    {

                        // Find this trade from the list of requested trades to tie in with the requested type (position or order)
                        requestedTrade reqTrade = new requestedTrade();
                        reqTrade = _thisApp.requestedTrades.Where(i => i.dealReference == tsm.DealReference).FirstOrDefault();

                        if (reqTrade != null)
                        {
                            await tradeSubUpdate.Add(_thisApp.the_app_db);

                            reqTrade.dealStatus = tsm.DealStatus;

                            clsCommonFunctions.AddStatusMessage($"CONFIRM - deal reference = {reqTrade.dealReference}, deal type = {reqTrade.dealType}, deal status = {reqTrade.dealStatus}");


                            if (tsm.Status == "OPEN" && tsm.Reason == "SUCCESS")
                            {
                                // trade/order opened successfully
                                clsCommonFunctions.AddStatusMessage($"CONFIRM - successful", "INFO");
                            }

                            if (reqTrade.dealType == "ORDER" && reqTrade.dealStatus == "REJECTED")
                            {

                                clsCommonFunctions.AddStatusMessage($"ORDER REJECTED -  {tsm.Reason} - {_thisApp.TradeErrors[tsm.Reason]} : retryCount = {_thisApp.retryOrderCount}, retryOrderLimit = {_thisApp.retryOrderLimit}");
                                // Order has been rejected, possibly because the market is moving too fast. Try again next time.
                                if (_thisApp.retryOrderCount < _thisApp.retryOrderLimit)
                                {
                                    _thisApp.retryOrder = true;
                                    _thisApp.retryOrderCount += 1;
                                    clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry set for next run");

                                }
                                else
                                {
                                    clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry limit hit. Just forget about it.");
                                    _thisApp.retryOrder = false;
                                    _thisApp.retryOrderCount = 0;
                                }
                            }

                            if (tsm.Status == null & tsm.Reason != "SUCCESS")
                            {
                                // trade/order not successful (could be update or open or delete)
                                clsCommonFunctions.AddStatusMessage($"CONFIRM - failed - deal type = {reqTrade.dealType} - {tsm.Reason} - {_thisApp.TradeErrors[tsm.Reason]}", "INFO");

                                if (reqTrade.dealType == "POSITION")
                                {
                                    clsCommonFunctions.AddStatusMessage($"CONFIRM - Resetting values due to {reqTrade.dealType} failure", "INFO");
                                    _thisApp.model.sellShort = false;
                                    _thisApp.model.sellLong = false;
                                    _thisApp.model.buyShort = false;
                                    _thisApp.model.shortOnMarket = false;
                                    _thisApp.model.buyLong = false;
                                    _thisApp.model.longOnmarket = false;
                                    _thisApp.model.onMarket = false;
                                }

                            }
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "UpdateTsCONFIRM";
                log.Epic = "";
                await log.Save();
            }
        }
        //public async static Task<bool> SetupDB()
        //{
        //    bool ret = true;

        //    try
        //    {
        //        igContainer.the_db = await IGModels.clsCommonFunctions.Get_Database();
        //        the_db = await IGModels.clsCommonFunctions.Get_App_Database();

        //        if (the_db != null)
        //        {
        //            EpicContainer = the_app_db.GetContainer("Epics");
        //            hourly_container = the_db.GetContainer("HourlyCandle");
        //            minute_container = the_db.GetContainer("MinuteCandle");
        //            TicksContainer = the_db.GetContainer("CandleTicks");
        //            candlesContainer = the_db.GetContainer("Candles");
        //            trade_container = the_app_db.GetContainer("TradingBrainTrades");

        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.Write(ex.ToString());
        //    }
        //    return ret;

        //}
        //public async static Task<bool> SetupDB(string epic)
        //{
        //    bool ret = true;

        //    try
        //    {
        //        the_db = await IGModels.clsCommonFunctions.Get_Database(epic);
        //        the_app_db = await IGModels.clsCommonFunctions.Get_App_Database(epic);

        //        if (the_db != null)
        //        {
        //            EpicContainer = the_app_db.GetContainer("Epics");
        //            hourly_container = the_db.GetContainer("HourlyCandle");

        //            if (epic == "IX.D.NIKKEI.DAILY.IP")
        //            {
        //                minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
        //                TicksContainer = the_db.GetContainer("CandleTicks_NIKKEI");
        //                trade_container = the_app_db.GetContainer("TradingBrainTrades");
        //            }
        //            else
        //            {
        //                minute_container = the_db.GetContainer("MinuteCandle");
        //                TicksContainer = the_db.GetContainer("CandleTicks");
        //                trade_container = the_app_db.GetContainer("TradingBrainTrades");
        //            }

        //            candlesContainer = the_db.GetContainer("Candles");


        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.Write(ex.ToString());
        //    }
        //    return ret;

        //}
    }
}
