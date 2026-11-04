using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace CampusMidnight
{
    public partial class App : Application
    {
        Mutex mutex;

        // shows the error and saves it to %APPDATA%\CampusMidnight\crash.txt so a failure is never silent
        static void Report(Exception ex)
        {
            string where = "";
            try
            {
                where = Disk.At("crash.txt");
                File.WriteAllText(where, DateTime.Now.ToString("F") + "\r\n\r\n" + ex.ToString());
            }
            catch (Exception) { }
            try
            {
                MessageBox.Show("Noctua hit a problem:\n\n" + ex.Message + (where.Length > 0 ? "\n\nDetails were saved to:\n" + where : ""),
                    "Noctua", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception) { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool created;
            mutex = new Mutex(true, "CampusMidnight.SingleInstance", out created);
            if (!created) { Shutdown(); return; }
            base.OnStartup(e);
            DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs a)
            {
                Log.Error("ui", a.Exception);
                a.Handled = true;
                Report(a.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs a)
            {
                Report(a.ExceptionObject as Exception ?? new Exception("Unknown error"));
            };
            Settings cfg = Settings.Load();
            Theme.NumbersOnly = cfg.NumbersOnly;
            Theme.Apply(cfg.Theme, cfg.Mode);
            MainWindow w = new MainWindow(cfg);
            MainWindow = w;
            w.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { if (mutex != null) { mutex.ReleaseMutex(); mutex.Dispose(); } } catch (Exception) { }
            base.OnExit(e);
        }
    }
}
