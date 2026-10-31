using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CampusMidnight
{
    // The guided setup: look, bell times, rotating days, which class is in which period, and the extras.
    // It runs on its own after the first sign-in and can be started again from Settings.
    partial class MainForm
    {
        const int WizSteps = 5;
        int wizStep;
        bool wizFirstRun;
        string wError = "";
        string wDay = "";                   // the day whose classes are being picked

        void WizardInit()
        {
            wizStep = 0;
            wError = "";
            wDay = "";
            GridLoad();
            Log.Write("setup started (first run: " + wizFirstRun + ")");
        }

        void StartWizard(int step)
        {
            wizFirstRun = false;
            WizardInit();
            wizStep = step;
            GoTo("setup");
        }

        void WizardFinish(bool completed)
        {
            cfg.SetupDone = true;
            cfg.SetupVer = SetupVersion;
            cfg.Save();
            Log.Write("setup " + (completed ? "finished" : "skipped") + " at step " + wizStep);
            wizFirstRun = false;
            wizStep = 0;
            view = "dashboard";
            Redo();
        }

        // ---------------------------------------------------------------- pieces
        string[] CampusLabelsInOrder()
        {
            List<string> l = new List<string>();
            if (snap != null)
            {
                List<CalDay> days = new List<CalDay>(snap.CalDays);
                days.Sort(delegate(CalDay a, CalDay b) { return a.Date.CompareTo(b.Date); });
                foreach (CalDay c in days) if (c.Label.Length > 0 && !l.Contains(c.Label)) l.Add(c.Label);
            }
            return l.ToArray();
        }

        void WizHead(string title, string sub)
        {
            AddLabel("STEP " + (wizStep + 1) + " OF " + WizSteps, Theme.SemiSmall, Theme.AccText, X0(), vy, CW(), Theme.S(20));
            vy += Theme.S(22);
            int barW = CW();
            AddCard(new WizBar(wizStep + 1, WizSteps), X0(), vy, barW, Theme.S(6));
            vy += Theme.S(18);
            Header(title, sub);
        }

        DarkInput WizField(string caption, string value, int x, int w, Action<string> set)
        {
            AddLabel(caption.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, x, vy, w, Theme.S(20));
            DarkInput d = new DarkInput(false, false);
            d.Text = value;
            d.SetBounds(x, vy + Theme.S(22), w, Theme.S(44));
            DarkInput self = d;
            d.Box.TextChanged += delegate { set(self.Text); self.Invalid = false; self.Invalidate(); };
            host.Controls.Add(d);
            return d;
        }

        void WizFooter(string nextText, Func<bool> onNext)
        {
            if (wError.Length > 0) AddNotice(wError);
            vy += Theme.S(8);
            int x = X0();
            if (wizStep > 0)
            {
                PillButton back = AddButton("Back", "", 0, x, vy, delegate { wizStep--; wError = ""; Redo(); });
                x += back.Width + Theme.S(10);
            }
            PillButton next = AddButton(nextText, "", 1, x, vy, delegate
            {
                wError = "";
                if (onNext()) wizStep++;
                Redo();
            });
            x += next.Width + Theme.S(16);
            AddButton(wizFirstRun ? "Skip setup" : "Cancel", "", 2, x, vy, delegate
            {
                if (wizFirstRun) WizardFinish(false);
                else { view = "settings"; wizStep = 0; Redo(); }
            });
            vy += Theme.S(64);
        }

        List<string> TermCourses()
        {
            List<string> names = new List<string>();
            if (snap != null)
                foreach (RosterItem r in snap.Roster)
                    if (!r.Dropped && Sched.TermMatches(r, term) && !names.Contains(r.Course)) names.Add(r.Course);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // ---------------------------------------------------------------- the pages
        void BuildSetup()
        {
            if (snap == null) { Header("Setup", "Loading your classes first…"); return; }
            switch (wizStep)
            {
                case 0: WizGrid(); break;
                case 1: WizClasses(); break;
                case 2: WizLook(); break;
                case 3: WizExtras(); break;
                default: WizDone(); break;
            }
        }

        void WizLook()
        {
            WizHead(wizFirstRun ? "Welcome to Campus Midnight" : "Look and feel", "A few quick steps and your schedule, grades and reminders will be ready. You can change all of it later in Settings.");
            SectionTitle("Light or dark");
            string[] modeNames = new string[] { "Match Windows", "Light", "Dark" };
            string[] modeKeys = new string[] { "system", "light", "dark" };
            Segmented ms = new Segmented();
            ms.SetOptions(modeNames, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)));
            ms.Changed += delegate { int k = ms.Selected; BeginInvoke(new MethodInvoker(delegate { ChangeMode(modeKeys[k]); })); };
            ms.Location = new Point(X0(), vy);
            host.Controls.Add(ms);
            vy += ms.Height + Theme.S(26);

            SectionTitle("Accent colour");
            string[] names = new string[Theme.All.Count];
            for (int i = 0; i < names.Length; i++) names[i] = Theme.All[i].Name;
            PillButton pick = AddButton(Theme.Current, "", 0, X0(), vy, null);
            PillButton ps = pick;
            pick.Click += delegate
            {
                DarkMenu.Pick(ps, names, Array.IndexOf(names, Theme.Current), delegate(int k) { BeginInvoke(new MethodInvoker(delegate { ChangeTheme(names[k]); })); });
            };
            vy += Theme.S(70);
            WizFooter("Next", delegate { return true; });
        }

        // each numbered period of the grid gets one class. Only the Auto fill button reads the class placements from Campus.
        void WizClasses()
        {
            List<string> nums = GridNumbers();
            WizHead("Which class is in which period?", "Pick the class for each period number. It applies on every letter day. Nothing is filled in unless you press Auto fill.");
            if (nums.Count == 0)
            {
                AddNotice("Your grid has no numbered periods yet. Go back and put period numbers in the circles.");
                WizFooter("Next", delegate { return true; });
                return;
            }
            List<string> courses = TermCourses();
            if (courses.Count == 0) AddNotice("No classes were found for this term, so there is nothing to pick yet.");
            AddButton("Auto fill from Campus", "\uE72C", 1, X0(), vy, delegate { AutoFillClasses(); Redo(); });
            vy += Theme.S(58);
            if (wAutoNote.Length > 0) AddNotice(wAutoNote);

            PlainCard card = new PlainCard();
            int rowH = Theme.S(56);
            int cardH = Theme.S(16) + nums.Count * rowH + Theme.S(8);
            AddCard(card, X0(), vy, CW(), cardH);
            int y = Theme.S(12);
            foreach (string num in nums)
            {
                string pnum = num;
                Label l = new Label();
                l.AutoSize = false; l.UseMnemonic = false; l.BackColor = Theme.Bg2; l.ForeColor = Theme.T1; l.Font = Theme.Body;
                l.Text = "Period " + pnum + "   " + GridTimesFor(pnum);
                l.TextAlign = ContentAlignment.MiddleLeft;
                l.SetBounds(Theme.S(20), y, Math.Min(Theme.S(300), CW() / 2 - Theme.S(30)), Theme.S(44));
                card.Controls.Add(l);

                string cur;
                cfg.PeriodMap.TryGetValue(Settings.PeriodKey("", pnum, ""), out cur);
                cur = cur ?? "";
                string txt;
                if (cur == "-") txt = "Free period";
                else if (cur.Length > 0) txt = CourseByKey(courses, cur) ?? "Pick a class";
                else txt = "Not set";
                PillButton pb = new PillButton();
                pb.Text = txt;
                pb.Glyph = "";
                pb.Kind = cur.Length > 0 ? 1 : 0;
                int bx = Theme.S(20) + l.Width + Theme.S(10);
                pb.SetBounds(bx, y + Theme.S(2), CW() - bx - Theme.S(20), Theme.S(40));
                PillButton self = pb;
                int curIdx = cur == "-" ? 1 : (cur.Length > 0 ? Math.Max(0, courses.FindIndex(delegate(string cn) { return Settings.Slug(cn) == cur; }) + 2) : 0);
                pb.Click += delegate
                {
                    string[] opts = new string[courses.Count + 2];
                    opts[0] = "Not set";
                    opts[1] = "Free period / no class";
                    for (int c = 0; c < courses.Count; c++) opts[c + 2] = courses[c];
                    DarkMenu.Pick(self, opts, curIdx, delegate(int k)
                    {
                        string key = Settings.PeriodKey("", pnum, "");
                        if (k == 0) cfg.PeriodMap.Remove(key);
                        else if (k == 1) cfg.PeriodMap[key] = "-";
                        else cfg.PeriodMap[key] = Settings.Slug(courses[k - 2]);
                        cfg.Save();
                        Redo();
                    });
                };
                card.Controls.Add(pb);
                y += rowH;
            }
            vy += cardH + Theme.S(16);
            WizFooter("Next", delegate { cfg.Save(); return true; });
        }

        static string CourseByKey(List<string> courses, string key)
        {
            foreach (string c in courses) if (Settings.Slug(c) == key) return c;
            return null;
        }

        void WizExtras()
        {
            WizHead("Extras", "A few switches. Everything here can be changed later in Settings.");
            SectionTitle("Grades");
            AddToggle("Count dropped classes in my GPA", cfg.CountDropped, delegate(bool v) { cfg.CountDropped = v; cfg.Save(); });
            AddToggle("Show a rounded number (92) instead of a letter grade (A-)", cfg.NumbersOnly, delegate(bool v) { cfg.NumbersOnly = v; Theme.NumbersOnly = v; cfg.Save(); });
            vy += Theme.S(12);
            SectionTitle("Reminders");
            AddToggle("Notify me before a class starts", cfg.NotifyStart, delegate(bool v) { cfg.NotifyStart = v; cfg.Save(); ApplyTray(); });
            AddToggle("Notify me before a class ends", cfg.NotifyEnd, delegate(bool v) { cfg.NotifyEnd = v; cfg.Save(); ApplyTray(); });
            AddToggle("Keep running in the system tray when I close the window", cfg.TrayOnClose, delegate(bool v) { cfg.TrayOnClose = v; cfg.Save(); ApplyTray(); });
            int[] leads = new int[] { 0, 1, 2, 5, 10, 15 };
            Segmented lg = new Segmented();
            lg.SetOptions(new string[] { "On time", "1 min", "2 min", "5 min", "10 min", "15 min" }, Math.Max(0, Array.IndexOf(leads, cfg.NotifyLead)));
            lg.Changed += delegate { cfg.NotifyLead = leads[lg.Selected]; cfg.Save(); };
            lg.Location = new Point(X0(), vy);
            host.Controls.Add(lg);
            vy += lg.Height + Theme.S(20);
            SectionTitle("Honors and AP");
            AddLabel("Weighted GPA guesses honors and AP from the class name. Open any class on the Grades page to correct it.", Theme.Body, Theme.T2, X0(), vy - Theme.S(6), CW(), Theme.S(26));
            vy += Theme.S(40);
            WizFooter("Next", delegate { return true; });
        }

        void WizDone()
        {
            WizHead("You are set", "Here is how a typical day looks now. Change anything later under Settings, then Schedule setup.");
            DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
            if (!p.School) p = Sched.Plan(cfg, snap, term, Sched.NextSchoolDay(cfg, snap, DateTime.Today));
            ListCard lc = new ListCard();
            lc.Heading = p.Date.ToString("dddd") + (p.DayLabel.Length > 0 ? "  ·  " + Sched.DayName(p.DayLabel) : "");
            lc.EmptyText = "No periods yet. Go back to the schedule step.";
            foreach (Slot s in p.Slots)
            {
                Item it = new Item();
                it.Lead = s.B.Label;
                it.Title = s.Title;
                it.Sub = s.B.Text();
                lc.Items.Add(it);
            }
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight() + Theme.S(20);
            AddButton("Finish", "", 1, X0(), vy, delegate { WizardFinish(true); });
            AddButton("Back", "", 0, X0() + Theme.S(130), vy, delegate { wizStep--; Redo(); });
            vy += Theme.S(64);
        }
    }

    // thin progress bar for the setup steps
    class WizBar : Control
    {
        readonly int at, total;
        public WizBar(int step, int steps)
        {
            at = step; total = steps;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg0);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, Height / 2, Theme.Bg4);
            int w = Math.Max(Height, (int)(r.Width * (double)at / total));
            Theme.FillRound(e.Graphics, new Rectangle(0, 0, w, r.Height), Height / 2, Theme.Acc);
        }
    }
}
