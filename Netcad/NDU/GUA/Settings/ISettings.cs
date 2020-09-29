using System;

namespace Netcad.NDU.GUA.Settings
{
    public interface ISettings
    {
        public const string DEFAULT_APPTYPE = "default";

        string Hostname { get; }
        string Token { get; }
        double IntervalInMinutes { get; }
        
        public string GetExtensionFolder(string appType = DEFAULT_APPTYPE);
        public string GetConfigFolder(string appType = DEFAULT_APPTYPE);
        public string GetYamlCollectionName(string appType = DEFAULT_APPTYPE);
        public string GetYamlFileName(string appType = DEFAULT_APPTYPE);
        public string[] GetRestartServices(string appType = DEFAULT_APPTYPE);

        string HistoryFolder { get; }
        Version GUAVersion { get; }

        void ListenChange(Action onChange);

        void ReloadIfRequired();
    }
}