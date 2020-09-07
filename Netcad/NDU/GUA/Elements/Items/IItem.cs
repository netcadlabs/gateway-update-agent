using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal interface IItem
    {
        string UUID { get; set; }
        Category Category { get; }
        int Version { get; set; }
        string URL { get; set; }
        States State { get; set; }
        Dictionary<string, object> YamlConnectorItems { get; }

        void Save(string fileName);
        bool DownloadIfRequired(ISettings stt, ILogger logger);
        bool UpdateIfRequired(ServiceState ss, ISettings stt, ILogger logger);
    }
}