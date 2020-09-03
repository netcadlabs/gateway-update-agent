using System.Runtime.CompilerServices;
using System;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Install;
using Netcad.NDU.GUA.Settings;
using Newtonsoft.Json;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Updater {
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
            settings.ReloadIfRequired();
            string url = string.Concat(settings.Hostname, settings.Token);

            var webClient = new WebClient();
            string bundlesArrayJson = webClient.DownloadString(url);

            UpdateInfo[] updates = null;
            try {
                updates = Helper.DeserializeFromJsonText<UpdateInfo[]>(bundlesArrayJson);
            } catch (Exception ex) {
                throw new Exception($"Error while parsing bundles! API response:{bundlesArrayJson}", ex);
            }
            this.installManager.CheckUpdates(updates);
        }

    }
}