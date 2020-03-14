using System;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace dmt
{
    class UpdateChecks
    {
        static readonly HttpClient client = new HttpClient();

        static string home = (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%USERPROFILE%");
        static string dmtHome = home + Path.DirectorySeparatorChar + ".DMTHelper";
        static string dmtVersionCache = dmtHome + Path.DirectorySeparatorChar + "version.txt";

        public static async Task checkForUpdates(string dmtPath) {

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string responseBody = checkCache();
            try {
                if (responseBody == null) {
                    responseBody = await client.GetStringAsync("https://bengert.dev-app01.soliantconsulting.com/dmt.txt");
                    File.WriteAllText(dmtVersionCache, responseBody, Encoding.UTF8);
                }
                checkVersions(dmtPath, responseBody);
            }  catch(HttpRequestException ) {}
        }

        private static string checkCache() {
            if (!Directory.Exists(dmtHome)) {
                Directory.CreateDirectory(dmtHome);
                return null;
            }
            if (!File.Exists(dmtVersionCache)) {
                return null;
            }
            DateTime yesterday = DateTime.Now.AddDays(-1);
            if (File.GetLastWriteTime(dmtVersionCache).CompareTo(yesterday) < 0) {
                return null;
            }
            return File.ReadAllText(dmtVersionCache, Encoding.UTF8);
        }

        private static void checkVersions(string dmtPath, string versions) {
            ConsoleColor bgColor = Console.BackgroundColor;
            ConsoleColor fgColor = Console.ForegroundColor;
            string[] versionArr = versions.Split(':');
            if (new Version(DMTHelper.VERSION) < new Version(versionArr[0])) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There is a newer version of the DMTHelper tool");
                Console.ForegroundColor = fgColor;
            }

            Process proc = new Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName = dmtPath;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();
            StreamReader reader = proc.StandardOutput;
            String dmtOut = reader.ReadToEnd();

            Regex r = new Regex(@"FMDataMigration ([0-9.]*) \(([0-9\-]*)\)", RegexOptions.IgnoreCase);
            Match m = r.Match(dmtOut);
            Group g = m.Groups[1];

            if (new Version(g.ToString()) < new Version(versionArr[1])) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("There is a newer version of FMDataMigration check the filemaker site for the new version");
                Console.WriteLine("You are running version " + g.ToString() + " the latest version is " + versionArr[1] + " and was released on " + versionArr[2]);
                Console.ForegroundColor = fgColor;
            }
        }

    }
}