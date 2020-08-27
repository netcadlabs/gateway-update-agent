using System;

namespace Netcad.NDU.GatewayUpdateAgent.Settings {
    public interface ISettings {
        string Hostname { get; }
        string Token { get; }
        double IntervalInMinutes { get; }
        string ExtensionFolder { get; }
        string ConfigFolder { get; }
        string YamlFileName { get; }

        Version UpdateAgentVersion { get; }
        string TempFolder { get; }

        void ReloadIfRequired();
    }
}