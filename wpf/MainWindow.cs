using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
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
        string view = "home";
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
        ContentControl pageHost;
        TextBlock titleText, stampBlock;
        readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();

        static readonly string[][] Nav = new string[][]
        {
            new string[] { "home", "Home", "" },
            new string[] { "now", "Now", "" },
            new string[] { "grades", "Grades", "" },
            new string[] { "assigns", "Assignments", "" },
            new string[] { "sched", "Schedule", "" },
            new string[] { "settings", "Settings", "" },
            new string[] { "diag", "Diagnostics", "" }
        };

        public MainWindow(Settings c)
        {
            cfg = c;
            Title = "Campus Midnight";
            Width = 1180; Height = 780;
            MinWidth = 860; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            SetResourceReference(BackgroundProperty, "Bg0");
            Content = root;
            try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico", UriKind.Absolute)); } catch (Exception) { }
            SourceInitialized += delegate { DarkChrome(); };
            Loaded += delegate { TryAutoLogin(); };
            tick.Interval = TimeSpan.FromSeconds(1);
            tick.Tick += delegate { LiveTick(); };
            tick.Start();
            LoadLocalImages();
            ShowLogin(null);
        }

        // dark title bar to match the theme
        void DarkChrome()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                int v = Theme.Dark ? 1 : 0;
                DwmSetWindowAttribute(h, 20, ref v, 4);
            }
            catch (Exception) { }
        }

        void ApplyTheme()
        {
            Theme.NumbersOnly = cfg.NumbersOnly;
            Theme.Apply(cfg.Theme, cfg.Mode);
            DarkChrome();
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

        void LoadLocalImages()
        {
            if (logo == null) logo = FromFile(Disk.At("logo.png")) ?? FromFile(Disk.At("logo-cache.png"));
            if (photo == null) photo = FromFile(Disk.At("photo.png"));
        }

        void LoadAssets(Api a, Student st)
        {
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
                    if (changed) ShowView(false);
                }));
            });
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
            sidebar = null; pageHost = null;

            Grid stage = new Grid();
            stage.SetResourceReference(Grid.BackgroundProperty, "Bg0");
            // soft accent glow behind the card
            Ellipse glow = new Ellipse();
            glow.Width = 620; glow.Height = 620;
            glow.HorizontalAlignment = HorizontalAlignment.Center;
            glow.VerticalAlignment = VerticalAlignment.Center;
            glow.IsHitTestVisible = false;
            RadialGradientBrush rb = new RadialGradientBrush();
            Color gc = Theme.Acc; gc.A = 70;
            Color g0 = Theme.Acc; g0.A = 0;
            rb.GradientStops.Add(new GradientStop(gc, 0));
            rb.GradientStops.Add(new GradientStop(g0, 1));
            glow.Fill = rb;
            stage.Children.Add(glow);

            StackPanel col = new StackPanel();
            col.Width = 380;
            col.VerticalAlignment = VerticalAlignment.Center;
            col.HorizontalAlignment = HorizontalAlignment.Center;

            Border badge = new Border();
            badge.Width = 76; badge.Height = 76;
            badge.CornerRadius = new CornerRadius(22);
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            LinearGradientBrush lg = new LinearGradientBrush(Theme.AccHover, Theme.AccDown, 60);
            badge.Background = lg;
            badge.Child = UI.Icon("", 36, "T1");
            ((TextBlock)badge.Child).Foreground = Brushes.White;
            col.Children.Add(badge);

            TextBlock h = UI.Text("Campus Midnight", 26, "T1", true);
            h.HorizontalAlignment = HorizontalAlignment.Center;
            h.Margin = new Thickness(0, 18, 0, 4);
            col.Children.Add(h);
            TextBlock sub = UI.Text("Sign in to your Infinite Campus portal", 13, "T3", false);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            sub.Margin = new Thickness(0, 0, 0, 22);
            col.Children.Add(sub);

            urlBox = new TextBox(); urlBox.Text = cfg.Url;
            userBox = new TextBox(); userBox.Text = cfg.User;
            passBox = new PasswordBox();
            cookieBox = new TextBox();
            cookieBox.AcceptsReturn = false;
            col.Children.Add(Field("Campus address", urlBox));
            if (!cookieMode)
            {
                col.Children.Add(Field("Username", userBox));
                col.Children.Add(Field("Password", passBox));
            }
            else col.Children.Add(Field("Session cookie", cookieBox));

            stayBox = new CheckBox();
            stayBox.Content = "Keep me signed in";
            stayBox.IsChecked = cfg.Stay;
            stayBox.Margin = new Thickness(0, 4, 0, 14);
            if (!cookieMode) col.Children.Add(stayBox);

            loginMsg = UI.Wrap(message ?? "", 12.5, "Bad");
            loginMsg.Margin = new Thickness(0, 0, 0, 10);
            col.Children.Add(loginMsg);

            signInBtn = UI.Btn("Sign in", "Pill", delegate { DoSignIn(); });
            signInBtn.Padding = new Thickness(18, 12, 18, 12);
            signInBtn.FontSize = 14;
            col.Children.Add(signInBtn);

            Button alt = UI.Btn(cookieMode ? "Use username and password" : "Use a session cookie instead", "Flat", delegate
            {
                cookieMode = !cookieMode;
                string keep = urlBox.Text;
                cfg.Url = keep;
                ShowLogin(null);
            });
            alt.Padding = new Thickness(10, 8, 10, 8);
            alt.HorizontalAlignment = HorizontalAlignment.Center;
            alt.Margin = new Thickness(0, 10, 0, 0);
            col.Children.Add(alt);

            stage.Children.Add(col);
            stage.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter && !busy) DoSignIn(); };
            root.Children.Add(stage);
            UI.FadeIn(col);
        }

        UIElement Field(string label, Control box)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(0, 0, 0, 12);
            p.Children.Add(UI.Margin(UI.Text(label, 12, "T3", true), 2, 0, 0, 5));
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
            api = a;
            cfg.Url = url;
            if (!viaCookie) cfg.User = user;
            cfg.Stay = stay && !viaCookie;
            cfg.EncPass = cfg.Stay ? Vault.Protect(pass) : "";
            cfg.Save();
            snap = null; current = null; term = ""; view = "home"; stampText = ""; stampBad = false;
            ShowShell();
            RefreshData();
            StartAutoRefresh();
        }

        void SignOut()
        {
            api = null; snap = null; current = null;
            photo = null; photoTried.Clear();
            LoadLocalImages();
            cfg.Stay = false; cfg.EncPass = "";
            cfg.Save();
            if (auto != null) auto.Stop();
            ShowLogin(null);
        }

        void StartAutoRefresh()
        {
            if (auto != null) auto.Stop();
            if (cfg.Interval <= 0) return;
            auto = new DispatcherTimer();
            auto.Interval = TimeSpan.FromMinutes(cfg.Interval);
            auto.Tick += delegate { RefreshData(); };
            auto.Start();
        }

        // ------------------------------------------------------------ shell
        void ShowShell()
        {
            root.Children.Clear();
            root.ColumnDefinitions.Clear();
            root.RowDefinitions.Clear();
            navButtons.Clear();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            root.ColumnDefinitions.Add(c0); root.ColumnDefinitions.Add(c1);

            // sidebar
            sidebar = new Border();
            sidebar.Width = cfg.RailCollapsed ? 68 : 224;
            sidebar.ClipToBounds = true;
            sidebar.SetResourceReference(Border.BackgroundProperty, "Bg1");
            sidebar.SetResourceReference(Border.BorderBrushProperty, "Line");
            sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
            StackPanel rail = new StackPanel();
            rail.Margin = new Thickness(10, 14, 10, 10);

            Button toggle = new Button();
            toggle.Style = (Style)Application.Current.Resources["Flat"];
            toggle.Padding = new Thickness(0);
            toggle.Height = 44;
            Grid th = UI.Cols(0, 1);
            Border mark = new Border();
            mark.Width = 38; mark.Height = 38; mark.CornerRadius = new CornerRadius(11);
            mark.Background = new LinearGradientBrush(Theme.AccHover, Theme.AccDown, 60);
            TextBlock mi = UI.Icon("", 18, "T1"); mi.Foreground = Brushes.White;
            mark.Child = mi;
            mark.Margin = new Thickness(3, 0, 0, 0);
            UI.At(th, mark, 0);
            TextBlock nm = UI.Text("Campus Midnight", 14.5, "T1", true);
            nm.VerticalAlignment = VerticalAlignment.Center;
            nm.Margin = new Thickness(12, 0, 0, 0);
            nm.TextTrimming = TextTrimming.None;
            UI.At(th, nm, 1);
            toggle.Content = th;
            toggle.Click += delegate { ToggleRail(); };
            rail.Children.Add(toggle);
            rail.Children.Add(UI.Margin(new Border(), 0, 8, 0, 0));

            foreach (string[] n in Nav)
            {
                string key = n[0];
                Button b = new Button();
                b.Style = (Style)Application.Current.Resources["Flat"];
                b.Height = 42;
                b.Margin = new Thickness(0, 2, 0, 2);
                b.Padding = new Thickness(0);
                Grid g = UI.Cols(0, 1);
                TextBlock ic = UI.Icon(n[2], 17, "T2");
                ic.Width = 44;
                UI.At(g, ic, 0);
                TextBlock lb = UI.Text(n[1], 13.5, "T2", true);
                lb.TextTrimming = TextTrimming.None;
                lb.VerticalAlignment = VerticalAlignment.Center;
                UI.At(g, lb, 1);
                b.Content = g;
                b.Click += delegate { Go(key); };
                b.ToolTip = n[1];
                navButtons[key] = b;
                rail.Children.Add(b);
            }
            sidebar.Child = rail;
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            // main column
            Grid main = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = new GridLength(1, GridUnitType.Star);
            main.RowDefinitions.Add(r0); main.RowDefinitions.Add(r1);

            Grid top = UI.Cols(1, 0, 0, 0);
            top.Margin = new Thickness(30, 18, 18, 6);
            titleText = UI.Text("", 26, "T1", true);
            titleText.HorizontalAlignment = HorizontalAlignment.Left;
            UI.At(top, titleText, 0);
            stampBlock = UI.Text("", 12, "T3", false);
            stampBlock.VerticalAlignment = VerticalAlignment.Center;
            stampBlock.Margin = new Thickness(0, 0, 10, 0);
            UI.At(top, stampBlock, 1);
            Button termBtn = UI.Btn("", "Ghost", null);
            termBtn.Name = "termBtn";
            termBtn.Padding = new Thickness(14, 7, 14, 7);
            termBtn.Margin = new Thickness(0, 0, 8, 0);
            termBtn.Click += delegate { OpenTermMenu(termBtn); };
            UI.At(top, termBtn, 2);
            Button more = UI.IconBtn("", 16, null);
            more.Click += delegate { OpenMoreMenu(more); };
            UI.At(top, more, 3);
            Grid.SetRow(top, 0);
            main.Children.Add(top);

            pageHost = new ContentControl();
            pageHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            pageHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(pageHost, 1);
            main.Children.Add(pageHost);
            Grid.SetColumn(main, 1);
            root.Children.Add(main);
            termButton = termBtn;
            ShowView(true);
            UpdateTop();
        }

        Button termButton;

        void ToggleRail()
        {
            cfg.RailCollapsed = !cfg.RailCollapsed;
            cfg.Save();
            DoubleAnimation da = new DoubleAnimation(cfg.RailCollapsed ? 68 : 224, TimeSpan.FromMilliseconds(220));
            da.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
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
            foreach (KeyValuePair<string, Button> kv in navButtons)
            {
                bool on = kv.Key == view;
                if (on) kv.Value.SetResourceReference(Button.BackgroundProperty, "AccSoft");
                else kv.Value.ClearValue(Button.BackgroundProperty);
                Grid g = (Grid)kv.Value.Content;
                ((TextBlock)g.Children[0]).SetResourceReference(TextBlock.ForegroundProperty, on ? "AccText" : "T2");
                ((TextBlock)g.Children[1]).SetResourceReference(TextBlock.ForegroundProperty, on ? "AccText" : "T2");
            }
        }

        void UpdateTop()
        {
            if (titleText == null) return;
            string t = "";
            foreach (string[] n in Nav) if (n[0] == view) t = n[1];
            if (view == "grades" && openCourse != null) t = openCourse.Name;
            titleText.Text = t;
            stampBlock.Text = loading ? "Refreshing…" : stampText;
            stampBlock.SetResourceReference(TextBlock.ForegroundProperty, stampBad ? "Bad" : "T3");
            if (termButton != null)
            {
                bool show = snap != null && snap.Terms.Count > 1;
                termButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                termButton.Content = term;
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
                new KeyValuePair<string, RoutedEventHandler>("Refresh now", delegate { RefreshData(); }),
                new KeyValuePair<string, RoutedEventHandler>("Open settings", delegate { Go("settings"); }),
                new KeyValuePair<string, RoutedEventHandler>("Sign out", delegate { SignOut(); })));
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
                    if (s != null)
                    {
                        snap = s;
                        foreach (Course c in snap.Courses) c.Level = Rows.LevelOf(cfg, c);
                        if (snap.Students.Count > 0)
                        {
                            Student found = null;
                            if (current != null) foreach (Student st in snap.Students) if (st.Id == current.Id) found = st;
                            current = found ?? snap.Students[0];
                        }
                        if (string.IsNullOrEmpty(term) || !snap.Terms.Contains(term)) term = snap.DefaultTerm();
                        stampText = "Updated " + snap.At.ToString("h:mm tt");
                        stampBad = false;
                        if (current != null) LoadAssets(a, current);
                        Log.Write("refresh done in " + (int)(DateTime.Now - t0).TotalMilliseconds + " ms: courses=" + snap.Courses.Count
                            + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " calendarDays=" + snap.CalDays.Count);
                    }
                    else { stampText = err ?? "Could not refresh."; stampBad = true; }
                    UpdateTop();
                    ShowView(false);
                }));
            });
        }

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
