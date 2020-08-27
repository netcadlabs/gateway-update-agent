using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GatewayUpdateAgent.Utils;

namespace Netcad.NDU.GatewayUpdateAgent.Settings {
    public class Settings : ISettings {

        private config _config;
        public string Hostname { get { return _config.Hostname; } }
        public string Token { get { return _config.Token; } }
        public double IntervalInMinutes { get { return _config.IntervalInMinutes; } }
        public string ExtensionFolder { get { return _config.ExtensionFolder; } }
        public string ConfigFolder { get { return _config.ConfigFolder; } }
        public string YamlFileName { get { return _config.YamlFileName; } }
        public Version UpdateAgentVersion { get; private set; }
        public string TempFolder { get; private set; }

        public readonly ILogger<Settings> _logger;

        public Settings(ILogger<Settings> logger) {
            this._logger = logger;
            this.load();
        }

#if DEBUG
        private string fileName;
#else
        private const string fileName = @"/etc/GatewayUpdateAgent/GatewayUpdateAgent.conf";
#endif
        private DateTime _lastWriteTimeUtc = DateTime.MinValue;
        public void ReloadIfRequired() {
            System.IO.FileInfo fi = new FileInfo(fileName);
            if (fi.LastWriteTimeUtc != this._lastWriteTimeUtc)
                this.load();
        }
        private void load() {
            try {
                this.UpdateAgentVersion = (Version)typeof(Settings).Assembly.GetName().Version;
#if DEBUG
                string assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                fileName = Path.Combine(assemblyFolder, "GatewayUpdateAgent.conf");
#endif
                this._config = System.Text.Json.JsonSerializer.Deserialize<config>(File.ReadAllText(fileName));
                if (string.IsNullOrWhiteSpace(this._config.Hostname))
                    throw new Exception($"No Hostname definden in {fileName}");
                if (!this._config.Hostname.EndsWith("/"))
                    this._config.Hostname += "/";
                this.TempFolder = @"/tmp/Netcad/NDU/GUA/temp";

#if DEBUG
                string testDir = @"/tmp/Netcad/NDU/GUA_test";
                // string sourceDir = Path.Combine(assemblyFolder, @"test");
                // Helper.CopyDirectory(sourceDir, testDir, true);
                this._config.YamlFileName = Path.Combine(testDir, @"tb_gateway.yaml");
                this._config.ConfigFolder = Path.Combine(testDir, @"configs");
                this._config.ExtensionFolder = Path.Combine(testDir, @"extensions");

#endif
            } catch (Exception ex) {
                _logger.LogError(ex, "Settings initialize error.");
                throw ex;
            }
        }

        private class config {
            public string Hostname { get; set; }
            public string Token { get; set; }
            public double IntervalInMinutes { get; set; }
            public string ExtensionFolder { get; set; }
            public string ConfigFolder { get; set; }
            public string YamlFileName { get; set; }

        }
    }
}