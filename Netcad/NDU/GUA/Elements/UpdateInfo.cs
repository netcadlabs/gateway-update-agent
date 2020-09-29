using Netcad.NDU.GUA.Settings;

namespace Netcad.NDU.GUA.Elements
{
    public class UpdateInfo
    {
        public string Type { get; set; }
        public string UUID { get; set; }
        public string Url { get; set; }
        public Category Category { get; set; }
        public int Version { get; set; }

        //**NDU-310
        public string config_type { get; set; }
        public CustomConfigType custom_config_type { get; set; }
    }    
}