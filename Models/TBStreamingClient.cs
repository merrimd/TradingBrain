#pragma warning disable S125
using com.lightstreamer.client;
using dto.endpoint.accountswitch;
using dto.endpoint.auth.session.v2;
using IGModels;
using IGModels.RSI_Models;
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
using NLog.LayoutRenderers;
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
using System.Security.Cryptography.Xml;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TradingBrain.Models;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using static TradingBrain.Models.CommonFunctions;
using Container = Microsoft.Azure.Cosmos.Container;
using LogLevel = NLog.LogLevel;
namespace TradingBrain.Models
{

    public class TBStreamingClient
    {
        public IgRestApiClient? IgRestApiClient { get; set; }
        public ConversationContext? Context { get; set; }
        public ObservableCollection<IgPublicApiData.AccountModel>? Accounts { get; set; }
        public string CurrentAccountId { get; set; } = "";
        public string IgAccountId { get; set; } = "";
        public List<EpicList> EpicList { get; set; } = new List<EpicList>();
        public List<LOepic> PriceEpicList { get; set; } = new List<LOepic>();
        public static bool LoggedIn { get; set; }
        public TBStreamingClient? TbClient { get; set; }
        public bool IsDirty { get; set; } = false;
        //public string PushServerUrl { get; set; } = "";
        //public string ForceT { get; set; } = "";
        public Database? TheDb { get; set; }
        public Database? TheAppDb { get; set; }
        public List<MainApp> WorkerList { get; set; } = new List<MainApp>();
        public IgApiCreds Creds { get; set; } = new IgApiCreds();
        public Delegates.LightstreamerUpdateDelegate? updateDelegate { get; set; }
        public Delegates.LightstreamerStatusChangedDelegate? statusChangeDelegate { get; set; }
        public LightstreamerClient? client { get; set; }
        private Subscription? subscription { get; set; }
        public bool connectionEstablished { get; set; }
        public bool FirstConfirmUpdate { get; set; }
        public IGContainer? _igContainer { get; set; }
        public int currentSecond { get; set; } = 0;


        public TBStreamingClient(

                IGContainer igContainer,
                Delegates.LightstreamerUpdateDelegate lsUpdateDelegate,
                Delegates.LightstreamerStatusChangedDelegate lsStatusChangeDelegate)
        {
            try
            {
                if (igContainer == null)
                {
                    throw new ArgumentNullException(nameof(igContainer));
                }
                _igContainer = igContainer;
                Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();
                updateDelegate = lsUpdateDelegate;
                statusChangeDelegate = lsStatusChangeDelegate;
                FirstConfirmUpdate = true;

                SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();

                string env = Environment.GetEnvironmentVariable("environment") ?? "";
                if (env == "")
                {
                    object v = ConfigurationManager.GetSection("appSettings");
                    NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
                    env = igWebApiConnectionConfig["environment"] ?? "DEMO";
                }
                _igContainer.igRestApiClient = new IgRestApiClient(env, smartDispatcher);

            }
            catch (Exception)
            {
                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "TBStreamingClient";
                //log.Save();
            }

        }

        private const string LOGGED_IN_MESSAGE = "Logged in, current account: ";

        //public static void AddStatusMessage(string message)
        //{
        //    clsCommonFunctions.AddStatusMessage(message);
        //}

        public async Task ConnectToRest()
        {
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG credentials are null in ConnectToRest");
                }
                if (_igContainer.igRestApiClient == null)
                {
                    throw new InvalidOperationException("IG REST API Client is null in ConnectToRest");
                }
                if (_igContainer.creds == null)
                {
                    throw new InvalidOperationException("IG creds are null in ConnectToRest");
                }
                //string env = _igContainer.creds.igEnvironment;
                string userName = _igContainer.creds.igUsername;
                string password = _igContainer.creds.igPassword;
                string apiKey = _igContainer.creds.igApiKey;
                string accountId = _igContainer.creds.igAccountId;

                var ar = new AuthenticationRequest { identifier = userName, password = password };
                _igContainer.Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();

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

