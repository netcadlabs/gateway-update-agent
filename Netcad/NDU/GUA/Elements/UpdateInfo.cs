using System;
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

        internal UpdateInfo Clone()
        {
            return new UpdateInfo()
            {
                Type = this.Type,
                UUID = this.UUID,
                Url = this.Url,
                Category = this.Category,
                Version = this.Version,
                config_type = this.config_type,
                custom_config_type = this.custom_config_type
            };
        }
    }    
}