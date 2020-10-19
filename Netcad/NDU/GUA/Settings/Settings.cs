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
            public configType[] ConfigTypes { get; set; }
        }
        private class configType
        {
            public string ConfigType { get; set; }
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

        private Dictionary<string, configType> _confTypes;
        private configType _getConfType(string confType)
        {
            if (string.IsNullOrWhiteSpace(confType))
                confType = ISettings.DEFAULT_CONFIG_TYPE;
            else if (!this._confTypes.ContainsKey(confType))
            {
                _logger.LogError($"Bad ConfigType:{confType}");
                confType = ISettings.DEFAULT_CONFIG_TYPE;
            }
            return _confTypes[confType];
        }
        public string GetExtensionFolder(string configType, CustomConfigType custom_config_type)
        {
            if (custom_config_type != null)
                return custom_config_type.ExtensionFolder;
            else
                return _getConfType(configType).ExtensionFolder;
        }
        public string GetConfigFolder(string configType, CustomConfigType custom_config_type)
        {
            if (custom_config_type != null)
                return custom_config_type.ConfigFolder;
            else
                return _getConfType(configType).ConfigFolder;
        }
        public string GetYamlCollectionName(string configType, CustomConfigType custom_config_type)
        {
            if (custom_config_type != null)
                return custom_config_type.YamlCollectionName;
            else
                return _getConfType(configType).YamlCollectionName;
        }
        public string GetYamlFileName(string configType, CustomConfigType custom_config_type)
        {
            if (custom_config_type != null)
                return custom_config_type.YamlFileName;
            else
                return _getConfType(configType).YamlFileName;
        }
        public string[] GetRestartServices(string configType, CustomConfigType custom_config_type)
        {
            if (custom_config_type != null)
                return custom_config_type.RestartServices;
            else
                return _getConfType(configType).RestartServices;
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

                this._confTypes = new Dictionary<string, configType>();
                foreach (var ty in this._config.ConfigTypes)
                    if (!this._confTypes.ContainsKey(ty.ConfigType))
                        this._confTypes.Add(ty.ConfigType, ty);
                    else
                        throw new Exception($"Duplicated ConfigType definden in {fileName}");
                if (!this._confTypes.ContainsKey(ISettings.DEFAULT_CONFIG_TYPE))
                    throw new Exception($"'{ISettings.DEFAULT_CONFIG_TYPE}' ConfigType not defined in {fileName}");

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

                // this._confTypes[ISettings.DEFAULT_CONFIG_TYPE].YamlFileName = Path.Combine(testDir, @"tb_gateway.yaml");
                // this._confTypes[ISettings.DEFAULT_CONFIG_TYPE].ConfigFolder = Path.Combine(testDir, @"configs");
                // this._confTypes[ISettings.DEFAULT_CONFIG_TYPE].ExtensionFolder = Path.Combine(testDir, @"extensions");

                //**NDU-317
                foreach (configType cf in this._confTypes.Values)
                {
                    cf.YamlFileName = testDir + cf.YamlFileName;
                    cf.ConfigFolder = testDir + cf.ConfigFolder;
                    cf.ExtensionFolder = testDir + cf.ExtensionFolder;
                }
                this._config.Hostname = "http://80.253.246.57:8083/";
                this._config.Token = "28rCtj9NOH94vxyan5MX";

                // this._config.IntervalInMinutes = Math.Min(this._config.IntervalInMinutes, 0.5);
#endif
                string extDir = this.GetExtensionFolder(ISettings.DEFAULT_CONFIG_TYPE, null);
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