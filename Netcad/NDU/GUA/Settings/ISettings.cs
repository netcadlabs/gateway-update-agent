using System;

namespace Netcad.NDU.GUA.Settings
{
    public interface ISettings
    {
        string Hostname { get; }
        string Token { get; }
        double IntervalInMinutes { get; }
        string ExtensionFolder { get; }
        string ConfigFolder { get; }
        string YamlFileName { get; }

        string HistoryFolder { get; }
        Version GUAVersion { get; }

        void ListenChange(Action onChange);

        void ReloadIfRequired();
    }
}