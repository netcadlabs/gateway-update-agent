using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

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

        public void CheckUpdates(IEnumerable<UpdateInfo> updates)
        {
            List<Exception> errors = new List<Exception>();
            Dictionary<string, Bundle> bundles = loadBundles();
            try
            {
                HashSet<Bundle> dirty = new HashSet<Bundle>();
                Dictionary<string, UpdateInfo> dicIdUpdates = new Dictionary<string, UpdateInfo>();
                foreach (UpdateInfo u in updates)
                {
                    if (dicIdUpdates.ContainsKey(u.ID))
                    {
                        errors.Add(new Exception($"Update item has a duplicated ID. Type: {u.Type} ID: {u.ID}"));
                        continue;
                    }
                    else
                        dicIdUpdates.Add(u.ID, u);

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
                        b.DownloadIfRequired(this.settings);
                    });

                    foreach (Bundle b in dirty)
                        b.UpdateIfRequired(ServiceState.BeforeStop, this.settings);

                    try
                    {
                        Shell.StopTbProcess(this.logger);

                        foreach (Bundle b in dirty)
                            b.UpdateIfRequired(ServiceState.Stopped, this.settings);

                        YamlManager yamlMan = new YamlManager(this.settings.YamlFileName, this.guaVersion, this.logger);
                        yamlMan.UpdateConnectors(bundles.Values.ToArray());
                    }
                    finally
                    {
                        Shell.StartTbProcess(this.logger);
                    }

                    foreach (Bundle b in dirty)
                        b.UpdateIfRequired(ServiceState.Started, this.settings);

                    if (!Directory.Exists(this.bundlesFolder))
                        Directory.CreateDirectory(this.bundlesFolder);
                    foreach (Bundle b in dirty)
                    {
                        string dir = Path.Combine(this.bundlesFolder, b.Type);
                        b.Save(dir, this.guaVersion);
                    }

                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            foreach (Bundle b in bundles.Values)
                errors.AddRange(b.GetErrors());

            if (errors.Count > 0)
            {
                //**** call api
            }

        }

        private Dictionary<string, Bundle> loadBundles()
        {
            Dictionary<string, Bundle> dic = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(this.bundlesFolder))
            {
                foreach (string dir in Directory.EnumerateDirectories(this.bundlesFolder))
                {
                    string type = Path.GetDirectoryName(dir);
                    dic.Add(type, Bundle.Load(dir));
                }
            }
            return dic;
        }
    }

}