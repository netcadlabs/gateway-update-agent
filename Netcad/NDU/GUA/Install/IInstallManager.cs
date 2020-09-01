using System.Collections.Generic;
using Netcad.NDU.GUA.Elements;

namespace Netcad.NDU.GUA.Install
{
    public interface IInstallManager {
        void CheckUpdates(IEnumerable<UpdateInfo> value);
    }
}