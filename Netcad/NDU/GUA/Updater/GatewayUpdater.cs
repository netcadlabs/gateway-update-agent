using System;
using System.Collections.Generic;
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
            lock(tickLocker)
            {
                logger.LogInformation($"Tick ... {++_tick}");
                // if (_tick % 10 == 0)throw new Exception("test ex");
                checkUpdates();
            }
        }

        private const string suffix = "/api/gus/v1/gateway/";
        private const string suffix_customConfig = "/api/gus/v1/gateway/custom_config/"; //******koray
        private void checkUpdates()
        {
            List<UpdateInfo> updates = new List<UpdateInfo>();
            updates.AddRange(getUpdates(Helper.CombineUrl(settings.Hostname, suffix, settings.Token)));
            updates.AddRange(getUpdates(Helper.CombineUrl(settings.Hostname, suffix_customConfig, settings.Token)));

            UpdateResult[] arr = this.installManager.CheckUpdates(updates).ToArray();
            if (arr.Length > 0)
            {
                string json = JsonConvert.SerializeObject(arr);

                using(var wcPost = new WebClient())
                {
                    wcPost.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string urlPost = Helper.CombineUrl(settings.Hostname, suffix, settings.Token, "result");
                    string res = wcPost.UploadString(urlPost, json);
                }
            }
        }
        private IEnumerable<UpdateInfo> getUpdates(string url)
        {
            using(var wcDownload = new WebClient())
            {
                string bundlesArrayJson = wcDownload.DownloadString(url);

                // //**test
                // string bundlesArrayJson = @"
                //             [
                //                 {
                //                     'type': 'sigara',
                //                     'uuid': 'uuid-5',
                //                     'url': 'https://github.com/netcadlabs/gateway-update-agent/archive/master.zip',
                //                     'category': 'PACKAGE',
                //                     'version': 1,
                //                     'status': 2
                //                 }
                //             ]
                //             ";
                
                UpdateInfo[] updates = null;
                try
                {
                    updates = Helper.DeserializeFromJsonText<UpdateInfo[]>(bundlesArrayJson);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error while parsing bundles! API response:{bundlesArrayJson}", ex);
                }

                if (updates != null)
                    foreach (UpdateInfo ui in updates)
                    {
                        if (string.IsNullOrWhiteSpace(ui.UUID))
                            ui.UUID = ui.Type;
                        yield return ui;
                    }
            }
        }

    }
}