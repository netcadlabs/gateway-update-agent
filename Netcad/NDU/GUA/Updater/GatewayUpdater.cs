using System;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Install;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Updater
{
    internal class GatewayUpdater : IUpdater
    {
        private readonly ILogger<GatewayUpdater> logger;
        private readonly ISettings settings;
        private readonly IInstallManager installManager;

        public GatewayUpdater(ILogger<GatewayUpdater> logger, ISettings settings, IInstallManager installManager)
        {
            this.logger = logger;
            this.settings = settings;
            this.installManager = installManager;
        }

        private static object tickLocker = new object();
        int _tick;
        void IUpdater.Tick(string gatewayToken)
        {
            lock (tickLocker)
            {
                logger.LogInformation($"Tick ... {++_tick}");
                // if (_tick % 10 == 0)throw new Exception("test ex");
                checkUpdates();
            }
        }

        private const string suffix = "/api/gus/v1/gateway/";
        private void checkUpdates()
        {
            string url = Helper.CombineUrl(settings.Hostname, suffix, settings.Token);
            var webClient = new WebClient();
            string bundlesArrayJson = webClient.DownloadString(url);

            UpdateInfo[] updates = null;
            try
            {
                updates = Helper.DeserializeFromJsonText<UpdateInfo[]>(bundlesArrayJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while parsing bundles! API response:{bundlesArrayJson}", ex);
            }

            ApiResult[] arr = this.installManager.CheckUpdates(updates).ToArray();
            if (arr.Length > 0)
            {
                string json = JsonConvert.SerializeObject(arr);
                string urlPost = Helper.CombineUrl(settings.Hostname, suffix, settings.Token, "result");
                string res = webClient.UploadString(urlPost, json);
            }

        }

    }
}