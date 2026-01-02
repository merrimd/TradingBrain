using dto.endpoint.confirms;
using dto.endpoint.positions.create.otc.v1;
using IGModels;
using IGWebApiClient;
using Org.BouncyCastle.Tls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingBrain.Models
{
    public class TradingAPIs
    {

        public async void CreateLongPosition(IgRestApiClient igRestApiClient, tradeItem trade, string epicName)
        {
            dto.endpoint.positions.create.otc.v1.CreatePositionRequest pos = new dto.endpoint.positions.create.otc.v1.CreatePositionRequest();

            pos.epic = epicName;
            pos.expiry = "DFB";
            pos.direction = "BUY";
            pos.size = (decimal?)trade.quantity;
            pos.orderType = "MARKET";
            pos.guaranteedStop = true;
            pos.stopDistance = (decimal?)trade.stopLossValue;
            pos.forceOpen = true;
            pos.currencyCode = "GBP";
            //pos.limitDistance = 50;

            //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);

            IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);


            //if (ret.StatusCode == System.Net.HttpStatusCode.OK)
            //{
            //    trade.tbDealId = ret.Response.dealReference;
            //    IgResponse<ConfirmsResponse> dealRet = await igRestApiClient.retrieveConfirm(ret.Response.dealReference);

            //    Console.WriteLine("Create position created: " + dealRet.Response.dealStatus);

            //    trade.tbDealResponse = dealRet.Response.dealStatus;
            //    trade.tbDealStatus = dealRet.Response.dealStatus;
            //    trade.tbDealReference = dealRet.Response.dealReference;
            //    trade.purchaseDate = DateTime.Now;
            //    trade.buyPrice =  (decimal)dealRet.Response.level;
            //    trade.tbReason = dealRet.Response.reason;
            //    trade.stopLossValue = (double)trade.buyPrice - (double)dealRet.Response.stopLevel;
            //}
            //else
            //{
            //    Console.WriteLine("Create position failed: " + ret.StatusCode.ToString());
            //    DealResponse = "";
            //}
            // OnPropertyChanged(nameof(DealID));
            //GetRestPositions();


        }
        //public async void CreateShortPosition(IgRestApiClient igRestApiClient)
        //{
        //    dto.endpoint.positions.create.otc.v1.CreatePositionRequest pos = new dto.endpoint.positions.create.otc.v1.CreatePositionRequest();

        //    pos.epic = "IX.D.NIKKEI.DAILY.IP";
        //    pos.expiry = "DFB";
        //    pos.direction = "SELL";
        //    pos.size = (decimal?)0.5;
        //    pos.orderType = "MARKET";
        //    pos.guaranteedStop = true;
        //    pos.stopDistance = 42;
        //    //pos.limitDistance = 50;
        //    pos.forceOpen = true;
        //    pos.currencyCode = "GBP";


        //    //var response = await igRestApiClient.SecureAuthenticate(ar, apiKey);

        //    IgResponse<CreatePositionResponse> ret = await igRestApiClient.createPositionV1(pos);


        //    if (ret.StatusCode == System.Net.HttpStatusCode.OK)
        //    {
        //        DealID = ret.Response.dealReference;
        //        IgResponse<ConfirmsResponse> dealRet = await igRestApiClient.retrieveConfirm(ret.Response.dealReference);
        //        UpdatePositionMessage("Create position created: " + dealRet.Response.dealStatus);
        //        DealResponse = dealRet.Response.dealStatus;
        //    }
        //    else
        //    {
        //        UpdatePositionMessage("Create position failed: " + ret.StatusCode.ToString());
        //        DealResponse = "";
        //    }
        //    // OnPropertyChanged(nameof(DealID));
        //    GetRestPositions();


        //}
    }
}