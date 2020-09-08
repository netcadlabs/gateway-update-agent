using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class Command : IItem
    {
        #region Model
        public string UUID { get; set; }
        public Category Category => Category.Command;
        public int Version { get; set; }
        public string URL { get; set; }
        public States State { get; set; }
        public Dictionary<string, object> YamlConnectorItems { get; set; }

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
            return Path.Combine(stt.ExtensionFolder, string.Concat("Comm_", _getIdForDir()));
        }
        private string getZipFileName(ISettings stt)
        {
            string dir = Path.Combine(stt.HistoryFolder, "_command_downloads", _getIdForDir());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string name = string.Concat("command_", Helper.ReplaceInvalidFileNameChars(this.UUID, "_"), ".zip");
            return Path.Combine(dir, name);
        }
        private string getPreInstallShFileName(ISettings stt)
        {
            return Path.Combine(this.getExtractDir(stt), "pre_install.sh");
        }
        private string getInstallShFileName(ISettings stt)
        {
            return Path.Combine(this.getExtractDir(stt), "install.sh");
        }
        private string getPostInstallShFileName(ISettings stt)
        {
            return Path.Combine(this.getExtractDir(stt), "post_install.sh");
        }
        private string getUninstallShFileName(ISettings stt)
        {
            return Path.Combine(this.getExtractDir(stt), "uninstall.sh");
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

                logger.LogInformation($"Downloading Command...  Type:{this.URL}");

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
            switch (this.State)
            {
                case (States.Downloaded):
                    this.install(ss, stt);
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Installed,
                            InstallLog = commandUpdateResultLog.CreateJson(ss, this.State)
                    };
                    break;

                case (States.UninstallRequired):
                    this.uninstall(ss, stt);
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Uninstalled,
                            InstallLog = commandUpdateResultLog.CreateJson(ss, this.State)
                    };
                    break;
                case (States.DeactivateRequired):
                    this.deactivate(ss, stt);
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Uninstalled,
                            InstallLog = commandUpdateResultLog.CreateJson(ss, this.State)
                    };
                    break;

                case (States.ActivateRequired):
                    this.activate(ss, stt);
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Installed,
                            InstallLog = commandUpdateResultLog.CreateJson(ss, this.State)
                    };
                    break;
                case (States.DownloadRequired):
                    this.DownloadIfRequired(parent, stt, logger);
                    this.State = States.Downloaded;
                    this.install(ss, stt);
                    yield return new UpdateResult()
                    {
                        Type = parent.Type,
                            UUID = this.UUID,
                            State = UpdateResultState.Installed,
                            InstallLog = commandUpdateResultLog.CreateJson(ss, this.State)
                    };
                    break;

            }
        }
        private class commandUpdateResultLog
        {
            [JsonProperty("tbservice_state")]
            public string ServiceState { get; set; }

            [JsonProperty("item_state")]
            public string ItemState { get; set; }

            public static string CreateJson(ServiceState ss, States itemState)
            {
                return JsonConvert.SerializeObject(
                    new commandUpdateResultLog()
                    {
                        ServiceState = ss.ToString(),
                            ItemState = itemState.ToString()
                    });
            }
        }

        private void install(ServiceState ss, ISettings stt)
        {
            switch (ss)
            {
                case (ServiceState.BeforeStop):
                    this.extract(stt);
                    ShellHelper.RunFile(getPreInstallShFileName(stt));
                    break;
                case (ServiceState.Stopped):
                    ShellHelper.RunFile(getInstallShFileName(stt));
                    break;
                case (ServiceState.Started):
                    ShellHelper.RunFile(getPostInstallShFileName(stt));
                    this.State = States.Installed;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        private void activate(ServiceState ss, ISettings stt)
        {
            switch (ss)
            {
                case (ServiceState.BeforeStop):
                    this.extract(stt);
                    ShellHelper.RunFile(getPreInstallShFileName(stt));
                    break;
                case (ServiceState.Stopped):
                    ShellHelper.RunFile(getInstallShFileName(stt));
                    break;
                case (ServiceState.Started):
                    ShellHelper.RunFile(getPostInstallShFileName(stt));
                    this.State = States.Installed;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void uninstall(ServiceState ss, ISettings stt)
        {
            if (ss == ServiceState.Stopped)
            {
                deactivate(ss, stt);

                string zipFn = getZipFileName(stt);
                if (File.Exists(zipFn))
                    File.Delete(zipFn);

                this.State = States.Uninstalled;
            }
        }
        private void deactivate(ServiceState ss, ISettings stt)
        {
            if (ss == ServiceState.Stopped)
            {
                ShellHelper.RunFile(getUninstallShFileName(stt));

                string extrDir = this.getExtractDir(stt);
                if (Directory.Exists(extrDir))
                    Helper.DeleteDir(extrDir);

                this.State = States.Deactivated;
            }
        }

        private static void _extract(string zipFn, string extractDir)
        {
            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);
            else
                Helper.CleanDir(extractDir);

            ZipFile.ExtractToDirectory(zipFn, extractDir);
        }
        private void extract(ISettings stt)
        {
            string zipFn = this.getZipFileName(stt);
            if (!File.Exists(zipFn))
                throw new Exception($"Downloaded file does not exist: {zipFn}");
            else
            {
                string extractDir = getExtractDir(stt);
                _extract(zipFn, extractDir);
            }
        }

        #endregion

    }
}