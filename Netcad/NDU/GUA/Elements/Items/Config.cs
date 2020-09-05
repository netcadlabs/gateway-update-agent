using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class Config : IItem
    {
        #region Model
        public string ID { get; set; }
        public Category Category => Category.Config;
        public int Version { get; set; }
        public string URL { get; set; }
        public States State { get; set; }
        public Dictionary<string, object> YamlConnectorItems { get; set; }

        public string Type { get; private set; }
        public Config(string type)
        {
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
            return string.Concat(Helper.ReplaceInvalidFileNameChars(this.Type, "_"), ".json");
        }
        private string getConfFileName(ISettings stt)
        {
            if (!Directory.Exists(stt.ConfigFolder))
                Directory.CreateDirectory(stt.ConfigFolder);
            return Path.Combine(stt.ConfigFolder, _getNameForConfigFile());
        }
        private string getDownloadFileName(ISettings stt)
        {
            string dir = Path.Combine(stt.HistoryFolder, "_config_downloads", Helper.ReplaceInvalidPathChars(string.Concat(this.Type, "_", this.ID), "_"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, _getNameForConfigFile());
        }
        #endregion

        #region Download

        public void DownloadIfRequired(ISettings stt, ILogger logger)
        {
            if (this.State == States.DownloadRequired)
            {
                string fn = getDownloadFileName(stt);
                if (File.Exists(fn))
                    File.Delete(fn);

                logger.LogInformation($"Downloading Config...  Type:{this.URL}");

                var webClient = new WebClient();
                webClient.DownloadFile(this.URL, fn);

                if (!File.Exists(fn))
                    throw new Exception($"Cannot download... url:{this.URL} target:{fn} ");
                else
                    this.State = States.Downloaded;
            }
        }

        #endregion

        #region Update

        public void UpdateIfRequired(ServiceState ss, ISettings stt, ILogger logger)
        {
            if (ss == ServiceState.Stopped)
            {
                switch (this.State)
                {
                    case (States.Downloaded):
                        this.install(stt);
                        this.State = States.Installed;
                        break;
                    case (States.UninstallRequired):
                        this.uninstall(stt);
                        this.State = States.Uninstalled;
                        break;
                    case (States.DeactivateRequired):
                        this.deactivate(stt);
                        this.State = States.Deactivated;
                        break;
                    case (States.ActivateRequired):
                        this.activate(stt, logger);
                        this.State = States.Installed;
                        break;
                    case (States.DownloadRequired):
                        this.DownloadIfRequired(stt, logger);
                        this.install(stt);
                        this.State = States.Installed;
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
                string name = _getNameForConfigFile();
                string fn = getConfFileName(stt);
                File.Copy(dFn, fn);

                this.YamlConnectorItems = new Dictionary<string, object>();
                this.YamlConnectorItems.Add("configuration", name);
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
        private void activate(ISettings stt, ILogger logger)
        {
            string fn = getDownloadFileName(stt);
            if (!File.Exists(fn))
            {
                this.State = States.DownloadRequired;
                this.DownloadIfRequired(stt, logger);
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