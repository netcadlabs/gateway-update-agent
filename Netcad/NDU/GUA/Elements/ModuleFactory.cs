namespace Netcad.NDU.GUA.Elements
{
    internal static class ModuleFactory
    {
        internal static IModule CreateModule()
        {
            return new Module();
        }
        internal static IModule Load(string dir)
        {
            return Module.Load(dir);
        }
    }
}