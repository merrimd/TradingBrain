using System;
using System.Windows;
using System.Threading.Tasks;
using IGWebApiClient.Common;
using System;
using System.Net.Http;



namespace TradingBrain.Common
{
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
}
