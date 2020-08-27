using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GatewayUpdateAgent.Settings;
using Netcad.NDU.GatewayUpdateAgent.Utils;

namespace Netcad.NDU.GatewayUpdateAgent.Install {
    internal class Package {
        public string Type { get; private set; }
        public int PackVersion { get; private set; }
        public string ClassName {
            get {
                return this.info.GetClassName();
            }
        }
        public Dictionary<string, object> ConnectorConfig {
            get {
                return this.info.connector_config;
            }
        }

        private string zipFileName;
        private string extractDir;
        private readonly ISettings settings;
        private PackInfoJson info;
        private readonly ILogger logger;
        public Package(string zipFileName, string type, string extractDir, int packVersion, ISettings settings, ILogger logger) {
            this.logger = logger;
            if (!File.Exists(zipFileName))
                throw new Exception("No package file!");
            this.Type = type;
            this.PackVersion = packVersion;
            this.zipFileName = zipFileName;
            this.settings = settings;
            this.extractDir = extractDir;

        }
        private void parse() {
            if (!Directory.Exists(this.extractDir))
                Directory.CreateDirectory(this.extractDir);
            else
                Helper.CleanDirectory(this.extractDir);
                
            logger.LogInformation($"Extracting... zipFileName: {this.zipFileName}  extractDir: {this.extractDir}");
            ZipFile.ExtractToDirectory(this.zipFileName, this.extractDir);
            this.info = PackInfoJson.Parse(this.extractDir);
            if (!this.info.connector_config.ContainsKey("class"))
                throw new Exception($"Cannot find connector_config/class in info.json file. {this.extractDir}");
        }
        public bool Install(Dictionary<string, Exception> errors) {
            try {
                if (this.info == null)
                    parse();
                if (this.info.copy != null) {
                    logger.LogInformation($"Installing pack: {this.Type}");
                    foreach (CopyInfo ci in this.info.copy) {
#if DEBUG
                        string packTestDir = Path.Combine(settings.ExtensionFolder, "__test_copyDir");
                        if (!ci.destination.StartsWith(packTestDir)) {
                            string dir = string.Concat(packTestDir, ci.destination);
                            ci.destination = dir;
                        }
#endif
                        string source = Path.Combine(this.extractDir, ci.source);
                        logger.LogInformation($"Copying file... Source: {ci.source} Destination: {ci.destination}");
                        if (File.Exists(source)) {
                            File.Copy(source, ci.destination, true);
                            File.Delete(source);
                        } else if (Directory.Exists(source)) {
                            Helper.CopyDirectory(source, ci.destination, false);
                            Helper.DeleteDirectory(source);
                        } else
                            throw new Exception($"Cannot copy a file in pack! Source: {source} Destination: {ci.destination}");
                    }
                }
                return true;
            } catch (Exception ex) {
                errors.Add(this.Type, ex);
                return false;
            }
        }
    }
}