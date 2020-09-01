using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Download;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Install
{
    public class InstallManager : IInstallManager
    {
        private readonly ISettings settings;
        private readonly ILogger logger;
        private readonly string guaVersion;
        private readonly string installedArrFn;
        public InstallManager(ISettings settings, ILogger<InstallManager> logger)
        {
            this.settings = settings;
            this.logger = logger;
            this.guaVersion = settings.GUAVersion.ToString();
            this.installedArrFn = Path.Combine(settings.HistoryFolder, "installed.json");
        }

        private Dictionary<string, InstalledType> getInstalledTypes()
        {
            Dictionary<string, InstalledType> res = new Dictionary<string, InstalledType>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(this.installedArrFn))
            {
                InstalledType[] arr = JsonConvert.DeserializeObject<InstalledType[]>(File.ReadAllText(this.installedArrFn));
                foreach (InstalledType it in arr)
                    res.Add(it.Type, it);
            }
            return res;
        }
        private void setInstalledTypes(IEnumerable<InstalledType> types)
        {
            HashSet<InstalledType> hs = new HashSet<InstalledType>();
            foreach (var it in types)
                if (!hs.Add(it))
                    throw new Exception($"Types duplicate! {it.Type}");

            InstalledType[] arr = hs.ToArray();

            YamlManager yamlMan = new YamlManager(this.settings.YamlFileName, this.guaVersion, this.logger);
            yamlMan.UpdateConnectors(arr);

            File.WriteAllText(this.installedArrFn, JsonConvert.SerializeObject(arr));
        }

        private class pair
        {
            public InstalledType Parent;
            public InstalledItem Item;

            public pair(InstalledType parent, InstalledItem item)
            {
                Parent = parent;
                Item = item;
            }
        }
        private static object locker = new object();
        public void CheckUpdates(IEnumerable<UpdateInfo> updates)
        {
            if (updates != null)
                lock(locker)
                {
                    List<UpdateInfo> install = new List<UpdateInfo>();
                    List<pair> uninstall = new List<pair>();
                    List<pair> activate = new List<pair>();
                    List<pair> deactivate = new List<pair>();
                    Dictionary<string, Exception> errors = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        Dictionary<string, InstalledType> installed = this.getInstalledTypes();
                        Dictionary<string, UpdateInfo> dicIdUpdates = new Dictionary<string, UpdateInfo>(StringComparer.OrdinalIgnoreCase);
                        foreach (UpdateInfo u in updates)
                        {
                            if (dicIdUpdates.ContainsKey(u.Id))
                                throw new Exception($"Update list has a duplicated ID: {u.Id}");
                            dicIdUpdates.Add(u.Id, u);
                            if (!installed.ContainsKey(u.Type))
                                install.Add(u);
                            else
                            {
                                InstalledType parent = installed[u.Type];
                                InstalledItem item = parent.GetItemByID(u.Id);
                                if (item == null)
                                    install.Add(u);
                                else if (item.Version < u.Version)
                                {
                                    uninstall.Add(new pair(parent, item));
                                    install.Add(u);
                                }
                                else if (item.Status == ItemStatus.Inactive)
                                {
                                    if (item.Version == u.Version)
                                        activate.Add(new pair(parent, item));
                                    else
                                    {
                                        uninstall.Add(new pair(parent, item));
                                        install.Add(u);
                                    }
                                }
                            }
                        }
                        foreach (InstalledType parent in installed.Values)
                        {
                            if (parent.Items != null)
                                foreach (InstalledItem item in parent.Items)
                                {
                                    if (!dicIdUpdates.ContainsKey(item.Id))
                                    {
                                        if (item.Category != Category.Command)
                                            deactivate.Add(new pair(parent, item));
                                        else
                                            uninstall.Add(new pair(parent, item));
                                    }
                                    else
                                        errors.Add(item.Id, new Exception($"Id is not unique. Type:{parent.Type} Id:{item.Id}"));
                                }
                        }

                        if (install.Count > 0 || uninstall.Count > 0 || activate.Count > 0 || deactivate.Count > 0)
                        {
                            Helper.CleanDirectory(settings.TempFolder);

                            DownloadedConfig[] downloadedConfigs;
                            DownloadedPack[] downloadedPacks;
                            DownloadedCommand[] downloadedCommands;
                            downloadPll(install, errors, out downloadedConfigs, out downloadedPacks, out downloadedCommands);

                            foreach (DownloadedCommand command in downloadedCommands)
                                command.PreInstall(errors);

                            try
                            {
                                stopTbProcess();

                                uninstallPairs(uninstall, errors);
                                deactivatePairs(deactivate, errors);
                                activatePairs(activate, errors);

                                foreach (DownloadedConfig config in downloadedConfigs)
                                    config.Install(errors);
                                foreach (DownloadedPack pack in downloadedPacks)
                                    pack.Install(errors);
                                foreach (DownloadedCommand command in downloadedCommands)
                                    command.Install(errors);
                            }
                            finally
                            {
                                startTbProcess();
                                foreach (DownloadedCommand command in downloadedCommands)
                                    command.PostInstall(errors);

                                Helper.CleanDirectory(settings.TempFolder);
                            }
                        }
                    }
                    finally
                    {
                        if (errors.Count > 0)
                        {
                            foreach (string id in errors.Keys)
                                logger.LogError(errors[id], $"Id: {id} Data: {errors[id].Data}");

                        }
                        //****** call api
                    }

                    if (false) //****sil
                    {
                        // Dictionary<string, InstalledBundle> uninstallConf = new Dictionary<string, InstalledBundle>(StringComparer.OrdinalIgnoreCase);
                        // Dictionary<string, InstalledBundle> uninstallPack = new Dictionary<string, InstalledBundle>(StringComparer.OrdinalIgnoreCase);
                        // Dictionary<string, Bundle> installConf = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
                        // Dictionary<string, Bundle> installPack = new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
                        // Dictionary<string, Exception> errors = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
                        // try
                        // {
                        //     Dictionary<string, InstalledBundle> yaml = yamlMan.GetInstalled();
                        //     Dictionary<string, Bundle> dicBundles = new Dictionary<string, Bundle>();
                        //     foreach (Bundle b in bundles)
                        //     {
                        //         if (!dicBundles.ContainsKey(b.Type))
                        //         {
                        //             if (b.ConfigVersion != null && string.IsNullOrWhiteSpace(b.ConfigUrl))b.ConfigVersion = null;
                        //             if (b.PackVersion != null && string.IsNullOrWhiteSpace(b.PackUrl))b.PackVersion = null;
                        //             dicBundles.Add(b.Type, b);
                        //         }
                        //         else
                        //             errors.Add(b.Type, new Exception($"API sent duplicated entry for the type: {b.Type}"));
                        //     }

                        //     foreach (Bundle b1 in dicBundles.Values)
                        //         if (!errors.ContainsKey(b1.Type))
                        //         {
                        //             if (!yaml.ContainsKey(b1.Type))
                        //             {
                        //                 if (b1.HasConfig)
                        //                     installConf.Add(b1.Type, b1);
                        //                 if (b1.HasPack)
                        //                     installPack.Add(b1.Type, b1);
                        //             }
                        //             else
                        //             {
                        //                 InstalledBundle b0 = yaml[b1.Type];
                        //                 if (b0.ConfigVersion != b1.ConfigVersion)
                        //                 {
                        //                     if (b0.HasConfig && !b1.HasConfig)
                        //                         uninstallConf.Add(b0.Type, b0);
                        //                     else if (!b0.HasConfig && b1.HasConfig)
                        //                         installConf.Add(b1.Type, b1);
                        //                     //else if (b0.ConfigVersion < b1.ConfigVersion) {
                        //                     else
                        //                     {
                        //                         uninstallConf.Add(b0.Type, b0);
                        //                         installConf.Add(b1.Type, b1);
                        //                     }
                        //                 }
                        //                 if (b0.PackVersion != b1.PackVersion)
                        //                 {
                        //                     if (b0.HasPack && !b1.HasPack)
                        //                         uninstallPack.Add(b0.Type, b0);
                        //                     else if (!b0.HasPack && b1.HasPack)
                        //                         installPack.Add(b1.Type, b1);
                        //                     //else if (b0.PackVersion < b1.PackVersion) {
                        //                     else
                        //                     {
                        //                         uninstallPack.Add(b0.Type, b0);
                        //                         installPack.Add(b1.Type, b1);
                        //                     }
                        //                 }
                        //             }
                        //         }
                        //     if (uninstallConf.Count > 0 || installConf.Count > 0 || uninstallPack.Count > 0 || installPack.Count > 0)
                        //     {
                        //         try
                        //         {
                        //             stopTbProcess();
                        //             Helper.CleanDirectory(settings.TempFolder);
                        //             bool yamlEdited = false;
                        //             Dictionary<Bundle, string> dicFnConf = new Dictionary<Bundle, string>();
                        //             HashSet<Package> packs = new HashSet<Package>();
                        //             if (installConf.Count > 0)
                        //             {
                        //                 Parallel.ForEach(installConf.Values, (Bundle b) =>
                        //                 {
                        //                     if (!errors.ContainsKey(b.Type))
                        //                     {
                        //                         string fn = downloadConfig(b, errors);
                        //                         if (File.Exists(fn) && !errors.ContainsKey(b.Type))
                        //                             lock(dicFnConf)
                        //                             {
                        //                                 dicFnConf.Add(b, fn);
                        //                             }
                        //                     }
                        //                 });
                        //             }
                        //             if (installPack.Count > 0)
                        //             {
                        //                 Parallel.ForEach(installPack.Values, (Bundle b) =>
                        //                 {
                        //                     if (!errors.ContainsKey(b.Type))
                        //                     {
                        //                         Package p = downloadPackage(b, errors, getPackInstallDir(b.Type));
                        //                         if (p != null)
                        //                             lock(packs)
                        //                             {
                        //                                 packs.Add(p);
                        //                             }
                        //                     }
                        //                 });
                        //             }
                        //             foreach (InstalledBundle b in uninstallConf.Values)
                        //                 if (!errors.ContainsKey(b.Type))
                        //                     if (uninstallConfig(b, errors))
                        //                     {
                        //                         yaml[b.Type].ConfigVersion = null;
                        //                         yamlEdited = true;
                        //                     }
                        //             foreach (InstalledBundle b in uninstallPack.Values)
                        //                 if (!errors.ContainsKey(b.Type))
                        //                     if (uninstallPackage(b, errors))
                        //                     {
                        //                         yaml[b.Type].PackVersion = null;
                        //                         yamlEdited = true;
                        //                     }

                        //             foreach (var kv in dicFnConf)
                        //             {
                        //                 Bundle b = kv.Key;
                        //                 string fn = kv.Value;
                        //                 if (installConfig(b, fn, errors))
                        //                 {
                        //                     yamlEdited = true;
                        //                     File.Delete(fn);
                        //                     if (yaml.ContainsKey(b.Type))
                        //                     {
                        //                         InstalledBundle ib = yaml[b.Type];
                        //                         ib.ConfigVersion = b.ConfigVersion;
                        //                     }
                        //                     else
                        //                     {
                        //                         InstalledBundle ib = new InstalledBundle()
                        //                         {
                        //                             Type = b.Type,
                        //                             UpdateAgentVersion = this.updateAgentVersion,
                        //                             ConfigVersion = b.ConfigVersion
                        //                         };
                        //                         yaml.Add(b.Type, ib);
                        //                     }
                        //                 }
                        //             }
                        //             foreach (Package p in packs)
                        //             {
                        //                 if (!errors.ContainsKey(p.Type))
                        //                 {
                        //                     if (installPackage(p, errors))
                        //                     {
                        //                         yamlEdited = true;
                        //                         if (yaml.ContainsKey(p.Type))
                        //                         {
                        //                             InstalledBundle ib = yaml[p.Type];
                        //                             ib.PackVersion = p.PackVersion;
                        //                             ib.ClassName = p.ClassName;
                        //                             ib.ConnectorConfig = p.ConnectorConfig;
                        //                         }
                        //                         else
                        //                         {
                        //                             InstalledBundle ib = new InstalledBundle()
                        //                             {
                        //                                 Type = p.Type,
                        //                                 PackVersion = p.PackVersion,
                        //                                 ClassName = p.ClassName,
                        //                                 ConnectorConfig = p.ConnectorConfig,
                        //                                 UpdateAgentVersion = this.updateAgentVersion
                        //                             };
                        //                             yaml.Add(p.Type, ib);
                        //                         }
                        //                     }
                        //                 }
                        //             }

                        //             if (yamlEdited)
                        //             {
                        //                 List<string> lstRemove = new List<string>();
                        //                 foreach (InstalledBundle ib in yaml.Values)
                        //                     if (!ib.HasConfig && !ib.HasPack)
                        //                         lstRemove.Add(ib.Type);
                        //                 foreach (string type in lstRemove)
                        //                     yaml.Remove(type);

                        //                 yamlMan.SetInstalled(yaml);
                        //             }

                        //         }
                        //         finally
                        //         {
                        //             startTbProcess();
                        //         }
                        //     }
                        //     else
                        //     {

                        //     }
                        // }
                        // finally
                        // {
                        //     if (errors.Count > 0)
                        //     {
                        //         foreach (string type in errors.Keys)
                        //             logger.LogError(errors[type], $"Type: {type} Data: {errors[type].Data}");

                        //         //****** call api
                        //     }
                        //     Helper.CleanDirectory(settings.TempFolder);

                        // }
                    }
                }
        }
        private void downloadPll(List<UpdateInfo> install, Dictionary<string, Exception> errors,
            out DownloadedConfig[] downloadedConfigs,
            out DownloadedPack[] downloadedPacks,
            out DownloadedCommand[] downloadedCommands)
        {
            HashSet<string> hsConfig = new HashSet<string>();
            List<UpdateInfo> lst = new List<UpdateInfo>();
            foreach (UpdateInfo info in install)
                if (!errors.ContainsKey(info.Id))
                {
                    if (info.Category != Category.Config)
                        lst.Add(info);
                    else if (hsConfig.Add(info.Type))
                        lst.Add(info);
                    else
                        errors.Add(info.Id, new Exception($"Config is not single for Type:{info.Type} Id:{info.Id}"));
                }

            List<DownloadedConfig> configs = new List<DownloadedConfig>();
            List<DownloadedPack> packs = new List<DownloadedPack>();
            List<DownloadedCommand> commands = new List<DownloadedCommand>();

            Parallel.ForEach(lst, (UpdateInfo info) =>
            {
                try
                {
                    switch (info.Category)
                    {
                        case Category.Config:
                            {
                                DownloadedConfig c = downloadConfig(info, errors, getConfigInstallFileName(info.Type, info.Id));
                                if (c != null)
                                    lock(configs)
                                    {
                                        configs.Add(c);
                                    }
                            }
                            break;
                        case Category.Package:
                            {
                                DownloadedPack p = downloadPackage(info, errors, getPackInstallDir(info.Type, info.Id));
                                if (p != null)
                                    lock(packs)
                                    {
                                        packs.Add(p);
                                    }
                            }
                            break;
                        case Category.Command:
                            {
                                DownloadedCommand c = downloadConmand(info, errors, getCommandInstallDir(info.Type, info.Id));
                                if (c != null)
                                    lock(commands)
                                    {
                                        commands.Add(c);
                                    }
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }
                catch (Exception ex)
                {
                    lock(errors)
                    {
                        errors.Add(info.Id, ex);
                    }
                }
            });

            downloadedConfigs = configs.ToArray();
            downloadedPacks = packs.ToArray();
            downloadedCommands = commands.ToArray();
        }
        private void uninstallPairs(List<pair> lst, Dictionary<string, Exception> errors)
        {
            foreach (pair p in lst)
            {
                switch (p.Item.Category)
                {
                    case Category.Config:
                        uninstallConfig(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Package:
                        uninstallPackage(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Command:
                        uninstallCommand(p.Parent.Type, p.Item.Id, errors);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
        private void activatePairs(List<pair> lst, Dictionary<string, Exception> errors)
        {
            foreach (pair p in lst)
            {
                switch (p.Item.Category)
                {
                    case Category.Config:
                        activateConfig(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Package:
                        activatePackage(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Command:
                        throw new Exception($"Commands cannot be activated! Type:{p.Parent.Type} Id:{p.Item.Id}");
                    default:
                        throw new NotImplementedException();
                }
            }
        }
        private void deactivatePairs(List<pair> lst, Dictionary<string, Exception> errors)
        {
            foreach (pair p in lst)
            {
                switch (p.Item.Category)
                {
                    case Category.Config:
                        deactivateConfig(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Package:
                        deactivatePackage(p.Parent.Type, p.Item.Id, errors);
                        break;
                    case Category.Command:
                        throw new Exception($"Commands cannot be deactivated! Type:{p.Parent.Type} Id:{p.Item.Id}");
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        #region Config
        private string getConfigDownloadFn(string type)
        {
            return Path.Combine(settings.TempFolder, string.Concat("conf_", type, ".json"));
        }
        private string downloadConfig(string type, string id, string url, Dictionary<string, Exception> errors)
        {
            try
            {
                var webClient = new WebClient();
                string fn = getConfigDownloadFn(type);
                webClient.DownloadFile(url, fn);
                return fn;
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(id, ex);
                }
                return null;
            }
        }

        private string getConfigInstallFileName(string type)
        {
            return Path.Combine(settings.ConfigFolder, string.Concat("config_", type, ".json"));
        }
        private bool installConfig(string type, string id, string sourceFn, Dictionary<string, Exception> errors)
        {
            try
            {
                string destFn = getConfigInstallFileName(type);
                if (File.Exists(destFn))
                    File.Delete(destFn);
                else
                {
                    string dir = Path.GetDirectoryName(destFn);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                logger.LogInformation($"Installing config: {destFn}");
                File.Copy(sourceFn, destFn, true);
                return true;
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(id, ex);
                }
                return false;
            }
        }
        private bool uninstallConfig(string type, string id, Dictionary<string, Exception> errors)
        {
            try
            {
                string fn = getConfigInstallFileName(type);
                logger.LogInformation($"Uninstalling config: {fn}");
                File.Delete(fn);
                return true;
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(id, ex);
                }
                return false;
            }
        }
        #endregion 

        #region Package

        private string getPackInstallDir(string type, string id)
        {
            return Path.Combine(settings.ExtensionFolder, string.Concat("GUA_", type, "_", id));
        }
        private string getPackDeactivatedDir(string type, string id)
        {
            return Path.Combine(settings.ExtensionFolder, string.Concat("_deactivated_GUA_", type, "_", id));
        }

        private string getTempPackFileName(string id)
        {
            return Path.Combine(settings.TempFolder, string.Concat("pack_", id, ".zip"));
        }
        private DownloadedPack downloadPackage(UpdateInfo info, Dictionary<string, Exception> errors, string extractDir)
        {
            try
            {
                var webClient = new WebClient();
                string fn = getTempPackFileName(info.Id);
                if (File.Exists(fn))
                    File.Delete(fn);
                logger.LogInformation($"Downloading...  url:{info.Url} target:{fn} ");
                webClient.DownloadFile(info.Url, fn);
                if (!File.Exists(fn))
                    throw new Exception($"Cannot download... url:{info.Url} target:{fn} ");
                return new DownloadedPack(fn, info, extractDir, this.settings, this.logger);
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(info.Id, ex);
                }
                return null;
            }
        }

        private bool uninstallPackage(string type, string id, Dictionary<string, Exception> errors)
        {
            try
            {
                string dir = getPackInstallDir(type, id);
                if (Directory.Exists(dir))
                {
                    logger.LogInformation($"Uninstalling pack: {dir}");

                    PackInfoJson info;
                    if (PackInfoJson.TryParse(dir, out info))
                        foreach (var copyItem in info.copy)
                        {
                            if (Directory.Exists(copyItem.destination))
                                Helper.DeleteDirectory(copyItem.destination);
                            else if (File.Exists(copyItem.destination))
                                File.Delete(copyItem.destination);
                        }

                    Helper.DeleteDirectory(dir);
                }
                return true;
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(id, ex);
                }
                return false;
            }
        }
        private bool deactivatePackage(string type, string id, Dictionary<string, Exception> errors)
        {
            try
            {
                string dir = getPackInstallDir(type, id);
                if (Directory.Exists(dir))
                {
                    string dir1 = getPackDeactivatedDir(type, id);

                    logger.LogInformation($"Deactivating pack: {dir}");

                    PackInfoJson info;
                    if (PackInfoJson.TryParse(dir, out info))
                        foreach (var copyItem in info.copy)
                        {
                            if (Directory.Exists(copyItem.destination))
                            {
                                string dir2 = Path.Combine(dir1, copyItem.source);
                                Helper.MoveDirectory(copyItem.destination, dir2, false);
                            }
                            else if (File.Exists(copyItem.destination))
                            {
                                string fn2 = Path.Combine(dir1, copyItem.source);
                                File.Move(copyItem.destination, fn2);
                            }
                        }
                    Helper.DeleteDirectory(dir);
                }
                return true;
            }
            catch (Exception ex)
            {
                lock(errors)
                {
                    errors.Add(id, ex);
                }
                return false;
            }
        }

        #endregion

        #region Process
        private void stopTbProcess()
        {
            this.logger.LogInformation("Stopping thingsboard-gateway.service!");
            runCommand("sudo systemctl stop thingsboard-gateway.service");

            string res = runCommand("sudo systemctl status thingsboard-gateway.service");
            this.logger.LogInformation($"Status: {res}");
        }
        private void startTbProcess()
        {
            this.logger.LogInformation("Starting thingsboard-gateway.service...");
            runCommand("sudo systemctl start thingsboard-gateway.service");

            string res = runCommand("sudo systemctl status thingsboard-gateway.service");
            this.logger.LogInformation($"Status: {res}");
        }
        private static string runCommand(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
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