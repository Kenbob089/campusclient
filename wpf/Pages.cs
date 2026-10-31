using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CampusMidnight
{
    // Pages: Home, Now, Grades, Assignments, Schedule, Diagnostics. Settings is in PagesSettings.cs.
    sealed partial class MainWindow
    {
        Course openCourse;
        string assignFilter = "upcoming";
        DateTime calMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selDate = DateTime.Today;

        // live "Now" page bits
        string liveKey = "";
        TextBlock nowCount;
        ContentControl nowBarHost;
        TimeSpan liveStart, liveEnd;
        string liveMode = "";

        void ShowView(bool animate)
        {
            if (pageHost == null) return;
            UpdateNav();
            UpdateTop();
            nowCount = null; nowBarHost = null; liveMode = "";
            UIElement page;
            if (snap == null) page = Center(loading ? "Loading your Campus data…" : (stampBad ? stampText : "Nothing loaded yet."));
            else
            {
                try
                {
                    switch (view)
                    {
                        case "now": page = PageNow(); break;
                        case "grades": page = openCourse != null ? PageCourse(openCourse) : PageGrades(); break;
                        case "assigns": page = PageAssigns(); break;
                        case "sched": page = PageSched(); break;
                        case "settings": page = PageSettings(); break;
                        case "diag": page = PageDiag(); break;
                        default: page = PageHome(); break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("page " + view, ex);
                    page = Center("Something went wrong drawing this page. The log has the details.");
                }
            }
            if (snap == null && view == "settings") { try { page = PageSettings(); } catch (Exception ex) { Log.Error("settings", ex); } }
            pageHost.Content = page;
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

        // scrolling page body, left aligned with the title above
        UIElement Body(params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(30, 10, 30, 30);
            p.MaxWidth = 1120;
            p.HorizontalAlignment = HorizontalAlignment.Left;
            foreach (UIElement k in kids) p.Children.Add(k);
            return UI.Scroll(p);
        }

        static TextBlock Heading(string s)
        {
            TextBlock t = UI.Text(s, 15, "T1", true);
            t.Margin = new Thickness(0, 0, 0, 10);
            return t;
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
                b.Child = UI.Text(Rows.Initials(current != null ? current.Name : ""), size * 0.36, "AccText", true);
                ((TextBlock)b.Child).HorizontalAlignment = HorizontalAlignment.Center;
                ((TextBlock)b.Child).VerticalAlignment = VerticalAlignment.Center;
            }
            return b;
        }

        UIElement Stat(string label, string value, string valueColor)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(0, 0, 28, 0);
            p.Children.Add(UI.Text(label, 12, "T3", true));
            TextBlock v = UI.Text(value, 26, valueColor, true);
            v.Margin = new Thickness(0, 2, 0, 0);
            p.Children.Add(v);
            return p;
        }

        UIElement ProfileCard()
        {
            List<Course> cs = TermCourses();
            double? avg = Snapshot.Average(cs);
            Grid g = UI.Cols(0, 1, 0);
            UIElement av = Avatar(72);
            ((FrameworkElement)av).Margin = new Thickness(0, 0, 18, 0);
            UI.At(g, av, 0);
            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            info.Children.Add(UI.Text(current != null ? current.Name : "Student", 22, "T1", true));
            string line = current != null ? current.Line() : "";
            if (line.Length > 0) info.Children.Add(UI.Margin(UI.Text(line, 13, "T3", false), 0, 3, 0, 0));
            UI.At(g, info, 1);
            StackPanel stats = new StackPanel();
            stats.Orientation = Orientation.Horizontal;
            stats.VerticalAlignment = VerticalAlignment.Center;
            stats.Children.Add(Stat("GPA", Rows.G2(Rows.Gpa(cs, false)), "T1"));
            stats.Children.Add(Stat("Weighted", Rows.G2(Rows.Gpa(cs, true)), "T1"));
            stats.Children.Add(Stat("Average", avg.HasValue ? avg.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "–", "AccText"));
            UI.At(g, stats, 2);
            return UI.Card(g, 22);
        }

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
            if (live) row.SetResourceReference(Border.BackgroundProperty, "AccDeep");
            row.Child = g;
            return row;
        }

        UIElement AssignRow(Assign a, bool showCourse)
        {
            Grid g = UI.Cols(0, 1, 0);
            g.Margin = new Thickness(0, 4, 0, 4);
            string color = a.Status == "missing" ? "Bad" : (a.Status == "upcoming" ? "AccText" : (a.Status == "graded" ? "Good" : "T3"));
            Border bar = UI.Bar(color);
            bar.Margin = new Thickness(0, 2, 12, 2);
            UI.At(g, bar, 0);
            StackPanel mid = new StackPanel();
            mid.Children.Add(UI.Text(a.Name, 14, "T1", true));
            string sub = showCourse ? a.Course : "";
            if (a.Due.HasValue) sub += (sub.Length > 0 ? " · " : "") + Rows.DayLabel(a.Due.Value);
            if (sub.Length > 0) mid.Children.Add(UI.Text(sub, 12, "T3", false));
            UI.At(g, mid, 1);
            StackPanel right = new StackPanel();
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Center;
            if (a.Status == "graded" && a.Score.HasValue)
            {
                TextBlock sc = UI.Text(Rows.Fmt(a.Score) + (a.Total.HasValue ? " / " + Rows.Fmt(a.Total) : ""), 14, "T1", true);
                sc.HorizontalAlignment = HorizontalAlignment.Right;
                right.Children.Add(sc);
                if (a.Percent.HasValue)
                {
                    TextBlock pc = UI.Text(Math.Round(a.Percent.Value) + "%", 12, "T3", false);
                    pc.HorizontalAlignment = HorizontalAlignment.Right;
                    right.Children.Add(pc);
                }
            }
            else
            {
                string badge = a.Status == "missing" ? "Missing" : (a.Status == "upcoming" ? "Upcoming" : (a.Status == "submitted" ? "Turned in" : (a.Status == "exempt" ? "Exempt" : "Not graded")));
                string bg = a.Status == "missing" ? "BadSoft" : (a.Status == "submitted" || a.Status == "upcoming" ? "AccSoft" : "Bg4");
                string fg = a.Status == "missing" ? "Bad" : (a.Status == "submitted" || a.Status == "upcoming" ? "AccText" : "T2");
                Border chip = UI.Chip(badge, bg, fg);
                chip.HorizontalAlignment = HorizontalAlignment.Right;
                right.Children.Add(chip);
            }
            if (a.Late) { Border l = UI.Chip("Late", "WarnSoft", "Warn"); l.HorizontalAlignment = HorizontalAlignment.Right; l.Margin = new Thickness(0, 4, 0, 0); right.Children.Add(l); }
            UI.At(g, right, 2);
            return g;
        }

        static UIElement Sep()
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
            b.SetResourceReference(Border.BackgroundProperty, rest);
            b.MouseEnter += delegate { b.SetResourceReference(Border.BackgroundProperty, over); };
            b.MouseLeave += delegate { b.SetResourceReference(Border.BackgroundProperty, rest); };
            b.MouseLeftButtonUp += delegate { click(); };
        }

        // ------------------------------------------------------------ Home
        UIElement PageHome()
        {
            DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
            StackPanel today = new StackPanel();
            today.Children.Add(Heading("Today"));
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            Sched.Phase(p, now, out cur, out next);
            if (!p.School) today.Children.Add(UI.Wrap(p.WhyNot.Length > 0 ? p.WhyNot : "No school today.", 13.5, "T3"));
            else if (p.Slots.Count == 0) today.Children.Add(UI.Wrap("No schedule is set up for today yet.", 13.5, "T3"));
            else
            {
                if (p.DayLabel.Length > 0) { Border c = UI.Chip(Sched.DayName(p.DayLabel), "AccSoft", "AccText"); c.Margin = new Thickness(0, 0, 0, 8); today.Children.Add(c); }
                for (int i = 0; i < p.Slots.Count; i++) today.Children.Add(SlotRow(p.Slots[i], i == cur));
            }

            StackPanel up = new StackPanel();
            up.Children.Add(Heading("Coming up"));
            List<Assign> list = snap.Assigns.FindAll(Rows.IsUpcoming);
            Rows.SortAssigns(list, true);
            if (list.Count == 0) up.Children.Add(UI.Wrap("Nothing due soon.", 13.5, "T3"));
            for (int i = 0; i < list.Count && i < 7; i++)
            {
                if (i > 0) up.Children.Add(Sep());
                up.Children.Add(AssignRow(list[i], true));
            }
            List<Assign> miss = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "missing" && !a.InDropped; });
            if (miss.Count > 0)
            {
                Border chip = UI.Chip(miss.Count + " missing", "BadSoft", "Bad");
                chip.Margin = new Thickness(0, 12, 0, 0);
                up.Children.Add(chip);
            }

            Grid two = UI.Cols(1, 1);
            Border cardA = UI.Card(today, 20); cardA.Margin = new Thickness(0, 0, 8, 0);
            Border cardB = UI.Card(up, 20); cardB.Margin = new Thickness(8, 0, 0, 0);
            UI.At(two, cardA, 0);
            UI.At(two, cardB, 1);
            two.Margin = new Thickness(0, 16, 0, 0);
            return Body(ProfileCard(), two);
        }

        // ------------------------------------------------------------ Now
        UIElement PageNow()
        {
            DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            string ph = Sched.Phase(p, now, out cur, out next);
            liveKey = Sched.StateKey(p, now);

            StackPanel hero = new StackPanel();
            if (ph == "none")
            {
                hero.Children.Add(UI.Text(p.School ? "No schedule yet" : "No school today", 26, "T1", true));
                hero.Children.Add(UI.Margin(UI.Wrap(p.School ? "Your classes will show here once a schedule is set up." : (p.WhyNot.Length > 0 ? p.WhyNot : "Enjoy the day off."), 14, "T3"), 0, 6, 0, 0));
            }
            else if (ph == "after")
            {
                hero.Children.Add(UI.Text("School is done for today", 26, "T1", true));
                hero.Children.Add(UI.Margin(UI.Text("Nice work. Tomorrow: " + Rows.DayLabel(Sched.NextSchoolDay(cfg, snap, DateTime.Today)), 14, "T3", false), 0, 6, 0, 0));
            }
            else
            {
                bool inClass = ph == "class";
                Slot s = p.Slots[inClass ? cur : next];
                hero.Children.Add(UI.Text(inClass ? "NOW" : (ph == "before" ? "SCHOOL STARTS SOON" : "UP NEXT"), 12, "AccText", true));
                TextBlock t = UI.Text(s.Title, 30, "T1", true);
                t.Margin = new Thickness(0, 4, 0, 2);
                hero.Children.Add(t);
                string sub = Bell.Clock(s.B.Start) + " – " + Bell.Clock(s.B.End);
                if (s.R != null && !string.IsNullOrEmpty(s.R.Room)) sub += " · Room " + s.R.Room;
                hero.Children.Add(UI.Text(sub, 14, "T3", false));
                liveMode = inClass ? "class" : "wait";
                liveStart = inClass ? s.B.Start : now;
                liveEnd = inClass ? s.B.End : s.B.Start;
                nowCount = UI.Text(Sched.Countdown(liveEnd - now), 52, "T1", true);
                nowCount.Margin = new Thickness(0, 16, 0, 0);
                hero.Children.Add(nowCount);
                hero.Children.Add(UI.Text(inClass ? "until this class ends" : "until it starts", 13, "T3", false));
                nowBarHost = new ContentControl();
                nowBarHost.Margin = new Thickness(0, 16, 0, 0);
                nowBarHost.Content = inClass ? UI.Meter(Progress(now), Theme.Acc) : null;
                hero.Children.Add(nowBarHost);
            }
            Border heroCard = UI.Card(hero, 28);
            heroCard.SetResourceReference(Border.BackgroundProperty, "AccDeep");

            StackPanel rest = new StackPanel();
            rest.Children.Add(Heading("The rest of today"));
            if (p.Slots.Count == 0) rest.Children.Add(UI.Text("Nothing scheduled.", 13.5, "T3", false));
            for (int i = 0; i < p.Slots.Count; i++) rest.Children.Add(SlotRow(p.Slots[i], i == cur));
            Border restCard = UI.Card(rest, 20);
            restCard.Margin = new Thickness(0, 16, 0, 0);
            return Body(heroCard, restCard);
        }

        double Progress(TimeSpan now)
        {
            double total = (liveEnd - liveStart).TotalSeconds;
            if (total <= 0) return 0;
            return Math.Max(0, Math.Min(100, (now - liveStart).TotalSeconds / total * 100));
        }

        void LiveTick()
        {
            if (snap == null || view != "now" || pageHost == null) return;
            try
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
                string key = Sched.StateKey(p, now);
                if (key != liveKey) { ShowView(false); return; }
                if (nowCount != null)
                {
                    nowCount.Text = Sched.Countdown(liveEnd - now);
                    if (nowBarHost != null && liveMode == "class") nowBarHost.Content = UI.Meter(Progress(now), Theme.Acc);
                }
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------ Grades
        UIElement PageGrades()
        {
            List<Course> cs = TermCourses();
            WrapPanel wrap = new WrapPanel();
            foreach (Course c in cs)
            {
                Course cc = c;
                Color gc = Theme.GradeColor(c.Percent);
                StackPanel t = new StackPanel();
                t.Width = 300;
                Grid head = UI.Cols(0, 1, 0);
                Border bar = new Border();
                bar.Width = 4; bar.CornerRadius = new CornerRadius(2);
                bar.Background = new SolidColorBrush(Theme.Tag(c.Name));
                bar.Margin = new Thickness(0, 2, 12, 2);
                UI.At(head, bar, 0);
                StackPanel nm = new StackPanel();
                nm.Children.Add(UI.Text(c.Name, 14.5, "T1", true));
                string sub = c.Teacher ?? "";
                if (c.Dropped) sub = "Dropped" + (sub.Length > 0 ? " · " + sub : "");
                nm.Children.Add(UI.Text(sub, 12, "T3", false));
                UI.At(head, nm, 1);
                TextBlock gr = UI.Text(Theme.GradeStr(c.Letter, c.Percent), 28, "T1", true);
                gr.Foreground = new SolidColorBrush(gc);
                gr.Margin = new Thickness(10, 0, 0, 0);
                UI.At(head, gr, 2);
                t.Children.Add(head);
                TextBlock pc = UI.Text(c.Percent.HasValue ? Math.Round(c.Percent.Value, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "No grade yet", 12.5, "T3", false);
                pc.Margin = new Thickness(0, 12, 0, 6);
                t.Children.Add(pc);
                t.Children.Add(UI.Meter(c.Percent.HasValue ? c.Percent.Value : 0, gc));
                Border card = UI.Card(t, 18);
                card.Margin = new Thickness(0, 0, 14, 14);
                Hover(card, "Bg2", "Bg3", delegate { openCourse = cc; ShowView(true); });
                wrap.Children.Add(card);
            }
            if (cs.Count == 0) return Body(UI.Wrap(Rows.NoData, 13.5, "T3"));
            return Body(ProfileCard(), UI.Margin(wrap, 0, 16, 0, 0));
        }

        UIElement PageCourse(Course c)
        {
            Button back = UI.Btn("←  All classes", "Ghost", delegate { openCourse = null; ShowView(true); });
            back.HorizontalAlignment = HorizontalAlignment.Left;
            back.Margin = new Thickness(0, 0, 0, 14);
            Color gc = Theme.GradeColor(c.Percent);
            Grid head = UI.Cols(1, 0);
            StackPanel nm = new StackPanel();
            nm.Children.Add(UI.Text(c.Name, 22, "T1", true));
            string sub = c.Teacher ?? "";
            if (!string.IsNullOrEmpty(c.Room)) sub += (sub.Length > 0 ? " · " : "") + "Room " + c.Room;
            if (sub.Length > 0) nm.Children.Add(UI.Margin(UI.Text(sub, 13, "T3", false), 0, 3, 0, 0));
            nm.Children.Add(UI.Margin(UI.Chip(Rows.LevelName(c.Level), "AccSoft", "AccText"), 0, 10, 0, 0));
            UI.At(head, nm, 0);
            StackPanel gcol = new StackPanel();
            TextBlock gl = UI.Text(Theme.GradeStr(c.Letter, c.Percent), 40, "T1", true);
            gl.Foreground = new SolidColorBrush(gc);
            gl.HorizontalAlignment = HorizontalAlignment.Right;
            gcol.Children.Add(gl);
            TextBlock gp = UI.Text(c.Percent.HasValue ? Math.Round(c.Percent.Value, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "", 13, "T3", false);
            gp.HorizontalAlignment = HorizontalAlignment.Right;
            gcol.Children.Add(gp);
            UI.At(head, gcol, 1);
            Border headCard = UI.Card(head, 22);

            List<UIElement> parts = new List<UIElement>();
            parts.Add(back);
            parts.Add(headCard);
            if (c.Tasks.Count > 0)
            {
                StackPanel tp = new StackPanel();
                tp.Children.Add(Heading("Grading categories"));
                foreach (TaskGrade tg in c.Tasks)
                {
                    Grid r = UI.Cols(1, 0, 0);
                    r.Margin = new Thickness(0, 3, 0, 3);
                    UI.At(r, UI.Text(tg.Name, 13.5, "T2", false), 0);
                    TextBlock pp = UI.Text(tg.Percent.HasValue ? Math.Round(tg.Percent.Value, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "", 13, "T3", false);
                    pp.Margin = new Thickness(0, 0, 14, 0);
                    UI.At(r, pp, 1);
                    UI.At(r, UI.Text(Theme.GradeStr(tg.Letter, tg.Percent), 13.5, "T1", true), 2);
                    tp.Children.Add(r);
                }
                Border tc = UI.Card(tp, 20);
                tc.Margin = new Thickness(0, 16, 0, 0);
                parts.Add(tc);
            }
            StackPanel ap = new StackPanel();
            ap.Children.Add(Heading("Assignments"));
            List<Assign> list = Rows.AssignsFor(snap, c);
            Rows.SortAssigns(list, false);
            if (list.Count == 0) ap.Children.Add(UI.Text("No assignments came back for this class.", 13.5, "T3", false));
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) ap.Children.Add(Sep());
                ap.Children.Add(AssignRow(list[i], false));
            }
            Border ac = UI.Card(ap, 20);
            ac.Margin = new Thickness(0, 16, 0, 0);
            parts.Add(ac);
            return Body(parts.ToArray());
        }

        // ------------------------------------------------------------ Assignments
        UIElement PageAssigns()
        {
            string[][] filters = new string[][] { new string[] { "upcoming", "Upcoming" }, new string[] { "missing", "Missing" }, new string[] { "graded", "Graded" }, new string[] { "all", "All" } };
            StackPanel bar = new StackPanel();
            bar.Orientation = Orientation.Horizontal;
            bar.Margin = new Thickness(0, 0, 0, 14);
            foreach (string[] f in filters)
            {
                string key = f[0];
                Button b = UI.Btn(f[1], key == assignFilter ? "Pill" : "Ghost", delegate { assignFilter = key; ShowView(false); });
                b.Margin = new Thickness(0, 0, 8, 0);
                bar.Children.Add(b);
            }
            List<Assign> list = snap.Assigns.FindAll(delegate(Assign a)
            {
                if (a.InDropped) return false;
                if (assignFilter == "upcoming") return Rows.IsUpcoming(a);
                if (assignFilter == "missing") return a.Status == "missing";
                if (assignFilter == "graded") return a.Status == "graded";
                return true;
            });
            Rows.SortAssigns(list, assignFilter == "upcoming");
            StackPanel col = new StackPanel();
            if (list.Count == 0) col.Children.Add(UI.Text("Nothing here.", 13.5, "T3", false));
            string lastDay = null;
            for (int i = 0; i < list.Count && i < 200; i++)
            {
                Assign a = list[i];
                string day = a.Due.HasValue ? Rows.DayLabel(a.Due.Value) : "No due date";
                if (day != lastDay)
                {
                    TextBlock h = UI.Text(day, 12.5, "AccText", true);
                    h.Margin = new Thickness(0, lastDay == null ? 0 : 14, 0, 4);
                    col.Children.Add(h);
                    lastDay = day;
                }
                else col.Children.Add(Sep());
                col.Children.Add(AssignRow(a, true));
            }
            return Body(bar, UI.Card(col, 20));
        }

        // ------------------------------------------------------------ Schedule
        string DayTypeText(DateTime d)
        {
            string t;
            if (!cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out t)) return "Automatic";
            return t == "none" ? "No school" : t;
        }

        void SetDayType(DateTime d, string type)
        {
            string key = d.ToString("yyyy-MM-dd");
            if (type == null) cfg.DayTypes.Remove(key); else cfg.DayTypes[key] = type;
            cfg.Save();
            ShowView(false);
        }

        UIElement PageSched()
        {
            // calendar
            StackPanel cal = new StackPanel();
            cal.Width = 340;
            Grid nav = UI.Cols(1, 0, 0);
            nav.Margin = new Thickness(0, 0, 0, 10);
            UI.At(nav, UI.Text(calMonth.ToString("MMMM yyyy"), 16, "T1", true), 0);
            Button prev = UI.IconBtn("", 12, delegate { calMonth = calMonth.AddMonths(-1); ShowView(false); });
            Button nxt = UI.IconBtn("", 12, delegate { calMonth = calMonth.AddMonths(1); ShowView(false); });
            UI.At(nav, prev, 1);
            UI.At(nav, nxt, 2);
            cal.Children.Add(nav);

            System.Windows.Controls.Primitives.UniformGrid heads = new System.Windows.Controls.Primitives.UniformGrid();
            heads.Columns = 7;
            foreach (string d in new string[] { "S", "M", "T", "W", "T", "F", "S" })
            {
                TextBlock t = UI.Text(d, 11.5, "T3", true);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 0, 0, 6);
                heads.Children.Add(t);
            }
            cal.Children.Add(heads);

            System.Windows.Controls.Primitives.UniformGrid days = new System.Windows.Controls.Primitives.UniformGrid();
            days.Columns = 7;
            int lead = (int)calMonth.DayOfWeek;
            DateTime start = calMonth.AddDays(-lead);
            int count = ((lead + DateTime.DaysInMonth(calMonth.Year, calMonth.Month) + 6) / 7) * 7;
            for (int i = 0; i < count; i++)
            {
                DateTime d = start.AddDays(i);
                bool inMonth = d.Month == calMonth.Month;
                bool school = Sched.IsSchoolDay(cfg, snap, d);
                string label = "";
                if (school && inMonth) { DayPlan dp = Sched.Plan(cfg, snap, term, d); label = dp.DayLabel; if (label.Length > 3) label = label.Substring(0, 1); }
                bool sel = d.Date == selDate.Date;
                bool today = d.Date == DateTime.Today;
                StackPanel cell = new StackPanel();
                cell.HorizontalAlignment = HorizontalAlignment.Center;
                cell.VerticalAlignment = VerticalAlignment.Center;
                TextBlock num = UI.Text(d.Day.ToString(), 13, sel ? "T1" : (today ? "AccText" : (inMonth && school ? "T1" : "T3")), sel || today);
                num.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(num);
                TextBlock lb = UI.Text(label.Length > 0 ? label : " ", 10.5, "AccText", true);
                lb.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(lb);
                Border b = new Border();
                b.Height = 46;
                b.Margin = new Thickness(1);
                b.CornerRadius = new CornerRadius(9);
                b.Child = cell;
                b.Opacity = inMonth ? 1 : 0.35;
                DateTime dd = d;
                Hover(b, sel ? "AccSoft" : "Bg2", sel ? "AccSoft" : "Bg3", delegate { selDate = dd; if (dd.Month != calMonth.Month) calMonth = new DateTime(dd.Year, dd.Month, 1); ShowView(false); });
                if (today && !sel) { b.BorderThickness = new Thickness(1); b.SetResourceReference(Border.BorderBrushProperty, "Acc"); }
                days.Children.Add(b);
            }
            cal.Children.Add(days);
            Button todayBtn = UI.Btn("Today", "Ghost", delegate { selDate = DateTime.Today; calMonth = new DateTime(selDate.Year, selDate.Month, 1); ShowView(false); });
            todayBtn.HorizontalAlignment = HorizontalAlignment.Left;
            todayBtn.Margin = new Thickness(0, 12, 0, 0);
            cal.Children.Add(todayBtn);
            Border calCard = UI.Card(cal, 18);
            calCard.VerticalAlignment = VerticalAlignment.Top;
            calCard.Margin = new Thickness(0, 0, 16, 0);

            // selected day
            DayPlan sp = Sched.Plan(cfg, snap, term, selDate);
            StackPanel day = new StackPanel();
            Grid dh = UI.Cols(1, 0);
            StackPanel dt = new StackPanel();
            dt.Children.Add(UI.Text(selDate.ToString("dddd, MMMM d"), 18, "T1", true));
            if (sp.School && sp.DayLabel.Length > 0) dt.Children.Add(UI.Margin(UI.Text(Sched.DayName(sp.DayLabel) + (sp.Sched != null ? " · " + sp.Sched.Name : ""), 13, "T3", false), 0, 3, 0, 0));
            UI.At(dh, dt, 0);
            Button dd2 = UI.Btn(DayTypeText(selDate) + "  ▾", "Ghost", null);
            dd2.Click += delegate
            {
                List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
                DateTime sd = selDate;
                items.Add(new KeyValuePair<string, RoutedEventHandler>("Automatic", delegate { SetDayType(sd, null); }));
                items.Add(new KeyValuePair<string, RoutedEventHandler>("No school", delegate { SetDayType(sd, "none"); }));
                items.Add(new KeyValuePair<string, RoutedEventHandler>("Regular times", delegate { SetDayType(sd, "Regular"); }));
                foreach (string vn in new List<string>(cfg.Variants.Keys))
                {
                    string name = vn;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>(name, delegate { SetDayType(sd, name); }));
                }
                UI.OpenMenu(dd2, UI.Menu(items.ToArray()));
            };
            UI.At(dh, dd2, 1);
            day.Children.Add(dh);
            day.Children.Add(UI.Margin(Sep(), 0, 12, 0, 8));
            if (!sp.School) day.Children.Add(UI.Wrap(sp.WhyNot.Length > 0 ? sp.WhyNot : "No school.", 13.5, "T3"));
            else if (sp.Slots.Count == 0) day.Children.Add(UI.Wrap("No schedule is set up for this day yet.", 13.5, "T3"));
            else
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                int cur = -1, nx;
                if (selDate.Date == DateTime.Today) Sched.Phase(sp, now, out cur, out nx);
                for (int i = 0; i < sp.Slots.Count; i++) day.Children.Add(SlotRow(sp.Slots[i], i == cur));
            }
            Border dayCard = UI.Card(day, 20);

            Grid layout = UI.Cols(0, 1);
            UI.At(layout, calCard, 0);
            UI.At(layout, dayCard, 1);
            return Body(layout, UI.Margin(UI.Wrap("Changing times, rotation, or classes is done in the WinForms app’s setup for now. This build reads the same saved schedule.", 12.5, "T3"), 0, 14, 0, 0));
        }

        // ------------------------------------------------------------ Diagnostics
        UIElement PageDiag()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Campus Midnight diagnostics (WPF)");
            sb.AppendLine("courses=" + snap.Courses.Count + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " calendarDays=" + snap.CalDays.Count);
            if (!string.IsNullOrEmpty(snap.CalStats)) sb.AppendLine(snap.CalStats);
            StackPanel col = new StackPanel();
            col.Children.Add(Heading("What Campus returned"));
            foreach (DiagRow r in snap.Diag)
            {
                string line = r.Key + "  " + r.Path + "  status " + r.Status + "  records " + r.Records + (r.Ok ? "" : "  " + r.Error);
                sb.AppendLine(line);
                Grid g = UI.Cols(0, 1, 0);
                g.Margin = new Thickness(0, 4, 0, 4);
                Border dot = UI.Dot(r.Ok ? Theme.Good : Theme.Bad, 9);
                dot.Margin = new Thickness(0, 0, 12, 0);
                UI.At(g, dot, 0);
                StackPanel m = new StackPanel();
                m.Children.Add(UI.Text(r.Key, 13.5, "T1", true));
                m.Children.Add(UI.Text(r.Path, 11.5, "T3", false));
                UI.At(g, m, 1);
                UI.At(g, UI.Text(r.Ok ? r.Records + " records" : (r.Status > 0 ? "HTTP " + r.Status : "failed"), 12.5, r.Ok ? "T2" : "Bad", false), 2);
                col.Children.Add(g);
            }
            if (snap.Diag.Count == 0) col.Children.Add(UI.Text("No requests recorded yet.", 13.5, "T3", false));
            Button copy = UI.Btn("Copy report", "Ghost", delegate { try { Clipboard.SetText(sb.ToString()); stampText = "Report copied"; UpdateTop(); } catch (Exception) { } });
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            copy.Margin = new Thickness(0, 0, 0, 14);
            return Body(copy, UI.Card(col, 20));
        }
    }
}
