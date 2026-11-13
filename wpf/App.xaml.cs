using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace CampusMidnight
{
    public partial class App : Application
    {
        Mutex mutex;

        // shows the error and saves it to %APPDATA%\Noctua\crash.txt so a failure is never silent
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

        static void AlignHint(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Controls.TextBox tb = sender as System.Windows.Controls.TextBox;
                if (tb == null || tb.Template == null) return;
                System.Windows.Controls.TextBlock ph = tb.Template.FindName("ph", tb) as System.Windows.Controls.TextBlock;
                if (ph == null) return;
                tb.ApplyTemplate();
                tb.UpdateLayout();
                Rect r = tb.GetRectFromCharacterIndex(0);
                if (r.IsEmpty || double.IsInfinity(r.Left) || double.IsNaN(r.Left)) return;
                // the hint sits at: border (1) + padding + its own margin; typing starts at r.Left
                double left = r.Left - 1 - tb.Padding.Left;
                ph.Margin = new Thickness(Math.Max(-20, Math.Min(20, left)), 0, 0, 0);
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
            // line the hint text up with where typing starts, measured from the real text position
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox), FrameworkElement.LoadedEvent, new RoutedEventHandler(AlignHint));
            Settings cfg = Settings.Load();
            Theme.NumbersOnly = cfg.NumbersOnly;
            Api.Legacy = cfg.Legacy;
            if (!cfg.Unlocked && cfg.Theme.Equals("Custom", StringComparison.OrdinalIgnoreCase)) cfg.Theme = "Midnight";
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
