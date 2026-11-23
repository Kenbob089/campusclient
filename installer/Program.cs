using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

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

    class SetupWindow : Window
    {
        static readonly Brush Bg = Br("#14161C"), Card = Br("#1D2029"), T1 = Br("#F2F3F7"), T2 = Br("#9AA0B2"),
            Acc = Br("#7C8CFF"), AccOn = Br("#0E1020"), Bad = Br("#FF7A7A"), Line = Br("#2B2F3B");
        static Brush Br(string h) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(h); b.Freeze(); return b; }

        const string BtnXaml =
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' " +
            "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'>" +
            "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.85'/></Trigger>" +
            "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.45'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate>";

        readonly bool un;
        readonly StackPanel body = new StackPanel();
        bool busy, removeSelf;

        public SetupWindow(bool uninstall)
        {
            un = uninstall;
            Title = un ? "Noctua Uninstall" : "Noctua Setup";
            Width = 520; Height = 430;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Bg;
            FontFamily = new FontFamily("Segoe UI");
            Foreground = T1;

            var root = new Grid { Margin = new Thickness(30, 26, 30, 24) };
            root.Children.Add(body);
            Content = root;

            Closing += (s, e) => { if (busy) e.Cancel = true; };
            Closed += (s, e) => { if (removeSelf) Inst.RemoveFolderAfterExit(); };

            if (un) AskUninstall(); else AskInstall();
        }

        // ---- small UI helpers ----
        void Clear() { body.Children.Clear(); }
        TextBlock Head(string t)
        {
            var b = new TextBlock { Text = t, FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = T1, Margin = new Thickness(0, 0, 0, 10) };
            body.Children.Add(b); return b;
        }
        TextBlock Txt(string t, Brush c = null, double size = 14)
        {
            var b = new TextBlock { Text = t, FontSize = size, Foreground = c ?? T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            body.Children.Add(b); return b;
        }
        CheckBox Chk(string t, bool on)
        {
            var c = new CheckBox { Content = t, IsChecked = on, Foreground = T1, FontSize = 14, Margin = new Thickness(0, 4, 0, 6) };
            body.Children.Add(c); return c;
        }
        Button Btn(string t, bool primary)
        {
            var b = new Button
            {
                Content = t, Padding = new Thickness(22, 9, 22, 9), FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand,
                Background = primary ? Acc : Card, Foreground = primary ? AccOn : T1,
                BorderBrush = primary ? Acc : Line, BorderThickness = new Thickness(1),
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 10, 0),
                Template = (ControlTemplate)XamlReader.Parse(BtnXaml)
            };
            return b;
        }
        StackPanel Row(params Button[] bs)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
            foreach (var b in bs) p.Children.Add(b);
            body.Children.Add(p); return p;
        }

        // ---- install ----
        void AskInstall()
        {
            bool upd = System.IO.File.Exists(Inst.Exe);
            Clear();
            Head(upd ? "Update Noctua" : "Install Noctua");
            Txt("Noctua installs just for your account, so there is no administrator prompt. Setup downloads the newest version from GitHub and adds Noctua to your Start menu.");
            Txt("Install location:  " + Inst.Dir, T2, 12);
            var desk = Chk("Add a desktop shortcut", true);
            var launch = Chk("Open Noctua when setup finishes", true);
            var go = Btn(upd ? "Update" : "Install", true);
            var cancel = Btn("Cancel", false);
            go.Click += async (s, e) => await RunInstall(desk.IsChecked == true, launch.IsChecked == true);
            cancel.Click += (s, e) => Close();
            Row(go, cancel);
        }

        async System.Threading.Tasks.Task RunInstall(bool desktop, bool launch)
        {
            Clear();
            Head("Installing…");
            var status = Txt("Preparing…");
            var bar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 1, Foreground = Acc, Background = Card, BorderThickness = new Thickness(0) };
            body.Children.Add(bar);
            busy = true;
            try
            {
                await Inst.Install(desktop, (p, t) => Dispatcher.Invoke(() => { bar.Value = p; status.Text = t; }));
                busy = false;
                Clear();
                Head("Noctua is installed");
                Txt("You can find it in the Start menu" + (desktop ? " and on your desktop" : "") + ". To remove it later, use Settings → Apps → Installed apps. Run this setup again any time to update.");
                var fin = Btn("Finish", true);
                fin.Click += (s, e) => { if (launch) Inst.Launch(); Close(); };
                Row(fin);
            }
            catch (Exception ex)
            {
                busy = false;
                Clear();
                var h = Head("Setup could not finish");
                h.Foreground = Bad;
                Txt(ex.Message);
                var again = Btn("Try again", true);
                var close = Btn("Close", false);
                again.Click += (s, e) => AskInstall();
                close.Click += (s, e) => Close();
                Row(again, close);
            }
        }

        // ---- uninstall ----
        void AskUninstall()
        {
            Clear();
            Head("Remove Noctua?");
            Txt("This removes Noctua and its shortcuts from your account.");
            var data = Chk("Also delete my settings and saved data (" + Inst.DataDir + ")", false);
            var go = Btn("Uninstall", true);
            var cancel = Btn("Cancel", false);
            go.Click += (s, e) =>
            {
                busy = true;
                try { Inst.Uninstall(data.IsChecked == true); removeSelf = true; }
                catch (Exception ex) { busy = false; Clear(); Head("Could not remove Noctua").Foreground = Bad; Txt(ex.Message); var c = Btn("Close", false); c.Click += (a, b) => Close(); Row(c); return; }
                busy = false;
                Clear();
                Head("Noctua was removed");
                Txt("Thanks for using it.");
                var fin = Btn("Finish", true);
                fin.Click += (a, b) => Close();
                Row(fin);
            };
            cancel.Click += (s, e) => Close();
            Row(go, cancel);
        }
    }
}
