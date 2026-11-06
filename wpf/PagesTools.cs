using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        int whatIfSel;

        static double? ParseNum(string t)
        {
            double v;
            if (t == null) return null;
            if (double.TryParse(t.Trim().Replace(',', '.').TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        UIElement LabeledBox(string label, TextBox box, string hint)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Margin(UI.Text(label, 11.5, "T3", true), 2, 0, 0, 6));
            box.ToolTip = hint;
            p.Children.Add(box);
            return p;
        }

        UIElement WhatIfCardUi()
        {
            List<Course> cs = snap.CoursesFor(term);
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("What-if calculator"));
            if (cs.Count == 0) { p.Children.Add(UI.Text("No classes to calculate with.", 13.5, "T3", false)); return UI.Card(p, 20); }
            if (whatIfSel >= cs.Count) whatIfSel = 0;

            Button pick = UI.Btn(cs[whatIfSel].Name + "  \u25BE", "Ghost", null);
            pick.HorizontalContentAlignment = HorizontalAlignment.Left;
            pick.Click += delegate
            {
                List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
                for (int i = 0; i < cs.Count; i++)
                {
                    int ix = i;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>(cs[i].Name, delegate { whatIfSel = ix; ShowView(false); }));
                }
                UI.OpenMenu(pick, UI.Menu(items.ToArray()));
            };
            p.Children.Add(pick);

            TextBox earned = new TextBox(), possible = new TextBox(), goal = new TextBox();
            goal.Text = Rows.Fmt(cfg.Goal);
            Grid row = UI.Cols(1, 1, 1);
            row.Margin = new Thickness(0, 14, 0, 12);
            UIElement a = LabeledBox("SCORE YOU GET", earned, "e.g. 18");
            UIElement b = LabeledBox("OUT OF", possible, "e.g. 20");
            UIElement c2 = LabeledBox("YOUR GOAL %", goal, "e.g. 90");
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 7, 0);
            ((FrameworkElement)b).Margin = new Thickness(7, 0, 7, 0);
            ((FrameworkElement)c2).Margin = new Thickness(7, 0, 0, 0);
            UI.At(row, a, 0); UI.At(row, b, 1); UI.At(row, c2, 2);
            p.Children.Add(row);

            Course c = cs[whatIfSel];
            double e0 = 0, t0 = 0;
            foreach (Assign x in Rows.AssignsFor(snap, c))
            {
                if (!AssignInTerm(x, term)) continue;   // only the quarter whose grade is on screen
                if (x.Status != "graded" || !x.Score.HasValue || !x.Total.HasValue || x.Total.Value <= 0) continue;
                e0 += x.Score.Value; t0 += x.Total.Value;
            }
            string now = "Campus shows " + (string.IsNullOrEmpty(c.Letter) ? "" : c.Letter + "  ") + (c.Percent.HasValue ? Rows.Fmt(c.Percent) + "%" : "no grade yet");
            if (t0 > 0) now += "   \u00B7   graded points so far: " + Rows.Fmt(e0) + " / " + Rows.Fmt(t0) + "  (" + Rows.Fmt(e0 / t0 * 100) + "%)";
            p.Children.Add(UI.Wrap(now, 12.5, "T2"));
            TextBlock res = UI.Wrap("", 14, "T3");
            res.Margin = new Thickness(0, 12, 0, 0);
            TextBlock need = UI.Wrap("", 13.5, "T1");
            need.Margin = new Thickness(0, 8, 0, 0);
            p.Children.Add(res);
            p.Children.Add(need);
            p.Children.Add(UI.Margin(UI.Wrap("An estimate from raw points. Your teacher's category weights can make Campus' number differ.", 12, "T3"), 0, 14, 0, 0));

            Action calc = delegate
            {
                double? x = ParseNum(earned.Text), pp = ParseNum(possible.Text), g = ParseNum(goal.Text);
                if (g.HasValue && g.Value > 0 && g.Value <= 100 && Math.Abs(g.Value - cfg.Goal) > 0.001) { cfg.Goal = g.Value; cfg.Save(); }
                need.Text = "";
                if (x.HasValue && pp.HasValue && pp.Value > 0)
                {
                    double np = (e0 + x.Value) / (t0 + pp.Value) * 100;
                    double d = t0 > 0 ? np - e0 / t0 * 100 : 0;
                    string line = "With " + Rows.Fmt(x) + " / " + Rows.Fmt(pp) + " your points-based grade becomes " + Rows.Fmt(np) + "%";
                    if (t0 > 0) line += "  (" + (d >= 0 ? "+" : "") + Rows.Fmt(d) + ")";
                    res.Text = line;
                    res.Foreground = new SolidColorBrush(Theme.GradeColor(np));
                }
                else
                {
                    res.Text = "Type a score and what it is out of to see what happens to this class.";
                    res.SetResourceReference(TextBlock.ForegroundProperty, "T3");
                }
                if (g.HasValue && pp.HasValue && pp.Value > 0 && t0 > 0)
                {
                    double n = g.Value / 100.0 * (t0 + pp.Value) - e0;
                    if (n <= 0) need.Text = "You would stay at or above " + Rows.Fmt(g) + "% even with a zero on this one.";
                    else if (n > pp.Value) need.Text = "To reach " + Rows.Fmt(g) + "% you would need " + Rows.Fmt(n) + " points, more than this " + Rows.Fmt(pp) + "-point assignment offers.";
                    else need.Text = "To reach " + Rows.Fmt(g) + "% you need " + Rows.Fmt(n) + " of " + Rows.Fmt(pp) + "  (" + Rows.Fmt(n / pp.Value * 100) + "%).";
                }
            };
            earned.TextChanged += delegate { calc(); };
            possible.TextChanged += delegate { calc(); };
            goal.TextChanged += delegate { calc(); };
            calc();
            return UI.Card(p, 20);
        }

        UIElement PageTools()
        {
            Border wi = (Border)WhatIfCardUi();
            Border cnt = (Border)CountdownCard();
            UIElement top = Pair(wi, cnt);

            // work due, next 14 days
            BarChart load = new BarChart();
            load.Heading = "Work due, next 14 days";
            load.EmptyText = "Nothing due in the next two weeks.";
            load.Height = 250;
            DateTime today = DateTime.Today;
            for (int i = 0; i < 14; i++)
            {
                DateTime d = today.AddDays(i);
                int n = 0;
                List<string> names = new List<string>();
                foreach (Assign a in snap.Assigns)
                    if (a.Due.HasValue && a.Due.Value.Date == d && (a.Status == "upcoming" || a.Status == "submitted" || a.Status == "ungraded")) { n++; if (names.Count < 4) names.Add(a.Name); }
                BarItem b = new BarItem();
                b.Label = d.ToString("ddd") + "|" + d.Day;
                b.Value = n;
                b.Tip = d.ToString("dddd, MMM d") + "\n" + (n == 0 ? "Nothing due" : n + " due") + (names.Count > 0 ? "\n" + string.Join("\n", names.ToArray()) + (n > names.Count ? "\n+" + (n - names.Count) + " more" : "") : "");
                b.Fill = i == 0 ? Theme.Warn : (n >= 4 ? Theme.Bad : Theme.Acc);
                load.Bars.Add(b);
            }

            // graded work by score
            BarChart dist = new BarChart();
            dist.Heading = "Your graded work by score";
            dist.EmptyText = "No graded assignments yet.";
            dist.Height = 250;
            string[] gn = new string[] { "A", "B", "C", "D", "F" };
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
                BarItem b = new BarItem();
                b.Label = gn[k];
                b.Value = counts[k];
                b.Tip = gn[k] + (k < 4 ? " (" + cuts[k] + "% and up)" : " (below 60%)") + "\n" + counts[k] + (counts[k] == 1 ? " assignment" : " assignments");
                b.Fill = cols[k];
                dist.Bars.Add(b);
            }
            Grid pair = UI.Cols(1, 1);
            load.Margin = new Thickness(0, 0, 8, 0);
            dist.Margin = new Thickness(8, 0, 0, 0);
            UI.At(pair, load, 0);
            UI.At(pair, dist, 1);
            pair.Margin = new Thickness(0, 0, 0, 16);

            List<Assign> graded = snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "graded" && a.Percent.HasValue && a.Due.HasValue; });
            graded.Sort(delegate(Assign a, Assign b) { return a.Due.Value.CompareTo(b.Due.Value); });
            if (graded.Count > 40) graded = graded.GetRange(graded.Count - 40, 40);
            LineChart trend = new LineChart();
            trend.Heading = "Score trend, last " + graded.Count + " graded assignments";
            trend.Height = 260;
            foreach (Assign a in graded) { trend.Xs.Add(a.Due.Value); trend.Ys.Add(a.Percent.Value); trend.Names.Add(a.Name + " (" + a.Course + ")"); }
            trend.Margin = new Thickness(0, 0, 0, 16);

            Button b1 = UI.Btn("Grades (.csv)", "Ghost", delegate { ExportGrades(); });
            Button b2 = UI.Btn("Assignments (.csv)", "Ghost", delegate { ExportAssignments(); });
            Button b3 = UI.Btn("Due dates (.ics calendar)", "Ghost", delegate { ExportCalendar(); });
            b1.Margin = new Thickness(0, 0, 8, 0); b2.Margin = new Thickness(0, 0, 8, 0);
            Button b4 = UI.Btn("Bell schedules…", "Ghost", delegate { Go("extract"); });
            b3.Margin = new Thickness(0, 0, 8, 0);
            Border ex = (Border)Section("Take it with you", UI.Row(b1, b2, b3, b4));

            List<ItemSpec> att = new List<ItemSpec>();
            foreach (AttRow ar in snap.Attendance)
            {
                if (att.Count >= 25) break;
                ItemSpec it = new ItemSpec();
                it.Title = (string.IsNullOrEmpty(ar.Course) ? "Attendance" : ar.Course);
                it.Sub = ar.Date.ToString("ddd, MMM d") + (string.IsNullOrEmpty(ar.Period) ? "" : "  ·  period " + ar.Period);
                it.Badge = string.IsNullOrEmpty(ar.Status) ? "Marked" : ar.Status;
                att.Add(it);
            }
            UIElement attCard = PageKit.ListCard("Attendance  ·  " + snap.Attendance.Count + (snap.Attendance.Count == 1 ? " mark" : " marks"),
                "No absences or tardies are on record.", att);
            ((FrameworkElement)attCard).Margin = new Thickness(0, 0, 0, 16);

            return Body(top, pair, trend, attCard, ex);
        }

        // ------------------------------------------------------------ exports
        static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        static string IcsEsc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
        }

        string AskSave(string name, string filter)
        {
            Microsoft.Win32.SaveFileDialog sf = new Microsoft.Win32.SaveFileDialog();
            sf.FileName = name;
            sf.Filter = filter;
            sf.OverwritePrompt = true;
            return sf.ShowDialog(this) == true ? sf.FileName : null;
        }

        void SaveText(string file, string text)
        {
            try
            {
                File.WriteAllText(file, text, new UTF8Encoding(true));
                stampText = "Saved " + Path.GetFileName(file);
                stampBad = false;
            }
            catch (Exception ex) { stampText = "Could not save: " + ex.Message; stampBad = true; }
            UpdateTop();
        }

        void ExportGrades()
        {
            if (snap == null) return;
            string f = AskSave("grades.csv", "CSV file (*.csv)|*.csv");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Class,Term,Teacher,Room,Letter,Percent");
            foreach (Course c in snap.Courses)
                sb.AppendLine(Csv(c.Name) + "," + Csv(c.Term) + "," + Csv(c.Teacher) + "," + Csv(c.Room) + "," + Csv(c.Letter) + "," + Rows.Fmt(c.Percent));
            SaveText(f, sb.ToString());
        }

        void ExportAssignments()
        {
            if (snap == null) return;
            string f = AskSave("assignments.csv", "CSV file (*.csv)|*.csv");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Assignment,Class,Due,Status,Score,Out of,Percent");
            foreach (Assign a in snap.Assigns)
                sb.AppendLine(Csv(a.Name) + "," + Csv(a.Course) + "," + (a.Due.HasValue ? a.Due.Value.ToString("yyyy-MM-dd") : "") + "," + Csv(a.Status) + "," + Rows.Fmt(a.Score) + "," + Rows.Fmt(a.Total) + "," + Rows.Fmt(a.Percent));
            SaveText(f, sb.ToString());
        }

        void ExportCalendar()
        {
            if (snap == null) return;
            string f = AskSave("due-dates.ics", "Calendar file (*.ics)|*.ics");
            if (f == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Noctua//EN\r\n");
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
                if (a.Total.HasValue) sb.Append("DESCRIPTION:" + IcsEsc(Rows.Fmt(a.Total) + " points") + "\r\n");
                sb.Append("END:VEVENT\r\n");
            }
            sb.Append("END:VCALENDAR\r\n");
            SaveText(f, sb.ToString());
        }
    }
}
