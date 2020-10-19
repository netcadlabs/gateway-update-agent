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
        //**NDU-310 private const string Key_Connectors = "connectors";
        private const string Key_Type = "type";
        private const string Key_GUAVersion = "guaVersion";
        private const string Key_Name = "name";
        private const string Key_Status = "status";
        private readonly ILogger logger;
        private readonly ISettings settings;

        string guaVersion;
        public YamlManager(string guaVersion, ILogger logger, ISettings settings)
        {
            this.settings = settings;
            this.logger = logger;
            this.guaVersion = guaVersion;
        }

        private static Dictionary<string, object> _parseYaml(string fn)
        {
            if (!File.Exists(fn))
                return new Dictionary<string, object>(); //**NDU-317

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();

            using(var stream = File.OpenText(fn))
            {
                return deserializer.Deserialize<Dictionary<string, object>>(stream);
            }
        }
        private static IEnumerable<Dictionary<object, object>> _getConnectors(string fn, string yamlCollectionName)
        {
            Dictionary<string, object> yaml = _parseYaml(fn);
            foreach (string key in yaml.Keys)
                if (yamlCollectionName.Equals(key as string, StringComparison.OrdinalIgnoreCase))
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
        internal void UpdateConnectors(IModule[] arr)
        {
            lock(ioLocker)
            {
                HashSet<string> hs = new HashSet<string>();
                foreach (IModule m in arr)
                {
                    string fn = m.GetYamlFileName(this.settings);
                    if (hs.Add(fn))
                    {
                        Dictionary<string, object> yaml = _parseYaml(fn);
                        List<object> connectors = null;
                        string yamlCollectionName = m.GetYamlCollectionName(this.settings);
                        foreach (string key in yaml.Keys)
                            if (yamlCollectionName.Equals(key as string, StringComparison.OrdinalIgnoreCase))
                                connectors = yaml[key] as List<object>;
                        if (connectors == null)
                        {
                            connectors = new List<object>();
                            yaml[yamlCollectionName] = connectors;
                        }

                        for (int i = connectors.Count - 1; i >= 0; i--)
                        {
                            Dictionary<object, object> connector = (Dictionary<object, object>)connectors[i];
                            if (connector.ContainsKey(Key_GUAVersion))
                                connectors.RemoveAt(i);
                        }
                        foreach (IModule b in arr)
                            if (b.HasInstalledItem)
                                if (b.GetYamlFileName(this.settings).Equals(fn, StringComparison.OrdinalIgnoreCase))
                                    connectors.Add(toConnector(b));

                        var serializer = new SerializerBuilder().Build();
                        if (!File.Exists(fn)) //**NDU-317
                        {
                            string dir = Path.GetDirectoryName(fn);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                        }
                        File.WriteAllText(fn, serializer.Serialize(yaml));
                    }
                }
            }

            //**NDU-310 öncesi
            // lock(ioLocker)
            // {
            //     Dictionary<string, object> yaml = _parseYaml(this.fn);
            //     List<object> connectors = null;
            //     foreach (string key in yaml.Keys)
            //         if (Key_Connectors.Equals(key as string, StringComparison.OrdinalIgnoreCase))
            //             connectors = (List<object>)yaml[key];
            //     if (connectors == null)
            //     {
            //         connectors = new List<object>();
            //         yaml.Add(Key_Connectors, connectors);
            //     }

            //     for (int i = connectors.Count - 1; i >= 0; i--)
            //     {
            //         Dictionary<object, object> connector = (Dictionary<object, object>)connectors[i];
            //         if (connector.ContainsKey(Key_GUAVersion))
            //             connectors.RemoveAt(i);
            //     }
            //     foreach (IModule b in arr)
            //         if (b.HasInstalledItem)
            //             connectors.Add(toConnector(b));

            //     var serializer = new SerializerBuilder().Build();
            //     File.WriteAllText(this.fn, serializer.Serialize(yaml));
            // }
        }

        private object toConnector(IModule b)
        {
            Dictionary<object, object> dic = new Dictionary<object, object>();
            dic.Add(Key_Name, $"{b.Type} Connector");
            dic.Add(Key_Type, $"{b.Type}");
            dic.Add(Key_Status, b.Status); //**NDU-340

            foreach (var kv in b.GetYamlCustomProperties())
                dic.Add(kv.Key, kv.Value);

            dic.Add(Key_GUAVersion, b.GUAVersion);
            return dic;
        }
    }
}