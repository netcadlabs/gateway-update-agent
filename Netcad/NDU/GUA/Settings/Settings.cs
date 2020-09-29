using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Settings
{
    //     "AppTypes": [
    //       {
    //           "AppType" :"default",
    //           "ExtensionFolder" : "/var/lib/thingsboard_gateway/extensions/",
    //           "ConfigFolder" : "/etc/thingsboard-gateway/config" ,
    //           "YamlCollectionName" : "connectors",
    //           "YamlFileName" : "/etc/thingsboard-gateway/config/tb_gateway.yaml",          
    //           "RestartServices" : [ "thingsboard-gateway.service"]          
    //       },    
    //       {
    //           "AppType" :"ndu_gate",
    //           "ExtensionFolder" : "/var/lib/ndu_gate/runners/",
    //           "ConfigFolder" : "/etc/ndu-gate/config" ,
    //           "YamlCollectionName" : "runners",
    //           "YamlFileName" : "/etc/ndu-gate/config/ndu_gate.yaml",          
    //           "RestartServices" : [ "thingsboard-gateway.service", "ndu-gate.service"]
    //       }
    //   ]

    public class Settings : ISettings
    {
        private class config
        {
            public string Hostname { get; set; }
            public string Token { get; set; }
            public double IntervalInMinutes { get; set; }
            public appType[] AppTypes { get; set; }
        }
        private class appType
        {
            public string AppType { get; set; }
            public string ExtensionFolder { get; set; }
            public string ConfigFolder { get; set; }
            public string YamlCollectionName { get; set; } //***********
            public string YamlFileName { get; set; }
            public string[] RestartServices { get; set; } //***********
        }

        private config _config;
        public string Hostname { get { return _config.Hostname; } }
        public string Token { get { return _config.Token; } }
        public double IntervalInMinutes { get { return _config.IntervalInMinutes; } }

        private Dictionary<string, appType> _appTypes;
        private appType _getAppType(string appType)
        {
            if (!this._appTypes.ContainsKey(appType))
            {
                _logger.LogError($"Bad AppType:{appType}");
                appType = ISettings.DEFAULT_APPTYPE;
            }
            return _appTypes[appType];
        }
        public string GetExtensionFolder(string appType = ISettings.DEFAULT_APPTYPE)
        {
            return _getAppType(appType).ExtensionFolder;
        }
        public string GetConfigFolder(string appType = ISettings.DEFAULT_APPTYPE)
        {
            return _getAppType(appType).ConfigFolder;
        }
        public string GetYamlCollectionName(string appType = ISettings.DEFAULT_APPTYPE)
        {
            return _getAppType(appType).YamlCollectionName;
        }
        public string GetYamlFileName(string appType = ISettings.DEFAULT_APPTYPE)
        {
            return _getAppType(appType).YamlFileName;
        }
        public string[] GetRestartServices(string appType = ISettings.DEFAULT_APPTYPE)
        {
            return _getAppType(appType).RestartServices;
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

                this._appTypes = new Dictionary<string, appType>();
                foreach (var ty in this._config.AppTypes)
                    if (!this._appTypes.ContainsKey(ty.AppType))
                        this._appTypes.Add(ty.AppType, ty);
                    else
                        throw new Exception($"Duplicated AppType definden in {fileName}");
                if (!this._appTypes.ContainsKey(ISettings.DEFAULT_APPTYPE))
                    throw new Exception($"'{ISettings.DEFAULT_APPTYPE}' AppType not defined in {fileName}");

#if DEBUG
                string assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string testDir = @"/tmp/Netcad/NDU/GUA_test";

                if (_firstRun)
                {
                    _firstRun = false;
                    string sourceDir = Path.Combine(assemblyFolder, @"test");
                    Helper.CopyDir(sourceDir, testDir, true);
                }

                this._appTypes[ISettings.DEFAULT_APPTYPE].YamlFileName = Path.Combine(testDir, @"tb_gateway.yaml");
                this._appTypes[ISettings.DEFAULT_APPTYPE].ConfigFolder = Path.Combine(testDir, @"configs");
                this._appTypes[ISettings.DEFAULT_APPTYPE].ExtensionFolder = Path.Combine(testDir, @"extensions");
                // this._config.IntervalInMinutes = Math.Min(this._config.IntervalInMinutes, 0.5);
#endif
                string extDir = this.GetExtensionFolder();
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