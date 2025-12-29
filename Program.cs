using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Collections.Specialized;
using System.Configuration;
using System.Net.Http.Headers;
//using IGCandleCreator.Models;
using System.ComponentModel;
using IGModels;
using Container = Microsoft.Azure.Cosmos.Container;
using Microsoft.AspNetCore.Http.Features;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using IGCandleCreator.Models;
using Microsoft.Extensions.Options;
using static IGModels.ModellingModels.GetModelClass;
using System.Reflection.Metadata;
using TradingBrain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using com.lightstreamer.client;

using NLog;
using NLog.Extensions.AzureBlobStorage;
using NLog.Targets;

using IGWebApiClient;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using static TradingBrain.Models.TBStreamingClient;
using System.Drawing.Text;
using System.Collections.Concurrent;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using MimeKit.Encodings;
using Org.BouncyCastle.Tsp;
using System.Timers;
using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;


namespace TradingBrain.Models
{
    partial class ProgramV2
    {
        public static Microsoft.Azure.Cosmos.Container? EpicContainer;
        public static Microsoft.Azure.Cosmos.Container? hourly_container;
        public static Microsoft.Azure.Cosmos.Container? minute_container;
        public static Microsoft.Azure.Cosmos.Container? TicksContainer;
        //public static Microsoft.Azure.Cosmos.Container? candlesContainer;
        public static Microsoft.Azure.Cosmos.Container? trade_container;
        public static Database? the_db;
        public static Database? the_app_db;

        public static List<MainApp> workerList = new List<MainApp>();

        public TBStreamingClient tbClient;
        private bool isDirty = false;

        private string pushServerUrl;
        public string forceT;
        private string forceTransport = "no";

        public IgRestApiClient? igRestApiClient;

        public delegate void StopDelegate();
        public static string region = "";
        public async static Task<bool> SetupDB()
        {
            bool ret = true;

            try
            {
                the_db = await IGModels.clsCommonFunctions.Get_Database();
                the_db = await IGModels.clsCommonFunctions.Get_App_Database();

                if (the_db != null)
                {
                    EpicContainer = the_app_db.GetContainer("Epics");
                    hourly_container = the_db.GetContainer("HourlyCandle");
                    minute_container = the_db.GetContainer("MinuteCandle");
                    TicksContainer = the_db.GetContainer("CandleTicks");
                    //candlesContainer = the_db.GetContainer("Candles");
                    trade_container = the_app_db.GetContainer("TradingBrainTrades");

                }
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

                if (the_db != null)
                {
                    EpicContainer = the_app_db.GetContainer("Epics");
                    hourly_container = the_db.GetContainer("HourlyCandle");

                    if (epic == "IX.D.NIKKEI.DAILY.IP")
                    {
                        minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
                        TicksContainer = the_db.GetContainer("CandleTicks_NIKKEI");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                    }
                    else
                    {
                        minute_container = the_db.GetContainer("MinuteCandle");
                        TicksContainer = the_db.GetContainer("CandleTicks");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                    }

                    //candlesContainer = the_db.GetContainer("Candles");


                }
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

            List<tbEpics> epcs = new List<tbEpics>();
            List<string> epicList = new List<string>();
            string settingEpics = "";

            region = Environment.GetEnvironmentVariable("Region") ?? "";

            if (region == "")
            {
                // not found in environment variables so get from config
                region = igWebApiConnectionConfig["region"] ?? "test";
                settingEpics = igWebApiConnectionConfig["epics"] ?? "IX.D.NASDAQ.CASH.IP|SMA";
            }
            else
            {
                useEnvironment = true;
                settingEpics = Environment.GetEnvironmentVariable("epics") ?? "IX.D.NASDAQ.CASH.IP|SMA";
            }


            if (settingEpics != "IX.D.NASDAQ.CASH.IP|SMA")
            {
                foreach (string tmp in settingEpics.Split(",").ToList())
                {
                    epcs.Add(new tbEpics(tmp));
                    epicList.Add(tmp);
                }
            }
            else
            {
                epcs.Add(new tbEpics("IX.D.NASDAQ.CASH.IP|SMA"));
                epicList.Add("IX.D.NASDAQ.CASH.IP|SMA");
            }
            string[] epics = { epcs[0].epic };

            string strategy = epcs[0].strategy;

            // Set up the logging //

            var config = new NLog.Config.LoggingConfiguration();

            string blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}app-log.log";
            if (strategy == "GRID")
            {
                blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}${date:format=HH}/app-log.log";
            }

