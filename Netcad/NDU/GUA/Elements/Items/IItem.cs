using Netcad.NDU.GUA.Settings;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal interface IItem
    {
        string ID { get; set; }
        Category Category { get; }
        int Version { get; set; }
        string URL { get; set; }
        States State { get; set; }

        void Save(string fileName);
        void DownloadIfRequired(ISettings stt);
        void UpdateIfRequired(ServiceState ss, ISettings stt);
    }
}