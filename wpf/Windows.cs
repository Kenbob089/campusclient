using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CampusMidnight
{
    // class and assignment details open in their own window, like the WinForms app
    sealed partial class MainWindow
    {
        Window NewWindow(string title, UIElement content, double w, double h)
        {
            Window win = new Window();
            win.Title = title;
            win.Owner = this;
            Rect wa = SystemParameters.WorkArea;
            double z = cfg.Zoom;
            win.Width = Math.Min(w * z, wa.Width - 60); win.Height = Math.Min(h * z, wa.Height - 60);
            win.MinWidth = Math.Min(460 * z, wa.Width - 60); win.MinHeight = Math.Min(420 * z, wa.Height - 60);
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.FontFamily = new FontFamily("Segoe UI");
            win.UseLayoutRounding = true;
            win.SetResourceReference(Window.BackgroundProperty, "Bg0");
            try { win.Icon = Icon; } catch (Exception) { }
            win.SourceInitialized += delegate
            {
                try
                {
                    ChromeColors(new WindowInteropHelper(win).Handle);
                }
                catch (Exception) { }
            };
            win.Opacity = 0;
            win.Loaded += delegate { win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); };
            win.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) win.Close(); };
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(28, 24, 28, 28);
            p.LayoutTransform = new ScaleTransform(z, z);   // these windows follow the app zoom too
            p.Children.Add(content);
            win.Content = UI.Scroll(p);
            return win;
        }

        UIElement KV(string k, string v)
        {
            Grid g = UI.Cols(0, 1);
            g.Margin = new Thickness(0, 7, 0, 7);
            TextBlock a = UI.Text(k, 13.5, "T3", false);
            a.Width = 120;
            UI.At(g, a, 0);
            TextBlock b = UI.Wrap(v, 13.5, "T1");
            UI.At(g, b, 1);
            return g;
        }

        UIElement TextCardUi(string heading, string body, bool muted)
        {
            StackPanel p = new StackPanel();
            if (heading.Length > 0) p.Children.Add(Heading(heading));
            p.Children.Add(UI.Wrap(body, 13.5, muted ? "T3" : "T1"));
            Border b = UI.Card(p, 20);
            b.Margin = new Thickness(0, 0, 0, 14);
            return b;
        }

        void OpenSite()
        {
            OpenCampusWindow("");
        }

        void OpenSiteExternal()
        {
            string url = api != null ? api.LoginUrl : cfg.Url;
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
        }

        Course FindCourse(string name)
        {
            if (snap == null || string.IsNullOrEmpty(name)) return null;
            string key = Norm.Key(name);
            foreach (Course c in snap.CoursesFor(term)) if (Norm.Key(c.Name) == key) return c;
            foreach (Course c in snap.Courses) if (Norm.Key(c.Name) == key) return c;
            return null;
        }

        // ------------------------------------------------------------ assignment
        void OpenAssign(Assign a)
        {
            Course c = FindCourse(a.Course);
            StackPanel p = new StackPanel();
            Grid top = UI.Cols(0, 1);
            Border strip = new Border();
            strip.Width = 5; strip.Height = 18; strip.CornerRadius = new CornerRadius(2);
            strip.Background = new SolidColorBrush(Theme.Tag(a.Course));
            strip.Margin = new Thickness(0, 0, 10, 0);
            UI.At(top, strip, 0);
            UI.At(top, UI.Text(a.Course, 14, "T2", true), 1);
            p.Children.Add(top);
            TextBlock title = UI.Wrap(a.Name, 22, "T1");
            title.FontFamily = new FontFamily("Segoe UI Semibold");
            title.Margin = new Thickness(0, 8, 0, 14);
            p.Children.Add(title);

            // score card
            StackPanel sc = new StackPanel();
            if (a.Status == "graded" && a.Score.HasValue)
            {
                Grid sg = UI.Cols(1, 0);
                StackPanel l = new StackPanel();
                l.Children.Add(UI.Text("SCORE", 11.5, "T3", true));
                TextBlock big = UI.Text(Rows.Fmt(a.Score) + (a.Total.HasValue ? " / " + Rows.Fmt(a.Total) : ""), 30, "T1", true);
                big.Margin = new Thickness(0, 2, 0, 0);
                l.Children.Add(big);
                UI.At(sg, l, 0);
                if (a.Percent.HasValue)
                {
                    TextBlock pc = UI.Text(Math.Round(a.Percent.Value, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%", 30, "T1", true);
                    pc.Foreground = new SolidColorBrush(Theme.GradeColor(a.Percent));
                    pc.VerticalAlignment = VerticalAlignment.Bottom;
                    UI.At(sg, pc, 1);
                }
                sc.Children.Add(sg);
                if (a.Percent.HasValue) sc.Children.Add(UI.Margin((FrameworkElement)UI.Meter(a.Percent.Value, Theme.GradeColor(a.Percent)), 0, 14, 0, 0));
            }
            else
            {
                string st = a.Status == "missing" ? "Missing" : (a.Status == "upcoming" ? "Upcoming" : (a.Status == "submitted" ? "Turned in" : (a.Status == "exempt" ? "Exempt" : "Not graded yet")));
                sc.Children.Add(UI.Text("STATUS", 11.5, "T3", true));
                TextBlock sv = UI.Text(st, 24, a.Status == "missing" ? "Bad" : "T1", true);
                sv.Margin = new Thickness(0, 2, 0, 0);
                sc.Children.Add(sv);
                if (a.Total.HasValue) sc.Children.Add(UI.Margin(UI.Text("Worth " + Rows.Fmt(a.Total) + " points", 13, "T3", false), 0, 4, 0, 0));
            }
            Border scb = UI.Card(sc, 22);
            scb.Margin = new Thickness(0, 0, 0, 14);
            p.Children.Add(scb);

            // details
            StackPanel d = new StackPanel();
            d.Children.Add(Heading("Details"));
            d.Children.Add(KV("Due", a.Due.HasValue ? a.Due.Value.ToString("dddd, MMMM d, yyyy") : "No due date"));
            if (a.Assigned.HasValue) d.Children.Add(KV("Assigned", a.Assigned.Value.ToString("dddd, MMMM d, yyyy")));
            if (a.Category.Length > 0) d.Children.Add(KV("Category", a.Category));
            if (a.Scoring.Length > 0) d.Children.Add(KV("Scoring", a.Scoring));
            if (a.Multiplier.HasValue && Math.Abs(a.Multiplier.Value - 1) > 0.001) d.Children.Add(KV("Weight", "×" + Rows.Fmt(a.Multiplier)));
            if (a.Term.Length > 0) d.Children.Add(KV("Term", a.Term));
            if (c != null && !string.IsNullOrEmpty(c.Teacher)) d.Children.Add(KV("Teacher", c.Teacher));
            List<string> flags = new List<string>();
            if (a.Late) flags.Add("Late");
            if (a.Missing) flags.Add("Missing");
            if (a.TurnedIn) flags.Add("Turned in");
            if (a.Dropped) flags.Add("Dropped from grade");
            if (a.Incomplete) flags.Add("Incomplete");
            if (a.Cheated) flags.Add("Cheating flag");
            if (a.HasUpload) flags.Add("Accepts uploads");
            if (flags.Count > 0) d.Children.Add(KV("Flags", string.Join(", ", flags.ToArray())));
            Border db = UI.Card(d, 20);
            db.Margin = new Thickness(0, 0, 0, 14);
            p.Children.Add(db);

            p.Children.Add(TextCardUi("Teacher comments", a.Comments.Length > 0 ? a.Comments : "No comments on this assignment.", a.Comments.Length == 0));
            if (a.Feedback.Length > 0) p.Children.Add(TextCardUi("Feedback", a.Feedback, false));
            if (a.Description.Length > 0) p.Children.Add(TextCardUi("Description", a.Description, false));

            Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusWindow("assignments"); });
            Button copy = UI.Btn("Copy summary", "Ghost", null);
            copy.Click += delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(a.Name + " (" + a.Course + ")");
                    if (a.Due.HasValue) sb.AppendLine("Due: " + a.Due.Value.ToString("yyyy-MM-dd"));
                    if (a.Score.HasValue) sb.AppendLine("Score: " + Rows.Fmt(a.Score) + " / " + Rows.Fmt(a.Total) + (a.Percent.HasValue ? " (" + Rows.Fmt(a.Percent) + "%)" : ""));
                    if (a.Comments.Length > 0) sb.AppendLine("Comments: " + a.Comments);
                    Clipboard.SetText(sb.ToString());
                    copy.Content = "Copied";
                }
                catch (Exception) { copy.Content = "Could not copy"; }
            };
            StackPanel btns = UI.Row(open, copy);
            copy.Margin = new Thickness(8, 0, 0, 0);
            p.Children.Add(btns);
            NewWindow(a.Name, p, 640, 780).ShowDialog();
        }

        // ------------------------------------------------------------ class
        // Campus tags an assignment with a term id, not a name, so fall back to the term's dates
        bool AssignInTerm(Assign a, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            if (!string.IsNullOrEmpty(a.Term)) return string.Equals(a.Term, term, StringComparison.OrdinalIgnoreCase);
            DateTime[] r;
            if (snap == null || !snap.TermDates.TryGetValue(term, out r)) return true;
            DateTime? when = a.Due.HasValue ? a.Due : a.Assigned;
            if (!when.HasValue) return false;
            return when.Value.Date >= r[0].Date && when.Value.Date <= r[1].Date;
        }

        // how this class counts in the weighted GPA
        StackPanel GpaLevelPicker(Course c)
        {
            StackPanel g = new StackPanel();
            if (c.Dropped)
            {
                g.Children.Add(UI.Wrap("This class was dropped, so it is left out of your GPA.", 13.5, "T2"));
                return g;
            }
            g.Children.Add(UI.Text("COUNTS IN WEIGHTED GPA AS", 11.5, "T3", true));
            StackPanel seg = new StackPanel();
            seg.Orientation = Orientation.Horizontal;
            seg.Margin = new Thickness(0, 8, 0, 0);
            string[] names = new string[] { "Regular", "Honors (+0.5)", "AP / dual (+1.0)" };
            List<Button> btns = new List<Button>();
            for (int i = 0; i < 3; i++)
            {
                int lv = i;
                Button b = UI.Btn(names[i], c.Level == i ? "Pill" : "Ghost", null);
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Click += delegate
                {
                    c.Level = lv;
                    cfg.Levels[Settings.Slug(c.Name)] = lv;
                    cfg.Save();
                    for (int k = 0; k < btns.Count; k++) btns[k].Style = (Style)Application.Current.Resources[k == lv ? "Pill" : "Ghost"];
                };
                btns.Add(b);
                seg.Children.Add(b);
            }
            g.Children.Add(seg);
            return g;
        }

        void OpenCourse(Course c)
        {
            if (c == null) return;
            List<Assign> allList = Rows.AssignsFor(snap, c);
            string shownTerm = !string.IsNullOrEmpty(c.Term) ? c.Term : snap.CurrentTerm();
            List<Assign> inTerm = allList.FindAll(delegate(Assign x) { return AssignInTerm(x, shownTerm); });
            bool filtered = inTerm.Count < allList.Count;
            List<Assign> list = filtered ? inTerm : allList;
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Wrap(c.Name, 26, "T1"));
            ((TextBlock)p.Children[0]).FontFamily = new FontFamily("Segoe UI Semibold");
            string meta = c.Teacher ?? "";
            if (!string.IsNullOrEmpty(c.Room)) meta += (meta.Length > 0 ? "  ·  " : "") + "Room " + c.Room;
            if (!string.IsNullOrEmpty(c.Term)) meta += (meta.Length > 0 ? "  ·  " : "") + c.Term;
            if (meta.Length > 0) p.Children.Add(UI.Margin(UI.Text(meta, 13.5, "T2", false), 0, 4, 0, 0));
            if (c.Dropped)
            {
                Border n = UI.Card(UI.Wrap("You dropped this class" + (c.EndDate.HasValue ? " on " + c.EndDate.Value.ToString("MMMM d, yyyy") : "") + ". It is left out of your GPA and averages.", 13, "Warn"), 16);
                n.SetResourceReference(Border.BackgroundProperty, "WarnSoft");
                n.Margin = new Thickness(0, 12, 0, 0);
                p.Children.Add(n);
            }
            TextBlock big = UI.Text((Theme.NumbersOnly ? "" : (string.IsNullOrEmpty(c.Letter) ? "–" : c.Letter)) + (c.Percent.HasValue ? (Theme.NumbersOnly ? "" : "   ") + Rows.Fmt(c.Percent) + "%" : ""), 40, "T1", true);
            big.Foreground = new SolidColorBrush(Theme.GradeColor(c.Percent));
            big.Margin = new Thickness(0, 14, 0, 14);
            if (big.Text.Trim().Length > 0) p.Children.Add(big);
            else p.Children.Add(UI.Margin(UI.Text("", 1, "T3", false), 0, 0, 0, 12));

            List<Assign> graded = list.FindAll(delegate(Assign a) { return a.Status == "graded" && a.Percent.HasValue && a.Due.HasValue; });
            graded.Sort(delegate(Assign a, Assign b) { return a.Due.Value.CompareTo(b.Due.Value); });
            if (graded.Count >= 2)
            {
                LineChart lc = new LineChart();
                lc.Heading = "Scores over time";
                lc.Height = 230;
                foreach (Assign a in graded) { lc.Xs.Add(a.Due.Value); lc.Ys.Add(a.Percent.Value); lc.Names.Add(a.Name); }
                lc.Margin = new Thickness(0, 0, 0, 14);
                p.Children.Add(lc);
            }

            if (c.Tasks.Count > 1)
            {
                StackPanel tp = new StackPanel();
                tp.Children.Add(Heading("By grading period"));
                foreach (TaskGrade t in c.Tasks)
                {
                    Grid r = UI.Cols(1, 0);
                    r.Margin = new Thickness(0, 5, 0, 5);
                    UI.At(r, UI.Text(string.IsNullOrEmpty(t.Name) ? "Grade" : t.Name, 13.5, "T1", false), 0);
                    string gt = Theme.NumbersOnly ? "" : (t.Letter ?? "");
                    if (t.Percent.HasValue) gt += (gt.Length > 0 ? "  ·  " : "") + Rows.Fmt(t.Percent) + "%";
                    TextBlock rt = UI.Text(gt, 13.5, "T1", true);
                    rt.Foreground = new SolidColorBrush(Theme.GradeColor(t.Percent));
                    UI.At(r, rt, 1);
                    tp.Children.Add(r);
                }
                Border tb = UI.Card(tp, 20);
                tb.Margin = new Thickness(0, 0, 0, 14);
                p.Children.Add(tb);
            }

            StackPanel apHost = new StackPanel();
            bool showAll = false;
            Action buildAssigns = null;
            buildAssigns = delegate
            {
                apHost.Children.Clear();
                List<Assign> rowsList = new List<Assign>(showAll || !filtered ? allList : inTerm);
                StackPanel ap = new StackPanel();
                Grid hg = UI.Cols(1, 0);
                UI.At(hg, Heading("Assignments" + (filtered && !showAll ? "  ·  " + shownTerm : "") + "  ·  click one for details"), 0);
                if (filtered)
                {
                    Button tgl = UI.Btn(showAll ? "Only " + shownTerm : "Show all quarters", "Ghost", delegate { showAll = !showAll; buildAssigns(); });
                    tgl.VerticalAlignment = VerticalAlignment.Top;
                    UI.At(hg, tgl, 1);
                }
                ap.Children.Add(hg);
                Rows.SortAssigns(rowsList, false);
                if (rowsList.Count == 0) ap.Children.Add(UI.Text("No assignments found for this class.", 13.5, "T3", false));
                for (int i = 0; i < rowsList.Count; i++)
                {
                    if (i > 0) ap.Children.Add(Sep());
                    ap.Children.Add(AssignRow(rowsList[i], false));
                }
                Border ab = UI.Card(ap, 20);
                ab.Margin = new Thickness(0, 0, 0, 14);
                apHost.Children.Add(ab);
            };
            buildAssigns();
            p.Children.Add(apHost);

            Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusWindow("grades"); });
            Button gpaBtn = UI.Btn("GPA settings", "Ghost", null);
            open.Margin = new Thickness(0, 0, 8, 0);
            StackPanel bottom = UI.Row(open, gpaBtn);
            p.Children.Add(bottom);

            ContentControl host = new ContentControl();
            host.Content = p;
            gpaBtn.Click += delegate
            {
                StackPanel gp = new StackPanel();
                Button back = UI.Btn("Back to " + c.Name, "Ghost", delegate { host.Content = p; });
                back.HorizontalAlignment = HorizontalAlignment.Left;
                gp.Children.Add(back);
                TextBlock gh = UI.Wrap("GPA settings", 26, "T1");
                gh.FontFamily = new FontFamily("Segoe UI Semibold");
                gh.Margin = new Thickness(0, 18, 0, 4);
                gp.Children.Add(gh);
                gp.Children.Add(UI.Text(c.Name, 13.5, "T2", false));
                gp.Children.Add(UI.Margin(GpaLevelPicker(c), 0, 20, 0, 0));
                host.Content = gp;
            };
            NewWindow(c.Name, host, 700, 820).ShowDialog();
            if (view == "grades" || view == "dashboard") ShowView(false);
        }
    }
}
