using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace NoctuaSetup
{
    class SetupWindow : Window
    {
        static Brush Br(string h) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(h); b.Freeze(); return b; }
        static readonly Brush Bg = Br("#14161C"), Bar = Br("#14161C"), Card = Br("#1D2029"), T1 = Br("#F2F3F7"), T2 = Br("#9AA0B2"),
            Acc = Br("#7C8CFF"), AccOn = Br("#0E1020"), Bad = Br("#FF7A7A"), Warn = Br("#FFC46B"), Line = Br("#2B2F3B"), Good = Br("#6FE0A0");

        const string NS = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
        const string BtnXaml =
            "<ControlTemplate " + NS + " TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' " +
            "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'>" +
            "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.85'/></Trigger>" +
            "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.4'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate>";
        const string CloseXaml =
            "<ControlTemplate " + NS + " TargetType='Button'>" +
            "<Border x:Name='b' Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#C42B1C'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";

        enum Screen { Welcome, Source, Options, Working, Done, Failed, UConfirm }

        readonly bool uninstallMode;
        readonly StackPanel body = new StackPanel();
        readonly StackPanel footerBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock footNote = new TextBlock { FontSize = 12, Foreground = T2, VerticalAlignment = VerticalAlignment.Center };
        Screen screen;
        bool busy, removeSelf, removeData;

        // what the person chose
        bool useLocal, optDesk = true, optStart = true, optRun, optLaunch = true;
        string localPath = "";
        Version latest;          // newest on GitHub (null = not known)
        bool latestChecked;
        Version installed;       // what is installed now (null = nothing)

        public SetupWindow(bool uninstall)
        {
            uninstallMode = uninstall;
            Title = uninstall ? "Noctua Uninstall" : "Noctua Setup";
            Width = 580; Height = 490;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Bg;
            FontFamily = new FontFamily("Segoe UI");
            Foreground = T1;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 40, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            });

            var logo = LoadLogo();

            // title bar, content, footer
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });

            var title = new Grid { Background = Bar };
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.Children.Add(new Image { Source = logo, Width = 18, Height = 18, Margin = new Thickness(14, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            var tt = new TextBlock { Text = Title, FontSize = 12.5, Foreground = T1, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tt, 1);
            title.Children.Add(tt);
            var x = new Button
            {
                Width = 46, Content = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = T1 },
                Template = (ControlTemplate)XamlReader.Parse(CloseXaml), Cursor = System.Windows.Input.Cursors.Arrow
            };
            WindowChrome.SetIsHitTestVisibleInChrome(x, true);
            x.Click += (s, e) => Close();
            Grid.SetColumn(x, 2);
            title.Children.Add(x);
            grid.Children.Add(title);

            body.Margin = new Thickness(34, 14, 34, 0);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            var rule = new Border { Background = Line };
            Grid.SetRow(rule, 2);
            grid.Children.Add(rule);

            var foot = new Grid { Margin = new Thickness(34, 0, 22, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foot.Children.Add(footNote);
            Grid.SetColumn(footerBtns, 1);
            foot.Children.Add(footerBtns);
            var corner = new Image { Source = logo, Width = 38, Height = 38, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(corner, 2);
            foot.Children.Add(corner);
            Grid.SetRow(foot, 3);
            grid.Children.Add(foot);

            Content = new Border { BorderBrush = Line, BorderThickness = new Thickness(1), Child = grid };

            Closing += (s, e) => { if (busy) e.Cancel = true; };
            Closed += (s, e) => { if (removeSelf) Inst.RemoveFolderAfterExit(removeData); };

            installed = Inst.FileVer(Inst.InstalledExe ?? "");
            string beside = Inst.BesideInstaller();
            if (beside != null) { useLocal = true; localPath = beside; }

            if (uninstall) { ShowUConfirm(); return; }
            ShowWelcome();
            FetchLatest();
        }

        static BitmapImage LoadLogo()
        {
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = new Uri("pack://application:,,,/app-logo.png");
            b.DecodePixelWidth = 96;
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            b.Freeze();
            return b;
        }

        async void FetchLatest()
        {
            latest = await Inst.GetLatest();
            latestChecked = true;
            if (screen == Screen.Welcome && !busy) ShowWelcome();
            else if (screen == Screen.Source) ShowSource();
        }

        // ---- building blocks
        void Reset(Screen s) { screen = s; body.Children.Clear(); footerBtns.Children.Clear(); footNote.Text = ""; }

        TextBlock Head(string t, Brush c = null)
        {
            var b = new TextBlock { Text = t, FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = c ?? T1, Margin = new Thickness(0, 0, 0, 8) };
            body.Children.Add(b); return b;
        }

        TextBlock Txt(string t, Brush c = null, double size = 13.5, double bottom = 12)
        {
            var b = new TextBlock { Text = t, FontSize = size, Foreground = c ?? T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, bottom) };
            body.Children.Add(b); return b;
        }

        Button Btn(string t, bool primary)
        {
            return new Button
            {
                Content = t, MinWidth = 92, Padding = new Thickness(20, 8, 20, 8), FontSize = 13.5, Cursor = System.Windows.Input.Cursors.Hand,
                Background = primary ? Acc : Card, Foreground = primary ? AccOn : T1,
                BorderBrush = primary ? Acc : Line, BorderThickness = new Thickness(1),
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(10, 0, 0, 0),
                Template = (ControlTemplate)XamlReader.Parse(BtnXaml)
            };
        }

        void Foot(params Button[] bs) { foreach (var b in bs) footerBtns.Children.Add(b); }

        // a short line with a coloured note, e.g. "Noctua is open..."
        void Notice(string t, Brush c)
        {
            var b = new Border { Background = Card, BorderBrush = c, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 12) };
            b.Child = new TextBlock { Text = t, FontSize = 13, Foreground = T1, TextWrapping = TextWrapping.Wrap };
            body.Children.Add(b);
        }

        Border VerCard(string label, string value, Brush valueColor, double right)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = T2 });
            sp.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = valueColor, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            return new Border { Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, right, 0), Child = sp };
        }

        void VersionCards()
        {
            string lt = latestChecked ? (latest != null ? Inst.Show(latest) : "Can't reach GitHub") : "Checking…";
            string it = installed != null ? Inst.Show(installed) : (Inst.InstalledExe != null ? "Installed" : "Not installed");
            var g = new Grid { Margin = new Thickness(0, 6, 0, 14) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var a = VerCard("Latest on GitHub", lt, latestChecked && latest == null ? Warn : T1, 6);
            var b = VerCard("Installed on this PC", it, T1, 0);
            Grid.SetColumn(b, 1);
            Grid.SetColumn(a, 0);
            g.Children.Add(a); g.Children.Add(b);
            // the second card gets a left gap
            b.Margin = new Thickness(6, 0, 0, 0); a.Margin = new Thickness(0, 0, 6, 0);
            body.Children.Add(g);
        }

        // ---- install screens
        void ShowWelcome()
        {
            Reset(Screen.Welcome);
            bool have = Inst.InstalledExe != null;
            bool newer = have && latest != null && installed != null && latest > installed;
            bool same = have && latest != null && installed != null && latest <= installed;

            Head(!have ? "Welcome to Noctua" : newer ? "An update is available" : same ? "Noctua is up to date" : "Noctua is installed");
            Txt(!have
                ? "Setup installs Noctua just for your account. There's no administrator prompt, and it adds Noctua to your Start menu."
                : newer ? "A newer version of Noctua is ready. Your settings and data are kept."
                : same ? "You have the newest version. You can reinstall it, or remove it."
                : "Setup can reinstall or update Noctua. Your settings and data are kept.");
            VersionCards();
            if (have && Inst.IsRunning()) Notice("Noctua is open right now. Setup will close it before it updates, then you can open it again.", Warn);
            if (latestChecked && latest == null) Txt("Setup couldn't reach GitHub. You can still install from a file you already have.", T2, 13, 0);
            footNote.Text = "Installs for you only  ·  no admin needed";

            if (have)
            {
                var un = Btn("Uninstall", false);
                un.Margin = new Thickness(0);
                un.Click += (s, e) => ShowUConfirm();
                footerBtns.Children.Add(un);
                var sp = new Border { Width = 10 };
                footerBtns.Children.Add(sp);
            }
            var next = Btn(!have ? "Next" : newer ? "Update" : "Reinstall", true);
            next.Click += (s, e) => ShowSource();
            var cancel = Btn("Cancel", false);
            cancel.Click += (s, e) => Close();
            Foot(next, cancel);
        }

        void ShowSource()
        {
            Reset(Screen.Source);
            Head("Where should Noctua come from?");
            Txt("Setup can download the newest version, or use a Noctua.exe you already have.", null, 13.5, 14);

            var rbNet = new RadioButton { GroupName = "src", IsChecked = !useLocal, Foreground = T1, FontSize = 14.5, Margin = new Thickness(0, 0, 0, 2), Content = "Download the latest version (recommended)" };
            body.Children.Add(rbNet);
            string sub = latestChecked ? (latest != null ? "Version " + Inst.Show(latest) + " from GitHub." : "GitHub can't be reached right now.") : "Checking GitHub…";
            Txt(sub, latest == null && latestChecked ? Warn : T2, 12.5, 16).Margin = new Thickness(24, 0, 0, 16);

            var rbLoc = new RadioButton { GroupName = "src", IsChecked = useLocal, Foreground = T1, FontSize = 14.5, Margin = new Thickness(0, 0, 0, 6), Content = "Install from a file on this computer" };
            body.Children.Add(rbLoc);

            var pick = new StackPanel { Margin = new Thickness(24, 0, 0, 0) };
            var path = new TextBlock { FontSize = 12.5, Foreground = T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            var browse = Btn("Browse…", false);
            browse.Margin = new Thickness(0); browse.HorizontalAlignment = HorizontalAlignment.Left;
            pick.Children.Add(browse);
            pick.Children.Add(path);
            body.Children.Add(pick);

            var next = Btn("Next", true);
            var back = Btn("Back", false);
            var cancel = Btn("Cancel", false);

            Action refresh = () =>
            {
                pick.IsEnabled = rbLoc.IsChecked == true;
                pick.Opacity = pick.IsEnabled ? 1 : 0.5;
                if (rbLoc.IsChecked == true)
                {
                    if (localPath.Length == 0) { path.Text = "No file chosen yet."; path.Foreground = T2; next.IsEnabled = false; }
                    else
                    {
                        string err = Inst.CheckExe(localPath);
                        if (err != null) { path.Text = err; path.Foreground = Bad; next.IsEnabled = false; }
                        else
                        {
                            var v = Inst.FileVer(localPath);
                            string t = localPath + (v != null ? "\nVersion " + Inst.Show(v) : "");
                            if (v != null && installed != null && v < installed) t += "  (older than the " + Inst.Show(installed) + " you have, this will downgrade)";
                            path.Text = t; path.Foreground = T1; next.IsEnabled = true;
                        }
                    }
                }
                else next.IsEnabled = true;
            };
            rbNet.Checked += (s, e) => { useLocal = false; refresh(); };
            rbLoc.Checked += (s, e) => { useLocal = true; refresh(); };
            browse.Click += (s, e) =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Title = "Choose Noctua.exe", Filter = "Noctua (*.exe)|*.exe|All files|*.*" };
                try { if (localPath.Length > 0) d.InitialDirectory = Path.GetDirectoryName(localPath); } catch { }
                if (d.ShowDialog(this) == true) { localPath = d.FileName; refresh(); }
            };
            if (useLocal && localPath.Length > 0 && Inst.BesideInstaller() == localPath)
                Txt("Found a Noctua.exe next to this installer, so it's selected for you.", T2, 12, 0).Margin = new Thickness(0, 10, 0, 0);
            refresh();

            next.Click += (s, e) => ShowOptions();
            back.Click += (s, e) => ShowWelcome();
            cancel.Click += (s, e) => Close();
            Foot(back, next, cancel);
        }

        void ShowOptions()
        {
            Reset(Screen.Options);
            bool have = Inst.InstalledExe != null;
            Head("Ready to " + (have ? "update" : "install"));
            Txt("Choose what setup should add.", null, 13.5, 8);

            CheckBox Chk(string t, bool on, Action<bool> set)
            {
                var c = new CheckBox { Content = t, IsChecked = on, Foreground = T1, FontSize = 14, Margin = new Thickness(0, 4, 0, 4) };
                c.Click += (s, e) => set(c.IsChecked == true);
                body.Children.Add(c); return c;
            }
            Chk("Add a Start menu shortcut", optStart, v => optStart = v);
            Chk("Add a desktop shortcut", optDesk, v => optDesk = v);
            Chk("Start Noctua when I sign in to Windows", optRun, v => optRun = v);

            var info = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            info.Children.Add(new TextBlock { Text = "Program files:  " + Inst.AppDir, FontSize = 12, Foreground = T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
            info.Children.Add(new TextBlock { Text = "Your data:  " + Inst.DataDir + "  (kept separate, so updates never touch it)", FontSize = 12, Foreground = T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) });
            if (!Directory.Exists(Inst.DataDir) && Directory.Exists(Inst.RoamingDir))
                info.Children.Add(new TextBlock { Text = "Your existing Noctua settings will be copied over the first time Noctua opens.", FontSize = 12, Foreground = T2, TextWrapping = TextWrapping.Wrap });
            body.Children.Add(info);
            if (have && Inst.IsRunning()) { var gap = new Border { Height = 12 }; body.Children.Add(gap); Notice("Noctua is open and will be closed to update.", Warn); }

            var go = Btn(have ? "Update" : "Install", true);
            var back = Btn("Back", false);
            var cancel = Btn("Cancel", false);
            go.Click += (s, e) => RunInstall();
            back.Click += (s, e) => ShowSource();
            cancel.Click += (s, e) => Close();
            Foot(back, go, cancel);
        }

        async void RunInstall()
        {
            Reset(Screen.Working);
            bool have = Inst.InstalledExe != null;
            Head(have ? "Updating Noctua…" : "Installing Noctua…");
            var status = Txt("Getting ready…", T1, 14.5, 4);
            var detail = Txt("", T2, 12.5, 14);
            var bar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 1, Foreground = Acc, Background = Card, BorderThickness = new Thickness(0) };
            body.Children.Add(bar);
            busy = true;
            try
            {
                await Inst.Install(useLocal ? localPath : null, optDesk, optStart, optRun, (p, h, d) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    bar.Value = p; status.Text = h; detail.Text = d;
                })));
                busy = false;
                ShowDone(have);
            }
            catch (Exception ex)
            {
                busy = false;
                ShowFailed(ex.Message);
            }
        }

        void ShowDone(bool wasUpdate)
        {
            Reset(Screen.Done);
            Head(wasUpdate ? "Noctua is updated" : "Noctua is installed", Good);
            var v = Inst.FileVer(Inst.Exe);
            Txt((v != null ? "Version " + Inst.Show(v) + " is ready. " : "") + "You can find Noctua in the Start menu" + (optDesk ? " and on your desktop" : "") +
                ". To remove it, use Settings → Apps → Installed apps, or run this setup again. Noctua checks for updates itself.");
            var launch = new CheckBox { Content = "Open Noctua", IsChecked = optLaunch, Foreground = T1, FontSize = 14, Margin = new Thickness(0, 6, 0, 0) };
            body.Children.Add(launch);
            var fin = Btn("Finish", true);
            fin.Click += async (s, e) =>
            {
                if (launch.IsChecked == true)
                {
                    // start it off the UI thread so the window never freezes, then leave
                    fin.IsEnabled = false; launch.IsEnabled = false;
                    fin.Content = "Opening…";
                    busy = true;
                    await Inst.LaunchAsync();
                    await Task.Delay(500);
                    busy = false;
                }
                Close();
            };
            Foot(fin);
        }

        void ShowFailed(string msg)
        {
            Reset(Screen.Failed);
            Head("Setup could not finish", Bad);
            Txt(msg, T2);
            var again = Btn("Try again", true);
            var close = Btn("Close", false);
            again.Click += (s, e) => ShowOptions();
            close.Click += (s, e) => Close();
            Foot(again, close);
        }

        // ---- uninstall screens
        void ShowUConfirm()
        {
            Reset(Screen.UConfirm);
            Head("Remove Noctua?");
            Txt("This removes Noctua and its shortcuts from your account.");
            if (installed != null) Txt("Installed version:  " + Inst.Show(installed), T2, 13, 12);
            if (Inst.IsRunning()) Notice("Noctua is open right now. Setup will close it first.", Warn);

            var data = new CheckBox { IsChecked = removeData, Foreground = T1, FontSize = 14, Margin = new Thickness(0, 4, 0, 4) };
            data.Content = new TextBlock { Text = "Also delete my settings and saved data", TextWrapping = TextWrapping.Wrap };
            data.Click += (s, e) => removeData = data.IsChecked == true;
            body.Children.Add(data);
            string where = Directory.Exists(Inst.DataDir) ? Inst.DataDir : Inst.RoamingDir;
            body.Children.Add(new TextBlock { Text = "Stored in " + where + ". If you keep it, a later install picks up where you left off.", FontSize = 12, Foreground = T2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 0, 0, 0) });

            var go = Btn("Uninstall", true);
            var cancel = Btn(uninstallMode ? "Cancel" : "Back", false);
            go.Click += (s, e) => RunUninstall();
            cancel.Click += (s, e) => { if (uninstallMode) Close(); else ShowWelcome(); };
            Foot(go, cancel);
            footNote.Text = "";
        }

        async void RunUninstall()
        {
            Reset(Screen.Working);
            Head("Removing Noctua…");
            var status = Txt("Getting ready…", T1, 14.5, 14);
            var bar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 1, Foreground = Acc, Background = Card, BorderThickness = new Thickness(0) };
            body.Children.Add(bar);
            busy = true;
            try
            {
                await Inst.Uninstall(removeData, (p, h, d) => Dispatcher.BeginInvoke(new Action(() => { bar.Value = p; status.Text = h; })));
                removeSelf = true;
                busy = false;
                Reset(Screen.Done);
                Head("Noctua was removed", Good);
                Txt(removeData ? "Your settings and data were deleted too." : "Your settings and data were kept, in case you install Noctua again.");
                var fin = Btn("Finish", true);
                fin.Click += (s, e) => Close();
                Foot(fin);
            }
            catch (Exception ex)
            {
                busy = false;
                Reset(Screen.Failed);
                Head("Could not remove Noctua", Bad);
                Txt(ex.Message);
                var c = Btn("Close", false);
                c.Click += (s, e) => Close();
                Foot(c);
            }
        }
    }
}
