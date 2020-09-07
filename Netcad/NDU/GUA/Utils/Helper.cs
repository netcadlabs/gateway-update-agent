using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
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

        public static string CombineUrl(params string[] parts)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            string result = "";
            bool inQuery = false, inFragment = false;

            string CombineEnsureSingleSeparator(string a, string b, char separator)
            {
                if (string.IsNullOrEmpty(a))return b;
                if (string.IsNullOrEmpty(b))return a;
                return a.TrimEnd(separator) + separator + b.TrimStart(separator);
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                if (result.EndsWith("?") || part.StartsWith("?"))
                    result = CombineEnsureSingleSeparator(result, part, '?');
                else if (result.EndsWith("#") || part.StartsWith("#"))
                    result = CombineEnsureSingleSeparator(result, part, '#');
                else if (inFragment)
                    result += part;
                else if (inQuery)
                    result = CombineEnsureSingleSeparator(result, part, '&');
                else
                    result = CombineEnsureSingleSeparator(result, part, '/');

                if (part.Contains("#"))
                {
                    inQuery = false;
                    inFragment = true;
                }
                else if (!inFragment && part.Contains("?"))
                {
                    inQuery = true;
                }
            }
            return EncodeIllegalCharacters(result);
        }
        private static string EncodeIllegalCharacters(string s, bool encodeSpaceAsPlus = false)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            if (encodeSpaceAsPlus)
                s = s.Replace(" ", "+");

            // Uri.EscapeUriString mostly does what we want - encodes illegal characters only - but it has a quirk
            // in that % isn't illegal if it's the start of a %-encoded sequence https://stackoverflow.com/a/47636037/62600

            // no % characters, so avoid the regex overhead
            if (!s.Contains("%"))
                return Uri.EscapeUriString(s);

            // pick out all %-hex-hex matches and avoid double-encoding 
            return Regex.Replace(s, "(.*?)((%[0-9A-Fa-f]{2})|$)", c =>
            {
                var a = c.Groups[1].Value; // group 1 is a sequence with no %-encoding - encode illegal characters
                var b = c.Groups[2].Value; // group 2 is a valid 3-character %-encoded sequence - leave it alone!
                return Uri.EscapeUriString(a) + b;
            });
        }

    }
}