using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Netcad.NDU.GUA.Utils
{
    internal static class ShellHelper
    {
        public static string StopTbProcess()
        {
#if DEBUG
            return "";
#endif            
            RunCommand("sudo systemctl stop thingsboard-gateway.service");
            return RunCommand("sudo systemctl status thingsboard-gateway.service");           
        }
        public static string StartTbProcess()
        {
#if DEBUG
            return "";
#endif
            RunCommand("sudo systemctl start thingsboard-gateway.service");
            return RunCommand("sudo systemctl status thingsboard-gateway.service");            
        }
        public static string RunCommand(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
            //var escapedArgs = cmd.Replace("\"", "\"\"");

            using(var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                };

                process.Start();
                //string result = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                string res = process.StandardError.ReadToEnd();
                string err = process.StandardOutput.ReadToEnd();
                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(err))
                    throw new Exception($"Command `{cmd}` failed with exit code `{process.ExitCode}` Result: {res} Error: {err}");

                return res;
            }
        }

        public static string RunFile(string fn)
        {
            if (File.Exists(fn))
            {
                //RunCommand($"sudo chmod +x \"{fn}\"");
                //Bash($"sh \"{fn}\"");
                return RunCommand($"sh {fn}");
            }
            // else
            return string.Empty;
        }
        // private static string runCommand(string cmd)
        // {
        //     var source = new TaskCompletionSource<int>();
        //     var escapedArgs = cmd.Replace("\"", "\\\"");
        //     var process = new Process
        //     {
        //         StartInfo = new ProcessStartInfo
        //         {
        //         FileName = "bash",
        //         Arguments = $"-c \"{escapedArgs}\"",
        //         RedirectStandardOutput = true,
        //         RedirectStandardError = true,
        //         UseShellExecute = false,
        //         CreateNoWindow = true
        //         },
        //         EnableRaisingEvents = true
        //     };
        //     process.Exited += (sender, args) =>
        //     {
        //         logger.LogWarning(process.StandardError.ReadToEnd());
        //         logger.LogInformation(process.StandardOutput.ReadToEnd());
        //         if (process.ExitCode == 0)
        //         {
        //             source.SetResult(0);
        //         }
        //         else
        //         {
        //             source.SetException(new Exception($"Command `{cmd}` failed with exit code `{process.ExitCode}`"));
        //         }

        //         process.Dispose();
        //     };

        //     try
        //     {
        //         process.Start();
        //     }
        //     catch (Exception e)
        //     {
        //         logger.LogError(e, "Command {} failed", cmd);
        //         source.SetException(e);
        //     }

        //     return source.Task;
        // }

    }
}