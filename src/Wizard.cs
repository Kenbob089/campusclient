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
        const int WizSteps = 6;
        int wizStep;
        bool wizFirstRun;
        string wName = "Regular", wStart = "8:05 AM", wClass = "50", wPass = "5", wCount = "7", wLunchAfter = "4", wLunch = "30";
        bool wMods;
        string wModMin = "15", wModsPer = "3", wLunchMods = "2";
        int wRotMode;                       // 0 no rotation, 1 whole day, 2 before / after lunch
        string wFirst = "1", wLast = "7", wLabels = "A,B";
        bool wOwnLabels;
        int wTodayIdx;
        string wError = "";
        string wDay = "";                   // the day whose classes are being picked
        BellSchedule wPreview;
        readonly Dictionary<int, string> wPick = new Dictionary<int, string>();   // class position -> "" (automatic), "-" (free) or a class key

        void WizardInit()
        {
            wizStep = 0;
            wError = "";
            wDay = "";
            wPreview = null;
            wPick.Clear();
            wOwnLabels = cfg.RotOn;
            if (cfg.RotLabels.Length > 0) wLabels = cfg.RotLabels;
            wRotMode = cfg.RotOn ? 1 : 0;
            BellSchedule cur = Editing();
            if (cur != null) wName = cur.Name;
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

        static List<Bell> ClassBells(BellSchedule bs)
        {
            List<Bell> l = new List<Bell>();
            if (bs == null) return l;
            List<Bell> all = new List<Bell>(bs.Entries);
            all.Sort(delegate(Bell a, Bell b) { return a.Start.CompareTo(b.Start); });
            foreach (Bell b in all) if (b.Key.Length > 0 && char.IsDigit(b.Key[0])) l.Add(b);
            return l;
        }

        // how many classes come before lunch (the schedule's own lunch row when there is one, else what was typed)
        int LunchPos(BellSchedule bs, int fallback)
        {
            if (bs == null) return fallback;
            List<Bell> all = new List<Bell>(bs.Entries);
            all.Sort(delegate(Bell a, Bell b) { return a.Start.CompareTo(b.Start); });
            int n = 0;
            foreach (Bell b in all)
            {
                bool cls = b.Key.Length > 0 && char.IsDigit(b.Key[0]);
                if (cls) n++;
                else if (n > 0 && b.Label.IndexOf("lunch", StringComparison.OrdinalIgnoreCase) >= 0) return n;
            }
            return fallback;
        }

        // moves each class "shift" places down inside the rotating group(s); everything else stays put
        static Dictionary<int, string> RotateDay(Dictionary<int, string> map, int shift, int mode, int first, int last, int lunchAfter, int count)
        {
            Dictionary<int, string> r = new Dictionary<int, string>(map);
            List<List<int>> groups = new List<List<int>>();
            if (mode == 1)
            {
                List<int> g = new List<int>();
                for (int p = first; p <= last; p++) g.Add(p);
                groups.Add(g);
            }
            else if (mode == 2)
            {
                List<int> g1 = new List<int>(), g2 = new List<int>();
                for (int p = first; p <= last; p++) { if (p <= lunchAfter) g1.Add(p); else g2.Add(p); }
                groups.Add(g1);
                groups.Add(g2);
            }
            foreach (List<int> g in groups)
            {
                int n = g.Count;
                if (n < 2) continue;
                for (int k = 0; k < n; k++)
                {
                    int src = g[(((k + shift) % n) + n) % n];
                    string v;
                    if (map.TryGetValue(src, out v)) r[g[k]] = v; else r.Remove(g[k]);
                }
            }
            return r;
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
                case 0: WizLook(); break;
                case 1: WizTimes(); break;
                case 2: WizDays(); break;
                case 3: WizClasses(); break;
                case 4: WizExtras(); break;
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

        bool WizBuild(out BellSchedule result)
        {
            result = null;
            TimeSpan first;
            int cm, pm, n, la, lm;
            if (wName.Trim().Length == 0 || wName.IndexOfAny(new char[] { '|', ';', '~', '=', '#', '^' }) >= 0) { wError = "Give the schedule a name without symbols like | ; ~ = # ^."; return false; }
            if (!Bell.TryTime(wStart, out first)) { wError = "The first class start time is not a time. Try 8:05 AM."; return false; }
            if (!int.TryParse(wPass.Trim(), out pm) || pm < 0 || pm > 60) { wError = "Minutes between classes should be a number from 0 to 60."; return false; }
            if (!int.TryParse(wCount.Trim(), out n) || n < 1 || n > 14) { wError = "Number of classes should be 1 to 14."; return false; }
            if (!int.TryParse(wLunchAfter.Trim(), out la) || la < 0 || la > n) { wError = "Lunch after class number should be 0 (no lunch) to " + n + "."; return false; }
            if (wMods)
            {
                int mm, mp, lmods;
                if (!int.TryParse(wModMin.Trim(), out mm) || mm < 1 || mm > 60) { wError = "Minutes per mod should be 1 to 60."; return false; }
                if (!int.TryParse(wModsPer.Trim(), out mp) || mp < 1 || mp > 12) { wError = "Mods per class should be 1 to 12."; return false; }
                if (!int.TryParse(wLunchMods.Trim(), out lmods) || lmods < 0 || lmods > 12) { wError = "Lunch length in mods should be 0 to 12."; return false; }
                cm = mm * mp;
                lm = mm * lmods;
            }
            else
            {
                if (!int.TryParse(wClass.Trim(), out cm) || cm < 5 || cm > 240) { wError = "Minutes per class should be 5 to 240."; return false; }
                if (!int.TryParse(wLunch.Trim(), out lm) || lm < 0 || lm > 120) { wError = "Lunch minutes should be 0 to 120."; return false; }
            }
            result = BellSchedule.Generate(wName.Trim(), first, cm, pm, n, la, lm);
            return true;
        }

        void WizTimes()
        {
            WizHead("Bell times", "When does each class start and end? Tell me the pattern and I will make every period for you. You can fine-tune any row afterwards.");
            if (snap.AutoBells.Count > 0)
            {
                AddNotice("Campus shared " + snap.AutoBells.Count + " bell schedule" + (snap.AutoBells.Count == 1 ? "" : "s") + ". You can use those instead of typing.");
                AddButton("Use the times from Campus", "", 1, X0(), vy, delegate { ImportAutoBells(true); wName = editName; wizStep = 2; wError = ""; Redo(); });
                vy += Theme.S(58);
            }
            else if (cfg.Schedules.Count > 0)
            {
                AddNotice("You already have a bell schedule. A new one with the same name replaces it; a different name adds another.");
            }
            int gap = Theme.S(16);
            int colW = Math.Min(Theme.S(300), (CW() - gap) / 2);
            int x2 = X0() + colW + gap;
            WizField("Schedule name", wName, X0(), colW, delegate(string v) { wName = v; });
            WizField("First class starts", wStart, x2, colW, delegate(string v) { wStart = v; });
            vy += Theme.S(76);

            DarkCheck mods = new DarkCheck();
            mods.Text = "My school uses mods (short blocks that make up each class)";
            mods.Checked = wMods;
            mods.BackColor = Theme.Bg0;
            mods.SetBounds(X0(), vy, CW(), Theme.S(30));
            mods.Changed += delegate { wMods = mods.Checked; Redo(); };
            host.Controls.Add(mods);
            vy += Theme.S(44);

            if (wMods)
            {
                WizField("Minutes per mod", wModMin, X0(), colW, delegate(string v) { wModMin = v; });
                WizField("Mods per class", wModsPer, x2, colW, delegate(string v) { wModsPer = v; });
                vy += Theme.S(76);
                WizField("Minutes between classes", wPass, X0(), colW, delegate(string v) { wPass = v; });
                WizField("Number of classes", wCount, x2, colW, delegate(string v) { wCount = v; });
                vy += Theme.S(76);
                WizField("Lunch after class number (0 for none)", wLunchAfter, X0(), colW, delegate(string v) { wLunchAfter = v; });
                WizField("Lunch length in mods", wLunchMods, x2, colW, delegate(string v) { wLunchMods = v; });
                vy += Theme.S(76);
            }
            else
            {
                WizField("Minutes per class", wClass, X0(), colW, delegate(string v) { wClass = v; });
                WizField("Minutes between classes", wPass, x2, colW, delegate(string v) { wPass = v; });
                vy += Theme.S(76);
                WizField("Number of classes", wCount, X0(), colW, delegate(string v) { wCount = v; });
                WizField("Lunch after class number (0 for none)", wLunchAfter, x2, colW, delegate(string v) { wLunchAfter = v; });
                vy += Theme.S(76);
                WizField("Lunch minutes", wLunch, X0(), colW, delegate(string v) { wLunch = v; });
                vy += Theme.S(76);
            }

            AddButton("Preview these times", "", 0, X0(), vy, delegate
            {
                BellSchedule b;
                if (WizBuild(out b)) { wPreview = b; wError = ""; } else wPreview = null;
                Redo();
            });
            vy += Theme.S(56);
            if (wPreview != null)
            {
                ListCard lc = new ListCard();
                lc.Heading = wPreview.Name + "  ·  " + wPreview.Entries.Count + " blocks";
                foreach (Bell b in wPreview.Entries)
                {
                    Item it = new Item();
                    it.Lead = b.Label;
                    it.Title = b.Text();
                    lc.Items.Add(it);
                }
                AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
                vy += lc.PreferredHeight() + Theme.S(16);
            }
            WizFooter("Next", delegate
            {
                BellSchedule b;
                if (!WizBuild(out b)) return false;
                BellSchedule old = cfg.Find(b.Name);
                if (old != null) cfg.Schedules.Remove(old);
                cfg.Schedules.Add(b);
                editName = b.Name;
                int cnt = int.Parse(wCount.Trim());
                int lastv;
                if (!int.TryParse(wLast, out lastv) || lastv > cnt || lastv < 1) wLast = cnt.ToString();
                cfg.Save();
                wPreview = null;
                Log.Write("setup: bell schedule '" + b.Name + "' made with " + b.Entries.Count + " blocks (mods=" + wMods + ")");
                return true;
            });
        }

        void WizDays()
        {
            WizHead("Rotating days", "Some schools rotate: Day A has one order of classes and Day B another. Tell me how yours works.");
            string[] campus = CampusLabelsInOrder();
            if (campus.Length > 0)
            {
                List<string> show = new List<string>();
                for (int i = 0; i < campus.Length && i < 6; i++) show.Add(campus[i]);
                AddNotice("Campus labels each school day with a schedule type: " + string.Join(", ", show.ToArray()) + (campus.Length > 6 ? ", …" : "") + " (" + campus.Length + " kinds). The Schedule and Now pages already use these.");
            }
            AddLabel("Does the order of your classes change from day to day?", Theme.Body, Theme.T1, X0(), vy, CW(), Theme.S(26));
            vy += Theme.S(34);
            Segmented rm = new Segmented();
            rm.SetOptions(new string[] { "No", "Yes: the whole day rotates", "Yes: before and after lunch rotate separately" }, wRotMode);
            rm.Changed += delegate { wRotMode = rm.Selected; Redo(); };
            rm.Location = new Point(X0(), vy);
            host.Controls.Add(rm);
            vy += rm.Height + Theme.S(22);

            if (wRotMode > 0)
            {
                bool needOwn = campus.Length < 2;
                if (!needOwn)
                {
                    DarkCheck own = new DarkCheck();
                    own.Text = "Use my own day names (like A and B) instead of Campus's";
                    own.Checked = wOwnLabels;
                    own.BackColor = Theme.Bg0;
                    own.SetBounds(X0(), vy, CW(), Theme.S(30));
                    own.Changed += delegate { wOwnLabels = own.Checked; Redo(); };
                    host.Controls.Add(own);
                    vy += Theme.S(44);
                }
                else wOwnLabels = true;
                int gap = Theme.S(16);
                int colW = Math.Min(Theme.S(300), (CW() - gap) / 2);
                if (wOwnLabels)
                {
                    WizField("Day names, separated by commas", wLabels, X0(), Math.Min(CW(), Theme.S(420)), delegate(string v) { wLabels = v; });
                    vy += Theme.S(76);
                    string[] labs = SplitLabels(wLabels);
                    if (labs.Length >= 2)
                    {
                        AddLabel("TODAY IS", Theme.SemiSmall, Theme.T3, X0(), vy, CW(), Theme.S(20));
                        vy += Theme.S(24);
                        Segmented ts = new Segmented();
                        ts.SetOptions(labs, Math.Min(wTodayIdx, labs.Length - 1));
                        ts.Changed += delegate { wTodayIdx = ts.Selected; };
                        ts.Location = new Point(X0(), vy);
                        host.Controls.Add(ts);
                        vy += ts.Height + Theme.S(20);
                    }
                }
                AddLabel("Which classes rotate? Periods outside this range stay the same every day.", Theme.Body, Theme.T2, X0(), vy, CW(), Theme.S(26));
                vy += Theme.S(34);
                WizField("First rotating period", wFirst, X0(), colW, delegate(string v) { wFirst = v; });
                WizField("Last rotating period", wLast, X0() + colW + gap, colW, delegate(string v) { wLast = v; });
                vy += Theme.S(76);
                AddLabel(wRotMode == 2 ? "Classes before lunch rotate among themselves, and classes after lunch rotate among themselves."
                    : "All the periods in that range rotate together, one place per day.", Theme.Small, Theme.T3, X0(), vy, CW(), Theme.S(24));
                vy += Theme.S(36);
            }
            WizFooter("Next", delegate
            {
                if (wRotMode == 0)
                {
                    cfg.RotOn = false;
                    cfg.Save();
                    return true;
                }
                int count = ClassBells(cfg.Find(wName) ?? Editing()).Count;
                int f, l;
                if (!int.TryParse(wFirst.Trim(), out f) || !int.TryParse(wLast.Trim(), out l) || f < 1 || l > Math.Max(1, count) || f > l)
                {
                    wError = "First and last rotating period should be numbers from 1 to " + Math.Max(1, count) + ", first not after last.";
                    return false;
                }
                if (wOwnLabels)
                {
                    string[] labs = SplitLabels(wLabels);
                    if (labs.Length < 2) { wError = "Type at least two day names, like A,B."; return false; }
                    cfg.RotOn = true;
                    cfg.RotLabels = string.Join(",", labs);
                    cfg.RotAnchor = DateTime.Today.ToString("yyyy-MM-dd");
                    cfg.RotIndex = Math.Min(wTodayIdx, labs.Length - 1);
                }
                else cfg.RotOn = false;
                cfg.Save();
                Log.Write("setup: rotation mode=" + wRotMode + " own=" + wOwnLabels + " range=" + f + "-" + l);
                return true;
            });
        }

        static string[] SplitLabels(string text)
        {
            List<string> l = new List<string>();
            foreach (string p in (text ?? "").Split(','))
            {
                string t = p.Trim();
                if (t.Length > 0 && t.IndexOfAny(new char[] { '|', ';', '~', '=', '#', '^' }) < 0 && !l.Contains(t)) l.Add(t);
            }
            return l.ToArray();
        }

        string[] RotationLabels()
        {
            if (wRotMode == 0) return new string[0];
            return cfg.RotOn ? Sched.Labels(cfg) : CampusLabelsInOrder();
        }

        void WizClasses()
        {
            BellSchedule bs = cfg.Find(wName) ?? Editing();
            List<Bell> bells = ClassBells(bs);
            string[] rotLabels = RotationLabels();
            WizHead("Which class is in which period?", rotLabels.Length >= 2
                ? "Pick the classes for one day. The other days follow your rotation automatically, and you can adjust them later."
                : "Pick the class for each period. Leave a period on Automatic to use what Campus says.");
            if (bs == null || bells.Count == 0)
            {
                AddNotice("There are no numbered periods yet. Go back and make your bell times first.");
                WizFooter("Next", delegate { return true; });
                return;
            }
            if (rotLabels.Length >= 2)
            {
                if (Array.IndexOf(rotLabels, wDay) < 0) wDay = rotLabels[0];
                AddLabel("Setting up:", Theme.Body, Theme.T2, X0(), vy + Theme.S(8), Theme.S(90), Theme.S(26));
                PillButton dp = AddButton(wDay, "", 0, X0() + Theme.S(90), vy, null);
                PillButton dps = dp;
                dp.Click += delegate
                {
                    DarkMenu.Pick(dps, rotLabels, Array.IndexOf(rotLabels, wDay), delegate(int k) { wDay = rotLabels[k]; wPick.Clear(); Redo(); });
                };
                vy += Theme.S(58);
            }
            else wDay = "";

            List<string> courses = TermCourses();
            if (courses.Count == 0) AddNotice("No classes were found for this term, so there is nothing to pick yet.");
            PlainCard card = new PlainCard();
            int rowH = Theme.S(56);
            int cardH = Theme.S(16) + bells.Count * rowH + Theme.S(8);
            AddCard(card, X0(), vy, CW(), cardH);
            int y = Theme.S(12);
            for (int i = 0; i < bells.Count; i++)
            {
                int pos = i + 1;
                Bell b = bells[i];
                Label l = new Label();
                l.AutoSize = false; l.UseMnemonic = false; l.BackColor = Theme.Bg2; l.ForeColor = Theme.T1; l.Font = Theme.Body;
                l.Text = "Period " + b.Label + "   " + b.Text();
                l.TextAlign = ContentAlignment.MiddleLeft;
                l.SetBounds(Theme.S(20), y, Math.Min(Theme.S(250), CW() / 2 - Theme.S(30)), Theme.S(44));
                card.Controls.Add(l);

                bool none;
                RosterItem auto = Sched.ClassFor(cfg, snap, term, bs.Name, b, wDay, out none);
                string cur;
                wPick.TryGetValue(pos, out cur);
                cur = cur ?? "";
                string txt;
                if (cur == "-") txt = "Free period";
                else if (cur.Length > 0) txt = CourseByKey(courses, cur) ?? "Pick a class";
                else txt = auto != null ? "Automatic: " + auto.Course : "Automatic: none found";
                PillButton pb = new PillButton();
                pb.Text = txt;
                pb.Glyph = "";
                pb.Kind = cur.Length > 0 ? 1 : 0;
                int bx = Theme.S(20) + l.Width + Theme.S(10);
                pb.SetBounds(bx, y + Theme.S(2), CW() - bx - Theme.S(20), Theme.S(40));
                PillButton self = pb;
                int curIdx = cur == "-" ? 1 : (cur.Length > 0 ? Math.Max(0, courses.FindIndex(delegate(string cn) { return Settings.Slug(cn) == cur; }) + 2) : 0);
                pb.Click += delegate
                {
                    string[] opts = new string[courses.Count + 2];
                    opts[0] = "Automatic (from Campus)";
                    opts[1] = "Free period / no class";
                    for (int c = 0; c < courses.Count; c++) opts[c + 2] = courses[c];
                    DarkMenu.Pick(self, opts, curIdx, delegate(int k)
                    {
                        if (k == 0) wPick.Remove(pos);
                        else if (k == 1) wPick[pos] = "-";
                        else wPick[pos] = Settings.Slug(courses[k - 2]);
                        Redo();
                    });
                };
                card.Controls.Add(pb);
                y += rowH;
            }
            vy += cardH + Theme.S(16);

            WizFooter("Save and continue", delegate
            {
                // what each position holds on the day being set up (a pick, or what Campus says)
                Dictionary<int, string> baseMap = new Dictionary<int, string>();
                for (int i = 0; i < bells.Count; i++)
                {
                    int pos = i + 1;
                    string v;
                    if (wPick.TryGetValue(pos, out v) && v.Length > 0) { baseMap[pos] = v; continue; }
                    bool none2;
                    RosterItem auto2 = Sched.ClassFor(cfg, snap, term, bs.Name, bells[i], wDay, out none2);
                    if (none2) baseMap[pos] = "-";
                    else if (auto2 != null) baseMap[pos] = Settings.Slug(auto2.Course);
                }
                string[] days = rotLabels.Length >= 2 ? rotLabels : new string[] { "" };
                int d0 = Math.Max(0, Array.IndexOf(days, wDay));
                int cnt = bells.Count;
                int first = 1, last = cnt, lunchPos = 0;
                int la0;
                int.TryParse(wLunchAfter.Trim(), out la0);
                lunchPos = LunchPos(bs, la0);
                int.TryParse(wFirst.Trim(), out first);
                int.TryParse(wLast.Trim(), out last);
                if (first < 1) first = 1;
                if (last < first || last > cnt) last = cnt;
                int written = 0;
                for (int di = 0; di < days.Length; di++)
                {
                    Dictionary<int, string> map = days.Length >= 2 ? RotateDay(baseMap, di - d0, wRotMode, first, last, lunchPos, cnt) : baseMap;
                    for (int i = 0; i < bells.Count; i++)
                    {
                        string key = Settings.PeriodKey("", bells[i].Label, days[di]);
                        string v;
                        if (map.TryGetValue(i + 1, out v)) { cfg.PeriodMap[key] = v; written++; }
                    }
                }
                cfg.Save();
                Log.Write("setup: assigned classes to periods, " + written + " entries across " + days.Length + " day type(s)");
                return true;
            });
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
            WizHead("You are set", "Here is how a typical day looks now. Change anything later under Settings, then Bell schedules.");
            DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
            if (!p.School) p = Sched.Plan(cfg, snap, term, Sched.NextSchoolDay(cfg, snap, DateTime.Today));
            ListCard lc = new ListCard();
            lc.Heading = p.Date.ToString("dddd") + (p.DayLabel.Length > 0 ? "  ·  " + Sched.DayName(p.DayLabel) : "");
            lc.EmptyText = "No periods yet. Add bell times from Settings.";
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
