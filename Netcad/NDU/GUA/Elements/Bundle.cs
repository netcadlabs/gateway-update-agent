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
    internal class Bundle
    {
        private bundleLite lite;
        private Dictionary<string, IItem> items;
        public string Type { get => this.lite.Type; set => this.lite.Type = value; }
        public string GUAVersion { get => this.lite.GUAVersion; set => this.lite.GUAVersion = value; }
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

        public Bundle()
        {
            this.lite = new bundleLite();
            this.items = new Dictionary<string, IItem>();
        }

        #region Errors
        private Dictionary<string, List<Exception>> _errors = new Dictionary<string, List<Exception>>();
        private void setError(string id, Exception ex)
        {
            lock(this._errors)
            {
                List<Exception> lst;
                if (!this._errors.TryGetValue(id, out lst))
                {
                    lst = new List<Exception>();
                    this._errors.Add(id, lst);
                }
                lst.Add(ex);
            }
        }
        internal IEnumerable<Exception> GetErrors()
        {
            if (this._errors != null)
                foreach (var lst in this._errors.Values)
                    foreach (var ex in lst)
                        yield return ex;
        }
        #endregion

        internal bool IsUpdateRequired(UpdateInfo u)
        {
            if (!this.items.ContainsKey(u.ID))
            {
                IItem item = createItem(u.Category);
                item.ID = u.ID;
                item.Version = u.Version;
                item.URL = u.Url;
                item.State = States.DownloadRequired;
                this.items.Add(u.ID, item);
                return true;
            }
            else
            {
                IItem item = this.items[u.ID];
                if (item.Version == u.Version)
                {
                    if (item.State == States.Installed)
                        return false;
                    else if (item.State == States.Deactivated)
                    {
                        item.State = States.ActivateRequired;
                        return true;
                    }
                }
                item.Version = u.Version;
                item.URL = u.Url;
                item.State = States.DownloadRequired;
                return true;
            }
        }
        internal bool IsUninstallOrDeactivationRequired(Dictionary<string, UpdateInfo> dicIdUpdates)
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

        internal void DownloadIfRequired(ISettings stt, ILogger logger)
        {
            Parallel.ForEach(this.items.Values, (IItem item) =>
            {
                try
                {
                    item.DownloadIfRequired(stt, logger);
                }
                catch (Exception ex)
                {
                    this.setError(item.ID, ex);
                }
            });
        }

        internal void UpdateIfRequired(ServiceState ss, ISettings stt, ILogger logger)
        {
            foreach (IItem item in this.items.Values)
            {
                try
                {
                    item.UpdateIfRequired(ss, stt, logger);
                }
                catch (Exception ex)
                {
                    this.setError(item.ID, ex);
                }
            }
        }

        private IItem createItem(Category c)
        {
            switch (c)
            {
                case Category.Config:
                    return new Config(this.Type);
                case Category.Package:
                    return new Package();
                case Category.Command:
                    return new Command();
                default:
                    throw new NotImplementedException();
            }
        }

        #region IO

        private static string getLiteFn(string dir)
        {
            return Path.Combine(dir, "bundle.json");
        }
        internal void Save(string dir, string guaVersion)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            this.GUAVersion = guaVersion;
            string fn = getLiteFn(dir);
            Helper.SerializeToJsonFile(this.lite, fn);

            this.saveItems(dir);
        }
        internal static Bundle Load(string dir)
        {
            string fn = getLiteFn(dir);
            if (!File.Exists(fn))
                throw new Exception($"File cannot be found: {fn}");
            bundleLite bl = Helper.DeserializeFromJsonFile<bundleLite>(fn);
            Bundle b = new Bundle();
            b.lite = bl;
            b.loadItems(dir);
            return b;
        }

        private const string itemJsonFileName = "item.json";
        private static string getItemFileName(string dir, IItem item)
        {
            string subDir = Path.Combine(dir, Helper.ReplaceInvalidPathChars(item.ID, "_"));
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
                        this.items.Add(item.ID, item);
                    }
                    catch (Exception ex)
                    {
                        this.setError(subDir, ex);
                    }
                }
            }
        }
        #endregion

        private class bundleLite
        {
            public string Type { get; set; }
            public string GUAVersion { get; set; }
        }
    }
}