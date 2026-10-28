using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CampusMidnight
{
    static class Rows
    {
        public const string NoData =
            "You are signed in, but nothing readable came back. Your school's portal may use different addresses than this app expects. " +
            "Open the Debug tab, press Copy report and send it over so the app can be adjusted. " +
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
            it.Title = a.Name;
            string due = a.Due.HasValue ? "Due " + a.Due.Value.ToString("MMM d") : "No due date";
            it.Sub = (a.Course.Length > 0 ? a.Course + "  ·  " : "") + due + (a.Late ? "  ·  Late" : "");
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
            else if (a.Status == "ungraded") it.Badge = "Not graded";
            else if (a.Status == "exempt") it.Badge = "Exempt";
            return it;
        }

        public static string GradeText(TaskGrade t)
        {
            string s = t.Letter;
            if (t.Percent.HasValue) s += (s.Length > 0 ? "  ·  " : "") + Theme.Fmt(t.Percent) + "%";
            return s;
        }
    }

    // ---------------------------------------------------------------- one class, in a window of its own
    class CourseForm : Form
    {
        public CourseForm(Course c, List<Assign> list)
        {
            Text = c.Name;
            BackColor = Theme.Bg0;
            ForeColor = Theme.T1;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.S(640), Theme.S(740));
            MinimumSize = new Size(Theme.S(460), Theme.S(420));
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            ScrollPanel sp = new ScrollPanel();
            sp.Dock = DockStyle.Fill;
            Controls.Add(sp);

            int x = Theme.S(28);
            int w = ClientSize.Width - Theme.S(56) - SystemInformation.VerticalScrollBarWidth;
            int y = Theme.S(24);

            Label name = new Label();
            name.AutoSize = false; name.UseMnemonic = false; name.AutoEllipsis = true;
            name.Text = c.Name; name.Font = Theme.Title; name.ForeColor = Theme.T1; name.BackColor = Theme.Bg0;
            name.SetBounds(x, y, w, Theme.S(44));
            sp.Controls.Add(name);
            y += Theme.S(46);

            string meta = c.Teacher;
            if (c.Room.Length > 0) meta += (meta.Length > 0 ? "  ·  " : "") + "Room " + c.Room;
            if (c.Term.Length > 0) meta += (meta.Length > 0 ? "  ·  " : "") + c.Term;
            Label ml = new Label();
            ml.AutoSize = false; ml.UseMnemonic = false;
            ml.Text = meta; ml.Font = Theme.Body; ml.ForeColor = Theme.T2; ml.BackColor = Theme.Bg0;
            ml.SetBounds(x, y, w, Theme.S(24));
            sp.Controls.Add(ml);
            y += Theme.S(40);

            Label big = new Label();
            big.AutoSize = false; big.UseMnemonic = false;
            big.Text = (string.IsNullOrEmpty(c.Letter) ? "–" : c.Letter) + (c.Percent.HasValue ? "   " + Theme.Fmt(c.Percent) + "%" : "");
            big.Font = Theme.Huge; big.ForeColor = Theme.GradeColor(c.Percent); big.BackColor = Theme.Bg0;
            big.SetBounds(x, y, w, Theme.S(54));
            sp.Controls.Add(big);
            y += Theme.S(70);

            if (c.Tasks.Count > 1)
            {
                ListCard tc = new ListCard();
                tc.Heading = "By grading period";
                foreach (TaskGrade t in c.Tasks)
                {
                    Item it = new Item();
                    it.Title = t.Name.Length > 0 ? t.Name : "Grade";
                    it.Right = Rows.GradeText(t);
                    it.RightColor = Theme.GradeColor(t.Percent);
                    tc.Items.Add(it);
                }
                tc.SetBounds(x, y, w, tc.PreferredHeight());
                sp.Controls.Add(tc);
                y += tc.Height + Theme.S(16);
            }

            ListCard ac = new ListCard();
            ac.Heading = "Assignments";
            ac.EmptyText = "No assignments found for this class.";
            Rows.SortAssigns(list, false);
            int n = 0;
            foreach (Assign a in list)
            {
                if (n++ >= 200) break;
                ac.Items.Add(Rows.ForAssign(a));
            }
            ac.SetBounds(x, y, w, ac.PreferredHeight());
            sp.Controls.Add(ac);
            y += ac.Height;
            sp.AutoScrollMinSize = new Size(0, y + Theme.S(32));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle);
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

        int CW()
        {
            return Math.Max(Theme.S(560), host.ClientSize.Width - Theme.S(64) - SystemInformation.VerticalScrollBarWidth);
        }

        int X0() { return Theme.S(32); }

        void ShowView(bool keepScroll)
        {
            for (int i = 0; i < navs.Count; i++)
            {
                bool on = navKeys[i] == view;
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
                vy = Theme.S(28);
                switch (view)
                {
                    case "grades": BuildGrades(); break;
                    case "assignments": BuildAssignments(); break;
                    case "schedule": BuildSchedule(); break;
                    case "attendance": BuildAttendance(); break;
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

        void AddNotice(string text)
        {
            NoticeCard nc = new NoticeCard();
            nc.Text2 = text;
            int h = nc.PreferredHeight(CW());
            AddCard(nc, X0(), vy, CW(), h);
            vy += h + Theme.S(16);
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

            List<Course> cs = snap.CoursesFor(term);
            double? avg = Snapshot.Average(cs);
            DateTime today = DateTime.Today;
            DateTime weekEnd = today.AddDays(7);
            List<Assign> upcoming = snap.Assigns.FindAll(delegate(Assign a)
            {
                return a.Status == "upcoming" && (!a.Due.HasValue || a.Due.Value >= today);
            });
            Rows.SortAssigns(upcoming, true);
            List<Assign> missing = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "missing"; });
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
            tiles[1].Big = (cs.Count > 0 ? cs.Count : snap.Roster.Count).ToString();
            tiles[1].Label = "Classes";
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
            for (int i = 0; i < upcoming.Count && i < 8; i++) soon.Items.Add(Rows.ForAssign(upcoming[i]));

            ListCard attn = new ListCard();
            attn.Heading = "Needs attention";
            attn.EmptyText = "All clear: no missing work and no grade below 70%.";
            for (int i = 0; i < missing.Count && i < 6; i++) attn.Items.Add(Rows.ForAssign(missing[i]));
            foreach (Course c in cs)
            {
                if (c.Percent.HasValue && c.Percent.Value < 70)
                {
                    Item it = new Item();
                    it.Title = c.Name;
                    it.Sub = "Grade is below 70%";
                    it.Right = Math.Round(c.Percent.Value) + "%";
                    it.RightColor = Theme.Bad;
                    attn.Items.Add(it);
                }
            }

            if (CW() >= Theme.S(860))
            {
                int lw = (CW() - gap) / 2;
                int lh = Math.Max(soon.PreferredHeight(), attn.PreferredHeight());
                AddCard(soon, X0(), vy, lw, lh);
                AddCard(attn, X0() + lw + gap, vy, lw, lh);
                vy += lh + gap;
            }
            else
            {
                AddCard(soon, X0(), vy, CW(), soon.PreferredHeight());
                vy += soon.PreferredHeight() + gap;
                AddCard(attn, X0(), vy, CW(), attn.PreferredHeight());
                vy += attn.PreferredHeight() + gap;
            }

            if (snap.Roster.Count > 0)
            {
                ListCard cl = new ListCard();
                cl.Heading = "Your classes";
                for (int i = 0; i < snap.Roster.Count && i < 10; i++) cl.Items.Add(RosterRow(snap.Roster[i], false));
                AddCard(cl, X0(), vy, CW(), cl.PreferredHeight());
                vy += cl.PreferredHeight() + gap;
            }
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

        // ------------------------------------------------------------ grades
        void BuildGrades()
        {
            Header("Grades", "Select a class to see every assignment in it.");
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

            List<Course> cs = snap.CoursesFor(term);
            int gap = Theme.S(16);
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
        }

        void OpenCourse(Course c)
        {
            List<Assign> list = new List<Assign>();
            string key = Norm.Key(c.Name);
            if (snap != null)
            {
                foreach (Assign a in snap.Assigns)
                {
                    string ak = Norm.Key(a.Course);
                    if (ak.Length > 0 && (ak == key || ak.Contains(key) || key.Contains(ak))) list.Add(a);
                }
            }
            using (CourseForm f = new CourseForm(c, list))
            {
                f.ShowDialog(this);
            }
        }

        // ------------------------------------------------------------ assignments
        void BuildAssignments()
        {
            Header("Assignments", snap != null ? snap.Assigns.Count + " total" : "");
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
                if (filter == "upcoming") return a.Status == "upcoming";
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
                lc.Items.Add(Rows.ForAssign(a));
                shown++;
            }
            lc.Heading = total > shown ? "Showing the first " + shown + " of " + total : total + (total == 1 ? " assignment" : " assignments");
            aList = lc;
            lc.SetBounds(X0(), aListY, CW(), lc.PreferredHeight());
            host.Controls.Add(lc);
            vy = aListY + lc.Height;
            host.AutoScrollMinSize = new Size(0, vy + Theme.S(36));
        }

        // ------------------------------------------------------------ schedule
        void BuildSchedule()
        {
            Header("Schedule", "Your classes by period.");
            if (snap == null) return;
            if (snap.Roster.Count == 0) { AddNotice(Rows.NoData); return; }
            ListCard lc = new ListCard();
            foreach (RosterItem r in snap.Roster) lc.Items.Add(RosterRow(r, true));
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
        }

        // ------------------------------------------------------------ attendance
        void BuildAttendance()
        {
            Header("Attendance", "Most recent first.");
            if (snap == null) return;
            if (snap.Attendance.Count == 0)
            {
                AddNotice("Your school's portal did not return attendance in a form this app can read. " +
                    "Use Original site to check it there, or open the Debug tab.");
                return;
            }
            Dictionary<string, int> tally = new Dictionary<string, int>();
            foreach (AttRow r in snap.Attendance)
            {
                int n;
                tally.TryGetValue(r.Status, out n);
                tally[r.Status] = n + 1;
            }
            List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(tally);
            pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
            int gap = Theme.S(16);
            int cols = CW() >= Theme.S(900) ? 4 : 2;
            int tw = (CW() - gap * (cols - 1)) / cols;
            int th = Theme.S(100);
            int count = Math.Min(4, pairs.Count);
            for (int i = 0; i < count; i++)
            {
                TileCard t = new TileCard();
                t.Big = pairs[i].Value.ToString();
                t.Label = pairs[i].Key;
                AddCard(t, X0() + (i % cols) * (tw + gap), vy + (i / cols) * (th + gap), tw, th);
            }
            vy += ((count + cols - 1) / cols) * (th + gap);

            ListCard lc = new ListCard();
            for (int i = 0; i < snap.Attendance.Count && i < 60; i++)
            {
                AttRow r = snap.Attendance[i];
                Item it = new Item();
                it.Title = r.Status;
                string sub = r.Period.Length > 0 ? "Period " + r.Period : "";
                if (r.Course.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + r.Course;
                it.Sub = sub;
                it.Right = Rows.DayLabel(r.Date);
                it.RightColor = Theme.T2;
                lc.Items.Add(it);
            }
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
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
                        + (d.Error != null ? "  error=" + d.Error : ""));
                    if (d.Shape != null) sb.AppendLine(d.Shape);
                    sb.AppendLine();
                }
                sb.AppendLine("Parsed: students=" + snap.Students.Count + " courses=" + snap.Courses.Count + " terms=" + snap.Terms.Count
                    + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " attendance=" + snap.Attendance.Count);
            }
            else sb.AppendLine("No data loaded yet.");
            return sb.ToString();
        }

        void BuildDebug()
        {
            Header("Debug", "What each Campus request returned. Names and counts only, never your data.");
            PillButton copy = new PillButton();
            copy.Text = "Copy report";
            copy.Click += delegate
            {
                try { Clipboard.SetText(ReportText()); copy.Text = "Copied"; }
                catch (Exception) { copy.Text = "Could not copy"; }
            };
            AddCard(copy, X0(), vy, Theme.S(150), Theme.S(40));
            PillButton again = new PillButton();
            again.Text = "Run again";
            again.Click += delegate { RefreshData(); };
            AddCard(again, X0() + Theme.S(162), vy, Theme.S(130), Theme.S(40));
            vy += Theme.S(58);

            ListCard lc = new ListCard();
            lc.EmptyText = "Nothing yet. Press Refresh.";
            if (snap != null)
            {
                foreach (DiagRow d in snap.Diag)
                {
                    Item it = new Item();
                    it.Title = d.Key + "   " + d.Path;
                    it.Sub = d.Error != null ? d.Error : "HTTP " + d.Status + "  ·  " + Math.Round(d.Bytes / 1024.0, 1) + " KB";
                    it.Badge = d.Ok ? "OK" : "Failed";
                    it.BadgeBack = d.Ok ? Theme.AccSoft : Theme.BadSoft;
                    it.BadgeFore = d.Ok ? Theme.AccText : Theme.Bad;
                    lc.Items.Add(it);
                }
                Item p = new Item();
                p.Title = "Parsed";
                p.Sub = snap.Students.Count + " students  ·  " + snap.Courses.Count + " courses  ·  " + snap.Assigns.Count
                    + " assignments  ·  " + snap.Roster.Count + " in schedule  ·  " + snap.Attendance.Count + " attendance rows";
                lc.Items.Add(p);
            }
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight();
        }
    }
}
