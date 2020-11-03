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
    internal class Config : ConfigBase
    {
        public Config(string type, string ct, CustomApp cct) : base(type, ct, cct) { }
        public override Category Category => Category.Config;
        protected override string Name => "Config";
        protected override string yamlKey => "configuration";
        protected override string ConfigFileName => string.Concat(this.Type, ".json");
    }
}