using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Updater;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Install
{
    internal class InstallManager : IInstallManager
    {
        private readonly ISettings settings;
        private readonly ILogger logger;
        private readonly string guaVersion;
        private readonly string bundlesFolder;
        public InstallManager(ISettings settings, ILogger<InstallManager> logger)
        {
            this.settings = settings;
            this.logger = logger;
            this.guaVersion = settings.GUAVersion.ToString();
            this.bundlesFolder = Path.Combine(settings.HistoryFolder, "Bundles");
            if (!Directory.Exists(this.bundlesFolder))
                Directory.CreateDirectory(this.bundlesFolder);
        }

        private string validateDownloadUrl(string url)
        {
            Uri uriResult;
            if (Uri.TryCreate(url, UriKind.Absolute, out uriResult))
            {
                if ((uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                    return url;
                else
                    throw new NotImplementedException();
            }
            else
            {
                return Helper.CombineUrl(this.settings.Hostname, "/api/gus/downloads/", url);
            }
        }
        public IEnumerable<UpdateResult> CheckUpdates(IEnumerable<UpdateInfo> updates)
        {
            List<GUAException> errors = new List<GUAException>();
            List<UpdateResult> results = new List<UpdateResult>();
            Dictionary<string, Bundle> bundles = loadBundles();
            try
            {
                HashSet<Bundle> dirty = new HashSet<Bundle>();
                Dictionary<string, UpdateInfo> dicIdUpdates = new Dictionary<string, UpdateInfo>();
                foreach (UpdateInfo u in updates)
                {
                    if (string.IsNullOrWhiteSpace(u.Url))
                    {
                        errors.Add(new GUAException(u.Type, u.UUID, $"Update item url is null. Type: {u.Type} ID: {u.UUID}"));
                        continue;
                    }
                    u.Url = validateDownloadUrl(u.Url);
                    if (dicIdUpdates.ContainsKey(u.UUID))
                    {
                        errors.Add(new GUAException(u.Type, u.UUID, $"Update item has a duplicated ID. Type: {u.Type} ID: {u.UUID}"));
                        continue;
                    }
                    else
                        dicIdUpdates.Add(u.UUID, u);

                    Bundle b;
                    if (!bundles.TryGetValue(u.Type, out b))
                    {
                        string dir = Path.Combine(this.bundlesFolder, u.Type);
                        b = new Bundle();
                        b.Type = u.Type;
                        b.GUAVersion = this.guaVersion;
                        bundles.Add(u.Type, b);
                    }
                    if (b.IsUpdateRequired(u))
                        dirty.Add(b);
                }
                foreach (Bundle b in bundles.Values)
                    if (b.IsUninstallOrDeactivationRequired(dicIdUpdates))
                        dirty.Add(b);

                if (dirty.Count > 0)
                {
                    Parallel.ForEach(dirty, (Bundle b) =>
                    {
                        b.DownloadIfRequired(this.settings, this.logger);
                    });

                    foreach (Bundle b in dirty)
                        b.UpdateIfRequired(ServiceState.BeforeStop, this.settings, this.logger);

                    try
                    {
                        logger.LogInformation("Stopping thingsboard-gateway.service!");
                        string status = ShellHelper.StopTbProcess();
                        logger.LogInformation($"Status: {status}");

                        foreach (Bundle b in dirty)
                            b.UpdateIfRequired(ServiceState.Stopped, this.settings, this.logger);

                        YamlManager yamlMan = new YamlManager(this.settings.YamlFileName, this.guaVersion, this.logger);
                        yamlMan.UpdateConnectors(bundles.Values.ToArray());
                    }
                    finally
                    {
                        logger.LogInformation("Starting thingsboard-gateway.service...");
                        string status = ShellHelper.StartTbProcess();
                        logger.LogInformation($"Status: {status}");
                    }

                    foreach (Bundle b in dirty)
                        b.UpdateIfRequired(ServiceState.Started, this.settings, this.logger);

                    if (!Directory.Exists(this.bundlesFolder))
                        Directory.CreateDirectory(this.bundlesFolder);
                    foreach (Bundle b in dirty)
                    {
                        string dir = Path.Combine(this.bundlesFolder, b.Type);
                        b.Save(dir, this.guaVersion);
                    }

                }
            }
            catch (GUAException ge)
            {
                errors.Add(ge);
            }
            catch (Exception ex)
            {
                errors.Add(new GUAException("generic", "generic", ex.Message));
            }

            foreach (Bundle b in bundles.Values)
                errors.AddRange(b.GetErrors());

            if (errors.Count > 0)
            {
                foreach (var ex in errors)
                {
                    logger.LogError(ex, "Error in CheckUpdates");
                    results.Add(
                        new UpdateResult
                        {
                            Type = ex.Type,
                                UUID = ex.UUID,
                                State = UpdateResultState.Error,
                                InstallLog = ex.Message
                        }
                    );
                }
            }
            return results;
        }
        private Dictionary<string, Bundle> loadBundles()
        {
            Dictionary<string, Bundle> dic = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(this.bundlesFolder))
            {
                foreach (string dir in Directory.EnumerateDirectories(this.bundlesFolder))
                {
                    Bundle b = Bundle.Load(dir);
                    dic.Add(b.Type, b);
                }
            }
            return dic;
        }
    }

}