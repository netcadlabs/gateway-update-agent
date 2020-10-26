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
        public string app { get; set; }
        public CustomApp custom_app { get; set; }

        //**NDU-340
        public int status { get; set; }

        internal UpdateInfo Clone()
        {
            return new UpdateInfo()
            {
                Type = this.Type,
                    UUID = this.UUID,
                    Url = this.Url,
                    Category = this.Category,
                    Version = this.Version,
                    app = this.app,
                    custom_app = this.custom_app
            };
        }
    }
}