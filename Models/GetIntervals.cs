using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class GetIntervals
    {
 

        public static double GetIntervalWithResolution(string region,string resolution)
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
        public static double GetInterval(string region,string strat = "")
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
        public static double GetIntervalSecond(string region,string strat = "")
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
            return delay;
        }
    }
}
