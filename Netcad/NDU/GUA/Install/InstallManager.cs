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
            Dictionary<string, IModule> modules = loadModules();
            try
            {
                HashSet<IModule> dirty = new HashSet<IModule>();
                HashSet<IModule> statusChanged = new HashSet<IModule>();
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

                    IModule b;
                    if (!modules.TryGetValue(u.Type, out b))
                    {
                        string dir = Path.Combine(this.bundlesFolder, u.Type);
                        b = ModuleFactory.CreateModule();
                        b.Type = u.Type;
                        b.GUAVersion = this.guaVersion;
                        b.App = u.app;
                        b.CustomApp = u.custom_app;
                        b.Status = u.status;
                        modules.Add(u.Type, b);
                    }
                    if (b.IsUpdateRequired(u))
                        dirty.Add(b);
                    else if (b.Status != u.status)
                    {
                        statusChanged.Add(b);
                        b.Status = u.status;
                    }
                }
                foreach (IModule b in modules.Values)
                    if (b.IsUninstallOrDeactivationRequired(dicIdUpdates))
                        dirty.Add(b);

                if (dirty.Count > 0)
                {
                    Parallel.ForEach(dirty, (IModule b) =>
                    {
                        b.DownloadIfRequired(this.settings, this.logger);
                    });

                    foreach (IModule b in dirty)
                        b.UpdateIfRequired(ServiceState.BeforeStop, this.settings, this.logger);

                    HashSet<string> services = new HashSet<string>();
                    try
                    {
                        foreach (IModule b in dirty)
                            foreach (string serviceName in settings.GetRestartServices(b.App, b.CustomApp))
                                if (services.Add(serviceName))
                                {
                                    logger.LogInformation($"Stopping {serviceName}!");
                                    string status = ShellHelper.StopService(serviceName);
                                    logger.LogInformation($"Status: {status}");
                                }

                        foreach (IModule b in dirty)
                            b.UpdateIfRequired(ServiceState.Stopped, this.settings, this.logger);

                        YamlManager yamlMan = new YamlManager(this.guaVersion, this.logger, this.settings);
                        yamlMan.UpdateConnectors(modules.Values.ToArray());
                    }
                    finally
                    {
                        foreach (string serviceName in services)
                        {
                            logger.LogInformation($"Starting {serviceName}...");
                            string status = ShellHelper.StartService(serviceName);
                            logger.LogInformation($"Status: {status}");
                        }
                    }

                    foreach (IModule b in dirty)
                        results.AddRange(b.UpdateIfRequired(ServiceState.Started, this.settings, this.logger));

                    if (!Directory.Exists(this.bundlesFolder))
                        Directory.CreateDirectory(this.bundlesFolder);
                    foreach (IModule b in dirty)
                    {
                        string dir = Path.Combine(this.bundlesFolder, b.Type);
                        b.Save(dir, this.guaVersion);
                    }
                }
                else if (statusChanged.Count > 0) //**NDU-340
                {
                    HashSet<string> services = new HashSet<string>();
                    try
                    {
                        foreach (IModule b in statusChanged)
                            foreach (string serviceName in settings.GetRestartServices(b.App, b.CustomApp))
                                if (services.Add(serviceName))
                                {
                                    logger.LogInformation($"Stopping {serviceName}!");
                                    string status = ShellHelper.StopService(serviceName);
                                    logger.LogInformation($"Status: {status}");
                                }

                        YamlManager yamlMan = new YamlManager(this.guaVersion, this.logger, this.settings);
                        yamlMan.UpdateConnectors(modules.Values.ToArray());
                    }
                    finally
                    {
                        foreach (string serviceName in services)
                        {
                            logger.LogInformation($"Starting {serviceName}...");
                            string status = ShellHelper.StartService(serviceName);
                            logger.LogInformation($"Status: {status}");
                        }
                    }
                    if (!Directory.Exists(this.bundlesFolder))
                        Directory.CreateDirectory(this.bundlesFolder);
                    foreach (IModule b in statusChanged)
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

            foreach (IModule b in modules.Values)
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
        private Dictionary<string, IModule> loadModules()
        {
            Dictionary<string, IModule> dic = new Dictionary<string, IModule>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(this.bundlesFolder))
            {
                foreach (string dir in Directory.EnumerateDirectories(this.bundlesFolder))
                {
                    IModule b = ModuleFactory.Load(dir);
                    dic.Add(b.Type, b);
                }
            }
            return dic;
        }
    }

}