using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements
{
    internal interface IModule
    {
        string Type { get; set; }
        string GUAVersion { get; set; }
        bool HasInstalledItem { get; }

        Dictionary<string, object> GetYamlCustomProperties();
        IEnumerable<GUAException> GetErrors();
        void Save(string dir, string guaVersion);

        IEnumerable<UpdateResult> UpdateIfRequired(ServiceState ss, ISettings stt, ILogger logger);
        IEnumerable<UpdateResult> DownloadIfRequired(ISettings stt, ILogger logger);
        bool IsUninstallOrDeactivationRequired(Dictionary<string, UpdateInfo> dicIdUpdates);
        bool IsUpdateRequired(UpdateInfo u);

        //**NDU-310
        string App { get; set; }
        CustomApp CustomApp { get; set; }

        //**NDU-340
        int Status { get; set; }

        string GetYamlFileName(ISettings stt);
        string GetYamlCollectionName(ISettings stt);
    }
}