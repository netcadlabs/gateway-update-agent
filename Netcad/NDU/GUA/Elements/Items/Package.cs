using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class Package : IItem
    {
        #region Model      
        public string UUID { get; set; }
        public Category Category => Category.Package;
        public int Version { get; set; }
        public string URL { get; set; }
        public States State { get; set; }
        public Dictionary<string, object> YamlConnectorItems { get; set; }
        public List<string> CustomCopiedFiles = new List<string>();

        //**NDU-310
        public string config_type { get; set; }
        public CustomConfigType custom_config_type { get; set; }

        public Package(string ct, CustomConfigType cct)
        {
            this.config_type = ct;
            this.custom_config_type = cct;
        }

        public void Save(string fileName)
        {
            Helper.SerializeToJsonFile(this, fileName);
        }

        #endregion

        #region IO
        private string _getIdForDir()
        {
            return Helper.ReplaceInvalidPathChars(this.UUID, "_");
        }
        private string getExtractDir(ISettings stt)
        {
            return Path.Combine(stt.GetExtensionFolder(this.config_type, this.custom_config_type), string.Concat("Pack_", _getIdForDir()));
        }
        private string getZipFileName(ISettings stt)
        {
            string dir = Path.Combine(stt.HistoryFolder, "_package_downloads", _getIdForDir());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string name = string.Concat("pack_", Helper.ReplaceInvalidFileNameChars(this.UUID, "_"), ".zip");
            return Path.Combine(dir, name);
        }
        #endregion

        #region Download

        public IEnumerable<UpdateResult> DownloadIfRequired(IModule parent, ISettings stt, ILogger logger)
        {
            if (this.State == States.DownloadRequired)
            {
                string fn = getZipFileName(stt);
                if (File.Exists(fn))
                    File.Delete(fn);

                logger.LogInformation($"Downloading Package...  Type:{this.URL}");

                using(var webClient = new WebClient())
                webClient.DownloadFile(this.URL, fn);

                if (!File.Exists(fn))
                    throw new Exception($"Cannot download... url:{this.URL} target:{fn} ");
                else
                {
                    this.State = States.Downloaded;
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Downloaded,
                            InstallLog = $"State: {this.State}"
                    };
                }
            }
        }

        #endregion

        #region Update

        public IEnumerable<UpdateResult> UpdateIfRequired(IModule parent, ServiceState ss, ISettings stt, ILogger logger)
        {
            if (ss == ServiceState.Stopped)
            {
                switch (this.State)
                {
                    case (States.Downloaded):
                        this.install(stt);
                        this.State = States.Installed;
                        yield return new UpdateResult()
                        {
                            Type = parent.Type,
                                UUID = this.UUID,
                                State = UpdateResultState.Installed,
                                InstallLog = $"State: {this.State}"
                        };
                        break;
                    case (States.UninstallRequired):
                        this.uninstall(stt);
                        this.State = States.Uninstalled;
                        yield return new UpdateResult()
                        {
                            Type = parent.Type,
                                UUID = this.UUID,
                                State = UpdateResultState.Uninstalled,
                                InstallLog = $"State: {this.State}"
                        };
                        break;
                    case (States.DeactivateRequired):
                        this.deactivate(stt);
                        this.State = States.Deactivated;
                        yield return new UpdateResult()
                        {
                            Type = parent.Type,
                                UUID = this.UUID,
                                State = UpdateResultState.Uninstalled,
                                InstallLog = $"State: {this.State}"
                        };
                        break;
                    case (States.ActivateRequired):
                        this.activate(parent, stt, logger);
                        this.State = States.Installed;
                        yield return new UpdateResult()
                        {
                            Type = parent.Type,
                                UUID = this.UUID,
                                State = UpdateResultState.Installed,
                                InstallLog = $"State: {this.State}"
                        };
                        break;
                    case (States.DownloadRequired):
                        foreach (UpdateResult ur in this.DownloadIfRequired(parent, stt, logger))
                            yield return ur;
                        this.install(stt);
                        this.State = States.Installed;
                        yield return new UpdateResult()
                        {
                            Type = parent.Type,
                                UUID = this.UUID,
                                State = UpdateResultState.Installed,
                                InstallLog = $"State: {this.State}"
                        };
                        break;;
                }
            }
        }

        private static PackInfoJson _extract(string zipFn, string extractDir)
        {
            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);
            else
                Helper.CleanDir(extractDir);

            ZipFile.ExtractToDirectory(zipFn, extractDir);
            return PackInfoJson.Parse(extractDir);
        }
        private void install(ISettings stt)
        {
            this.deactivate(stt);
            string zipFn = this.getZipFileName(stt);
            if (!File.Exists(zipFn))
                throw new Exception($"Downloaded file does not exist: {zipFn}");
            else
            {
                string extractDir = getExtractDir(stt);
                PackInfoJson info = _extract(zipFn, extractDir);
                this.YamlConnectorItems = info.connector_config;

                //**NDU-317 - 2
                if (this.YamlConnectorItems == null)
                    this.YamlConnectorItems = new Dictionary<string, object>();
                const string keyUuids = "uuids";
                List<string> lstUuid = null;
                if (this.YamlConnectorItems.ContainsKey(keyUuids))
                    lstUuid = this.YamlConnectorItems[keyUuids] as List<string>;
                if (lstUuid == null)
                    lstUuid = new List<string>();
                if (!lstUuid.Contains(this.UUID))
                    lstUuid.Add(this.UUID);
                this.YamlConnectorItems[keyUuids] = lstUuid;
                //**

                if (info.copy != null)
                {
                    foreach (CopyInfo ci in info.copy)
                    {
                        string dest = ci.destination;
#if DEBUG
                        dest = string.Concat("/tmp/Netcad/NDU/GUA_test/custom_copy", dest);
#endif
                        string source = Path.Combine(extractDir, ci.source);
                        if (File.Exists(source))
                        {
                            File.Copy(source, dest, true);
                            File.Delete(source);
                            this.CustomCopiedFiles.Add(dest);
                        }
                        else if (Directory.Exists(source))
                        {
                            this.CustomCopiedFiles.AddRange(Helper.CopyDir(source, dest, false));
                            Helper.DeleteDir(source);
                        }
                        else
                            throw new Exception($"Cannot copy a file in pack! Source: {source} Destination: {dest}");
                    }
                }
            }
        }
        private void deactivate(ISettings stt)
        {
            string dir = getExtractDir(stt);
            if (Directory.Exists(dir))
            {
                PackInfoJson info;
                if (PackInfoJson.TryParse(dir, out info))
                {
                    foreach (var ci in info.copy)
                    {
                        string dest = ci.destination;
#if DEBUG
                        dest = string.Concat("/tmp/Netcad/NDU/GUA_test/custom_copy", dest);
#endif
                        foreach (string fn in this.CustomCopiedFiles)
                            File.Delete(fn);
                        this.CustomCopiedFiles.Clear();
                    }
                }
                Helper.DeleteDir(dir);
            }
        }
        private void uninstall(ISettings stt)
        {
            this.deactivate(stt);
            string zipFn = getZipFileName(stt);
            if (File.Exists(zipFn))
                File.Delete(zipFn);
        }
        private void activate(IModule parent, ISettings stt, ILogger logger)
        {
            string zipFn = getZipFileName(stt);
            if (!File.Exists(zipFn))
            {
                this.State = States.DownloadRequired;
                this.DownloadIfRequired(parent, stt, logger);
                this.install(stt);
            }
            else
            {
                this.install(stt);
            }
        }

        #endregion

        #region info.json
        private class PackInfoJson
        {
            public CopyInfo[] copy { get; set; }
            public Dictionary<string, object> connector_config { get; set; }

            private static string getInfoJsonFn(string extractDir)
            {
                return Path.Combine(extractDir, "info.json");
            }
            public static bool TryParse(string extractDir, out PackInfoJson p)
            {
                string infoJsonFn = getInfoJsonFn(extractDir);
                if (!File.Exists(infoJsonFn))
                {
                    p = null;
                    return false;
                }
                else
                {
                    try
                    {
                        p = Helper.DeserializeFromJsonFile<PackInfoJson>(infoJsonFn);
                        return p != null;
                    }
                    catch
                    {
                        p = null;
                        return false;
                    }
                }
            }
            public static PackInfoJson Parse(string extractDir)
            {
                string infoJsonFn = getInfoJsonFn(extractDir);
                if (!File.Exists(infoJsonFn))
                    throw new Exception($"Cannot find info.json file. {extractDir}");
                    
                return Helper.DeserializeFromJsonFile<PackInfoJson>(infoJsonFn);
            }
        }
        internal class CopyInfo
        {
            public string source { get; set; }
            public string destination { get; set; }
        }
        #endregion

    }
}