            BlobStorageTarget azureBlobTarget = new BlobStorageTarget()
            {
                Name = "azureBlob",

                // Your Azure Blob connection string
                ConnectionString = "DefaultEndpointsProtocol=https;AccountName=mikewardig;AccountKey=RwdizLFFqzdH7VkjssHQtlXyyPc/WitD5lWPl67XEqvObt1wFSFI6amn2mc/DPmMTXeoIkMxoRPo+ASts6Rm/Q==;EndpointSuffix=core.windows.net",

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
                         
            IGContainer igContainer = null;
            IGContainer igContainer2 = null;
            IgApiCreds creds = new IgApiCreds();
            IgApiCreds creds2 = new IgApiCreds();

            SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();

            if (useEnvironment)
            {
                clsCommonFunctions.AddStatusMessage(Environment.GetEnvironmentVariable("environment") ?? "DEMO", "INFO");
                creds.igEnvironment = Environment.GetEnvironmentVariable("environment") ?? "DEMO";
                creds.igUsername = Environment.GetEnvironmentVariable("username." + creds.igEnvironment) ?? "";
                creds.igPassword = Environment.GetEnvironmentVariable("password." + creds.igEnvironment) ?? "";
                creds.igApiKey = Environment.GetEnvironmentVariable("apikey." + creds.igEnvironment) ?? "";
                creds.igAccountId = Environment.GetEnvironmentVariable("accountId." + creds.igEnvironment) ?? "";
            }
            else
            {
                //Get credentials from config
                clsCommonFunctions.AddStatusMessage(igWebApiConnectionConfig["environment"] ?? "DEMO", "INFO");
                creds.igEnvironment = igWebApiConnectionConfig["environment"] ?? "DEMO";
                creds.igUsername = igWebApiConnectionConfig["username." + creds.igEnvironment] ?? "";
                creds.igPassword = igWebApiConnectionConfig["password." + creds.igEnvironment] ?? "";
                creds.igApiKey = igWebApiConnectionConfig["apikey." + creds.igEnvironment] ?? "";
                creds.igAccountId = igWebApiConnectionConfig["accountId." + creds.igEnvironment] ?? "";
            }

            creds.primary = true;
            igContainer = new IGContainer(creds);
            igContainer.igRestApiClient = new IgRestApiClient(creds.igEnvironment, smartDispatcher);
            igContainer.EpicList = clsCommonFunctions.GetEpicList(epicList.ToArray());

            // Start the lightstreamer bits in a new thread
            clsCommonFunctions.AddStatusMessage("Starting lightstreamer in a new thread", "INFO");
            Thread t = new Thread(new ThreadStart(igContainer.StartLightstreamer));
            t.Start();


            if (strategy == "GRID")
            {

                if (useEnvironment)
                {
                    creds2.igEnvironment = Environment.GetEnvironmentVariable("environment") ?? "DEMO";
                    creds2.igUsername = Environment.GetEnvironmentVariable("username." + creds2.igEnvironment) ?? "";
                    creds2.igPassword = Environment.GetEnvironmentVariable("password." + creds2.igEnvironment) ?? "";
                    creds2.igApiKey = Environment.GetEnvironmentVariable("apikey." + creds2.igEnvironment) ?? "";
                    creds2.igAccountId = Environment.GetEnvironmentVariable("accountId." + creds2.igEnvironment) ?? "";
                }
                else
                {
                    //Get credentials from config
                    creds2.igEnvironment = igWebApiConnectionConfig["environment"] ?? "DEMO";
                    creds2.igUsername = igWebApiConnectionConfig["username2." + creds2.igEnvironment] ?? "";
                    creds2.igPassword = igWebApiConnectionConfig["password2." + creds2.igEnvironment] ?? "";
                    creds2.igApiKey = igWebApiConnectionConfig["apikey2." + creds2.igEnvironment] ?? "";
                    creds2.igAccountId = igWebApiConnectionConfig["accountId2." + creds2.igEnvironment] ?? "";
                }

                creds2.primary = false;
                igContainer2 = new IGContainer(creds2);
                igContainer2.igRestApiClient = new IgRestApiClient(creds2.igEnvironment, smartDispatcher);
                igContainer2.EpicList = clsCommonFunctions.GetEpicList(epicList.ToArray());

                // Start the lightstreamer bits in a new thread
                clsCommonFunctions.AddStatusMessage("Starting second lightstreamer in a new thread", "INFO");
                Thread u = new Thread(new ThreadStart(igContainer2.StartLightstreamer));
                u.Start();
            }

            Database? the_db = IGModels.clsCommonFunctions.Get_Database("IX.D.NASDAQ.CASH.IP").Result;
            Database? the_app_db = IGModels.clsCommonFunctions.Get_App_Database("IX.D.NASDAQ.CASH.IP").Result;

            foreach (tbEpics tbepic in epcs)
            {
                string jobId = IGModels.clsCommonFunctions.GetLogName(tbepic.epic, tbepic.strategy, tbepic.resolution);
                //MappedDiagnosticsLogicalContext.Set("jobId", jobId);
                ScopeContext.PushProperty("app", "TRADINGBRAIN/");
                ScopeContext.PushProperty("epic", tbepic.epic + "/") ;
                ScopeContext.PushProperty("strategy", tbepic.strategy + "/");
                ScopeContext.PushProperty("resolution", tbepic.resolution + "/");

                ILogger tbLog;
                tbLog = LogManager.GetLogger(jobId);

                tbLog.Info("-------------------------------------------------------------");
                tbLog.Info($"-- TradingBrain started - strategy: {tbepic.strategy + " " + tbepic.resolution} : {tbepic.epic} --");
                tbLog.Info("-------------------------------------------------------------");

                if (the_db != null)
                {


                    Container container = null;
                    Container chart_container = null;
                    Container trade_container = null;
                    Container minute_container = null;
                    Container TicksContainer = null;
                    Container candles_RSI_container = null;

                    candles_RSI_container = the_db.GetContainer("Candles_RSI");

                    switch (tbepic.epic)
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

                    if (tbepic.strategy == "RSI" || 
                        tbepic.strategy == "REI" || 
                        tbepic.strategy == "RSI-ATR" || 
                        tbepic.strategy == "RSI-CUML" || 
                        tbepic.strategy == "CASEYC" ||
                        tbepic.strategy == "CASEYCSHORT" ||
                        tbepic.strategy == "VWAP" ||
                        tbepic.strategy == "CASEYCEQUITIES")
                    {
                        minute_container = the_db.GetContainer("Candles_RSI");
                    }
                    else
                    {
                        if (tbepic.epic == "IX.D.NIKKEI.DAILY.IP")
                        {
                            minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
                        }
                        else
                        {
                            minute_container = the_db.GetContainer("MinuteCandle");
                        }
                    }
                    tbLog.Info("Initialising app");

                    workerList.Add(new MainApp(the_db, the_app_db, container, chart_container, tbepic.epic, minute_container, candles_RSI_container, TicksContainer, trade_container, igContainer,igContainer2, tbepic.strategy, tbepic.resolution));
         
                    ;

                }
                igContainer.workerList = workerList;
                if (igContainer2 != null)
                {
                    igContainer2.workerList = workerList;
                }
            }
            System.Timers.Timer ti = new System.Timers.Timer();
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

            //}
            int i = await WaitForChanges();


        }
            static async Task RunSecondAlignedTimer(Func<Task> action)
            {
                while (true)
                {
          
                DateTime now = DateTime.UtcNow;
                    double msToNextSecond = 1000 - now.Millisecond   - (now.Ticks % TimeSpan.TicksPerMillisecond) / 10000.0;

                    if (msToNextSecond < 0) msToNextSecond =0;

                    await Task.Delay((int)msToNextSecond);
                await action();
                //await action();
            }
            }
        public static async void RunMainAppCode(object sender, System.Timers.ElapsedEventArgs e)
        {
            var parallelTasks = new List<Task<RunRet>>();
            var t = (System.Timers.Timer)sender;
            string strat = "";
            foreach (MainApp app in workerList)
            {
                //bool ret = await app.GetPositions();

                Task<RunRet> task;
                switch (app.strategy){
                    //case "RSI":
                    //    task = Task.Run(() => app.RunCode_RSI(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "RSI-ATR":
                    //    task = Task.Run(() => app.RunCode_RSI_ATR(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "RSI-CUML":
                    //    task = Task.Run(() => app.RunCode_RSI_CUML(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "CASEYC":
                    //    //task = Task.Run(() => app.RunCode_CASEYC(sender, e));
                    //    task = Task.Run(() => app.RunCode_CASEYCv2(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "CASEYCEQUITIES":
                    //    task = Task.Run(() => app.RunCode_CASEYC(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "CASEYCSHORT":
                    //    task = Task.Run(() => app.RunCode_CASEYCSHORT(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "REI":
                    //    task = Task.Run(() => app.RunCode_REI(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    //case "VWAP":
                    //    task = Task.Run(() => app.RunCode_VWAP(sender, e));
                    //    parallelTasks.Add(task);
                    //    break;
                    case "BOLLI":
                        task = Task.Run(() => app.RunCode_BOLLI(sender, e));
                        parallelTasks.Add(task);
                        break;
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
            catch (Exception ex)
            {
                var exc = ex;
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
            int testOffset = 0;
            if (region == "test")
            {
                testOffset = 5;
            }
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
                //if (res.Length == 1 && res[0] == "HOUR")
                //{
                //    resUnit = "HOUR";
                //    resNum = 1;
                //}
                //else
                //{
                //    if (res.Length == 2)
                //    {
                //        resUnit = res[0];
                //        resNum = Convert.ToInt16(res[1]);
                //    }
                //}
            }
            switch (resUnit)
            {
                case "MINUTE":
                    if (resNum == 2)
                    {
                        nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, 0).AddMinutes(2 - (now.Minute % 2)); // Next minute
                    }
                    else
                    {
                        if (now.Minute < 30)
                            nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, 0); // Next half-hour
                        else
                            nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0).AddHours(1); // Next hour
                    }
                    break;

                case "HOUR":
                    nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, 0).AddHours(resNum - (now.Hour % resNum)); // Next x hour
                    break;

