#pragma warning disable S125
#pragma warning disable S1764
#pragma warning disable S2589
#pragma warning disable S3776

using Azure.Storage.Blobs;
using com.lightstreamer.client;
using IGCandleCreator.Models;
using IGModels;
using IGWebApiClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit.Encodings;
using NLog;
using NLog.Extensions.AzureBlobStorage;
using NLog.Targets;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using Org.BouncyCastle.Tsp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
//using IGCandleCreator.Models;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Configuration;
using System.Drawing.Text;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TradingBrain.Models;
using static IGModels.ModellingModels.GetModelClass;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using static TradingBrain.Models.TBStreamingClient;
using Container = Microsoft.Azure.Cosmos.Container;


namespace TradingBrain.Models
{
    partial class ProgramV2
    {
        private const string DEFAULT_EPIC = "IX.D.NASDAQ.CASH.IP|SMA";
        public const string ENVIRONMENT = "environment";

        private static Database? the_db;
        private static Database? the_app_db;

        private static List<MainApp> workerList = [];
        private bool isDirty = false;

        //private string pushServerUrl;
        //public string forceT;
        //private string forceTransport = "no";

        // public IgRestApiClient? igRestApiClient;

        public delegate void StopDelegate();
        private static string region = "";
        public async static Task<bool> SetupDB()
        {
            bool ret = true;

            try
            {
                the_db = await IGModels.clsCommonFunctions.Get_Database();
                the_app_db = await IGModels.clsCommonFunctions.Get_App_Database();

                //if (the_db != null && the_app_db != null)
                //{
                //    EpicContainer = the_app_db.GetContainer("Epics");
                //    hourly_container = the_db.GetContainer("HourlyCandle");
                //    minute_container = the_db.GetContainer("MinuteCandle");
                //    TicksContainer = the_db.GetContainer("CandleTicks");
                //    //candlesContainer = the_db.GetContainer("Candles");
                //    trade_container = the_app_db.GetContainer("TradingBrainTrades");

                //}
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
            }
            return ret;

        }
        public async static Task<bool> SetupDB(string epic)
        {
            bool ret = true;

            try
            {
                the_db = await IGModels.clsCommonFunctions.Get_Database(epic);
                the_app_db = await IGModels.clsCommonFunctions.Get_App_Database(epic);

                //if (the_db != null && the_app_db != null)
                //{
                //    EpicContainer = the_app_db.GetContainer("Epics");
                //    hourly_container = the_db.GetContainer("HourlyCandle");

                //    if (epic == "IX.D.NIKKEI.DAILY.IP")
                //    {
                //        minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
                //        TicksContainer = the_db.GetContainer("CandleTicks_NIKKEI");
                //        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                //    }
                //    else
                //    {
                //        minute_container = the_db.GetContainer("MinuteCandle");
                //        TicksContainer = the_db.GetContainer("CandleTicks");
                //        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                //    }

                //    //candlesContainer = the_db.GetContainer("Candles");


                //}
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
            }
            return ret;

        }
        //public System.Timers.Timer ti = new System.Timers.Timer();
        static async Task Main(string[] args)
        {

            object v = ConfigurationManager.GetSection("appSettings");
            NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
            bool useEnvironment = false;

            //Get list of epics from config

            List<TbEpics> epcs = [];
            List<string> epicList = [];
            string settingEpics = "";

            region = Environment.GetEnvironmentVariable("Region") ?? "";

            if (region == "")
            {
                // not found in environment variables so get from config
                region = igWebApiConnectionConfig["region"] ?? "test";
                settingEpics = igWebApiConnectionConfig["epics"] ?? DEFAULT_EPIC;
            }
            else
            {
                useEnvironment = true;
                settingEpics = Environment.GetEnvironmentVariable("epics") ?? DEFAULT_EPIC;
            }


            if (settingEpics != DEFAULT_EPIC)
            {
                foreach (string tmp in settingEpics.Split(",").ToList())
                {
                    epcs.Add(new TbEpics(tmp));
                    epicList.Add(tmp);
                }
            }
            else
            {
                epcs.Add(new TbEpics(DEFAULT_EPIC));
                epicList.Add(DEFAULT_EPIC);
            }
            //string[] epics = { epcs[0].epic };

            string strategy = epcs[0].strategy;

            // Set up the logging //

            var config = new NLog.Config.LoggingConfiguration();

            string blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}container/${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}app-log.log";
            if (strategy == "GRID")
            {
                blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}container/${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}${date:format=HH}/app-log.log";
            }
            string blobConnectionString = Environment.GetEnvironmentVariable("BlobConnectionString") ?? "";
            if (blobConnectionString == "")
            {
                blobConnectionString = igWebApiConnectionConfig["BlobConnectionString"] ?? "";
            }

