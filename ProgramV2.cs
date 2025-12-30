using IGWebApiClient;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.AzureBlobStorage;
using NLog.Extensions.Logging;
using NLog.Targets;
//using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingBrain.Models;
using static TradingBrain.Models.TBStreamingClient;
using ConfigurationManager = System.Configuration.ConfigurationManager;
using Container = Microsoft.Azure.Cosmos.Container;
using LogLevel = NLog.LogLevel;



namespace TradingBrain
{
    public class Program
    {
        public static string region = "test";
        public static List<MainApp> workerList = new List<MainApp>();
        public static Database? the_db;
        public static Database? the_app_db;
        public TBStreamingClient tbClient;
        public IgRestApiClient? igRestApiClient;

        

        static async Task Main(string[] args)
        {

            List<tbEpics> items = new List<tbEpics>();

            object v = ConfigurationManager.GetSection("appSettings");
            NameValueCollection igWebApiConnectionConfig = (NameValueCollection)v;
            List<tbEpics> epcs = new List<tbEpics>();

            region = igWebApiConnectionConfig["region"] ?? "test";

            string settingEpics = igWebApiConnectionConfig["epics"] ?? "IX.D.NASDAQ.CASH.IP|SMA";
            List<string> epicList = new List<string>();
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


            var workerCount = epics.Count();



            // Set up the logging //

            var config = new NLog.Config.LoggingConfiguration();

            string blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}app-log.log";
            if (strategy == "GRID")
            {
                blobName = "${date:format=yyyy-MM-dd}/${scopeproperty:item=app}${scopeproperty:item=strategy}${scopeproperty:item=epic}${scopeproperty:item=resolution}${date:format=HH}/app-log.log";
            }
            string blobConnectionString = Environment.GetEnvironmentVariable("BlobConnectionString") ?? "";
            if (blobConnectionString == "")
            {
                blobConnectionString = igWebApiConnectionConfig["BlobConnectionString"] ?? "";
            }
            BlobStorageTarget azureBlobTarget = new BlobStorageTarget()
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

            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${longdate} |${level:uppercase=true}|${scopeproperty:item=strategy}|${scopeproperty:item=epic}-${scopeproperty:item=resolution}|${message}|${exception:format=toString}";

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);


            NLog.LogManager.Configuration = config;

            //MappedDiagnosticsLogicalContext.Set("jobId", "default");

            ScopeContext.PushProperty("app", "TRADINGBRAIN/");
            ScopeContext.PushProperty("epic", "DEFAULT/");
            ScopeContext.PushProperty("strategy", "");
            ScopeContext.PushProperty("resolution", "");

            var logger= NLog.LogManager.GetCurrentClassLogger();

            IGContainer igContainer = null;
            IGContainer igContainer2 = null;
            IgApiCreds creds = new IgApiCreds();
            IgApiCreds creds2 = new IgApiCreds();
            clsCommonFunctions.AddStatusMessage(igWebApiConnectionConfig["environment"] ?? "DEMO", "INFO");
            SmartDispatcher smartDispatcher = (SmartDispatcher)SmartDispatcher.getInstance();

            //Get credentials from config
            creds.igEnvironment = igWebApiConnectionConfig["environment"] ?? "DEMO";
            creds.igUsername = igWebApiConnectionConfig["username." + creds.igEnvironment] ?? "";
            creds.igPassword = igWebApiConnectionConfig["password." + creds.igEnvironment] ?? "";
            creds.igApiKey = igWebApiConnectionConfig["apikey." + creds.igEnvironment] ?? "";
            creds.igAccountId = igWebApiConnectionConfig["accountId." + creds.igEnvironment] ?? "";
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

                //Get credentials from config
                creds2.igEnvironment = igWebApiConnectionConfig["environment"] ?? "DEMO";
                creds2.igUsername = igWebApiConnectionConfig["username2." + creds2.igEnvironment] ?? "";
                creds2.igPassword = igWebApiConnectionConfig["password2." + creds2.igEnvironment] ?? "";
                creds2.igApiKey = igWebApiConnectionConfig["apikey2." + creds2.igEnvironment] ?? "";
                creds2.igAccountId = igWebApiConnectionConfig["accountId2." + creds2.igEnvironment] ?? "";
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
                ScopeContext.PushProperty("epic", tbepic.epic + "/");
                ScopeContext.PushProperty("strategy", tbepic.strategy + "/");
                ScopeContext.PushProperty("resolution", tbepic.resolution + "/");

                //ILogger tbLog;
                logger = LogManager.GetLogger(jobId);

                logger.Info("-------------------------------------------------------------");
                logger.Info($"-- TradingBrain started - strategy: {tbepic.strategy + " " + tbepic.resolution} : {tbepic.epic} --");
                logger.Info("-------------------------------------------------------------");

                if (the_db != null)
                {

                    Container trade_container = null;
                    Container candles_RSI_container = null;

                    candles_RSI_container = the_db.GetContainer("Candles_RSI");
                    trade_container = the_app_db.GetContainer("TradingBrainTrades");
                    
                    logger.Info("Initialising app");

                    MainApp mainApp = new MainApp(the_db, the_app_db,   tbepic.epic, candles_RSI_container, trade_container, igContainer, igContainer2, tbepic.strategy, tbepic.resolution);
                    workerList.Add(mainApp);

                }

                igContainer.workerList = workerList;
                if (igContainer2 != null)
                {
                    igContainer2.workerList = workerList;
                }


            }

 




            //var workerCount = ParseWorkerCount(args);
            Console.WriteLine($"Starting with worker count: {workerCount}");

            var builder = Host.CreateApplicationBuilder(args);

            // Push worker count into configuration so WorkerPool can read it
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "WorkerCount", workerCount.ToString() }
            });

            // Replace default logging with NLog
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.Logging.AddNLog();

            // Register services
            builder.Services.AddSingleton(workerList);
            //builder.Services.AddSingleton(new ProducerConfig(epcs));
            //builder.Services.AddSingleton<WorkChannel>();
            builder.Services.AddHostedService<WorkerManager>();
            //builder.Services.AddHostedService<WorkerPool>();




            var host = builder.Build();

            // This turns Ctrl+C into a graceful shutdown event
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Shutting down...");
                host.StopAsync().Wait();
            };

            await host.RunAsync();
        }
    }
    
    //public class ProducerConfigItem
    //{
    //    public string epic { get; set; } = "";
    //    public string strategy { get; set; } = "";
    //    public string resolution { get; set; } = "";
    //}
    public class ProducerConfig
    {
        public List<tbEpics> items { get; set; } = new List<tbEpics>();
        public ProducerConfig(List<tbEpics> epcs)
        {
            items = epcs;
        }
    }
}
