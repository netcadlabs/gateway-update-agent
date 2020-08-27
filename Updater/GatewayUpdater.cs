using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GatewayUpdateAgent.Download;
using Netcad.NDU.GatewayUpdateAgent.Install;
using Netcad.NDU.GatewayUpdateAgent.Settings;

namespace Netcad.NDU.GatewayUpdateAgent.Updater {
    public class GatewayUpdater : IUpdater {
        private readonly ILogger<GatewayUpdater> logger;
        private readonly ISettings settings;
        private readonly IInstallManager installManager;

        public GatewayUpdater(ILogger<GatewayUpdater> logger, ISettings settings, IInstallManager installManager) {
            this.logger = logger;
            this.settings = settings;
            this.installManager = installManager;
        }

        int _tick;
        void IUpdater.Tick(string gatewayToken) {
            logger.LogInformation($"Tick ... {++_tick}");
            //if (_test % 100 == 0) throw new Exception("test ex");

            checkUpdates();
            //checkYaml();
        }

        private void checkUpdates() {
            //****
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf1.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf1_Pack1.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf1_Pack2.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf2.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf2_Pack1.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Conf2_Pack2.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Pack1.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_Pack1.json";
            // string url = "https://raw.githubusercontent.com/korhun/nduagent/master/test/getUpdates_uninstall.json";

            settings.Reload(); //****
            string url = settings.Hostname;

            var webClient = new WebClient();
            string bundlesArrayJson = webClient.DownloadString(url);

            var obj = System.Text.Json.JsonSerializer.Deserialize<Bundle[]>(bundlesArrayJson);

            this.installManager.CheckUpdates(obj);
        }

        // private class jsonObj {
        //     public Bundle[] value { get; set; }
        // }
    }
}