using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CampusMidnight
{
    // Home, Now, Grades and Assignments, laid out like the WinForms app
    sealed partial class MainWindow
    {
        Course openCourse;
        string assignFilter = "upcoming", assignSearch = "";
        string pageSub = "", pageTitle = "";
        DispatcherTimer resizeTimer;
        int lastCw;

        // live page bits
        string liveKey = "";
        RingMeter heroRing;
        TextBlock heroLeft, heroEnd;
        ContentControl heroBar, stripHost;
        DayPlan livePlan;

        int CW()
        {
            double w = pageHost != null && pageHost.ActualWidth > 100 ? pageHost.ActualWidth : (ActualWidth > 300 ? ActualWidth - 200 : 1100);
            return (int)Math.Max(560, w - 60 - 16);
        }

        void OnHostResized()
        {
            if (snap == null) return;
            if (resizeTimer == null)
            {
                resizeTimer = new DispatcherTimer();
                resizeTimer.Interval = TimeSpan.FromMilliseconds(220);
                resizeTimer.Tick += delegate
                {
                    resizeTimer.Stop();
                    int cw = CW();
                    if (Math.Abs(cw - lastCw) > 24 && view != "setup") ShowView(false);
                };
            }
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        void ShowView(bool animate)
        {
            if (pageHost == null) return;
            UpdateNav();
            UpdateTop();
            heroRing = null; heroLeft = null; heroEnd = null; heroBar = null; stripHost = null; livePlan = null;
            pageTitle = ""; pageSub = "";
            lastCw = CW();
            UIElement page;
            double keep = 0;
            ScrollViewer old = pageHost.Content as ScrollViewer;
            if (old != null && !animate) keep = old.VerticalOffset;
            if (snap == null && view != "settings" && view != "debug" && view != "setup")
                page = Center(loading ? "Loading your Campus data…" : (stampBad ? stampText : "Nothing loaded yet."));
            else
            {
                try
                {
                    switch (view)
                    {
                        case "now": page = PageNow(); break;
                        case "grades": page = PageGrades(); break;
                        case "assignments": page = PageAssignments(); break;
                        case "schedule": page = PageSched(); break;
                        case "tools": page = PageTools(); break;
                        case "settings": page = PageSettings(); break;
                        case "debug": page = PageDebug(); break;
                        case "setup": page = PageSetup(); break;
                        case "inbox": page = PageInbox(); break;
                        case "docs": page = PageDocs(); break;
                        default: page = PageHome(); break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("page " + view, ex);
                    page = Center("Something went wrong drawing this page. The log has the details.");
                }
            }
            pageHost.Content = page;
            ScrollViewer nv = page as ScrollViewer;
            if (nv != null && keep > 0) { double k = keep; nv.Loaded += delegate { nv.UpdateLayout(); nv.ScrollToVerticalOffset(k); }; }
            if (animate && page is FrameworkElement) UI.FadeIn((FrameworkElement)page);
        }

        UIElement Center(string msg)
        {
            TextBlock t = UI.Wrap(msg, 14, "T3");
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            t.MaxWidth = 460;
            return t;
        }

        // scrolling page body, title left aligned
        UIElement Body(params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(30, 4, 30, 36);
            p.MaxWidth = 1160;
            p.HorizontalAlignment = HorizontalAlignment.Left;
            TextBlock title = UI.Text(pageTitle.Length > 0 ? pageTitle : ViewTitle(), 26, "T1", true);
            title.Margin = new Thickness(0, 0, 0, pageSub.Length > 0 ? 2 : 16);
            p.Children.Add(title);
            if (pageSub.Length > 0)
            {
                TextBlock sub = UI.Text(pageSub, 13.5, "T2", false);
                sub.Margin = new Thickness(0, 0, 0, 18);
                p.Children.Add(sub);
            }
            foreach (UIElement k in kids) if (k != null) p.Children.Add(k);
            return UI.Scroll(p);
        }

        static TextBlock Heading(string s)
        {
            TextBlock t = UI.Text(s, 15, "T1", true);
            t.Margin = new Thickness(0, 0, 0, 10);
            return t;
        }

        static Border Sep()
        {
            Border b = new Border();
            b.Height = 1;
            b.SetResourceReference(Border.BackgroundProperty, "Line");
            b.Margin = new Thickness(0, 2, 0, 2);
            return b;
        }

        static void Hover(Border b, string rest, string over, Action click)
        {
            b.Cursor = Cursors.Hand;
            if (rest == null) b.Background = Brushes.Transparent; else b.SetResourceReference(Border.BackgroundProperty, rest);
            b.MouseEnter += delegate { b.SetResourceReference(Border.BackgroundProperty, over); };
            b.MouseLeave += delegate { if (rest == null) b.Background = Brushes.Transparent; else b.SetResourceReference(Border.BackgroundProperty, rest); };
            b.MouseLeftButtonUp += delegate { click(); };
        }

        List<Course> TermCourses()
        {
            return cfg.CountDropped ? snap.CoursesFor(term) : snap.Active(term);
        }

        // ------------------------------------------------------------ shared pieces
        UIElement Avatar(double size)
        {
            Border b = new Border();
            b.Width = size; b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            if (photo != null && !cfg.HidePhoto)
            {
                ImageBrush ib = new ImageBrush(photo);
                ib.Stretch = Stretch.UniformToFill;
                b.Background = ib;
            }
            else
            {
                b.SetResourceReference(Border.BackgroundProperty, "AccSoft");
                TextBlock t = UI.Text(Rows.Initials(current != null ? current.Name : ""), size * 0.36, "AccText", true);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
                b.Child = t;
            }
            return b;
        }

        UIElement SegBar(string[] names, int sel, Action<int> changed)
        {
            UIElement s = SuSeg(names, sel, changed);
            ((FrameworkElement)s).Margin = new Thickness(0, 0, 0, 18);
            return s;
        }

        ItemSpec ForAssign(Assign a)
        {
            ItemSpec it = new ItemSpec();
            it.Dim = a.InDropped;
            it.Title = a.Name;
            string due = a.Due.HasValue ? "Due " + a.Due.Value.ToString("MMM d") : "No due date";
            it.Sub = (a.Course.Length > 0 ? a.Course + "  ·  " : "") + due + (a.Late ? "  ·  Late" : "") + (a.InDropped ? "  ·  Dropped class" : "");
            if (a.Status == "graded")
            {
                string score = a.Total.HasValue ? Rows.Fmt(a.Score) + "/" + Rows.Fmt(a.Total) : Rows.Fmt(a.Score);
                if (a.Percent.HasValue) score += "  ·  " + Math.Round(a.Percent.Value) + "%";
                it.Right = score;
                it.RightColor = Theme.GradeColor(a.Percent);
            }
            else if (a.Status == "missing") { it.Badge = "Missing"; it.BadgeBg = "BadSoft"; it.BadgeFg = "Bad"; }
            else if (a.Status == "upcoming") { it.Badge = "Upcoming"; it.BadgeBg = "Bg4"; it.BadgeFg = "T2"; }
            else if (a.Status == "submitted") { it.Badge = "Turned in"; }
            else if (a.Status == "ungraded") { it.Badge = "Not graded"; it.BadgeBg = "Bg4"; it.BadgeFg = "T2"; }
            else if (a.Status == "exempt") { it.Badge = "Exempt"; it.BadgeBg = "Bg4"; it.BadgeFg = "T2"; }
            Assign aa = a;
            it.Click = delegate { OpenAssign(aa); };
            return it;
        }

        // used by the class window
        UIElement AssignRow(Assign a, bool showCourse)
        {
            ItemSpec it = ForAssign(a);
            if (!showCourse) it.Sub = (a.Due.HasValue ? "Due " + a.Due.Value.ToString("MMM d") : "No due date") + (a.Late ? "  ·  Late" : "");
            return PageKit.Row(it);
        }

        void HomeClickCourse(Course c) { OpenCourse(c); }

        // one class meeting in a day list
        UIElement SlotRow(Slot s, bool live)
        {
            Grid g = UI.Cols(0, 0, 1);
            g.Margin = new Thickness(0, 3, 0, 3);
            Border bar = new Border();
            bar.Width = 4; bar.CornerRadius = new CornerRadius(2);
            bar.Background = new SolidColorBrush(s.IsClass ? Theme.Tag(s.Title) : Theme.Line2);
            bar.Margin = new Thickness(0, 2, 12, 2);
            UI.At(g, bar, 0);
            TextBlock time = UI.Text(Bell.Clock(s.B.Start) + " – " + Bell.Clock(s.B.End), 12.5, live ? "AccText" : "T3", true);
            time.Width = 150;
            time.VerticalAlignment = VerticalAlignment.Center;
            UI.At(g, time, 1);
            StackPanel nm = new StackPanel();
            nm.Children.Add(UI.Text(s.Title, 14, s.IsClass ? "T1" : "T3", s.IsClass));
            string sub = "";
            if (s.R != null) { sub = s.R.Teacher ?? ""; if (!string.IsNullOrEmpty(s.R.Room)) sub += (sub.Length > 0 ? " · " : "") + "Room " + s.R.Room; }
            if (sub.Length > 0) nm.Children.Add(UI.Text(sub, 12, "T3", false));
            UI.At(g, nm, 2);
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Padding = new Thickness(8, 5, 8, 5);
            row.Child = g;
            if (s.C != null) Hover(row, live ? "AccDeep" : null, live ? "AccSoft" : "Bg3", delegate { OpenCourse(s.C); });
            else if (live) row.SetResourceReference(Border.BackgroundProperty, "AccDeep");
            return row;
        }

        // the "who is this" card at the top of Home
        UIElement ProfileCard()
        {
            Student s = current;
            Grid g = UI.Cols(0, 1, 0);
            double pad = 26;
            if (!cfg.HidePhoto)
            {
                UIElement av = Avatar(92);
                ((FrameworkElement)av).Margin = new Thickness(0, 0, 22, 0);
                ((FrameworkElement)av).VerticalAlignment = VerticalAlignment.Center;
                UI.At(g, av, 0);
            }
            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            info.Children.Add(UI.Text(s.Name, 24, "T1", true));
            string line = s.Line();
            info.Children.Add(UI.Margin(UI.Text(line.Length > 0 ? line : "Student", 13.5, "T2", false), 0, 3, 0, 12));
            WrapPanel chips = new WrapPanel();
            if (s.Number.Length > 0)
            {
                Border id = UI.Chip("ID  " + s.Number, "Bg4", "T1");
                id.Cursor = Cursors.Hand;
                id.ToolTip = "Click to copy";
                string num = s.Number;
                id.MouseLeftButtonUp += delegate
                {
                    try
                    {
                        Clipboard.SetText(num);
                        id.Child = UI.Text("Copied", 12, "AccText", true);
                        id.SetResourceReference(Border.BackgroundProperty, "AccSoft");
                        DispatcherTimer t = new DispatcherTimer();
                        t.Interval = TimeSpan.FromMilliseconds(1600);
                        t.Tick += delegate { t.Stop(); id.Child = UI.Text("ID  " + num, 12, "T1", true); id.SetResourceReference(Border.BackgroundProperty, "Bg4"); };
                        t.Start();
                    }
                    catch (Exception) { }
                };
                id.Margin = new Thickness(0, 0, 8, 6);
                chips.Children.Add(id);
            }
            if (s.StateId.Length > 0) { Border c = UI.Chip("State ID  " + s.StateId, "Bg4", "T1"); c.Margin = new Thickness(0, 0, 8, 6); chips.Children.Add(c); }
            if (s.Year.Length > 0) { Border c = UI.Chip(s.Year, "Bg4", "T2"); c.Margin = new Thickness(0, 0, 8, 6); chips.Children.Add(c); }
            if (s.ClassOf.Length > 0) { Border c = UI.Chip(s.ClassOf, "Bg4", "T2"); c.Margin = new Thickness(0, 0, 8, 6); chips.Children.Add(c); }
            info.Children.Add(chips);
            UI.At(g, info, 1);
            if (logo != null)
            {
                Image im = new Image();
                im.Source = logo; im.Height = 64; im.Width = 64; im.Stretch = Stretch.Uniform;
                im.Margin = new Thickness(16, 0, 0, 0);
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                UI.At(g, im, 2);
            }
            Border card = UI.Card(g, pad);
            card.Margin = new Thickness(0, 0, 0, 16);
            return card;
        }

        // slim "now / up next" bar under the profile card
        UIElement StripContent(DayPlan plan)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            string ph = Sched.Phase(plan, now, out cur, out next);
            string kicker, title, right = "";
            double frac = -1;
            if (ph == "class")
            {
                Slot s = plan.Slots[cur];
                kicker = "NOW"; title = s.Title; right = Sched.Span(s.B.End - now) + " left";
                TimeSpan total = s.B.End - s.B.Start;
                frac = total.TotalSeconds > 0 ? (now - s.B.Start).TotalSeconds / total.TotalSeconds : 0;
            }
            else if (ph == "between" || ph == "before")
            {
                Slot n = plan.Slots[next];
                kicker = ph == "before" ? "SCHOOL STARTS" : "UP NEXT"; title = n.Title; right = "in " + Sched.Span(n.B.Start - now);
            }
            else if (ph == "after") { kicker = "TODAY"; title = "School is out"; }
            else { kicker = "TODAY"; title = !plan.School ? "No school" : "Set up your bell schedule"; right = !plan.School ? "" : "Open"; }
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Text(kicker, 11.5, "AccText", true));
            Grid row = UI.Cols(1, 0);
            row.Margin = new Thickness(0, 2, 0, 0);
            UI.At(row, UI.Text(title, 17, "T1", true), 0);
            if (right.Length > 0) UI.At(row, UI.Text(right, 14, "T1", true), 1);
            p.Children.Add(row);
            if (frac >= 0) p.Children.Add(UI.Margin((FrameworkElement)UI.Meter(Math.Max(0, Math.Min(1, frac)) * 100, Theme.Acc), 0, 10, 0, 0));
            return p;
        }

        // ------------------------------------------------------------ Home
        UIElement PageHome()
        {
            string first = current != null ? current.First : "";
            int hr = DateTime.Now.Hour;
            string greet = hr < 12 ? "Good morning" : (hr < 18 ? "Good afternoon" : "Good evening");
            pageTitle = greet + (first.Length > 0 ? ", " + first : "");
            pageSub = DateTime.Now.ToString("dddd, MMMM d");
            List<UIElement> kids = new List<UIElement>();
            if (!snap.HasAny) kids.Add(PageKit.Notice(Rows.NoData, false));
            if (current != null) kids.Add(ProfileCard());

            DayPlan plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            livePlan = plan;
            stripHost = new ContentControl();
            stripHost.Content = StripContent(plan);
            Border strip = UI.Card(stripHost, 22);
            strip.Margin = new Thickness(0, 0, 0, 16);
            Hover(strip, "Bg2", "Bg3", delegate { if (plan.Sched == null) StartScheduleEdit(); else Go("now"); });
            kids.Add(strip);
            liveKey = Sched.StateKey(plan, DateTime.Now.TimeOfDay);

            List<Course> cs = snap.Active(term);
            double? avg = Snapshot.Average(cs);
            List<Course> gset = cfg.CountDropped ? snap.CoursesFor(term) : cs;
            double? gpaU = Rows.Gpa(gset, false), gpaW = Rows.Gpa(gset, true);
            DateTime today = DateTime.Today;
            DateTime weekEnd = today.AddDays(7);
            List<Assign> upcoming = snap.Assigns.FindAll(delegate(Assign a) { return Rows.IsUpcoming(a) && (!a.Due.HasValue || a.Due.Value >= today); });
            Rows.SortAssigns(upcoming, true);
            List<Assign> missing = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "missing" && !a.InDropped; });
            Rows.SortAssigns(missing, true);
            int dueWeek = 0;
            foreach (Assign a in upcoming) if (a.Due.HasValue && a.Due.Value <= weekEnd) dueWeek++;

            UIElement t0 = avg.HasValue
                ? PageKit.Tile(Theme.GradeStr(Norm.LetterFor(avg), avg), Theme.GradeColor(avg), "Average" + (term.Length > 0 ? "  ·  " + term : ""), avg.Value)
                : PageKit.Tile("–", null, "Average" + (term.Length > 0 ? "  ·  " + term : ""), null);
            UIElement t1 = PageKit.Tile(Rows.G2(gpaU), null, "GPA  ·  " + Rows.G2(gpaW) + " weighted", null);
            UIElement t2 = PageKit.Tile(missing.Count.ToString(), missing.Count > 0 ? (Color?)Theme.Bad : null, "Missing assignments", null);
            UIElement t3 = PageKit.Tile(dueWeek.ToString(), dueWeek > 0 ? (Color?)Theme.Warn : null, "Due in the next 7 days", null);
            kids.Add(PageKit.TileRow(CW(), t0, t1, t2, t3));

            List<ItemSpec> soon = new List<ItemSpec>();
            for (int i = 0; i < upcoming.Count && i < 8; i++) soon.Add(ForAssign(upcoming[i]));
            List<ItemSpec> attn = new List<ItemSpec>();
            for (int i = 0; i < missing.Count && i < 6; i++) attn.Add(ForAssign(missing[i]));
            foreach (Course c in cs)
            {
                if (c.Percent.HasValue && c.Percent.Value < 70)
                {
                    ItemSpec it = new ItemSpec();
                    it.Title = c.Name;
                    it.Sub = "Grade is below 70%";
                    it.Right = Math.Round(c.Percent.Value) + "%";
                    it.RightColor = Theme.Bad;
                    Course cc = c;
                    it.Click = delegate { OpenCourse(cc); };
                    attn.Add(it);
                }
            }
            kids.Add(PageKit.Pair(CW(), PageKit.ListCard("Coming up", "Nothing due. Enjoy it.", soon),
                PageKit.ListCard("Needs attention", "All clear: no missing work and no grade below 70%.", attn)));

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
                List<ItemSpec> inb = new List<ItemSpec>();
                for (int i = 0; i < recent.Count && i < 4; i++) inb.Add(MsgRow(recent[i]));
                kids.Add(PageKit.ListCard("Latest from your inbox", "", inb));
            }
            return Body(kids.ToArray());
        }

        // ------------------------------------------------------------ Now
        static void CountText(TimeSpan left, out string big, out string small)
        {
            if (left.TotalMinutes >= 60)
            {
                int mins = (int)Math.Ceiling(left.TotalMinutes);
                big = (mins / 60) + ":" + (mins % 60).ToString("00");
                small = "hours left";
            }
            else if (left.TotalSeconds > 60)
            {
                big = ((int)Math.Ceiling(left.TotalMinutes)).ToString();
                small = "min left";
            }
            else
            {
                big = ((int)Math.Max(0, Math.Ceiling(left.TotalSeconds))).ToString();
                small = "sec left";
            }
        }

        UIElement PageNow()
        {
            pageSub = DateTime.Now.ToString("dddd, MMMM d");
            if (cfg.Schedules.Count == 0)
            {
                Button b = UI.Btn("Set up my schedule", "Pill", delegate { StartScheduleEdit(); });
                b.HorizontalAlignment = HorizontalAlignment.Left;
                return Body(PageKit.Notice("Set up your schedule once and this page shows your current class, a live countdown and what comes next.", false), b);
            }
            DayPlan plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            livePlan = plan;
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            string ph = Sched.Phase(plan, now, out cur, out next);
            liveKey = Sched.StateKey(plan, now);
            DateTime nx = Sched.NextSchoolDay(cfg, snap, DateTime.Today);
            string nf;
            string nl = Sched.LabelFor(cfg, snap, nx, out nf);
            string nextText = "Next school day: " + nx.ToString("dddd, MMM d") + (nl.Length > 0 ? "  ·  " + Sched.DayName(nl) : "") + ".";

            RingMeter ring = new RingMeter();
            ring.Width = 160; ring.Height = 160; ring.Thickness = 11; ring.BigSize = 32;
            ring.Margin = new Thickness(0, 0, 32, 0);
            ring.VerticalAlignment = VerticalAlignment.Center;
            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            heroRing = null;
            if (ph == "class")
            {
                Slot s = plan.Slots[cur];
                TimeSpan total = s.B.End - s.B.Start, left = s.B.End - now;
                double frac = total.TotalSeconds > 0 ? (total - left).TotalSeconds / total.TotalSeconds : 0;
                string big, small; CountText(left, out big, out small);
                ring.Frac = frac; ring.Fill = Theme.Acc; ring.Text = big; ring.SubText = small;
                heroRing = ring;
                Grid tag = UI.Cols(0, 0);
                Border kind = UI.Chip(s.IsClass ? "IN CLASS" : (s.Free ? "FREE PERIOD" : "NOW"), "Acc", "T1");
                ((TextBlock)kind.Child).Foreground = Brushes.White;
                UI.At(tag, kind, 0);
                string per = s.IsClass && s.R != null && s.R.Period.Length > 0 ? "Period " + s.R.Period : s.B.Label;
                TextBlock pt = UI.Text(per, 12, "T3", true);
                pt.Margin = new Thickness(10, 0, 0, 0); pt.VerticalAlignment = VerticalAlignment.Center;
                UI.At(tag, pt, 1);
                info.Children.Add(tag);
                TextBlock title = UI.Text(s.Title, 26, "T1", true);
                title.Margin = new Thickness(0, 10, 0, 2);
                info.Children.Add(title);
                string sub = "";
                if (s.R != null) { sub = s.R.Teacher ?? ""; if (!string.IsNullOrEmpty(s.R.Room)) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + s.R.Room; }
                else sub = s.B.Text();
                info.Children.Add(UI.Text(sub, 13.5, "T2", false));
                heroBar = new ContentControl();
                heroBar.Margin = new Thickness(0, 14, 0, 6);
                heroBar.Content = UI.Meter(Math.Max(0, Math.Min(1, frac)) * 100, Theme.Acc);
                info.Children.Add(heroBar);
                Grid times = UI.Cols(1, 0);
                UI.At(times, UI.Text(Bell.Clock(s.B.Start), 12, "T3", false), 0);
                heroEnd = UI.Text("Ends " + Bell.Clock(s.B.End), 12, "T2", false);
                UI.At(times, heroEnd, 1);
                info.Children.Add(times);
                string nt = next >= 0 ? "Next:  " + plan.Slots[next].Title + "  ·  " + Bell.ClockShort(plan.Slots[next].B.Start) : "Last block of the day";
                info.Children.Add(UI.Margin(UI.Text(nt, 12.5, "T2", false), 0, 8, 0, 0));
            }
            else if (ph == "between" || ph == "before")
            {
                Slot n = plan.Slots[next];
                TimeSpan left = n.B.Start - now;
                double frac = 0;
                if (ph == "between")
                {
                    TimeSpan prevEnd = TimeSpan.Zero;
                    foreach (Slot sl in plan.Slots) if (sl.B.End <= now && sl.B.End > prevEnd) prevEnd = sl.B.End;
                    double span = (n.B.Start - prevEnd).TotalSeconds;
                    frac = span > 0 ? (span - left.TotalSeconds) / span : 0;
                }
                string big, small; CountText(left, out big, out small);
                ring.Frac = frac; ring.Fill = Theme.AccText; ring.Text = big; ring.SubText = small;
                heroRing = ring;
                info.Children.Add(UI.Chip(ph == "before" ? "SCHOOL STARTS SOON" : "PASSING TIME", "AccSoft", "AccText"));
                info.Children.Add(UI.Margin(UI.Text("Up next", 12, "T3", true), 0, 12, 0, 0));
                info.Children.Add(UI.Margin(UI.Text(n.Title, 26, "T1", true), 0, 2, 0, 4));
                string sub = n.B.Text();
                if (n.R != null)
                {
                    sub = n.R.Teacher ?? "";
                    if (!string.IsNullOrEmpty(n.R.Room)) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + n.R.Room;
                    sub += "  ·  starts " + Bell.Clock(n.B.Start);
                }
                else sub = "Starts " + Bell.Clock(n.B.Start);
                info.Children.Add(UI.Text(sub, 13.5, "T2", false));
                info.Children.Add(UI.Margin(UI.Wrap(nextText, 12.5, "T3"), 0, 10, 0, 0));
            }
            else if (ph == "after")
            {
                ring.Frac = 1; ring.Fill = Theme.Good; ring.Text = "✓"; ring.BigSize = 44;
                info.Children.Add(UI.Chip("DONE FOR TODAY", "AccSoft", "AccText"));
                info.Children.Add(UI.Margin(UI.Text("School is out", 26, "T1", true), 0, 12, 0, 6));
                info.Children.Add(UI.Wrap(nextText, 13.5, "T2"));
            }
            else
            {
                ring.Frac = 0; ring.Fill = Theme.T3; ring.Text = "–"; ring.BigSize = 44;
                string head = !plan.School ? "No school today" : "Nothing scheduled";
                string det = !plan.School ? plan.WhyNot + " " + nextText : "The bell schedule for today has no periods yet.";
                info.Children.Add(UI.Text(head, 26, "T1", true));
                info.Children.Add(UI.Margin(UI.Wrap(det, 13.5, "T2"), 0, 8, 0, 0));
            }
            Grid hero = UI.Cols(0, 1, 0);
            UI.At(hero, ring, 0);
            UI.At(hero, info, 1);
            if (plan.DayLabel.Length > 0)
            {
                Border dc = UI.Chip(Sched.DayName(plan.DayLabel), "Bg4", "T1");
                dc.VerticalAlignment = VerticalAlignment.Top;
                UI.At(hero, dc, 2);
            }
            Border heroCard = UI.Card(hero, 28);
            LinearGradientBrush wash = new LinearGradientBrush(Theme.AccDeep, Theme.Bg2, 0);
            heroCard.Background = wash;
            heroCard.Margin = new Thickness(0, 0, 0, 16);

            TimeSpan nowT = DateTime.Now.TimeOfDay;
            UIElement t1 = PageKit.Tile(plan.DayLabel.Length > 0 ? plan.DayLabel : "–", null,
                plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) + (plan.LabelFrom.Length > 0 ? ", " + plan.LabelFrom : "") : "Day (turn on rotating days to see it)", null);
            UIElement t2 = PageKit.Tile(plan.Sched != null ? plan.Sched.Name : "–", null, "Bell schedule today", null);
            UIElement t3 = PageKit.Tile(Sched.ClassesLeft(plan, nowT).ToString(), null, "Classes left today", null);
            UIElement t4 = PageKit.Tile(plan.Slots.Count > 0 ? Bell.ClockShort(plan.Slots[plan.Slots.Count - 1].B.End) : "–", null, "School ends", null);

            List<UIElement> kids = new List<UIElement>();
            kids.Add(heroCard);
            kids.Add(PageKit.TileRow(CW(), t1, t2, t3, t4));
            if (plan.School && plan.Slots.Count > 0) kids.Add(Timeline(plan));
            Button setup = UI.Btn("Schedule setup", "Ghost", delegate { StartScheduleEdit(); });
            setup.HorizontalAlignment = HorizontalAlignment.Left;
            setup.Margin = new Thickness(0, 0, 0, 8);
            kids.Add(setup);
            return Body(kids.ToArray());
        }

        // today as a vertical timeline
        UIElement Timeline(DayPlan plan)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            Sched.Phase(plan, now, out cur, out next);
            StackPanel col = new StackPanel();
            TextBlock h = UI.Text("TODAY", 11.5, "T3", true);
            h.Margin = new Thickness(6, 0, 0, 8);
            col.Children.Add(h);
            for (int i = 0; i < plan.Slots.Count; i++)
            {
                Slot s = plan.Slots[i];
                bool done = s.B.End <= now, isNow = i == cur;
                Grid g = UI.Cols(0, 0, 1, 0);
                g.Margin = new Thickness(0, 4, 0, 4);
                StackPanel times = new StackPanel();
                times.Width = 84;
                TextBlock a = UI.Text(Bell.ClockShort(s.B.Start), 13.5, isNow ? "AccText" : (done ? "T3" : "T1"), true);
                a.HorizontalAlignment = HorizontalAlignment.Right;
                TextBlock b = UI.Text(Bell.ClockShort(s.B.End), 12, "T3", false);
                b.HorizontalAlignment = HorizontalAlignment.Right;
                times.Children.Add(a); times.Children.Add(b);
                UI.At(g, times, 0);
                Border bar = new Border();
                bar.Width = 4; bar.CornerRadius = new CornerRadius(2);
                bar.Margin = new Thickness(14, 2, 14, 2);
                if (isNow) bar.SetResourceReference(Border.BackgroundProperty, "Acc");
                else bar.Background = new SolidColorBrush(done ? Theme.Line2 : (s.IsClass ? Theme.Tag(s.Title) : Theme.Line2));
                UI.At(g, bar, 1);
                StackPanel mid = new StackPanel();
                mid.VerticalAlignment = VerticalAlignment.Center;
                mid.Children.Add(UI.Text(s.Title, 14.5, done || !s.IsClass ? "T3" : "T1", s.IsClass && !done));
                string sub = s.B.Label.Length > 0 && s.IsClass ? (s.R != null && s.R.Period.Length > 0 ? "Period " + s.R.Period : s.B.Label) : "";
                if (s.R != null)
                {
                    string t = s.R.Teacher ?? "";
                    if (!string.IsNullOrEmpty(s.R.Room)) t += (t.Length > 0 ? "  ·  " : "") + "Room " + s.R.Room;
                    if (t.Length > 0) sub = sub.Length > 0 ? sub + "  ·  " + t : t;
                }
                if (sub.Length > 0) mid.Children.Add(UI.Text(sub, 12, "T3", false));
                UI.At(g, mid, 2);
                if (isNow || i == next)
                {
                    Border ch = UI.Chip(isNow ? "Now" : "Next", isNow ? "Acc" : "AccSoft", isNow ? "T1" : "AccText");
                    if (isNow) ((TextBlock)ch.Child).Foreground = Brushes.White;
                    ch.VerticalAlignment = VerticalAlignment.Center;
                    UI.At(g, ch, 3);
                }
                Border row = new Border();
                row.CornerRadius = new CornerRadius(10);
                row.Padding = new Thickness(8, 4, 8, 4);
                row.Child = g;
                if (s.C != null) { Course cc = s.C; Hover(row, null, "Bg3", delegate { OpenCourse(cc); }); }
                col.Children.Add(row);
            }
            Border card = UI.Card(col, 18);
            card.Margin = new Thickness(0, 0, 0, 16);
            return card;
        }

        void LiveTick()
        {
            if (DateTime.Now.Second == 0) { try { CheckSchoolEnd(); } catch (Exception ex) { Log.Error("CheckSchoolEnd", ex); } }
            try { CheckNotify(); } catch (Exception ex) { Log.Error("CheckNotify", ex); }
            if (snap == null || pageHost == null || !IsVisible || WindowState == WindowState.Minimized) return;
            if ((view != "now" && view != "dashboard") || livePlan == null) return;
            try
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
                string key = Sched.StateKey(p, now);
                if (key != liveKey) { ShowView(false); return; }
                if (stripHost != null) stripHost.Content = StripContent(p);
                int cur, next;
                if (heroRing != null && Sched.Phase(p, now, out cur, out next) == "class")
                {
                    Slot s = p.Slots[cur];
                    TimeSpan total = s.B.End - s.B.Start, left = s.B.End - now;
                    double frac = total.TotalSeconds > 0 ? (total - left).TotalSeconds / total.TotalSeconds : 0;
                    string big, small; CountText(left, out big, out small);
                    heroRing.Frac = frac; heroRing.Text = big; heroRing.SubText = small; heroRing.InvalidateVisual();
                    if (heroBar != null) heroBar.Content = UI.Meter(Math.Max(0, Math.Min(1, frac)) * 100, Theme.Acc);
                }
                else if (heroRing != null)
                {
                    // passing time: count down to the next class
                    if (Sched.Phase(p, now, out cur, out next) != "class" && next >= 0)
                    {
                        string big, small; CountText(p.Slots[next].B.Start - now, out big, out small);
                        heroRing.Text = big; heroRing.SubText = small; heroRing.InvalidateVisual();
                    }
                }
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------ Grades
        UIElement CourseCardUi(Course c)
        {
            bool dropped = c.Dropped;
            Color gc = dropped ? Theme.T3 : Theme.GradeColor(c.Percent);
            StackPanel t = new StackPanel();
            Grid head = UI.Cols(1, 0);
            StackPanel nm = new StackPanel();
            Grid nameRow = UI.Cols(0, 0);
            TextBlock name = UI.Text(c.Name, 14.5, dropped ? "T3" : "T1", true);
            UI.At(nameRow, name, 0);
            string chip = dropped ? "" : (c.Level >= 2 ? "AP / dual" : (c.Level == 1 ? "Honors" : ""));
            if (chip.Length > 0) { Border cb = UI.Chip(chip, "AccSoft", "AccText"); cb.Margin = new Thickness(8, 0, 0, 0); UI.At(nameRow, cb, 1); }
            nm.Children.Add(nameRow);
            string meta = c.Teacher ?? "";
            if (dropped) meta = "Dropped" + (c.EndDate.HasValue ? " " + c.EndDate.Value.ToString("MMM d") : "") + (meta.Length > 0 ? "  ·  " + meta : "");
            else if (!string.IsNullOrEmpty(c.Room)) meta += (meta.Length > 0 ? "  ·  " : "") + "Room " + c.Room;
            nm.Children.Add(UI.Margin(UI.Text(meta, 12, "T2", false), 0, 3, 0, 0));
            UI.At(head, nm, 0);
            if (dropped) UI.At(head, UI.Chip("Dropped", "BadSoft", "Bad"), 1);
            else
            {
                TextBlock gr = UI.Text(Theme.GradeStr(c.Letter, c.Percent), 28, "T1", true);
                gr.Foreground = new SolidColorBrush(gc);
                gr.Margin = new Thickness(10, 0, 0, 0);
                UI.At(head, gr, 1);
            }
            t.Children.Add(head);
            t.Children.Add(UI.Margin((FrameworkElement)UI.Meter(c.Percent.HasValue ? Math.Max(2, Math.Min(100, c.Percent.Value)) : 0, dropped ? Theme.Line2 : gc), 0, 16, 0, 8));
            Grid foot = UI.Cols(1, 0);
            UI.At(foot, UI.Text((dropped ? "Last grade: " : "") + (string.IsNullOrEmpty(c.TaskName) ? "Current" : c.TaskName), 12, "T2", false), 0);
            UI.At(foot, UI.Text(c.Percent.HasValue ? Rows.Fmt(c.Percent) + "%" : "No grade yet", 12, "T2", false), 1);
            t.Children.Add(foot);
            Border card = UI.Card(t, 20);
            card.Margin = new Thickness(0, 0, 16, 16);
            Course cc = c;
            Hover(card, "Bg2", "Bg3", delegate { OpenCourse(cc); });
            return card;
        }

        UIElement CourseGrid(List<Course> list)
        {
            System.Windows.Controls.Primitives.UniformGrid ug = new System.Windows.Controls.Primitives.UniformGrid();
            ug.Columns = Math.Max(1, (CW() + 16) / (310 + 16));
            foreach (Course c in list) ug.Children.Add(CourseCardUi(c));
            ug.Margin = new Thickness(0, 0, -16, 0);
            return ug;
        }

        UIElement PageGrades()
        {
            if (snap.Courses.Count == 0) return Body(PageKit.Notice(Rows.NoData, false));
            List<UIElement> kids = new List<UIElement>();
            if (snap.Terms.Count > 1)
            {
                List<string> terms = new List<string>();
                for (int i = Math.Max(0, snap.Terms.Count - 6); i < snap.Terms.Count; i++) terms.Add(snap.Terms[i]);
                kids.Add(SegBar(terms.ToArray(), Math.Max(0, terms.IndexOf(term)), delegate(int k) { term = terms[k]; Dispatcher.BeginInvoke(new Action(delegate { ShowView(false); })); }));
            }
            List<Course> all = snap.CoursesFor(term);
            List<Course> cs = all.FindAll(delegate(Course c) { return !c.Dropped; });
            List<Course> dropped = all.FindAll(delegate(Course c) { return c.Dropped; });
            double? avg = Snapshot.Average(cs);
            List<Course> gset = cfg.CountDropped ? all : cs;
            double? gu = Rows.Gpa(gset, false), gw = Rows.Gpa(gset, true);
            Course best = null;
            foreach (Course c in cs) if (c.Percent.HasValue && (best == null || c.Percent.Value > best.Percent.Value)) best = c;
            kids.Add(PageKit.TileRow(CW(),
                PageKit.Tile(avg.HasValue ? Rows.Fmt(avg) + "%" : "–", Theme.GradeColor(avg), "Average, " + cs.Count + (cs.Count == 1 ? " class" : " classes"), null),
                PageKit.Tile(Rows.G2(gu), null, "Unweighted GPA (4.0 scale)", null),
                PageKit.Tile(Rows.G2(gw), Theme.AccText, "Weighted GPA (honors +0.5, AP +1.0)", null),
                PageKit.Tile(best != null ? Math.Round(best.Percent.Value) + "%" : "–", null, best != null ? "Highest: " + best.Name : "Highest class", null)));
            TextBlock note = UI.Wrap((cfg.CountDropped ? "Dropped classes are counted in GPA (change in Settings). " : "") + "GPA is an estimate from each class's current letter. A class counts as honors or AP from its name; open a class to change its level.", 12, "T3");
            note.Margin = new Thickness(0, -2, 0, 16);
            kids.Add(note);
            kids.Add(CourseGrid(cs));
            if (dropped.Count > 0)
            {
                kids.Add(PageKit.Section("Dropped classes"));
                kids.Add(CourseGrid(dropped));
            }
            return Body(kids.ToArray());
        }

        // ------------------------------------------------------------ Assignments
        ContentControl assignListHost;

        UIElement AssignList()
        {
            string filter = assignFilter;
            List<Assign> list = snap.Assigns.FindAll(delegate(Assign a)
            {
                if (filter == "upcoming") return Rows.IsUpcoming(a);
                if (filter == "missing") return a.Status == "missing";
                if (filter == "graded") return a.Status == "graded";
                return true;
            });
            string q = assignSearch.Trim().ToLowerInvariant();
            if (q.Length > 0) list = list.FindAll(delegate(Assign a) { return (a.Name + " " + a.Course).ToLowerInvariant().Contains(q); });
            Rows.SortAssigns(list, filter == "upcoming");
            List<ItemSpec> items = new List<ItemSpec>();
            int total = list.Count, shown = 0;
            string lastKey = null;
            foreach (Assign a in list)
            {
                if (shown >= 250) break;
                string k = a.Due.HasValue ? a.Due.Value.ToString("yyyyMMdd") : "none";
                if (k != lastKey)
                {
                    ItemSpec h = new ItemSpec();
                    h.Header = true;
                    h.Title = a.Due.HasValue ? Rows.DayLabel(a.Due.Value) : "No due date";
                    items.Add(h);
                    lastKey = k;
                }
                ItemSpec row = ForAssign(a);
                row.Strip = Theme.Tag(a.Course);
                items.Add(row);
                shown++;
            }
            string heading = total > shown ? "Showing the first " + shown + " of " + total : total + (total == 1 ? " assignment" : " assignments");
            return PageKit.ListCard(heading, "Nothing matches.", items);
        }

        UIElement PageAssignments()
        {
            pageSub = snap.Assigns.Count + " total";
            if (snap.Assigns.Count == 0) return Body(PageKit.Notice(Rows.NoData, false));
            string[] keys = new string[] { "upcoming", "missing", "graded", "all" };
            assignListHost = new ContentControl();
            UIElement seg = SuSeg(new string[] { "Upcoming", "Missing", "Graded", "All" }, Math.Max(0, Array.IndexOf(keys, assignFilter)), delegate(int k) { assignFilter = keys[k]; assignListHost.Content = AssignList(); });
            TextBox box = UI.Input("Search assignments or classes", assignSearch);
            box.Width = 300;
            box.Padding = new Thickness(12, 8, 12, 8);
            box.TextChanged += delegate { assignSearch = box.Text; assignListHost.Content = AssignList(); };
            Grid bar = UI.Cols(0, 0, 1);
            UI.At(bar, seg, 0);
            ((FrameworkElement)box).Margin = new Thickness(8, 0, 0, 0);
            UI.At(bar, box, 1);
            bar.Margin = new Thickness(0, 0, 0, 18);
            assignListHost.Content = AssignList();
            return Body(bar, assignListHost);
        }
    }
}
