using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CampusMidnight
{
    // the newer Tools page: planning helpers rather than charts
    sealed partial class MainWindow
    {
        // ------------------------------------------------------------ the switch at the top of Tools
        UIElement ToolsSwitch()
        {
            StackPanel sp = new StackPanel();
            sp.Margin = new Thickness(0, 0, 0, 16);
            sp.Children.Add(SuSeg(new string[] { "Classic tools", "New tools" }, cfg.ToolsNew ? 1 : 0, delegate(int k)
            {
                cfg.ToolsNew = k == 1; cfg.Save(); ShowView(false);
            }));
            return sp;
        }

        static readonly string[] ProjLetters = new string[] { "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D+", "D", "F" };
        readonly Dictionary<string, string> projPick = new Dictionary<string, string>();
        DispatcherTimer focusTimer;
        int focusLeft = 25 * 60;
        bool focusBreak, focusRunning;
        TextBlock focusText, focusState;
        Button focusGo;

        UIElement Pair(UIElement a, UIElement b)
        {
            Grid g = UI.Cols(1, 1);
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 8, 16);
            ((FrameworkElement)b).Margin = new Thickness(8, 0, 0, 16);
            UI.At(g, a, 0); UI.At(g, b, 1);
            return g;
        }

        UIElement PageToolsNew()
        {
            Border fin = (Border)FinalExamCard();
            Border gpa = (Border)GpaProjectorCard();
            Border cnt = (Border)CountdownCard();
            Border foc = (Border)FocusCard();
            Border avg = (Border)AveragerCard();
            Border mis = (Border)MissingCard();
            return Body(ToolsSwitch(), Pair(fin, gpa), Pair(cnt, foc), Pair(avg, mis));
        }

        // ------------------------------------------------------------ what do I need on the final
        UIElement FinalExamCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Final exam planner"));
            p.Children.Add(UI.Wrap("What score you need on a big test or exam to land the grade you want.", 12.5, "T3"));
            TextBox cur = new TextBox(), wt = new TextBox(), want = new TextBox();
            wt.Text = "20"; want.Text = Rows.Fmt(cfg.Goal);
            List<Course> cs = snap != null ? snap.Active(term) : new List<Course>();
            foreach (Course c in cs) { if (c.Percent.HasValue) { cur.Text = Rows.Fmt(c.Percent); break; } }
            Grid g = UI.Cols(1, 1, 1);
            g.Margin = new Thickness(0, 14, 0, 12);
            UIElement a = LabeledBox("GRADE NOW %", cur, "your current percent");
            UIElement b = LabeledBox("EXAM COUNTS %", wt, "how much of the grade it is worth");
            UIElement c2 = LabeledBox("GRADE YOU WANT %", want, "e.g. 90");
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 7, 0);
            ((FrameworkElement)b).Margin = new Thickness(7, 0, 7, 0);
            ((FrameworkElement)c2).Margin = new Thickness(7, 0, 0, 0);
            UI.At(g, a, 0); UI.At(g, b, 1); UI.At(g, c2, 2);
            p.Children.Add(g);
            TextBlock res = UI.Wrap("", 14, "T1");
            p.Children.Add(res);
            Action calc = delegate
            {
                double? n = ParseNum(cur.Text), w = ParseNum(wt.Text), t = ParseNum(want.Text);
                if (!n.HasValue || !w.HasValue || !t.HasValue || w.Value <= 0 || w.Value > 100) { res.Text = "Fill in all three boxes."; return; }
                double need = (t.Value - n.Value * (1 - w.Value / 100.0)) / (w.Value / 100.0);
                if (need <= 0) res.Text = "You would reach " + Rows.Fmt(t) + "% even with a zero on it.";
                else if (need > 100) res.Text = "You would need " + Rows.Fmt(need) + "%, more than a perfect score. Try a lower goal.";
                else res.Text = "You need " + Rows.Fmt(Math.Ceiling(need * 10) / 10) + "% on it to finish with " + Rows.Fmt(t) + "%.";
            };
            cur.TextChanged += delegate { calc(); };
            wt.TextChanged += delegate { calc(); };
            want.TextChanged += delegate { calc(); };
            calc();
            return UI.Card(p, 20);
        }

        // ------------------------------------------------------------ GPA projector
        UIElement GpaProjectorCard()
        {
            LiveHost host = null;
            host = Live(delegate
            {
                StackPanel p = new StackPanel();
                p.Children.Add(Heading("GPA projector"));
                p.Children.Add(UI.Wrap("Click a letter to try another grade and see where your GPA would land.", 12.5, "T3"));
                List<Course> cs = snap != null ? snap.Active(term) : new List<Course>();
                if (cs.Count == 0) { p.Children.Add(UI.Margin(UI.Text("No classes yet.", 13.5, "T3", false), 0, 12, 0, 0)); return UI.Card(p, 20); }
                List<Course> proj = new List<Course>();
                foreach (Course c in cs)
                {
                    Course cc = c;
                    string cur = !string.IsNullOrEmpty(c.Letter) ? c.Letter : Norm.LetterFor(c.Percent);
                    string key = c.Name;
                    string pick;
                    if (!projPick.TryGetValue(key, out pick)) pick = cur;
                    if (string.IsNullOrEmpty(pick) || Array.IndexOf(ProjLetters, pick) < 0) pick = Array.IndexOf(ProjLetters, cur) >= 0 ? cur : "A";
                    Course pc = new Course();
                    pc.Name = c.Name; pc.Level = c.Level; pc.Letter = pick; pc.Section = c.Section; pc.Term = c.Term;
                    proj.Add(pc);
                    Grid row = UI.Cols(1, 0);
                    row.Margin = new Thickness(0, 10, 0, 0);
                    TextBlock nm = UI.Text(c.Name, 13.5, "T1", false);
                    nm.TextTrimming = TextTrimming.CharacterEllipsis;
                    nm.VerticalAlignment = VerticalAlignment.Center;
                    UI.At(row, nm, 0);
                    string pk = pick;
                    Button lb = UI.Btn(pick + (pick != cur && cur.Length > 0 ? "  (was " + cur + ")" : ""), "Ghost", delegate
                    {
                        int i = Array.IndexOf(ProjLetters, pk);
                        projPick[key] = ProjLetters[(i + 1) % ProjLetters.Length];
                        host.Refresh();
                    });
                    lb.MinWidth = 70;
                    UI.At(row, lb, 1);
                    p.Children.Add(row);
                }
                double? gu = Rows.Gpa(proj, false), gw = Rows.Gpa(proj, true);
                p.Children.Add(UI.Margin(UI.Text("Projected GPA  " + Rows.G2(gu) + "  unweighted   ·   " + Rows.G2(gw) + "  weighted", 14, "T1", true), 0, 16, 0, 0));
                Button reset = UI.Btn("Back to my real grades", "Flat", delegate { projPick.Clear(); host.Refresh(); });
                reset.HorizontalAlignment = HorizontalAlignment.Left;
                reset.Margin = new Thickness(0, 6, 0, 0);
                p.Children.Add(reset);
                return UI.Card(p, 20);
            });
            Border b = new Border();
            b.Child = host;
            return b;
        }

        // ------------------------------------------------------------ countdown
        UIElement CountdownCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Countdown"));
            DateTime today = DateTime.Today;
            HashSet<DateTime> school = new HashSet<DateTime>();
            DateTime lastDay = today;
            if (snap != null) foreach (CalDay d in snap.CalDays) { if (d.School) { school.Add(d.Date.Date); if (d.Date.Date > lastDay) lastDay = d.Date.Date; } }
            DateTime[] tr;
            if (snap != null && snap.TermDates.TryGetValue(term, out tr) && tr[1].Date >= today)
            {
                int left = 0;
                foreach (DateTime d in school) if (d >= today && d <= tr[1].Date) left++;
                p.Children.Add(CountLine(left > 0 ? left.ToString() : ((int)(tr[1].Date - today).TotalDays).ToString(), (left > 0 ? "school days left in " : "days left in ") + term, "ends " + tr[1].ToString("MMM d")));
            }
            if (school.Count > 0 && lastDay > today)
            {
                int left2 = 0;
                foreach (DateTime d in school) if (d >= today) left2++;
                p.Children.Add(CountLine(left2.ToString(), "school days left this year", "last day " + lastDay.ToString("MMM d")));
                for (DateTime d = today.AddDays(1); d < lastDay; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || school.Contains(d)) continue;
                    p.Children.Add(CountLine(((int)(d - today).TotalDays).ToString(), (((int)(d - today).TotalDays) == 1 ? "day" : "days") + " until the next day off", d.ToString("dddd, MMM d")));
                    break;
                }
            }
            if (p.Children.Count == 1) p.Children.Add(UI.Margin(UI.Text("The school calendar has not loaded yet.", 13.5, "T3", false), 0, 12, 0, 0));
            return UI.Card(p, 20);
        }

        UIElement CountLine(string big, string label, string sub)
        {
            Grid g = UI.Cols(0, 1);
            g.Margin = new Thickness(0, 14, 0, 0);
            TextBlock n = UI.Text(big, 30, "AccText", true);
            n.MinWidth = 70;
            n.VerticalAlignment = VerticalAlignment.Center;
            UI.At(g, n, 0);
            StackPanel s = new StackPanel();
            s.VerticalAlignment = VerticalAlignment.Center;
            s.Children.Add(UI.Text(label, 13.5, "T1", true));
            s.Children.Add(UI.Text(sub, 12, "T3", false));
            UI.At(g, s, 1);
            return g;
        }

        // ------------------------------------------------------------ focus timer
        UIElement FocusCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Focus timer"));
            p.Children.Add(UI.Wrap("25 minutes of work, then a 5 minute break. Noctua tells you when time is up.", 12.5, "T3"));
            focusText = UI.Text("", 44, "T1", true);
            focusText.Margin = new Thickness(0, 14, 0, 0);
            focusState = UI.Text("", 12.5, "T3", false);
            p.Children.Add(focusText);
            p.Children.Add(focusState);
            focusGo = UI.Btn("", "Pill", delegate { ToggleFocus(); });
            Button reset = UI.Btn("Reset", "Ghost", delegate { focusRunning = false; focusBreak = false; focusLeft = 25 * 60; UpdateFocus(); });
            focusGo.Margin = new Thickness(0, 0, 8, 0);
            StackPanel row = UI.Row(focusGo, reset);
            row.Margin = new Thickness(0, 14, 0, 0);
            p.Children.Add(row);
            if (focusTimer == null)
            {
                focusTimer = new DispatcherTimer();
                focusTimer.Interval = TimeSpan.FromSeconds(1);
                focusTimer.Tick += delegate
                {
                    if (!focusRunning) return;
                    focusLeft--;
                    if (focusLeft <= 0)
                    {
                        focusBreak = !focusBreak;
                        focusLeft = (focusBreak ? 5 : 25) * 60;
                        PushToast(focusBreak ? "Focus time is over" : "Break is over", focusBreak ? "Take 5 minutes." : "Ready for another round?");
                    }
                    UpdateFocus();
                };
                focusTimer.Start();
            }
            UpdateFocus();
            return UI.Card(p, 20);
        }

        void ToggleFocus() { focusRunning = !focusRunning; UpdateFocus(); }

        void UpdateFocus()
        {
            if (focusText == null) return;
            try
            {
                focusText.Text = (focusLeft / 60).ToString("00") + ":" + (focusLeft % 60).ToString("00");
                focusState.Text = focusBreak ? "Break" : (focusRunning ? "Focus" : "Ready");
                focusGo.Content = focusRunning ? "Pause" : "Start";
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------ score averager
        UIElement AveragerCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Score averager"));
            p.Children.Add(UI.Wrap("Type scores like 18/20, 45/50, 90%. Anything works as a list.", 12.5, "T3"));
            TextBox box = new TextBox();
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 70;
            box.Margin = new Thickness(0, 12, 0, 10);
            p.Children.Add(box);
            TextBlock res = UI.Wrap("", 14, "T1");
            p.Children.Add(res);
            box.TextChanged += delegate
            {
                double pts = 0, tot = 0, psum = 0; int n = 0;
                foreach (Match m in Regex.Matches(box.Text, @"(\d+(?:[.,]\d+)?)\s*/\s*(\d+(?:[.,]\d+)?)|(\d+(?:[.,]\d+)?)\s*%"))
                {
                    if (m.Groups[3].Success) { double? v = ParseNum(m.Groups[3].Value); if (v.HasValue) { psum += v.Value; n++; } }
                    else
                    {
                        double? e = ParseNum(m.Groups[1].Value), t = ParseNum(m.Groups[2].Value);
                        if (e.HasValue && t.HasValue && t.Value > 0) { pts += e.Value; tot += t.Value; psum += e.Value / t.Value * 100; n++; }
                    }
                }
                if (n == 0) { res.Text = ""; return; }
                res.Text = n + (n == 1 ? " score" : " scores") + "   average " + Rows.Fmt(psum / n) + "%" + (tot > 0 ? "   ·   by points " + Rows.Fmt(pts) + " / " + Rows.Fmt(tot) + "  (" + Rows.Fmt(pts / tot * 100) + "%)" : "");
            };
            return UI.Card(p, 20);
        }

        // ------------------------------------------------------------ missing work
        UIElement MissingCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Missing work"));
            List<Assign> miss = snap != null ? snap.Assigns.FindAll(delegate(Assign a) { return a.Status == "missing" && !a.InDropped; }) : new List<Assign>();
            miss.Sort(delegate(Assign x, Assign y) { return Rows.DueCompare(x, y, false); });
            if (miss.Count == 0) { p.Children.Add(UI.Margin(UI.Text("Nothing is marked missing. Nice.", 13.5, "T3", false), 0, 12, 0, 0)); return UI.Card(p, 20); }
            p.Children.Add(UI.Wrap(miss.Count + (miss.Count == 1 ? " assignment is" : " assignments are") + " marked missing. Click one to open it.", 12.5, "T3"));
            int shown = 0;
            foreach (Assign a in miss)
            {
                if (shown++ >= 8) break;
                Assign ax = a;
                StackPanel inner = new StackPanel();
                inner.Children.Add(UI.Text(a.Name, 13.5, "T1", true));
                inner.Children.Add(UI.Text(a.Course + (a.Due.HasValue ? "  ·  due " + a.Due.Value.ToString("MMM d") : ""), 12, "T3", false));
                Button bt = UI.Btn("", "Flat", delegate { OpenAssign(ax); });
                bt.Content = inner;
                bt.HorizontalContentAlignment = HorizontalAlignment.Left;
                bt.HorizontalAlignment = HorizontalAlignment.Stretch;
                bt.Margin = new Thickness(0, 6, 0, 0);
                p.Children.Add(bt);
            }
            if (miss.Count > 8) p.Children.Add(UI.Margin(UI.Text("+" + (miss.Count - 8) + " more on the Work page", 12, "T3", false), 0, 8, 0, 0));
            return UI.Card(p, 20);
        }
    }
}
