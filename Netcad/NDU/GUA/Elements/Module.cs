using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements.Items;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Elements
{
    internal class Module : IModule
    {
        private moduleLite lite;
        private Dictionary<string, IItem> items;
        public string Type { get => this.lite.Type; set => this.lite.Type = value; }
        public string GUAVersion { get => this.lite.GUAVersion; set => this.lite.GUAVersion = value; }

        public string ConfigType { get => this.lite.ConfigType; set => this.lite.ConfigType = value; }
        public CustomConfigType CustomConfigType { get => this.lite.CustomConfigType; set => this.lite.CustomConfigType = value; }
        public string GetYamlFileName(ISettings settings)
        {
            if (this.CustomConfigType != null)
                return this.CustomConfigType.YamlFileName;
            else
                return settings.GetYamlFileName(this.ConfigType, this.CustomConfigType);
        }
        public string GetYamlCollectionName(ISettings settings)
        {
            if (this.CustomConfigType != null)
                return this.CustomConfigType.YamlCollectionName;
            else
                return settings.GetYamlCollectionName(this.ConfigType, this.CustomConfigType);
        }

        public Dictionary<string, object> GetYamlCustomProperties()
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            if (this.items != null)
                foreach (IItem item in this.items.Values)
                {
                    Dictionary<string, object> d = item.YamlConnectorItems;
                    if (d != null)
                        foreach (string key in d.Keys)
                        {
                            if (!dic.ContainsKey(key))
                                dic.Add(key, d[key]);
                            else
                            {
                                if (d[key] != dic[key])
                                    throw new Exception($"There is a conflict in the info.json connector_config values. Type: {this.Type} key: {key} value1: {dic[key]} value2: {d[key]}");
                            }
                        }
                }
            return dic;
        }
        public string Name
        {
            get
            {
                return string.Concat(this.Type, " Connector");
            }
        }

        public bool HasInstalledItem
        {
            get
            {
                if (this.items != null)
                    foreach (IItem item in this.items.Values)
                        if (item.Category == Category.Command || item.State == States.Installed)
                            return true;
                return false;
            }
        }

        public Module()
        {
            this.lite = new moduleLite();
            this.items = new Dictionary<string, IItem>();
        }

        #region Errors
        private List<GUAException> _errors = new List<GUAException>();
        private void setError(string type, string uuid, Exception ex)
        {
            lock(this._errors)
            {
                _errors.Add(new GUAException(type, uuid, ex.Message));
            }
        }
        public IEnumerable<GUAException> GetErrors()
        {
            if (this._errors != null)
                lock(this._errors)
                {
                    return this._errors.ToArray();
                }
            return new GUAException[0];
        }
        #endregion

        public bool IsUpdateRequired(UpdateInfo u)
        {
            if (!this.items.ContainsKey(u.UUID))
            {
                IItem item = createItem(u.Category, u.config_type, u.custom_config_type);
                item.UUID = u.UUID;
                item.Version = u.Version;
                item.URL = u.Url;
                item.State = States.DownloadRequired;
                this.items.Add(u.UUID, item);
                return true;
            }
            else
            {
                IItem item = this.items[u.UUID];
                if (item.Version == u.Version)
                {
                    switch (item.State)
                    {
                        case States.Installed:
                            return false;

                        case States.Deactivated:
                            item.State = States.ActivateRequired;
                            return true;

                        case States.ActivateRequired:
                        case States.Downloaded:
                            return true;
                    }
                }
                item.Version = u.Version;
                item.URL = u.Url;
                item.State = States.DownloadRequired;
                return true;
            }
        }
        public bool IsUninstallOrDeactivationRequired(Dictionary<string, UpdateInfo> dicIdUpdates)
        {
            bool res = false;
            foreach (string id in this.items.Keys)
                if (!dicIdUpdates.ContainsKey(id))
                {
                    IItem item = this.items[id];
                    if (item.State == States.Installed)
                    {
                        res = true;
                        switch (item.Category)
                        {
                            case Category.Package:
                            case Category.Config:
                                item.State = States.DeactivateRequired;
                                break;
                            default:
                                item.State = States.UninstallRequired;
                                break;
                        }
                    }
                }
            return res;
        }

        public IEnumerable<UpdateResult> DownloadIfRequired(ISettings stt, ILogger logger)
        {
            List<UpdateResult> lst = new List<UpdateResult>();
            Parallel.ForEach(this.items.Values, (IItem item) =>
            {
                try
                {
                    foreach (UpdateResult ur in item.DownloadIfRequired(this, stt, logger))
                        lst.Add(new UpdateResult()
                        {
                            Type = this.Type,
                                UUID = item.UUID,
                                State = UpdateResultState.Downloaded,
                                InstallLog = $"State: {item.State}"
                        });
                }
                catch (Exception ex)
                {
                    this.setError(this.Type, item.UUID, ex);
                    lst.Add(new UpdateResult()
                    {
                        Type = this.Type,
                            UUID = item.UUID,
                            State = UpdateResultState.Error,
                            InstallLog = $"Error.Message: {ex.Message}"
                    });
                }
            });
            return lst;
        }

        public IEnumerable<UpdateResult> UpdateIfRequired(ServiceState ss, ISettings stt, ILogger logger)
        {
            List<UpdateResult> lst = new List<UpdateResult>();
            foreach (IItem item in this.items.Values)
            {
                try
                {
                    lst.AddRange(item.UpdateIfRequired(this, ss, stt, logger));
                }
                catch (Exception ex)
                {
                    this.setError(this.Type, item.UUID, ex);
                    lst.Add(new UpdateResult()
                    {
                        Type = this.Type,
                            UUID = item.UUID,
                            State = UpdateResultState.Error,
                            InstallLog = $"Error.Message: {ex.Message}"
                    });
                }
            }
            return lst;
        }

        private IItem createItem(Category c, string ct, CustomConfigType cct)
        {
            switch (c)
            {
                case Category.Config:
                    return new Config(this.Type, ct, cct);
                case Category.Package:
                    return new Package(ct, cct);
                case Category.Command:
                    return new Command(ct, cct);
                default:
                    throw new NotImplementedException();
            }
        }

        #region IO

        private static string getLiteFn(string dir)
        {
            return Path.Combine(dir, "bundle.json");
        }
        public void Save(string dir, string guaVersion)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            this.GUAVersion = guaVersion;
            string fn = getLiteFn(dir);
            Helper.SerializeToJsonFile(this.lite, fn);

            this.saveItems(dir);
        }
        internal static IModule Load(string dir)
        {
            string fn = getLiteFn(dir);
            if (!File.Exists(fn))
                throw new Exception($"File cannot be found: {fn}");
            moduleLite bl = Helper.DeserializeFromJsonFile<moduleLite>(fn);
            Module b = new Module();
            b.lite = bl;
            b.loadItems(dir);
            return b;
        }

        private const string itemJsonFileName = "item.json";
        private static string getItemFileName(string dir, IItem item)
        {
            string subDir = Path.Combine(dir, Helper.ReplaceInvalidPathChars(item.UUID, "_"));
            if (!Directory.Exists(subDir))
                Directory.CreateDirectory(subDir);
            return Path.Combine(subDir, itemJsonFileName);
        }
        private void saveItems(string dir)
        {
            foreach (IItem item in this.items.Values)
            {
                item.Save(getItemFileName(dir, item));
            }
        }
        private void loadItems(string dir)
        {
            this.items = new Dictionary<string, IItem>();
            foreach (string subDir in Directory.EnumerateDirectories(dir))
            {
                string fn = Path.Combine(subDir, itemJsonFileName);
                if (File.Exists(fn))
                {
                    try
                    {
                        IItem item = Helper.DeserializeFromJsonFile<IItem>(fn);
                        this.items.Add(item.UUID, item);
                    }
                    catch (Exception ex)
                    {
                        this.setError(this.Type, subDir, ex);
                    }
                }
            }
        }
        #endregion

        private class moduleLite
        {
            public string Type { get; set; }
            public string GUAVersion { get; set; }

            public string ConfigType { get; set; }
            public CustomConfigType CustomConfigType { get; internal set; }
        }
    }
}