using System;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;

namespace dmt
{
    class DMTHelper
    {
        static private Dictionary<string, string> getSettings(string[] args)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            if (File.Exists("DMTHelper.ini")) {
                foreach (var row in File.ReadAllLines("DMTHelper.ini"))
                {
                    if (row.IndexOf('=') > 0 && row.IndexOf(';') != 0)
                    {
                        string key = row.Split('=')[0];
                        string val = row.Substring(row.IndexOf('=') + 1);

                        if (key == "pass")
                        {
                            Console.WriteLine("INI: " + key + " : **********");
                        } else
                        {
                            Console.WriteLine("INI: " + key + " :" + val);
                        }

                        settings.Add(key, val);
                    }
                }
            }

            string dmtPath;
            if (!settings.ContainsKey("dmtPath")) {
                dmtPath = FindDMT(args);
                if (dmtPath == null) {
                    Console.WriteLine("failed to find FMDataMigration.exe");
                    System.Environment.Exit(-10);
                }
                settings.Add("dmtPath", dmtPath);
            } else {
                dmtPath = settings["dmtPath"];
            }

            Console.WriteLine("Found Data Migration tool at - " + dmtPath);
            Console.WriteLine("");

            if (!settings.ContainsKey("dmtArgs"))
            {
                Console.Write("Extra DMT args: ");
                string extraArgs = Console.ReadLine();
                settings.Add("dmtArgs", extraArgs);
            }

            if (!settings.ContainsKey("processCnt"))
            {
                int procCnt = getProccessCount();
                settings.Add("processCnt", procCnt.ToString());
            }

            if (!settings.ContainsKey("user"))
            {   
                Console.Write("Username: ");
                string username = Console.ReadLine();
                settings.Add("user", username);
            }

            if (!settings.ContainsKey("pass"))
            {
                Console.Write("Password: ");
                string password = ReadPassword();
                Console.Write("\n");
                settings.Add("pass", password);
            }

            return settings;
        }


        static void Main(string[] args)
        {
            Console.WriteLine("DMTHelper Version 0.1");
            Console.WriteLine("(c) 2020 Soliant Consulting, Inc");
            Console.WriteLine("Instructions:");
            Console.WriteLine("Needs Source, Clone, Target folders in current folder.");
            Console.WriteLine("Needs DMT exe in subfolder of current/parent/sibling folder.");
            Console.WriteLine("	You can optionally specify DMT exe path as the only argument.");
            Console.WriteLine("Automatically specifies these DMT arguments; ");
            Console.WriteLine("	-src_path, -clone_path, -target_path");
            Console.WriteLine("	-src_account, -clone_account");
            Console.WriteLine("	-src_pwd, -clone_pwd");
            Console.WriteLine("Prompts for additional DMT arguments which can be any of these;");
            Console.WriteLine("	-src_key, -clone_key");
            Console.WriteLine("	-force, ");
            Console.WriteLine("	-ignore_valuelists, -ignore_accounts, -ignore_fonts");
            Console.WriteLine("	-v, -q");
            Console.WriteLine("Prompts for process count (how many concurrent copies of DMT to run).");
            Console.WriteLine("Prompts for account name and password.");
            Console.WriteLine("Gets list of databases in Source and runs DMT for those that have a match in Clone.");
            Console.WriteLine("The files are proccessed in descending size.");
            Console.WriteLine("Creates log file for each target.");
            Console.WriteLine("");

            Dictionary<string, string> settings = getSettings(args);
            
            string[] files = Directory.GetFiles("source", "*.fmp12", SearchOption.TopDirectoryOnly);
            Array.Sort(files, (x, y) => new FileInfo(y).Length.CompareTo(new FileInfo(x).Length));
            
            int fileIndex = 0;

            int procCnt = Int32.Parse(settings["processCnt"]);
            Worker[] workers = new Worker[procCnt];
            bool allDone = false;
            bool keepRunning = true;

            // if someone hits ctrl+c kill all the workers
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
                fileIndex = files.Length;
                keepRunning = false;
                e.Cancel = true;
                
                for (int i = 0; i < procCnt; i++) {
                    if (workers[i] != null && workers[i].isRunning()) {
                        workers[i].Dispose();
                    }
                }
            };

