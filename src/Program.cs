using System;
using System.Threading;
using System.Windows.Forms;

namespace CampusMidnight
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "CampusMidnight.SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("Something went wrong:\n\n" + e.Exception.Message, "Campus Midnight",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
                Application.Run(new MainForm());
            }
        }
    }
}
