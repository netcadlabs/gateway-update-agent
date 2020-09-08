using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Elements
{
    //public class UpdateResult
    internal class UpdateResult
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("uuid")]
        public string UUID { get; set; }

        [JsonProperty("install_log")]
        public string InstallLog { get; set; }

        [JsonProperty("state")]
        public UpdateResultState State { get; set; }

        // public string ToJson()
        // {
        //     return JsonConvert.SerializeObject(this);
        // }
    }
    internal enum UpdateResultState : int
    {
        Downloaded = 0,
        Installed = 1,
        Error = 2,
        Uninstalled = 3
    }

}