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
using IGWebApiClient.Models;

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
using System.Diagnostics.Eventing.Reader;
using System.ComponentModel;
using Microsoft.Azure.Cosmos;
using Container = Microsoft.Azure.Cosmos.Container;
using static TradingBrain.Models.clsCommonFunctions;
using dto.endpoint.accountswitch;
using Microsoft.Identity.Client;
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
            string accountId = igWebApiConnectionConfig["accountId." + env] ?? "";

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

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _thisApp.igRestApiClient.accountSwitch(asr);

                        if (response2 != null)
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + accountId, "INFO");
                        }
                        else
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                        }
                    }
                    else
                    {
                        clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                    }

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
            string accountId = igWebApiConnectionConfig["accountId." + env] ?? "";
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

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _thisApp.igRestApiClient.accountSwitch(asr);

                        if (response2 != null)
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + accountId, "INFO");
                            _thisApp.igAccountId = accountId;
                        }
                        else
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                            _thisApp.igAccountId = response.Response.currentAccountId;
                        }
                    }
                    else
                    {
                        clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                        _thisApp.igAccountId = response.Response.currentAccountId;
                    }

                    _thisApp.context = _thisApp.igRestApiClient.GetConversationContext();

                    clsCommonFunctions.AddStatusMessage("establishing datastream connection", "INFO");

                    if ((_thisApp.context != null) && (response.Response.lightstreamerEndpoint != null) &&
                        (_thisApp.context.apiKey != null) && (_thisApp.context.xSecurityToken != null) && (_thisApp.context.cst != null))
                    {
                        try
                        {
                            CurrentAccountId = _thisApp.igAccountId;


                            client = new LightstreamerClient(response.Response.lightstreamerEndpoint, "DEFAULT");
                            client.connectionDetails.User = _thisApp.igAccountId;
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
                    //clsCommonFunctions.AddStatusMessage("Trade Subscription Update", "INFO");
                    try
                    {
                  
                        var confirms = update.getValue("CONFIRMS");
                        var opu = update.getValue("OPU");
                        var wou = update.getValue("WOU");

                        if (!(String.IsNullOrEmpty(opu)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - OPU" + opu);
                            await UpdateTsOPU(itemPos, update.ItemName, update, opu, TradeSubscriptionType.Opu);
                        }
                        if (!(String.IsNullOrEmpty(wou)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - WOU" + wou);
                            await UpdateTsWOU(itemPos, update.ItemName, update, wou, TradeSubscriptionType.Wou);
                        }
                        if (!(String.IsNullOrEmpty(confirms)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - CONFIRMS" + confirms);
                           await UpdateTsCONFIRM(itemPos, update.ItemName, update, confirms, TradeSubscriptionType.Confirm);
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


            //clsCommonFunctions.AddStatusMessage("Trade - " + itemPos, "INFO");

        }

          private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsOPU(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
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
                    //clsCommonFunctions.AddStatusMessage("Trade update " + tsm.TradeType + " - " + inputData, "INFO");
                    //clsCommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                    //clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);

                    if (tsm.Epic == _thisApp.epicName)
                    {
                        if (tsm.Status == "UPDATED" )
                        {
                            // Deal has been updated, so save the new data and move on.
                            if (tsm.DealStatus == "ACCEPTED")
                            {

                                //Only update if it is the current trade or is the supplementary trade that is affected (in case we have 2 trades running at the same time)
                                if (tsm.DealId == _thisApp.currentTrade.dealId)
                                {
                                    clsCommonFunctions.AddStatusMessage($"Trade update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");
                                    clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);
                                    clsCommonFunctions.AddStatusMessage("Updating  - " + tsm.DealId + " - Current Deal = " + _thisApp.currentTrade.dealId, "INFO");
                                    await _thisApp.GetTradeFromDB(tsm.DealId, _thisApp.strategy,_thisApp.resolution);
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

                                    await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);

                                    // Save the last run vars into the TB settings table
                                    _thisApp.tb.lastRunVars = _thisApp.model.modelVar.DeepCopy();
                                    await _thisApp.tb.SaveDocument(_thisApp.the_app_db);

                                    clsCommonFunctions.SendBroadcast("DealUpdated", JsonConvert.SerializeObject(_thisApp.model.thisModel.currentTrade), _thisApp.the_app_db);
                                    //_thisApp.model.stopPriceOld = _thisApp.model.stopPrice;
                                }
                                else
                                {
                                    if (_thisApp.suppTrade != null)
                                    {
                                        if (tsm.DealId == _thisApp.suppTrade.dealId)
                                        {
                                            clsCommonFunctions.AddStatusMessage("Updating supp trade  - " + tsm.DealId + " - Current Deal = " + _thisApp.suppTrade.dealId, "INFO");
                                            await _thisApp.GetTradeFromDB(tsm.DealId,_thisApp.strategy,_thisApp.resolution);
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

                                            await _thisApp.model.thisModel.suppTrade.SaveDocument(_thisApp.trade_container);

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
                            }

                            await tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                        else if (tsm.Status == "DELETED" )
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
                                        await _thisApp.GetTradeFromDB(tsm.DealId,_thisApp.strategy, _thisApp.resolution);
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
                                        _thisApp.model.modelVar.numCandlesOnMarket = 0;
                                        _thisApp.model.thisModel.currentTrade.numCandlesOnMarket = _thisApp.model.modelVar.numCandlesOnMarket;
                                        clsCommonFunctions.AddStatusMessage("Saving trade", "INFO");
                                        await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                        clsCommonFunctions.AddStatusMessage("Trade saved", "INFO");

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
                                            string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                            if (region == "LIVE")
                                            {
                                                clsEmail obj = new clsEmail();
                                                List<recip> recips = new List<recip>();
                                                //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
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
                                        }catch(Exception ex)
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
                                        await _thisApp.GetTradeFromDB(tsm.DealId,_thisApp.strategy,_thisApp.resolution);

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
                                        await _thisApp.model.thisModel.suppTrade.SaveDocument(_thisApp.trade_container);
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
                                        }catch(Exception ex)
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
                            }
                            await tradeSubUpdate.Add(_thisApp.the_app_db);

                        }
                        else if (tsm.Status == "OPEN" )
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
                                                await _thisApp.model.thisModel.suppTrade.Add(_thisApp.the_app_db, _thisApp.trade_container);

                                                _thisApp.model.thisModel.currentTrade.hasSuppTrade = true;

                                                //Update the current trade to have the same stop loss as this one.

                                                _thisApp.currentTrade.stopLevel = Math.Abs(Convert.ToDecimal(tsm.StopLevel));

                                                if (tsm.Direction == "BUY")
                                                {
                                                    _thisApp.model.thisModel.currentTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.suppTrade.stopLevel) - (double)_thisApp.model.thisModel.currentTrade.buyPrice);
                                                    _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = (decimal)_thisApp.suppTrade.stopLevel;
                                                    _thisApp.EditDeal((double)_thisApp.suppTrade.stopLevel, _thisApp.model.thisModel.currentTrade.tbDealId);
                                                }
                                                else
                                                {
                                                    _thisApp.model.thisModel.currentTrade.stopLossValue = Math.Abs(Convert.ToDouble(_thisApp.suppTrade.stopLevel) - (double)_thisApp.model.thisModel.currentTrade.sellPrice);
                                                    _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = (decimal)_thisApp.suppTrade.stopLevel;
                                                    _thisApp.EditDeal((double)_thisApp.suppTrade.stopLevel, _thisApp.model.thisModel.currentTrade.tbDealId);

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
                                                reqTrade.dealReference = await _thisApp.PlaceOrder(tsm.Direction, orderValues.quantity, orderValues.stopDistance, _thisApp.igAccountId, orderValues.level);
                                                _thisApp.requestedTrades.Add(reqTrade);

                                            }

                                            _thisApp.model.thisModel.currentTrade.targetPrice = orderValues.targetPrice;
                                        }

                                        if (_thisApp.strategy == "RSI")
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

                                        await _thisApp.model.thisModel.currentTrade.Add(_thisApp.the_app_db, _thisApp.trade_container);

                                        //Send email
                                        string region = IGModels.clsCommonFunctions.Get_AppSetting("region").ToUpper();
                                        try
                                        {
                                            if (region == "LIVE")
                                            {

                                                clsEmail obj = new clsEmail();
                                                List<recip> recips = new List<recip>();
                                                //recips.Add(new recip("Mike Ward", "n278mp@gmail.com"));
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
                            }
                            await tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                    }
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
            return tsm;
        }
        private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsWOU(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
                TradeSubUpdate tradeSubUpdate =  JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
                tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
                tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
                tradeSubUpdate.updateType = "WOU";

                if (tradeSubUpdate.epic == _thisApp.epicName )
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
                    tsm.TradeType = "WOU";
                    if(tsm.Reason == null) { tsm.Reason = ""; }
                    tradeSubUpdate.reasonDescription = _thisApp.TradeErrors[tsm.Reason];

                    //////////////////////////////
                    /// Working order updates   //
                    //////////////////////////////
                    if (tsm.Epic == _thisApp.epicName)
                    {
                        clsCommonFunctions.AddStatusMessage($"Order update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");

                        if (tsm.Status == "OPEN" )
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                requestedTrade reqTrade = new requestedTrade();
                                reqTrade = _thisApp.requestedTrades.Where(i => i.dealReference == tsm.DealReference).FirstOrDefault();

                                if (reqTrade != null)
                                {
                                    //clsCommonFunctions.AddStatusMessage("Trade update " + tsm.TradeType + "-" + tsm.DealReference + " - " + inputData, "INFO");
                                    //clsCommonFunctions.SaveLog("TradeUpdate", "UpdateTs", "Trade update " + tsm.TradeType + " - " + inputData, _thisApp.the_app_db);

                                    reqTrade.dealStatus = tsm.DealStatus;

                                //    if (_thisApp.model.thisModel.currentTrade.attachedOrder == null || _thisApp.model.thisModel.currentTrade.attachedOrder.dealId == "")
                                //{
                                    orderItem thisOrder = new orderItem();
                                    thisOrder.dealId = tsm.DealId;
                                    thisOrder.direction = tsm.Direction;
                                    thisOrder.createdDate = DateTime.Now; ;
                                    thisOrder.createdDateUTC = DateTime.UtcNow;
                                    thisOrder.status = tsm.Status;
                                    thisOrder.epic = tsm.Epic;
                                    thisOrder.orderLevel = Convert.ToDecimal(tsm.Level);
                                    thisOrder.orderSize = Convert.ToDouble(tsm.Size);
                                    thisOrder.accountId = _thisApp.igAccountId;
                                    thisOrder.channel = tsm.Channel;
                                    thisOrder.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                    thisOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                    thisOrder.dealReference = tsm.DealReference;
                                    thisOrder.dealStatus = tsm.DealStatus;
                                    thisOrder.associatedDealId = _thisApp.model.thisModel.currentTrade.tbDealId;
                                    _thisApp.model.thisModel.currentTrade.suppOrderId = tsm.DealId;
                                    _thisApp.model.thisModel.currentTrade.attachedOrder = thisOrder;

                                    if (tsm.Direction == "BUY")
                                    {
                                        thisOrder.stopLevel = thisOrder.orderLevel - thisOrder.stopDistance;
                                    }
                                    else
                                    {
                                        thisOrder.stopLevel = thisOrder.orderLevel + thisOrder.stopDistance;
                                    }

                                    // reset any retry logic if needs be as this order has been successful
                                    _thisApp.retryOrder = false;
                                    _thisApp.retryOrderCount = 0;

                                    //Save current trade with this suppOrderId
                                    await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                    clsCommonFunctions.AddStatusMessage($"Order {thisOrder.dealId} saved to current trade - {_thisApp.model.thisModel.currentTrade.tbDealId}", "INFO");
                                }
                            }
                            await tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                        if (tsm.Status == "DELETED" )
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                string orderDealId = tsm.DealId;

                                if (_thisApp.model.thisModel.currentTrade != null)
                                {
                                    if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId == orderDealId)
                                    {
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.deletedDate = DateTime.Now;
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.status = tsm.Status;
                                        await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                    }
                                }
                                else
                                {
                                    //Get trade by the attachedOrder dealid
                                    tradeItem thisTrade = await _thisApp.GetTradeFromDBByOrder(orderDealId);
                                    thisTrade.attachedOrder.deletedDate = DateTime.Now;
                                    thisTrade.attachedOrder.status = tsm.Status;
                                    await thisTrade.SaveDocument(_thisApp.trade_container);
                                }
                            }
                            await tradeSubUpdate.Add(_thisApp.the_app_db);
                        }
                        if (tsm.Status == "UPDATED")
                        {
                            if (tsm.DealStatus == "ACCEPTED")
                            {
                                string orderDealId = tsm.DealId;

                                if (_thisApp.model.thisModel.currentTrade != null)
                                {
                                    if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId == orderDealId)
                                    {
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.stopDistance = Convert.ToDecimal(tsm.StopDistance);
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.orderLevel = Convert.ToDecimal(tsm.Level);
                                        _thisApp.model.thisModel.currentTrade.attachedOrder.status = tsm.Status;
                                        await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
                                    }
                                }
                                else
                                {
                                    //Get trade by the attachedOrder dealid
                                    tradeItem thisTrade = await _thisApp.GetTradeFromDBByOrder(orderDealId);
                                    thisTrade.attachedOrder.stopDistance =Convert.ToDecimal(tsm.StopDistance);
                                    thisTrade.attachedOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
                                    thisTrade.attachedOrder.orderLevel = Convert.ToDecimal(tsm.Level);
                                    await thisTrade.SaveDocument(_thisApp.trade_container);
                                }

                            }
                            await tradeSubUpdate.Add(_thisApp.the_app_db);

                        }
                    }

 
                }

            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "UpdateTsWOU";
                log.Epic = "";
                await log.Save();
            }
            return tsm;
        }
        private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsCONFIRM(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            try
            {
                var tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);

                if (tradeSubUpdate.epic == _thisApp.epicName )
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
                            //clsCommonFunctions.AddStatusMessage("Trade confirmation - " + inputData, "INFO");
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