            while (keepRunning) {
                for (int i = 0; i < procCnt; i++) {
                    //launch new workers if we can
                    if (workers[i] == null && fileIndex < files.Length) {
                        workers[i] = GetWorker(files[fileIndex++], settings["user"], settings["pass"], settings["dmtPath"], settings["dmtArgs"]);
                        workers[i].start();
                    }
                    //clean out finished workers
                    if (workers[i] != null && !workers[i].isRunning()) {
                        workers[i] = null;
                    }
                }

                if (fileIndex >= files.Length) {
                    allDone = true;
                    for (int i = 0; i < procCnt; i++) {
                        if (workers[i] != null) {
                            allDone = false;
                            break;
                        }
                    }
                }
                if (allDone) {
                    // we have finished!
                    break;
                }
                Thread.Sleep(100);
            }

            done();
        }

        private static string FindDMT(string [] args) {
            Console.WriteLine("Seperator: " + Path.DirectorySeparatorChar);

            if (args.Length > 0 && File.Exists(args[0])) {
                return args[0];
            }

            String DMTExe = "FMDataMigration.exe";
            if (Path.DirectorySeparatorChar == '/') {
                DMTExe = "FMDataMigration";
            }

            Console.WriteLine("Searching for DMT");
            string[] files = Directory.GetFiles("." + Path.DirectorySeparatorChar, DMTExe, SearchOption.AllDirectories);
            if (files.Length > 0) {
                return files[0];
            }

            files = Directory.GetFiles(".." + Path.DirectorySeparatorChar, DMTExe, SearchOption.AllDirectories);
            if (files.Length > 0) {
                return files[0];
            }

            return null;
        }


        private static Worker GetWorker(string file, string username, string password, string dmtPath, string extraArgs) 
        {
            String regex = "source\\\\(.*).fmp12";
            if (Path.DirectorySeparatorChar != '\\') {
                regex = "source/(.*).fmp12";
            }

            Regex r = new Regex(regex, RegexOptions.IgnoreCase);
            Match m = r.Match(file);
            Group g = m.Groups[1];
            String baseName = g.ToString();
            String clone = "clone" + Path.DirectorySeparatorChar + baseName + " Clone.fmp12";
            String target = "target" + Path.DirectorySeparatorChar + baseName + ".fmp12";

            if (!File.Exists(clone))
            {
                Console.WriteLine("Failed to find clone file: " + clone);
                return null;
            }

            return new Worker(baseName, file, clone, target,  username, password, dmtPath, extraArgs);
        }

        private static int getProccessCount()
        {
            bool firstRun = true;
            int result = -1;
            while (result < 1 || result > 10) {
                if (!firstRun) {
                    Console.WriteLine("Please enter a number between 1 and 10 inclusive");
                    firstRun = false;
                }
                Console.Write("Process Count: ");
                string procCnt = Console.ReadLine();
                try
                {
                    result = Int32.Parse(procCnt);
                } catch (FormatException){}
                
            }
            return result;
        }


        private enum StdHandle
        {
            Input = -10,
            Output = -11,
            Error = -12,
        }

        private enum ConsoleMode
        {
            ENABLE_ECHO_INPUT = 4
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(StdHandle nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);

        public static string ReadPassword()
        {
            if (Path.DirectorySeparatorChar == '/') {
                return Console.ReadLine();
            }
            IntPtr stdInputHandle = GetStdHandle(StdHandle.Input);
            if (stdInputHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("No console input");
            }

            int previousConsoleMode;
            if (!GetConsoleMode(stdInputHandle, out previousConsoleMode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not get console mode.");
            }

            // disable console input echo
            if (!SetConsoleMode(stdInputHandle, previousConsoleMode & ~(int)ConsoleMode.ENABLE_ECHO_INPUT))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not disable console input echo.");
            }

            // just read the password using standard Console.ReadLine()
            string password = Console.ReadLine();

            // reset console mode to previous
            if (!SetConsoleMode(stdInputHandle, previousConsoleMode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not reset console mode.");
            }

            return password;
        }

        public static void done()
        {
            if (Path.DirectorySeparatorChar == '\\') {
                Process.Start(@"powershell", "-c (New-Object Media.SoundPlayer 'C:\\Windows\\media\\Alarm01.wav').PlaySync();");
            } else {
                Console.Beep(800, 200);
            }
        }
    }
}
