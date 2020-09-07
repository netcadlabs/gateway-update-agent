using System.Collections.Generic;
using Netcad.NDU.GUA.Elements;
using Netcad.NDU.GUA.Updater;

namespace Netcad.NDU.GUA.Install
{
    internal interface IInstallManager {
        IEnumerable<UpdateResult> CheckUpdates(IEnumerable<UpdateInfo> value);
    }
}