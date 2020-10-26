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
        IEnumerable<UpdateResult> DownloadIfRequired(IModule parent, ISettings stt, ILogger logger);
        IEnumerable<UpdateResult> UpdateIfRequired(IModule parent, ServiceState ss, ISettings stt, ILogger logger);

        //**NDU-310
        string app { get; set; }
        CustomApp custom_app { get; set; }
    }
}