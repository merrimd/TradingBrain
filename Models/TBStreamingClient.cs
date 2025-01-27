using com.lightstreamer.client;
using Lightstreamer.DotNet.Logging.Log; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lightstreamer.DotNet.Client.Test;
using System.Runtime.InteropServices.JavaScript;
using System.Net.NetworkInformation;
using IGWebApiClient;
using dto.endpoint.auth.session.v2;
using IGWebApiClient.Common;
using System.Collections.ObjectModel;
using NLog;
using System.Collections.Specialized;
using System.Configuration;
using IGModels;
using Lightstreamer.DotNet.Client;
using Newtonsoft.Json;
using System.CodeDom;
using Microsoft.AspNetCore.Components.Forms;
using TradingBrain.Models;
namespace TradingBrain.Models
{

    public class TBStreamingClient
    {
        //private DemoForm demoForm;
        private Delegates.LightstreamerUpdateDelegate updateDelegate;
        private Delegates.LightstreamerStatusChangedDelegate statusChangeDelegate;
   
        public LightstreamerClient client;
        private Subscription subscription;
        //private Dictionary<ChartQuotes, Subscription> charts = new Dictionary<ChartQuotes, Subscription>();

  
        public ObservableCollection<IgPublicApiData.AccountModel>? Accounts { get; set; }
        public static string? CurrentAccountId;
        public bool LoggedIn;
        public bool connectionEstablished;
        public bool FirstConfirmUpdate;
        public MainApp _thisApp;
        public TBStreamingClient(
                string pushServerUrl,
                string forceT,
                MainApp thisApp,
                Delegates.LightstreamerUpdateDelegate lsUpdateDelegate,
                Delegates.LightstreamerStatusChangedDelegate lsStatusChangeDelegate)
        {
            try
            {
                _thisApp = thisApp;
                Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();
                //demoForm = form;
                updateDelegate = lsUpdateDelegate;
                statusChangeDelegate = lsStatusChangeDelegate;
                FirstConfirmUpdate = true;
                //var config = new NLog.Config.LoggingConfiguration();

                //var filename = "DEBUG-" + DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + "-" + DateTime.UtcNow.Hour + ".txt";
                //var logfile = new NLog.Targets.FileTarget("logfile") { FileName = filename };
                //var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

 
                //config.AddRule(LogLevel.Warn, LogLevel.Fatal, logconsole);
                //config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);

                //NLog.LogManager.Configuration = config;
                
                //LightstreamerClient.setLoggerProvider(new Log4NetLoggerProviderWrapper());

                //LightstreamerClient.setLoggerProvider(new );
                bool connectionEstablished = false;

                SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();
                object v = ConfigurationManager.GetSection("appSettings");
                NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
                string env = igWebApiConnectionConfig["environment"] ?? "DEMO";
                _thisApp.igRestApiClient = new IgRestApiClient(env, smartDispatcher);
                //_thisApp.igAccountId = this.client.connectionDetails.User;
            }
            catch (Exception ex)
            {
                Log log = new Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TBStreamingClient";
                log.Save();
            }

        }
        //public static void AddStatusMessage(string message)
        //{
        //    clsCommonFunctions.AddStatusMessage(message);
        //}

