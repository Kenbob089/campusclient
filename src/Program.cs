using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CampusMidnight
{
    static class Program
    {
        // Shows the error and saves it to %APPDATA%\CampusMidnight\crash.txt so a failure is never silent.
        static void Report(Exception ex)
        {
            string where = "";
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CampusMidnight");
                Directory.CreateDirectory(dir);
                where = Path.Combine(dir, "crash.txt");
                File.WriteAllText(where, DateTime.Now.ToString("F") + "\r\n\r\n" + ex.ToString());
            }
            catch (Exception) { }
            try
            {
                MessageBox.Show("Campus Midnight hit a problem:\n\n" + ex.Message
                    + (where.Length > 0 ? "\n\nDetails were saved to:\n" + where : ""),
                    "Campus Midnight", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception) { }
        }

        [STAThread]
        static void Main()
        {
            bool created = false;
            Mutex mutex = null;
            try
            {
                mutex = new Mutex(true, "CampusMidnight.SingleInstance", out created);
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Report(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Report(e.ExceptionObject as Exception ?? new Exception("Unknown error"));
                };
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Report(ex);
            }
            finally
            {
                if (mutex != null)
                {
                    if (created) { try { mutex.ReleaseMutex(); } catch (Exception) { } }
                    mutex.Dispose();
                }
            }
        }
    }
}
