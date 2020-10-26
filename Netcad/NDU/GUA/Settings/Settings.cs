using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Settings
{
    public class Settings : ISettings
    {
        private class config
        {
            public string Hostname { get; set; }
            public string Token { get; set; }
            public double IntervalInMinutes { get; set; }
            public app[] Apps { get; set; }
        }
        private class app
        {
            public string App { get; set; }
            public string ExtensionFolder { get; set; }
            public string ConfigFolder { get; set; }
            public string YamlCollectionName { get; set; }
            public string YamlFileName { get; set; }
            public string[] RestartServices { get; set; }
        }

        private config _config;
        public string Hostname { get { return _config.Hostname; } }
        public string Token { get { return _config.Token; } }
        public double IntervalInMinutes { get { return _config.IntervalInMinutes; } }

        private Dictionary<string, app> _confTypes;
        private app _getConfType(string confType)
        {
            if (string.IsNullOrWhiteSpace(confType))
                confType = ISettings.DEFAULT_APP;
            else if (!this._confTypes.ContainsKey(confType))
            {
                _logger.LogError($"Bad app:{confType}");
                confType = ISettings.DEFAULT_APP;
            }
            return _confTypes[confType];
        }
        public string GetExtensionFolder(string app, CustomApp custom_app)
        {
            if (custom_app != null)
                return custom_app.ExtensionFolder;
            else
                return _getConfType(app).ExtensionFolder;
        }
        public string GetConfigFolder(string app, CustomApp custom_app)
        {
            if (custom_app != null)
                return custom_app.ConfigFolder;
            else
                return _getConfType(app).ConfigFolder;
        }
        public string GetYamlCollectionName(string app, CustomApp custom_app)
        {
            if (custom_app != null)
                return custom_app.YamlCollectionName;
            else
                return _getConfType(app).YamlCollectionName;
        }
        public string GetYamlFileName(string app, CustomApp custom_app)
        {
            if (custom_app != null)
                return custom_app.YamlFileName;
            else
                return _getConfType(app).YamlFileName;
        }
        public string[] GetRestartServices(string app, CustomApp custom_app)
        {
            if (custom_app != null)
                return custom_app.RestartServices;
            else
                return _getConfType(app).RestartServices;
        }

        public string HistoryFolder { get; private set; }
        public Version GUAVersion { get; private set; }

        public readonly ILogger<Settings> _logger;

        private List<Action> onChangeActions = new List<Action>();
        public void ListenChange(Action onChange)
        {
            this.onChangeActions.Add(onChange);
        }
        public Settings(ILogger<Settings> logger)
        {
#if DEBUG
            string assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            fileName = Path.Combine(assemblyFolder, "GatewayUpdateAgent.conf");
#endif

            this._logger = logger;
            this.load();
        }

#if DEBUG
        private string fileName;
        private static bool _firstRun = true;
#else
        private const string fileName = @"/etc/GatewayUpdateAgent/GatewayUpdateAgent.conf";
#endif
        private DateTime _lastWriteTimeUtc = DateTime.MinValue;
        public void ReloadIfRequired()
        {
            System.IO.FileInfo fi = new FileInfo(fileName);
            if (fi.LastWriteTimeUtc != this._lastWriteTimeUtc)
                this.load();
        }
        private void load()
        {
            try
            {
                this.GUAVersion = (Version)typeof(Settings).Assembly.GetName().Version;
                this._config = Helper.DeserializeFromJsonFile<config>(fileName);
                if (string.IsNullOrWhiteSpace(this._config.Hostname))
                    throw new Exception($"No Hostname definden in {fileName}");
                if (!string.IsNullOrWhiteSpace(this._config.Token) && !this._config.Hostname.EndsWith("/"))
                    this._config.Hostname += "/";

                this._confTypes = new Dictionary<string, app>();
                foreach (var ty in this._config.Apps)
                    if (!this._confTypes.ContainsKey(ty.App))
                        this._confTypes.Add(ty.App, ty);
                    else
                        throw new Exception($"Duplicated App definden in {fileName}");
                if (!this._confTypes.ContainsKey(ISettings.DEFAULT_APP))
                    throw new Exception($"'{ISettings.DEFAULT_APP}' App not defined in {fileName}");

#if DEBUG
                string assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string testDir = @"/tmp/Netcad/NDU/GUA_test";

                bool initialize = false;
                if (_firstRun && initialize)
                {
                    _firstRun = false;
                    string sourceDir = Path.Combine(assemblyFolder, @"test");
                    Helper.CopyDir(sourceDir, testDir, true);
                }

                // this._confTypes[ISettings.DEFAULT_APP].YamlFileName = Path.Combine(testDir, @"tb_gateway.yaml");
                // this._confTypes[ISettings.DEFAULT_APP].ConfigFolder = Path.Combine(testDir, @"configs");
                // this._confTypes[ISettings.DEFAULT_APP].ExtensionFolder = Path.Combine(testDir, @"extensions");

                //**NDU-317
                foreach (app cf in this._confTypes.Values)
                {
                    cf.YamlFileName = testDir + cf.YamlFileName;
                    cf.ConfigFolder = testDir + cf.ConfigFolder;
                    cf.ExtensionFolder = testDir + cf.ExtensionFolder;
                }
                this._config.Hostname = "http://80.253.246.57:8083/";
                this._config.Token = "28rCtj9NOH94vxyan5MX";

                // this._config.IntervalInMinutes = Math.Min(this._config.IntervalInMinutes, 0.5);
#endif
                string extDir = this.GetExtensionFolder(ISettings.DEFAULT_APP, null);
                if (!Directory.Exists(extDir))
                    Directory.CreateDirectory(extDir);
                this.HistoryFolder = Path.Combine(extDir, "_GUA_History");
                if (!Directory.Exists(this.HistoryFolder))
                    Directory.CreateDirectory(this.HistoryFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Settings initialize error.");
                throw ex;
            }
        }

    }
}