                        CommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName, "INFO");
                    }

                    LoggedIn = true;

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _igContainer.igRestApiClient.accountSwitch(asr);

                        if (response2 != null)
                        {
                            CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + accountId, "INFO");
                        }
                        else
                        {
                            CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + response.Response.currentAccountId, "INFO");
                        }
                    }
                    else
                    {
                        CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + response.Response.currentAccountId, "INFO");
                    }

                    _igContainer.context = _igContainer.igRestApiClient.GetConversationContext();

                }
                else
                {
                    CommonFunctions.AddStatusMessage("Failed to login. HttpResponse StatusCode = " + response.StatusCode, "ERROR");
                }
            }
            catch (Exception e)
            {
                CommonFunctions.AddStatusMessage("ConnectToRest failed - " + e.ToString(), "ERROR");

            }
        }
        public async Task LogIn()
        {

            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG credentials are null in LogIn");
                }
                if (_igContainer.igRestApiClient == null)
                {
                    throw new InvalidOperationException("IG REST API Client is null in LogIn");
                }
                if (_igContainer.creds == null)
                {
                    throw new InvalidOperationException("IG creds are null in LogIn");
                }
                //string env = _igContainer.creds.igEnvironment;
                string userName = _igContainer.creds.igUsername;
                string password = _igContainer.creds.igPassword;
                string apiKey = _igContainer.creds.igApiKey;
                string accountId = _igContainer.creds.igAccountId;

                var ar = new AuthenticationRequest { identifier = userName, password = password };
                _igContainer.Accounts = new ObservableCollection<IgPublicApiData.AccountModel>();


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

                        CommonFunctions.AddStatusMessage("Account:" + igAccount.ClientId + " " + account.accountName, "INFO");
                    }
                    LoggedIn = true;

                    if (response.Response.currentAccountId != accountId)
                    {
                        // Need to switch accounts
                        AccountSwitchRequest asr = new AccountSwitchRequest { accountId = accountId };

                        AccountSwitchResponse response2 = await _igContainer.igRestApiClient.accountSwitch(asr);

                        if (response2 != null)
                        {
                            CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + accountId, "INFO");
                            _igContainer.igAccountId = accountId;
                        }
                        else
                        {
                            CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + response.Response.currentAccountId, "INFO");
                            _igContainer.igAccountId = response.Response.currentAccountId;
                        }
                    }
                    else
                    {
                        CommonFunctions.AddStatusMessage(LOGGED_IN_MESSAGE + response.Response.currentAccountId, "INFO");
                        _igContainer.igAccountId = response.Response.currentAccountId;
                    }

                    _igContainer.context = _igContainer.igRestApiClient.GetConversationContext();

                    CommonFunctions.AddStatusMessage("establishing datastream connection", "INFO");

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
                            CommonFunctions.AddStatusMessage(ex.Message, "ERROR");
                        }
                    }


                    // Run a timer for every 10 mins to keep the connection alive.

                    // set a timeout to run this again in 30 mins. this will keep the session active.
                    CommonFunctions.AddStatusMessage("Setting a timer so we can re run the AccountDetails after 10 mins to stop it expiring.", "INFO");
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
                    CommonFunctions.AddStatusMessage("Failed to login. HttpResponse StatusCode = " +
                                        response.StatusCode, "ERROR");
                    //Log log = new Log(_thisApp.the_app_db);
                    //log.Log_Message = "Failed to login. HttpResponse StatusCode = " + response.StatusCode;
                    //log.Log_Type = "Error";
                    //log.Log_App = "Login";
                    //log.Save();
                }
            }
            catch (Exception)
            {

                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "Login";
                //log.Save();

            }




        }
        public void OnTimedEvent(object? source, ElapsedEventArgs e)
        {
            CommonFunctions.AddStatusMessage("Calling the AccountDetails API to ensure token doesn't expire", "INFO");
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("IG credentials are null in OnTimedEvent");
                }
                if (_igContainer.igRestApiClient == null)
                {
                    throw new InvalidOperationException("IG REST API Client is null in OnTimedEvent");
                }
                IgResponse<dto.endpoint.accountbalance.AccountDetailsResponse> ret = _igContainer.igRestApiClient.accountBalance().Result;
                if (ret != null)
                {
                    CommonFunctions.AddStatusMessage("AccountDetails response = " + ret.StatusCode.ToString(), "INFO");

                    if (ret.StatusCode.ToString() == "Forbidden")
                    {
                        // re connect to API

                    }
                }
            }
            catch (Exception)
            {
                // empty
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
                Disconnect();
            }
        }
        public class SmartDispatcher : PropertyEventDispatcher
        {
            private static PropertyEventDispatcher instance = new SmartDispatcher();

            //private static bool _designer = false;



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
        private async void Execute(int ph)
        {
            try
            {
                if (this._igContainer == null)
                {
                    throw new InvalidOperationException("IG credentials are null in Execute");
                }

                if (ph != this.phase)
                {
                    return;
                }
                ph = Interlocked.Increment(ref this.phase);
                await this.LogIn();
                this.Connect(ph);
                if (this._igContainer.creds.primary)
                {
                    //ony subscribe to charts on primary connection
                    this.ChartSubscribe();
                }
                await this.TradeSubscribe(this._igContainer.igAccountId);
                //this._igContainer.igAccountId = CurrentAccountId;
                // this.subscribeChart()
            }
            catch (Exception)
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
            try
            {

                CommonFunctions.AddStatusMessage("Status changed to " + status + " (" + cStatus + ")", "INFO");
                if (cStatus == 0 && Interlocked.CompareExchange(ref this.reset, 0, 1) == 1)
                {
                    int phs = Interlocked.Increment(ref this.phase);
                    Thread t = new Thread(new ThreadStart(delegate ()
                    {
                        Execute(phs);
                    }));
                    t.Start();

                }
            }
            catch (Exception)
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

            var epic = update.ItemName.Replace("L1:", "").Replace("CHART:", "").Replace(":1MINUTE", "").Replace(":TICK", "").Replace(":SECOND", "");
            try
            {
                if (_igContainer == null)
                {
                    throw new InvalidOperationException("_igContainer is null in ChartUpdateReceived");
                }
                var wlmUpdate = update;

                //clsCommonFunctions.AddStatusMessage($"Chart update received for {epic} - UTM:{(DateTime)EpocStringToNullableDateTime(wlmUpdate.getValue("UTM"))} BID:{wlmUpdate.getValue("BID")} OFR:{wlmUpdate.getValue("OFR")}", "INFO");

                if (wlmUpdate.getValue("BID") != "" && wlmUpdate.getValue("OFR") != "" && wlmUpdate.getValue("BID") != "0" && wlmUpdate.getValue("OFR") != "0" && wlmUpdate.getValue("BID") != null && wlmUpdate.getValue("OFR") != null)
                {

                    DateTime thisUTM = DateTime.MinValue;
                    if (wlmUpdate.getValue("UTM") != null)
                    {
                        DateTime? utmValue = EpocStringToNullableDateTime(wlmUpdate.getValue("UTM"));
                        if (utmValue.HasValue)
                        {
                            thisUTM = utmValue.Value;
                        }
                        else
                        {
                            thisUTM = DateTime.MinValue; // or handle as appropriate for your logic
                        }
                    }

                    LOepic? thisEpic = _igContainer.PriceEpicList.FirstOrDefault(x => x.name == epic);
                    if (thisEpic != null)
                    {
                        tick thisTick = new tick();
                        thisTick.bid = Convert.ToDecimal(wlmUpdate.getValue("BID"));
                        thisTick.offer = Convert.ToDecimal(wlmUpdate.getValue("OFR"));
                        thisTick.UTM = thisUTM;

                        thisEpic.ticks.Add(thisTick);
                    }


                }











                //foreach (clsEpicList item in this.EpicList)
                //{
                //    if (item.Epic == epic)
                //    {
                //        item.counter++;
                //    }
                //}
                //ChartUpdateMini currentTick = new ChartUpdateMini();

                //if (wlmUpdate.getValue("BID") != "" && wlmUpdate.getValue("OFR") != "")
                //{
                //    //ChartUpdateMini objUpdate = new ChartUpdateMini();
                //    if (wlmUpdate.getValue("BID") != null && wlmUpdate.getValue("OFR") != null)
                //    {
                //       currentTick.Epic = epic;
                //        currentTick.Bid = Convert.ToDecimal(wlmUpdate.getValue("BID"));
                //        currentTick.Offer = Convert.ToDecimal(wlmUpdate.getValue("OFR"));
                //        //currentTick.LTP = Convert.ToDecimal(wlmUpdate.getValue("LTP"));
                //        //currentTick.LTV = Convert.ToDecimal(wlmUpdate.getValue("LTV"));
                //        //currentTick.TTV = Convert.ToDecimal(wlmUpdate.getValue("TTV"));
                //        if (wlmUpdate.getValue("UTM") != null)
                //        {
                //            currentTick.UTM = EpocStringToNullableDateTime(wlmUpdate.getValue("UTM"));
                //        }
                //        //currentTick.DAY_OPEN_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_OPEN_MID"));
                //        //currentTick.DAY_NET_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_NET_CHG_MID"));
                //        //currentTick.DAY_PERC_CHG_MID = Convert.ToDecimal(wlmUpdate.getValue("DAY_PERC_CHG_MID"));
                //        //currentTick.DAY_HIGH = Convert.ToDecimal(wlmUpdate.getValue("DAY_HIGH"));
                //        //currentTick.DAY_LOW = Convert.ToDecimal(wlmUpdate.getValue("DAY_LOW"));
                //        int thisSecond = currentTick.UTM.Value.Second;
                //        if (thisSecond != currentSecond)
                //        {
                //           // clsCommonFunctions.SendBroadcast("PriceChange", JsonConvert.SerializeObject(currentTick), _igContainer.the_app_db);
                //            //Console.WriteLine("PriceChange: " + JsonConvert.SerializeObject(currentTick));
                //            currentSecond = thisSecond;
                //        }


                //        //Console.WriteLine("PriceChange: " + JsonConvert.SerializeObject(currentTick));
                //    }



                //}


            }

            catch (Exception)
            {
                //Log log = new Log(_thisApp.the_app_db);
                //log.Log_Message = ex.ToString();
                //log.Log_Type = "Error";
                //log.Log_App = "OnTickUpdate";
                //log.Save();
            }

        }

        public async Task TradeUpdateReceived(int ph, int itemPos, ItemUpdate update)
        {

            // Deal with Trade updates here
            CommonFunctions.AddStatusMessage($"Trade update received: ph={ph}, this.phase={this.phase}, FirstConfirmUpdate={this.FirstConfirmUpdate}", "INFO");

            if (ph != this.phase)
            {
                CommonFunctions.AddStatusMessage("Trade not updated as ph <> this.phase", "INFO");
                return;
            }
            try
            {
                if (!this.FirstConfirmUpdate)
                {
                    //var sb = new StringBuilder();
                    //sb.AppendLine("Trade Subscription Update");
                    //clsCommonFunctions.AddStatusMessage("Trade Subscription Update", "INFO");
                    try
                    {

                        var confirms = update.getValue("CONFIRMS");
                        var opu = update.getValue("OPU");
                        var wou = update.getValue("WOU");

                        if (!(String.IsNullOrEmpty(opu)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - OPU" + opu);
                            _ = Task.Run(() => UpdateTsOpu(update, opu));
                        }
                        if (!(String.IsNullOrEmpty(wou)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - WOU" + wou);
                            //await UpdateTsWou(itemPos, update.ItemName, update, wou, TradeSubscriptionType.Wou);
                        }
                        if (!(String.IsNullOrEmpty(confirms)))
                        {
                            //clsCommonFunctions.AddStatusMessage("Trade update - CONFIRMS" + confirms);
                            await UpdateTsConfirm(update, confirms);
                        }

                    }
                    catch (Exception)
                    {
                        // empty
                    }
                }
                else { this.FirstConfirmUpdate = false; }
            }
            catch (Exception)
            {
                // empty
            }


            //clsCommonFunctions.AddStatusMessage("Trade - " + itemPos, "INFO");

        }

        private IgPublicApiData.TradeSubscriptionModel UpdateTsOpu(ItemUpdate update, string inputData)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();
            if (_igContainer == null)
            {
                throw new InvalidOperationException("_igContainer is null in UpdateTsOpu");
            }
            //if (_igContainer.the_app_db == null)
            //{
            //     throw new InvalidOperationException("_igContainer.the_app_db is null in UpdateTsOpu");
            //}
            //try
            //{
            //    //var tradeSubUpdate = JsonConvert.DeserializeObject<LsTradeSubscriptionData>(inputData);
            TradeSubUpdate? tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
            if (tradeSubUpdate != null)
            {
                tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString() ?? "";
                tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString() ?? "";
                tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString() ?? "";
                tradeSubUpdate.updateType = "OPU";

                //MainApp _thisApp = null;

                foreach (MainApp wrk in _igContainer.workerList)
                {
                    //List<string> parm = new List<string>();
                    //parm = wrk._thread.Name.Split("|").ToList();
                    if (wrk.epicName == tradeSubUpdate.epic)
                    {
                        UpdateMessage msg = new UpdateMessage();
                        msg.itemName = update.ItemName;
                        msg.updateData = inputData;
                        msg.updateType = "UPDATE";
                        _ = wrk.iGUpdate(msg);

                        CommonFunctions.SendBroadcast("UpdateOPU", inputData);
                        //Console.WriteLine("UpdateOPU: " + inputData);
                    }
                }
            }

            return tsm;
        }

        private async Task<IgPublicApiData.TradeSubscriptionModel> UpdateTsConfirm(ItemUpdate update, string inputData)
        {

            var tsm = new IgPublicApiData.TradeSubscriptionModel();
            if (_igContainer == null)
            {
                throw new InvalidOperationException("IG Container is null in UpdateTsConfirm");
            }
            if (_igContainer.the_app_db == null)
            {
                throw new InvalidOperationException("IG Container the_app_db is null in UpdateTsConfirm");
            }
            try
            {


                TradeSubUpdate? tradeSubUpdate = JsonConvert.DeserializeObject<TradeSubUpdate>(inputData);
                if (tradeSubUpdate != null)
                {
                    tradeSubUpdate.statusVal = tradeSubUpdate.status.ToString() ?? "";
                    tradeSubUpdate.directionVal = tradeSubUpdate.direction.ToString() ?? "";
                    tradeSubUpdate.dealStatusVal = tradeSubUpdate.dealStatus.ToString() ?? "";
                    tradeSubUpdate.updateType = "OPU";

                    //MainApp _thisApp = null;

                    foreach (MainApp wrk in _igContainer.workerList)
                    {
                        //List<string> parm = new List<string>();
                        //parm = wrk._thread.Name.Split("|").ToList();
                        if (wrk.epicName == tradeSubUpdate.epic)
                        {
                            UpdateMessage msg = new UpdateMessage();
                            msg.itemName = update.ItemName;
                            msg.updateData = inputData;
                            msg.updateType = "CONFIRM";
                            _ = wrk.iGUpdate(msg);
                            CommonFunctions.SendBroadcast("UpdateConfirm", inputData);
                            //Console.WriteLine("updateConfirm: " + inputData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var log = new TradingBrain.Models.Log(_igContainer.the_app_db);
                log.Log_Message = ex.ToString();
                log.Log_Type = "Error";
                log.Log_App = "UpdateTsConfirm";
                log.Epic = "";
                await log.Save();
            }
            return tsm;
        }

        protected static decimal? StringToNullableDecimal(string value)
        {
            decimal number;
            return decimal.TryParse(value, out number) ? number : (decimal?)null;
        }

        protected static int? StringToNullableInt(string value)
        {
            int number;
            return int.TryParse(value, out number) ? number : (int?)null;
        }

        protected static DateTime? EpocStringToNullableDateTime(string value)
        {
            ulong epoc;
            if (!ulong.TryParse(value, out epoc))
            {
                return null;
            }
            return DateTime.UnixEpoch.AddMilliseconds(epoc);
        }
        private void Connect(int ph)
        {
            bool connected = false;
            //this method will not exit until the openConnection returns without throwing an exception
            while (!connected)
            {
                try
                {
                    if (client == null)
                    {
                        throw new InvalidOperationException("Lightstreamer client is null in Connect");
                    }
                    if (ph != this.phase)
                        return;
                    ph = Interlocked.Increment(ref this.phase);
                    client.addListener(new ChartConnectionListener(this, ph));
                    client.addListener(new TradeConnectionListener(this, ph));
                    client.connect();
                    connected = true;
                }
                catch (Exception)
                {
                    // empty
                }

                if (!connected)
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void Disconnect()
        {
            //demoForm.Invoke(statusChangeDelegate, new Object[] {
            //        StocklistConnectionListener.VOID,
            //        "Disconnecting to Lightstreamer Server @ " + client.connectionDetails.ServerAddress
            //    });
            try
            {
                if (client == null)
                {
                    throw new InvalidOperationException("Lightstreamer client is null in Disconnect");
                }
                client.disconnect();
            }
            catch (Exception)
            {
                //empty
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
            if (_igContainer == null)
            {
                throw new InvalidOperationException("IG Container is null in ChartSubscribe");
            }
            try
            {
                //string chartName = "CHART:IX.D.NASDAQ.CASH.IP:TICK";
                if (client == null)
                {
                    throw new InvalidOperationException("Lightstreamer client is null in ChartSubscribe");
                }
                List<string> epics = new List<string>();

                foreach (EpicList epic in _igContainer.EpicList)
                {
                    string tmpEpic = "";
                    if (epic.Epic.Contains("|"))
                    {
                        List<string> tmpLst = epic.Epic.Split("|").ToList();
                        tmpEpic = tmpLst[0];
                    }
                    else
                    {
                        tmpEpic = epic.Epic;
                    }
                    epics.Add("CHART:" + tmpEpic + ":TICK");
                    _igContainer.PriceEpicList.Add(new LOepic(tmpEpic));
                }
                //if (_igContainer.ep != "")
                //{
                //    chartName = "CHART:" + _igContainer.epicName + ":TICK";
                //}
                //subscription = new Subscription("DISTINCT", new string[1] { chartName }, new string[11] { "BID", "OFR", "LTP", "LTV", "TTV", "UTM", "DAY_OPEN_MID", "DAY_NET_CHG_MID", "DAY_PERC_CHG_MID", "DAY_HIGH", "DAY_LOW" });
                subscription = new Subscription("DISTINCT", epics.ToArray(), new string[3] { "UTM", "BID", "OFR" });


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
                _ = log.Save();
            }
        }
        private async Task TradeSubscribe(string accountId)
        {
            //this method will try just one subscription.
            //we know that when this method executes we should be already connected
            //If we're not or we disconnect while subscribing we don't have to do anything here as an
            //event will be (or was) sent to the ConnectionListener that will handle the case.
            //If we're connected but the subscription fails we can't do anything as the same subscription 
            //would fail again and again (btw this should never happen)

            try
            {
                if (client == null)
                {
                    throw new InvalidOperationException("Lightstreamer client is null in TradeSubscribe");
                }
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
                if (_igContainer == null)
                {
                    return;
                }
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
            if (client != null)
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
        }

        internal void MaxFrequency(int value)
        {
            if (subscription == null)
            {
                return;
            }
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
        public ConversationContext? context { get; set; }
        public ObservableCollection<IgPublicApiData.AccountModel>? Accounts { get; set; }
        public string CurrentAccountId { get; set; }
        public string igAccountId { get; set; }
        public List<EpicList> EpicList { get; set; }
        public List<LOepic> PriceEpicList { get; set; }
        public static bool LoggedIn { get; set; }
        public TBStreamingClient? tbClient { get; set; }
        private bool isDirty = false;
        //public string forceT { get; set; }  

        public Database? the_db { get; set; }
        public Database? the_app_db { get; set; }
        public List<MainApp> workerList { get; set; }
        public IgApiCreds creds { get; set; }
        public IGContainer()
        {
            igRestApiClient = null;
            CurrentAccountId = "";
            igAccountId = "";
            Accounts = null;
            EpicList = new List<EpicList>();
            PriceEpicList = new List<LOepic>();
            LoggedIn = false;
            the_db = null;
            the_app_db = null;
            tbClient = null;
            workerList = new List<MainApp>();
            creds = new IgApiCreds();
            //forceT = "";
            //pushServerUrl = "";
        }
        public IGContainer(IgApiCreds _creds)
        {
            igRestApiClient = null;
            CurrentAccountId = _creds.igAccountId;
            creds = _creds;
            igAccountId = "";
            Accounts = null;
            EpicList = new List<EpicList>();
            PriceEpicList = new List<LOepic>();
            LoggedIn = false;
            the_db = null;
            the_app_db = null;
            tbClient = null;
            workerList = new List<MainApp>();
            //forceT = "";
            //pushServerUrl = "";
        }
        public void StartLightstreamer()
        {

            tbClient = new TBStreamingClient(
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
            //var a = 1;


        }
    }

    public class LOepic
    {
        public string name { get; set; }
        public ClsChartMinUpdate lastUpdate { get; set; }
        public DateTime lastUTM { get; set; }
        public List<double> closePrices { get; set; }
        public tbPrice latestCandle { get; set; }
        public List<tick> ticks { get; set; }
        public LOepic()
        {
            this.name = "";
            this.lastUpdate = new ClsChartMinUpdate();
            this.lastUTM = DateTime.MinValue;
            this.closePrices = new List<double>();
            this.latestCandle = new tbPrice();
            this.ticks = new List<tick>();
        }

        public LOepic(string _name)
        {
            this.name = _name;
            this.lastUpdate = new ClsChartMinUpdate();
            this.lastUTM = DateTime.MinValue;
            this.closePrices = new List<double>();
            this.ticks = new List<tick>();
            this.latestCandle = new tbPrice();
        }
    }
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    public class tick
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    {
        public decimal bid { get; set; }
        public decimal offer { get; set; }
        public DateTime UTM { get; set; }
        public tick()
        {
            bid = 0;
            offer = 0;
            UTM = DateTime.MinValue;

        }
        public tick(decimal _bid, decimal _offer, DateTime _UTM)
        {
            bid = _bid;
            offer = _offer;
            UTM = _UTM;
        }
    }
    public class ClsChartMinUpdate
    {
        public string Epic { get; set; }
        public Decimal Bid_Open { get; set; }
        public Decimal Bid_Close { get; set; }
        public Decimal Bid_High { get; set; }
        public Decimal Bid_Low { get; set; }
        public Decimal Offer_Open { get; set; }
        public Decimal Offer_Close { get; set; }
        public Decimal Offer_High { get; set; }
        public Decimal Offer_Low { get; set; }
        public Decimal LTV { get; set; }
        public DateTime UTM { get; set; }
        public DateTime snapshotTimeUTC { get; set; }
        public DateTime IGUpdateDateTime { get; set; }
        public int SyncStatus { get; set; }
        public string id { get; set; }




        public ClsChartMinUpdate()
        {
            this.Epic = "";
            this.Bid_Open = new Decimal();
            this.Bid_Close = new Decimal();
            this.Bid_High = new Decimal();
            this.Bid_Low = new Decimal();
            this.Offer_Open = new Decimal();
            this.Offer_Close = new Decimal();
            this.Offer_High = new Decimal();
            this.Offer_Low = new Decimal();
            this.LTV = 0;
            this.UTM = DateTime.MinValue;
            this.snapshotTimeUTC = DateTime.UtcNow;
            this.id = System.Guid.NewGuid().ToString();
            this.SyncStatus = 0;
            this.IGUpdateDateTime = DateTime.MinValue;
        }

        public async Task<bool> Add(Database the_db, Container container)
        {
            bool ret = true;
            if (string.IsNullOrEmpty(this.id))
            {
                this.id = System.Guid.NewGuid().ToString();
            }

            this.SyncStatus = 0;
            bool blnLoop = true;
            while (blnLoop)
            {
                try
                {

                    if (the_db != null)
                    {
                        // DatabaseResponse db = await the_db.ReadAsync();
                        //Container container = the_db.GetContainer("Candles");

                        bool exist = await DoesThisTickExist(the_db, container);
                        if (!exist)
                        {
                            this.id = System.Guid.NewGuid().ToString();
                            ItemResponse<ClsChartMinUpdate> SaveResponse = await container.CreateItemAsync<ClsChartMinUpdate>(this, new PartitionKey(this.id));
                        }
                        blnLoop = false;
                    }
                }
                catch (CosmosException de)
                {
                    Console.WriteLine(de.ToString());
                    var log = new Log();
                    log.Log_Message = de.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "clsChartUpdate/Add";
                    log.Epic = this.Epic;
                    await log.Save();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    var log = new Log();
                    log.Log_Message = e.ToString();
                    log.Log_Type = "Error";
                    log.Log_App = "clsChartUpdate/Add";
                    log.Epic = this.Epic;
                    await log.Save();
                }
            }


            return (ret);
        }
        public async Task<bool> DoesThisTickExist(Database the_db, Container container)
        {
            bool ret = false;

            try
            {
                if (the_db != null)
                {


                    var parameterizedQuery = new QueryDefinition(
                        query: "SELECT * FROM c WHERE c.Epic= @epicName and c.UTM = @UTM"
                    )
                    .WithParameter("@epicName", this.Epic)
                    .WithParameter("@UTM", this.UTM);

                    using FeedIterator<ChartUpdate> filteredFeed = container.GetItemQueryIterator<ChartUpdate>(
                        queryDefinition: parameterizedQuery
                    );

                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<ChartUpdate> response = await filteredFeed.ReadNextAsync();

                        // Iterate query results
                        foreach (ChartUpdate item in response)
                        {
                            if (item.Epic == this.Epic && this.Bid_Open == item.Bid)
                            {
                                ret = true;
                            }

                        }
                    }
                }

            }
            catch (CosmosException de)
            {
                Console.WriteLine(de.ToString());
                var log = new Log();
                log.Log_Message = de.ToString();
                log.Log_Type = "Error";
                log.Log_App = "clsChartUpdate/DoesThisTickExist";
                log.Epic = this.Epic;
                await log.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                var log = new Log();
                log.Log_Message = e.ToString();
                log.Log_Type = "Error";
                log.Log_App = "clsChartUpdate/DoesThisTickExist";
                log.Epic = this.Epic;
                await log.Save();
            }
            return ret;
        }

    }

    public class IgApiCreds
    {
        public string igEnvironment { get; set; }
        public string igApiKey { get; set; }
        public string igUsername { get; set; }
        public string igPassword { get; set; }
        public string igAccountId { get; set; }
        public bool primary { get; set; }
        public IgApiCreds()
        {
            igEnvironment = "";
            igApiKey = "";
            igUsername = "";
            igPassword = "";
            igAccountId = "";
            primary = false;
        }
        public IgApiCreds(string environment, string apiKey, string username, string password, string accountId, bool _primary)
        {
            igEnvironment = environment;
            igApiKey = apiKey;
            igUsername = username;
            igPassword = password;
            igAccountId = accountId;
            primary = _primary;
        }
    }
}