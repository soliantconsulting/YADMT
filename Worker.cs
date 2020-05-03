using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace dmt
{
    class Worker
    {
        public string file { get; private set; }
        private string source;
        private string clone;
        private string target;

        private string username;
        private string password;

        private string dmtPath;
        private string extraArgs;

        private bool running;

        private Thread t;

        private Process proc;

        public DateTime startTime { get; private set; }

        public DateTime endTime { get; private set; }

        public long fileSize { get; private set; }

        public int fileNumber { get; private set; }

        public Worker(string file, string source, string clone, string target, string username, string password, string dmtPath, string extraArgs, int fileNumber) {
            this.dmtPath = dmtPath;
            this.file = escape(file);
            this.source = escape(source);
            this.clone = escape(clone);
            this.target = escape(target);
            this.username = escape(username);
            this.password = escape(password);
            this.extraArgs = extraArgs;
            this.fileNumber = fileNumber;
        }

        private static string escape(string raw) {
            Regex pattern = new Regex("[\"]");
            return pattern.Replace(raw, "\"\"\"");
        }

        public void ThreadProc() {
            this.startTime = DateTime.Now;

            String outFileName = file + ".log";
            try {
                if (!File.Exists(outFileName)) {
                    File.Create(outFileName).Dispose();
                }

                if (!Directory.Exists("target"))
                {
                    Directory.CreateDirectory("target");
                }

                proc = new Process();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName = this.dmtPath;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.Arguments = "-src_path \""+source+"\" -clone_path \""+clone+"\" -target_path \""+target+"\"" +
                    " -src_account \""+username+"\" -src_pwd \""+password+"\" -clone_account \""+username+"\" -clone_pwd \""+password+"\" "+extraArgs;
                proc.Start();
                Console.WriteLine(file + " - " + DateTime.Now.ToString("HH:mm:ss") + " - Started");
                StreamReader reader = proc.StandardOutput;
                string line = null;


                while (!proc.HasExited) {
                    //Thread.Sleep(100);
                    line = reader.ReadLine();
                    Console.WriteLine(file + " - " + DateTime.Now.ToString("HH:mm:ss") + " - " + line);
                    File.AppendAllText(outFileName,DateTime.Now.ToString("HH:mm:ss") + "\t" + line + "\r\n");
                }
                line = reader.ReadToEnd();
                Console.WriteLine(line);
                File.AppendAllText(outFileName,line + "\r\n");

            } catch (Exception e) {
                running = false;
                Console.Beep(800, 200);
                throw e;
            }
            this.endTime = DateTime.Now;

            this.fileSize = -1;
            try {
                FileInfo info = new FileInfo(this.target);
                this.fileSize = info.Length;
            } catch (Exception) {

            }

            running = false;
            Console.Beep(800, 200);
        }

        public void Dispose()
        {
            proc.Kill();
        }

        public bool isRunning() {
            return running;
        }

        public void start() {

            this.t = new Thread(new ThreadStart(ThreadProc));
            this.t.Start();
            this.running = true;
        }
    }
}
