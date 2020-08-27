using System.Text.Json.Serialization;

namespace Netcad.NDU.GatewayUpdateAgent.Download {
    public class Bundle {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("pack_version")]
        public int? PackVersion { get; set; }

        [JsonPropertyName("pack_url")]
        public string PackUrl { get; set; }
        public bool HasPack {
            get {
                return this.PackVersion != null && !string.IsNullOrWhiteSpace(this.PackUrl);
            }
        }

        [JsonPropertyName("config_version")]
        public int? ConfigVersion { get; set; }

        [JsonPropertyName("config_url")]
        public string ConfigUrl { get; set; }
        public bool HasConfig {
            get {
                return this.ConfigVersion != null && !string.IsNullOrWhiteSpace(this.ConfigUrl);
            }
        }
    }
}