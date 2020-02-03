using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace dmt
{
    class Worker  : IDisposable
    {
        private string file;
        private string source;
        private string clone;
        private string target;

        private string username;
        private string password;

        private string dmtPath;

        private bool running;

        private Thread t;

        public Worker(string file, string source, string clone, string target, string username, string password, string dmtPath) {
            this.dmtPath = dmtPath;
            this.file = escape(file);
            this.source = escape(source);
            this.clone = escape(clone);
            this.target = escape(target);
            this.username = escape(username);
            this.password = escape(password);
        }

        private static string escape(string raw) {
            Regex pattern = new Regex("[\"]");
            return pattern.Replace(raw, "\"\"\"");
        }

        public void ThreadProc() {
            String outFileName = file + ".txt";
            File.Create(outFileName).Dispose();

            using (Process proc = new Process()) {
                //sw.AutoFlush = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName = this.dmtPath;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.Arguments = "-src_path \""+source+"\" -clone_path \""+clone+"\" -target_path \""+target+"\"" +
                    " -src_account \""+username+"\" -src_pwd \""+password+"\" -clone_account \""+username+"\" -clone_pwd \""+password+"\" -v -force";
                proc.Start();
                Console.WriteLine(file + " - " + DateTime.Now.ToString("HH:mm:ss") + " - Started");
                StreamReader reader = proc.StandardOutput;
                string line = null;


                while (!proc.HasExited) {
                    Thread.Sleep(100);
                    line = reader.ReadLine();
                    Console.WriteLine(file + " - " + DateTime.Now.ToString("HH:mm:ss") + " - " + line);
                    File.AppendAllText(outFileName,DateTime.Now.ToString("HH:mm:ss") + " - " + line + "\r\n");
                    
                }
            }
            running = false;
            Console.Beep(800, 200);
        }

        void IDisposable.Dispose()
        {
            
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
