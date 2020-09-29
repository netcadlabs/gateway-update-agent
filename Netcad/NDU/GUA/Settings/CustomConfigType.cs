namespace Netcad.NDU.GUA.Settings
{
    public class CustomConfigType
    {
        public string ExtensionFolder { get; set; }
        public string ConfigFolder { get; set; }
        public string YamlCollectionName { get; set; }
        public string YamlFileName { get; set; }
        public string[] RestartServices { get; set; }
    }
}