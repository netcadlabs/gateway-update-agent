using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using Netcad.NDU.GatewayUpdateAgent.Settings;

namespace Netcad.NDU.GatewayUpdateAgent.Install {
    internal class InstalledBundle {

        public string Type { get; set; }
        public string UpdateAgentVersion { get; set; }
        public int? ConfigVersion { get; set; }
        public int? PackVersion { get; set; }
        public string ClassName { get; set; }
        public Dictionary<string, object> ConnectorConfig { get; set; }
        public bool HasConfig {
            get {
                return this.ConfigVersion != null;
            }
        }
        public bool HasPack {
            get {
                return this.PackVersion != null;
            }
        }

    }
}