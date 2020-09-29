using System;

namespace Netcad.NDU.GUA.Settings
{
    public interface ISettings
    {
        public const string DEFAULT_CONFIG_TYPE = "default";

        string Hostname { get; }
        string Token { get; }
        double IntervalInMinutes { get; }

        string GetExtensionFolder(string configType, CustomConfigType custom_config_type);
        string GetConfigFolder(string configType, CustomConfigType custom_config_type);
        string GetYamlCollectionName(string configType, CustomConfigType custom_config_type);
        string GetYamlFileName(string configType, CustomConfigType custom_config_type);
        string[] GetRestartServices(string configType, CustomConfigType custom_config_type);

        string HistoryFolder { get; }
        Version GUAVersion { get; }

        void ListenChange(Action onChange);

        void ReloadIfRequired();
    }
}