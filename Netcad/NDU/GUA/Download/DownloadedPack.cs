using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Download
{
    public class DownloadedPack
    {
        public UpdateInfo UpdateInfo { get; private set; }
        public string ClassName
        {
            get
            {
                return this.info.GetClassName();
            }
        }
        public Dictionary<string, object> ConnectorConfig
        {
            get
            {
                return this.info.connector_config;
            }
        }

        private string zipFileName;
        private string extractDir;
        private readonly ISettings settings;
        private PackInfoJson info;
        private readonly ILogger logger;
        public DownloadedPack(string zipFileName, UpdateInfo info, string extractDir, ISettings settings, ILogger logger)
        {
            this.logger = logger;
            if (!File.Exists(zipFileName))
                throw new Exception("No package file!");
            this.UpdateInfo = info;
            this.zipFileName = zipFileName;
            this.settings = settings;
            this.extractDir = extractDir;

        }
        private void parse()
        {
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
        public bool Install(Dictionary<string, Exception> errors)
        {
            try
            {
                if (this.info == null)
                    parse();
                if (this.info.copy != null)
                {
                    logger.LogInformation($"Installing pack: Type:{this.UpdateInfo.Type} Id:{this.UpdateInfo.Id}");
                    foreach (CopyInfo ci in this.info.copy)
                    {
#if DEBUG
                        string packTestDir = Path.Combine(settings.ExtensionFolder, "__test_copyDir");
                        if (!ci.destination.StartsWith(packTestDir))
                        {
                            string dir = string.Concat(packTestDir, ci.destination);
                            ci.destination = dir;
                        }
#endif
                        string source = Path.Combine(this.extractDir, ci.source);
                        logger.LogInformation($"Copying file... Source: {ci.source} Destination: {ci.destination}");
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
                return true;
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(this.UpdateInfo.Id, ex);
                }
                return false;
            }
        }
    }

    internal class PackInfoJson
    {
        public CopyInfo[] copy { get; set; }
        public Dictionary<string, object> connector_config { get; set; }

        public string GetClassName()
        {
            return this.connector_config["class"].ToString();
        }
        public static bool TryParse(string extractDir, out PackInfoJson p)
        {
            string infoJson = Path.Combine(extractDir, "info.json");
            if (!File.Exists(infoJson))
            {
                p = null;
                return false;
            }
            else
            {
                try
                {
                    p = JsonConvert.DeserializeObject<PackInfoJson>(File.ReadAllText(infoJson));
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
            string infoJson = Path.Combine(extractDir, "info.json");
            if (!File.Exists(infoJson))
                throw new Exception($"Cannot find info.json file. {extractDir}");

            return JsonConvert.DeserializeObject<PackInfoJson>(File.ReadAllText(infoJson));
        }
    }
    internal class CopyInfo
    {
        public string source { get; set; }
        public string destination { get; set; }
    }
}