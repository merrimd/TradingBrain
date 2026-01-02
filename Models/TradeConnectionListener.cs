using com.lightstreamer.client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    class TradeConnectionListener : ClientListener
    {
        public const int VOID = -1;
        public const int DISCONNECTED = 0;
        public const int POLLING = 1;
        public const int STREAMING = 2;
        public const int STALLED = 3;

        private TBStreamingClient slClient;
        private int phase;
        public TradeConnectionListener(
        TBStreamingClient slClient,
        int phase)
        {

            this.slClient = slClient;
            this.phase = phase;
        }
        private void OnDisconnection(String status)
        {
            this.slClient.StatusChanged(this.phase, DISCONNECTED, status);
            this.slClient.Start(this.phase);
        }

        public void OnClose()
        {
            this.OnDisconnection("Trade - Connection closed");
        }

        public void OnEnd(int cause)
        {
            this.OnDisconnection("Trade - Connection forcibly closed");
        }

        void ClientListener.onListenEnd(LightstreamerClient client)
        {
            // ...
        }

        void ClientListener.onListenStart(LightstreamerClient client)
        {
            // ...
        }

        void ClientListener.onServerError(int errorCode, string errorMessage)
        {
            this.OnDisconnection("Trade - Server Error " + errorCode + " - " + errorMessage);
        }

        void ClientListener.onStatusChange(string status)
        {
            if (status.StartsWith("CONNECTED:WS"))
            {
                if (status.EndsWith("POLLING"))
                {
                    this.slClient.StatusChanged(this.phase, POLLING, "Trade - Connected over Webscocket in polling mode");
                }
                else if (status.EndsWith("STREAMING"))
                {
                    this.slClient.StatusChanged(this.phase, STREAMING, "Trade - Connected over Websocket in streaming mode");
                }
            }
            else if (status.StartsWith("CONNECTED:HT"))
            {
                if (status.EndsWith("POLLING"))
                {
                    this.slClient.StatusChanged(this.phase, POLLING, "Trade - Connected over HTTP in polling mode");
                }
                else if (status.EndsWith("STREAMING"))
                {
                    this.slClient.StatusChanged(this.phase, STREAMING, "Trade - Connected over HTTP in streaming mode");
                }
            }
            else if (status.StartsWith("CONNECTING"))
            {
                this.slClient.StatusChanged(this.phase, VOID, "Trade - Connecting to Lightstreamer Server...");
            }
            else if (status.StartsWith("DISCONNECTED"))
            {
                this.slClient.StatusChanged(this.phase, DISCONNECTED, "Trade - Disconnected");
            }
            else if (status.StartsWith("STALLED"))
            {
                this.slClient.StatusChanged(this.phase, STALLED, "Trade - Connection stalled");
            }
        }

        void ClientListener.onPropertyChange(string property)
        {
            // ...
        }
    }
}