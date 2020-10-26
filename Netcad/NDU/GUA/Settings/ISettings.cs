using System;

namespace Netcad.NDU.GUA.Settings
{
    public interface ISettings
    {
        public const string DEFAULT_APP = "default";

        string Hostname { get; }
        string Token { get; }
        double IntervalInMinutes { get; }

        string GetExtensionFolder(string app, CustomApp custom_app);
        string GetConfigFolder(string app, CustomApp custom_app);
        string GetYamlCollectionName(string app, CustomApp custom_app);
        string GetYamlFileName(string app, CustomApp custom_app);
        string[] GetRestartServices(string app, CustomApp custom_app);

        string HistoryFolder { get; }
        Version GUAVersion { get; }

        void ListenChange(Action onChange);

        void ReloadIfRequired();
    }
}