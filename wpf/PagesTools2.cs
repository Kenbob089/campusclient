using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        UIElement Pair(UIElement a, UIElement b)
        {
            Grid g = UI.Cols(1, 1);
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 8, 16);
            ((FrameworkElement)b).Margin = new Thickness(8, 0, 0, 16);
            UI.At(g, a, 0); UI.At(g, b, 1);
            return g;
        }

        // ------------------------------------------------------------ countdown
        UIElement CountdownCard()
        {
            StackPanel p = new StackPanel();
            Grid head = UI.Cols(1, 0);
            UI.At(head, Heading("Countdown"), 0);
            Button plus = UI.Btn("+", "Ghost", delegate { AddCountdown(); });
            plus.FontSize = 18; plus.Padding = new Thickness(11, 2, 11, 4);
            plus.ToolTip = "New countdown";
            plus.VerticalAlignment = VerticalAlignment.Top;
            UI.At(head, plus, 1);
            p.Children.Add(head);
            int fixedLines = p.Children.Count;
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
            // the student's own countdowns
            foreach (string cd in new List<string>(cfg.Countdowns))
            {
                string[] cp = cd.Split('~');
                DateTime when;
                if (cp.Length < 2 || !DateTime.TryParseExact(cp[1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out when)) continue;
                string nm = cp[0];
                bool onSchool = cp.Length > 2 && cp[2] == "1";
                int days = (int)(when.Date - today).TotalDays;
                if (onSchool)
                {
                    // count only school days: the calendar's own days, and plain weekdays past the end of the calendar
                    int sd = 0;
                    DateTime a0 = days >= 0 ? today : when.Date, b0 = days >= 0 ? when.Date : today;
                    for (DateTime x = a0.AddDays(1); x <= b0; x = x.AddDays(1))
                    {
                        bool wk = x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday;
                        if (school.Count > 0 && x <= lastDay ? school.Contains(x) : wk) sd++;
                    }
                    days = days >= 0 ? sd : -sd;
                }
                string unit = onSchool ? "school day" : "day";
                string big = Math.Abs(days).ToString();
                string label = days > 0 ? (days == 1 ? unit + " until " : unit + "s until ") + nm : days == 0 ? "today: " + nm : (days == -1 ? unit + " since " : unit + "s since ") + nm;
                Grid row = UI.Cols(1, 0);
                UIElement line = CountLine(days == 0 ? "0" : big, label, when.ToString("dddd, MMM d, yyyy"));
                UI.At(row, line, 0);
                string key = cd;
                Button rm = UI.Btn("\u2715", "Flat", delegate { cfg.Countdowns.Remove(key); cfg.Save(); ShowView(false); });
                rm.ToolTip = "Remove this countdown";
                rm.VerticalAlignment = VerticalAlignment.Bottom;
                UI.At(row, rm, 1);
                p.Children.Add(row);
            }
            if (p.Children.Count == fixedLines) p.Children.Add(UI.Margin(UI.Text("The school calendar has not loaded yet. Press + to make your own countdown.", 13.5, "T3", false), 0, 12, 0, 0));
            return UI.Card(p, 20);
        }

        void AddCountdown()
        {
            TextBox nameBox = UI.Input("e.g. Prom, Spring break, AP Calculus exam", "");
            TextBox dateBox = UI.Input("e.g. 5/12/2027", "");
            TextBlock err = UI.Text("", 12.5, "Bad", false);
            err.Margin = new Thickness(0, 8, 0, 0);
            CheckBox useSchool = new CheckBox();
            useSchool.Content = "Use school days";
            useSchool.ToolTip = "Count only days the school is open, instead of every day";
            useSchool.Margin = new Thickness(0, 14, 0, 0);
            Window win = null;
            Action done = delegate
            {
                string t = nameBox.Text.Trim();
                DateTime when;
                if (t.Length == 0 || t.IndexOfAny(new char[] { '~', '^', '=', '|', ';' }) >= 0) { err.Text = "Give it a short name (no ~ ^ = | ;)."; return; }
                if (!DateTime.TryParse(dateBox.Text.Trim(), System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out when)) { err.Text = "Type a date like 5/12/2027."; return; }
                cfg.Countdowns.Add(t + "~" + when.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + (useSchool.IsChecked == true ? "~1" : ""));
                cfg.Save();
                win.Close();
                ShowView(false);
            };
            Button ok = UI.Btn("Add", "Pill", delegate { done(); });
            Button no = UI.Btn("Cancel", "Ghost", delegate { win.Close(); });
            no.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(ok, no);
            btns.Margin = new Thickness(0, 18, 0, 0);
            dateBox.Margin = new Thickness(0, 12, 0, 0);
            win = NewWindow("New countdown", UI.Col(UI.Text("WHAT ARE YOU COUNTING DOWN TO?", 11.5, "T3", true), UI.Margin(nameBox, 0, 6, 0, 0), UI.Margin(UI.Text("DATE", 11.5, "T3", true), 0, 14, 0, 0), UI.Margin(dateBox, 0, 6, 0, 0), useSchool, err, btns), 520, 420);
            nameBox.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; done(); } };
            dateBox.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; done(); } };
            win.Loaded += delegate { nameBox.Focus(); };
            win.ShowDialog();
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

    }
}
