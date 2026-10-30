using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace CampusMidnight
{
    partial class MainForm : Form
    {
        Api api;
        Snapshot snap;
        readonly Settings cfg;
        string view = "dashboard";
        string term = "";
        Student current;
        bool loading, busy, cookieMode, stampBad;
        string stampText = "";

        ScrollPanel login, host;
        Panel shell, rail, top;
        readonly List<NavButton> navs = new List<NavButton>();
        readonly List<string> navKeys = new List<string>();
        Label lblWho, lblStamp;
        Segmented segStudents;
        PillButton btnRefresh, btnSite, btnOut, btnMode;
        NavButton railToggle;
        int railW;
        string lastKey;
        System.Windows.Forms.Timer liveTimer, railTimer;

        LoginCard card;
        Label lblBrand, lblIntro, lblUrlCap, lblUserCap, lblPassCap, lblCookieCap, lblError, lnkSso, lblFine;
        DarkInput urlBox, userBox, passBox, cookieBox;
        DarkCheck chkStay;
        PillButton btnSignIn;

        System.Windows.Forms.Timer resizeTimer, autoTimer;

        Bitmap photo, logo;
        string photoNote = "", logoNote = "";
        bool logoTried;
        readonly HashSet<string> photoTried = new HashSet<string>();

        public MainForm()
        {
            Theme.Init();
            cfg = Settings.Load();
            Theme.Apply(cfg.Theme, cfg.Mode);
            railW = cfg.RailCollapsed ? Theme.S(68) : Theme.S(196);
            LoadLocalImages();
            Text = "Campus Midnight";
            BackColor = Theme.Bg0;
            ForeColor = Theme.T1;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.S(1240), Theme.S(800));
            MinimumSize = new Size(Theme.S(940), Theme.S(660));
            StartPosition = FormStartPosition.CenterScreen;
            UpdateIcon();

            // timers first: building the shell raises Resize events that use them
            resizeTimer = new System.Windows.Forms.Timer();
            resizeTimer.Interval = 140;
            resizeTimer.Tick += delegate
            {
                resizeTimer.Stop();
                if (shell.Visible && snap != null) ShowView(true);
            };
            autoTimer = new System.Windows.Forms.Timer();
            autoTimer.Tick += delegate { if (api != null && shell.Visible) RefreshData(); };
            ApplyInterval();
            liveTimer = new System.Windows.Forms.Timer();
            liveTimer.Interval = 1000;
            liveTimer.Tick += delegate { LiveTick(); };
            liveTimer.Start();
            railTimer = new System.Windows.Forms.Timer();
            railTimer.Interval = 15;
            railTimer.Tick += delegate { RailStep(); };

            BuildShell();
            BuildLogin();
            Controls.Add(shell);
            Controls.Add(login);

            ShowLogin(null);
            Shown += delegate { TryAutoLogin(); };
            // follow Windows' light/dark setting when it changes while the app is open
            Activated += delegate
            {
                if (cfg.Mode == "system" && Theme.SystemIsDark() != Theme.Dark) BeginInvoke(new MethodInvoker(delegate { RebuildUi(); }));
            };
        }

        void LiveTick()
        {
            if (shell == null || !shell.Visible || snap == null) return;
            if (liveHero != null) liveHero.Invalidate();
            if (liveStrip != null) liveStrip.Invalidate();
            if (liveTimeline != null) liveTimeline.Invalidate();
            if (plan == null || (view != "now" && view != "dashboard")) return;
            string key = Sched.StateKey(plan, DateTime.Now.TimeOfDay);
            if (lastKey == null) { lastKey = key; return; }
            if (key != lastKey) { lastKey = key; ShowView(true); }
        }

        // widens or narrows the sidebar a few pixels per tick
        void RailStep()
        {
            int target = cfg.RailCollapsed ? Theme.S(68) : Theme.S(196);
            int step = Math.Max(1, Theme.S(22));
            if (railW < target) railW = Math.Min(target, railW + step);
            else if (railW > target) railW = Math.Max(target, railW - step);
            bool wide = railW >= Theme.S(140);
            foreach (NavButton nb in navs) nb.Expanded = wide;
            if (railToggle != null) railToggle.Expanded = wide;
            LayoutShell();
            rail.Invalidate();
            if (railW == target)
            {
                railTimer.Stop();
                ShowView(true);
            }
        }

        void ToggleRail()
        {
            cfg.RailCollapsed = !cfg.RailCollapsed;
            cfg.Save();
            railTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (resizeTimer != null) resizeTimer.Dispose();
                if (autoTimer != null) autoTimer.Dispose();
                if (liveTimer != null) liveTimer.Dispose();
                if (railTimer != null) railTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        // the window and taskbar icon: the school logo when it is square enough, else the icon built into the exe, else a drawn "C"
        void UpdateIcon()
        {
            Icon ic = Draw.MakeIcon(logo);
            if (ic == null)
            {
                try { ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch (Exception) { ic = null; }
            }
            if (ic == null) ic = DrawnIcon();
            if (ic != null) Icon = ic;
        }

        static Icon DrawnIcon()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath p = Theme.Round(new Rectangle(1, 1, 30, 30), 8))
                    using (SolidBrush b = new SolidBrush(Theme.Acc))
                    {
                        g.FillPath(b, p);
                    }
                    using (Font f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        TextRenderer.DrawText(g, "C", f, new Rectangle(0, 0, 32, 32), Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                    return Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch (Exception) { return null; }
        }

        // ================================================================ shell
        void BuildShell()
        {
            shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = Theme.Bg0;
            rail = new Panel();
            rail.BackColor = Theme.Bg1;
            top = new Panel();
            top.BackColor = Theme.Bg0;
            host = new ScrollPanel();
            shell.Controls.Add(host);
            shell.Controls.Add(top);
            shell.Controls.Add(rail);

            string[,] items = new string[,]
            {
                { "dashboard", "Home", "\uE80F" },
                { "now", "Now", "\uE916" },
                { "grades", "Grades", "\uE7BE" },
                { "assignments", "Work", "\uE73A" },
                { "schedule", "Schedule", "\uE787" },
                { "inbox", "Inbox", "\uE715" },
                { "docs", "Docs", "\uE8A5" },
                { "tools", "Tools", "\uE8EF" },
                { "settings", "Settings", "\uE713" }
            };
            for (int i = 0; i < items.GetLength(0); i++)
            {
                NavButton nb = new NavButton();
                nb.Caption = items[i, 1];
                nb.Glyph = items[i, 2];
                string key = items[i, 0];
                nb.Expanded = railW >= Theme.S(140);
                nb.Click += delegate { view = key; ShowView(false); };
                navs.Add(nb);
                navKeys.Add(key);
                rail.Controls.Add(nb);
            }
            railToggle = new NavButton();
            railToggle.Caption = "Collapse";
            railToggle.Glyph = "\uE700";
            railToggle.Expanded = railW >= Theme.S(140);
            railToggle.Click += delegate { ToggleRail(); };
            rail.Controls.Add(railToggle);
            rail.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool wide = railW >= Theme.S(140);
                int ls = Theme.S(44);
                Draw.Logo(g, logo, new Rectangle(wide ? Theme.S(16) : (rail.Width - ls) / 2, Theme.S(18), ls, ls));
                if (wide)
                {
                    int tx = Theme.S(16) + ls + Theme.S(12);
                    Theme.DrawText(g, "Campus", Theme.Semi, Theme.T1, new Rectangle(tx, Theme.S(18), rail.Width - tx - Theme.S(8), Theme.S(22)));
                    Theme.DrawText(g, "Midnight", Theme.Small, Theme.T3, new Rectangle(tx, Theme.S(40), rail.Width - tx - Theme.S(8), Theme.S(20)));
                }
            };
            top.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (current == null) return;
                int d = Theme.S(40);
                Draw.Avatar(e.Graphics, photo, current.Name, new Rectangle(Theme.S(24), (top.Height - d) / 2, d, d));
            };

            lblWho = new Label();
            lblWho.AutoSize = false;
            lblWho.UseMnemonic = false;
            lblWho.AutoEllipsis = true;
            lblWho.Font = new Font("Segoe UI Semibold", 12f);
            lblWho.ForeColor = Theme.T1;
            lblWho.BackColor = Theme.Bg0;
            lblWho.TextAlign = ContentAlignment.MiddleLeft;
            lblStamp = new Label();
            lblStamp.AutoSize = false;
            lblStamp.UseMnemonic = false;
            lblStamp.Font = Theme.Small;
            lblStamp.ForeColor = Theme.T3;
            lblStamp.BackColor = Theme.Bg0;
            lblStamp.TextAlign = ContentAlignment.MiddleRight;

            segStudents = new Segmented();
            segStudents.Visible = false;
            segStudents.Changed += delegate
            {
                if (snap == null || segStudents.Selected < 0 || segStudents.Selected >= snap.Students.Count) return;
                current = snap.Students[segStudents.Selected];
                RefreshData();
            };

            btnRefresh = new PillButton();
            btnRefresh.Text = "Refresh";
            btnRefresh.Glyph = "\uE72C";
            btnRefresh.Click += delegate { RefreshData(); };
            btnSite = new PillButton();
            btnSite.Text = "Original site";
            btnSite.Glyph = "\uE8A7";
            btnSite.Kind = 2;
            btnSite.Click += delegate { OpenSite(); };
            btnMode = new PillButton();
            btnMode.Text = "";
            btnMode.Glyph = Theme.Dark ? "\uE708" : "\uE706";
            btnMode.Kind = 2;
            btnMode.Click += delegate
            {
                string[] modeKeys = new string[] { "system", "light", "dark" };
                DarkMenu.Pick(btnMode, new string[] { "Match Windows", "Light", "Dark" }, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)),
                    delegate(int k) { BeginInvoke(new MethodInvoker(delegate { ChangeMode(modeKeys[k]); })); });
            };
            btnOut = new PillButton();
            btnOut.Text = "Sign out";
            btnOut.Glyph = "\uE7E8";
            btnOut.Kind = 3;
            btnOut.Click += delegate { SignOut(); };

            top.Controls.Add(lblWho);
            top.Controls.Add(segStudents);
            top.Controls.Add(lblStamp);
            top.Controls.Add(btnRefresh);
            top.Controls.Add(btnMode);
            top.Controls.Add(btnSite);
            top.Controls.Add(btnOut);

            shell.Resize += delegate
            {
                LayoutShell();
                if (resizeTimer != null)
                {
                    resizeTimer.Stop();
                    resizeTimer.Start();
                }
            };
        }

        void LayoutShell()
        {
            int rw = railW, th = Theme.S(64);
            rail.SetBounds(0, 0, rw, shell.Height);
            top.SetBounds(rw, 0, Math.Max(0, shell.Width - rw), th);
            host.SetBounds(rw, th, Math.Max(0, shell.Width - rw), Math.Max(0, shell.Height - th));
            int bw = rw - Theme.S(20), bh = Theme.S(48);
            int y0 = Theme.S(84);
            for (int i = 0; i < navs.Count; i++)
                navs[i].SetBounds(Theme.S(10), y0 + i * (bh + Theme.S(4)), bw, bh);
            if (railToggle != null) railToggle.SetBounds(Theme.S(10), Math.Max(y0 + navs.Count * (bh + Theme.S(4)) + Theme.S(8), shell.Height - bh - Theme.S(14)), bw, bh);
            LayoutTop();
        }

        void LayoutTop()
        {
            int pad = Theme.S(24), h = top.Height, bh = Theme.S(38);
            int y = (h - bh) / 2;
            int x = top.Width - pad;
            int wOut = btnOut.PreferredWidth(), wSite = btnSite.PreferredWidth(), wRef = Math.Max(btnRefresh.PreferredWidth(), Theme.S(112));
            btnOut.SetBounds(x - wOut, y, wOut, bh);
            x -= wOut + Theme.S(8);
            btnSite.SetBounds(x - wSite, y, wSite, bh);
            x -= wSite + Theme.S(8);
            btnRefresh.SetBounds(x - wRef, y, wRef, bh);
            x -= wRef + Theme.S(8);
            btnMode.SetBounds(x - Theme.S(46), y, Theme.S(46), bh);
            x -= Theme.S(46) + Theme.S(14);
            lblStamp.SetBounds(Math.Max(0, x - Theme.S(230)), 0, Theme.S(230), h);
            int avatarW = current != null ? Theme.S(52) : 0;
            int ww = Math.Min(Theme.S(260), Theme.Measure(lblWho.Text, lblWho.Font).Width + Theme.S(10));
            lblWho.SetBounds(pad + avatarW, 0, ww, h);
            segStudents.SetBounds(pad + avatarW + ww + Theme.S(12), (h - segStudents.Height) / 2, segStudents.Width, segStudents.Height);
        }

        void UpdateTop()
        {
            lblWho.Text = current != null ? current.Name : "Campus Midnight";
            top.Invalidate();
            lblStamp.Text = loading ? "Refreshing…" : stampText;
            lblStamp.ForeColor = (stampBad && !loading) ? Theme.Bad : Theme.T3;
            btnRefresh.Enabled = !loading;
            bool multi = snap != null && snap.Students.Count > 1;
            segStudents.Visible = multi;
            if (multi)
            {
                string[] names = new string[snap.Students.Count];
                int sel = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = snap.Students[i].First.Length > 0 ? snap.Students[i].First : snap.Students[i].Name;
                    if (current != null && snap.Students[i].Id == current.Id) sel = i;
                }
                segStudents.SetOptions(names, sel);
            }
            LayoutTop();
        }

        // ================================================================ sign-in screen
        Label MakeLabel(Control parent, Font f, Color fore)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.Font = f;
            l.ForeColor = fore;
            l.BackColor = Theme.Bg2;
            parent.Controls.Add(l);
            return l;
        }

        void BuildLogin()
        {
            login = new ScrollPanel();
            login.Dock = DockStyle.Fill;
            login.BackColor = Theme.Bg0;
            card = new LoginCard();
            card.Logo = logo;
            login.Controls.Add(card);

            lblBrand = MakeLabel(card, Theme.Title, Theme.T1);
            lblBrand.Text = "Campus Midnight";
            lblIntro = MakeLabel(card, Theme.Body, Theme.T2);
            lblIntro.Text = "A cleaner, darker way to see your grades, assignments and schedule. Sign in with your normal Campus username and password.";

            lblUrlCap = MakeLabel(card, Theme.SemiSmall, Theme.T3);
            lblUrlCap.Text = "YOUR CAMPUS SIGN-IN LINK";
            urlBox = new DarkInput(false, false);
            urlBox.Placeholder = "https://yourdistrict.infinitecampus.org/campus/portal/students/yourdistrict.jsp";
            urlBox.Text = cfg.Url;
            card.Controls.Add(urlBox);

            lblUserCap = MakeLabel(card, Theme.SemiSmall, Theme.T3);
            lblUserCap.Text = "USERNAME";
            userBox = new DarkInput(false, false);
            userBox.Text = cfg.User;
            card.Controls.Add(userBox);
            lblPassCap = MakeLabel(card, Theme.SemiSmall, Theme.T3);
            lblPassCap.Text = "PASSWORD";
            passBox = new DarkInput(false, true);
            card.Controls.Add(passBox);

            chkStay = new DarkCheck();
            chkStay.Text = "Stay signed in on this PC";
            chkStay.Checked = cfg.Stay;
            chkStay.BackColor = Theme.Bg2;
            card.Controls.Add(chkStay);

            lblCookieCap = MakeLabel(card, Theme.SemiSmall, Theme.T3);
            lblCookieCap.Text = "PASTE YOUR SESSION COOKIE";
            cookieBox = new DarkInput(true, false);
            cookieBox.Placeholder = "In your browser, signed in to Campus: press F12, open Network, click any request to your Campus address, and copy the \"cookie\" request header.";
            card.Controls.Add(cookieBox);

            lblError = MakeLabel(card, Theme.Body, Theme.Bad);
            btnSignIn = new PillButton();
            btnSignIn.Text = "Sign in";
            btnSignIn.Primary = true;
            btnSignIn.Click += delegate { DoSignIn(); };
            card.Controls.Add(btnSignIn);

            lnkSso = MakeLabel(card, Theme.Body, Theme.AccText);
            lnkSso.Text = "My school uses Google or Microsoft sign-in";
            lnkSso.Cursor = Cursors.Hand;
            lnkSso.Click += delegate
            {
                cookieMode = !cookieMode;
                lnkSso.Text = cookieMode ? "Back to username and password" : "My school uses Google or Microsoft sign-in";
                lblError.Text = "";
                LayoutLogin();
            };

            lblFine = MakeLabel(card, Theme.Small, Theme.T3);
            lblFine.Text = "Your password goes only to your school's Campus server. It is saved only if you tick Stay signed in, and then it is encrypted for this Windows account.";

            KeyEventHandler enter = delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; e.Handled = true; DoSignIn(); }
            };
            urlBox.Box.KeyDown += enter;
            userBox.Box.KeyDown += enter;
            passBox.Box.KeyDown += enter;

            login.Resize += delegate { LayoutLogin(); };
        }

        void LayoutLogin()
        {
            if (login == null || card == null) return;
            int avail = login.ClientSize.Width;
            int cw = Math.Min(Theme.S(560), Math.Max(Theme.S(320), avail - Theme.S(40)));
            int p = Theme.S(36), iw = cw - p * 2, y = p;

            lblBrand.SetBounds(p, y, iw, Theme.S(46));
            y += Theme.S(52);
            int ih = Theme.WrapHeight(lblIntro.Text, lblIntro.Font, iw) + Theme.S(4);
            lblIntro.SetBounds(p, y, iw, ih);
            y += ih + Theme.S(22);

            lblUrlCap.SetBounds(p, y, iw, Theme.S(20));
            y += Theme.S(24);
            urlBox.SetBounds(p, y, iw, Theme.S(44));
            y += Theme.S(56);

            bool pw = !cookieMode;
            lblUserCap.Visible = pw;
            userBox.Visible = pw;
            lblPassCap.Visible = pw;
            passBox.Visible = pw;
            chkStay.Visible = pw;
            lblCookieCap.Visible = !pw;
            cookieBox.Visible = !pw;
            if (pw)
            {
                lblUserCap.SetBounds(p, y, iw, Theme.S(20));
                y += Theme.S(24);
                userBox.SetBounds(p, y, iw, Theme.S(44));
                y += Theme.S(56);
                lblPassCap.SetBounds(p, y, iw, Theme.S(20));
                y += Theme.S(24);
                passBox.SetBounds(p, y, iw, Theme.S(44));
                y += Theme.S(54);
                chkStay.SetBounds(p, y, iw, Theme.S(28));
                y += Theme.S(40);
            }
            else
            {
                lblCookieCap.SetBounds(p, y, iw, Theme.S(20));
                y += Theme.S(24);
                cookieBox.SetBounds(p, y, iw, Theme.S(120));
                y += Theme.S(132);
            }

            // messages ending in an ellipsis are progress notes, everything else is an error
            lblError.ForeColor = lblError.Text.EndsWith("…") ? Theme.T2 : Theme.Bad;
            int eh = string.IsNullOrEmpty(lblError.Text) ? 0 : Theme.WrapHeight(lblError.Text, lblError.Font, iw) + Theme.S(8);
            lblError.SetBounds(p, y, iw, eh);
            y += eh + Theme.S(eh > 0 ? 10 : 2);
            btnSignIn.SetBounds(p, y, iw, Theme.S(50));
            y += Theme.S(64);
            lnkSso.SetBounds(p, y, iw, Theme.S(26));
            y += Theme.S(38);
            int fh = Theme.WrapHeight(lblFine.Text, lblFine.Font, iw) + Theme.S(4);
            lblFine.SetBounds(p, y, iw, fh);
            y += fh + p;

            card.SetBounds(Math.Max(0, (avail - cw) / 2), Math.Max(Theme.S(24), (login.ClientSize.Height - y) / 2), cw, y);
            login.AutoScrollMinSize = new Size(0, y + Theme.S(48));
        }

        void SetLoginError(string msg)
        {
            lblError.Text = msg ?? "";
            LayoutLogin();
        }

        void SetBusy(bool b)
        {
            busy = b;
            btnSignIn.Enabled = !b;
            btnSignIn.Text = b ? "Signing in…" : "Sign in";
        }

        void ShowLogin(string message)
        {
            shell.Visible = false;
            login.Visible = true;
            login.BringToFront();
            lblError.Text = message ?? "";
            LayoutLogin();
        }

        void ShowShell()
        {
            login.Visible = false;
            shell.Visible = true;
            shell.BringToFront();
            LayoutShell();
            UpdateTop();
            ShowView(false);
        }

        void DoSignIn()
        {
            string cookie = cookieMode ? cookieBox.Text.Trim() : "";
            if (cookieMode && cookie.Length == 0)
            {
                SetLoginError("Paste your session cookie, or go back and use your username and password.");
                return;
            }
            StartSignIn(urlBox.Text, userBox.Text.Trim(), passBox.Text, cookie);
        }

        void TryAutoLogin()
        {
            if (!cfg.Stay || string.IsNullOrEmpty(cfg.Url) || string.IsNullOrEmpty(cfg.User)) return;
            string pass = Vault.Unprotect(cfg.EncPass);
            if (pass == null) return;
            SetLoginError("Signing you in…");
            StartSignIn(cfg.Url, cfg.User, pass, "");
        }

        void StartSignIn(string url, string user, string pass, string cookie)
        {
            if (busy) return;
            string origin, app, portal, err;
            if (!Api.ParseLoginUrl(url, out origin, out app, out portal, out err))
            {
                ShowLogin(err);
                return;
            }
            bool useCookie = !string.IsNullOrEmpty(cookie);
            if (!useCookie && (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)))
            {
                ShowLogin("Enter your Campus username and password.");
                return;
            }
            SetBusy(true);
            SetLoginError("");
            string cleanUrl = url.Trim();
            Api a = new Api(origin, cleanUrl, app, portal);
            ThreadPool.QueueUserWorkItem(delegate(object state)
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
                    else
                    {
                        ok = a.SignIn(user, pass, out e2);
                    }
                }
                catch (Exception ex)
                {
                    ok = false;
                    e2 = Friendly(ex);
                }
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        SetBusy(false);
                        if (ok) OnSignedIn(a, cleanUrl, user, pass, useCookie);
                        else ShowLogin(e2 ?? "Sign-in did not finish.");
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        void OnSignedIn(Api a, string url, string user, string pass, bool viaCookie)
        {
            api = a;
            cfg.Url = url;
            if (!viaCookie) cfg.User = user;
            cfg.Stay = chkStay.Checked && !viaCookie;
            cfg.EncPass = cfg.Stay ? Vault.Protect(pass) : "";
            cfg.Save();
            passBox.Text = "";
            cookieBox.Text = "";
            snap = null;
            current = null;
            term = "";
            view = "dashboard";
            stampText = "";
            stampBad = false;
            ShowShell();
            RefreshData();
        }

        void SignOut()
        {
            api = null;
            snap = null;
            current = null;
            if (photo != null) { photo.Dispose(); photo = null; }
            photoNote = "";
            photoTried.Clear();
            LoadLocalImages();
            cfg.Stay = false;
            cfg.EncPass = "";
            cfg.Save();
            chkStay.Checked = false;
            chkStay.Invalidate();
            ShowLogin(null);
        }

        static string Friendly(Exception ex)
        {
            if (ex is WebException) return "Could not reach your school's Campus server. Check your internet connection and try again.";
            return ex.Message;
        }

        // ================================================================ data
        void RefreshData()
        {
            if (loading || api == null) return;
            loading = true;
            UpdateTop();
            string pid = current != null ? current.Id : null;
            Api a = api;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Snapshot s = null;
                string err = null;
                bool auth = false;
                try { s = a.LoadAll(pid); }
                catch (AuthException) { auth = true; }
                catch (Exception ex) { err = Friendly(ex); }
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        loading = false;
                        if (a != api) return; // signed out meanwhile
                        if (auth) { SessionEnded(); return; }
                        if (s != null)
                        {
                            snap = s;
                            ApplyLevels();
                            if (snap.Students.Count > 0)
                            {
                                Student found = null;
                                if (current != null)
                                {
                                    foreach (Student st in snap.Students) if (st.Id == current.Id) found = st;
                                }
                                current = found ?? snap.Students[0];
                            }
                            if (string.IsNullOrEmpty(term) || !snap.Terms.Contains(term)) term = snap.DefaultTerm();
                            stampText = "Updated " + snap.At.ToString("h:mm tt");
                            stampBad = false;
                            if (current != null) LoadAssets(a, current, false);
                        }
                        else
                        {
                            stampText = err ?? "Could not refresh.";
                            stampBad = true;
                        }
                        UpdateTop();
                        UpdateBadges();
                        ShowView(true);
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        void SessionEnded()
        {
            string pass = cfg.Stay ? Vault.Unprotect(cfg.EncPass) : null;
            api = null;
            if (pass != null && !string.IsNullOrEmpty(cfg.Url) && !string.IsNullOrEmpty(cfg.User))
            {
                ShowLogin("Your session ended. Signing you in again…");
                StartSignIn(cfg.Url, cfg.User, pass, "");
            }
            else
            {
                ShowLogin("Your Campus session ended. Please sign in again.");
            }
        }

        // ================================================================ pictures, themes, small helpers
        static Bitmap LoadBmp(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (Image im = Image.FromFile(path)) { return new Bitmap(im); }
            }
            catch (Exception) { return null; }
        }

        // your own files win; otherwise whatever was saved from an earlier sign-in
        void LoadLocalImages()
        {
            if (logo == null)
            {
                Bitmap b = LoadBmp(Disk.At("logo.png"));
                if (b != null) { logo = Draw.Trim(b); logoNote = "Logo: using your own logo.png."; }
                else
                {
                    b = LoadBmp(Disk.At("logo-cache.png"));
                    if (b != null) { logo = Draw.Trim(b); logoNote = "Logo: saved from your school's sign-in page."; }
                }
            }
            if (photo == null)
            {
                Bitmap b = LoadBmp(Disk.At("photo.png"));
                if (b != null) { photo = b; photoNote = "Photo: using your own picture."; }
            }
        }

        // fetches the student's photo and the school logo from the school's own server (once per sign-in)
        void LoadAssets(Api a, Student st, bool force)
        {
            if (force) { logoTried = false; photoTried.Remove(st.Id); }
            bool needPhoto = photo == null && !photoTried.Contains(st.Id);
            bool needLogo = logo == null && !logoTried;
            if (!needPhoto && !needLogo) return;
            if (needPhoto) photoTried.Add(st.Id);
            if (needLogo) logoTried = true;
            string pid = st.Id;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Bitmap np = null, nl = null;
                string pn = "", ln = "";
                try
                {
                    if (needPhoto)
                    {
                        string cache = Disk.At("photo-cache-" + Disk.SafeName(pid) + ".png");
                        np = LoadBmp(cache);
                        if (np != null) pn = "Photo: saved from Campus.";
                        else
                        {
                            string src;
                            np = a.FetchPhoto(pid, out src);
                            if (np != null)
                            {
                                try { np.Save(cache, System.Drawing.Imaging.ImageFormat.Png); } catch (Exception) { }
                                pn = "Photo: from Campus (" + src + ").";
                            }
                            else pn = "Photo: Campus did not return one. Use Choose my photo in Settings.";
                        }
                    }
                    if (needLogo)
                    {
                        string src;
                        nl = a.FetchLogo(out src);
                        if (nl != null)
                        {
                            try { nl.Save(Disk.At("logo-cache.png"), System.Drawing.Imaging.ImageFormat.Png); } catch (Exception) { }
                            nl = Draw.Trim(nl);
                            ln = "Logo: from your school's sign-in page.";
                        }
                        else ln = "Logo: none found on the sign-in page. Put a logo.png in the data folder or use Choose logo in Settings.";
                    }
                }
                catch (Exception) { }
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (a != api) { if (np != null) np.Dispose(); if (nl != null) nl.Dispose(); return; }
                        if (np != null && photo == null) photo = np; else if (np != null) np.Dispose();
                        if (nl != null && logo == null) { logo = nl; card.Logo = logo; UpdateIcon(); } else if (nl != null) nl.Dispose();
                        if (pn.Length > 0) photoNote = pn;
                        if (ln.Length > 0) logoNote = ln;
                        rail.Invalidate();
                        top.Invalidate();
                        if (view == "dashboard" || view == "settings") ShowView(true);
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        void UpdateBadges()
        {
            int unread = 0;
            if (snap != null)
            {
                foreach (Msg m in snap.Messages) if (!m.Read) unread++;
                foreach (Msg m in snap.Notices) if (!m.Read) unread++;
            }
            for (int i = 0; i < navs.Count; i++)
            {
                if (navKeys[i] == "inbox" && navs[i].Badge != unread) { navs[i].Badge = unread; navs[i].Invalidate(); }
            }
        }

        void ApplyInterval()
        {
            autoTimer.Stop();
            if (cfg.Interval > 0)
            {
                autoTimer.Interval = cfg.Interval * 60 * 1000;
                autoTimer.Start();
            }
        }

        void ChangeTheme(string name)
        {
            cfg.Theme = name;
            cfg.Save();
            Theme.Apply(name, cfg.Mode);
            RebuildUi();
        }

        void ChangeMode(string mode)
        {
            cfg.Mode = mode;
            cfg.Save();
            Theme.Apply(cfg.Theme, mode);
            RebuildUi();
        }

        // every control reads the theme colours when it is created, so a theme change rebuilds them all
        void RebuildUi()
        {
            bool wasShell = shell != null && shell.Visible;
            SuspendLayout();
            Controls.Remove(shell);
            Controls.Remove(login);
            shell.Dispose();
            login.Dispose();
            navs.Clear();
            navKeys.Clear();
            aList = null;
            liveHero = null; liveStrip = null; liveTimeline = null;
            lastKey = null;
            BackColor = Theme.Bg0;
            ForeColor = Theme.T1;
            BuildShell();
            BuildLogin();
            Controls.Add(shell);
            Controls.Add(login);
            ResumeLayout(true);
            Native.DarkTitle(Handle);
            UpdateIcon();
            if (wasShell)
            {
                login.Visible = false;
                shell.Visible = true;
                shell.BringToFront();
                LayoutShell();
                UpdateTop();
                UpdateBadges();
                ShowView(false);
            }
            else ShowLogin(null);
        }

        void OpenCampusPage(string page)
        {
            if (api == null) return;
            try
            {
                string role = api.Portal == "parents" ? "parent" : "student";
                ProcessStartInfo psi = new ProcessStartInfo(api.Origin + "/campus/nav-wrapper/" + role + "/portal/" + role + "/" + page);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception) { }
        }

        void OpenSite()
        {
            if (api == null) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(api.LoginUrl);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception) { }
        }
    }
}
