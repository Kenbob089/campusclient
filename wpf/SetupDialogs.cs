using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CampusMidnight
{
    // small dialogs for the setup: popup picker, name box, time box, saved-schedule editor
    sealed partial class MainWindow
    {
        // a popup list under a button; a tick marks the current choice
        void SuPick(FrameworkElement anchor, string[] opts, int sel, Action<int> chosen)
        {
            List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
            for (int i = 0; i < opts.Length; i++)
            {
                int ix = i;
                items.Add(new KeyValuePair<string, RoutedEventHandler>((i == sel ? "✓  " : (sel >= 0 ? "     " : "")) + opts[i], delegate { chosen(ix); }));
            }
            UI.OpenMenu(anchor, UI.Menu(items.ToArray()));
        }

        UIElement SuDlgLabel(string caption)
        {
            TextBlock t = UI.Wrap(caption, 13.5, "T2");
            t.Margin = new Thickness(0, 0, 0, 12);
            return t;
        }

        // returns the typed name, or null when cancelled
        string SuNameDialog(string title, string caption, string initial)
        {
            string result = null;
            TextBox box = new TextBox();
            box.Text = initial ?? "";
            TextBlock err = UI.Text("", 12.5, "Bad", false);
            err.Margin = new Thickness(0, 8, 0, 0);
            Window win = null;
            Action done = delegate
            {
                string t = box.Text.Trim();
                if (t.Length == 0 || t.IndexOfAny(new char[] { '|', ';', '~', '=' }) >= 0)
                {
                    err.Text = "Use a short name without | ; ~ or =.";
                    return;
                }
                result = t;
                win.Close();
            };
            Button ok = UI.Btn("Save", "Pill", delegate { done(); });
            Button no = UI.Btn("Cancel", "Ghost", delegate { win.Close(); });
            no.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(ok, no);
            btns.Margin = new Thickness(0, 18, 0, 0);
            win = NewWindow(title, UI.Col(SuDlgLabel(caption), box, err, btns), 520, 300);
            box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; done(); } };
            win.Loaded += delegate { box.Focus(); box.SelectAll(); };
            win.ShowDialog();
            return result;
        }

        // start and end of one grid row, typed like 8:00 AM
        bool SuTimeDialog(int row, string start, string end, out string startOut, out string endOut)
        {
            string rs = null, re = null;
            TimeSpan t;
            TextBox a = new TextBox(), b = new TextBox();
            a.Text = Bell.TryTime(start, out t) ? Bell.Clock(t) : start;
            b.Text = Bell.TryTime(end, out t) ? Bell.Clock(t) : end;
            TextBlock err = UI.Text("", 12.5, "Bad", false);
            err.Margin = new Thickness(0, 10, 0, 0);
            Window win = null;
            Action done = delegate
            {
                TimeSpan x, y;
                bool okA = Bell.TryTime(a.Text, out x), okB = Bell.TryTime(b.Text, out y);
                if (!okA || !okB) { err.Text = "Type times like 8:00 AM."; return; }
                if (y <= x) { err.Text = "The end has to be after the start."; return; }
                rs = x.ToString(@"hh\:mm"); re = y.ToString(@"hh\:mm");
                win.Close();
            };
            Button ok = UI.Btn("Save", "Pill", delegate { done(); });
            Button no = UI.Btn("Cancel", "Ghost", delegate { win.Close(); });
            no.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(ok, no);
            btns.Margin = new Thickness(0, 18, 0, 0);
            StackPanel col = UI.Col(
                SuDlgLabel("When does this row start and end? For example 8:00 AM and 8:50 AM."),
                UI.Margin(UI.Text("STARTS", 11.5, "T3", true), 2, 0, 0, 6), a,
                UI.Margin(UI.Text("ENDS", 11.5, "T3", true), 2, 12, 0, 6), b, err, btns);
            win = NewWindow("Row " + row + " times", col, 520, 400);
            KeyEventHandler enter = delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; done(); } };
            a.KeyDown += enter; b.KeyDown += enter;
            win.Loaded += delegate { a.Focus(); a.SelectAll(); };
            win.ShowDialog();
            startOut = rs; endOut = re;
            return rs != null;
        }

        // ------------------------------------------------------------ saved schedules
        void EditVariant(string old)
        {
            if (cfg.GridStarts.Count == 0) { StartScheduleEdit(); return; }
            List<string> ex = null;
            if (old != null) cfg.Variants.TryGetValue(old, out ex);
            List<string> starts = cfg.GridStarts, ends = cfg.GridEnds;

            TextBox name = new TextBox();
            name.Text = old ?? "Half day";
            TextBlock err = UI.Text("", 12.5, "Bad", false);
            err.Margin = new Thickness(0, 10, 0, 0);
            List<TextBox> ia = new List<TextBox>(), ib = new List<TextBox>();
            StackPanel col = new StackPanel();
            col.Children.Add(UI.Margin(UI.Text("NAME", 11.5, "T3", true), 2, 0, 0, 6));
            col.Children.Add(name);
            TextBlock help = UI.Wrap("Set the start and end for each row. Leave both empty to skip a row on this schedule.", 13.5, "T2");
            help.Margin = new Thickness(0, 14, 0, 6);
            col.Children.Add(help);
            for (int i = 0; i < starts.Count; i++)
            {
                TimeSpan a0 = TimeSpan.Zero, b0 = TimeSpan.Zero;
                bool hasA = Bell.TryTime(starts[i], out a0), hasB = Bell.TryTime(ends[i], out b0);
                string reg = (hasA ? Bell.Clock(a0) : starts[i]) + " - " + (hasB ? Bell.Clock(b0) : ends[i]);
                col.Children.Add(UI.Margin(UI.Text("ROW " + (i + 1) + "   (regular " + reg + ")", 11.5, "T3", true), 2, 10, 0, 6));
                TextBox ta = new TextBox(), tb = new TextBox();
                string sv = "", ev = "";
                if (ex != null && i < ex.Count)
                {
                    string[] pr = ex[i].Split('-');
                    TimeSpan t1, t2;
                    if (pr.Length == 2 && Bell.TryTime(pr[0], out t1) && Bell.TryTime(pr[1], out t2)) { sv = Bell.Clock(t1); ev = Bell.Clock(t2); }
                }
                else { sv = hasA ? Bell.Clock(a0) : starts[i]; ev = hasB ? Bell.Clock(b0) : ends[i]; }
                ta.Text = sv; tb.Text = ev;
                ia.Add(ta); ib.Add(tb);
                Grid g = UI.Cols(1, 1);
                ta.Margin = new Thickness(0, 0, 6, 0);
                tb.Margin = new Thickness(6, 0, 0, 0);
                UI.At(g, ta, 0); UI.At(g, tb, 1);
                col.Children.Add(g);
            }
            col.Children.Add(err);

            Window win = null;
            bool saved = false;
            string finalName = "";
            List<string> finalTimes = null;
            Action done = delegate
            {
                string n = name.Text.Trim();
                bool bad = n.Length == 0 || n.IndexOfAny(new char[] { '|', ';', '~', '=', '#', '^', ':' }) >= 0
                    || string.Equals(n, "regular", StringComparison.OrdinalIgnoreCase) || string.Equals(n, "none", StringComparison.OrdinalIgnoreCase)
                    || (n != old && cfg.Variants.ContainsKey(n));
                if (bad) { err.Text = "Pick a short unused name (not Regular or None, and no | ; ~ = # ^ :)."; return; }
                List<string> times = new List<string>();
                bool any = false;
                for (int i = 0; i < ia.Count; i++)
                {
                    string sa = ia[i].Text.Trim(), sb = ib[i].Text.Trim();
                    if (sa.Length == 0 && sb.Length == 0) { times.Add(""); continue; }
                    TimeSpan x, y;
                    bool okRow = Bell.TryTime(sa, out x) && Bell.TryTime(sb, out y) && y > x;
                    if (!okRow) { err.Text = "Row " + (i + 1) + " needs times like 8:00 AM and a later end."; return; }
                    Bell.TryTime(sa, out x); Bell.TryTime(sb, out y);
                    times.Add(x.ToString(@"hh\:mm") + "-" + y.ToString(@"hh\:mm"));
                    any = true;
                }
                if (!any) { err.Text = "Fill in at least one row."; return; }
                finalName = n; finalTimes = times; saved = true;
                win.Close();
            };
            Button ok = UI.Btn("Save", "Pill", delegate { done(); });
            Button no = UI.Btn("Cancel", "Ghost", delegate { win.Close(); });
            no.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(ok, no);
            btns.Margin = new Thickness(0, 18, 0, 0);
            col.Children.Add(btns);
            win = NewWindow(old == null ? "New saved schedule" : "Edit " + old, col, 560, 760);
            win.Loaded += delegate { name.Focus(); name.SelectAll(); };
            win.ShowDialog();
            if (!saved) return;

            if (old != null && old != finalName)
            {
                cfg.Variants.Remove(old);
                List<string> keys = new List<string>(cfg.DayTypes.Keys);
                foreach (string k in keys) if (cfg.DayTypes[k] == old) cfg.DayTypes[k] = finalName;
            }
            cfg.Variants[finalName] = finalTimes;
            cfg.Save();
            ShowView(false);
        }

        void DeleteVariant(string name)
        {
            cfg.Variants.Remove(name);
            List<string> keys = new List<string>(cfg.DayTypes.Keys);
            foreach (string k in keys) if (cfg.DayTypes[k] == name) cfg.DayTypes.Remove(k);
            cfg.Save();
            ShowView(false);
        }

        // once the last day of school has passed the student's schedule is cleared
        void CheckSchoolEnd()
        {
            DateTime end;
            if (cfg.SchoolEnd.Length == 0 || !DateTime.TryParseExact(cfg.SchoolEnd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out end)) return;
            bool over = DateTime.Today > end;
            if (!over && DateTime.Today == end && snap != null)
            {
                DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
                if (p.School && p.Slots.Count > 0 && DateTime.Now.TimeOfDay > p.Slots[p.Slots.Count - 1].B.End) over = true;
            }
            if (!over) return;
            cfg.Schedules.Clear(); cfg.DayScheds.Clear(); cfg.PeriodMap.Clear(); cfg.CourseDays.Clear();
            cfg.GridDays.Clear(); cfg.GridStarts.Clear(); cfg.GridEnds.Clear(); cfg.GridCells.Clear();
            cfg.Variants.Clear(); cfg.DayTypes.Clear();
            cfg.RotOn = false; cfg.RotAnchor = ""; cfg.SchoolEnd = "";
            cfg.SetupVer = SuVersion;
            cfg.Save();
            Log.Write("last day of school passed: schedule cleared");
            SuToast("Campus Midnight", "School is out. Your schedule was cleared. Set up a new one when the next year starts.");
            if (view == "setup") return;
            ShowView(false);
        }
    }
}