        public async void ConnectToRest()
        {
            object v = ConfigurationManager.GetSection("appSettings");
            NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
            //string env = igWebApiConnectionConfig["environment"] ?? "";
            string env = igWebApiConnectionConfig["environment"] ?? "DEMO";

            string userName = igWebApiConnectionConfig["username." + env] ?? "";
            string password = igWebApiConnectionConfig["password." + env] ?? "";
            string apiKey = igWebApiConnectionConfig["apikey." + env] ?? "";
            var ar = new AuthenticationRequest { identifier = userName, password = password };

            try
            {
                var response = await _thisApp.igRestApiClient.SecureAuthenticate(ar, apiKey);
                if (response && (response.Response != null) && (response.Response.accounts.Count > 0))
                {
                    Accounts.Clear();

                    foreach (var account in response.Response.accounts)
                    {
                        var igAccount = new IgPublicApiData.AccountModel
                        {
                            ClientId = response.Response.clientId,
                            ProfitLoss = response.Response.accountInfo.profitLoss,
                            AvailableCash = response.Response.accountInfo.available,
                            Deposit = response.Response.accountInfo.deposit,
                            Balance = response.Response.accountInfo.balance,
                            LsEndpoint = response.Response.lightstreamerEndpoint,
                            AccountId = account.accountId,
                            AccountName = account.accountName,
                            AccountType = account.accountType
                        };

                        Accounts.Add(igAccount);

                        clsCommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName,"INFO");
                    }

                    LoggedIn = true;

                    clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");

                    _thisApp.context = _thisApp.igRestApiClient.GetConversationContext();

                }
                else
                {
                    clsCommonFunctions.AddStatusMessage("Failed to login. HttpResponse StatusCode = " +
                      response.StatusCode, "ERROR");
                }
            }
            catch(Exception e)
            {
                clsCommonFunctions.AddStatusMessage("ConnectToRest failed - " + e.ToString,"ERROR");
                
            }
        }
        public async void LogIn()
        {
            object v = ConfigurationManager.GetSection("appSettings");
            NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
            //string env = igWebApiConnectionConfig["environment"] ?? "";
            string env = igWebApiConnectionConfig["environment"] ?? "DEMO";

            string userName = igWebApiConnectionConfig["username." + env] ?? "";
            string password = igWebApiConnectionConfig["password." + env] ?? "";
            string apiKey = igWebApiConnectionConfig["apikey." + env] ?? "";
            var ar = new AuthenticationRequest { identifier = userName, password = password };

            try
            {
                var response = await _thisApp.igRestApiClient.SecureAuthenticate(ar, apiKey);
                if (response && (response.Response != null) && (response.Response.accounts.Count > 0))
                {
                    Accounts.Clear();

                    foreach (var account in response.Response.accounts)
                    {
                        var igAccount = new IgPublicApiData.AccountModel
                        {
                            ClientId = response.Response.clientId,
                            ProfitLoss = response.Response.accountInfo.profitLoss,
                            AvailableCash = response.Response.accountInfo.available,
                            Deposit = response.Response.accountInfo.deposit,
                            Balance = response.Response.accountInfo.balance,
                            LsEndpoint = response.Response.lightstreamerEndpoint,
                            AccountId = account.accountId,
                            AccountName = account.accountName,
                            AccountType = account.accountType
                        };

                        Accounts.Add(igAccount);

                        clsCommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName, "INFO");
                    }

                    LoggedIn = true;

                    clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");

                    _thisApp.context = _thisApp.igRestApiClient.GetConversationContext();

                    clsCommonFunctions.AddStatusMessage("establishing datastream connection", "INFO");

                    if ((_thisApp.context != null) && (response.Response.lightstreamerEndpoint != null) &&
                        (_thisApp.context.apiKey != null) && (_thisApp.context.xSecurityToken != null) && (_thisApp.context.cst != null))
                    {
                        try
                        {
                            CurrentAccountId = response.Response.currentAccountId;


                            client = new LightstreamerClient(response.Response.lightstreamerEndpoint, "DEFAULT");
                            client.connectionDetails.User = response.Response.currentAccountId;
                            client.connectionDetails.Password = string.Format("CST-{0}|XST-{1}", _thisApp.context.cst, _thisApp.context.xSecurityToken);// string.Format("CST-{0}|XST-{1}", cstToken, xSecurityToken);
                            client.connectionDetails.ServerAddress = response.Response.lightstreamerEndpoint;


                        }
                        catch (Exception ex)
                        {
                            clsCommonFunctions.AddStatusMessage(ex.Message, "ERROR");
                        }
                    }


                    //get any current positions
                    _thisApp.GetPositions();
                }
                else
                {
                    clsCommonFunctions.AddStatusMessage("Failed to login. HttpResponse StatusCode = " +
                                        response.StatusCode,"ERROR");
                    Log log = new Log(_thisApp.the_app_db);
                    log.Log_Message = "Failed to login. HttpResponse StatusCode = " + response.StatusCode;
                    log.Log_Type = "Error";
                    log.Log_App = "Login";
                    log.Save();
                }
            }
            catch (Exception ex)
            {

                Log log = new Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Login";
                log.Save();

            }




        }
        private int phase = 0;

        private int reset = 0;

        public void Start(int ph)
        {
            if (ph != this.phase)
            {
                // ignore old calls
                return;
            }
            this.Start();
        }

        public void Start()
        {
            int ph = Interlocked.Increment(ref this.phase);
            Thread t = new Thread(new ThreadStart(delegate ()
            {
                Execute(ph);
            }));
            t.Start();
        }

        public void Reset()
        {
            if (Interlocked.CompareExchange(ref this.reset, 1, 0) == 0)
            {
                Disconnect(this.phase);
            }
        }
        public class SmartDispatcher : PropertyEventDispatcher
        {
            private static PropertyEventDispatcher instance = new SmartDispatcher();

            private static bool _designer = false;



            public static PropertyEventDispatcher getInstance()
            {
                return instance;
            }





            public void BeginInvoke(Action a)
            {
                //BeginInvoke(a, false);


            }


            public void addEventMessage(string message)
            {
                //instance.addEventMessage(message);
            }
        }
        private void Execute(int ph)
        {
            try { 
            if (ph != this.phase)
            {
                return;
            }
            ph = Interlocked.Increment(ref this.phase);
            this.LogIn();
            this.Connect(ph);
            this.ChartSubscribe();
            this.TradeSubscribe(CurrentAccountId);
            this._thisApp.igAccountId = CurrentAccountId;
                // this.subscribeChart()
            }
            catch (Exception ex)
            {

                Log log = new Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "Execute";
                log.Save();

            }
        }

        public void StatusChanged(int ph, int cStatus, string status)
        {
             if (ph != this.phase)
                return;
            try { 

            clsCommonFunctions.AddStatusMessage("Status changed to " + status + " (" + cStatus + ")", "INFO");
            if (cStatus == 0)
            {
                if (Interlocked.CompareExchange(ref this.reset, 0, 1) == 1)
                {
                    int phs = Interlocked.Increment(ref this.phase);
                    Thread t = new Thread(new ThreadStart(delegate ()
                    {
                        Execute(phs);
                    }));
                    t.Start();
                }
            }
            }
            catch (Exception ex)
            {

                Log log = new Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "StatusChanged";
                log.Save();

            }
        }

        public void ChartUpdateReceived(int ph, int itemPos, ItemUpdate update)
        {

            // Deal with chart updates here

            if (ph != this.phase)
                return;

            var epic = update.ItemName.Replace("L1:", "").Replace("CHART:", "").Replace(":TICK", "");
            try
            {
                var wlmUpdate = update ;



                //foreach (clsEpicList item in this.EpicList)
                //{
                //    if (item.Epic == epic)
                //    {
                //        item.counter++;
                //    }
                //}


                if (wlmUpdate.getValue("BID") != "" && wlmUpdate.getValue("OFR") != "")
                {
                    clsChartUpdate objUpdate = new clsChartUpdate();
                    if (wlmUpdate.getValue("BID") != null && wlmUpdate.getValue("OFR") != null)
                    {
                        _thisApp.currentTick.Epic = epic;
                        _thisApp.currentTick.Bid =Convert.ToDecimal(wlmUpdate.getValue("BID")) ;
                        _thisApp.currentTick.Offer = Convert.ToDecimal(wlmUpdate.getValue("OFR"));
                        _thisApp.currentTick.LTP = Convert.ToDecimal(wlmUpdate.getValue("LTP"));
                        _thisApp.currentTick.LTV = Convert.ToDecimal(wlmUpdate.getValue("LTV"));
                        _thisApp.currentTick.TTV = Convert.ToDecimal(wlmUpdate.getValue("TTV"));
                        if (wlmUpdate.getValue("UTM") != null)
                        {
                            _thisApp.currentTick.UTM = EpocStringToNullableDateTime( wlmUpdate.getValue("UTM"));
                        }
                        _thisApp.currentTick.DAY_OPEN_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_OPEN_MID"));
                        _thisApp.currentTick.DAY_NET_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_NET_CHG_MID"));
                        _thisApp.currentTick.DAY_PERC_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_PERC_CHG_MID"));
                        _thisApp.currentTick.DAY_HIGH = Convert.ToDecimal(wlmUpdate.getValue("DAY_HIGH"));
                        _thisApp.currentTick.DAY_LOW = Convert.ToDecimal(wlmUpdate.getValue("DAY_LOW"));
                    }



                    }


                }

            catch (Exception ex)
            {
                Log log = new Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "OnTickUpdate";
                log.Save();
            }

        }

        public async void TradeUpdateReceived(int ph, int itemPos, ItemUpdate update)
        {

            // Deal with Trade updates here
            clsCommonFunctions.AddStatusMessage($"Trade update received: ph={ph}, this.phase={this.phase}, FirstConfirmUpdate={this.FirstConfirmUpdate}", "INFO");

            if (ph != this.phase)
            {
                clsCommonFunctions.AddStatusMessage("Trade not updated as ph <> this.phase", "INFO");
                return;
            }
            try
            {
                if (!this.FirstConfirmUpdate)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Trade Subscription Update");
                    clsCommonFunctions.AddStatusMessage("Trade Subscription Update", "INFO");
                    try
                    {
                  
                        var confirms = update.getValue("CONFIRMS");
                        var opu = update.getValue("OPU");
                        var wou = update.getValue("WOU");

                        if (!(String.IsNullOrEmpty(opu)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - OPU" + opu);
                            await UpdateTs(itemPos, update.ItemName, update, opu, TradeSubscriptionType.Opu);
                        }
                        if (!(String.IsNullOrEmpty(wou)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - WOU" + wou);
                            await UpdateTs(itemPos, update.ItemName, update, wou, TradeSubscriptionType.Wou);
                        }
                        if (!(String.IsNullOrEmpty(confirms)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - CONFIRMS" + confirms);
                           await UpdateTs(itemPos, update.ItemName, update, confirms, TradeSubscriptionType.Confirm);
                        }

                    }
                    catch (Exception ex)
                    {
                        //_applicationViewModel.ApplicationDebugData += "Exception thrown in TradeSubscription Lightstreamer update" + ex.Message;
                    }
                }
                else { this.FirstConfirmUpdate = false; }
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TradeUpdateReceived";
                log.Epic = "";
                log.Save();
            }


            clsCommonFunctions.AddStatusMessage("Trade - " + itemPos, "INFO");

        }

        private async  Task<IgPublicApiData.TradeSubscriptionModel> UpdateTs(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {
           
            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);

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

                   

                    switch (updateType)
                    {
                        case TradeSubscriptionType.Opu:
                            tsm.TradeType = "OPU";
                            break;
                        case TradeSubscriptionType.Wou:
                            tsm.TradeType = "WOU";
                            break;
                        case TradeSubscriptionType.Confirm:
                            tsm.TradeType = "CONFIRM";
                            break;
                    }
                    clsCommonFunctions.AddStatusMessage("Trade update " + tsm.TradeType + " - " + inputData, "INFO");
                    // log the message
                    clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);

                    // Set the variables for a long or short trade
                    if (tsm.TradeType == "CONFIRM" && tsm.Status == "OPEN" && tsm.Epic == _thisApp.epicName)
                    {

                        if (tsm.Reason == "SUCCESS")
                        {
                            if (!_thisApp.model.onMarket)
                            {
                                _thisApp.currentTrade = new clsTradeUpdate();
                                _thisApp.currentTrade.epic = tsm.Epic;
                                _thisApp.currentTrade.dealReference = tsm.DealReference;
                                _thisApp.currentTrade.dealId = tsm.DealId;
                                _thisApp.currentTrade.lastUpdated = tsm.date;
                                _thisApp.currentTrade.status = tsm.Status;
                                _thisApp.currentTrade.dealStatus = tsm.DealStatus;
                                _thisApp.currentTrade.level = Convert.ToDecimal(tsm.Level);
                                _thisApp.currentTrade.stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel));
                                _thisApp.currentTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                _thisApp.currentTrade.size = Convert.ToDecimal(tsm.Size);
                                _thisApp.currentTrade.direction = tsm.Direction;
                                _thisApp.currentTrade.accountId = _thisApp.igAccountId;

                                _thisApp.model.thisModel.currentTrade = new tradeItem();
                                _thisApp.model.thisModel.currentTrade.quantity = Convert.ToDouble(_thisApp.currentTrade.size);
                                _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                _thisApp.model.thisModel.currentTrade.tbDealId = tsm.DealId;
                                _thisApp.model.thisModel.currentTrade.tbDealReference = tsm.DealReference;
                                _thisApp.model.thisModel.currentTrade.tbDealStatus = tsm.DealStatus;
                                _thisApp.model.thisModel.currentTrade.tbReason = tsm.Status;
                                _thisApp.model.stopPrice = _thisApp.model.thisModel.currentTrade.stopLossValue;
                                _thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                _thisApp.model.thisModel.currentTrade.tradeStarted = new DateTime(tsm.date.Year, tsm.date.Month, tsm.date.Day, tsm.date.Hour, tsm.date.Minute, tsm.date.Second);
                                _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID;
                                _thisApp.model.thisModel.currentTrade.epic = _thisApp.epicName;
                                _thisApp.model.thisModel.currentTrade.timestamp = DateTime.UtcNow;
                                _thisApp.model.thisModel.currentTrade.accountId = _thisApp.igAccountId;

                                if (tsm.Direction == "BUY")
                                {
                                    _thisApp.model.thisModel.currentTrade.longShort = "Long";
                                    _thisApp.model.thisModel.currentTrade.buyPrice = Convert.ToDecimal(_thisApp.currentTrade.level);
                                    _thisApp.model.thisModel.currentTrade.purchaseDate = tsm.date;

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
                                    //log.tradeType = "Long";
                                    //log.tradeAction = "Buy";
                                    //log.quantity = quantity;
                                    clsCommonFunctions.SendBroadcast("BuyLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade),_thisApp.the_app_db);
                                }
                                else
                                {
                                    _thisApp.model.thisModel.currentTrade.longShort = "Short";
                                    _thisApp.model.thisModel.currentTrade.sellPrice = (decimal)_thisApp.currentTrade.level;
                                    _thisApp.model.thisModel.currentTrade.sellDate = tsm.date;
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
                                    clsCommonFunctions.SendBroadcast("SellShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade),_thisApp.the_app_db);
                                }

                                // Save this trade in the database
                                _thisApp.model.thisModel.currentTrade.candleSold = null;
                                _thisApp.model.thisModel.currentTrade.candleBought = null;
                                _thisApp.model.thisModel.currentTrade.count = _thisApp.modelVar.counter;
                                await _thisApp.model.thisModel.currentTrade.Add(_thisApp.the_app_db, _thisApp.trade_container);

                                _thisApp.model.onMarket = true;


                                //Send email
                                string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                if (region == "LIVE")
                                {

                                    clsEmail obj = new clsEmail();
                                    List<recip> recips = new List<recip>();
                                    recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                    recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                    string subject = "NEW TRADE STARTED - " + _thisApp.currentTrade.epic;
                                    string text = "A new trade has started in the " + region + " environment</br></br>";
                                    text += "<ul>";
                                    text += "<li>Trade ID : " + _thisApp.currentTrade.dealId + "</li>";
                                    text += "<li>Epic : " + _thisApp.currentTrade.epic + "</li>";
                                    text += "<li>Date : " + _thisApp.currentTrade.lastUpdated + "</li>";
                                    text += "<li>Type : " + _thisApp.model.thisModel.currentTrade.longShort + "</li>";
                                    text += "<li>Size : " + _thisApp.currentTrade.size + "</li>";
                                    text += "<li>Price : " + _thisApp.currentTrade.level + "</li>";
                                    text += "<li>Stop Level : " + _thisApp.currentTrade.stopLevel + "</li>";
                                    text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                    text += "</ul>";

                                    obj.sendEmail(recips, subject, text);
                                }
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("CONFIRM deal failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "CONFIRM deal failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], _thisApp.the_app_db);
                                _thisApp.model.sellShort = false;
                                _thisApp.model.sellLong = false;
                                _thisApp.model.buyShort = false;
                                _thisApp.model.shortOnMarket = false;
                                _thisApp.model.buyLong = false;
                                _thisApp.model.longOnmarket = false;
                                _thisApp.model.onMarket = false;

                                // already on a trade so don't record this one please

                            }
                        }
                        else
                        {

                            clsCommonFunctions.AddStatusMessage("CONFIRM failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                            TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "CONFIRM failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason],_thisApp.the_app_db);
                            // manage failed deals here. Maybe retry once or twice if necessary. Should log error as well I guess.
                            //if (tsm.Direction == "BUY")
                            //{
                            //    _thisApp.model.buyLong = false;
                            //    _thisApp.model.longOnmarket = false;
                            //}
                            //else
                            //{
                            //    _thisApp.model.buyShort = false;
                            //    _thisApp.model.shortOnMarket = false;
                            //}

                            _thisApp.model.sellShort = false;
                            _thisApp.model.sellLong = false;
                            _thisApp.model.buyShort = false;
                            _thisApp.model.shortOnMarket = false;
                            _thisApp.model.buyLong = false;
                            _thisApp.model.longOnmarket = false;
                            _thisApp.model.onMarket = false;
                        }
                    }
                    if (tsm.TradeType == "CONFIRM" && tsm.Status == null && tsm.Epic == _thisApp.epicName)
                    {
                        TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "CONFIRM failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason],_thisApp.the_app_db);
                        _thisApp.model.sellShort = false;
                        _thisApp.model.sellLong = false;
                        _thisApp.model.buyShort = false;
                        _thisApp.model.shortOnMarket = false;
                        _thisApp.model.buyLong = false;
                        _thisApp.model.longOnmarket = false;
                        _thisApp.model.onMarket = false;
                    }

                    if (tsm.TradeType == "OPU")
                    {
                        if (tsm.Status == "UPDATED" && tsm.Epic == _thisApp.epicName)
                        {
                            // Deal has been updated, so save the new data and move on.
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                clsCommonFunctions.AddStatusMessage("Updating  - " + tsm.DealId + " - Current Deal = " + _thisApp.currentTrade.dealId, "INFO");
                                //Only update if it is the current trade that is affected (in case we have 2 trades running at the same time)
                                if (tsm.DealId == _thisApp.currentTrade.dealId)
                                {

                                    await _thisApp.GetTradeFromDB(tsm.DealId);
                                    _thisApp.model.thisModel.currentTrade.candleSold = null;
                                    _thisApp.model.thisModel.currentTrade.candleBought = null;
                                    //_thisApp.currentTrade = new clsTradeUpdate();
                                    //_thisApp.currentTrade.epic = tsm.Epic;
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

                                    //if (_thisApp.modelVar.breakEvenVar == 1)
                                    //{
                                    //    if (tsm.Direction == "BUY")
                                    //    {
                                    //        _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.level) + Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                    //    }
                                    //    else
                                    //    {
                                    //        _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.level) - Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                    //    }
                                    //}
                                    //else
                                    //{
                                    //    if (tsm.Direction == "BUY")
                                    //    {
                                    //        _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.level) - Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                    //    }
                                    //    else
                                    //    {
                                    //        _thisApp.model.thisModel.currentTrade.stopLossValue = Convert.ToDouble(_thisApp.currentTrade.level) + Convert.ToDouble(_thisApp.currentTrade.stopLevel);
                                    //    }
                                    //}
                                    _thisApp.model.stopPriceOld = Math.Abs(_thisApp.model.stopPrice);
                                    _thisApp.model.stopPrice = Math.Abs(_thisApp.model.thisModel.currentTrade.stopLossValue);

                                    await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);

                                    // Save the last run vars into the TB settings table
                                    _thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                    await _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                    clsCommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade),_thisApp.the_app_db);
                                    //_thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                }
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("UPDATE failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "UPDATE failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason],_thisApp.the_app_db);
                            }
                        }
                        else if (tsm.Status == "DELETED" && tsm.Epic == _thisApp.epicName)
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                // Deal has been closed (either by the software or by the stop being met).

                                //_thisApp.currentTrade = new clsTradeUpdate();
                                //_thisApp.currentTrade.epic = tsm.Epic;
                                //_thisApp.currentTrade.dealReference = tsm.DealReference;
                                //_thisApp.currentTrade.dealId = tsm.DealId;

                                clsCommonFunctions.AddStatusMessage("Deleting  - " + tsm.DealId + " - Current Deal = " + _thisApp.currentTrade.dealId, "INFO");
                                //Only delete if it is the current trade that is affected (in case we have 2 trades running at the same time)
                                if (tsm.DealId == _thisApp.currentTrade.dealId)
                                {
                                    DateTime dtNow = DateTime.UtcNow;
                                    await _thisApp.GetTradeFromDB(tsm.DealId);
                                    _thisApp.model.thisModel.currentTrade.candleSold = null;
                                    _thisApp.model.thisModel.currentTrade.candleBought = null;

                                    _thisApp.currentTrade.lastUpdated = dtNow;
                                    _thisApp.currentTrade.status = tsm.Status;
                                    _thisApp.currentTrade.dealStatus = tsm.DealStatus;
                                    _thisApp.currentTrade.level = Convert.ToDecimal(tsm.Level);
                                    _thisApp.currentTrade.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                    _thisApp.currentTrade.stopDistance = Convert.ToDecimal(tsm.StopDistance);

                                    if (Convert.ToDecimal(tsm.Size) > 0)
                                    {
                                        _thisApp.currentTrade.size = Convert.ToDecimal(tsm.Size);
                                    }
                                    _thisApp.currentTrade.direction = tsm.Direction;

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
                                        clsCommonFunctions.SendBroadcast("SellLong", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade),_thisApp.the_app_db);
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
                                        clsCommonFunctions.SendBroadcast("BuyShort", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade),_thisApp.the_app_db);
                                    }
                                    _thisApp.model.sellLong = false;
                                    _thisApp.model.buyLong = false;
                                    _thisApp.model.longOnmarket = false;
                                    _thisApp.model.buyShort = false;
                                    _thisApp.model.sellShort = false;
                                    _thisApp.model.shortOnMarket = false;

                                    //_thisApp.model.thisModel.currentTrade.tradeValue = _thisApp.model.thisModel.currentTrade.buyPrice - _thisApp.model.thisModel.currentTrade.sellPrice;

                                    _thisApp.model.modelVar.strategyProfit += _thisApp.model.thisModel.currentTrade.tradeValue;
                                    if (_thisApp.model.thisModel.currentTrade.tradeValue <= 0)
                                    {
                                        _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss + (double)Math.Abs(_thisApp.model.thisModel.currentTrade.tradeValue);
                                    }
                                    else
                                    {
                                        //_thisApp.model.modelVar.carriedForwardLoss = 0;
                                        _thisApp.model.modelVar.carriedForwardLoss = _thisApp.model.modelVar.carriedForwardLoss - (double)Math.Abs(_thisApp.model.thisModel.currentTrade.tradeValue);
                                        if (_thisApp.model.modelVar.carriedForwardLoss < 0) { _thisApp.model.modelVar.carriedForwardLoss = 0; }
                                        _thisApp.model.modelVar.currentGain += (double)_thisApp.model.thisModel.currentTrade.tradeValue;
                                    }
                                    if (_thisApp.model.modelVar.strategyProfit > _thisApp.model.modelVar.maxStrategyProfit) { _thisApp.model.modelVar.maxStrategyProfit = _thisApp.model.modelVar.strategyProfit; }

                                    // Save the last run vars into the TB settings table
                                    _thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                    await _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                    _thisApp.model.thisModel.currentTrade.units = _thisApp.model.thisModel.currentTrade.sellPrice - _thisApp.model.thisModel.currentTrade.buyPrice;
                                    _thisApp.model.thisModel.currentTrade.tbDealStatus = tsm.DealStatus;

                                    _thisApp.model.thisModel.currentTrade.timestamp = DateTime.UtcNow;
                                    _thisApp.model.thisModel.currentTrade.candleSold = null;
                                    _thisApp.model.thisModel.currentTrade.candleBought = null;
                                    if (_thisApp.epicName != "") { _thisApp.model.thisModel.currentTrade.epic = _thisApp.epicName; }
                                    if (_thisApp.modelID != "") { _thisApp.model.thisModel.currentTrade.modelRunID = _thisApp.modelID; }
                                    _thisApp.model.thisModel.currentTrade.tbReason = tsm.Status;
                                    _thisApp.model.thisModel.modelTrades.Add(_thisApp.model.thisModel.currentTrade);

                                    clsCommonFunctions.AddStatusMessage("Saving trade", "INFO");
                                    await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                    clsCommonFunctions.AddStatusMessage("Trade saved", "INFO");

                                    //Send email
                                    string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                    if (region == "LIVE")
                                    {
                                        clsEmail obj = new clsEmail();
                                        List<recip> recips = new List<recip>();
                                        recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
                                        recips.Add(new recip("Dave Merriman", "dave.merriman72@btinternet.com"));
                                        string subject = "TRADE ENDED - " + _thisApp.currentTrade.epic;
                                        string text = "The trade has ended in the " + region + " environment</br></br>";
                                        text += "<ul>";
                                        text += "<li>Trade ID : " + _thisApp.currentTrade.dealId + "</li>";
                                        text += "<li>Epic : " + _thisApp.currentTrade.epic + "</li>";
                                        text += "<li>Date : " + _thisApp.currentTrade.lastUpdated + "</li>";
                                        text += "<li>Type : " + _thisApp.model.thisModel.currentTrade.longShort + "</li>";
                                        text += "<li>Trade value : " + _thisApp.model.thisModel.currentTrade.tradeValue + "</li>";
                                        text += "<li>Size : " + _thisApp.currentTrade.size + "</li>";
                                        text += "<li>Price : " + _thisApp.currentTrade.level + "</li>";
                                        text += "<li>Stop Level : " + _thisApp.currentTrade.stopLevel + "</li>";
                                        text += "<li>NG count : " + _thisApp.modelVar.counter + "</li>";
                                        text += "</ul>";
                                        obj.sendEmail(recips, subject, text);
                                    }

                                    _thisApp.model.thisModel.currentTrade = null;
                                    _thisApp.currentTrade = null;
                                    _thisApp.model.onMarket = false;



                                }
                            }
                            else
                            {
                                clsCommonFunctions.AddStatusMessage("DELETED failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason], "ERROR");
                                TradingBrain.Models.clsCommonFunctions.SaveLog("Error", "UpdateTs", "DELETED failed - " + tsm.Reason + " - " + _thisApp.TradeErrors[tsm.Reason],_thisApp.the_app_db);
                            }


                        }
                        else if (tsm.Status == "OPEN")
                        {
                            //Deal has bneen opened, however this should be caught by the CONFIRM message
                            // check to see if we don't already have a deal open as this could have come from the IG Portal
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                if (tsm.Channel == "WTP")
                                {

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
                log.Log_App = "UpdateTs";
                log.Epic = "";
                await log.Save();
            }
            return tsm;
        }

        protected decimal? StringToNullableDecimal(string value)
        {
            decimal number;
            return decimal.TryParse(value, out number) ? number : (decimal?)null;
        }

        protected int? StringToNullableInt(string value)
        {
            int number;
            return int.TryParse(value, out number) ? number : (int?)null;
        }

        protected DateTime? EpocStringToNullableDateTime(string value)
        {
            ulong epoc;
            if (!ulong.TryParse(value, out epoc))
            {
                return null;
            }
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(epoc);
        }
        private void Connect(int ph)
        {
            bool connected = false;
            //this method will not exit until the openConnection returns without throwing an exception
            while (!connected)
            {
                try
                {
                    if (ph != this.phase)
                        return;
                    ph = Interlocked.Increment(ref this.phase);
                    client.addListener(new ChartConnectionListener(this, ph));
                    client.addListener(new TradeConnectionListener(this, ph));
                    client.connect();
                    connected = true;
                }
                catch (Exception e)
                {

                }

                if (!connected)
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void Disconnect(int ph)
        {
            //demoForm.Invoke(statusChangeDelegate, new Object[] {
            //        StocklistConnectionListener.VOID,
            //        "Disconnecting to Lightstreamer Server @ " + client.connectionDetails.ServerAddress
            //    });
            try
            {
                client.disconnect();
            }
            catch (Exception e)
            {
                //demoForm.Invoke(statusChangeDelegate, new Object[] {
                //        StocklistConnectionListener.VOID, e.Message
                //    });
            }
        }

        private void ChartSubscribe()
        {
            //this method will try just one subscription.
            //we know that when this method executes we should be already connected
            //If we're not or we disconnect while subscribing we don't have to do anything here as an
            //event will be (or was) sent to the ConnectionListener that will handle the case.
            //If we're connected but the subscription fails we can't do anything as the same subscription 
            //would fail again and again (btw this should never happen)

            try
            {
                string chartName = "CHART:IX.D.NASDAQ.CASH.IP:TICK";
                if (_thisApp.epicName != "")
                {
                    chartName = "CHART:" + _thisApp.epicName + ":TICK";
                }
                subscription = new Subscription("DISTINCT", new string[1] { chartName }, new string[11] { "BID", "OFR", "LTP", "LTV", "TTV", "UTM", "DAY_OPEN_MID", "DAY_NET_CHG_MID", "DAY_PERC_CHG_MID", "DAY_HIGH", "DAY_LOW" });


                //subscription = new Subscription("MERGE", new string[30] { "item1", "item2", "item3", "item4", "item5", "item6", "item7", "item8", "item9", "item10", "item11", "item12", "item13", "item14", "item15", "item16", "item17", "item18", "item19", "item20", "item21", "item22", "item23", "item24", "item25", "item26", "item27", "item28", "item29", "item30" },
                //    new string[12] { "stock_name", "last_price", "time", "pct_change", "bid_quantity", "bid", "ask", "ask_quantity", "min", "max", "ref_price", "open_price" });
                //subscription.DataAdapter = "QUOTE_ADAPTER";
                subscription.RequestedSnapshot = "yes";

                subscription.addListener(new ChartSubscriptionListener(this, this.phase));
                client.subscribe(subscription);
            }
            catch (Exception e)
            {
                var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "ChartSubscribe";
                log.Epic = "";
                log.Save();
            }
        }
        private void TradeSubscribe(string accountId)
        {
            //this method will try just one subscription.
            //we know that when this method executes we should be already connected
            //If we're not or we disconnect while subscribing we don't have to do anything here as an
            //event will be (or was) sent to the ConnectionListener that will handle the case.
            //If we're connected but the subscription fails we can't do anything as the same subscription 
            //would fail again and again (btw this should never happen)

            try
            {

                subscription = new Subscription("DISTINCT", new string[1] { "TRADE:" + accountId }, new string[3] { "CONFIRMS", "OPU", "WOU" });


                //subscription = new Subscription("MERGE", new string[30] { "item1", "item2", "item3", "item4", "item5", "item6", "item7", "item8", "item9", "item10", "item11", "item12", "item13", "item14", "item15", "item16", "item17", "item18", "item19", "item20", "item21", "item22", "item23", "item24", "item25", "item26", "item27", "item28", "item29", "item30" },
                //    new string[12] { "stock_name", "last_price", "time", "pct_change", "bid_quantity", "bid", "ask", "ask_quantity", "min", "max", "ref_price", "open_price" });
                //subscription.DataAdapter = "QUOTE_ADAPTER";
                subscription.RequestedSnapshot = "no";

                subscription.addListener(new TradeSubscriptionListener(this, this.phase));
                client.subscribe(subscription);
            }
            catch (Exception e)
            {
                //demoForm.Invoke(statusChangeDelegate, new Object[] {
                //        StocklistConnectionListener.VOID, e.Message
                //    });
                var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TradeSubscribe";
                log.Epic = "";
                log.Save();
            }
        }
        internal void ForceTransport(string selectedText)
        {
            if (selectedText.StartsWith("no"))
            {
                client.connectionOptions.ForcedTransport = null;
            }
            else
            {
                client.connectionOptions.ForcedTransport = selectedText;
            }

        }

        internal void MaxFrequency(int value)
        {
            switch (value)
            {
                case 0:
                    subscription.RequestedMaxFrequency = "unlimited";
                    break;
                case 1:
                    subscription.RequestedMaxFrequency = "5";
                    break;
                case 2:
                    subscription.RequestedMaxFrequency = "2";
                    break;
                case 3:
                    subscription.RequestedMaxFrequency = "1";
                    break;
                case 4:
                    subscription.RequestedMaxFrequency = "0.5";
                    break;
                case 5:
                    subscription.RequestedMaxFrequency = "0.3";
                    break;
                case 6:
                    subscription.RequestedMaxFrequency = "0.2";
                    break;
                case 7:
                    subscription.RequestedMaxFrequency = "0.1";
                    break;
                case 8:
                    subscription.RequestedMaxFrequency = "0.05";
                    break;
                case 9:
                    subscription.RequestedMaxFrequency = "0.01";
                    break;
                default:
                    subscription.RequestedMaxFrequency = "0.01";
                    break;
            }

            // client.subscribe(subscription);
        }
    }
 
}
