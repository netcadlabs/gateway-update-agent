// using Newtonsoft.Json;

// namespace Netcad.NDU.GUA.Updater
// {
//     internal class ApiResult
//     {
//         [JsonProperty("type")]
//         public string Type { get; set; }

//         [JsonProperty("uuid")]
//         public string UUID { get; set; }

//         [JsonProperty("install_log")]
//         public string InstallLog { get; set; }

//         [JsonProperty("state")]
//         public ApiResultState State { get; set; }

//         public string ToJson()
//         {
//             return JsonConvert.SerializeObject(this);
//         }
//     }
//     internal enum ApiResultState : int
//     {
//         Downloaded = 0,
//         Installed = 1,
//         Error = 2
//     }
// }*******