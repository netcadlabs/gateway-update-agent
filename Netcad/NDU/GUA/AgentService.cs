using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Updater;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA
{
    public class AgentService : BackgroundService
    {

        private readonly ILogger<AgentService> _logger;
        private readonly IUpdater _updater;
        private readonly ISettings _settings;

        public AgentService(ILogger<AgentService> logger, IUpdater updater, ISettings settings)
        {
            _logger = logger;
            _updater = updater;
            _settings = settings;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"AgentService is starting.");

            stoppingToken.Register(() => _logger.LogDebug($"AgentService background task is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug($"AgentService task doing background work.");

                try
                {
                    _settings.ReloadIfRequired();
                    _updater.Tick("token_sample");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                    _logger.LogInformation($"Error Message: { e.Message}");
                }

                await Task.Delay(getInterval(), stoppingToken);
            }
            _logger.LogDebug($"AgentService background task is stopping.");
        }
        private int getInterval()
        {
            double interval = _settings.IntervalInMinutes;
            return (int)(Helper.ParseDouble(interval, 30) * 60000d);
        }
    }

}