                case "DAY":
                    nextRun = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, 0).AddDays(resNum - (now.Day % resNum)); // Next x day
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

            clsCommonFunctions.AddStatusMessage($"Next run scheduled at: {nextRun:yyyy-MM-dd HH:mm:ss.fff}");
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
            if (delay <= 0) { delay = 50;
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
        static async Task CreateTBThread(string epic, string strategy, string resolution,IGContainer igContainer)
        {
            System.Timers.Timer t;

            MainApp? app = null;

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

            var config = new NLog.Config.LoggingConfiguration();

            var filename = "DEBUG-" + DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + "-" + DateTime.UtcNow.Hour + ".txt";

            string nme = strategy;
            if (strategy == "RSI" || strategy == "REI")
            {
                nme += "_" + resolution;
            }
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "c:/tblogs/App." + epic + "." + nme + ".${shortdate}.txt", MaxArchiveDays = 31, KeepFileOpen = false, Layout = "${longdate}|${level:uppercase=true}|${message}|${exception:format=toString}" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=toString}";

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;
            Logger tbLog = LogManager.GetCurrentClassLogger();



            tbLog.Info("-------------------------------------------------------------");
            tbLog.Info($"-- TradingBrain started - strategy: {strategy + " " + resolution} : {epic} --");
            tbLog.Info("-------------------------------------------------------------");

            tbLog.Info("Connecting to database....");

            Database? the_db = await IGModels.clsCommonFunctions.Get_Database(epic);
            Database? the_app_db = await IGModels.clsCommonFunctions.Get_App_Database(epic);


            if (the_db != null)
            {


                Container container;
                Container chart_container;
                Container trade_container;

                Container candles_RSI_container = the_db.GetContainer("Candles_RSI");
                switch (epic)
                {
                    case "IX.D.NIKKEI.DAILY.IP":
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks_NIKKEI");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");

                        break;

                    default:
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                        break;
                }
                await SetupDB(epic);
                if (strategy == "RSI" || strategy == "REI")
                {
                    minute_container = the_db.GetContainer("Candles_RSI");
                }

                tbLog.Info("Initialising app");

                //app = new MainApp(the_db, the_app_db, container, chart_container, epic, minute_container, candles_RSI_container , TicksContainer, trade_container,igContainer, igContainer, strategy, resolution);



                if (app != null)
                {
                    tbLog.Info("Waiting for changes...");

                    int i = await app.WaitForChanges();
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
        //public void StartLightstreamer()
        //{

        //    tbClient = new TBStreamingClient(
        //        pushServerUrl,
        //        forceT,
        //        igContainer,
        //        new Delegates.LightstreamerUpdateDelegate(OnLightstreamerUpdate),
        //        new Delegates.LightstreamerStatusChangedDelegate(OnLightstreamerStatusChanged));


        //    tbClient.Start();

        //}
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
