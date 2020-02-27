using System;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace dmt
{
    class UpdateChecks
    {
        static readonly HttpClient client = new HttpClient();

        public static async Task checkForUpdates(string dmtPath) {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try {
                string responseBody = await client.GetStringAsync("https://bengert.dev-app01.soliantconsulting.com/dmt.txt");
                checkVersions(dmtPath, responseBody);
            }  catch(HttpRequestException e)
            {
                Console.WriteLine("Error Checking for new versions of DMT and DMTHelper");
                Console.WriteLine(e.ToString());
            }
        }

        public static void checkVersions(string dmtPath, string versions) {
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
                Console.ForegroundColor = fgColor;
            }
        }

    }
}