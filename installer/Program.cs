using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace NoctuaSetup
{
    static class Program
    {
        // a crash is never silent: it is shown and saved to the temp folder
        static void Report(Exception ex)
        {
            string where = "";
            try
            {
                where = Path.Combine(Path.GetTempPath(), "NoctuaSetup-error.txt");
                File.WriteAllText(where, DateTime.Now.ToString("F") + "\r\n\r\n" + ex);
            }
            catch { }
            try { MessageBox.Show("Noctua Setup hit a problem:\n\n" + ex.Message + (where.Length > 0 ? "\n\nDetails were saved to:\n" + where : ""), "Noctua Setup", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Report(e.ExceptionObject as Exception ?? new Exception("Unknown error"));
            bool un = args.Any(a => string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase));
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += (s, e) => { e.Handled = true; Report(e.Exception); };
            app.Run(new SetupWindow(un));
        }
    }
}
