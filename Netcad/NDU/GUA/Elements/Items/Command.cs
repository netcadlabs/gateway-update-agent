using System.Collections.Generic;
using Netcad.NDU.GUA.Settings;

namespace Netcad.NDU.GUA.Elements.Items
{
    internal class Command : IItem
    {
        public string ID { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public Category Category => throw new System.NotImplementedException();

        public int Version { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public string URL { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public States State { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public Dictionary<string, object> YamlCustomProperties => throw new System.NotImplementedException();

        public void DownloadIfRequired(ISettings stt)
        {
            throw new System.NotImplementedException();
        }

        public void Save(string fileName)
        {
            throw new System.NotImplementedException();
        }

        public void UpdateIfRequired(ServiceState ss, ISettings stt)
        {
            throw new System.NotImplementedException();
        }
    }
}