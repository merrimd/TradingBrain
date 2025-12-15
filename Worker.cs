using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.lightstreamer.client.requests;
using Microsoft.Extensions.Hosting;
using NLog;
using TradingBrain.Models;
namespace TradingBrain
{
    public class Worker : BackgroundService
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly WorkChannel _channel;
        private readonly MainApp _mainApp;

        public Worker(MainApp mainApp, WorkChannel channel)
        {
            _mainApp = mainApp;
            _channel = channel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info($"Worker {_mainApp.epicName} ({_mainApp.strategy}/{_mainApp.resolution}) started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Get current time
                var now = DateTime.UtcNow;

                // 2. Compute next second boundary
                var next = now.AddMilliseconds(1000 - now.Millisecond)
                              .AddTicks(-now.Ticks % TimeSpan.TicksPerMillisecond);

                // 3. Wait until the exact next second
                var delay = next - now;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken);

                // 4. Execute the job for this worker
                await RunJobAsync(stoppingToken);
            }

            //await foreach (var msg in _channel.channel.Reader.ReadAllAsync(stoppingToken))
            //{
            //    _logger.Info($"Worker {_mainApp.epicName} processing message: {msg}");
            //    System.Timers.Timer ti = new System.Timers.Timer();
            //    ti.AutoReset = false;
            //    ti.Elapsed += new System.Timers.ElapsedEventHandler(_mainApp.RunMainAppCode);

            //    switch (_mainApp.resolution)
            //    {
            //        case "HOUR":
            //            ti.Interval = GetIntervals.GetIntervalWithResolution(_mainApp.region, "HOUR");
            //            break;

            //        case "SECOND":
            //            ti.Interval = GetIntervals.GetIntervalSecond(_mainApp.region);
            //            break;
            //        default:

            //            ti.Interval = GetIntervals.GetInterval(_mainApp.region, _mainApp.resolution);
            //            break;
            //    }

            //    ti.Start();
            //}
        }

        private async Task<Task> RunJobAsync(CancellationToken token)
        {
            _logger.Info($"[{DateTime.UtcNow:HH:mm:ss.fff}] Worker {_mainApp.epicName} running job: {_mainApp.strategy}/{_mainApp.resolution}");

            await _mainApp.RunMainAppCode(null,null);

            return Task.CompletedTask;
        }

    }
}
