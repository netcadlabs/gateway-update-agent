using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using Newtonsoft.Json;

namespace Netcad.NDU.GUA.Utils
{
    internal static class Helper
    {
        public static string ReplaceInvalidFileNameChars(string filename, string replace)
        {
            return string.Join(replace, filename.Split(Path.GetInvalidFileNameChars()));
        }
        public static string ReplaceInvalidPathChars(string path, string replace)
        {
            return string.Join(replace, path.Split(Path.GetInvalidPathChars()));
        }

        public static string[] CopyDir(string source, string destination, bool cleanDestination)
        {
            if (cleanDestination)
                DeleteDir(destination);
            //return copyDir(source, destination);
            return copyDir(new DirectoryInfo(source), new DirectoryInfo(destination)).ToArray();
        }
        private static IEnumerable<string> copyDir(DirectoryInfo source, DirectoryInfo target)
        {
            if (!target.Exists)
                target.Create();

            foreach (DirectoryInfo dir in source.GetDirectories())
                foreach (string fn in copyDir(dir, target.CreateSubdirectory(dir.Name)))
                    yield return fn;

            foreach (FileInfo file in source.GetFiles())
            {
                string fn = Path.Combine(target.FullName, file.Name);
                file.CopyTo(fn);
                yield return fn;
            }
        }
        // private static IEnumerable<string> copyDir(string source, string destination)
        // {
        //     foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        //         Directory.CreateDirectory(dirPath.Replace(source, destination));

        //     foreach (string newPath in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        //     {
        //         string fn = newPath.Replace(source, destination);
        //         File.Copy(newPath, fn, true);
        //         yield return fn;
        //     }
        // }

        // public static void CopyDirectory(string source, string destination, bool cleanDestination)
        // {
        //     if (cleanDestination)
        //         DeleteDirectory(destination);
        //     if (!Directory.Exists(destination))
        //         Directory.CreateDirectory(destination);
        //     Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(source, destination, true);
        // }
        // public static void MoveDirectory(string source, string destination, bool cleanDestination)
        // {
        //     if (cleanDestination)
        //         DeleteDirectory(destination);
        //     if (!Directory.Exists(destination))
        //         Directory.CreateDirectory(destination);
        //     Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(source, destination, true);
        // }
        public static void DeleteDir(string dir)
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
        public static void CleanDir(string dir)
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

        public static void SerializeToJsonFile<T>(T obj, string fileName)
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Objects;
            File.WriteAllText(fileName, JsonConvert.SerializeObject(obj, Formatting.Indented, settings));
        }
        public static T DeserializeFromJsonFile<T>(string fileName)
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Objects;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(fileName), settings);
        }
        public static T DeserializeFromJsonText<T>(string jsonText)
        {
            return JsonConvert.DeserializeObject<T>(jsonText);
        }
    }
}