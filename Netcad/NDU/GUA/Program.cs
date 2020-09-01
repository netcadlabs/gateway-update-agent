using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Netcad.NDU.GUA.Install;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Updater;
using NLog.Extensions.Logging;

namespace Netcad.NDU.GUA {
    class Program {
        public static async Task Main(string[] args) {
            var builder = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) => {
                    config.AddEnvironmentVariables();

                    if (args != null) {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) => {
                    services.AddOptions();
                    services.AddSingleton<ISettings, Settings.Settings>();
                    services.AddSingleton<IHostedService, AgentService>();
                    services.AddSingleton<IUpdater, GatewayUpdater>();
                    services.AddSingleton<IInstallManager, InstallManager>();
                })
                .ConfigureLogging((hostingContext, logging) => {
#if DEBUG
                    var t = new NLog.Targets.ConsoleTarget();
                    var config = NLog.LogManager.Configuration;
                    config.LoggingRules[0].Targets.Clear();
                    config.LoggingRules[0].Targets.Add(t);
                    NLog.LogManager.Configuration = config;
#endif
                    logging.AddNLog();
                });

            await builder.RunConsoleAsync();
        }
    }
}