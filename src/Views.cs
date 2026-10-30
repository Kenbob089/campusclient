using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CampusMidnight
{
    static class Rows
    {
        public const string NoData =
            "You are signed in, but nothing readable came back. Your school's portal may use different addresses than this app expects. " +
            "Open Settings > Diagnostics, press Copy report and send it over so the app can be adjusted. " +
            "Until then, Original site opens the normal Campus page in your browser.";

        public static string DayLabel(DateTime d)
        {
            int diff = (int)Math.Round((d.Date - DateTime.Today).TotalDays);
            if (diff == 0) return "Today";
            if (diff == 1) return "Tomorrow";
            if (diff == -1) return "Yesterday";
            return d.ToString("dddd, MMM d");
        }

        public static int DueCompare(Assign a, Assign b, bool asc)
        {
            if (a.Due.HasValue && b.Due.HasValue)
            {
                int c = a.Due.Value.CompareTo(b.Due.Value);
                if (c == 0) return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return asc ? c : -c;
            }
            if (a.Due.HasValue) return -1;
            if (b.Due.HasValue) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static void SortAssigns(List<Assign> list, bool asc)
        {
            list.Sort(delegate(Assign a, Assign b) { return DueCompare(a, b, asc); });
        }

        public static Item ForAssign(Assign a)
        {
            Item it = new Item();
            it.Tag = a;
            it.Dim = a.InDropped;
            it.Title = a.Name;
            string due = a.Due.HasValue ? "Due " + a.Due.Value.ToString("MMM d") : "No due date";
            it.Sub = (a.Course.Length > 0 ? a.Course + "  ·  " : "") + due + (a.Late ? "  ·  Late" : "") + (a.InDropped ? "  ·  Dropped class" : "");
            if (a.Status == "graded")
            {
                string score = a.Total.HasValue ? Theme.Fmt(a.Score) + "/" + Theme.Fmt(a.Total) : Theme.Fmt(a.Score);
                if (a.Percent.HasValue) score += "  ·  " + Math.Round(a.Percent.Value) + "%";
                it.Right = score;
                it.RightColor = Theme.GradeColor(a.Percent);
            }
            else if (a.Status == "missing")
            {
                it.Badge = "Missing"; it.BadgeBack = Theme.BadSoft; it.BadgeFore = Theme.Bad;
            }
            else if (a.Status == "upcoming") it.Badge = "Upcoming";
            else if (a.Status == "submitted") { it.Badge = "Turned in"; it.BadgeBack = Theme.AccSoft; it.BadgeFore = Theme.AccText; }
            else if (a.Status == "ungraded") it.Badge = "Not graded";
            else if (a.Status == "exempt") it.Badge = "Exempt";
            return it;
        }

        public static bool IsUpcoming(Assign a)
        {
            if (a.InDropped) return false;
            if (a.Status == "upcoming") return true;
            return a.Status == "submitted" && a.Due.HasValue && a.Due.Value >= DateTime.Today;
        }

        public static double? Points(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return null;
            string l = letter.Trim().ToUpperInvariant();
            switch (l)
            {
                case "A+": case "A": return 4.0;
                case "A-": return 3.7;
                case "B+": return 3.3;
                case "B": return 3.0;
                case "B-": return 2.7;
                case "C+": return 2.3;
                case "C": return 2.0;
                case "C-": return 1.7;
                case "D+": return 1.3;
                case "D": return 1.0;
                case "D-": return 0.7;
                case "F": return 0.0;
            }
            return null;
        }

        // honors / AP / dual-credit guess from the class name; the student can override it per class
        public static int AutoLevel(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\b(AP|IB|DE|dual|college|advanced placement)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 2;
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\b(honors?|hon|pre-?ap|accelerated|gifted)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 1;
            return 0;
        }

        public static int LevelOf(Settings cfg, Course c)
        {
            int v = cfg.LevelFor(c.Name);
            return v >= 0 ? v : AutoLevel(c.Name);
        }

        public static string LevelName(int level)
        {
            return level >= 2 ? "AP / IB / dual credit" : (level == 1 ? "Honors" : "Regular");
        }

        // 4.0 scale from letters; weighted adds 0.5 (honors) or 1.0 (AP / IB / dual) to any passing grade
        public static double? Gpa(List<Course> cs, bool weighted)
        {
            double sum = 0; int n = 0;
            foreach (Course c in cs)
            {
                if (c.Dropped) continue;
                string l = !string.IsNullOrEmpty(c.Letter) ? c.Letter : Norm.LetterFor(c.Percent);
                double? p = Points(l);
                if (!p.HasValue) continue;
                double v = p.Value;
                if (weighted && v > 0) v += c.Level >= 2 ? 1.0 : (c.Level == 1 ? 0.5 : 0.0);
                sum += v; n++;
            }
            return n == 0 ? (double?)null : Math.Round(sum / n, 2);
        }

        public static string G2(double? v)
        {
            return v.HasValue ? v.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "–";
        }

        public static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static string GradeText(TaskGrade t)
        {
            string s = t.Letter;
            if (t.Percent.HasValue) s += (s.Length > 0 ? "  ·  " : "") + Theme.Fmt(t.Percent) + "%";
            return s;
        }
    }


    // ---------------------------------------------------------------- the main window's pages
    partial class MainForm
    {
        int vy;
        string aFilter = "upcoming";
        string aSearch = "";
        ListCard aList;
        int aListY;
        string inboxTab = "message";
        NowHero liveHero;
        NowStrip liveStrip;
        NowTimeline liveTimeline;
        DayPlan plan;
        string schedTerm = "";

        int CW()
        {
            return Math.Max(Theme.S(560), host.ClientSize.Width - Theme.S(64) - SystemInformation.VerticalScrollBarWidth);
        }

        int X0() { return Theme.S(32); }

        void ShowView(bool keepScroll)
        {
            string navKey = (view == "bells" || view == "debug") ? "settings" : view;
            for (int i = 0; i < navs.Count; i++)
            {
                bool on = navKeys[i] == navKey;
                if (navs[i].On != on) { navs[i].On = on; navs[i].Invalidate(); }
            }
            if (shell == null || !shell.Visible || host.Width < 10) return;
            int sy = keepScroll ? -host.AutoScrollPosition.Y : 0;
            Native.SetRedraw(host, false);
            try
            {
                host.AutoScrollPosition = new Point(0, 0);
                for (int i = host.Controls.Count - 1; i >= 0; i--)
                {
                    Control c = host.Controls[i];
                    host.Controls.RemoveAt(i);
                    c.Dispose();
                }
                aList = null;
                liveHero = null; liveStrip = null; liveTimeline = null;
                lastKey = null;
                vy = Theme.S(28);
                switch (view)
                {
                    case "now": BuildNow(); break;
                    case "bells": BuildBells(); break;
                    case "grades": BuildGrades(); break;
                    case "assignments": BuildAssignments(); break;
                    case "schedule": BuildSchedule(); break;
                    case "inbox": BuildInbox(); break;
                    case "docs": BuildDocs(); break;
                    case "tools": BuildTools(); break;
                    case "settings": BuildSettings(); break;
                    case "debug": BuildDebug(); break;
                    default: BuildDashboard(); break;
                }
                host.AutoScrollMinSize = new Size(0, vy + Theme.S(36));
                if (sy > 0) host.AutoScrollPosition = new Point(0, sy);
            }
            finally
            {
                Native.SetRedraw(host, true);
            }
        }

        void GoTo(string key)
        {
            view = key;
            ShowView(false);
        }

        // ------------------------------------------------------------ builders
        Label AddLabel(string text, Font f, Color c, int x, int y, int w, int h)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.AutoEllipsis = true;
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = Theme.Bg0;
            l.SetBounds(x, y, w, h);
            host.Controls.Add(l);
            return l;
        }

        T AddCard<T>(T c, int x, int y, int w, int h) where T : Control
        {
            c.SetBounds(x, y, w, h);
            host.Controls.Add(c);
            return c;
        }

        void Header(string title, string sub)
        {
            AddLabel(title, Theme.Title, Theme.T1, X0(), vy, CW(), Theme.S(44));
            vy += Theme.S(46);
            if (!string.IsNullOrEmpty(sub))
            {
                AddLabel(sub, Theme.Body, Theme.T2, X0(), vy, CW(), Theme.S(24));
                vy += Theme.S(30);
            }
            vy += Theme.S(14);
        }

        void SectionTitle(string text)
        {
            AddLabel(text, Theme.Mid, Theme.T1, X0(), vy, CW(), Theme.S(30));
            vy += Theme.S(38);
        }

        void AddNotice(string text)
        {
            NoticeCard nc = new NoticeCard();
            nc.Text2 = text;
            int h = nc.PreferredHeight(CW());
            AddCard(nc, X0(), vy, CW(), h);
            vy += h + Theme.S(16);
        }

        PillButton AddButton(string text, string glyph, int kind, int x, int y, EventHandler click)
        {
            PillButton b = new PillButton();
            b.Text = text;
            b.Glyph = glyph;
            b.Kind = kind;
            b.Click += click;
            b.SetBounds(x, y, b.PreferredWidth(), Theme.S(40));
            host.Controls.Add(b);
            return b;
        }

        // two cards side by side on wide windows, stacked on narrow ones
        void AddPair(Control left, int lh, Control right, int rh)
        {
            int gap = Theme.S(16);
            if (CW() >= Theme.S(860))
            {
                int lw = (CW() - gap) / 2;
                int h = Math.Max(lh, rh);
                AddCard(left, X0(), vy, lw, h);
                AddCard(right, X0() + lw + gap, vy, lw, h);
                vy += h + gap;
            }
            else
            {
                AddCard(left, X0(), vy, CW(), lh);
                vy += lh + gap;
                AddCard(right, X0(), vy, CW(), rh);
                vy += rh + gap;
            }
        }

        void OpenAssign(Assign a)
        {
            Course c = FindCourse(a.Course);
            using (AssignForm f = new AssignForm(a, c, delegate { OpenCampusPage("assignment-list"); }))
            {
                f.ShowDialog(this);
            }
        }

        Course FindCourse(string name)
        {
            if (snap == null || string.IsNullOrEmpty(name)) return null;
            string key = Norm.Key(name);
            Course best = null;
            foreach (Course c in snap.Courses)
            {
                string ck = Norm.Key(c.Name);
                if (ck.Length == 0) continue;
                if (ck == key || ck.Contains(key) || key.Contains(ck))
                {
                    if (best == null || c.Term == term) best = c;
                }
            }
            return best;
        }

        void OpenMessage(Msg m)
        {
            using (MessageForm f = new MessageForm(m, delegate { OpenCampusPage("message-center"); }))
            {
                f.ShowDialog(this);
            }
            if (!m.Read) { m.Read = true; UpdateBadges(); if (view == "inbox") BeginInvoke(new MethodInvoker(delegate { ShowView(true); })); }
        }

        // ------------------------------------------------------------ home
        void BuildDashboard()
        {
            string first = current != null ? current.First : "";
            int hr = DateTime.Now.Hour;
            string greet = hr < 12 ? "Good morning" : (hr < 18 ? "Good afternoon" : "Good evening");
            Header(greet + (first.Length > 0 ? ", " + first : ""), DateTime.Now.ToString("dddd, MMMM d"));
            if (snap == null)
            {
                AddLabel("Loading your information…", Theme.Body, Theme.T2, X0(), vy, CW(), Theme.S(28));
                vy += Theme.S(40);
                return;
            }
            if (!snap.HasAny) AddNotice(Rows.NoData);

            if (current != null)
            {
                ProfileCard pc = new ProfileCard();
                pc.S = current;
                pc.Photo = photo;
                pc.Logo = logo;
                AddCard(pc, X0(), vy, CW(), ProfileCard.HeightFor());
                vy += ProfileCard.HeightFor() + Theme.S(16);
            }

            plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            NowStrip ns = new NowStrip();
            ns.Plan = plan;
            ns.Click += delegate { GoTo(plan.Sched == null ? "bells" : "now"); };
            liveStrip = ns;
            AddCard(ns, X0(), vy, CW(), NowStrip.HeightFor());
            vy += NowStrip.HeightFor() + Theme.S(16);

            List<Course> cs = snap.Active(term);
            double? avg = Snapshot.Average(cs);
            double? gpaU = Rows.Gpa(cs, false), gpaW = Rows.Gpa(cs, true);
            DateTime today = DateTime.Today;
            DateTime weekEnd = today.AddDays(7);
            List<Assign> upcoming = snap.Assigns.FindAll(delegate(Assign a)
            {
                return Rows.IsUpcoming(a) && (!a.Due.HasValue || a.Due.Value >= today);
            });
            Rows.SortAssigns(upcoming, true);
            List<Assign> missing = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "missing" && !a.InDropped; });
            Rows.SortAssigns(missing, true);
            int dueWeek = 0;
            foreach (Assign a in upcoming) if (a.Due.HasValue && a.Due.Value <= weekEnd) dueWeek++;

            int gap = Theme.S(16);
            int cols = CW() >= Theme.S(900) ? 4 : 2;
            int tw = (CW() - gap * (cols - 1)) / cols;
            int th = Theme.S(100);
            TileCard[] tiles = new TileCard[4];
            for (int i = 0; i < 4; i++) tiles[i] = new TileCard();

            if (avg.HasValue)
            {
                tiles[0].Ring = avg.Value;
                tiles[0].RingText = Math.Round(avg.Value) + "%";
                tiles[0].Big = Norm.LetterFor(avg);
                tiles[0].BigColor = Theme.GradeColor(avg);
            }
            else tiles[0].Big = "–";
            tiles[0].Label = "Average" + (term.Length > 0 ? "  ·  " + term : "");
            tiles[1].Big = Rows.G2(gpaU);
            tiles[1].Label = "GPA  ·  " + Rows.G2(gpaW) + " weighted";
            tiles[2].Big = missing.Count.ToString();
            tiles[2].BigColor = missing.Count > 0 ? Theme.Bad : Theme.T1;
            tiles[2].Label = "Missing assignments";
            tiles[3].Big = dueWeek.ToString();
            tiles[3].BigColor = dueWeek > 0 ? Theme.Warn : Theme.T1;
            tiles[3].Label = "Due in the next 7 days";
            for (int i = 0; i < 4; i++)
                AddCard(tiles[i], X0() + (i % cols) * (tw + gap), vy + (i / cols) * (th + gap), tw, th);
            vy += ((4 + cols - 1) / cols) * (th + gap);

            ListCard soon = new ListCard();
            soon.Heading = "Coming up";
            soon.EmptyText = "Nothing due. Enjoy it.";
            soon.Clickable = true;
            for (int i = 0; i < upcoming.Count && i < 8; i++) soon.Items.Add(Rows.ForAssign(upcoming[i]));

            ListCard attn = new ListCard();
            attn.Heading = "Needs attention";
            attn.EmptyText = "All clear: no missing work and no grade below 70%.";
            attn.Clickable = true;
            for (int i = 0; i < missing.Count && i < 6; i++) attn.Items.Add(Rows.ForAssign(missing[i]));
            foreach (Course c in cs)
            {
                if (c.Percent.HasValue && c.Percent.Value < 70)
                {
                    Item it = new Item();
                    it.Tag = c;
                    it.Title = c.Name;
                    it.Sub = "Grade is below 70%";
                    it.Right = Math.Round(c.Percent.Value) + "%";
                    it.RightColor = Theme.Bad;
                    attn.Items.Add(it);
                }
            }
            soon.ItemClick += HomeClick;
            attn.ItemClick += HomeClick;
            AddPair(soon, soon.PreferredHeight(), attn, attn.PreferredHeight());

            // newest inbox items
            List<Msg> recent = new List<Msg>();
            recent.AddRange(snap.Messages);
            recent.AddRange(snap.Notices);
            recent.Sort(delegate(Msg a, Msg b)
            {
                DateTime da = a.Date.HasValue ? a.Date.Value : DateTime.MinValue;
                DateTime db = b.Date.HasValue ? b.Date.Value : DateTime.MinValue;
                return db.CompareTo(da);
            });
            if (recent.Count > 0)
            {
                ListCard inb = new ListCard();
                inb.Heading = "Latest from your inbox";
                inb.Clickable = true;
                for (int i = 0; i < recent.Count && i < 4; i++) inb.Items.Add(MsgRow(recent[i]));
                inb.ItemClick += delegate(Item it)
                {
                    Msg m = it.Tag as Msg;
                    if (m != null) BeginInvoke(new MethodInvoker(delegate { OpenMessage(m); }));
                };
                AddCard(inb, X0(), vy, CW(), inb.PreferredHeight());
                vy += inb.PreferredHeight() + gap;
            }
        }

        void HomeClick(Item it)
        {
            Assign a = it.Tag as Assign;
            Course c = it.Tag as Course;
            if (a != null) BeginInvoke(new MethodInvoker(delegate { OpenAssign(a); }));
            else if (c != null) BeginInvoke(new MethodInvoker(delegate { OpenCourse(c); }));
        }

        static Item RosterRow(RosterItem r, bool showTerm)
        {
            Item it = new Item();
            it.Lead = r.Period.Length > 0 ? r.Period : "–";
            it.Title = r.Course;
            string sub = r.Teacher;
            if (r.Room.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + r.Room;
            it.Sub = sub;
            if (showTerm) { it.Right = r.Term; it.RightColor = Theme.T3; }
            return it;
        }

        static Item MsgRow(Msg m)
        {
            Item it = new Item();
            it.Tag = m;
            it.Title = m.Subject.Length > 0 ? m.Subject : "(no subject)";
            string prev = m.Body.Replace("\n", " ");
            if (prev.Length > 110) prev = prev.Substring(0, 110) + "…";
            string sub = m.From;
            if (prev.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + prev;
            it.Sub = sub;
            it.Dot = !m.Read;
            if (m.Date.HasValue) { it.Right = Rows.DayLabel(m.Date.Value); it.RightColor = Theme.T3; }
            if (m.Kind == "notice") { it.Badge = "Announcement"; it.BadgeBack = Theme.AccSoft; it.BadgeFore = Theme.AccText; }
            return it;
        }

        // ------------------------------------------------------------ grades
        void BuildGrades()
        {
            Header("Grades", "Select a class to see its trend, level and every assignment.");
            if (snap == null) return;
            if (snap.Courses.Count == 0) { AddNotice(Rows.NoData); return; }

            if (snap.Terms.Count > 1)
            {
                List<string> terms = new List<string>();
                for (int i = Math.Max(0, snap.Terms.Count - 6); i < snap.Terms.Count; i++) terms.Add(snap.Terms[i]);
                Segmented sg = new Segmented();
                sg.SetOptions(terms.ToArray(), terms.IndexOf(term));
                sg.Changed += delegate
                {
                    term = terms[sg.Selected];
                    // rebuild after this click has finished (the rebuild disposes this very control)
                    BeginInvoke(new MethodInvoker(delegate { ShowView(false); }));
                };
                sg.Location = new Point(X0(), vy);
                host.Controls.Add(sg);
                vy += sg.Height + Theme.S(18);
            }

            List<Course> all = snap.CoursesFor(term);
            List<Course> cs = all.FindAll(delegate(Course c) { return !c.Dropped; });
            List<Course> dropped = all.FindAll(delegate(Course c) { return c.Dropped; });
            int gap = Theme.S(16);
            double? avg = Snapshot.Average(cs);
            double? gu = Rows.Gpa(cs, false), gw = Rows.Gpa(cs, true);
            int cols0 = CW() >= Theme.S(900) ? 4 : 2;
            int tw0 = (CW() - gap * (cols0 - 1)) / cols0;
            TileCard t1 = new TileCard();
            t1.Big = avg.HasValue ? Theme.Fmt(avg) + "%" : "–";
            t1.BigColor = Theme.GradeColor(avg);
            t1.Label = "Average, " + cs.Count + (cs.Count == 1 ? " class" : " classes");
            TileCard t2 = new TileCard();
            t2.Big = Rows.G2(gu);
            t2.Label = "Unweighted GPA (4.0 scale)";
            TileCard t3 = new TileCard();
            t3.Big = Rows.G2(gw);
            t3.BigColor = Theme.AccText;
            t3.Label = "Weighted GPA (honors +0.5, AP +1.0)";
            Course best = null;
            foreach (Course c in cs) if (c.Percent.HasValue && (best == null || c.Percent.Value > best.Percent.Value)) best = c;
            TileCard t4 = new TileCard();
            t4.Big = best != null ? Math.Round(best.Percent.Value) + "%" : "–";
            t4.Label = best != null ? "Highest: " + best.Name : "Highest class";
            TileCard[] tl = new TileCard[] { t1, t2, t3, t4 };
            for (int i = 0; i < tl.Length; i++)
                AddCard(tl[i], X0() + (i % cols0) * (tw0 + gap), vy + (i / cols0) * (Theme.S(100) + gap), tw0, Theme.S(100));
            vy += ((tl.Length + cols0 - 1) / cols0) * (Theme.S(100) + gap);
            AddLabel("GPA is an estimate from each class's current letter. A class counts as honors or AP from its name; open a class to change its level.",
                Theme.Small, Theme.T3, X0(), vy - Theme.S(4), CW(), Theme.S(22));
            vy += Theme.S(30);

            int cols = Math.Max(1, (CW() + gap) / (Theme.S(310) + gap));
            int cw = (CW() - gap * (cols - 1)) / cols;
            int ch = Theme.S(140);
            for (int i = 0; i < cs.Count; i++)
            {
                Course course = cs[i];
                CourseCard cc = new CourseCard();
                cc.Data = course;
                cc.Click += delegate { OpenCourse(course); };
                AddCard(cc, X0() + (i % cols) * (cw + gap), vy + (i / cols) * (ch + gap), cw, ch);
            }
            vy += ((cs.Count + cols - 1) / cols) * (ch + gap);

            if (dropped.Count > 0)
            {
                vy += Theme.S(8);
                SectionTitle("Dropped classes");
                for (int i = 0; i < dropped.Count; i++)
                {
                    Course course = dropped[i];
                    CourseCard cc = new CourseCard();
                    cc.Data = course;
                    cc.Click += delegate { OpenCourse(course); };
                    AddCard(cc, X0() + (i % cols) * (cw + gap), vy + (i / cols) * (ch + gap), cw, ch);
                }
                vy += ((dropped.Count + cols - 1) / cols) * (ch + gap);
            }
        }

        void ApplyLevels()
        {
            if (snap == null) return;
            foreach (Course c in snap.Courses) c.Level = Rows.LevelOf(cfg, c);
        }

        void OpenCourse(Course c)
        {
            List<Assign> list = new List<Assign>();
            string key = Norm.Key(c.Name);
            if (snap != null)
            {
                foreach (Assign a in snap.Assigns)
                {
                    if (c.Section.Length > 0 && a.SectionId.Length > 0)
                    {
                        if (a.SectionId == c.Section) list.Add(a);
                        continue;
                    }
                    string ak = Norm.Key(a.Course);
                    if (ak.Length > 0 && (ak == key || ak.Contains(key) || key.Contains(ak))) list.Add(a);
                }
            }
            Course course = c;
            using (CourseForm f = new CourseForm(c, list, delegate { OpenCampusPage("grades"); }, delegate(int level)
            {
                cfg.Levels[Settings.Slug(course.Name)] = level;
                cfg.Save();
                ApplyLevels();
                BeginInvoke(new MethodInvoker(delegate { ShowView(true); }));
            }))
            {
                f.ShowDialog(this);
            }
        }

        // ------------------------------------------------------------ assignments
        void BuildAssignments()
        {
            Header("Assignments", snap != null ? snap.Assigns.Count + " total  ·  click any one for details and teacher comments" : "");
            if (snap == null) return;
            if (snap.Assigns.Count == 0) { AddNotice(Rows.NoData); return; }

            string[] keys = new string[] { "upcoming", "missing", "graded", "all" };
            string[] names = new string[] { "Upcoming", "Missing", "Graded", "All" };
            Segmented sg = new Segmented();
            sg.SetOptions(names, Math.Max(0, Array.IndexOf(keys, aFilter)));
            sg.Changed += delegate
            {
                aFilter = keys[sg.Selected];
                RebuildAssignList();
            };
            sg.Location = new Point(X0(), vy);
            host.Controls.Add(sg);

            DarkInput box = new DarkInput(false, false);
            box.Placeholder = "Search assignments or classes";
            box.Text = aSearch;
            int bx = X0() + sg.Width + Theme.S(16);
            int bw = Math.Min(Theme.S(340), Math.Max(Theme.S(180), X0() + CW() - bx));
            box.SetBounds(bx, vy, bw, sg.Height);
            box.Box.TextChanged += delegate
            {
                aSearch = box.Text;
                RebuildAssignList();
            };
            host.Controls.Add(box);

            vy += sg.Height + Theme.S(18);
            aListY = vy;
            RebuildAssignList();
        }

        void RebuildAssignList()
        {
            if (snap == null) return;
            if (aList != null)
            {
                host.Controls.Remove(aList);
                aList.Dispose();
                aList = null;
            }
            string filter = aFilter;
            List<Assign> list = snap.Assigns.FindAll(delegate(Assign a)
            {
                if (filter == "upcoming") return Rows.IsUpcoming(a);
                if (filter == "missing") return a.Status == "missing";
                if (filter == "graded") return a.Status == "graded";
                return true;
            });
            string q = aSearch.Trim().ToLowerInvariant();
            if (q.Length > 0)
            {
                list = list.FindAll(delegate(Assign a) { return (a.Name + " " + a.Course).ToLowerInvariant().Contains(q); });
            }
            Rows.SortAssigns(list, filter == "upcoming");

            ListCard lc = new ListCard();
            lc.EmptyText = "Nothing matches.";
            lc.Clickable = true;
            int total = list.Count;
            int shown = 0;
            string lastKey = null;
            foreach (Assign a in list)
            {
                if (shown >= 250) break;
                string k = a.Due.HasValue ? a.Due.Value.ToString("yyyyMMdd") : "none";
                if (k != lastKey)
                {
                    Item h = new Item();
                    h.Header = true;
                    h.Title = a.Due.HasValue ? Rows.DayLabel(a.Due.Value) : "No due date";
                    lc.Items.Add(h);
                    lastKey = k;
                }
                Item row = Rows.ForAssign(a);
                row.Strip = Theme.Tag(a.Course);
                lc.Items.Add(row);
                shown++;
            }
            lc.ItemClick += HomeClick;
            lc.Heading = total > shown ? "Showing the first " + shown + " of " + total : total + (total == 1 ? " assignment" : " assignments");
            aList = lc;
            lc.SetBounds(X0(), aListY, CW(), lc.PreferredHeight());
            host.Controls.Add(lc);
            vy = aListY + lc.Height;
            host.AutoScrollMinSize = new Size(0, vy + Theme.S(36));
        }

        // ------------------------------------------------------------ schedule
        string schedDay = "";

        void BuildSchedule()
        {
            Header("Schedule", "Your classes by period. Click a class for its grade and work.");
            if (snap == null) return;
            if (snap.Roster.Count == 0) { AddNotice(Rows.NoData); return; }

            if (string.IsNullOrEmpty(schedTerm) || !snap.Terms.Contains(schedTerm)) schedTerm = term;
            List<RosterItem> items = snap.Roster.FindAll(delegate(RosterItem r) { return Sched.TermMatches(r, schedTerm); });
            if (items.Count == 0) items = snap.Roster;

            if (snap.Terms.Count > 1)
            {
                List<string> terms = new List<string>();
                for (int i = Math.Max(0, snap.Terms.Count - 6); i < snap.Terms.Count; i++) terms.Add(snap.Terms[i]);
                Segmented sg = new Segmented();
                sg.SetOptions(terms.ToArray(), Math.Max(0, terms.IndexOf(schedTerm)));
                sg.Changed += delegate
                {
                    schedTerm = terms[sg.Selected];
                    BeginInvoke(new MethodInvoker(delegate { ShowView(false); }));
                };
                sg.Location = new Point(X0(), vy);
                host.Controls.Add(sg);
                vy += sg.Height + Theme.S(14);
            }

            // rotating days: filter the list to one day
            string[] labels = cfg.RotOn ? Sched.Labels(cfg) : new string[0];
            if (labels.Length > 0)
            {
                string[] opts = new string[labels.Length + 1];
                opts[0] = "All";
                for (int i = 0; i < labels.Length; i++) opts[i + 1] = labels[i];
                int sel = Math.Max(0, Array.IndexOf(opts, schedDay));
                Segmented dg = new Segmented();
                dg.SetOptions(opts, sel);
                dg.Changed += delegate
                {
                    schedDay = dg.Selected == 0 ? "" : opts[dg.Selected];
                    BeginInvoke(new MethodInvoker(delegate { ShowView(false); }));
                };
                dg.Location = new Point(X0(), vy);
                host.Controls.Add(dg);
                vy += dg.Height + Theme.S(18);
            }
            else schedDay = "";

            BellSchedule bs = cfg.ForDate(DateTime.Today);
            if (bs == null)
            {
                NoticeCard nc = new NoticeCard();
                nc.Text2 = "Campus does not share your bell times. Add them once and this page shows start and end times and marks what is happening now and next.";
                int nh = nc.PreferredHeight(CW());
                AddCard(nc, X0(), vy, CW(), nh);
                vy += nh + Theme.S(10);
                AddButton("Set up bell schedule", "\uE713", 0, X0(), vy, delegate { GoTo("bells"); });
                vy += Theme.S(54);
            }

            TimeSpan now = DateTime.Now.TimeOfDay;
            bool school = Sched.IsSchoolDay(cfg, snap, DateTime.Today);
            DayPlan todayPlan = Sched.Plan(cfg, snap, term, DateTime.Today);
            int pcur, pnext;
            Sched.Phase(todayPlan, now, out pcur, out pnext);

            // active classes first, dropped ones last
            List<RosterItem> shown = new List<RosterItem>();
            foreach (RosterItem ri in items)
                if (!ri.Dropped && (schedDay.Length == 0 || Sched.CourseApplies(cfg, ri.Course, schedDay))) shown.Add(ri);
            foreach (RosterItem ri in items) if (ri.Dropped) shown.Add(ri);

            int gap = Theme.S(16);
            int cols = CW() >= Theme.S(1000) ? 3 : (CW() >= Theme.S(660) ? 2 : 1);
            int cw = (CW() - gap * (cols - 1)) / cols;
            int ch = Theme.S(128);
            for (int i = 0; i < shown.Count; i++)
            {
                RosterItem ri = shown[i];
                PeriodCard pc = new PeriodCard();
                pc.R = ri;
                pc.C = Sched.CourseOf(snap, ri, schedTerm);
                pc.Dropped = ri.Dropped;
                pc.EndText = ri.EndDate.HasValue ? "Dropped " + ri.EndDate.Value.ToString("MMM d") : "";
                if (bs != null && !ri.Dropped)
                {
                    string key = Bell.KeyOf(ri.Period);
                    foreach (Bell b in bs.Entries)
                    {
                        if (key.Length == 0 || b.Key != key) continue;
                        pc.TimeText = b.Text();
                        if (school && todayPlan.Slots.Count > 0)
                        {
                            if (pcur >= 0 && todayPlan.Slots[pcur].B == b) pc.Badge = "Now";
                            else if (pnext >= 0 && todayPlan.Slots[pnext].B == b) pc.Badge = "Next";
                        }
                        break;
                    }
                }
                Course cc = pc.C;
                pc.Click += delegate { if (cc != null) OpenCourse(cc); };
                int row = i / cols;
                AddCard(pc, X0() + (i % cols) * (cw + gap), vy + row * (ch + gap), cw, ch);
            }
            vy += ((shown.Count + cols - 1) / cols) * (ch + gap);
        }

        Course FindCourseInTerm(string name, string t)
        {
            if (snap == null) return null;
            string key = Norm.Key(name);
            Course any = null;
            foreach (Course c in snap.Courses)
            {
                string ck = Norm.Key(c.Name);
                if (ck.Length == 0 || !(ck == key || ck.Contains(key) || key.Contains(ck))) continue;
                if (c.Term == t) return c;
                if (any == null) any = c;
            }
            return any;
        }

        // ------------------------------------------------------------ inbox
        void BuildInbox()
        {
            Header("Inbox", "Messages and announcements from your school and teachers.");
            if (snap == null) return;

            string[] keys = new string[] { "message", "notice" };
            string[] names = new string[] { "Messages (" + snap.Messages.Count + ")", "Announcements (" + snap.Notices.Count + ")" };
            Segmented sg = new Segmented();
            sg.SetOptions(names, inboxTab == "notice" ? 1 : 0);
            sg.Changed += delegate
            {
                inboxTab = keys[sg.Selected];
                BeginInvoke(new MethodInvoker(delegate { ShowView(false); }));
            };
            sg.Location = new Point(X0(), vy);
            host.Controls.Add(sg);
            vy += sg.Height + Theme.S(18);

            List<Msg> list = inboxTab == "notice" ? snap.Notices : snap.Messages;
            if (list.Count == 0)
            {
                AddNotice(inboxTab == "notice"
                    ? "No announcements were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Original site shows them in the meantime."
                    : "No messages were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Original site shows them in the meantime.");
                AddButton("Open in Campus", "\uE8A7", 0, X0(), vy, delegate { OpenCampusPage("message-center"); });
                vy += Theme.S(54);
                return;
            }
            ListCard lc = new ListCard();
            lc.Clickable = true;
            lc.RowHeight = Theme.S(64);
            int unread = 0;
            foreach (Msg m in list) if (!m.Read) unread++;
            lc.Heading = list.Count + (list.Count == 1 ? " item" : " items") + (unread > 0 ? "  ·  " + unread + " unread" : "");
            for (int i = 0; i < list.Count && i < 200; i++) lc.Items.Add(MsgRow(list[i]));
            lc.ItemClick += delegate(Item it)
            {
                Msg m = it.Tag as Msg;
                if (m != null) BeginInvoke(new MethodInvoker(delegate { OpenMessage(m); }));
            };
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
        }

        // ------------------------------------------------------------ documents
        void BuildDocs()
        {
            Header("Documents", "Files your school has put in your Campus account.");
            if (snap == null) return;
            if (snap.Docs.Count == 0)
            {
                AddNotice("No documents were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Open in Campus shows them in the meantime.");
                AddButton("Open in Campus", "\uE8A7", 0, X0(), vy, delegate { OpenCampusPage("documents"); });
                vy += Theme.S(54);
                return;
            }
            ListCard lc = new ListCard();
            lc.Clickable = true;
            lc.Heading = snap.Docs.Count + (snap.Docs.Count == 1 ? " document" : " documents") + "  ·  click to open";
            foreach (Doc d in snap.Docs)
            {
                Item it = new Item();
                it.Tag = d;
                it.Title = d.Name;
                string sub = d.Type;
                if (d.Date.HasValue) sub += (sub.Length > 0 ? "  ·  " : "") + d.Date.Value.ToString("MMM d, yyyy");
                it.Sub = sub;
                lc.Items.Add(it);
            }
            lc.ItemClick += delegate(Item it)
            {
                Doc d = it.Tag as Doc;
                if (d != null) BeginInvoke(new MethodInvoker(delegate { OpenDoc(d); }));
            };
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
        }

        void OpenDoc(Doc d)
        {
            if (api == null) return;
            if (d.Url.Length == 0)
            {
                OpenCampusPage("documents");
                return;
            }
            Api a = api;
            stampText = "Opening " + d.Name + "…";
            stampBad = false;
            UpdateTop();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                string file = a.Download(d.Url, d.Name);
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (file != null)
                        {
                            try
                            {
                                ProcessStartInfo psi = new ProcessStartInfo(file);
                                psi.UseShellExecute = true;
                                Process.Start(psi);
                                stampText = snap != null ? "Updated " + snap.At.ToString("h:mm tt") : "";
                            }
                            catch (Exception) { stampText = "Could not open that file."; stampBad = true; }
                        }
                        else { stampText = "Could not download that document."; stampBad = true; }
                        UpdateTop();
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        // ------------------------------------------------------------ tools
        void BuildTools()
        {
            Header("Tools", "Extras that the Campus website does not have.");
            if (snap == null) return;
            int gap = Theme.S(16);

            // what-if
            SectionTitle("What-if calculator");
            WhatIfCard wi = new WhatIfCard(cfg.Goal);
            wi.Courses = snap.CoursesFor(term);
            wi.Assigns = snap.Assigns;
            AddCard(wi, X0(), vy, CW(), WhatIfCard.HeightFor());
            wi.Ready();
            wi.Disposed += delegate
            {
                double g = wi.GoalValue;
                if (g > 0 && g <= 100 && Math.Abs(g - cfg.Goal) > 0.001) { cfg.Goal = g; cfg.Save(); }
            };
            vy += WhatIfCard.HeightFor() + Theme.S(26);

            // insights
            SectionTitle("Insights");
            BarChart load = new BarChart();
            load.Heading = "Work due, next 14 days";
            load.EmptyText = "Nothing due in the next two weeks.";
            DateTime today = DateTime.Today;
            for (int i = 0; i < 14; i++)
            {
                DateTime d = today.AddDays(i);
                int n = 0;
                foreach (Assign a in snap.Assigns)
                    if (a.Due.HasValue && a.Due.Value == d && (a.Status == "upcoming" || a.Status == "submitted" || a.Status == "ungraded")) n++;
                Bar b = new Bar();
                b.Label = d.ToString("ddd") + "|" + d.Day;
                b.Value = n;
                b.Fill = i == 0 ? Theme.Warn : (n >= 4 ? Theme.Bad : Theme.Acc);
                load.Bars.Add(b);
            }

            BarChart dist = new BarChart();
            dist.Heading = "Your graded work by score";
            dist.EmptyText = "No graded assignments yet.";
            string[] names = new string[] { "A", "B", "C", "D", "F" };
            double[] cuts = new double[] { 90, 80, 70, 60, -1 };
            int[] counts = new int[5];
            foreach (Assign a in snap.Assigns)
            {
                if (a.Status != "graded" || !a.Percent.HasValue) continue;
                for (int k = 0; k < 5; k++) if (a.Percent.Value >= cuts[k]) { counts[k]++; break; }
            }
            Color[] cols = new Color[] { Theme.Good, Theme.Ok, Theme.Warn, Theme.Bad, Theme.Bad };
            for (int k = 0; k < 5; k++)
            {
                Bar b = new Bar();
                b.Label = names[k];
                b.Value = counts[k];
                b.Fill = cols[k];
                dist.Bars.Add(b);
            }
            AddPair(load, Theme.S(250), dist, Theme.S(250));

            List<Assign> graded = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "graded" && a.Percent.HasValue && a.Due.HasValue; });
            graded.Sort(delegate(Assign a, Assign b) { return a.Due.Value.CompareTo(b.Due.Value); });
            if (graded.Count > 40) graded = graded.GetRange(graded.Count - 40, 40);
            LineChart trend = new LineChart();
            trend.Heading = "Score trend, last " + graded.Count + " graded assignments";
            foreach (Assign a in graded) { trend.Xs.Add(a.Due.Value); trend.Ys.Add(a.Percent.Value); }
            AddCard(trend, X0(), vy, CW(), Theme.S(260));
            vy += Theme.S(260) + Theme.S(26);

            // export
            SectionTitle("Take it with you");
            PlainCard ex = new PlainCard();
            AddCard(ex, X0(), vy, CW(), Theme.S(92));
            int bx = Theme.S(20);
            PillButton b1 = new PillButton(); b1.Text = "Grades (.csv)"; b1.Glyph = "\uE74E";
            b1.Click += delegate { ExportGrades(); };
            PillButton b2 = new PillButton(); b2.Text = "Assignments (.csv)"; b2.Glyph = "\uE74E";
            b2.Click += delegate { ExportAssignments(); };
            PillButton b3 = new PillButton(); b3.Text = "Due dates (.ics calendar)"; b3.Glyph = "\uE787";
            b3.Click += delegate { ExportCalendar(); };
            foreach (PillButton pb in new PillButton[] { b1, b2, b3 })
            {
                pb.SetBounds(bx, Theme.S(26), pb.PreferredWidth(), Theme.S(40));
                ex.Controls.Add(pb);
                bx += pb.Width + Theme.S(10);
            }
            vy += Theme.S(92) + gap;
        }

        string AskSave(string name, string filter)
        {
            using (SaveFileDialog sf = new SaveFileDialog())
            {
                sf.FileName = name;
                sf.Filter = filter;
                sf.OverwritePrompt = true;
                if (sf.ShowDialog(this) == DialogResult.OK) return sf.FileName;
            }
            return null;
        }

        void ExportGrades()
        {
            if (snap == null) return;
            string f = AskSave("grades.csv", "CSV file (*.csv)|*.csv");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Class,Term,Teacher,Room,Grading period,Letter,Percent");
            foreach (Course c in snap.Courses)
                sb.AppendLine(Rows.Csv(c.Name) + "," + Rows.Csv(c.Term) + "," + Rows.Csv(c.Teacher) + "," + Rows.Csv(c.Room) + ","
                    + Rows.Csv(c.TaskName) + "," + Rows.Csv(c.Letter) + "," + (c.Percent.HasValue ? Theme.Fmt(c.Percent) : ""));
            SaveText(f, sb.ToString());
        }

        void ExportAssignments()
        {
            if (snap == null) return;
            string f = AskSave("assignments.csv", "CSV file (*.csv)|*.csv");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Class,Assignment,Due,Status,Score,Out of,Percent,Comments");
            foreach (Assign a in snap.Assigns)
                sb.AppendLine(Rows.Csv(a.Course) + "," + Rows.Csv(a.Name) + "," + (a.Due.HasValue ? a.Due.Value.ToString("yyyy-MM-dd") : "") + ","
                    + a.Status + "," + (a.Score.HasValue ? Theme.Fmt(a.Score) : Rows.Csv(a.ScoreText)) + "," + Theme.Fmt(a.Total) + ","
                    + Theme.Fmt(a.Percent) + "," + Rows.Csv(a.Comments));
            SaveText(f, sb.ToString());
        }

        static string IcsEsc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
        }

        void ExportCalendar()
        {
            if (snap == null) return;
            string f = AskSave("due-dates.ics", "Calendar file (*.ics)|*.ics");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Campus Midnight//EN\r\n");
            int n = 0;
            foreach (Assign a in snap.Assigns)
            {
                if (!a.Due.HasValue || !(Rows.IsUpcoming(a) || a.Status == "ungraded" || a.Status == "missing")) continue;
                n++;
                sb.Append("BEGIN:VEVENT\r\n");
                sb.Append("UID:cm-" + (a.Id.Length > 0 ? a.Id : n.ToString()) + "-" + a.Due.Value.ToString("yyyyMMdd") + "@campusmidnight\r\n");
                sb.Append("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'") + "\r\n");
                sb.Append("DTSTART;VALUE=DATE:" + a.Due.Value.ToString("yyyyMMdd") + "\r\n");
                sb.Append("DTEND;VALUE=DATE:" + a.Due.Value.AddDays(1).ToString("yyyyMMdd") + "\r\n");
                sb.Append("SUMMARY:" + IcsEsc(a.Name + " (" + a.Course + ")") + "\r\n");
                if (a.Total.HasValue) sb.Append("DESCRIPTION:" + IcsEsc(Theme.Fmt(a.Total) + " points") + "\r\n");
                sb.Append("END:VEVENT\r\n");
            }
            sb.Append("END:VCALENDAR\r\n");
            SaveText(f, sb.ToString());
        }

        void SaveText(string file, string text)
        {
            try
            {
                File.WriteAllText(file, text, new UTF8Encoding(true));
                stampText = "Saved " + Path.GetFileName(file);
                stampBad = false;
            }
            catch (Exception ex)
            {
                stampText = "Could not save: " + ex.Message;
                stampBad = true;
            }
            UpdateTop();
        }

        // ------------------------------------------------------------ settings
        void BuildSettings()
        {
            Header("Settings", "Make it yours.");
            int gap = Theme.S(16);

            SectionTitle("Appearance");
            string[] modeNames = new string[] { "System", "Light", "Dark" };
            string[] modeKeys = new string[] { "system", "light", "dark" };
            Segmented ms = new Segmented();
            ms.SetOptions(modeNames, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)));
            ms.Changed += delegate
            {
                int k = ms.Selected;
                BeginInvoke(new MethodInvoker(delegate { ChangeMode(modeKeys[k]); }));
            };
            ms.Location = new Point(X0(), vy);
            host.Controls.Add(ms);
            vy += ms.Height + Theme.S(24);

            SectionTitle("Accent theme");
            int sw = Theme.S(168), sh = Theme.S(124);
            int perRow = Math.Max(1, (CW() + gap) / (sw + gap));
            for (int i = 0; i < Theme.All.Count; i++)
            {
                SwatchCard sc = new SwatchCard();
                sc.Def = Theme.All[i];
                sc.Selected = string.Equals(Theme.All[i].Name, Theme.Current, StringComparison.OrdinalIgnoreCase);
                string name = Theme.All[i].Name;
                sc.Click += delegate { BeginInvoke(new MethodInvoker(delegate { ChangeTheme(name); })); };
                AddCard(sc, X0() + (i % perRow) * (sw + gap), vy + (i / perRow) * (sh + gap), sw, sh);
            }
            vy += ((Theme.All.Count + perRow - 1) / perRow) * (sh + gap) + Theme.S(10);

            SectionTitle("Pictures");
            PlainCard pcard = new PlainCard();
            AddCard(pcard, X0(), vy, CW(), Theme.S(150));
            Label pl = new Label();
            pl.AutoSize = false; pl.UseMnemonic = false;
            pl.BackColor = Theme.Bg2; pl.ForeColor = Theme.T2; pl.Font = Theme.Body;
            pl.Text = "Your picture and the school logo come from Campus when your school makes them available. You can also choose your own."
                + (photoNote.Length > 0 ? "\n" + photoNote : "") + (logoNote.Length > 0 ? "\n" + logoNote : "");
            pl.SetBounds(Theme.S(20), Theme.S(16), CW() - Theme.S(40), Theme.S(66));
            pcard.Controls.Add(pl);
            int px = Theme.S(20);
            PillButton ph = new PillButton(); ph.Text = "Choose my photo"; ph.Glyph = "\uE91B";
            ph.Click += delegate { ChooseImage("photo.png", true); };
            PillButton ph2 = new PillButton(); ph2.Text = "Use Campus photo"; ph2.Glyph = "\uE72C";
            ph2.Click += delegate { RemoveCustom("photo.png", true); };
            PillButton lg = new PillButton(); lg.Text = "Choose logo"; lg.Glyph = "\uE91B";
            lg.Click += delegate { ChooseImage("logo.png", false); };
            PillButton lg2 = new PillButton(); lg2.Text = "Use Campus logo"; lg2.Glyph = "\uE72C";
            lg2.Click += delegate { RemoveCustom("logo.png", false); };
            int by = Theme.S(92);
            foreach (PillButton b in new PillButton[] { ph, ph2, lg, lg2 })
            {
                if (px + b.PreferredWidth() > CW() - Theme.S(20)) { px = Theme.S(20); by += Theme.S(48); pcard.Height += Theme.S(48); }
                b.SetBounds(px, by, b.PreferredWidth(), Theme.S(38));
                pcard.Controls.Add(b);
                px += b.Width + Theme.S(10);
            }
            vy += pcard.Height + gap + Theme.S(10);

            SectionTitle("Bell schedules and rotating days");
            PlainCard bcard = new PlainCard();
            AddCard(bcard, X0(), vy, CW(), Theme.S(150));
            Label bl = new Label();
            bl.AutoSize = false; bl.UseMnemonic = false;
            bl.BackColor = Theme.Bg2; bl.ForeColor = Theme.T2; bl.Font = Theme.Body;
            string rot = cfg.RotOn ? "Rotating days: " + cfg.RotLabels + "." : "Rotating days are off.";
            bl.Text = cfg.Schedules.Count == 0
                ? "No bell schedule yet. Add your period times once to see what is happening now, what is next and how long is left."
                : cfg.Schedules.Count + (cfg.Schedules.Count == 1 ? " schedule: " : " schedules: ") + string.Join(", ", cfg.Schedules.ConvertAll<string>(delegate(BellSchedule x) { return x.Name; }).ToArray()) + ".  " + rot;
            bl.SetBounds(Theme.S(20), Theme.S(16), CW() - Theme.S(40), Theme.S(56));
            bcard.Controls.Add(bl);
            PillButton edit = new PillButton(); edit.Text = "Edit bell schedules"; edit.Glyph = "\uE713"; edit.Kind = 1;
            edit.SetBounds(Theme.S(20), Theme.S(88), edit.PreferredWidth(), Theme.S(40));
            edit.Click += delegate { GoTo("bells"); };
            bcard.Controls.Add(edit);
            vy += Theme.S(150) + gap + Theme.S(10);

            SectionTitle("Refresh");
            string[] labels = new string[] { "Every 5 min", "Every 15 min", "Every 30 min", "Off" };
            int[] mins = new int[] { 5, 15, 30, 0 };
            Segmented rs = new Segmented();
            rs.SetOptions(labels, Math.Max(0, Array.IndexOf(mins, cfg.Interval)));
            rs.Changed += delegate
            {
                cfg.Interval = mins[rs.Selected];
                cfg.Save();
                ApplyInterval();
            };
            rs.Location = new Point(X0(), vy);
            host.Controls.Add(rs);
            vy += rs.Height + Theme.S(30);

            SectionTitle("More");
            AddButton("Diagnostics", "\uE9D9", 0, X0(), vy, delegate { GoTo("debug"); });
            AddButton("Open data folder", "\uE838", 0, X0() + Theme.S(160), vy, delegate
            {
                try { Process.Start(new ProcessStartInfo(Disk.Dir()) { UseShellExecute = true }); } catch (Exception) { }
            });
            vy += Theme.S(56);
        }

        void ChooseImage(string fileName, bool isPhoto)
        {
            using (OpenFileDialog of = new OpenFileDialog())
            {
                of.Filter = "Pictures (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                if (of.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Image img = Image.FromFile(of.FileName))
                    {
                        Bitmap bmp = new Bitmap(img);
                        bmp.Save(Disk.At(fileName), System.Drawing.Imaging.ImageFormat.Png);
                        if (isPhoto) { if (photo != null) photo.Dispose(); photo = bmp; photoNote = "Using your own photo."; }
                        else { if (logo != null) logo.Dispose(); logo = Draw.Trim(bmp); card.Logo = logo; logoNote = "Using your own logo."; UpdateIcon(); }
                    }
                    rail.Invalidate();
                    top.Invalidate();
                    ShowView(true);
                }
                catch (Exception) { stampText = "That picture could not be read."; stampBad = true; UpdateTop(); }
            }
        }

        void RemoveCustom(string fileName, bool isPhoto)
        {
            try { if (File.Exists(Disk.At(fileName))) File.Delete(Disk.At(fileName)); } catch (Exception) { }
            if (isPhoto) { if (photo != null) photo.Dispose(); photo = null; photoNote = ""; }
            else { if (logo != null) logo.Dispose(); logo = null; logoNote = ""; }
            LoadLocalImages();
            card.Logo = logo;
            UpdateIcon();
            if (api != null && current != null) LoadAssets(api, current, true);
            rail.Invalidate();
            top.Invalidate();
            ShowView(true);
        }

        // ------------------------------------------------------------ debug
        string ReportText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Campus Midnight diagnostics");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("F"));
            sb.AppendLine("Contains endpoint names, HTTP status and field names only. No grades, names or other values.");
            sb.AppendLine();
            if (snap != null)
            {
                foreach (DiagRow d in snap.Diag)
                {
                    sb.AppendLine(d.Key + "  " + d.Path + "  status=" + d.Status + "  ok=" + d.Ok + "  bytes=" + d.Bytes
                        + (d.Records > 0 ? "  records=" + d.Records : "")
                        + (d.Error != null ? "  error=" + d.Error : ""));
                    if (d.Shape != null) sb.AppendLine(d.Shape);
                    sb.AppendLine();
                }
                sb.AppendLine("Address discovery: " + (snap.DiscoverNote.Length > 0 ? snap.DiscoverNote : "not needed"));
                foreach (string dl in snap.DiscoverLog) sb.AppendLine("  discovery: " + dl);
                if (api != null)
                {
                    sb.AppendLine("Photo: " + (photoNote.Length > 0 ? photoNote : "none"));
                    sb.AppendLine("Logo: " + (logoNote.Length > 0 ? logoNote : "none"));
                }
                sb.AppendLine("Parsed: students=" + snap.Students.Count + " courses=" + snap.Courses.Count + " terms=" + snap.Terms.Count
                    + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " attendance=" + snap.Attendance.Count
                    + " messages=" + snap.Messages.Count + " notices=" + snap.Notices.Count + " documents=" + snap.Docs.Count
                    + " dropped=" + snap.Courses.FindAll(delegate(Course c) { return c.Dropped; }).Count
                    + " calendarDays=" + snap.CalDays.Count);
                if (current != null)
                    sb.AppendLine("Profile: id=" + (current.Number.Length > 0 ? "yes" : "no") + " grade=" + (current.Grade.Length > 0 ? "yes" : "no")
                        + " school=" + (current.School.Length > 0 ? "yes" : "no"));
                // candidate portal addresses mentioning things we look for (addresses only)
                if (api != null)
                {
                    string[] disc = api.DiscoveredPaths();
                    int shown = 0;
                    sb.AppendLine();
                    sb.AppendLine("Portal addresses that look relevant:");
                    foreach (string p in disc)
                    {
                        if (shown >= 60) break;
                        if (System.Text.RegularExpressions.Regex.IsMatch(p, "messag|inbox|notice|announce|document|attend|absen|picture|photo|logo|assign|calendar|schedule", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            sb.AppendLine(p);
                            shown++;
                        }
                    }
                }
            }
            else sb.AppendLine("No data loaded yet.");
            return sb.ToString();
        }

        void BuildDebug()
        {
            Header("Diagnostics", "What each Campus request returned. Names and counts only, never your data.");
            PillButton copy = AddButton("Copy report", "\uE8C8", 1, X0(), vy, null);
            copy.Click += delegate
            {
                try { Clipboard.SetText(ReportText()); copy.Text = "Copied"; }
                catch (Exception) { copy.Text = "Could not copy"; }
            };
            AddButton("Run again", "\uE72C", 0, X0() + copy.Width + Theme.S(10), vy, delegate { RefreshData(); });
            vy += Theme.S(58);

            ListCard lc = new ListCard();
            lc.EmptyText = "Nothing yet. Press Refresh.";
            if (snap != null)
            {
                foreach (DiagRow d in snap.Diag)
                {
                    Item it = new Item();
                    it.Title = d.Key + "   " + d.Path;
                    it.Sub = d.Error != null ? d.Error : "HTTP " + d.Status + "  ·  " + Math.Round(d.Bytes / 1024.0, 1) + " KB" + (d.Records > 0 ? "  ·  " + d.Records + " records" : "");
                    it.Badge = d.Ok ? "OK" : "Failed";
                    it.BadgeBack = d.Ok ? Theme.AccSoft : Theme.BadSoft;
                    it.BadgeFore = d.Ok ? Theme.AccText : Theme.Bad;
                    lc.Items.Add(it);
                }
                Item p = new Item();
                p.Title = "Parsed";
                p.Sub = snap.Students.Count + " students  ·  " + snap.Courses.Count + " courses  ·  " + snap.Assigns.Count
                    + " assignments  ·  " + snap.Roster.Count + " in schedule  ·  " + snap.Messages.Count + " messages  ·  "
                    + snap.Notices.Count + " announcements  ·  " + snap.Docs.Count + " documents";
                lc.Items.Add(p);
            }
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
        }
    }
}
