using System;
using System.Collections.Generic;

namespace Netcad.NDU.GUA.Elements
{
    internal class InstalledType
    {
        public string Type { get; set; }
        public string GUAVersion { get; set; }
        public string ClassName { get; set; }
        public string Name
        {
            get
            {
                return string.Concat(this.Type, " Connector");
            }
        }

        public List<InstalledItem> Items { get; set; }

        public InstalledItem GetItemByID(string id)
        {
            if (this.Items != null)
                foreach (InstalledItem it in this.Items)
                    if (it.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                        return it;
            return null;
        }
    }
}