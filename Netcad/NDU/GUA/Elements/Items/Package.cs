using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class Package : IItem
    {
        #region Model
        public string ID
        {
            get => this._lite.ID;
            set => this._lite.ID = value;
        }
        public Category Category => Category.Package;
        public int Version
        {
            get => this._lite.Version;
            set => this._lite.Version = value;
        }
        public string URL
        {
            get => this._lite.URL;
            set => this._lite.URL = value;
        }
        public States State
        {
            get => this._lite.State;
            set => this._lite.State = value;
        }

        public void Save(string fileName)
        {
            Helper.SerializeToJsonFile(this._lite, fileName);
        }

        private packageLite _lite;
        private class packageLite
        {
            public string ID { get; set; }
            public int Version { get; set; }
            public string URL { get; set; }
            public States State { get; set; }
        }

        #endregion

        #region IO
        private string _getIdForDir()
        {
            return Helper.ReplaceInvalidPathChars(this.ID, "_");
        }
        private string getExtractDir(ISettings stt)
        {
            return Path.Combine(stt.ExtensionFolder, string.Concat("Pack_", _getIdForDir()));
        }
        private string getZipFileName(ISettings stt)
        {
            string dir = Path.Combine(stt.ExtensionFolder, "_package_downloads", _getIdForDir());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string name = string.Concat("pack_", Helper.ReplaceInvalidFileNameChars(this.ID, "_"), ".zip");
            return Path.Combine(dir, name);
        }
        #endregion

        #region Download

        public void DownloadIfRequired(ISettings stt)
        {
            if (this.State == States.DownloadRequired)
            {
                string fn = getZipFileName(stt);
                if (File.Exists(fn))
                    File.Delete(fn);

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

        public void UpdateIfRequired(ServiceState ss, ISettings stt)
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
                        this.activate(stt);
                        this.State = States.Installed;
                        break;
                    case (States.DownloadRequired):
                        this.DownloadIfRequired(stt);
                        this.install(stt);
                        this.State = States.Installed;
                        break;
                }
            }
        }

        private static PackInfoJson _extract(string zipFn, string extractDir)
        {
            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);
            else
                Helper.CleanDirectory(extractDir);

            ZipFile.ExtractToDirectory(zipFn, extractDir);
            return PackInfoJson.Parse(extractDir);
        }
        private void install(ISettings stt)
        {
            string zipFn = this.getZipFileName(stt);
            if (!File.Exists(zipFn))
                throw new Exception($"Downloaded file does not exist: {zipFn}");
            else
            {
                string extractDir = getExtractDir(stt);
                PackInfoJson info = _extract(zipFn, extractDir);
                if (info.copy != null)
                {
                    foreach (CopyInfo ci in info.copy)
                    {
                        string source = Path.Combine(extractDir, ci.source);
                        if (File.Exists(source))
                        {
                            File.Copy(source, ci.destination, true);
                            File.Delete(source);
                        }
                        else if (Directory.Exists(source))
                        {
                            Helper.CopyDirectory(source, ci.destination, false);
                            Helper.DeleteDirectory(source);
                        }
                        else
                            throw new Exception($"Cannot copy a file in pack! Source: {source} Destination: {ci.destination}");
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
                    foreach (var copyItem in info.copy)
                    {
                        if (Directory.Exists(copyItem.destination))
                            Helper.DeleteDirectory(copyItem.destination);
                        else if (File.Exists(copyItem.destination))
                            File.Delete(copyItem.destination);
                    }

                Helper.DeleteDirectory(dir);
            }
        }
        private void uninstall(ISettings stt)
        {
            this.deactivate(stt);
            string zipFn = getZipFileName(stt);
            if (File.Exists(zipFn))
                File.Delete(zipFn);
        }
        private void activate(ISettings stt)
        {
            string zipFn = getZipFileName(stt);
            if (!File.Exists(zipFn))
            {
                this.State = States.DownloadRequired;
                this.DownloadIfRequired(stt);
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

            public string GetClassName()
            {
                return this.connector_config["class"].ToString();
            }
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