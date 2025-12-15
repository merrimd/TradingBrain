using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingBrain.Models;
namespace TradingBrain
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using NLog;

    public class WorkerManager : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly List<MainApp> _mainApps;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly List<Worker> _workers = new();

        public WorkerManager(IServiceProvider provider, List<MainApp> mainApps)
        {
            _provider = provider;
            _mainApps = mainApps;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info($"Starting {_mainApps.Count} workers...");

            foreach (var cfg in _mainApps)
            {
                var channel = new WorkChannel();
                var worker = new Worker(cfg, channel);
                   
                _workers.Add(worker);

                // Start background process
                _ = worker.StartAsync(stoppingToken);

                // Send initial message to each worker
                channel.channel.Writer.TryWrite(cfg);
            }

            // This manager just keeps running
            await Task.Delay(-1, stoppingToken);
        }
    }
}
