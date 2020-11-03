using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class CustomConfig : ConfigBase
    {
        public CustomConfig(string type, string ct, CustomApp cct) : base(type, ct, cct) { }
        public override Category Category => Category.CustomConfig;
        protected override string Name => "CustomConfig";
        protected override string yamlKey => "custom_configuration";
        protected override string ConfigFileName => string.Concat(this.Type, "_custom.json");
    }
}