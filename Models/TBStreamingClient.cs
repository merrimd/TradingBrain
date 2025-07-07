using com.lightstreamer.client;
using dto.endpoint.accountswitch;
using dto.endpoint.auth.session.v2;
using IGModels;
using IGWebApiClient;
using IGWebApiClient.Common;
using IGWebApiClient.Models;
using Lightstreamer.DotNet.Client;
using Lightstreamer.DotNet.Client.Test;
using Lightstreamer.DotNet.Logging.Log; 
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using NLog;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TradingBrain.Models;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using static TradingBrain.Models.clsCommonFunctions;
using Container = Microsoft.Azure.Cosmos.Container;
using LogLevel = NLog.LogLevel;
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
        public IGContainer _igContainer;
        public TBStreamingClient(
                string pushServerUrl,
                string forceT,
                IGContainer igContainer,
                Delegates.LightstreamerUpdateDelegate lsUpdateDelegate,
                Delegates.LightstreamerStatusChangedDelegate lsStatusChangeDelegate)
        {
            try
            {
                _igContainer = igContainer;
                //_igRestApiClient = igContainer.igRestApiClient;
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

                bool connectionEstablished = false;

                SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();
                object v = ConfigurationManager.GetSection("appSettings");
                NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
                string env = igWebApiConnectionConfig["environment"] ?? "DEMO";
                _igContainer.igRestApiClient = new IgRestApiClient(env, smartDispatcher);
                //_thisApp.igAccountId = this.client.connectionDetails.User;
            }
            catch (Exception ex)
            {
                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "TBStreamingClient";
                //log.Save();
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
            _igContainer.Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();
            try
            {
                var response = await _igContainer.igRestApiClient.SecureAuthenticate(ar, apiKey);
                if (response && (response.Response != null) && (response.Response.accounts.Count > 0))
                {
                    _igContainer.Accounts.Clear();

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

                        _igContainer.Accounts.Add(igAccount);

                        clsCommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName,"INFO");
                    }

                    LoggedIn = true;

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _igContainer.igRestApiClient.accountSwitch(asr);

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

                    _igContainer.context = _igContainer.igRestApiClient.GetConversationContext();

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
            _igContainer.Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();

            try
            {
                var response = await _igContainer.igRestApiClient.SecureAuthenticate(ar, apiKey);
                if (response && (response.Response != null) && (response.Response.accounts.Count > 0))
                {
                    _igContainer.Accounts.Clear();

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

                        _igContainer.Accounts.Add(igAccount);

                        clsCommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName, "INFO");
                    }
                    LoggedIn = true;

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _igContainer.igRestApiClient.accountSwitch(asr);

                        if (response2 != null)
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + accountId, "INFO");
                            _igContainer.igAccountId = accountId;
                        }
                        else
                        {
                            clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                            _igContainer.igAccountId = response.Response.currentAccountId;
                        }
                    }
                    else
                    {
                        clsCommonFunctions.AddStatusMessage("Logged in, current account: " + response.Response.currentAccountId, "INFO");
                        _igContainer.igAccountId = response.Response.currentAccountId;
                    }

                    _igContainer.context = _igContainer.igRestApiClient.GetConversationContext();

                    clsCommonFunctions.AddStatusMessage("establishing datastream connection", "INFO");

                    if ((_igContainer.context != null) && (response.Response.lightstreamerEndpoint != null) &&
                        (_igContainer.context.apiKey != null) && (_igContainer.context.xSecurityToken != null) && (_igContainer.context.cst != null))
                    {
                        try
                        {
                            CurrentAccountId = _igContainer.igAccountId;


                            client = new LightstreamerClient(response.Response.lightstreamerEndpoint, "DEFAULT");
                            client.connectionDetails.User = _igContainer.igAccountId;
                            client.connectionDetails.Password = string.Format("CST-{0}|XST-{1}", _igContainer.context.cst, _igContainer.context.xSecurityToken);// string.Format("CST-{0}|XST-{1}", cstToken, xSecurityToken);
                            client.connectionDetails.ServerAddress = response.Response.lightstreamerEndpoint;


                        }
                        catch (Exception ex)
                        {
                            clsCommonFunctions.AddStatusMessage(ex.Message, "ERROR");
                        }
                    }


                    // Run a timer for every 10 mins to keep the connection alive.

                    // set a timeout to run this again in 30 mins. this will keep the session active.
                    clsCommonFunctions.AddStatusMessage("Setting a timer so we can re run the AccountDetails after 10 mins to stop it expiring.", "INFO" );
                    System.Timers.Timer timer = new System.Timers.Timer(600000); // 10 mins
                    timer.Elapsed += OnTimedEvent;
                    timer.AutoReset = true; // Run only once
                    timer.Enabled = true;
                    //get any current positions

                    //DTM
                    //_thisApp.GetPositions();
                }
                else
                {
                    clsCommonFunctions.AddStatusMessage("Failed to login. HttpResponse StatusCode = " +
                                        response.StatusCode,"ERROR");
                    //Log log = new Log(_thisApp.the_app_db);
                    //log.Log_Message = "Failed to login. HttpResponse StatusCode = " + response.StatusCode;
                    //log.Log_Type = "Error";
                    //log.Log_App = "Login";
                    //log.Save();
                }
            }
            catch (Exception ex)
            {

                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "Login";
                //log.Save();

            }




        }
        public void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            clsCommonFunctions.AddStatusMessage("Calling the AccountDetails API to ensure token doesn't expire", "INFO");
            try
            {
                IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = _igContainer.igRestApiClient.accountBalance().Result;
                if (ret != null)
                {
                    clsCommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO");

                    if (ret.StatusCode.ToString() == "Forbidden")
                    {
                        // re connect to API

                    }
                }
            }
            catch (Exception ex) { }
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
            //this.ChartSubscribe();
            this.TradeSubscribe(CurrentAccountId);
            this._igContainer.igAccountId = CurrentAccountId;
                // this.subscribeChart()
            }
            catch (Exception ex)
            {

                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "Execute";
                //log.Save();

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

                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "StatusChanged";
                //log.Save();

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
                //var wlmUpdate = update ;



                //foreach (clsEpicList item in this.EpicList)
                //{
                //    if (item.Epic == epic)
                //    {
                //        item.counter++;
                //    }
                //}


                //if (wlmUpdate.getValue("BID") != "" && wlmUpdate.getValue("OFR") != "")
                //{
                //    clsChartUpdate objUpdate = new clsChartUpdate();
                //    if (wlmUpdate.getValue("BID") != null && wlmUpdate.getValue("OFR") != null)
                //    {
                //        _thisApp.currentTick.Epic = epic;
                //        _thisApp.currentTick.Bid = Convert.ToDecimal(wlmUpdate.getValue("BID"));
                //        _thisApp.currentTick.Offer = Convert.ToDecimal(wlmUpdate.getValue("OFR"));
                //        _thisApp.currentTick.LTP = Convert.ToDecimal(wlmUpdate.getValue("LTP"));
                //        _thisApp.currentTick.LTV = Convert.ToDecimal(wlmUpdate.getValue("LTV"));
                //        _thisApp.currentTick.TTV = Convert.ToDecimal(wlmUpdate.getValue("TTV"));
                //        if (wlmUpdate.getValue("UTM") != null)
                //        {
                //            _thisApp.currentTick.UTM = EpocStringToNullableDateTime(wlmUpdate.getValue("UTM"));
                //        }
                //        _thisApp.currentTick.DAY_OPEN_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_OPEN_MID"));
                //        _thisApp.currentTick.DAY_NET_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_NET_CHG_MID"));
                //        _thisApp.currentTick.DAY_PERC_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_PERC_CHG_MID"));
                //        _thisApp.currentTick.DAY_HIGH = Convert.ToDecimal(wlmUpdate.getValue("DAY_HIGH"));
                //        _thisApp.currentTick.DAY_LOW = Convert.ToDecimal(wlmUpdate.getValue("DAY_LOW"));
                //    }



                //}


            }

            catch (Exception ex)
            {
                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "OnTickUpdate";
                //log.Save();
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
                //var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "TradeUpdateReceived";
                //log.Epic = "";
                //log.Save();
            }


            //clsCommonFunctions.AddStatusMessage("Trade - " + itemPos, "INFO");

        }

          private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsOPU(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            //try
            //{
            //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
            TradeSubUpdate tradeSubUpdate = (TradeSubUpdate)JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
            tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
            tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
            tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
            tradeSubUpdate.updateType = "OPU";

            MainApp _thisApp = null;

            foreach (MainApp wrk in _igContainer.workerList)
            {
                //List<string> parm = new List<string>();
                //parm = wrk._thread.Name.Split("|").ToList();
                if (wrk.epicName == tradeSubUpdate.epic)
                {
                    updateMessage msg = new updateMessage();
                    msg.itemName = update.ItemName;
                    msg.updateData = inputData;
                    msg.updateType = "UPDATE";
                    runRet taskRet = await wrk.iGUpdate(msg);
                }
            }


            return tsm;
        }
        private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsWOU(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            //try
            //{
            //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
            //    TradeSubUpdate tradeSubUpdate =  JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
            //    tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
            //    tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
            //    tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
            //    tradeSubUpdate.updateType = "WOU";

            //    if (tradeSubUpdate.epic == _thisApp.epicName )
            //    {
            //        tradeSubUpdate.date = tradeSubUpdate.timestamp;
            //        tsm.Channel = tradeSubUpdate.channel;
            //        tsm.DealId = tradeSubUpdate.dealId;
            //        tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
            //        tsm.DealReference = tradeSubUpdate.dealReference;
            //        tsm.DealStatus = tradeSubUpdate.dealStatus.ToString();
            //        tsm.Direction = tradeSubUpdate.direction.ToString();
            //        tsm.ItemName = itemName;
            //        tsm.Epic = tradeSubUpdate.epic;
            //        tsm.Expiry = tradeSubUpdate.expiry;
            //        tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
            //        tsm.Level = tradeSubUpdate.level;
            //        tsm.Limitlevel = tradeSubUpdate.limitLevel;
            //        tsm.Size = tradeSubUpdate.size;
            //        tsm.Status = tradeSubUpdate.status.ToString();
            //        tsm.StopLevel = tradeSubUpdate.stopLevel;
            //        tsm.Reason = tradeSubUpdate.reason;
            //        tsm.date = tradeSubUpdate.timestamp;
            //        tsm.StopDistance = tradeSubUpdate.stopDistance;
            //        tsm.TradeType = "WOU";
            //        if(tsm.Reason == null) { tsm.Reason = ""; }
            //        tradeSubUpdate.reasonDescription = _thisApp.TradeErrors[tsm.Reason];

            //        //////////////////////////////
            //        /// Working order updates   //
            //        //////////////////////////////
            //        if (tsm.Epic == _thisApp.epicName)
            //        {
            //            clsCommonFunctions.AddStatusMessage($"Order update {tsm.Status} : {tsm.DealStatus} - {inputData}", "INFO");

            //            if (tsm.Status == "OPEN" )
            //            {
            //                if (tsm.DealStatus == "ACCEPTED")
            //                {
            //                    requestedTrade reqTrade = new requestedTrade();
            //                    reqTrade = _thisApp.requestedTrades.Where(i => i.dealReference == tsm.DealReference).FirstOrDefault();

            //                    if (reqTrade != null)
            //                    {
            //                        reqTrade.dealStatus = tsm.DealStatus;

            //                        orderItem thisOrder = new orderItem();
            //                        thisOrder.dealId = tsm.DealId;
            //                        thisOrder.direction = tsm.Direction;
            //                        thisOrder.createdDate = DateTime.Now; ;
            //                        thisOrder.createdDateUTC = DateTime.UtcNow;
            //                        thisOrder.status = tsm.Status;
            //                        thisOrder.epic = tsm.Epic;
            //                        thisOrder.orderLevel = Convert.ToDecimal(tsm.Level);
            //                        thisOrder.orderSize = Convert.ToDouble(tsm.Size);
            //                        thisOrder.accountId = _thisApp.igAccountId;
            //                        thisOrder.channel = tsm.Channel;
            //                        thisOrder.stopDistance = Convert.ToDecimal(tsm.StopDistance);
            //                        thisOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
            //                        thisOrder.dealReference = tsm.DealReference;
            //                        thisOrder.dealStatus = tsm.DealStatus;
            //                        thisOrder.associatedDealId = _thisApp.model.thisModel.currentTrade.tbDealId;
            //                        _thisApp.model.thisModel.currentTrade.suppOrderId = tsm.DealId;
            //                        _thisApp.model.thisModel.currentTrade.attachedOrder = thisOrder;

            //                        if (tsm.Direction == "BUY")
            //                        {
            //                            thisOrder.stopLevel = thisOrder.orderLevel - thisOrder.stopDistance;
            //                        }
            //                        else
            //                        {
            //                            thisOrder.stopLevel = thisOrder.orderLevel + thisOrder.stopDistance;
            //                        }

            //                        // reset any retry logic if needs be as this order has been successful
            //                        _thisApp.retryOrder = false;
            //                        _thisApp.retryOrderCount = 0;

            //                        //Save current trade with this suppOrderId
            //                        await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
            //                        clsCommonFunctions.AddStatusMessage($"Order {thisOrder.dealId} saved to current trade - {_thisApp.model.thisModel.currentTrade.tbDealId}", "INFO");
            //                    }
            //                }
            //                await tradeSubUpdate.Add(_thisApp.the_app_db);
            //            }
            //            if (tsm.Status == "DELETED" )
            //            {
            //                if (tsm.DealStatus == "ACCEPTED")
            //                {
            //                    string orderDealId = tsm.DealId;

            //                    if (_thisApp.model.thisModel.currentTrade != null)
            //                    {
            //                        if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId == orderDealId)
            //                        {
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.deletedDate = DateTime.Now;
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.status = tsm.Status;
            //                            await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
            //                        }
            //                    }
            //                    else
            //                    {
            //                        //Get trade by the attachedOrder dealid
            //                        tradeItem thisTrade = await _thisApp.GetTradeFromDBByOrder(orderDealId);
            //                        thisTrade.attachedOrder.deletedDate = DateTime.Now;
            //                        thisTrade.attachedOrder.status = tsm.Status;
            //                        await thisTrade.SaveDocument(_thisApp.trade_container);
            //                    }
            //                }
            //                await tradeSubUpdate.Add(_thisApp.the_app_db);
            //            }
            //            if (tsm.Status == "UPDATED")
            //            {
            //                if (tsm.DealStatus == "ACCEPTED")
            //                {
            //                    string orderDealId = tsm.DealId;

            //                    if (_thisApp.model.thisModel.currentTrade != null)
            //                    {
            //                        if (_thisApp.model.thisModel.currentTrade.attachedOrder.dealId == orderDealId)
            //                        {
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.stopDistance = Convert.ToDecimal(tsm.StopDistance);
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.orderLevel = Convert.ToDecimal(tsm.Level);
            //                            _thisApp.model.thisModel.currentTrade.attachedOrder.status = tsm.Status;
            //                            await _thisApp.model.thisModel.currentTrade.SaveDocument(_thisApp.trade_container);
            //                        }
            //                    }
            //                    else
            //                    {
            //                        //Get trade by the attachedOrder dealid
            //                        tradeItem thisTrade = await _thisApp.GetTradeFromDBByOrder(orderDealId);
            //                        thisTrade.attachedOrder.stopDistance =Convert.ToDecimal(tsm.StopDistance);
            //                        thisTrade.attachedOrder.stopLevel = Convert.ToDecimal(tsm.StopLevel);
            //                        thisTrade.attachedOrder.orderLevel = Convert.ToDecimal(tsm.Level);
            //                        await thisTrade.SaveDocument(_thisApp.trade_container);
            //                    }

            //                }
            //                await tradeSubUpdate.Add(_thisApp.the_app_db);

            //            }
            //        }

 
            //    }

            //}
            //catch (Exception ex)
            //{
            //    var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
            //    log.Log_Message = ex.ToString();
            //    log.Log_Type = "Error";
            //    log.Log_App = "UpdateTsWOU";
            //    log.Epic = "";
            //    await log.Save();
            //}
            return tsm;
        }
        private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsCONFIRM(int itemPos, string itemName, ItemUpdate update, string inputData, TradeSubscriptionType updateType)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();

            //try
            //{
            //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
            TradeSubUpdate tradeSubUpdate = (TradeSubUpdate)JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
            tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
            tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
            tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
            tradeSubUpdate.updateType = "OPU";

            MainApp _thisApp = null;

            foreach (MainApp wrk in _igContainer.workerList)
            {
                //List<string> parm = new List<string>();
                //parm = wrk._thread.Name.Split("|").ToList();
                if (wrk.epicName == tradeSubUpdate.epic)
                {
                    updateMessage msg = new updateMessage();
                    msg.itemName = update.ItemName;
                    msg.updateData = inputData;
                    msg.updateType = "CONFIRM";
                    runRet taskRet = await wrk.iGUpdate(msg);
                }
            }
            //try
            //{
            //    var tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);

            //    if (tradeSubUpdate.epic == _thisApp.epicName )
            //    {
            //        tsm.Channel = tradeSubUpdate.channel;
            //        tsm.DealId = tradeSubUpdate.dealId;
            //        tsm.AffectedDealId = tradeSubUpdate.affectedDealId;
            //        tsm.DealReference = tradeSubUpdate.dealReference;
            //        tsm.DealStatus = tradeSubUpdate.dealStatus.ToString();
            //        tsm.Direction = tradeSubUpdate.direction.ToString();
            //        tsm.ItemName = itemName;
            //        tsm.Epic = tradeSubUpdate.epic;
            //        tsm.Expiry = tradeSubUpdate.expiry;
            //        tsm.GuaranteedStop = tradeSubUpdate.guaranteedStop;
            //        tsm.Level = tradeSubUpdate.level;
            //        tsm.Limitlevel = tradeSubUpdate.limitLevel;
            //        tsm.Size = tradeSubUpdate.size;
            //        tsm.Status = tradeSubUpdate.status.ToString();
            //        tsm.StopLevel = tradeSubUpdate.stopLevel;
            //        tsm.Reason = tradeSubUpdate.reason;
            //        tsm.date = tradeSubUpdate.date;
            //        tsm.StopDistance = tradeSubUpdate.stopDistance;
            //        tsm.TradeType = "CONFIRM";

            //        tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString();
            //        tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString();
            //        tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString();
            //        if (tsm.Reason != null)
            //        {
            //            if (tsm.Reason != "")
            //            {
            //                tradeSubUpdate.reasonDescription = _thisApp.TradeErrors[tsm.Reason];
            //            }
            //        }

            //        tradeSubUpdate.updateType = tsm.TradeType;

            //        if (tsm.Epic == _thisApp.epicName)
            //        {

            //            // Find this trade from the list of requested trades to tie in with the requested type (position or order)
            //            requestedTrade reqTrade = new requestedTrade();
            //            reqTrade = _thisApp.requestedTrades.Where(i => i.dealReference == tsm.DealReference).FirstOrDefault();

            //            if (reqTrade != null)
            //            {
            //                await tradeSubUpdate.Add(_thisApp.the_app_db);

            //                reqTrade.dealStatus = tsm.DealStatus;

            //                clsCommonFunctions.AddStatusMessage($"CONFIRM - deal reference = {reqTrade.dealReference}, deal type = {reqTrade.dealType}, deal status = {reqTrade.dealStatus}");


            //                if (tsm.Status == "OPEN" && tsm.Reason == "SUCCESS")
            //                {
            //                    // trade/order opened successfully
            //                    clsCommonFunctions.AddStatusMessage($"CONFIRM - successful", "INFO");
            //                }

            //                if (reqTrade.dealType == "ORDER" && reqTrade.dealStatus == "REJECTED")
            //                {

            //                    clsCommonFunctions.AddStatusMessage($"ORDER REJECTED -  {tsm.Reason} - {_thisApp.TradeErrors[tsm.Reason]} : retryCount = {_thisApp.retryOrderCount}, retryOrderLimit = {_thisApp.retryOrderLimit}");
            //                    // Order has been rejected, possibly because the market is moving too fast. Try again next time.
            //                    if (_thisApp.retryOrderCount < _thisApp.retryOrderLimit)
            //                    {
            //                        _thisApp.retryOrder = true;
            //                        _thisApp.retryOrderCount += 1;
            //                        clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry set for next run");

            //                    }
            //                    else
            //                    {
            //                        clsCommonFunctions.AddStatusMessage($"ORDER REJECTED. Retry limit hit. Just forget about it.");
            //                        _thisApp.retryOrder = false;
            //                        _thisApp.retryOrderCount = 0;
            //                    }
            //                }

            //                if (tsm.Status == null & tsm.Reason != "SUCCESS")
            //                {
            //                    // trade/order not successful (could be update or open or delete)
            //                    clsCommonFunctions.AddStatusMessage($"CONFIRM - failed - deal type = {reqTrade.dealType} - {tsm.Reason} - {_thisApp.TradeErrors[tsm.Reason]}", "INFO");

            //                    if (reqTrade.dealType == "POSITION")
            //                    {
            //                        clsCommonFunctions.AddStatusMessage($"CONFIRM - Resetting values due to {reqTrade.dealType} failure", "INFO");
            //                        _thisApp.model.sellShort = false;
            //                        _thisApp.model.sellLong = false;
            //                        _thisApp.model.buyShort = false;
            //                        _thisApp.model.shortOnMarket = false;
            //                        _thisApp.model.buyLong = false;
            //                        _thisApp.model.longOnmarket = false;
            //                        _thisApp.model.onMarket = false;
            //                    }

            //                }
            //            }

            //        }
            //    }

            //}
            //catch (Exception ex)
            //{
            //    var log = new TradingBrain.Models.Log(_thisApp.the_app_db);
            //    log.Log_Message = ex.ToString();
            //    log.Log_Type = "Error";
            //    log.Log_App = "UpdateTsCONFIRM";
            //    log.Epic = "";
            //    await log.Save();
            //}
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
                    //client.addListener(new ChartConnectionListener(this, ph));
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
                //if (_igContainer.epicName != "")
                //{
                //    chartName = "CHART:" + _igContainer.epicName + ":TICK";
                //}
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
                var log = new TradingBrain.Models.Log(_igContainer.the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "ChartSubscribe";
                log.Epic = "";
                log.Save();
            }
        }
        private async void TradeSubscribe(string accountId)
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
                //subscription.RequestedMaxFrequency = "unfiltered";
                //subscription.RequestedBufferSize = "50";
                subscription.addListener(new TradeSubscriptionListener(this, this.phase));
                client.subscribe(subscription);
            }
            catch (Exception e)
            {
                //demoForm.Invoke(statusChangeDelegate, new Object[] {
                //        StocklistConnectionListener.VOID, e.Message
                //    });
                var log = new TradingBrain.Models.Log(_igContainer.the_app_db);
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "TradeSubscribe";
                log.Epic = "";
                await log.Save();
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

    public class IGContainer
    {
        public IgRestApiClient? igRestApiClient { get; set; }
        public ConversationContext context { get; set; }
        public ObservableCollection<IgPublicApiData.AccountModel>? Accounts { get; set; }
        public string CurrentAccountId { get; set; }
        public string igAccountId { get; set; }
        public List<clsEpicList> EpicList { get; set; }
        public static bool LoggedIn { get; set; }
        public TBStreamingClient tbClient { get; set; }
        private bool isDirty = false;

        private string pushServerUrl;
        public string forceT;
        private string forceTransport = "no";
        public Database? the_db { get; set; }
        public Database? the_app_db { get; set; }
        public List<MainApp> workerList { get; set; }
        public IGContainer()
        {
            igRestApiClient = null;
            CurrentAccountId = "";
            igAccountId = "";
            Accounts = null;
            EpicList = new List<clsEpicList>();
            LoggedIn = false;
            the_db = null;
            the_app_db = null;
            tbClient = null;
            workerList = new List<MainApp>();
        }
        public void StartLightstreamer()
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
    }
}
