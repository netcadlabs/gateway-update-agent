namespace Netcad.NDU.GUA.Elements
{
    internal enum States
    {
        DownloadRequired,
        ActivateRequired,
        UninstallRequired,
        DeactivateRequired,
        
        Installed,
        Deactivated,
        Uninstalled,

        Downloaded
    }
}