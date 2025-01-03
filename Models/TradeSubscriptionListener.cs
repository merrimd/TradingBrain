using com.lightstreamer.client;
using IGWebApiClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
     class TradeSubscriptionListener : SubscriptionListener
    {
        private int phase;
        private TBStreamingClient slClient;
        public TradeSubscriptionListener(TBStreamingClient slClient, int phase)
        {

            this.phase = phase;
            this.slClient = slClient;
        }

        public void OnUpdate(int itemPos, string itemName, ItemUpdate update)
        {

            this.slClient.TradeUpdateReceived(this.phase, itemPos, update);





        }

        void SubscriptionListener.onClearSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("Trade - On cleasrsnapshot called");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            Console.WriteLine("Trade - On commandsecondlevelitemlostupdates called");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            Console.WriteLine("Trade - On commandsecondlevelsubscriptionerror called");
            // ...
        }

        void SubscriptionListener.onEndOfSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("Trade - On endofsnaphot called");
            // ...
        }

        void SubscriptionListener.onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            Console.WriteLine("Trade - On Itemlostupdates called");
            // ...
        }

        void SubscriptionListener.onItemUpdate(ItemUpdate itemUpdate)
        {
            slClient.TradeUpdateReceived(phase, itemUpdate.ItemPos, itemUpdate);

   

        }

        void SubscriptionListener.onListenEnd(Subscription subscription)
        {
            Console.WriteLine("Trade - On Listen end called");
            // ...
        }

        void SubscriptionListener.onListenStart(Subscription subscription)
        {
            Console.WriteLine("Trade - On Listen start called");
            // ...
        }

        void SubscriptionListener.onSubscription()
        {
            Console.WriteLine("Trade - On subscription called");
            // ...
        }

        void SubscriptionListener.onSubscriptionError(int code, string message)
        {
            Console.WriteLine("Trade - On subscriptionError called : " + code + " - " + message);
            // ...
        }

        void SubscriptionListener.onUnsubscription()
        {
            Console.WriteLine("Trade - On unsubscription called");
            // ...
        }

        void SubscriptionListener.onRealMaxFrequency(string frequency)
        {
            Console.WriteLine("Trade - On realmaxfrequency called");
            // ...
        }


    }
}
