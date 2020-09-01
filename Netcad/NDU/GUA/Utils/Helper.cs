using System.IO;

namespace Netcad.NDU.GUA.Utils
{
    internal static class Helper
    {

        public static void CopyDirectory(string source, string destination, bool cleanDestination)
        {
            if (cleanDestination)
                DeleteDirectory(destination);
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);
            Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(source, destination, true);
        }
        public static void MoveDirectory(string source, string destination, bool cleanDestination)
        {
            if (cleanDestination)
                DeleteDirectory(destination);
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);
            Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(source, destination, true);
        }
        public static void DeleteDirectory(string dir)
        {
            if (System.IO.Directory.Exists(dir))
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(dir);
                foreach (FileInfo file in di.EnumerateFiles())
                    file.Delete();
                foreach (DirectoryInfo di1 in di.EnumerateDirectories())
                    di1.Delete(true);
                di.Delete();
            }
        }
        public static void CleanDirectory(string dir)
        {
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.DirectoryInfo di = new DirectoryInfo(dir);
            foreach (FileInfo file in di.EnumerateFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir1 in di.EnumerateDirectories())
            {
                dir1.Delete(true);
            }
        }

        public static int ParseVersion(object val)
        {
            return ParseInt(val, int.MinValue);
        }
        public static int ParseInt(object val, int defaultValue)
        {
            if (val is int)
                return (int)val;
            int i;
            if (!int.TryParse(val as string, out i))
                i = defaultValue;
            return i;
        }
        public static double ParseDouble(object val, double defaultValue)
        {
            if (val is double)
                return (double)val;
            double d;
            if (!double.TryParse(val as string, out d))
                d = defaultValue;
            return d;
        }
    }
}