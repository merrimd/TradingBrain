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
        private readonly int phase;
        private readonly TBStreamingClient slClient;
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
            CommonFunctions.AddStatusMessage("Chart - On clearsnapshot called", "INFO");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            CommonFunctions.AddStatusMessage("Chart - On commandsecondlevelitemlostupdates called", "INFO");
            // ...
        }

        void SubscriptionListener.onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            CommonFunctions.AddStatusMessage("Chart - On commandsecondlevelsubscriptionerror called", "ERROR");
            // ...
        }

        void SubscriptionListener.onEndOfSnapshot(string itemName, int itemPos)
        {
            CommonFunctions.AddStatusMessage("Chart - On endofsnaphot called", "INFO");
            // ...
        }

        void SubscriptionListener.onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            CommonFunctions.AddStatusMessage("Chart - On Itemlostupdates called", "INFO");
            // ...
        }

        void SubscriptionListener.onItemUpdate(ItemUpdate itemUpdate)
        {
            slClient.ChartUpdateReceived(phase, itemUpdate.ItemPos, itemUpdate);
        }

        void SubscriptionListener.onListenEnd(Subscription subscription)
        {
            CommonFunctions.AddStatusMessage("Chart - On Listen end called", "INFO");
            // ...
        }

        void SubscriptionListener.onListenStart(Subscription subscription)
        {
            CommonFunctions.AddStatusMessage("Chart - On Listen start called", "INFO");
            // ...
        }

        void SubscriptionListener.onSubscription()
        {
            CommonFunctions.AddStatusMessage("Chart - On subscription called", "INFO");
            // ...
        }

        void SubscriptionListener.onSubscriptionError(int code, string message)
        {
            CommonFunctions.AddStatusMessage("Chart - On subscriptionError called : " + code + " - " + message, "ERROR");
            // ...
        }

        void SubscriptionListener.onUnsubscription()
        {
            CommonFunctions.AddStatusMessage("Chart - On unsubscription called", "INFO");
            // ...
        }

        void SubscriptionListener.onRealMaxFrequency(string frequency)
        {
            CommonFunctions.AddStatusMessage("Chart - On realmaxfrequency called", "INFO" );
            // ...
        }


    }
}