            BlobStorageTarget azureBlobTarget = new()
            {
                Name = "azureBlob",

                // Your Azure Blob connection string
                ConnectionString = blobConnectionString,

                // Container and blob path
                Container = "tb-logs-" + region,
                BlobName = blobName,

                // How log messages are rendered
                Layout = "${longdate} |${level:uppercase=true}|${scopeproperty:item=strategy}|${scopeproperty:item=epic}-${scopeproperty:item=resolution}|${message}|${exception:format=toString}",

                BatchSize = 20,
            };

            config.AddTarget(azureBlobTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, azureBlobTarget);






            //var filename = "DEBUG-" + DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + "-" + DateTime.UtcNow.Hour + ".txt";

            //var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "c:/tblogs/App.${mdlc:item=jobId}.${shortdate}.txt", MaxArchiveDays = 31, KeepFileOpen = false, Layout = "${longdate} [${mdlc:item=jobId}] |${level:uppercase=true}|${message}|${exception:format=toString}" };
            //config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            //logconsole.Layout = "${longdate} [${threadid}] |${level:uppercase=true}|${mdlc:item=jobId}|${message}|${exception:format=toString}";
            logconsole.Layout = "${longdate} |${level:uppercase=true}|${scopeproperty:item=strategy}|${scopeproperty:item=epic}-${scopeproperty:item=resolution}|${message}|${exception:format=toString}";

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);


            NLog.LogManager.Configuration = config;

            //MappedDiagnosticsLogicalContext.Set("jobId", "default");

            ScopeContext.PushProperty("app", "TRADINGBRAIN/");
            ScopeContext.PushProperty("epic", "DEFAULT/");
            ScopeContext.PushProperty("strategy", "");
            ScopeContext.PushProperty("resolution", "");
            //Connect to IG & Lightstreamer

            IGContainer? igContainer = null;
            IGContainer? igContainer2 = null;
            IgApiCreds? creds = new();
            IgApiCreds? creds2 = new();

            SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();

            if (useEnvironment)
            {
                CommonFunctions.AddStatusMessage(Environment.GetEnvironmentVariable(ENVIRONMENT) ?? "DEMO", "INFO");
                creds.igEnvironment = Environment.GetEnvironmentVariable(ENVIRONMENT) ?? "DEMO";
                creds.igUsername = Environment.GetEnvironmentVariable("username." + creds.igEnvironment) ?? "";
                creds.igPassword = Environment.GetEnvironmentVariable("password." + creds.igEnvironment) ?? "";
                creds.igApiKey = Environment.GetEnvironmentVariable("apikey." + creds.igEnvironment) ?? "";
                creds.igAccountId = Environment.GetEnvironmentVariable("accountId." + creds.igEnvironment) ?? "";
            }
            else
            {
                //Get credentials from config
                CommonFunctions.AddStatusMessage(igWebApiConnectionConfig[ENVIRONMENT] ?? "DEMO", "INFO");
                creds.igEnvironment = igWebApiConnectionConfig[ENVIRONMENT] ?? "DEMO";
                creds.igUsername = igWebApiConnectionConfig["username." + creds.igEnvironment] ?? "";
                creds.igPassword = igWebApiConnectionConfig["password." + creds.igEnvironment] ?? "";
                creds.igApiKey = igWebApiConnectionConfig["apikey." + creds.igEnvironment] ?? "";
                creds.igAccountId = igWebApiConnectionConfig["accountId." + creds.igEnvironment] ?? "";
            }

            creds.primary = true;
            igContainer = new IGContainer(creds);
            igContainer.igRestApiClient = new IgRestApiClient(creds.igEnvironment, smartDispatcher);
            igContainer.EpicList = CommonFunctions.GetEpicList(epicList.ToArray());

            // Start the lightstreamer bits in a new thread
            CommonFunctions.AddStatusMessage("Starting lightstreamer in a new thread", "INFO");
            Thread t = new(new ThreadStart(igContainer.StartLightstreamer));
            t.Start();


            if (strategy == "GRID")
            {

                if (useEnvironment)
                {
                    creds2.igEnvironment = Environment.GetEnvironmentVariable(ENVIRONMENT) ?? "DEMO";
                    creds2.igUsername = Environment.GetEnvironmentVariable("username2." + creds2.igEnvironment) ?? "";
                    creds2.igPassword = Environment.GetEnvironmentVariable("password2." + creds2.igEnvironment) ?? "";
                    creds2.igApiKey = Environment.GetEnvironmentVariable("apikey2." + creds2.igEnvironment) ?? "";
                    creds2.igAccountId = Environment.GetEnvironmentVariable("accountId2." + creds2.igEnvironment) ?? "";
                }
                else
                {
                    //Get credentials from config
                    creds2.igEnvironment = igWebApiConnectionConfig[ENVIRONMENT] ?? "DEMO";
                    creds2.igUsername = igWebApiConnectionConfig["username2." + creds2.igEnvironment] ?? "";
                    creds2.igPassword = igWebApiConnectionConfig["password2." + creds2.igEnvironment] ?? "";
                    creds2.igApiKey = igWebApiConnectionConfig["apikey2." + creds2.igEnvironment] ?? "";
                    creds2.igAccountId = igWebApiConnectionConfig["accountId2." + creds2.igEnvironment] ?? "";
                }

                creds2.primary = false;
                igContainer2 = new IGContainer(creds2);
                igContainer2.igRestApiClient = new IgRestApiClient(creds2.igEnvironment, smartDispatcher);
                igContainer2.EpicList = CommonFunctions.GetEpicList(epicList.ToArray());

                // Start the lightstreamer bits in a new thread
                CommonFunctions.AddStatusMessage("Starting second lightstreamer in a new thread", "INFO");
                Thread u = new(new ThreadStart(igContainer2.StartLightstreamer));
                u.Start();
            }
            else
            {
                igContainer2 = null;

            }

            the_db = IGModels.clsCommonFunctions.Get_Database("IX.D.NASDAQ.CASH.IP").Result;
            the_app_db = IGModels.clsCommonFunctions.Get_App_Database("IX.D.NASDAQ.CASH.IP").Result;

            foreach (TbEpics tbepic in epcs)
            {
                string jobId = IGModels.clsCommonFunctions.GetLogName(tbepic.epic, tbepic.strategy, tbepic.resolution);
                //MappedDiagnosticsLogicalContext.Set("jobId", jobId);
                ScopeContext.PushProperty("app", "TRADINGBRAIN/");
                ScopeContext.PushProperty("epic", tbepic.epic + "/");
                ScopeContext.PushProperty("strategy", tbepic.strategy + "/");
                ScopeContext.PushProperty("resolution", tbepic.resolution + "/");

                ILogger tbLog;
                tbLog = LogManager.GetLogger(jobId);

                tbLog.Info("-------------------------------------------------------------");
                tbLog.Info("-- TradingBrain started - strategy: {strategy} {respolution} : {epic} --", tbepic.strategy, tbepic.resolution, tbepic.epic);

                if (the_db != null && the_app_db != null)
                {



                    tbLog.Info("Initialising app");

                    workerList.Add(new MainApp(the_db, the_app_db, tbepic.epic, igContainer, igContainer2, tbepic.strategy, tbepic.resolution));



                }
                igContainer.workerList = workerList;
                if (igContainer2 != null)
                {
                    igContainer2.workerList = workerList;
                }
            }
            System.Timers.Timer ti = new();
            ti.AutoReset = false;
            //if (epcs[0].strategy == "GRID")
            //{
            //    await RunSecondAlignedTimer(async () =>
            //    {
            //        RunGridMainAppCode();
            //        await Task.CompletedTask;
            //    });

            //}
            //else
            //{
            if (epcs[0].strategy == "RSI" ||
            epcs[0].strategy == "REI" ||
            epcs[0].strategy == "RSI-ATR" ||
            epcs[0].strategy == "RSI-CUML" ||
            epcs[0].strategy == "CASEYC" ||
            epcs[0].strategy == "CASEYCSHORT" ||
            epcs[0].strategy == "VWAP" ||
            epcs[0].strategy == "CASEYCEQUITIES")
            {
                ti.Elapsed += new System.Timers.ElapsedEventHandler(RunMainAppCode);
                ti.Interval = GetIntervalWithResolution("HOUR");
                //ti.Interval = 1000;
            }
            else
            {

                ti.Elapsed += new System.Timers.ElapsedEventHandler(RunMainAppCode);
                if (epcs[0].strategy == "GRID")
                {


                    ti.Interval = GetIntervalSecond();

                    //var now = DateTime.UtcNow;
                    ////var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second , 100).AddSeconds(1);// now.AddSeconds(1).AddMilliseconds(-50);
                    //var next = now.AddSeconds(1);
                    ////var delay = (next - now).TotalMilliseconds - now.Millisecond;
                    //ti.Interval = (next - now).TotalMilliseconds;

                }
                else
                    ti.Interval = GetInterval(epcs[0].strategy);
            }

            ti.Start();

            await WaitForChanges();


        }

        public static async void RunMainAppCode(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var parallelTasks = new List<Task<RunRet>>();

            try
            {
                if (sender == null) { throw new InvalidOperationException("Timer sender is null"); }

                var t = (System.Timers.Timer?)sender ?? throw new InvalidOperationException("Timer t is null");
                string strat = "";
                foreach (MainApp app in workerList)
                {
                    Task<RunRet> task;
                    switch (app.strategy)
                    {
                        case "GRID":
                            task = Task.Run(() => app.RunCode_GRID(sender, e));
                            parallelTasks.Add(task);
                            break;
                        default:
                            task = Task.Run(() => app.RunCodeV5(sender, e));
                            parallelTasks.Add(task);
                            break;
                    }
                    if (strat == "") { strat = app.strategy; }
                }
                try
                {
                    await Task.WhenAll(parallelTasks);
                }
                catch (Exception)
                {
                    // Log any exceptions from the tasks
                }
                if (workerList[0].strategy == "RSI" ||
                    workerList[0].strategy == "REI" ||
                    workerList[0].strategy == "RSI-ATR" ||
                    workerList[0].strategy == "RSI-CUML" ||
                    workerList[0].strategy == "CASEYC" ||
                     workerList[0].strategy == "VWAP" ||
                    workerList[0].strategy == "CASEYCSHORT" ||
                    workerList[0].strategy == "CASEYCEQUITIES")
                {
                    t.Interval = GetIntervalWithResolution("HOUR");
                }
                else
                {
                    if (workerList[0].strategy == "GRID")
                    {
                        t.Interval = GetIntervalSecond();
                        //clsCommonFunctions.AddStatusMessage($"Next GRID interval set to : {t.Interval} ms", "INFO");
                        //var now = DateTime.UtcNow;
                        ////var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second , 100).AddSeconds(1);// now.AddSeconds(1).AddMilliseconds(-50);
                        //var next = now.AddSeconds(1);
                        ////var delay = (next - now).TotalMilliseconds - now.Millisecond;
                        //t.Interval = (next - now).TotalMilliseconds;
                    }
                    else
                        t.Interval = GetInterval(strat);

                }
                t.Start();
            }
            catch (Exception)
            {
                // blank section
            }
        }
        //public static async void RunGridMainAppCode()
        //{
        //    var parallelTasks = new List<Task<RunRet>>();
        //    //var t = (System.Timers.Timer)sender;
        //    string strat = "";
        //    foreach (MainApp app in workerList)
        //    {
        //        bool ret = await app.GetPositions();

        //        Task<RunRet> task;

        //        task = Task.Run(() => app.RunCode_GRID());
        //        parallelTasks.Add(task);
        //        break;                 
        //    }
        //}
        public static double GetIntervalWithResolution(string resolution)
        {
            DateTime now = DateTime.Now;
            DateTime nextRun = DateTime.MinValue;
            //int testOffset = 0;
            //if (region == "test")
            //{
            //    testOffset = 5;
            //}
            string resUnit = "MINUTE";
            int resNum = 1;
            string[] res = resolution.Split('_');
            if (res.Length == 1 && res[0] == "DAY")
            {
                resUnit = "DAY";
                resNum = 1;
            }
            else
            {
                if (res[0] == "HOUR")
                {
                    resUnit = "HOUR";
                    resNum = 1;
                }
            }
            switch (resUnit)
            {
                case "MINUTE":

                    if (now.Minute < 30)
                        nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, 0, DateTimeKind.Utc); // Next half-hour
                    else
                        nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0, DateTimeKind.Utc).AddHours(1); // Next hour

                    break;

                case "HOUR":
                    nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0, DateTimeKind.Utc).AddHours(resNum - (now.Hour % resNum)); // Next x hour
                    break;

                case "DAY":
                    nextRun = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, 0, DateTimeKind.Utc).AddDays(resNum - (now.Day % resNum)); // Next x day
                    break;

            }

            //Just to test
            //nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, 0).AddMinutes(1);

            // Determine the next execution time: next hour or next half-hour
            //if (now.Minute < 30)
            //    nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, 0); // Next half-hour
            //else
            //    nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0, 0); // Next hour

            if (region == "test")
            {
                //if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
                //{
                //    nextRun = nextRun.AddSeconds(45);
                //}
                //else
                //{

                // nextRun = nextRun.AddSeconds(10);

                nextRun = nextRun.AddSeconds(150);



                //}
            }
            else
            {
                nextRun = nextRun.AddSeconds(150);
                // Make the hour_2, hour_3 and hour_4 resolutions run 15 seconds later to ensure all the candles have been created.
                //if (resolution == "HOUR_2" || resolution == "HOUR_3" || resolution == "HOUR_4")
                //{
                //    nextRun = nextRun.AddSeconds(15);
                //}
            }



            // Calculate the precise interval in milliseconds
            double interval = (nextRun - now).TotalMilliseconds;

            CommonFunctions.AddStatusMessage($"Next run scheduled at: {nextRun:yyyy-MM-dd HH:mm:ss.fff}");
            return interval;

        }
        public static double GetInterval(string strat = "")
        {
            DateTime now = DateTime.Now;

            // testOffset will move it to 10 seconds past the minute to ensure it doesn't interfere with live
            int testOffset = 0;
            if (region == "test")
            {
                testOffset = 20;
            }
            if (strat == "SMA2")
            {
                testOffset = 5;
            }

            if (strat == "RSI")
            {
                testOffset = 15;
            }
            if (strat == "REI" || strat == "RSI-ATR" || strat == "RSI-CUML")
            {
                testOffset = 25;
            }
            return ((now.Second > 30 ? 120 : 60) - now.Second + testOffset) * 1000 - now.Millisecond;
        }
        public static double GetIntervalSecond(string strat = "")
        {

            var now = DateTime.UtcNow;
            //var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second , 100).AddSeconds(1);// now.AddSeconds(1).AddMilliseconds(-50);
            var next = now.AddSeconds(1);
            //var delay = (next - now).TotalMilliseconds - now.Millisecond;
            var delay = (next - now).TotalMilliseconds - now.Millisecond;
            if (delay <= 0)
            {
                delay = 50;
            }
            //clsCommonFunctions.AddStatusMessage($"Interval set : now = {now.Second}:{now.Millisecond}, next = {next.Second}:{next.Millisecond}, delay = {delay}", "INFO");
            return delay + 75;
        }
        public static async Task<int> WaitForChanges()
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

        public void OnLightstreamerUpdate(int item, ItemUpdate values)
        {
            //dataGridview.SuspendLayout();

            if (this.isDirty)
            {
                this.isDirty = false;
                // CleanGrid();
            }


        }

        public static void OnLightstreamerStatusChanged(int cStatus, string status)
        {
            //statusLabel.Text = status;
            //var a = 1;


        }
    }

}