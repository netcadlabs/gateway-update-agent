using System;
using System.Collections.Generic;
using System.IO;

namespace Netcad.NDU.GatewayUpdateAgent.Install {
    internal class PackInfoJson {
        public CopyInfo[] copy { get; set; }
        public Dictionary<string, object> connector_config { get; set; }

        public string GetClassName() {
            return this.connector_config["class"].ToString();
        }
        public static bool TryParse(string extractDir, out PackInfoJson p) {
            string infoJson = Path.Combine(extractDir, "info.json");
            if (!File.Exists(infoJson)) {
                p = null;
                return false;
            } else {
                try {
                    p = System.Text.Json.JsonSerializer.Deserialize<PackInfoJson>(File.ReadAllText(infoJson));
                    return p != null;
                } catch {
                    p = null;
                    return false;
                }
            }
        }
        public static PackInfoJson Parse(string extractDir) {
            string infoJson = Path.Combine(extractDir, "info.json");
            if (!File.Exists(infoJson))
                throw new Exception($"Cannot find info.json file. {extractDir}");

            return System.Text.Json.JsonSerializer.Deserialize<PackInfoJson>(File.ReadAllText(infoJson));
        }
    }
    internal class CopyInfo {
        public string source { get; set; }
        public string destination { get; set; }
    }
}