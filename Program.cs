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
                the_db = await IGModels.clsCommonFunctions.Get_Database();
                the_app_db = await IGModels.clsCommonFunctions.Get_App_Database();

                if (the_db != null)
                {
                    EpicContainer = the_app_db.GetContainer("Epics");
                    hourly_container = the_db.GetContainer("HourlyCandle");

                    if (epic == "IX.D.NIKKEI.DAILY.IP")
                    {
                        minute_container = the_db.GetContainer("MinuteCandle_NIKKEI");
                        TicksContainer = the_db.GetContainer("CandleTicks_NIKKEI");
                        trade_container = the_app_db.GetContainer("TradingBrainTrades_NIKKEI");
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





            Console.Write("Connecting to database....");

            Database? the_db = await IGModels.clsCommonFunctions.Get_Database();
            Database? the_app_db = await IGModels.clsCommonFunctions.Get_App_Database();


            if (the_db != null)
            {
                string epic = "IX.D.NASDAQ.CASH.IP";

                // See if an epic has been passed in. if not then default to NASDAQ
                if (Environment.GetCommandLineArgs().Length >= 2)
                {
                    epic = Environment.GetCommandLineArgs()[1];
                }

                Container container;
                Container chart_container;
                Container trade_container;
                switch (epic)
                {
                    case "IX.D.NIKKEI.DAILY.IP":
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks_NIKKEI"); 
                        trade_container = the_app_db.GetContainer("TradingBrainTrades_NIKKEI");

                        break;

                    default:
                        container = the_db.GetContainer("CandleUpdate");
                        chart_container = the_db.GetContainer("CandleTicks"); 
                        trade_container = the_app_db.GetContainer("TradingBrainTrades");
                        break;
                }

                SetupDB(epic);

                Console.Write("Initialising app");

                app = new MainApp(the_db,the_app_db, container, chart_container, epic,minute_container,TicksContainer,trade_container);


                if (app != null)
                {
                    Console.WriteLine("Waiting for changes...");

                    int i = await app.WaitForChanges();
                }

                Console.WriteLine("Exiting...");

                Environment.Exit(0);



                // test the timer function
                //    if (1 == 2)
                //{
                //    t = new System.Timers.Timer();
                //    t.AutoReset = false;
                //    t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
                //    t.Interval = GetInterval();
                //    t.Start();
                //    Console.ReadLine();
                //}


                //    string epic = "IX.D.NASDAQ.CASH.IP";

                //    if (Environment.GetCommandLineArgs().Length == 2)
                //    {
                //        epic = Environment.GetCommandLineArgs()[1];
                //    }

                //    Console.WriteLine(DateTime.Now + " - Connecting to database....");

                //    bool blnOK = await SetupDB(epic);

                //    if (the_db != null)
                //    {
                //        Console.WriteLine(DateTime.Now + " - Getting Settings...");
                //        Settings settings = await clsCommonFunctions.Get_Settings(the_db);

                //        DateTime dtNow = DateTime.UtcNow;

                //        DateTime currentTime = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0).AddMinutes(-1);

                //        Console.WriteLine(DateTime.Now + " - Creating Candle...");
                //        var watch = new System.Diagnostics.Stopwatch();
                //        watch.Start();
                //        int var1 = 10;
                //        int var2 = 15; 
                //        int var3 = 30; 
                //        int var13 = 6;

                //        ModelMinuteCandle thisCandle = await CreateLiveCandle(the_db, var1, var2,var3,var13, currentTime, epic);
                //        watch.Stop();
                //        Console.WriteLine("CreateLiveCandle  Time taken = " + watch.ElapsedMilliseconds);
                //        Console.WriteLine(DateTime.Now + " - Candle created");

                //        Console.ReadLine();
                //        var i = 1;
                //    }


                // }

                static void Exiting(Exception exception, MainApp app)
                {
                    //Put common cleanup code here (or at the end of the method)
                    if (app != null)
                    {
                        //app.UnsubscribeTidyUp();
                    }

                    if (exception == null)
                    {
                        Console.WriteLine("normal proc exit");
                    }
                    else
                    {
                        Console.WriteLine("unhandled exception: " + exception.GetType().Name);
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
                    Console.WriteLine(DateTime.Now.ToString("o"));
                    t.Interval = GetInterval();
                    t.Start();
                }


            }
        }


    }
}
