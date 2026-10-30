using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CampusMidnight
{
    // ---------------------------------------------------------------- small dialogs
    class NameForm : DarkForm
    {
        public string Value = "";
        readonly DarkInput box;

        public NameForm(string title, string caption, string initial) : base(title)
        {
            ClientSize = new Size(Theme.S(480), Theme.S(230));
            Fw = ClientSize.Width - Theme.S(56);
            Lbl(caption, Theme.Body, Theme.T2, Theme.S(26));
            Gap(8);
            box = new DarkInput(false, false);
            box.Text = initial;
            Add(box, Theme.S(44));
            Gap(18);
            PillButton ok = new PillButton(); ok.Text = "Save"; ok.Kind = 1;
            ok.Click += delegate { Done(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
            box.Box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Done(); }
            };
            Shown += delegate { box.Box.Focus(); box.Box.SelectAll(); };
        }

        void Done()
        {
            string t = box.Text.Trim();
            if (t.Length == 0 || t.IndexOfAny(new char[] { '|', ';', '~', '=' }) >= 0) { box.Invalid = true; box.Invalidate(); return; }
            Value = t;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // "8 classes, 50 minutes each..." makes a whole day of bells in one go
    class FillForm : DarkForm
    {
        public BellSchedule Result;
        readonly DarkInput iName, iStart, iClass, iPass, iCount, iLunchAfter, iLunch;

        DarkInput Field(string caption, string value)
        {
            Lbl(caption.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, Theme.S(22));
            DarkInput d = new DarkInput(false, false);
            d.Text = value;
            Add(d, Theme.S(44));
            Gap(10);
            return d;
        }

        public FillForm(string suggestedName) : base("Fill in a whole day")
        {
            ClientSize = new Size(Theme.S(520), Theme.S(700));
            Fw = ClientSize.Width - Theme.S(56) - SystemInformation.VerticalScrollBarWidth;
            Wrapped("Tell it when the first class starts and how long classes are. It makes every period for you, and you can adjust any row afterwards.", Theme.Body, Theme.T2);
            Gap(10);
            iName = Field("Schedule name", suggestedName);
            iStart = Field("First class starts", "8:05 AM");
            iClass = Field("Minutes per class", "50");
            iPass = Field("Minutes between classes", "5");
            iCount = Field("Number of classes", "7");
            iLunchAfter = Field("Lunch after class number (0 for none)", "4");
            iLunch = Field("Lunch length in minutes", "30");
            PillButton ok = new PillButton(); ok.Text = "Make schedule"; ok.Kind = 1;
            ok.Click += delegate { Make(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
            Finish();
        }

        static bool Num(DarkInput d, int lo, int hi, out int v)
        {
            bool ok = int.TryParse(d.Text.Trim(), out v) && v >= lo && v <= hi;
            d.Invalid = !ok;
            d.Invalidate();
            return ok;
        }

        void Make()
        {
            TimeSpan first;
            int cm, pm, n, la, lm;
            bool good = true;
            if (!Bell.TryTime(iStart.Text, out first)) { iStart.Invalid = true; good = false; } else iStart.Invalid = false;
            iStart.Invalidate();
            string name = iName.Text.Trim();
            if (name.Length == 0 || name.IndexOfAny(new char[] { '|', ';', '~', '=' }) >= 0) { iName.Invalid = true; good = false; } else iName.Invalid = false;
            iName.Invalidate();
            if (!Num(iClass, 5, 240, out cm)) good = false;
            if (!Num(iPass, 0, 60, out pm)) good = false;
            if (!Num(iCount, 1, 14, out n)) good = false;
            if (!Num(iLunchAfter, 0, 14, out la)) good = false;
            if (!Num(iLunch, 0, 120, out lm)) good = false;
            if (!good) return;
            Result = BellSchedule.Generate(name, first, cm, pm, n, la, lm);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ---------------------------------------------------------------- the Now page and the bell editor
    partial class MainForm
    {
        string editName = "";

        // ============================================================ Now
        void BuildNow()
        {
            Header("Now", DateTime.Now.ToString("dddd, MMMM d"));
            if (snap == null)
            {
                AddLabel("Loading your classes…", Theme.Body, Theme.T2, X0(), vy, CW(), Theme.S(28));
                vy += Theme.S(40);
                return;
            }
            if (cfg.Schedules.Count == 0)
            {
                AddNotice("Campus does not share bell times, so add yours once. It takes about a minute, and then this page shows your current class, a live countdown and what comes next.");
                AddButton("Set up bell schedule", "", 1, X0(), vy, delegate { GoTo("bells"); });
                vy += Theme.S(56);
                return;
            }

            plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            NowHero hero = new NowHero();
            hero.Plan = plan;
            hero.DayText = plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) : "";
            DateTime nx = Sched.NextSchoolDay(cfg, snap, DateTime.Today);
            string nl, nf;
            nl = Sched.LabelFor(cfg, snap, nx, out nf);
            string nextText = "Next school day: " + nx.ToString("dddd, MMM d") + (nl.Length > 0 ? "  ·  " + Sched.DayName(nl) : "") + ".";
            if (!plan.School)
            {
                hero.Headline = "No school today";
                hero.Detail = plan.WhyNot + " " + nextText;
            }
            else if (plan.Slots.Count == 0)
            {
                hero.Headline = "Nothing scheduled";
                hero.Detail = "The bell schedule for today has no periods yet.";
            }
            else hero.Detail = nextText;
            liveHero = hero;
            AddCard(hero, X0(), vy, CW(), NowHero.HeightFor());
            vy += NowHero.HeightFor() + Theme.S(16);

            int gap = Theme.S(16);
            int cols = CW() >= Theme.S(900) ? 4 : 2;
            int tw = (CW() - gap * (cols - 1)) / cols;
            int th = Theme.S(100);
            TimeSpan now = DateTime.Now.TimeOfDay;
            TileCard t1 = new TileCard();
            t1.Big = plan.DayLabel.Length > 0 ? plan.DayLabel : "–";
            t1.Label = plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) + (plan.LabelFrom.Length > 0 ? ", " + plan.LabelFrom : "") : "Day (turn on rotating days to see it)";
            TileCard t2 = new TileCard();
            t2.Big = plan.Sched != null ? plan.Sched.Name : "–";
            t2.Label = "Bell schedule today";
            TileCard t3 = new TileCard();
            t3.Big = Sched.ClassesLeft(plan, now).ToString();
            t3.Label = "Classes left today";
            TileCard t4 = new TileCard();
            t4.Big = plan.Slots.Count > 0 ? Bell.ClockShort(plan.Slots[plan.Slots.Count - 1].B.End) : "–";
            t4.Label = "School ends";
            TileCard[] tl = new TileCard[] { t1, t2, t3, t4 };
            for (int i = 0; i < tl.Length; i++)
                AddCard(tl[i], X0() + (i % cols) * (tw + gap), vy + (i / cols) * (th + gap), tw, th);
            vy += ((tl.Length + cols - 1) / cols) * (th + gap);

            if (plan.School && plan.Slots.Count > 0)
            {
                NowTimeline tline = new NowTimeline();
                tline.Plan = plan;
                tline.SlotClick += delegate(Slot s) { if (s.C != null) OpenCourse(s.C); };
                liveTimeline = tline;
                AddCard(tline, X0(), vy, CW(), tline.PreferredHeight());
                vy += tline.PreferredHeight() + gap;
            }
            AddButton("Edit bell schedules", "", 0, X0(), vy, delegate { GoTo("bells"); });
            vy += Theme.S(56);
        }

        // ============================================================ bell schedule editor
        BellSchedule Editing()
        {
            if (cfg.Schedules.Count == 0) return null;
            BellSchedule s = cfg.Find(editName);
            if (s == null) { s = cfg.Schedules[0]; editName = s.Name; }
            return s;
        }

        bool redoPending;

        // rebuild the page once the current event has finished; ignores repeats (focus changes while the page is torn down)
        void Redo()
        {
            if (redoPending || !IsHandleCreated) return;
            redoPending = true;
            BeginInvoke(new MethodInvoker(delegate
            {
                try { ShowView(true); }
                finally { redoPending = false; }
            }));
        }

        string UniqueName(string wanted)
        {
            string n = wanted;
            int i = 2;
            while (cfg.Find(n) != null) n = wanted + " " + (i++);
            return n;
        }

        static string NextLabel(BellSchedule s)
        {
            int max = 0;
            foreach (Bell b in s.Entries)
            {
                int v;
                if (int.TryParse(b.Label.Trim(), out v) && v > max) max = v;
            }
            return (max + 1).ToString();
        }

        void BuildBells()
        {
            Header("Bell schedules", "When each period starts and ends, and which days are A or B.");
            if (snap == null) return;
            int gap = Theme.S(16);
            BellSchedule cur = Editing();

            // ---- chooser
            if (cfg.Schedules.Count > 0)
            {
                string[] names = new string[cfg.Schedules.Count];
                for (int i = 0; i < names.Length; i++) names[i] = cfg.Schedules[i].Name;
                Segmented sg = new Segmented();
                sg.SetOptions(names, Math.Max(0, Array.IndexOf(names, cur.Name)));
                sg.Changed += delegate
                {
                    editName = names[sg.Selected];
                    Redo();
                };
                sg.Location = new Point(X0(), vy);
                host.Controls.Add(sg);
                vy += sg.Height + Theme.S(12);
            }
            int bx = X0();
            PillButton bNew = AddButton("New", "", 0, bx, vy, delegate { NewSchedule(); });
            bx += bNew.Width + Theme.S(8);
            PillButton bFill = AddButton("Fill in a whole day", "", 1, bx, vy, delegate { FillSchedule(); });
            bx += bFill.Width + Theme.S(8);
            if (cur != null)
            {
                PillButton bRen = AddButton("Rename", "", 0, bx, vy, delegate { RenameSchedule(); });
                bx += bRen.Width + Theme.S(8);
                PillButton bDup = AddButton("Duplicate", "", 0, bx, vy, delegate { DuplicateSchedule(); });
                bx += bDup.Width + Theme.S(8);
                PillButton bDel = AddButton("Delete", "", 3, bx, vy, delegate { DeleteSchedule(); });
            }
            vy += Theme.S(58);

            if (cur == null)
            {
                AddNotice("No bell schedule yet. Choose Fill in a whole day to make one from a start time and class length, or New to type each period yourself. Number a period 1, 2, 3… to link it to that period in your Campus schedule.");
                return;
            }

            // ---- periods
            int rowH = Theme.S(54);
            int head = Theme.S(52);
            int rows = Math.Max(1, cur.Entries.Count);
            PlainCard pc = new PlainCard();
            pc.Heading = cur.Name + "  ·  periods";
            int cardH = head + rows * rowH + Theme.S(70);
            AddCard(pc, X0(), vy, CW(), cardH);

            int pad = Theme.S(20);
            int wLabel = Theme.S(150), wTime = Theme.S(132), wDel = Theme.S(44), g2 = Theme.S(10);
            Label c1 = ColCaption(pc, "PERIOD", pad, Theme.S(34), wLabel);
            ColCaption(pc, "STARTS", pad + wLabel + g2, Theme.S(34), wTime);
            ColCaption(pc, "ENDS", pad + wLabel + wTime + g2 * 2, Theme.S(34), wTime);
            int y = head;
            BellSchedule sched = cur;
            if (sched.Entries.Count == 0)
            {
                Label e = new Label();
                e.AutoSize = false; e.UseMnemonic = false; e.BackColor = Theme.Bg2; e.ForeColor = Theme.T3; e.Font = Theme.Body;
                e.Text = "No periods yet. Add the first one below.";
                e.SetBounds(pad, y + Theme.S(8), CW() - pad * 2, Theme.S(28));
                pc.Controls.Add(e);
                y += rowH;
            }
            for (int i = 0; i < sched.Entries.Count; i++)
            {
                Bell bell = sched.Entries[i];
                DarkInput inL = new DarkInput(false, false);
                inL.Text = bell.Label;
                inL.SetBounds(pad, y, wLabel, Theme.S(44));
                DarkInput inS = new DarkInput(false, false);
                inS.Text = Bell.Clock(bell.Start);
                inS.SetBounds(pad + wLabel + g2, y, wTime, Theme.S(44));
                DarkInput inE = new DarkInput(false, false);
                inE.Text = Bell.Clock(bell.End);
                inE.SetBounds(pad + wLabel + wTime + g2 * 2, y, wTime, Theme.S(44));
                pc.Controls.Add(inL);
                pc.Controls.Add(inS);
                pc.Controls.Add(inE);

                inL.Box.TextChanged += delegate
                {
                    string t = inL.Text.Trim();
                    bool ok = t.Length > 0 && t.IndexOfAny(new char[] { '|', ';', '~', '=' }) < 0;
                    inL.Invalid = !ok;
                    inL.Invalidate();
                    if (ok) { bell.Label = t; cfg.Save(); }
                };
                EventHandler timeChanged = delegate
                {
                    TimeSpan a, b;
                    bool okS = Bell.TryTime(inS.Text, out a);
                    bool okE = Bell.TryTime(inE.Text, out b);
                    inS.Invalid = !okS;
                    inE.Invalid = !okE || (okS && b <= a);
                    inS.Invalidate();
                    inE.Invalidate();
                    if (okS && okE && b > a) { bell.Start = a; bell.End = b; cfg.Save(); }
                };
                inS.Box.TextChanged += timeChanged;
                inE.Box.TextChanged += timeChanged;
                PillButton del = new PillButton();
                del.Glyph = ""; del.Text = ""; del.Kind = 3;
                del.SetBounds(pad + wLabel + wTime * 2 + g2 * 3, y, wDel, Theme.S(44));
                del.Click += delegate
                {
                    sched.Entries.Remove(bell);
                    cfg.Save();
                    Redo();
                };
                pc.Controls.Add(del);
                y += rowH;
            }
            PillButton add = new PillButton();
            add.Text = "Add period"; add.Glyph = "";
            add.SetBounds(pad, y + Theme.S(6), add.PreferredWidth(), Theme.S(40));
            add.Click += delegate
            {
                Bell nb = new Bell();
                nb.Label = NextLabel(sched);
                TimeSpan last = sched.Entries.Count > 0 ? sched.Entries[sched.Entries.Count - 1].End + TimeSpan.FromMinutes(5) : new TimeSpan(8, 0, 0);
                nb.Start = last;
                nb.End = last + TimeSpan.FromMinutes(50);
                sched.Entries.Add(nb);
                cfg.Save();
                Redo();
            };
            pc.Controls.Add(add);
            Label hint = new Label();
            hint.AutoSize = false; hint.UseMnemonic = false; hint.BackColor = Theme.Bg2; hint.ForeColor = Theme.T3; hint.Font = Theme.Small;
            hint.Text = "Name a period 1, 2, 3… to link it to that period in your Campus schedule. Anything else (Lunch, Advisory) shows as a break.";
            hint.SetBounds(pad + add.Width + Theme.S(14), y + Theme.S(8), CW() - pad * 2 - add.Width - Theme.S(14), Theme.S(40));
            pc.Controls.Add(hint);
            vy += cardH + gap;

            BuildWeekCard(gap);
            BuildRotationCard(gap);
            BuildCourseDays(gap);
        }

        Label ColCaption(Control parent, string text, int x, int y, int w)
        {
            Label l = new Label();
            l.AutoSize = false; l.UseMnemonic = false; l.BackColor = Theme.Bg2; l.ForeColor = Theme.T3; l.Font = Theme.SemiSmall;
            l.Text = text;
            l.SetBounds(x, y, w, Theme.S(18));
            parent.Controls.Add(l);
            return l;
        }

        // ---- which schedule on which weekday
        void BuildWeekCard(int gap)
        {
            if (cfg.Schedules.Count < 2)
            {
                SectionTitle("Different days");
                AddLabel("Make a second schedule (for example Early release or Assembly) to use it on certain weekdays or just today.", Theme.Body, Theme.T2, X0(), vy - Theme.S(4), CW(), Theme.S(26));
                vy += Theme.S(44);
                return;
            }
            SectionTitle("Which schedule on which day");
            string[] names = new string[cfg.Schedules.Count];
            for (int i = 0; i < names.Length; i++) names[i] = cfg.Schedules[i].Name;
            PlainCard card = new PlainCard();
            int cardH = Theme.S(150);
            AddCard(card, X0(), vy, CW(), cardH);
            string[] days = new string[] { "Mon", "Tue", "Wed", "Thu", "Fri" };
            int x = Theme.S(20), y = Theme.S(18);
            for (int d = 0; d < days.Length; d++)
            {
                int dow = d + 1;
                string cur = cfg.WeekMap[dow].Length > 0 && cfg.Find(cfg.WeekMap[dow]) != null ? cfg.WeekMap[dow] : names[0];
                PillButton b = new PillButton();
                b.Text = days[d] + "  ·  " + cur;
                b.Glyph = "";
                int w = b.PreferredWidth();
                if (x + w > CW() - Theme.S(20)) { x = Theme.S(20); y += Theme.S(48); cardH += Theme.S(48); card.Height = cardH; }
                b.SetBounds(x, y, w, Theme.S(38));
                PillButton self = b;
                string curName = cur;
                b.Click += delegate
                {
                    DarkMenu.Pick(self, names, Array.IndexOf(names, curName), delegate(int k)
                    {
                        cfg.WeekMap[dow] = k == 0 ? "" : names[k];
                        cfg.Save();
                        Redo();
                    });
                };
                card.Controls.Add(b);
                x += w + Theme.S(10);
            }
            y += Theme.S(56);
            string todayKey = DateTime.Today.ToString("yyyy-MM-dd");
            bool has = cfg.OverrideDate == todayKey && cfg.Find(cfg.OverrideName) != null;
            PillButton ovr = new PillButton();
            ovr.Text = has ? "Today only  ·  " + cfg.OverrideName : "Use a different schedule today only";
            ovr.Glyph = ""; ovr.Kind = has ? 1 : 0;
            ovr.SetBounds(Theme.S(20), y, ovr.PreferredWidth(), Theme.S(38));
            PillButton ovrSelf = ovr;
            ovr.Click += delegate
            {
                string[] opts = new string[names.Length + 1];
                opts[0] = "Normal schedule";
                for (int i = 0; i < names.Length; i++) opts[i + 1] = names[i];
                DarkMenu.Pick(ovrSelf, opts, has ? Array.IndexOf(names, cfg.OverrideName) + 1 : 0, delegate(int k)
                {
                    if (k == 0) { cfg.OverrideDate = ""; cfg.OverrideName = ""; }
                    else { cfg.OverrideDate = todayKey; cfg.OverrideName = names[k - 1]; }
                    cfg.Save();
                    Redo();
                });
            };
            card.Controls.Add(ovr);
            card.Height = y + Theme.S(60);
            vy += card.Height + gap + Theme.S(6);
        }

        // ---- A day / B day
        void BuildRotationCard(int gap)
        {
            SectionTitle("Rotating days (A day, B day…)");
            PlainCard card = new PlainCard();
            AddCard(card, X0(), vy, CW(), Theme.S(120));
            int pad = Theme.S(20), y = Theme.S(18), iw = CW() - pad * 2;

            DarkCheck on = new DarkCheck();
            on.Text = "My school rotates days";
            on.Checked = cfg.RotOn;
            on.BackColor = Theme.Bg2;
            on.SetBounds(pad, y, iw, Theme.S(28));
            on.Changed += delegate
            {
                cfg.RotOn = on.Checked;
                if (cfg.RotOn && cfg.RotAnchor.Length == 0)
                {
                    cfg.RotAnchor = DateTime.Today.ToString("yyyy-MM-dd");
                    cfg.RotIndex = 0;
                }
                cfg.Save();
                Redo();
            };
            card.Controls.Add(on);
            y += Theme.S(40);

            if (!cfg.RotOn)
            {
                Label l = new Label();
                l.AutoSize = false; l.UseMnemonic = false; l.BackColor = Theme.Bg2; l.ForeColor = Theme.T2; l.Font = Theme.Small;
                l.Text = "Turn this on if your classes change between an A day and a B day (or Day 1 / Day 2). Then you can say which day today is, and mark which classes meet on which day.";
                l.SetBounds(pad, y, iw, Theme.S(48));
                card.Controls.Add(l);
                card.Height = y + Theme.S(66);
                vy += card.Height + gap + Theme.S(6);
                return;
            }

            ColCaption(card, "DAY NAMES, SEPARATED BY COMMAS", pad, y, iw);
            y += Theme.S(22);
            DarkInput labs = new DarkInput(false, false);
            labs.Text = cfg.RotLabels;
            labs.SetBounds(pad, y, Math.Min(iw, Theme.S(320)), Theme.S(44));
            labs.Box.TextChanged += delegate
            {
                string t = labs.Text.Trim();
                bool ok = t.Length > 0 && t.IndexOfAny(new char[] { '|', ';', '~', '=' }) < 0;
                labs.Invalid = !ok;
                labs.Invalidate();
                if (ok) { cfg.RotLabels = t; cfg.Save(); }
            };
            labs.Box.Leave += delegate { Redo(); };
            card.Controls.Add(labs);
            y += Theme.S(62);

            string[] labels = Sched.Labels(cfg);
            if (labels.Length > 0)
            {
                string from;
                string curLabel = Sched.LabelFor(cfg, snap, DateTime.Today, out from);
                ColCaption(card, "TODAY IS", pad, y, iw);
                y += Theme.S(22);
                Segmented today = new Segmented();
                today.SetOptions(labels, Array.IndexOf(labels, curLabel));
                today.Changed += delegate
                {
                    cfg.RotAnchor = DateTime.Today.ToString("yyyy-MM-dd");
                    cfg.RotIndex = today.Selected;
                    cfg.Save();
                    Redo();
                };
                today.Location = new Point(pad, y);
                card.Controls.Add(today);
                y += today.Height + Theme.S(16);

                string tk = DateTime.Today.ToString("yyyy-MM-dd");
                DarkCheck ns = new DarkCheck();
                ns.Text = "There is no school today (holiday, snow day)";
                ns.Checked = cfg.NoSchool.Contains(tk);
                ns.BackColor = Theme.Bg2;
                ns.SetBounds(pad, y, iw, Theme.S(28));
                ns.Changed += delegate
                {
                    if (ns.Checked) { if (!cfg.NoSchool.Contains(tk)) cfg.NoSchool.Add(tk); }
                    else cfg.NoSchool.Remove(tk);
                    cfg.Save();
                    Redo();
                };
                card.Controls.Add(ns);
                y += Theme.S(40);

                DateTime nx = Sched.NextSchoolDay(cfg, snap, DateTime.Today);
                string nf;
                string nl = Sched.LabelFor(cfg, snap, nx, out nf);
                Label info = new Label();
                info.AutoSize = false; info.UseMnemonic = false; info.BackColor = Theme.Bg2; info.ForeColor = Theme.T2; info.Font = Theme.Body;
                info.Text = "Next school day is " + nx.ToString("dddd, MMM d") + (nl.Length > 0 ? ", " + Sched.DayName(nl) : "")
                    + ". The rotation moves forward on school days only (weekends and days you mark as no school are skipped).";
                int ih = Theme.WrapHeight(info.Text, info.Font, iw) + Theme.S(6);
                info.SetBounds(pad, y, iw, ih);
                card.Controls.Add(info);
                y += ih + Theme.S(8);
            }
            card.Height = y + Theme.S(12);
            vy += card.Height + gap + Theme.S(6);
        }

        // ---- which classes meet on which day
        void BuildCourseDays(int gap)
        {
            string[] labels = Sched.Labels(cfg);
            if (!cfg.RotOn || labels.Length < 2 || snap.Roster.Count == 0) return;
            SectionTitle("Which classes meet on which day");
            List<string> seen = new List<string>();
            PlainCard card = new PlainCard();
            int pad = Theme.S(20), rowH = Theme.S(52);
            List<RosterItem> items = snap.Roster.FindAll(delegate(RosterItem r) { return !r.Dropped && Sched.TermMatches(r, term); });
            int n = 0;
            foreach (RosterItem ri in items) { if (seen.Contains(ri.Course)) continue; seen.Add(ri.Course); n++; }
            int cardH = Theme.S(20) + n * rowH + Theme.S(14);
            AddCard(card, X0(), vy, CW(), cardH);
            string[] opts = new string[labels.Length + 1];
            opts[0] = "Every day";
            for (int i = 0; i < labels.Length; i++) opts[i + 1] = Sched.DayName(labels[i]) + " only";
            int y = Theme.S(14);
            foreach (string course in seen)
            {
                string cname = course;
                Label l = new Label();
                l.AutoSize = false; l.UseMnemonic = false; l.AutoEllipsis = true; l.BackColor = Theme.Bg2; l.ForeColor = Theme.T1; l.Font = Theme.Body;
                l.Text = cname;
                int bw = Theme.S(190);
                l.SetBounds(pad, y, CW() - pad * 2 - bw - Theme.S(10), rowH - Theme.S(8));
                l.TextAlign = ContentAlignment.MiddleLeft;
                card.Controls.Add(l);
                string v;
                int selIdx = 0;
                if (cfg.CourseDays.TryGetValue(Settings.Slug(cname), out v) && v.Length > 0)
                {
                    int k = Array.IndexOf(labels, v);
                    if (k >= 0) selIdx = k + 1;
                }
                PillButton pb = new PillButton();
                pb.Text = opts[selIdx];
                pb.Glyph = "";
                pb.Kind = selIdx > 0 ? 1 : 0;
                pb.SetBounds(CW() - pad - bw, y, bw, Theme.S(38));
                PillButton self = pb;
                int cur = selIdx;
                pb.Click += delegate
                {
                    DarkMenu.Pick(self, opts, cur, delegate(int k)
                    {
                        string slug = Settings.Slug(cname);
                        if (k == 0) cfg.CourseDays.Remove(slug);
                        else cfg.CourseDays[slug] = labels[k - 1];
                        cfg.Save();
                        Redo();
                    });
                };
                card.Controls.Add(pb);
                y += rowH;
            }
            vy += cardH + gap;
        }

        // ---- schedule list actions
        void NewSchedule()
        {
            using (NameForm f = new NameForm("New bell schedule", "Name it, for example Regular or Early release.", UniqueName(cfg.Schedules.Count == 0 ? "Regular" : "New schedule")))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                BellSchedule s = new BellSchedule();
                s.Name = UniqueName(f.Value);
                cfg.Schedules.Add(s);
                editName = s.Name;
                cfg.Save();
            }
            Redo();
        }

        void FillSchedule()
        {
            using (FillForm f = new FillForm(UniqueName(cfg.Schedules.Count == 0 ? "Regular" : "New schedule")))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Result == null) return;
                f.Result.Name = UniqueName(f.Result.Name);
                cfg.Schedules.Add(f.Result);
                editName = f.Result.Name;
                cfg.Save();
            }
            Redo();
        }

        void RenameSchedule()
        {
            BellSchedule s = Editing();
            if (s == null) return;
            using (NameForm f = new NameForm("Rename schedule", "New name for this schedule.", s.Name))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                string old = s.Name;
                if (!string.Equals(old, f.Value, StringComparison.OrdinalIgnoreCase) && cfg.Find(f.Value) != null) return;
                s.Name = f.Value;
                for (int i = 0; i < cfg.WeekMap.Length; i++) if (string.Equals(cfg.WeekMap[i], old, StringComparison.OrdinalIgnoreCase)) cfg.WeekMap[i] = s.Name;
                if (string.Equals(cfg.OverrideName, old, StringComparison.OrdinalIgnoreCase)) cfg.OverrideName = s.Name;
                editName = s.Name;
                cfg.Save();
            }
            Redo();
        }

        void DuplicateSchedule()
        {
            BellSchedule s = Editing();
            if (s == null) return;
            BellSchedule c = s.Copy(UniqueName(s.Name + " copy"));
            cfg.Schedules.Add(c);
            editName = c.Name;
            cfg.Save();
            Redo();
        }

        void DeleteSchedule()
        {
            BellSchedule s = Editing();
            if (s == null) return;
            if (MessageBox.Show(this, "Delete the schedule \"" + s.Name + "\"?", "Delete schedule", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            cfg.Schedules.Remove(s);
            for (int i = 0; i < cfg.WeekMap.Length; i++) if (string.Equals(cfg.WeekMap[i], s.Name, StringComparison.OrdinalIgnoreCase)) cfg.WeekMap[i] = "";
            if (string.Equals(cfg.OverrideName, s.Name, StringComparison.OrdinalIgnoreCase)) { cfg.OverrideName = ""; cfg.OverrideDate = ""; }
            editName = "";
            cfg.Save();
            Redo();
        }
    }
}
