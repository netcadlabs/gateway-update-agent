using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements.Items
{
    //********koray
    internal class CustomConfig : IItem
    {
        #region Model
        private string _UUID;
        public string UUID
        {
            get
            {
                if (string.IsNullOrEmpty(this._UUID))
                    return this.Type;
                else
                    return this._UUID;
            }
            set
            {
                this._UUID = value;
            }
        }
        public Category Category => Category.CustomConfig;
        public int Version { get; set; }
        public string URL { get; set; }
        public States State { get; set; }
        public Dictionary<string, object> YamlConnectorItems { get; set; }

        //**NDU-310
        public string app { get; set; }
        public CustomApp custom_app { get; set; }

        public string Type { get; private set; }
        public CustomConfig(string type, string ct, CustomApp cct)
        {
            this.app = ct;
            this.custom_app = cct;
            this.Type = type;
        }

        public void Save(string fileName)
        {
            Helper.SerializeToJsonFile(this, fileName);
        }

        #endregion

        #region IO
        private string _getNameForConfigFile()
        {
            return string.Concat(Helper.ReplaceInvalidFileNameChars(this.Type, "_"), "_custom.json");
        }
        private string getConfFileName(ISettings stt)
        {
            string configFolder = stt.GetConfigFolder(this.app, this.custom_app);
            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);
            return Path.Combine(configFolder, _getNameForConfigFile());
        }
        private string getDownloadFileName(ISettings stt)
        {
            return Path.Combine(getDownloadDir(stt), "custom_config.json");
        }
        private string getDownloadDir(ISettings stt)
        {
            string dir = Path.Combine(stt.HistoryFolder, "_config_downloads", Helper.ReplaceInvalidPathChars(string.Concat(this.Type, "_", this.UUID), "_"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
        #endregion

        #region Download

        public IEnumerable<UpdateResult> DownloadIfRequired(IModule parent, ISettings stt, ILogger logger)
        {
            if (this.State == States.DownloadRequired)
            {
                string fn = getDownloadFileName(stt);
                if (File.Exists(fn))
                    File.Delete(fn);

                logger.LogInformation($"Downloading CustomConfig...  Type:{this.URL}");

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
                        this.DownloadIfRequired(parent, stt, logger);
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
                }
            }
        }

        private void install(ISettings stt)
        {
            this.deactivate(stt);
            string dFn = this.getDownloadFileName(stt);
            if (!File.Exists(dFn))
                throw new Exception($"Downloaded file does not exist: {dFn}");
            else
            {
                string configFn = dFn;
                if (!File.Exists(configFn))
                    throw new Exception($"custom_config.json file cannot be found! url:{this.URL}");

                string name = _getNameForConfigFile();
                string fn = getConfFileName(stt);
                File.Copy(configFn, fn);

                if (this.YamlConnectorItems == null)
                    this.YamlConnectorItems = new Dictionary<string, object>();
                this.YamlConnectorItems["custom_configuration"] = name;
            }
        }
        private void deactivate(ISettings stt)
        {
            string fn = getConfFileName(stt);
            if (File.Exists(fn))
                File.Delete(fn);
        }
        private void uninstall(ISettings stt)
        {
            this.deactivate(stt);
            string fn = getDownloadFileName(stt);
            if (File.Exists(fn))
                File.Delete(fn);
        }
        private void activate(IModule parent, ISettings stt, ILogger logger)
        {
            string fn = getDownloadFileName(stt);
            if (!File.Exists(fn))
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

    }
}