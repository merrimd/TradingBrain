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

namespace TradingBrain.Models
{
    partial class Program
    {
        public static Microsoft.Azure.Cosmos.Container? EpicContainer;
        public static Microsoft.Azure.Cosmos.Container? hourly_container;
        public static Microsoft.Azure.Cosmos.Container? minute_container;
        public static Microsoft.Azure.Cosmos.Container? TicksContainer;
        public static Microsoft.Azure.Cosmos.Container? candlesContainer;
        public static Microsoft.Azure.Cosmos.Container? trade_container;
        public static Database? the_db;
        public static Database? the_app_db;

        static System.Timers.Timer t;

 

        public delegate void StopDelegate();

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
                    candlesContainer = the_db.GetContainer("Candles");
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

                    candlesContainer = the_db.GetContainer("Candles");


                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
            }
            return ret;

        }
    
        
        static async Task Main(string[] args)
        {
            MainApp? app = null;

            string epic = "IX.D.NASDAQ.CASH.IP";
            string strategy = "SMA";
            string resolution = "";

            // See if an epic has been passed in. if not then default to NASDAQ
            if (Environment.GetCommandLineArgs().Length >= 2)
            {
                epic = Environment.GetCommandLineArgs()[1].ToUpper();
            }


            // See if a strategy and resolution has been passed in, otherwise use default. //

            if (Environment.GetCommandLineArgs().Length >= 4)
            {
                strategy = Environment.GetCommandLineArgs()[2].ToUpper();
                resolution = Environment.GetCommandLineArgs()[3].ToUpper();
            }

            // Set up the logging //

            var config = new NLog.Config.LoggingConfiguration();

            var filename = "DEBUG-" + DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + "-" + DateTime.UtcNow.Hour + ".txt";

            string nme = strategy;
            if (strategy == "RSI")
            {
                nme  += "_" + resolution;
            }
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "c:/tblogs/App." + epic + "." + nme + ".${shortdate}.txt", MaxArchiveDays=31, KeepFileOpen=false, Layout = "${longdate}|${level:uppercase=true}|${message}|${exception:format=toString}" };
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
                SetupDB(epic);
                if (strategy == "RSI")
                {
                    minute_container = the_db.GetContainer("Candles_RSI");
                }



                tbLog.Info("Initialising app");

                app = new MainApp(the_db,the_app_db, container, chart_container, epic,minute_container,TicksContainer,trade_container,strategy, resolution );


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
                        clsCommonFunctions.AddStatusMessage("normal proc exit","INFO");
                    }
                    else
                    {
                        clsCommonFunctions.AddStatusMessage("unhandled exception: " + exception.GetType().Name,"ERROR");
                    }
                }




                static double GetInterval()
                {
                    DateTime now = DateTime.Now;
                    //return ((60 - now.Second) * 1000 - now.Millisecond);
                    return ((now.Second > 30 ? 120 : 60) - now.Second) * 1000 - now.Millisecond;
                }

                static void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
                {
                    //Console.WriteLine(DateTime.Now.ToString("o"));
                    t.Interval = GetInterval();
                    t.Start();
                }

 
            }
        }


    }
}
