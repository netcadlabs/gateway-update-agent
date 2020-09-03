using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Netcad.NDU.GUA.Utils
{
    internal static class Shell
    {
        public static void StopTbProcess(ILogger logger)
        {
            logger.LogInformation("Stopping thingsboard-gateway.service!");
            RunCommand("sudo systemctl stop thingsboard-gateway.service");

            string res = RunCommand("sudo systemctl status thingsboard-gateway.service");
            logger.LogInformation($"Status: {res}");
        }
        public static void StartTbProcess(ILogger logger)
        {
            logger.LogInformation("Starting thingsboard-gateway.service...");
            RunCommand("sudo systemctl start thingsboard-gateway.service");

            string res = RunCommand("sudo systemctl status thingsboard-gateway.service");
            logger.LogInformation($"Status: {res}");
        }
        public static string RunCommand(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

    }
}