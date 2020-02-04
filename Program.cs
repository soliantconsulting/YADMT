using System;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections;

namespace dmt
{
    class Program
    {
        static void Main(string[] args)
        {
            string dmtPath = FindDMT();
            if (dmtPath == null) {
                Console.WriteLine("failed to find FMDataMigration.exe");
                return;
            }
            Console.WriteLine("Found Data Migration tool at - " + dmtPath);
            Console.Write("Extra DMT args: ");
            string extraArgs = Console.ReadLine();
            int procCnt = getProccessCount();
            Console.Write("Username: ");
            string username = Console.ReadLine();
            //Console.Write("\n");
            Console.Write("Password: ");
            string password = ReadPassword();
            Console.Write("\n");


            
            string[] files = Directory.GetFiles("source", "*.fmp12", SearchOption.TopDirectoryOnly);
            int fileIndex = 0;
            
            Worker[] workers = new Worker[procCnt];
            bool allDone = false;
            bool keepRunning = true;
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
                    if (workers[i] == null && fileIndex < files.Length) {
                        workers[i] = GetWorker(files[fileIndex++], username, password, dmtPath, extraArgs);
                        workers[i].start();
                    }
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
                    break;
                }
                Thread.Sleep(100);
            }

            done();
        }

        private static string FindDMT() {
            Console.WriteLine("Searching for DMT");
            string[] files = Directory.GetFiles(".\\", "FMDataMigration.exe", SearchOption.AllDirectories);
            if (files.Length > 0) {
                return files[0];
            }

            files = Directory.GetFiles("..\\", "FMDataMigration.exe", SearchOption.AllDirectories);
            if (files.Length > 0) {
                return files[0];
            }

            return null;
        }


        private static Worker GetWorker(string file, string username, string password, string dmtPath, string extraArgs) 
        {
            Regex r = new Regex("source\\\\(.*).fmp12", RegexOptions.IgnoreCase);
            Match m = r.Match(file);
            Group g = m.Groups[1];
            String baseName = g.ToString();
            String clone = "clone\\" + baseName + " Clone.fmp12";
            String target = "target\\" + baseName + ".fmp12";

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
            Process.Start(@"powershell", "-c (New-Object Media.SoundPlayer 'C:\\Windows\\media\\Alarm01.wav').PlaySync();");
        }
    }
}
