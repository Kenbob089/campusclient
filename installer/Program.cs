using System;
using System.Linq;
using System.Windows;

namespace NoctuaSetup
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool un = args.Any(a => string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase));
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new SetupWindow(un));
        }
    }
}
