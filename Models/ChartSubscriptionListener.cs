using com.lightstreamer.client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
     class ChartSubscriptionListener :SubscriptionListener
    {
        private int phase;
        private TBStreamingClient slClient;
        public ChartSubscriptionListener(TBStreamingClient slClient, int phase)
        {

            this.phase = phase;
            this.slClient = slClient;
        }

        public void OnUpdate(int itemPos, string itemName, ItemUpdate update)
        {
            
            this.slClient.ChartUpdateReceived(this.phase, itemPos, update);
        }

        void SubscriptionListener.onClearSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("Chart - On cleasrsnapshot called");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            Console.WriteLine("Chart - On commandsecondlevelitemlostupdates called");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            Console.WriteLine("Chart - On commandsecondlevelsubscriptionerror called");
            // ...
        }

        void SubscriptionListener.onEndOfSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("Chart - On endofsnaphot called");
            // ...
        }

        void SubscriptionListener.onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            Console.WriteLine("Chart - On Itemlostupdates called");
            // ...
        }

        void SubscriptionListener.onItemUpdate(ItemUpdate itemUpdate)
        {
            slClient.ChartUpdateReceived(phase, itemUpdate.ItemPos, itemUpdate);
        }

        void SubscriptionListener.onListenEnd(Subscription subscription)
        {
            Console.WriteLine("Chart - On Listen end called");
            // ...
        }

        void SubscriptionListener.onListenStart(Subscription subscription)
        {
            Console.WriteLine("Chart - On Listen start called");
            // ...
        }

        void SubscriptionListener.onSubscription()
        {
            Console.WriteLine("Chart - On subscription called");
            // ...
        }

        void SubscriptionListener.onSubscriptionError(int code, string message)
        {
            Console.WriteLine("Chart - On subscriptionError called : " + code + " - " + message);
            // ...
        }

        void SubscriptionListener.onUnsubscription()
        {
            Console.WriteLine("Chart - On unsubscription called");
            // ...
        }

        void SubscriptionListener.onRealMaxFrequency(string frequency)
        {
            Console.WriteLine("Chart - On realmaxfrequency called");
            // ...
        }


    }
}
