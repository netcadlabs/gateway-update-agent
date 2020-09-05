namespace Netcad.NDU.GUA.Elements
{
    internal enum States : int
    {
        Uninstalled = 0,
        Installed = 1,
        Deactivated = 2,

        Downloaded = 3,

        DownloadRequired = 4,
        ActivateRequired = 5,
        UninstallRequired = 6,
        DeactivateRequired = 7
    }
}