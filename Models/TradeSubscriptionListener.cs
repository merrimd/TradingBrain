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
            CommonFunctions.AddStatusMessage("Trade - On cleasrsnapshot called","INFO");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            CommonFunctions.AddStatusMessage("Trade - On commandsecondlevelitemlostupdates called", "INFO");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            CommonFunctions.AddStatusMessage("Trade - On commandsecondlevelsubscriptionerror called", "ERROR");
            // ...
        }

        void SubscriptionListener.onEndOfSnapshot(string itemName, int itemPos)
        {
            CommonFunctions.AddStatusMessage("Trade - On endofsnaphot called", "INFO");
            // ...
        }

        void SubscriptionListener.onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            CommonFunctions.AddStatusMessage("Trade - On Itemlostupdates called", "INFO");
            // ...
        }

        void SubscriptionListener.onItemUpdate(ItemUpdate itemUpdate)
        {
            slClient.TradeUpdateReceived(phase, itemUpdate.ItemPos, itemUpdate);

   

        }

        void SubscriptionListener.onListenEnd(Subscription subscription)
        {
            CommonFunctions.AddStatusMessage("Trade - On Listen end called", "INFO");
            // ...
        }

        void SubscriptionListener.onListenStart(Subscription subscription)
        {
            CommonFunctions.AddStatusMessage("Trade - On Listen start called", "INFO");
            // ...
        }

        void SubscriptionListener.onSubscription()
        {
            CommonFunctions.AddStatusMessage("Trade - On subscription called", "INFO");
            // ...
        }

        void SubscriptionListener.onSubscriptionError(int code, string message)
        {
            CommonFunctions.AddStatusMessage("Trade - On subscriptionError called : " + code + " - " + message, "ERROR");
            // ...
        }

        void SubscriptionListener.onUnsubscription()
        {
            CommonFunctions.AddStatusMessage("Trade - On unsubscription called", "INFO");
            // ...
        }

        void SubscriptionListener.onRealMaxFrequency(string frequency)
        {
            CommonFunctions.AddStatusMessage("Trade - On realmaxfrequency called", "INFO");
            // ...
        }


    }
}
