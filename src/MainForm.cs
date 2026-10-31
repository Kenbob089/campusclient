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
        PillButton btnMenu;
        NotifyIcon tray;
        bool wizStarted;
        bool quitting, trayHinted, keepScrollRebuild, railAnimating;
        readonly HashSet<string> fired = new HashSet<string>();
        DayPlan notifyPlan;
        DateTime notifyPlanAt = DateTime.MinValue;
        int lastCw;
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
            Theme.NumbersOnly = cfg.NumbersOnly;
            Log.Write("app started: scale=" + Theme.Scale + " theme=" + cfg.Theme + " mode=" + cfg.Mode + " dark=" + Theme.Dark + " icons=" + Theme.HasIcons);
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
            ApplyTray();

            // timers first: building the shell raises Resize events that use them
            resizeTimer = new System.Windows.Forms.Timer();
            resizeTimer.Interval = 220;
            resizeTimer.Tick += delegate
            {
                resizeTimer.Stop();
                if (shell.Visible && snap != null && host.Width != lastCw) { lastCw = host.Width; ShowView(true); }
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

        void ShowTopMenu()
        {
            List<DarkMenu.Entry> es = new List<DarkMenu.Entry>();
            es.Add(DarkMenu.Entry.Item(loading ? "Refreshing…" : "Refresh now", "\uE72C", false, delegate { if (!loading) RefreshData(); }));
            es.Add(DarkMenu.Entry.Item("Open original Campus site", "\uE8A7", false, delegate { OpenSite(); }));
            es.Add(DarkMenu.Entry.Sep());
            es.Add(DarkMenu.Entry.Head("Appearance"));
            es.Add(DarkMenu.Entry.Item("Match Windows", "\uE713", cfg.Mode == "system", delegate { BeginInvoke(new MethodInvoker(delegate { ChangeMode("system"); })); }));
            es.Add(DarkMenu.Entry.Item("Light", "\uE706", cfg.Mode == "light", delegate { BeginInvoke(new MethodInvoker(delegate { ChangeMode("light"); })); }));
            es.Add(DarkMenu.Entry.Item("Dark", "\uE708", cfg.Mode == "dark", delegate { BeginInvoke(new MethodInvoker(delegate { ChangeMode("dark"); })); }));
            es.Add(DarkMenu.Entry.Sep());
            es.Add(DarkMenu.Entry.Item("Settings", "\uE713", false, delegate { GoTo("settings"); }));
            es.Add(DarkMenu.Entry.Item("Sign out", "\uE7E8", false, delegate { BeginInvoke(new MethodInvoker(delegate { SignOut(); })); }));
            DarkMenu.Popup(btnMenu, es);
        }

        static readonly string[][] NavAll = new string[][]
        {
            new string[] { "dashboard", "Home", "\uE80F" },
            new string[] { "now", "Now", "\uE916" },
            new string[] { "grades", "Grades", "\uE7BE" },
            new string[] { "assignments", "Work", "\uE73A" },
            new string[] { "schedule", "Schedule", "\uE787" },
            new string[] { "inbox", "Inbox", "\uE715" },
            new string[] { "docs", "Docs", "\uE8A5" },
            new string[] { "tools", "Tools", "\uE8EF" },
            new string[] { "settings", "Settings", "\uE713" }
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
            RebuildNav();
        }

        // ---------------------------------------------------------------- notifications (Windows notification centre)
        void ApplyTray() { ApplyTray(false); }

        void ApplyTray(bool force)
        {
            bool want = force || cfg.NotifyStart || cfg.NotifyEnd || cfg.TrayOnClose;
            if (want && tray == null)
            {
                tray = new NotifyIcon();
                tray.Text = "Campus Midnight";
                if (Icon != null) tray.Icon = Icon;
                ContextMenuStrip tm = new ContextMenuStrip();
                tm.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                tm.ShowImageMargin = false;
                ToolStripMenuItem open = new ToolStripMenuItem("Open Campus Midnight");
                open.Click += delegate { ShowFromTray(); };
                ToolStripMenuItem quit = new ToolStripMenuItem("Quit");
                quit.Click += delegate { quitting = true; Close(); };
                tm.Items.Add(open);
                tm.Items.Add(quit);
                tray.ContextMenuStrip = tm;
                tray.DoubleClick += delegate { ShowFromTray(); };
            }
            if (tray != null) tray.Visible = want;
        }

        void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (cfg.TrayOnClose && !quitting && e.CloseReason == CloseReason.UserClosing && tray != null)
            {
                e.Cancel = true;
                Hide();
                if (!trayHinted)
                {
                    trayHinted = true;
                    try { tray.ShowBalloonTip(4000, "Still running", "Campus Midnight keeps running here so it can remind you about classes. Right-click the icon to quit.", ToolTipIcon.Info); } catch (Exception) { }
                }
                return;
            }
            base.OnFormClosing(e);
        }

        void Toast(string title, string body)
        {
            ApplyTray(true);
            if (tray == null) return;
            Log.Write("notification: " + (title.Length > 0 ? "sent" : "empty"));
            try { tray.ShowBalloonTip(10000, title, body, ToolTipIcon.Info); } catch (Exception ex) { Log.Error("notification", ex); }
        }

        // once a second: fires "starts soon" / "ends soon" reminders for today's classes
        void CheckNotify()
        {
            if (snap == null || (!cfg.NotifyStart && !cfg.NotifyEnd) || cfg.Schedules.Count == 0) return;
            DateTime nowDt = DateTime.Now;
            if (notifyPlan == null || notifyPlan.Date != nowDt.Date || (nowDt - notifyPlanAt).TotalSeconds > 60)
            {
                notifyPlan = Sched.Plan(cfg, snap, term, nowDt.Date);
                notifyPlanAt = nowDt;
            }
            if (!notifyPlan.School) return;
            TimeSpan now = nowDt.TimeOfDay;
            int lead = cfg.NotifyLead;
            for (int i = 0; i < notifyPlan.Slots.Count; i++)
            {
                Slot sl = notifyPlan.Slots[i];
                if (!sl.IsClass) continue;
                string where = sl.R != null && sl.R.Room.Length > 0 ? "  ·  Room " + sl.R.Room : "";
                string per = sl.R != null && sl.R.Period.Length > 0 ? "Period " + sl.B.Label : sl.B.Label;
                if (cfg.NotifyStart)
                {
                    TimeSpan t = sl.B.Start - TimeSpan.FromMinutes(lead);
                    string id = nowDt.ToString("yyyyMMdd") + "s" + i;
                    if (now >= t && now < t + TimeSpan.FromMinutes(2) && now < sl.B.End && fired.Add(id))
                        Toast(sl.Title, (lead > 0 ? "Starts in " + lead + " min" : "Starting now") + "  ·  " + Bell.Clock(sl.B.Start) + where);
                }
                if (cfg.NotifyEnd)
                {
                    TimeSpan t = sl.B.End - TimeSpan.FromMinutes(lead);
                    string id = nowDt.ToString("yyyyMMdd") + "e" + i;
                    if (now >= t && now < t + TimeSpan.FromMinutes(2) && now < sl.B.End + TimeSpan.FromMinutes(1) && fired.Add(id))
                        Toast(sl.Title, (lead > 0 ? "Ends in " + lead + " min" : "Class is over") + "  ·  " + Bell.Clock(sl.B.End));
                }
            }
            if (fired.Count > 400) fired.Clear();
        }

        void LiveTick()
        {
            try { CheckNotify(); } catch (Exception ex) { Log.Error("CheckNotify", ex); }
            if (shell == null || !shell.Visible || snap == null || !Visible || WindowState == FormWindowState.Minimized) return;
            if (liveHero != null) liveHero.Invalidate();
            if (liveStrip != null) liveStrip.Invalidate();
            if (liveTimeline != null && DateTime.Now.Second % 15 == 0) liveTimeline.Invalidate();
            if (plan == null || (view != "now" && view != "dashboard")) return;
            string key = Sched.StateKey(plan, DateTime.Now.TimeOfDay);
            if (lastKey == null) { lastKey = key; return; }
            if (key != lastKey) { lastKey = key; ShowView(true); }
        }

        // widens or narrows the sidebar a few pixels per tick
        void RailStep()
        {
            int target = cfg.RailCollapsed ? Theme.S(68) : Theme.S(196);
            int step = Math.Max(1, Theme.S(26));
            if (railW < target) railW = Math.Min(target, railW + step);
            else if (railW > target) railW = Math.Max(target, railW - step);
            bool wide = railW >= Theme.S(140);
            foreach (NavButton nb in navs) nb.Expanded = wide;
            if (railToggle != null) railToggle.Expanded = wide;
            if (railW == target)
            {
                railTimer.Stop();
                railAnimating = false;
                LayoutShell();
                rail.Invalidate();
                ShowView(true);
                return;
            }
            // while it slides, only the sidebar is resized: the page is laid out once at the end
            rail.Width = railW;
            LayoutRail();
            rail.Invalidate();
        }

        void ToggleRail()
        {
            cfg.RailCollapsed = !cfg.RailCollapsed;
            cfg.Save();
            railAnimating = true;
            rail.BringToFront();
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
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
            }
            base.Dispose(disposing);
        }

        // the window and taskbar icon: the school logo when it is square enough, else the icon built into the exe, else a drawn "C"
        void UpdateIcon()
        {
            Icon ic = null;
            try
            {
                if (File.Exists(Disk.At("icon.png")))
                    using (Image im = Image.FromFile(Disk.At("icon.png")))
                    using (Bitmap ib = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (Graphics ig = Graphics.FromImage(ib))
                        {
                            ig.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            ig.DrawImage(im, new Rectangle(0, 0, 64, 64));
                        }
                        ic = Icon.FromHandle(ib.GetHicon());
                    }
            }
            catch (Exception) { ic = null; }
            if (ic == null) ic = Draw.MakeIcon(logo);
            if (ic == null)
            {
                try { ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch (Exception) { ic = null; }
            }
            if (ic == null) ic = DrawnIcon();
            if (ic != null) { Icon = ic; if (tray != null) tray.Icon = ic; }
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

            MakeNav();
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

            btnMenu = new PillButton();
            btnMenu.Text = "";
            btnMenu.Glyph = "\uE712";
            btnMenu.Kind = 0;
            btnMenu.Click += delegate { ShowTopMenu(); };

            top.Controls.Add(lblWho);
            top.Controls.Add(segStudents);
            top.Controls.Add(lblStamp);
            top.Controls.Add(btnMenu);

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

        void MakeNav()
        {
            List<string[]> items = NavItems(false);
            for (int i = 0; i < items.Count; i++)
            {
                NavButton nb = new NavButton();
                nb.Caption = items[i][1];
                nb.Glyph = items[i][2];
                string key = items[i][0];
                nb.Expanded = railW >= Theme.S(140);
                nb.Click += delegate { view = key; ShowView(false); };
                navs.Add(nb);
                navKeys.Add(key);
                rail.Controls.Add(nb);
            }
        }

        // swap just the sidebar buttons (no full rebuild) after the student reorders or hides some
        void RebuildNav()
        {
            Native.SetRedraw(rail, false);
            try
            {
                foreach (NavButton nb in navs) { rail.Controls.Remove(nb); nb.Dispose(); }
                navs.Clear();
                navKeys.Clear();
                MakeNav();
                LayoutRail();
                UpdateBadges();
                string navKey = (view == "debug") ? "settings" : view;
                for (int i = 0; i < navs.Count; i++) navs[i].On = navKeys[i] == navKey;
            }
            finally { Native.SetRedraw(rail, true); }
            rail.Invalidate(true);
        }

        void LayoutRail()
        {
            int rw = railW;
            int bw = rw - Theme.S(20), bh = Theme.S(48);
            int y0 = Theme.S(84);
            for (int i = 0; i < navs.Count; i++)
                navs[i].SetBounds(Theme.S(10), y0 + i * (bh + Theme.S(4)), bw, bh);
            if (railToggle != null) railToggle.SetBounds(Theme.S(10), Math.Max(y0 + navs.Count * (bh + Theme.S(4)) + Theme.S(8), shell.Height - bh - Theme.S(14)), bw, bh);
        }

        void LayoutShell()
        {
            int rw = railW, th = Theme.S(64);
            rail.SetBounds(0, 0, rw, shell.Height);
            top.SetBounds(rw, 0, Math.Max(0, shell.Width - rw), th);
            host.SetBounds(rw, th, Math.Max(0, shell.Width - rw), Math.Max(0, shell.Height - th));
            LayoutRail();
            LayoutTop();
        }

        void LayoutTop()
        {
            int pad = Theme.S(24), h = top.Height, bh = Theme.S(38);
            int y = (h - bh) / 2;
            int x = top.Width - pad;
            btnMenu.SetBounds(x - Theme.S(46), y, Theme.S(46), bh);
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
            btnMenu.Invalidate();
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
                        else { Log.Write("sign-in failed: " + (e2 ?? "unknown")); ShowLogin(e2 ?? "Sign-in did not finish."); }
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        void OnSignedIn(Api a, string url, string user, string pass, bool viaCookie)
        {
            Log.Write("signed in (" + (viaCookie ? "cookie" : "password") + ")");
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
            DateTime t0 = DateTime.Now;
            Log.Write("refresh started");
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Snapshot s = null;
                string err = null;
                bool auth = false;
                try { s = a.LoadAll(pid); }
                catch (AuthException) { auth = true; Log.Write("refresh: session ended"); }
                catch (Exception ex) { err = Friendly(ex); Log.Error("refresh", ex); }
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
                            Log.Write("refresh done in " + (int)(DateTime.Now - t0).TotalMilliseconds + " ms: courses=" + snap.Courses.Count
                                + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " calendarDays=" + snap.CalDays.Count
                                + " autoBells=" + snap.AutoBells.Count + " " + snap.CalStats);
                            if ((!cfg.SetupDone || cfg.SetupVer < SetupVersion) && !wizStarted)
                            {
                                wizStarted = true;
                                wizFirstRun = true;
                                WizardInit();
                                view = "setup";
                            }
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
            Log.Write("accent theme -> " + name);
            Theme.Apply(name, cfg.Mode);
            RebuildUi();
        }

        void ChangeMode(string mode)
        {
            cfg.Mode = mode;
            cfg.Save();
            Log.Write("appearance mode -> " + mode);
            Theme.Apply(cfg.Theme, mode);
            RebuildUi();
        }

        // every control reads the theme colours when it is created, so a theme change rebuilds them all
        void RebuildUi()
        {
            bool wasShell = shell != null && shell.Visible;
            Native.SetRedraw(this, false);
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
            ApplyTray();
            if (wasShell)
            {
                login.Visible = false;
                shell.Visible = true;
                shell.BringToFront();
                LayoutShell();
                UpdateTop();
                UpdateBadges();
                ShowView(keepScrollRebuild);
                keepScrollRebuild = false;
            }
            else ShowLogin(null);
            Native.SetRedraw(this, true);
        }

        // Campus has no stable deep links across districts, so this opens your school's own sign-in link
        void OpenCampusPage(string page)
        {
            OpenSite();
        }

        void OpenSite()
        {
            string url = api != null ? api.LoginUrl : cfg.Url;
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception) { }
        }
    }
}
