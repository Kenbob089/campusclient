using System;
using System.Windows;

namespace CampusMidnight
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs a)
            {
                Log.Error("ui", a.Exception);
                a.Handled = true;
            };
            Settings cfg = Settings.Load();
            Theme.NumbersOnly = cfg.NumbersOnly;
            Theme.Apply(cfg.Theme, cfg.Mode);
            MainWindow w = new MainWindow(cfg);
            w.Show();
        }
    }
}
