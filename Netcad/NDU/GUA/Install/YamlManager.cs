using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Settings;
using Netcad.NDU.GUA.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Netcad.NDU.GUA.Install
{
    internal class YamlManager
    {
        private const string Key_Connectors = "connectors";
        private const string Key_Type = "type";
        private const string Key_Config = "configuration";
        private const string Key_GUAVersion = "guaVersion";
        private const string Key_Name = "name";
        private const string Key_ClassName = "class";
        private readonly ILogger logger;

        string fn;
        string guaVersion;
        public YamlManager(string fn, string guaVersion, ILogger logger)
        {
            this.logger = logger;
            this.fn = fn;
            this.guaVersion = guaVersion;
        }

        private static Dictionary<string, object> _parseYaml(string fn)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();

            using(var stream = File.OpenText(fn))
            {
                return deserializer.Deserialize<Dictionary<string, object>>(stream);
            }
        }
        private static IEnumerable<Dictionary<object, object>> _getConnectors(string fn)
        {
            Dictionary<string, object> yaml = _parseYaml(fn);
            foreach (string key in yaml.Keys)
                if (Key_Connectors.Equals(key as string, StringComparison.OrdinalIgnoreCase))
                    foreach (Dictionary<object, object> connector in (List<object>)yaml[key])
                        if (connector.ContainsKey(Key_GUAVersion))
                        {
                            if (connector.ContainsKey(Key_Type))
                                yield return connector;
                            else
                            {
                                foreach (object key1 in connector.Keys)
                                {
                                    string k = key1 as string;
                                    if (!string.IsNullOrEmpty(k) && Key_Type.Equals(k, StringComparison.OrdinalIgnoreCase))
                                    {
                                        yield return connector;
                                        break;
                                    }
                                }
                            }
                        }
        }

        private static object ioLocker = new object();
        internal void UpdateConnectors(InstalledType[] arr)
        {
            lock(ioLocker)
            {
                Dictionary<string, object> yaml = _parseYaml(this.fn);
                List<object> connectors = null;
                foreach (string key in yaml.Keys)
                    if (Key_Connectors.Equals(key as string, StringComparison.OrdinalIgnoreCase))
                        connectors = (List<object>)yaml[key];
                if (connectors == null)
                {
                    connectors = new List<object>();
                    yaml.Add(Key_Connectors, connectors);
                }

                for (int i = connectors.Count - 1; i >= 0; i--)
                {
                    Dictionary<object, object> connector = (Dictionary<object, object>)connectors[i];
                    if (connector.ContainsKey(Key_GUAVersion))
                        connectors.RemoveAt(i);
                }
                foreach (InstalledType it in arr)
                {
                    connectors.Add(toConnector(it));
                }

                var serializer = new SerializerBuilder().Build();
                File.WriteAllText(this.fn, serializer.Serialize(yaml));
            }
        }

        private object toConnector(InstalledType it)
        {
            // name: < type > Connector
            // type: < type >
            //     configuration: < type >.json
            // class: < class > //info.json'dan okunan değer.
            //     pack_version: < >
            //     conf_version: < >
            Dictionary<object, object> dic = new Dictionary<object, object>();
            dic.Add(Key_Name, $"{it.Type} Connector");
            dic.Add(Key_Type, $"{it.Type}");
            dic.Add(Key_Config, $"{it.Type}.json");
            if (!string.IsNullOrWhiteSpace(it.ClassName))
                dic.Add(Key_ClassName, it.ClassName);
            dic.Add(Key_GUAVersion, it.GUAVersion);
            return dic;
        }

        // private static object ioLocker = new object();
        // public Dictionary<string, InstalledBundle> GetInstalled()
        // {
        //     lock(ioLocker)
        //     {
        //         Dictionary<string, InstalledBundle> res = new Dictionary<string, InstalledBundle>(StringComparer.OrdinalIgnoreCase);
        //         foreach (Dictionary<object, object> connector in _getConnectors(this.fn))
        //         {
        //             string type = connector[Key_Type] as string;
        //             //string ver = connector[Key_UpdateAgentVersion] as string;
        //             //if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(ver)) {
        //             InstalledBundle b = fromConnector(connector);
        //             object obj;
        //             if (connector.TryGetValue(Key_ConfigVersion, out obj))
        //                 b.ConfigVersion = Helper.ParseVersion(obj);
        //             if (connector.TryGetValue(Key_PackVersion, out obj))
        //                 b.PackVersion = Helper.ParseVersion(obj);
        //             res.Add(type, b);
        //             //}
        //         }
        //         return res;
        //     }
        // }
        // public void SetInstalled(Dictionary<string, InstalledBundle> installed)
        // {
        //     lock(ioLocker)
        //     {
        //         Dictionary<string, object> yaml = _parseYaml(this.fn);
        //         //List<Dictionary<object, object>> connectors = null;
        //         List<object> connectors = null;
        //         foreach (string key in yaml.Keys)
        //             if (Key_Connectors.Equals(key as string, StringComparison.OrdinalIgnoreCase))
        //                 connectors = (List<object>)yaml[key];
        //         if (connectors == null)
        //         {
        //             connectors = new List<object>();
        //             yaml.Add(Key_Connectors, connectors);
        //         }

        //         // foreach (Dictionary<object, object> connector in connectors.ToList())
        //         for (int i = connectors.Count - 1; i >= 0; i--)
        //         {
        //             Dictionary<object, object> connector = (Dictionary<object, object>)connectors[i];
        //             if (connector.ContainsKey(Key_UpdateAgentVersion))
        //             {
        //                 connectors.RemoveAt(i);
        //             }
        //         }
        //         foreach (InstalledBundle b in installed.Values)
        //         {
        //             connectors.Add(toConnector(b));
        //         }

        //         var serializer = new SerializerBuilder().Build();
        //         File.WriteAllText(this.fn, serializer.Serialize(yaml));
        //     }
        // }

        // #region InstalledBundle
        // private static Dictionary<object, object> toConnector(InstalledBundle b)
        // {
        //     // name: < type > Connector
        //     // type: < type >
        //     //     configuration: < type >.json
        //     // class: < class > //info.json'dan okunan değer.
        //     //     pack_version: < >
        //     //     conf_version: < >
        //     Dictionary<object, object> dic = new Dictionary<object, object>();
        //     dic.Add(Key_Name, $"{b.Type} Connector");
        //     dic.Add(Key_Type, $"{b.Type}");
        //     dic.Add(Key_Config, $"{b.Type}.json");
        //     if (!string.IsNullOrWhiteSpace(b.ClassName))
        //         dic.Add(Key_ClassName, b.ClassName);
        //     if (b.PackVersion != null)
        //         dic.Add(Key_PackVersion, b.PackVersion);
        //     if (b.ConfigVersion != null)
        //         dic.Add(Key_ConfigVersion, b.ConfigVersion);
        //     dic.Add(Key_UpdateAgentVersion, b.UpdateAgentVersion);
        //     return dic;
        // }
        // private static InstalledBundle fromConnector(Dictionary<object, object> connector)
        // {
        //     InstalledBundle ib = new InstalledBundle();
        //     foreach (object key in connector.Keys)
        //     {
        //         switch (key)
        //         {
        //             case Key_Type:
        //                 ib.Type = (string)connector[key];
        //                 break;
        //             case Key_UpdateAgentVersion:
        //                 ib.UpdateAgentVersion = (string)connector[key];
        //                 break;
        //             case Key_ClassName:
        //                 ib.ClassName = (string)connector[key];
        //                 break;
        //             case Key_ConfigVersion:
        //                 ib.ConfigVersion = Helper.ParseVersion(connector[key]);
        //                 break;
        //             case Key_PackVersion:
        //                 ib.PackVersion = Helper.ParseVersion(connector[key]);
        //                 break;
        //         }
        //     }
        //     return ib;
        // }
        // #endregion
    }
}