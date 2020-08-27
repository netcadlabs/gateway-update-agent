using System.Collections.Generic;
using Netcad.NDU.GatewayUpdateAgent.Download;

namespace Netcad.NDU.GatewayUpdateAgent.Install {
    public interface IInstallManager {
        void CheckUpdates(IEnumerable<Bundle> value);
    }
}