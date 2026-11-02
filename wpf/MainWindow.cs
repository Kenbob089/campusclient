using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CampusMidnight
{
    // Window shell: sign-in, sidebar, top bar and the data refresh. Pages live in Pages*.cs (same class).
    sealed partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly Settings cfg;
        Api api;
        Snapshot snap;
        Student current;
        string term = "";
        string view = "dashboard";
        bool loading;
        string stampText = "";
        bool stampBad;
        ImageSource photo, logo;
        readonly HashSet<string> photoTried = new HashSet<string>();
        bool logoTried;

        readonly Grid root = new Grid();
        readonly DispatcherTimer tick = new DispatcherTimer();
        DispatcherTimer auto;
        Border sidebar;
        ContentControl pageHost, topAvatar;
        TextBlock stampBlock, whoBlock;
        StackPanel studentSeg;
        Button termButton;
        ContentControl brandHost;
        StackPanel navPanel;
        int navLabelBase, navIconBase;
        readonly List<Grid> railIcons = new List<Grid>();
        readonly List<UIElement> railLabels = new List<UIElement>();
        readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Border> navBadges = new Dictionary<string, Border>();

        // storage keys match the WinForms app so both read the same sidebar settings
        static readonly string[][] NavAll = new string[][]
        {
            new string[] { "dashboard", "Home", "" },
            new string[] { "now", "Now", "" },
            new string[] { "grades", "Grades", "" },
            new string[] { "assignments", "Work", "" },
            new string[] { "schedule", "Schedule", "" },
            new string[] { "inbox", "Inbox", "" },
            new string[] { "docs", "Docs", "" },
            new string[] { "tools", "Tools", "" },
            new string[] { "settings", "Settings", "" }
        };

        // the sidebar buttons in the student's order, without the ones they switched off (Settings can never be hidden)
        List<string[]> NavItems(bool includeHidden)
        {
            List<string[]> result = new List<string[]>();
            List<string> order = new List<string>(cfg.NavOrder.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (string k in order)
                foreach (string[] d in NavAll)
                    if (d[0] == k && !result.Exists(delegate(string[] x) { return x[0] == k; })) result.Add(d);
            foreach (string[] d in NavAll)
            {
                string key = d[0];
                if (!result.Exists(delegate(string[] x) { return x[0] == key; })) result.Add(d);
            }
            if (includeHidden) return result;
            List<string> hide = new List<string>(cfg.NavHide.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            result.RemoveAll(delegate(string[] x) { return x[0] != "settings" && hide.Contains(x[0]); });
            return result;
        }

        void SaveNav(List<string[]> all, List<string> hidden)
        {
            List<string> keys = new List<string>();
            foreach (string[] d in all) keys.Add(d[0]);
            cfg.NavOrder = string.Join(",", keys.ToArray());
            cfg.NavHide = string.Join(",", hidden.ToArray());
            cfg.Save();
            Log.Write("sidebar changed: order=" + cfg.NavOrder + " hidden=" + cfg.NavHide);
            if (sidebar != null && navPanel != null) { BuildNavItems(); if (navHost != null && view == "settings") navHost.Refresh(); }
        }

        string ViewTitle()
        {
            switch (view)
            {
                case "dashboard": return "Home";
                case "now": return "Now";
                case "grades": return "Grades";
                case "assignments": return "Assignments";
                case "schedule": return "Schedule";
                case "inbox": return "Inbox";
                case "docs": return "Documents";
                case "tools": return "Tools";
                case "settings": return "Settings";
                case "debug": return "Diagnostics";
                case "extract": return "Export schedules";
                case "setup": return "Setup";
            }
            return "";
        }

        public MainWindow(Settings c)
        {
            cfg = c;
            Title = "Noctua";
            Width = 1280; Height = 820;
            MinWidth = 940; MinHeight = 640;
            WindowState = WindowState.Maximized;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            SetResourceReference(BackgroundProperty, "Bg0");
            Content = root;
            Log.Write("app started: theme=" + cfg.Theme + " mode=" + cfg.Mode + " dark=" + Theme.Dark);
            LoadLocalImages();
            UpdateIcon();
            ApplyTray(false);
            SourceInitialized += delegate { DarkChrome(); };
            Loaded += delegate { TryAutoLogin(); };
            Activated += delegate
            {
                if (cfg.Mode == "system" && Theme.SystemIsDark() != Theme.Dark) Dispatcher.BeginInvoke(new Action(RestyleAll));
            };
            tick.Interval = TimeSpan.FromSeconds(1);
            tick.Tick += delegate { LiveTick(); };
            tick.Start();
            ApplyInterval();
            ShowLogin(null);
        }

        // title bar, its border and its text take the page colour (Windows 11; older Windows keeps a plain dark or light bar)
        static void ChromeColors(IntPtr h)
        {
            int v = Theme.Dark ? 1 : 0;
            DwmSetWindowAttribute(h, 20, ref v, 4);
            int cap = Theme.Bg0.R | (Theme.Bg0.G << 8) | (Theme.Bg0.B << 16);
            int txt = Theme.T1.R | (Theme.T1.G << 8) | (Theme.T1.B << 16);
            DwmSetWindowAttribute(h, 35, ref cap, 4);
            DwmSetWindowAttribute(h, 34, ref cap, 4);
            DwmSetWindowAttribute(h, 36, ref txt, 4);
        }

        // dark title bar to match the theme
        void DarkChrome()
        {
            try
            {
                ChromeColors(new WindowInteropHelper(this).Handle);
            }
            catch (Exception) { }
        }

        void ApplyTheme()
        {
            Theme.NumbersOnly = cfg.NumbersOnly;
            Theme.Apply(cfg.Theme, cfg.Mode);
            DarkChrome();
        }

        void ChangeTheme(string name)
        {
            cfg.Theme = name;
            cfg.Save();
            Log.Write("accent theme -> " + name);
            RestyleInPlace();
        }

        void ChangeMode(string mode)
        {
            cfg.Mode = mode;
            cfg.Save();
            Log.Write("appearance mode -> " + mode);
            RestyleInPlace();
        }

        // colours come from dynamic resources, so only the bits drawn with fixed colours need a nudge; the page is not rebuilt
        void RestyleInPlace()
        {
            if (api == null || sidebar == null) { RestyleAll(); return; }
            ApplyTheme();
            UpdateTop();
            if (themeHost != null && view == "settings") themeHost.Refresh();
        }

        void ApplyInterval()
        {
            if (auto != null) auto.Stop();
            if (cfg.Interval <= 0) return;
            if (auto == null)
            {
                auto = new DispatcherTimer();
                auto.Tick += delegate { if (api != null) RefreshData(); };
            }
            auto.Interval = TimeSpan.FromMinutes(cfg.Interval);
            auto.Start();
        }

        // ------------------------------------------------------------ images
        static ImageSource Decode(byte[] b)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                using (MemoryStream ms = new MemoryStream(b))
                {
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                }
                bi.Freeze();
                return bi;
            }
            catch (Exception) { return null; }
        }

        static ImageSource FromFile(string path)
        {
            try { return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null; }
            catch (Exception) { return null; }
        }

        // your own files win; otherwise whatever was saved from an earlier sign-in
        void LoadLocalImages()
        {
            if (logo == null) logo = FromFile(Disk.At("logo.png")) ?? FromFile(Disk.At("logo-cache.png"));
            if (photo == null) photo = FromFile(Disk.At("photo.png"));
        }

        void LoadAssets(Api a, Student st, bool force)
        {
            if (force) { logoTried = false; photoTried.Remove(st.Id); }
            bool needPhoto = photo == null && !photoTried.Contains(st.Id);
            bool needLogo = logo == null && !logoTried;
            if (!needPhoto && !needLogo) return;
            if (needPhoto) photoTried.Add(st.Id);
            if (needLogo) logoTried = true;
            string pid = st.Id;
            Task.Run(delegate
            {
                byte[] pb = null, lb = null;
                try
                {
                    if (needPhoto)
                    {
                        string cache = Disk.At("photo-cache-" + Disk.SafeName(pid) + ".png");
                        if (File.Exists(cache)) pb = File.ReadAllBytes(cache);
                        else
                        {
                            string src;
                            pb = a.FetchPhoto(pid, out src);
                            if (pb != null) { try { File.WriteAllBytes(cache, pb); } catch (Exception) { } }
                        }
                    }
                    if (needLogo)
                    {
                        string src;
                        lb = a.FetchLogo(out src);
                        if (lb != null) { try { File.WriteAllBytes(Disk.At("logo-cache.png"), lb); } catch (Exception) { } }
                    }
                }
                catch (Exception) { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (a != api) return;
                    bool changed = false;
                    if (pb != null && photo == null) { photo = Decode(pb); changed = photo != null; }
                    if (lb != null && logo == null) { logo = Decode(lb); changed = changed || logo != null; }
                    if (changed) AfterPictureChange();
                }));
            });
        }

        // the school logo (or the moon) in a rounded tile
        UIElement LogoMark(double size)
        {
            if (logo != null)
            {
                Image im = new Image();
                im.Source = logo;
                im.Width = size; im.Height = size;
                im.Stretch = Stretch.Uniform;
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                return im;
            }
            return MoonBadge(size);
        }

        // the app's own mark: your own icon.png if you set one, otherwise the moon
        UIElement AppMark(double size)
        {
            ImageSource ic = FromFile(Disk.At("icon.png"));
            if (ic != null)
            {
                Image im = new Image();
                im.Source = ic;
                im.Width = size; im.Height = size;
                im.Stretch = Stretch.Uniform;
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                return im;
            }
            return MoonBadge(size);
        }

        // the Noctua logo: the same picture as the app icon, so the window, taskbar and the app always match
        UIElement MoonBadge(double size)
        {
            Image im = new Image();
            try { im.Source = new BitmapImage(new Uri("pack://application:,,,/app-logo.png", UriKind.Absolute)); } catch (Exception) { }
            im.Width = size; im.Height = size;
            im.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
            return im;
        }

        // the Noctua logo as PNG bytes, for saving it as the school logo or app icon
        byte[] MoonPng(int px)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                System.Windows.Resources.StreamResourceInfo ri = Application.GetResourceStream(new Uri("pack://application:,,,/app-logo.png", UriKind.Absolute));
                ri.Stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // window icon: your own icon.png if there is one, otherwise the moon
        void UpdateIcon()
        {
            ImageSource ic = FromFile(Disk.At("icon.png"));
            if (ic == null)
            {
                try { ic = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico", UriKind.Absolute)); } catch (Exception) { ic = null; }
            }
            if (ic != null) Icon = ic;
            TrayIconChanged();
        }

        // ------------------------------------------------------------ sign-in
        TextBox urlBox, userBox, cookieBox;
        PasswordBox passBox;
        CheckBox stayBox;
        TextBlock loginMsg;
        Button signInBtn;
        bool cookieMode, busy;

        void ShowLogin(string message)
        {
            root.Children.Clear();
            root.ColumnDefinitions.Clear();
            root.RowDefinitions.Clear();
            sidebar = null; pageHost = null; topAvatar = null; whoBlock = null; stampBlock = null; studentSeg = null; termButton = null;

            Grid stage = new Grid();
            stage.SetResourceReference(Grid.BackgroundProperty, "Bg0");
            Ellipse glow = new Ellipse();
            glow.Width = 720; glow.Height = 720;
            glow.HorizontalAlignment = HorizontalAlignment.Center;
            glow.VerticalAlignment = VerticalAlignment.Center;
            glow.IsHitTestVisible = false;
            RadialGradientBrush rb = new RadialGradientBrush();
            Color gc = Theme.Acc; gc.A = 64;
            Color g0 = Theme.Acc; g0.A = 0;
            rb.GradientStops.Add(new GradientStop(gc, 0));
            rb.GradientStops.Add(new GradientStop(g0, 1));
            glow.Fill = rb;
            stage.Children.Add(glow);

            StackPanel col = new StackPanel();
            col.Width = 420;
            Border card = UI.Card(col, 32);
            card.Width = 480;
            card.VerticalAlignment = VerticalAlignment.Center;
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.CornerRadius = new CornerRadius(20);

            UIElement mark = AppMark(76);
            ((FrameworkElement)mark).HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(mark);

            TextBlock h = UI.Text("Noctua", 28, "T1", true);
            h.HorizontalAlignment = HorizontalAlignment.Center;
            h.Margin = new Thickness(0, 16, 0, 4);
            col.Children.Add(h);
            TextBlock sub = UI.Text("Sign in with your Campus username and password.", 13.5, "T2", false);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            sub.Margin = new Thickness(0, 0, 0, 26);
            col.Children.Add(sub);

            urlBox = UI.Input("https://yourdistrict.infinitecampus.org/campus/portal/students/yourdistrict.jsp", cfg.Url);
            userBox = UI.Input("", cfg.User);
            passBox = new PasswordBox();
            cookieBox = UI.Input("In your browser, signed in to Campus: press F12, open Network, click any request to your Campus address, and copy the \"cookie\" request header.", "");
            cookieBox.AcceptsReturn = false;
            cookieBox.TextWrapping = TextWrapping.Wrap;
            cookieBox.Height = 100;
            cookieBox.VerticalContentAlignment = VerticalAlignment.Top;
            cookieBox.Padding = new Thickness(12, 10, 12, 10);
            userBox.Tag = "Your Campus username";
            urlBox.Height = 44; userBox.Height = 44; passBox.Height = 44;
            col.Children.Add(Field("YOUR CAMPUS SIGN-IN LINK", urlBox));
            if (!cookieMode)
            {
                col.Children.Add(Field("USERNAME", userBox));
                col.Children.Add(Field("PASSWORD", passBox));
            }
            else col.Children.Add(Field("PASTE YOUR SESSION COOKIE", cookieBox));

            stayBox = new CheckBox();
            stayBox.Content = "Stay signed in on this PC";
            stayBox.IsChecked = cfg.Stay;
            stayBox.Margin = new Thickness(0, 4, 0, 14);
            if (!cookieMode) col.Children.Add(stayBox);

            loginMsg = UI.Wrap(message ?? "", 12.5, "Bad");
            loginMsg.Margin = new Thickness(0, 0, 0, 10);
            col.Children.Add(loginMsg);

            signInBtn = UI.Btn("Sign in", "Pill", delegate { DoSignIn(); });
            signInBtn.Height = 46;
            signInBtn.FontSize = 14.5;
            col.Children.Add(signInBtn);

            Button alt = UI.Btn(cookieMode ? "Back to username and password" : "My school uses Google or Microsoft sign-in", "Flat", delegate
            {
                cfg.Url = urlBox.Text;
                cookieMode = !cookieMode;
                ShowLogin(null);
            });
            alt.Padding = new Thickness(10, 8, 10, 8);
            alt.HorizontalAlignment = HorizontalAlignment.Center;
            alt.Margin = new Thickness(0, 10, 0, 0);
            alt.Foreground = (Brush)Application.Current.Resources["AccText"];
            col.Children.Add(alt);

            TextBlock fine = UI.Wrap("Your password goes only to your school's Campus server. It is saved only if you tick Stay signed in, and then it is encrypted for this Windows account.", 11.5, "T3");
            fine.Margin = new Thickness(0, 12, 0, 0);
            fine.TextAlignment = TextAlignment.Center;
            col.Children.Add(fine);

            stage.Children.Add(UI.Scroll(card));
            ((ScrollViewer)stage.Children[1]).VerticalContentAlignment = VerticalAlignment.Center;
            stage.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter && !busy && !(e.OriginalSource is Button)) { e.Handled = true; DoSignIn(); } };
            root.Children.Add(stage);
            UI.FadeIn(card);
        }

        UIElement Field(string label, Control box)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(0, 0, 0, 16);
            p.Children.Add(UI.Margin(UI.Text(label, 11.5, "T3", true), 2, 0, 0, 6));
            p.Children.Add(box);
            return p;
        }

        void SetLoginError(string m) { if (loginMsg != null) loginMsg.Text = m ?? ""; }

        void SetBusy(bool b)
        {
            busy = b;
            if (signInBtn != null) { signInBtn.IsEnabled = !b; signInBtn.Content = b ? "Signing in…" : "Sign in"; }
        }

        void DoSignIn()
        {
            string cookie = cookieMode ? cookieBox.Text.Trim() : "";
            if (cookieMode && cookie.Length == 0) { SetLoginError("Paste your session cookie, or go back and use your username and password."); return; }
            StartSignIn(urlBox.Text, userBox.Text.Trim(), passBox.Password, cookie, stayBox.IsChecked == true);
        }

        void TryAutoLogin()
        {
            if (!cfg.Stay || string.IsNullOrEmpty(cfg.Url) || string.IsNullOrEmpty(cfg.User)) return;
            string pass = Vault.Unprotect(cfg.EncPass);
            if (pass == null) return;
            SetLoginError("Signing you in…");
            StartSignIn(cfg.Url, cfg.User, pass, "", true);
        }

        static string Friendly(Exception ex)
        {
            if (ex is WebException) return "Could not reach your school's Campus server. Check your internet connection and try again.";
            return ex.Message;
        }

        void StartSignIn(string url, string user, string pass, string cookie, bool stay)
        {
            if (busy) return;
            string origin, app, portal, err;
            if (!Api.ParseLoginUrl(url, out origin, out app, out portal, out err)) { ShowLogin(err); return; }
            bool useCookie = !string.IsNullOrEmpty(cookie);
            if (!useCookie && (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))) { ShowLogin("Enter your Campus username and password."); return; }
            SetBusy(true);
            SetLoginError("");
            string cleanUrl = url.Trim();
            Api a = new Api(origin, cleanUrl, app, portal);
            Task.Run(delegate
            {
                bool ok = false;
                string e2 = null;
                try
                {
                    if (useCookie)
                    {
                        a.UseCookieHeader(cookie);
                        ok = a.CheckAuth();
                        if (!ok) e2 = "That cookie did not work. Copy it again right after signing in, from a request to your Campus address.";
                    }
                    else ok = a.SignIn(user, pass, out e2);
                }
                catch (Exception ex) { ok = false; e2 = Friendly(ex); }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    if (ok) OnSignedIn(a, cleanUrl, user, pass, useCookie, stay);
                    else { Log.Write("sign-in failed: " + (e2 ?? "unknown")); ShowLogin(e2 ?? "Sign-in did not finish."); }
                }));
            });
        }

        void OnSignedIn(Api a, string url, string user, string pass, bool viaCookie, bool stay)
        {
            Log.Write("signed in (" + (viaCookie ? "cookie" : "password") + ")");
            if (a.IsDemo) SeedDemoSchedule();
            else if (UrlIsDemo(cfg.Url)) { ClearScheduleData(); cfg.Save(); }
            api = a;
            cfg.Url = url;
            if (!viaCookie) cfg.User = user;
            cfg.Stay = stay && !viaCookie;
            cfg.EncPass = cfg.Stay ? Vault.Protect(pass) : "";
            cfg.Save();
            snap = null; current = null; term = ""; view = "dashboard"; stampText = ""; stampBad = false;
            ShowShell();
            RefreshData();
        }

        void SignOut()
        {
            if (api != null && api.IsDemo) { ClearScheduleData(); cfg.User = ""; cfg.Url = ""; }
            api = null; snap = null; current = null;
            photo = null; photoTried.Clear();
            LoadLocalImages();
            cfg.Stay = false; cfg.EncPass = "";
            cfg.Save();
            ShowLogin(null);
        }

        // ------------------------------------------------------------ shell
        void RestyleAll()
        {
            ApplyTheme();
            if (api == null) ShowLogin(null); else ShowShell();
        }

        void ShowShell()
        {
            root.Children.Clear();
            root.ColumnDefinitions.Clear();
            root.RowDefinitions.Clear();
            navButtons.Clear();
            navBadges.Clear();
            railLabels.Clear();
            railIcons.Clear();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            root.ColumnDefinitions.Add(c0); root.ColumnDefinitions.Add(c1);

            // sidebar
            sidebar = new Border();
            sidebar.Width = cfg.RailCollapsed ? 68 : 196;
            sidebar.ClipToBounds = true;
            sidebar.SetResourceReference(Border.BackgroundProperty, "Bg1");
            sidebar.SetResourceReference(Border.BorderBrushProperty, "Line");
            sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
            DockPanel rail = new DockPanel();
            rail.Margin = new Thickness(10, 14, 10, 12);
            rail.LastChildFill = false;

            Grid brand = UI.Cols(0, 1);
            brand.Height = 48;
            brand.Margin = new Thickness(2, 0, 0, 14);
            brandHost = new ContentControl();
            brandHost.Content = LogoMark(44);
            brandHost.HorizontalAlignment = HorizontalAlignment.Left;
            UI.At(brand, brandHost, 0);
            StackPanel bn = new StackPanel();
            bn.VerticalAlignment = VerticalAlignment.Center;
            bn.Margin = new Thickness(12, 0, 0, 0);
            TextBlock b1 = UI.Text("Noctua", 17, "T1", true); b1.TextTrimming = TextTrimming.None;
            bn.Children.Add(b1);
            UI.At(brand, bn, 1);
            railLabels.Add(bn);
            DockPanel.SetDock(brand, Dock.Top);
            rail.Children.Add(brand);

            Button collapse = NavButton("Collapse", "", null);
            collapse.Click += delegate { ToggleRail(); };
            DockPanel.SetDock(collapse, Dock.Bottom);
            rail.Children.Add(collapse);

            navPanel = new StackPanel();
            navLabelBase = railLabels.Count; navIconBase = railIcons.Count;
            BuildNavItems();
            DockPanel.SetDock(navPanel, Dock.Top);
            rail.Children.Add(navPanel);
            sidebar.Child = rail;
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            // main column
            Grid main = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = new GridLength(1, GridUnitType.Star);
            main.RowDefinitions.Add(r0); main.RowDefinitions.Add(r1);

            Grid top = UI.Cols(0, 0, 0, 1, 0, 0, 0);
            top.Height = 64;
            top.Margin = new Thickness(24, 0, 18, 0);
            topAvatar = new ContentControl();
            topAvatar.VerticalAlignment = VerticalAlignment.Center;
            topAvatar.Margin = new Thickness(0, 0, 12, 0);
            UI.At(top, topAvatar, 0);
            whoBlock = UI.Text("", 15, "T1", true);
            whoBlock.VerticalAlignment = VerticalAlignment.Center;
            whoBlock.MaxWidth = 300;
            UI.At(top, whoBlock, 1);
            studentSeg = new StackPanel();
            studentSeg.Orientation = Orientation.Horizontal;
            studentSeg.Margin = new Thickness(14, 0, 0, 0);
            studentSeg.VerticalAlignment = VerticalAlignment.Center;
            UI.At(top, studentSeg, 2);
            stampBlock = UI.Text("", 12, "T3", false);
            stampBlock.VerticalAlignment = VerticalAlignment.Center;
            stampBlock.HorizontalAlignment = HorizontalAlignment.Right;
            stampBlock.Margin = new Thickness(0, 0, 12, 0);
            UI.At(top, stampBlock, 3);
            termButton = UI.Btn("", "Ghost", null);
            termButton.Padding = new Thickness(14, 7, 14, 7);
            termButton.Margin = new Thickness(0, 0, 8, 0);
            termButton.Click += delegate { OpenTermMenu(termButton); };
            UI.At(top, termButton, 5);
            Button more = UI.IconBtn("", 16, null);
            more.VerticalAlignment = VerticalAlignment.Center;
            more.Click += delegate { OpenMoreMenu(more); };
            UI.At(top, more, 6);
            Grid.SetRow(top, 0);
            main.Children.Add(top);

            pageHost = new ContentControl();
            pageHost.SizeChanged += delegate { OnHostResized(); };
            pageHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            pageHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(pageHost, 1);
            main.Children.Add(pageHost);
            Grid.SetColumn(main, 1);
            root.Children.Add(main);
            SetRailLabels();
            ShowView(true);
            UpdateBadges();
        }

        Button NavButton(string caption, string glyph, string key)
        {
            Button b = new Button();
            b.Style = (Style)Application.Current.Resources["Flat"];
            b.Height = 44;
            b.Margin = new Thickness(0, 2, 0, 2);
            b.Padding = new Thickness(0);
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Grid g = UI.Cols(0, 1, 0);
            g.ColumnDefinitions[0].Width = new GridLength(46);
            g.HorizontalAlignment = cfg.RailCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            railIcons.Add(g);
            TextBlock ic = UI.Icon(glyph, 17, "T2");
            ic.TextAlignment = TextAlignment.Center;
            UI.At(g, ic, 0);
            TextBlock lb = UI.Text(caption, 13.5, "T2", true);
            lb.TextTrimming = TextTrimming.None;
            lb.VerticalAlignment = VerticalAlignment.Center;
            UI.At(g, lb, 1);
            railLabels.Add(lb);
            if (key != null)
            {
                Border badge = new Border();
                badge.CornerRadius = new CornerRadius(9);
                badge.MinWidth = 18; badge.Height = 18;
                badge.Padding = new Thickness(5, 0, 5, 0);
                badge.Margin = new Thickness(0, 0, 8, 0);
                badge.SetResourceReference(Border.BackgroundProperty, "Acc");
                TextBlock bt = UI.Text("", 11, "T1", true);
                bt.Foreground = Brushes.White;
                bt.HorizontalAlignment = HorizontalAlignment.Center;
                bt.VerticalAlignment = VerticalAlignment.Center;
                badge.Child = bt;
                badge.Visibility = Visibility.Collapsed;
                UI.At(g, badge, 2);
                navBadges[key] = badge;
                railLabels.Add(badge);
            }
            b.Content = g;
            b.ToolTip = caption;
            return b;
        }

        void BuildNavItems()
        {
            navPanel.Children.Clear();
            if (railLabels.Count > navLabelBase) railLabels.RemoveRange(navLabelBase, railLabels.Count - navLabelBase);
            if (railIcons.Count > navIconBase) railIcons.RemoveRange(navIconBase, railIcons.Count - navIconBase);
            navButtons.Clear();
            navBadges.Clear();
            foreach (string[] n in NavItems(false))
            {
                string key = n[0];
                Button b = NavButton(n[1], n[2], key);
                b.Click += delegate { Go(key); };
                WireNavDrag(b, key);
                navButtons[key] = b;
                navPanel.Children.Add(b);
            }
            SetRailLabels();
            UpdateBadges();
            UpdateNav();
        }

        // drag a sidebar button to move it; right-click to hide it
        Point navDragStart;
        string navDragKey;

        void WireNavDrag(Button b, string key)
        {
            b.AllowDrop = true;
            b.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { navDragStart = e.GetPosition(null); navDragKey = key; };
            b.PreviewMouseMove += delegate(object s, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || navDragKey != key) return;
                Point p = e.GetPosition(null);
                if (Math.Abs(p.X - navDragStart.X) < 6 && Math.Abs(p.Y - navDragStart.Y) < 6) return;
                navDragKey = null;
                b.Opacity = 0.5;
                try { DragDrop.DoDragDrop(b, new DataObject("cmnav", key), DragDropEffects.Move); }
                finally { b.Opacity = 1; }
            };
            b.DragOver += delegate(object s, DragEventArgs e) { e.Effects = e.Data.GetDataPresent("cmnav") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            b.Drop += delegate(object s, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent("cmnav")) return;
                string srcKey = (string)e.Data.GetData("cmnav");
                e.Handled = true;
                if (srcKey == key) return;
                List<string[]> all = NavItems(true);
                int fi = all.FindIndex(delegate(string[] x) { return x[0] == srcKey; });
                int ti = all.FindIndex(delegate(string[] x) { return x[0] == key; });
                if (fi < 0 || ti < 0) return;
                string[] item = all[fi];
                all.RemoveAt(fi);
                all.Insert(ti, item);
                SaveNav(all, new List<string>(cfg.NavHide.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            };
            if (key != "settings")
            {
                b.ContextMenu = UI.Menu(new KeyValuePair<string, RoutedEventHandler>("Hide from sidebar", delegate
                {
                    List<string> hid = new List<string>(cfg.NavHide.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    if (!hid.Contains(key)) hid.Add(key);
                    SaveNav(NavItems(true), hid);
                    if (view == key) Go("dashboard");
                }), new KeyValuePair<string, RoutedEventHandler>("Reset sidebar", delegate
                {
                    cfg.NavOrder = ""; cfg.NavHide = ""; cfg.Save(); BuildNavItems();
                }));
            }
            else
            {
                b.ContextMenu = UI.Menu(new KeyValuePair<string, RoutedEventHandler>("Reset sidebar", delegate
                {
                    cfg.NavOrder = ""; cfg.NavHide = ""; cfg.Save(); BuildNavItems();
                }));
            }
        }

        void SetRailLabels()
        {
            foreach (Grid ig in railIcons)
            {
                ig.ColumnDefinitions[0].Width = new GridLength(46);
                ig.HorizontalAlignment = cfg.RailCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            }
            foreach (UIElement e in railLabels)
            {
                Border bd = e as Border;
                if (bd != null && bd.Child is TextBlock && ((TextBlock)bd.Child).Text.Length == 0) { bd.Visibility = Visibility.Collapsed; continue; }
                e.Visibility = cfg.RailCollapsed ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        void UpdateBadges()
        {
            int unread = 0;
            if (snap != null)
            {
                foreach (Msg m in snap.Messages) if (!m.Read) unread++;
                foreach (Msg m in snap.Notices) if (!m.Read) unread++;
            }
            Border b;
            if (navBadges.TryGetValue("inbox", out b))
            {
                ((TextBlock)b.Child).Text = unread > 99 ? "99+" : unread.ToString();
                b.Visibility = unread > 0 && !cfg.RailCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void ToggleRail()
        {
            cfg.RailCollapsed = !cfg.RailCollapsed;
            cfg.Save();
            if (!cfg.RailCollapsed) { SetRailLabels(); UpdateBadges(); }
            DoubleAnimation da = new DoubleAnimation(cfg.RailCollapsed ? 68 : 196, TimeSpan.FromMilliseconds(200));
            da.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (cfg.RailCollapsed) da.Completed += delegate { SetRailLabels(); UpdateBadges(); };
            sidebar.BeginAnimation(FrameworkElement.WidthProperty, da);
        }

        void Go(string key)
        {
            view = key;
            openCourse = null;
            ShowView(true);
        }

        void UpdateNav()
        {
            string navKey = view == "debug" ? "settings" : (view == "extract" ? "tools" : view);
            foreach (KeyValuePair<string, Button> kv in navButtons)
            {
                bool on = kv.Key == navKey;
                if (on) kv.Value.SetResourceReference(Button.BackgroundProperty, "AccSoft");
                else kv.Value.ClearValue(Button.BackgroundProperty);
                Grid g = (Grid)kv.Value.Content;
                ((TextBlock)g.Children[0]).SetResourceReference(TextBlock.ForegroundProperty, on ? "AccText" : "T2");
                ((TextBlock)g.Children[1]).SetResourceReference(TextBlock.ForegroundProperty, on ? "AccText" : "T2");
            }
        }

        void UpdateTop()
        {
            if (whoBlock == null) return;
            whoBlock.Text = current != null ? current.Name : "Noctua";
            topAvatar.Content = current != null && !cfg.HidePhoto ? Avatar(40) : null;
            topAvatar.Margin = new Thickness(0, 0, current != null && !cfg.HidePhoto ? 12 : 0, 0);
            stampBlock.Text = loading ? "Refreshing…" : stampText;
            stampBlock.SetResourceReference(TextBlock.ForegroundProperty, stampBad && !loading ? "Bad" : "T3");
            if (termButton != null)
            {
                bool show = false; // the term picker lives on Grades and Schedule, like the original
                termButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                termButton.Content = term;
            }
            studentSeg.Children.Clear();
            if (snap != null && snap.Students.Count > 1)
            {
                for (int i = 0; i < snap.Students.Count; i++)
                {
                    Student st = snap.Students[i];
                    bool on = current != null && st.Id == current.Id;
                    Button b = UI.Btn(st.First.Length > 0 ? st.First : st.Name, on ? "Pill" : "Ghost", delegate
                    {
                        if (current != null && current.Id == st.Id) return;
                        current = st;
                        photo = null;
                        LoadLocalImages();
                        RefreshData();
                    });
                    b.Padding = new Thickness(14, 6, 14, 6);
                    b.Margin = new Thickness(0, 0, 6, 0);
                    studentSeg.Children.Add(b);
                }
            }
        }

        void OpenTermMenu(FrameworkElement anchor)
        {
            if (snap == null) return;
            List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
            foreach (string t in snap.Terms)
            {
                string tt = t;
                items.Add(new KeyValuePair<string, RoutedEventHandler>((tt == term ? "✓  " : "     ") + tt, delegate { term = tt; UpdateTop(); ShowView(false); }));
            }
            UI.OpenMenu(anchor, UI.Menu(items.ToArray()));
        }

        void OpenMoreMenu(FrameworkElement anchor)
        {
            UI.OpenMenu(anchor, UI.Menu(
                new KeyValuePair<string, RoutedEventHandler>(loading ? "Refreshing…" : "Refresh now", delegate { if (!loading) RefreshData(); }),
                new KeyValuePair<string, RoutedEventHandler>("Open original Campus site", delegate { OpenSite(); }),
                new KeyValuePair<string, RoutedEventHandler>("-", null),
                new KeyValuePair<string, RoutedEventHandler>("#Appearance", null),
                new KeyValuePair<string, RoutedEventHandler>((cfg.Mode == "system" ? "✓  " : "     ") + "Match Windows", delegate { ChangeMode("system"); }),
                new KeyValuePair<string, RoutedEventHandler>((cfg.Mode == "light" ? "✓  " : "     ") + "Light", delegate { ChangeMode("light"); }),
                new KeyValuePair<string, RoutedEventHandler>((cfg.Mode == "dark" ? "✓  " : "     ") + "Dark", delegate { ChangeMode("dark"); }),
                new KeyValuePair<string, RoutedEventHandler>("-", null),
                new KeyValuePair<string, RoutedEventHandler>("Settings", delegate { Go("settings"); }),
                new KeyValuePair<string, RoutedEventHandler>("Sign out", delegate { Dispatcher.BeginInvoke(new Action(SignOut)); })));
        }

        // ------------------------------------------------------------ data
        void RefreshData()
        {
            if (loading || api == null) return;
            loading = true;
            UpdateTop();
            string pid = current != null ? current.Id : null;
            Api a = api;
            DateTime t0 = DateTime.Now;
            Log.Write("refresh started");
            Task.Run(delegate
            {
                Snapshot s = null;
                string err = null;
                bool authEnded = false;
                try { s = a.LoadAll(pid); }
                catch (AuthException) { authEnded = true; Log.Write("refresh: session ended"); }
                catch (Exception ex) { err = Friendly(ex); Log.Error("refresh", ex); }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    loading = false;
                    if (a != api) return;
                    if (authEnded) { SessionEnded(); return; }
                    bool openSetup = false;
                    if (s != null)
                    {
                        snap = s;
                        foreach (Course c in snap.Courses) c.Level = Rows.LevelOf(cfg, c);
                        try { CheckSchoolEnd(); } catch (Exception) { }
                        if (snap.Students.Count > 0)
                        {
                            Student found = null;
                            if (current != null) foreach (Student st in snap.Students) if (st.Id == current.Id) found = st;
                            current = found ?? snap.Students[0];
                        }
                        if (string.IsNullOrEmpty(term) || !snap.Terms.Contains(term)) term = snap.DefaultTerm();
                        stampText = "Updated " + snap.At.ToString("h:mm tt");
                        stampBad = false;
                        if (current != null) LoadAssets(a, current, false);
                        Log.Write("refresh done in " + (int)(DateTime.Now - t0).TotalMilliseconds + " ms: courses=" + snap.Courses.Count
                            + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " calendarDays=" + snap.CalDays.Count
                            + " autoBells=" + snap.AutoBells.Count + " " + snap.CalStats);
                        if (NeedsSetup() && !setupOffered) { setupOffered = true; openSetup = true; }
                    }
                    else { stampText = err ?? "Could not refresh."; stampBad = true; }
                    UpdateTop();
                    UpdateBadges();
                    if (openSetup) StartSetup(true);
                    else if (view != "setup") ShowView(false);
                }));
            });
        }

        bool setupOffered;

        void SessionEnded()
        {
            string pass = cfg.Stay ? Vault.Unprotect(cfg.EncPass) : null;
            api = null;
            if (pass != null && !string.IsNullOrEmpty(cfg.Url) && !string.IsNullOrEmpty(cfg.User))
            {
                ShowLogin("Your session ended. Signing you in again…");
                StartSignIn(cfg.Url, cfg.User, pass, "", true);
            }
            else ShowLogin("Your Campus session ended. Please sign in again.");
        }
    }
}
