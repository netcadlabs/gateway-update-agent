using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GatewayUpdateAgent.Download;
using Netcad.NDU.GatewayUpdateAgent.Settings;
using Netcad.NDU.GatewayUpdateAgent.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Netcad.NDU.GatewayUpdateAgent.Install {
    public class InstallManager : IInstallManager {
        private readonly ISettings settings;
        private readonly ILogger logger;
        private YamlManager yamlMan;
        private readonly string updateAgentVersion;
        public InstallManager(ISettings settings, ILogger<InstallManager> logger) {
            this.settings = settings;
            this.logger = logger;
            this.updateAgentVersion = settings.UpdateAgentVersion.ToString();
            this.yamlMan = new YamlManager(this.settings.YamlFileName, this.updateAgentVersion, logger);
        }

        private static object locker = new object();
        public void CheckUpdates(IEnumerable<Bundle> bundles) {
            if (bundles != null)
                lock(locker) {
                    Dictionary<string, InstalledBundle> uninstallConf = new Dictionary<string, InstalledBundle>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, InstalledBundle> uninstallPack = new Dictionary<string, InstalledBundle>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, Bundle> installConf = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, Bundle> installPack = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, Exception> errors = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
                    try {
                        Dictionary<string, InstalledBundle> yaml = yamlMan.GetInstalled();
                        Dictionary<string, Bundle> dicBundles = new Dictionary<string, Bundle>();
                        foreach (Bundle b in bundles) {
                            if (!dicBundles.ContainsKey(b.Type)) {
                                if (b.ConfigVersion != null && string.IsNullOrWhiteSpace(b.ConfigUrl))b.ConfigVersion = null;
                                if (b.PackVersion != null && string.IsNullOrWhiteSpace(b.PackUrl))b.PackVersion = null;
                                dicBundles.Add(b.Type, b);
                            } else
                                errors.Add(b.Type, new Exception($"API sent duplicated entry for the type: {b.Type}"));
                        }

                        foreach (Bundle b1 in dicBundles.Values)
                            if (!errors.ContainsKey(b1.Type)) {
                                if (!yaml.ContainsKey(b1.Type)) {
                                    if (b1.HasConfig)
                                        installConf.Add(b1.Type, b1);
                                    if (b1.HasPack)
                                        installPack.Add(b1.Type, b1);
                                } else {
                                    InstalledBundle b0 = yaml[b1.Type];
                                    if (b0.ConfigVersion != b1.ConfigVersion) {
                                        if (b0.HasConfig && !b1.HasConfig)
                                            uninstallConf.Add(b0.Type, b0);
                                        else if (!b0.HasConfig && b1.HasConfig)
                                            installConf.Add(b1.Type, b1);
                                        //else if (b0.ConfigVersion < b1.ConfigVersion) {
                                        else {
                                            uninstallConf.Add(b0.Type, b0);
                                            installConf.Add(b1.Type, b1);
                                        }
                                    }
                                    if (b0.PackVersion != b1.PackVersion) {
                                        if (b0.HasPack && !b1.HasPack)
                                            uninstallPack.Add(b0.Type, b0);
                                        else if (!b0.HasPack && b1.HasPack)
                                            installPack.Add(b1.Type, b1);
                                        //else if (b0.PackVersion < b1.PackVersion) {
                                        else {
                                            uninstallPack.Add(b0.Type, b0);
                                            installPack.Add(b1.Type, b1);
                                        }
                                    }
                                }
                            }
                        if (uninstallConf.Count > 0 || installConf.Count > 0 || uninstallPack.Count > 0 || installPack.Count > 0) {
                            try {
                                stopTbProcess();
                                Helper.CleanDirectory(settings.TempFolder);
                                bool yamlEdited = false;
                                Dictionary<Bundle, string> dicFnConf = new Dictionary<Bundle, string>();
                                HashSet<Package> packs = new HashSet<Package>();
                                if (installConf.Count > 0) {
                                    Parallel.ForEach(installConf.Values, (Bundle b) => {
                                        if (!errors.ContainsKey(b.Type)) {
                                            string fn = downloadConfig(b, errors);
                                            if (File.Exists(fn) && !errors.ContainsKey(b.Type))
                                                lock(dicFnConf) {
                                                    dicFnConf.Add(b, fn);
                                                }
                                        }
                                    });
                                }
                                if (installPack.Count > 0) {
                                    Parallel.ForEach(installPack.Values, (Bundle b) => {
                                        if (!errors.ContainsKey(b.Type)) {
                                            Package p = downloadPackage(b, errors, getPackInstallDir(b.Type));
                                            if (p != null)
                                                lock(packs) {
                                                    packs.Add(p);
                                                }
                                        }
                                    });
                                }
                                foreach (InstalledBundle b in uninstallConf.Values)
                                    if (!errors.ContainsKey(b.Type))
                                        if (uninstallConfig(b, errors)) {
                                            yaml[b.Type].ConfigVersion = null;
                                            yamlEdited = true;
                                        }
                                foreach (InstalledBundle b in uninstallPack.Values)
                                    if (!errors.ContainsKey(b.Type))
                                        if (uninstallPackage(b, errors)) {
                                            yaml[b.Type].PackVersion = null;
                                            yamlEdited = true;
                                        }

                                foreach (var kv in dicFnConf) {
                                    Bundle b = kv.Key;
                                    string fn = kv.Value;
                                    if (installConfig(b, fn, errors)) {
                                        yamlEdited = true;
                                        File.Delete(fn);
                                        if (yaml.ContainsKey(b.Type)) {
                                            InstalledBundle ib = yaml[b.Type];
                                            ib.ConfigVersion = b.ConfigVersion;
                                        } else {
                                            InstalledBundle ib = new InstalledBundle() {
                                                Type = b.Type,
                                                UpdateAgentVersion = this.updateAgentVersion,
                                                ConfigVersion = b.ConfigVersion
                                            };
                                            yaml.Add(b.Type, ib);
                                        }
                                    }
                                }
                                foreach (Package p in packs) {
                                    if (!errors.ContainsKey(p.Type)) {
                                        if (installPackage(p, errors)) {
                                            yamlEdited = true;
                                            if (yaml.ContainsKey(p.Type)) {
                                                InstalledBundle ib = yaml[p.Type];
                                                ib.PackVersion = p.PackVersion;
                                                ib.ClassName = p.ClassName;
                                                ib.ConnectorConfig = p.ConnectorConfig;
                                            } else {
                                                InstalledBundle ib = new InstalledBundle() {
                                                    Type = p.Type,
                                                    PackVersion = p.PackVersion,
                                                    ClassName = p.ClassName,
                                                    ConnectorConfig = p.ConnectorConfig,
                                                    UpdateAgentVersion = this.updateAgentVersion
                                                };
                                                yaml.Add(p.Type, ib);
                                            }
                                        }
                                    }
                                }

                                if (yamlEdited) {
                                    List<string> lstRemove = new List<string>();
                                    foreach (InstalledBundle ib in yaml.Values)
                                        if (!ib.HasConfig && !ib.HasPack)
                                            lstRemove.Add(ib.Type);
                                    foreach (string type in lstRemove)
                                        yaml.Remove(type);

                                    yamlMan.SetInstalled(yaml);
                                }

                            } finally {
                                startTbProcess();
                            }
                        } else {

                        }
                    } finally {
                        if (errors.Count > 0) {
                            foreach (string type in errors.Keys)
                                logger.LogError(errors[type], $"Type: {type} Data: {errors[type].Data}");

                            //****** call api
                        }
                        Helper.CleanDirectory(settings.TempFolder);

                    }
                }
        }

        #region download

        private string getTempConfFileName(string type) {
            return Path.Combine(settings.TempFolder, string.Concat("conf_", type, ".json"));
        }
        private string downloadConfig(Bundle b, Dictionary<string, Exception> errors) {
            try {
                var webClient = new WebClient();
                string fn = getTempConfFileName(b.Type);
                webClient.DownloadFile(b.ConfigUrl, fn);
                return fn;
            } catch (Exception ex) {
                lock(errors)
                errors.Add(b.Type, ex);
                return null;
            }
        }

        private string getTempPackFileName(string type) {
            return Path.Combine(settings.TempFolder, string.Concat("pack_", type, ".zip"));
        }
        private Package downloadPackage(Bundle b, Dictionary<string, Exception> errors, string extractDir) {
            try {
                var webClient = new WebClient();
                string fn = getTempPackFileName(b.Type);
                if (File.Exists(fn))
                    File.Delete(fn);
                logger.LogInformation($"Downloading...  url:{b.PackUrl} target:{fn} ");
                webClient.DownloadFile(b.PackUrl, fn);
                if (!File.Exists(fn))
                    throw new Exception($"Cannot download... url:{b.PackUrl} target:{fn} ");
                return new Package(fn, b.Type, extractDir, Helper.ParseVersion(b.PackVersion), this.settings, this.logger);
            } catch (Exception ex) {
                lock(errors) {
                    errors.Add(b.Type, ex);
                }
                return null;
            }
        }
        #endregion
        private string getConfigInstallFileName(string type) {
            return Path.Combine(settings.ConfigFolder, string.Concat("config_", type, ".json"));
        }
        private string getPackInstallDir(string type) {
            return Path.Combine(settings.ExtensionFolder, string.Concat("GUA_", type));
        }
        private bool uninstallConfig(InstalledBundle b, Dictionary<string, Exception> errors) {
            try {
                string fn = getConfigInstallFileName(b.Type);
                logger.LogInformation($"Uninstalling config: {fn}");
                File.Delete(fn);
                return true;
            } catch (Exception ex) {
                lock(errors) {
                    errors.Add(b.Type, ex);
                }
                return false;
            }
        }
        private bool uninstallPackage(InstalledBundle b, Dictionary<string, Exception> errors) {
            try {
                string dir = getPackInstallDir(b.Type);
                if (Directory.Exists(dir)) {
                    logger.LogInformation($"Uninstalling pack: {dir}");

                    PackInfoJson info;
                    if (PackInfoJson.TryParse(dir, out info))
                        foreach (var copyItem in info.copy) {
                            if (Directory.Exists(copyItem.destination))
                                Helper.DeleteDirectory(copyItem.destination);
                            else if (File.Exists(copyItem.destination))
                                File.Delete(copyItem.destination);
                        }

                    Helper.DeleteDirectory(dir);
                }
                return true;
            } catch (Exception ex) {
                lock(errors) {
                    errors.Add(b.Type, ex);
                }
                return false;
            }
        }

        private bool installConfig(Bundle b, string sourceFn, Dictionary<string, Exception> errors) {
            try {
                string destFn = getConfigInstallFileName(b.Type);
                if (File.Exists(destFn))
                    File.Delete(destFn);
                else {
                    string dir = Path.GetDirectoryName(destFn);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                logger.LogInformation($"Installing config: {destFn}");
                File.Copy(sourceFn, destFn, true);
                return true;
            } catch (Exception ex) {
                errors.Add(b.Type, ex);
                return false;
            }
        }
        private bool installPackage(Package p, Dictionary<string, Exception> errors) {
            try {
                if (p.Install(errors)) {
                    return true;
                }
            } catch (Exception ex) {
                errors.Add(p.Type, ex);
            }
            return false;
        }

        #region Process
        private void stopTbProcess() {
            this.logger.LogInformation("Stopping thingsboard-gateway.service!");
            runCommand("sudo systemctl stop thingsboard-gateway.service");

            string res = runCommand("sudo systemctl status thingsboard-gateway.service");
            this.logger.LogInformation($"Status: {res}");
        }
        private void startTbProcess() {
            this.logger.LogInformation("Starting thingsboard-gateway.service...");
            runCommand("sudo systemctl start thingsboard-gateway.service");

            string res = runCommand("sudo systemctl status thingsboard-gateway.service");
            this.logger.LogInformation($"Status: {res}");
        }
        private static string runCommand(string cmd) {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process() {
                StartInfo = new ProcessStartInfo {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        #endregion

    }
}