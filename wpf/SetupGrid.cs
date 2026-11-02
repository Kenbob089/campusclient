using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CampusMidnight
{
    // Step 1 of the setup: the rotation grid. Columns are letter days, rows are time slots, circles are periods.
    sealed partial class MainWindow
    {
        List<string> suDays = new List<string>();
        List<string> suStart = new List<string>(), suEnd = new List<string>();
        Dictionary<string, List<string>> suCells = new Dictionary<string, List<string>>();
        string suFirstS = "1", suLastS = "7", suLunchS = "4";
        int suRotMode, suRotDir;
        bool suUseCampus;
        string suTodayDay = "";
        string suAutoNote = "";
        bool suNoRot, suEndOn;
        DateTime suEndDate = DateTime.Today;

        static string SuHM(TimeSpan t) { return t.ToString(@"hh\:mm"); }

        void SuGridLoad()
        {
            suDays = new List<string>(cfg.GridDays);
            suStart = new List<string>(cfg.GridStarts);
            suEnd = new List<string>(cfg.GridEnds);
            suCells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in cfg.GridCells) suCells[kv.Key] = new List<string>(kv.Value);
            if (suDays.Count == 0 || suStart.Count == 0) SuGridSeed();
            SuGridNormalize();
            suAutoNote = "";
            suRotMode = 0; suRotDir = 0;
            suFirstS = "1"; suLastS = suStart.Count.ToString(); suLunchS = Math.Max(1, suStart.Count / 2).ToString();
            suUseCampus = !cfg.RotOn && SuCampusLabels().Length >= 2;
            suNoRot = cfg.NoRotation;
            DateTime pe = DateTime.MinValue;
            suEndOn = cfg.SchoolEnd.Length > 0 && DateTime.TryParseExact(cfg.SchoolEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out pe);
            if (suEndOn) suEndDate = pe;
            else suEndDate = new DateTime(DateTime.Today.Month >= 8 ? DateTime.Today.Year + 1 : DateTime.Today.Year, 5, 31);
            suTodayDay = "";
            if (cfg.RotOn && snap != null)
            {
                string from;
                string lab = Sched.LabelFor(cfg, snap, DateTime.Today, out from);
                if (suDays.Contains(lab)) suTodayDay = lab;
            }
            if (suTodayDay.Length == 0 && suDays.Count > 0) suTodayDay = suDays[0];
        }

        // letters and times from Campus when it shared them, else a plain A/B day with seven periods
        void SuGridSeed()
        {
            suDays.Clear(); suStart.Clear(); suEnd.Clear(); suCells.Clear();
            List<string> letters = new List<string>();
            BellSchedule src = null;
            if (snap != null)
            {
                foreach (string nm in snap.SchedNames.Values)
                    if (nm.Length > 0 && nm.IndexOf(' ') < 0 && nm.Length <= 3 && !letters.Contains(nm)) letters.Add(nm);
                letters.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (BellSchedule ab in snap.AutoBells) if (letters.Count > 0 && ab.Name == letters[0]) { src = ab; break; }
                if (src == null && snap.AutoBells.Count > 0) src = snap.AutoBells[0];
            }
            if (letters.Count == 0) { letters.Add("A"); letters.Add("B"); }
            suDays.AddRange(letters);
            if (src != null) SuGridTimesFrom(src);
            if (suStart.Count == 0)
            {
                TimeSpan t = new TimeSpan(8, 0, 0);
                for (int i = 0; i < 7; i++)
                {
                    suStart.Add(SuHM(t));
                    t = t.Add(TimeSpan.FromMinutes(50));
                    suEnd.Add(SuHM(t));
                    t = t.Add(TimeSpan.FromMinutes(5));
                }
            }
            foreach (string d in suDays)
            {
                List<string> c = new List<string>();
                for (int i = 0; i < suStart.Count; i++) c.Add((i + 1).ToString());
                suCells[d] = c;
            }
        }

        void SuGridTimesFrom(BellSchedule src)
        {
            List<Bell> ib = new List<Bell>();
            foreach (Bell b in src.Entries) if (!b.NonInstr) ib.Add(b);
            ib.Sort(delegate(Bell x, Bell y) { return x.Start.CompareTo(y.Start); });
            if (ib.Count == 0) return;
            suStart.Clear(); suEnd.Clear();
            foreach (Bell b in ib) { suStart.Add(SuHM(b.Start)); suEnd.Add(SuHM(b.End)); }
        }

        void SuGridNormalize()
        {
            while (suEnd.Count < suStart.Count) suEnd.Add(suStart[suEnd.Count]);
            while (suEnd.Count > suStart.Count) suEnd.RemoveAt(suEnd.Count - 1);
            foreach (string d in suDays)
            {
                List<string> c;
                if (!suCells.TryGetValue(d, out c)) { c = new List<string>(); suCells[d] = c; }
                while (c.Count < suStart.Count) c.Add("");
                while (c.Count > suStart.Count) c.RemoveAt(c.Count - 1);
            }
            List<string> gone = new List<string>();
            foreach (string k in suCells.Keys) if (!suDays.Contains(k)) gone.Add(k);
            foreach (string k in gone) suCells.Remove(k);
        }

        static bool SuIsNum(string s)
        {
            int n;
            return s != null && int.TryParse(s, out n) && n > 0;
        }

        List<string> SuGridNumbers()
        {
            List<int> nums = new List<int>();
            foreach (KeyValuePair<string, List<string>> kv in suCells)
                foreach (string c in kv.Value)
                {
                    int n;
                    if (int.TryParse(c, out n) && n > 0 && !nums.Contains(n)) nums.Add(n);
                }
            nums.Sort();
            List<string> r = new List<string>();
            foreach (int n in nums) r.Add(n.ToString());
            return r;
        }

        static string SuShort(string hm)
        {
            TimeSpan t;
            if (!Bell.TryTime(hm, out t)) return hm;
            return DateTime.Today.Add(t).ToString("h:mm", CultureInfo.InvariantCulture);
        }

        string SuGridTimesFor(string num)
        {
            foreach (string d in suDays)
            {
                List<string> c;
                if (!suCells.TryGetValue(d, out c)) continue;
                for (int i = 0; i < c.Count && i < suStart.Count; i++)
                    if (c[i] == num) return SuShort(suStart[i]) + " - " + SuShort(suEnd[i]) + "  (" + d + ")";
            }
            return "";
        }

        string SuGridCheck()
        {
            if (suDays.Count == 0) return "Add at least one letter day.";
            if (suStart.Count == 0) return "Add at least one row.";
            for (int i = 0; i < suStart.Count; i++)
            {
                TimeSpan a, b;
                if (!Bell.TryTime(suStart[i], out a) || !Bell.TryTime(suEnd[i], out b) || b <= a) return "Row " + (i + 1) + " has a start or end time that is not valid. Click its time to fix it.";
            }
            return "";
        }

        // turns the grid into the bell schedules the rest of the app reads
        void SuGridApply()
        {
            SuGridNormalize();
            cfg.GridDays = new List<string>(suDays);
            cfg.GridStarts = new List<string>(suStart);
            cfg.GridEnds = new List<string>(suEnd);
            cfg.GridCells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in suCells) cfg.GridCells[kv.Key] = new List<string>(kv.Value);
            cfg.Schedules.Clear();
            cfg.DayScheds.Clear();
            foreach (string d in suDays)
            {
                BellSchedule bs = new BellSchedule();
                bs.Name = d;
                List<string> c = suCells[d];
                for (int i = 0; i < c.Count; i++)
                {
                    if (c[i].Trim().Length == 0) continue;
                    TimeSpan a, e;
                    if (!Bell.TryTime(suStart[i], out a) || !Bell.TryTime(suEnd[i], out e)) continue;
                    Bell b = new Bell();
                    b.Label = c[i].Trim(); b.Start = a; b.End = e;
                    bs.Entries.Add(b);
                }
                cfg.Schedules.Add(bs);
                cfg.DayScheds[d] = d;
            }
            cfg.RotLabels = string.Join(",", suDays.ToArray());
            cfg.NoRotation = suNoRot;
            cfg.SchoolEnd = suEndOn ? suEndDate.ToString("yyyy-MM-dd") : "";
            cfg.RotOn = !suUseCampus;
            // on weekends and days off the letter is not changed; the saved one keeps counting
            bool keepAnchor = cfg.RotOn && cfg.RotAnchor.Length > 0 && !Sched.IsSchoolDay(cfg, snap, DateTime.Today);
            if (cfg.RotOn && !keepAnchor)
            {
                DateTime a0 = DateTime.Today;
                if (!Sched.IsSchoolDay(cfg, snap, a0)) a0 = Sched.NextSchoolDay(cfg, snap, a0);
                cfg.RotAnchor = a0.ToString("yyyy-MM-dd");
                cfg.RotIndex = Math.Max(0, suDays.IndexOf(suTodayDay));
            }
            // class choices are per period number now; drop the old per-day and per-time choices
            List<string> old = new List<string>();
            foreach (string k in cfg.PeriodMap.Keys) if (!k.EndsWith("#")) old.Add(k);
            foreach (string k in old) cfg.PeriodMap.Remove(k);
            cfg.Save();
            Log.Write("setup: grid saved, days=" + string.Join(",", suDays.ToArray()) + " rows=" + suStart.Count + " rotation=" + cfg.RotOn);
        }

        // the period number a class sits in, found from where Campus puts it on that letter day's grid column
        string SuNumberFor(Placement p)
        {
            string letter = null;
            foreach (string d in suDays) if (string.Equals(d, p.SchedName, StringComparison.OrdinalIgnoreCase)) { letter = d; break; }
            if (letter == null) return null;
            TimeSpan start = TimeSpan.Zero;
            bool have = false;
            foreach (BellSchedule ab in snap.AutoBells)
            {
                if (!(ab.Id.Length > 0 && ab.Id == p.SchedId) && ab.Name != p.SchedName) continue;
                foreach (Bell b in ab.Entries) if (!b.NonInstr && string.Equals(b.Label, p.Name, StringComparison.OrdinalIgnoreCase)) { start = b.Start; have = true; break; }
                if (have) break;
            }
            if (!have)
            {
                TimeSpan t;
                if (!Bell.TryTime(p.Name, out t)) return null;
                start = t;
            }
            List<string> col = suCells[letter];
            for (int i = 0; i < suStart.Count && i < col.Count; i++)
            {
                TimeSpan rs;
                if (!Bell.TryTime(suStart[i], out rs)) continue;
                if (Math.Abs((rs - start).TotalMinutes) <= 4 && SuIsNum(col[i])) return col[i];
            }
            return null;
        }

        // only runs when the Auto fill button is pressed
        void SuAutoFill()
        {
            suAutoNote = "";
            if (snap == null) return;
            SuGridNormalize();
            int filled = 0;
            List<string> missed = new List<string>();
            Dictionary<string, string> seen = new Dictionary<string, string>();
            foreach (RosterItem ri in snap.Roster)
            {
                if (ri.Dropped || !Sched.TermMatches(ri, term)) continue;
                string num = null;
                foreach (Placement p in ri.Places)
                {
                    if (term.Length > 0 && p.Term.Length > 0 && p.Term.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0 && term.IndexOf(p.Term, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    num = SuNumberFor(p);
                    if (num != null) break;
                }
                if (num == null) { missed.Add(ri.Course); continue; }
                string key = Settings.PeriodKey("", num, "");
                if (seen.ContainsKey(key)) { missed.Add(ri.Course + " (period " + num + " already has " + seen[key] + ")"); continue; }
                cfg.PeriodMap[key] = Settings.Slug(ri.Course);
                seen[key] = ri.Course;
                filled++;
            }
            cfg.Save();
            suAutoNote = "Filled " + filled + " period" + (filled == 1 ? "" : "s") + " from Campus." + (missed.Count > 0 ? " Could not place: " + string.Join(", ", missed.ToArray()) + ". Pick those by hand." : "");
            Log.Write("setup: " + suAutoNote);
        }

        // moves the periods inside a group of rows; blocks like M and Lunch stay where they are
        static List<string> SuRotateCells(List<string> src, int shift, int mode, int first, int last, int lunchRow)
        {
            List<string> r = new List<string>(src);
            List<List<int>> groups = new List<List<int>>();
            List<int> g1 = new List<int>(), g2 = new List<int>();
            for (int i = first; i <= last && i < src.Count; i++)
            {
                if (mode == 1 && i >= lunchRow) g2.Add(i); else g1.Add(i);
            }
            groups.Add(g1);
            if (mode == 1) groups.Add(g2);
            foreach (List<int> g in groups)
            {
                List<int> idx = new List<int>();
                foreach (int i in g) if (SuIsNum(src[i])) idx.Add(i);
                int n = idx.Count;
                if (n < 2) continue;
                for (int k = 0; k < n; k++)
                {
                    int from = (((k - shift) % n) + n) % n;
                    r[idx[k]] = src[idx[from]];
                }
            }
            return r;
        }

        void SuFillOtherDays()
        {
            int f, l, lu;
            if (!int.TryParse(suFirstS.Trim(), out f)) f = 1;
            if (!int.TryParse(suLastS.Trim(), out l)) l = suStart.Count;
            if (!int.TryParse(suLunchS.Trim(), out lu)) lu = suStart.Count / 2;
            f = Math.Max(1, f); l = Math.Min(suStart.Count, Math.Max(f, l)); lu = Math.Max(f, Math.Min(l, lu));
            List<string> baseCells = suCells[suDays[0]];
            for (int di = 1; di < suDays.Count; di++)
                suCells[suDays[di]] = SuRotateCells(baseCells, suRotDir == 0 ? di : -di, suRotMode, f - 1, l - 1, lu);
        }

        // ------------------------------------------------------------ the grid widget
        // one circle: a number is filled, a named block is grey, empty is an outline
        UIElement SuCircle(string v, Action click)
        {
            Grid cg = new Grid();
            cg.Width = 38; cg.Height = 38;
            Ellipse el = new Ellipse();
            TextBlock tb = new TextBlock();
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.FontFamily = new FontFamily("Segoe UI Semibold");
            tb.IsHitTestVisible = false;
            bool isNum = SuIsNum(v);
            bool isLabel = !isNum && v.Length > 0;
            string rest = isNum ? "Acc" : "Bg4";
            string over = isNum ? "AccHover" : "Bg5";
            if (isNum)
            {
                tb.Text = v;
                tb.FontSize = 14;
                tb.Foreground = Brushes.White;
                el.SetResourceReference(Shape.FillProperty, rest);
            }
            else if (isLabel)
            {
                tb.Text = v.Length > 5 ? v.Substring(0, 4) + "…" : v;
                tb.FontSize = v.Length > 2 ? 11 : 13.5;
                tb.SetResourceReference(TextBlock.ForegroundProperty, "T3");
                el.SetResourceReference(Shape.FillProperty, rest);
            }
            else
            {
                el.StrokeThickness = 1.5;
                el.SetResourceReference(Shape.StrokeProperty, "Line2");
            }
            cg.Children.Add(el);
            cg.Children.Add(tb);
            Border hit = new Border();
            hit.Background = Brushes.Transparent;
            hit.Cursor = Cursors.Hand;
            hit.Child = cg;
            hit.MouseEnter += delegate
            {
                if (isNum || isLabel) el.SetResourceReference(Shape.FillProperty, over);
                else el.SetResourceReference(Shape.StrokeProperty, "AccText");
            };
            hit.MouseLeave += delegate
            {
                if (isNum || isLabel) el.SetResourceReference(Shape.FillProperty, rest);
                else el.SetResourceReference(Shape.StrokeProperty, "Line2");
            };
            hit.MouseLeftButtonUp += delegate { click(); };
            return hit;
        }

        static void SuPut(Grid g, UIElement e, int row, int col)
        {
            Grid.SetRow(e, row);
            Grid.SetColumn(e, col);
            g.Children.Add(e);
        }

        UIElement SuGridCard()
        {
            Grid g = new Grid();
            ColumnDefinition lc = new ColumnDefinition();
            lc.Width = new GridLength(150);
            g.ColumnDefinitions.Add(lc);
            for (int c = 0; c < suDays.Count; c++)
            {
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new GridLength(1, GridUnitType.Star);
                cd.MaxWidth = 140;
                g.ColumnDefinitions.Add(cd);
            }
            ColumnDefinition spare = new ColumnDefinition();
            spare.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(spare);
            RowDefinition hr = new RowDefinition();
            hr.Height = new GridLength(46);
            g.RowDefinitions.Add(hr);
            for (int r = 0; r < suStart.Count; r++)
            {
                RowDefinition rd = new RowDefinition();
                rd.Height = new GridLength(56);
                g.RowDefinitions.Add(rd);
            }

            // letter headers
            for (int c = 0; c < suDays.Count; c++)
            {
                int col = c;
                Button hb = new Button();
                hb.Style = (Style)Application.Current.Resources["Flat"];
                hb.Content = UI.Text(suDays[c], 15, "T1", true);
                ((TextBlock)hb.Content).HorizontalAlignment = HorizontalAlignment.Center;
                hb.HorizontalContentAlignment = HorizontalAlignment.Center;
                hb.Margin = new Thickness(4, 4, 4, 4);
                hb.ToolTip = "Rename, move or remove this day";
                hb.Click += delegate { SuHeadClick(col, hb); };
                SuPut(g, hb, 0, c + 1);
            }
            Border line = new Border();
            line.Height = 1;
            line.VerticalAlignment = VerticalAlignment.Bottom;
            line.Margin = new Thickness(12, 0, 12, 0);
            line.SetResourceReference(Border.BackgroundProperty, "Line");
            Grid.SetColumnSpan(line, suDays.Count + 2);
            SuPut(g, line, 0, 0);

            for (int r = 0; r < suStart.Count; r++)
            {
                int row = r;
                Button tbn = new Button();
                tbn.Style = (Style)Application.Current.Resources["Flat"];
                TextBlock tt = UI.Text(SuShort(suStart[r]) + " - " + SuShort(suEnd[r]), 13, "T2", false);
                tbn.Content = tt;
                tbn.Margin = new Thickness(8, 6, 4, 6);
                tbn.Padding = new Thickness(10, 0, 10, 0);
                tbn.ToolTip = "Change this row's start and end";
                tbn.Click += delegate { SuTimeClick(row); };
                SuPut(g, tbn, r + 1, 0);
                for (int c = 0; c < suDays.Count; c++)
                {
                    int col = c;
                    string v = "";
                    List<string> cells;
                    if (suCells.TryGetValue(suDays[c], out cells) && r < cells.Count) v = cells[r];
                    UIElement circ = null;
                    circ = SuCircle(v, delegate { SuCellClick(col, row, (FrameworkElement)circ); });
                    ((FrameworkElement)circ).HorizontalAlignment = HorizontalAlignment.Center;
                    ((FrameworkElement)circ).VerticalAlignment = VerticalAlignment.Center;
                    SuPut(g, circ, r + 1, c + 1);
                }
            }
            return UI.Card(g, 6);
        }

        void SuCellClick(int c, int r, FrameworkElement anchor)
        {
            List<string> opts = new List<string>();
            int nmax = Math.Max(9, suStart.Count);
            for (int n = 1; n <= nmax; n++) opts.Add("Period " + n);
            opts.Add("M"); opts.Add("Lunch"); opts.Add("Custom…"); opts.Add("Empty");
            string cur = suCells[suDays[c]][r];
            int sel = SuIsNum(cur) ? int.Parse(cur) - 1 : -1;
            if (sel >= nmax) sel = -1;
            int day = c, row = r;
            SuPick(anchor, opts.ToArray(), sel, delegate(int k)
            {
                List<string> colList = suCells[suDays[day]];
                if (k < nmax) colList[row] = (k + 1).ToString();
                else if (k == nmax) colList[row] = "M";
                else if (k == nmax + 1) colList[row] = "Lunch";
                else if (k == nmax + 3) colList[row] = "";
                else
                {
                    string name = SuNameDialog("Name this block", "For example Advisory, Homeroom or Flex.", SuIsNum(colList[row]) ? "" : colList[row]);
                    if (name != null) colList[row] = name;
                }
                SuRedo(true);
            });
        }

        void SuTimeClick(int r)
        {
            string s, e;
            if (SuTimeDialog(r + 1, suStart[r], suEnd[r], out s, out e))
            {
                suStart[r] = s; suEnd[r] = e;
                SuRedo(true);
            }
        }

        void SuHeadClick(int c, FrameworkElement anchor)
        {
            int day = c;
            SuPick(anchor, new string[] { "Rename", "Move left", "Remove" }, -1, delegate(int k)
            {
                string old = suDays[day];
                if (k == 0)
                {
                    string nv = SuNameDialog("Rename letter day", "What is this day called? A, B, Day 1...", old);
                    if (nv == null || nv == old) return;
                    if (suDays.Contains(nv)) { suError = "There is already a day called " + nv + "."; SuRedo(true); return; }
                    suDays[day] = nv;
                    suCells[nv] = suCells[old];
                    suCells.Remove(old);
                    if (suTodayDay == old) suTodayDay = nv;
                }
                else if (k == 1 && day > 0) { string t = suDays[day - 1]; suDays[day - 1] = suDays[day]; suDays[day] = t; }
                else if (k == 2 && suDays.Count > 1) { suDays.RemoveAt(day); suCells.Remove(old); if (suTodayDay == old) suTodayDay = suDays[0]; }
                SuRedo(true);
            });
        }

        // ------------------------------------------------------------ the screen
        void SuPageGrid(List<UIElement> kids)
        {
            SuHead(kids, "Your schedule", "Each column is a letter day and each row is a time slot. Click a circle to choose its period, click a time to change it, click a letter to rename it.");
            if (snap != null && snap.AutoBells.Count > 0 && cfg.GridDays.Count == 0)
                kids.Add(SuNotice("The letters and times below came from Campus. Change anything that is not right.", false));
            kids.Add(SuGridCard());

            WrapPanel bar = new WrapPanel();
            bar.Margin = new Thickness(0, 14, 0, 0);
            Button b1 = UI.Btn("Add row", "Ghost", delegate
            {
                TimeSpan e0 = new TimeSpan(15, 0, 0);
                if (suEnd.Count > 0) Bell.TryTime(suEnd[suEnd.Count - 1], out e0);
                TimeSpan s0 = e0.Add(TimeSpan.FromMinutes(5));
                suStart.Add(SuHM(s0)); suEnd.Add(SuHM(s0.Add(TimeSpan.FromMinutes(50))));
                SuGridNormalize(); SuRedo(true);
            });
            Button b2 = UI.Btn("Remove last row", "Ghost", delegate
            {
                if (suStart.Count > 1) { suStart.RemoveAt(suStart.Count - 1); suEnd.RemoveAt(suEnd.Count - 1); SuGridNormalize(); SuRedo(true); }
            });
            Button b3 = UI.Btn("Add letter day", "Ghost", delegate
            {
                string nx;
                if (suDays.Count > 0 && suDays[suDays.Count - 1].Length == 1 && char.IsLetter(suDays[suDays.Count - 1][0])) nx = ((char)(suDays[suDays.Count - 1][0] + 1)).ToString();
                else nx = (suDays.Count + 1).ToString();
                string nv = SuNameDialog("New letter day", "What is the new day called?", nx);
                if (nv == null) return;
                if (suDays.Contains(nv)) { suError = "There is already a day called " + nv + "."; SuRedo(true); return; }
                suDays.Add(nv);
                suCells[nv] = new List<string>(suCells[suDays[0]]);
                SuRedo(true);
            });
            foreach (Button b in new Button[] { b1, b2, b3 }) { b.Margin = new Thickness(0, 0, 10, 8); bar.Children.Add(b); }
            if (snap != null && snap.AutoBells.Count > 0)
            {
                Button b4 = UI.Btn("Import times from Campus", "Ghost", delegate
                {
                    BellSchedule src = null;
                    foreach (BellSchedule ab in snap.AutoBells) if (suDays.Count > 0 && ab.Name == suDays[0]) { src = ab; break; }
                    if (src == null) src = snap.AutoBells[0];
                    SuGridTimesFrom(src);
                    SuGridNormalize();
                    SuRedo(true);
                });
                b4.Margin = new Thickness(0, 0, 10, 8);
                bar.Children.Add(b4);
            }
            kids.Add(bar);

            // which letter is it today
            kids.Add(SuSection("Which letter is it today?"));
            bool campusOk = SuCampusLabels().Length >= 2;
            if (campusOk)
            {
                UIElement seg = SuSeg(new string[] { "Follow the Campus calendar", "I set it myself" }, suUseCampus ? 0 : 1, delegate(int k) { suUseCampus = k == 0; SuRedo(true); });
                ((FrameworkElement)seg).Margin = new Thickness(0, 0, 0, 12);
                kids.Add(seg);
            }
            else suUseCampus = false;
            if (!suUseCampus)
            {
                bool offToday = !Sched.IsSchoolDay(cfg, snap, DateTime.Today);
                StackPanel row = UI.Row();
                TextBlock lbl = UI.Text("Today (or the next school day) is:", 13.5, "T2", false);
                lbl.VerticalAlignment = VerticalAlignment.Center;
                lbl.Margin = new Thickness(0, 0, 14, 0);
                row.Children.Add(lbl);
                string[] dn = suDays.ToArray();
                Button dp = UI.Btn(suTodayDay + " day  ▾", "Ghost", null);
                dp.IsEnabled = !offToday;
                dp.Click += delegate
                {
                    SuPick(dp, dn, Array.IndexOf(dn, suTodayDay), delegate(int k) { suTodayDay = dn[k]; SuRedo(true); });
                };
                row.Children.Add(dp);
                kids.Add(row);
                kids.Add(SuNote(offToday ? "Today is not a school day, so the letter can't be changed right now." : "After that the letters follow your columns in order and skip weekends and days off."));
            }

            // rotating periods
            kids.Add(SuSection("Rotating the periods"));
            kids.Add(SuToggle("Rotate the periods for me (turn off to set every letter day by hand)", !suNoRot, delegate(bool v) { suNoRot = !v; SuRedo(true); }));
            if (!suNoRot)
            {
                kids.Add(SuNote("Type the periods for the first day, then let the rest shift automatically. Blocks like M and Lunch stay put. You can still click any circle afterwards."));
                kids.Add(SuSeg(new string[] { "Whole day", "Before and after lunch" }, suRotMode, delegate(int k) { suRotMode = k; SuRedo(true); }));
                UIElement dirSeg = SuSeg(new string[] { "Periods move later each day", "Periods move earlier each day" }, suRotDir, delegate(int k) { suRotDir = k; });
                ((FrameworkElement)dirSeg).Margin = new Thickness(0, 12, 0, 0);
                kids.Add(dirSeg);
                WrapPanel fields = new WrapPanel();
                fields.Margin = new Thickness(0, 14, 0, 0);
                fields.Children.Add(SuField("Rotate from row", suFirstS, 170, delegate(string v) { suFirstS = v; }));
                fields.Children.Add(SuField("to row", suLastS, 170, delegate(string v) { suLastS = v; }));
                if (suRotMode == 1) fields.Children.Add(SuField("Lunch after row", suLunchS, 170, delegate(string v) { suLunchS = v; }));
                kids.Add(fields);
                Button fill = UI.Btn("Fill the other days", "Pill", delegate { SuFillOtherDays(); SuRedo(true); });
                fill.HorizontalAlignment = HorizontalAlignment.Left;
                fill.Margin = new Thickness(0, 4, 0, 0);
                kids.Add(fill);
            }

            // last day of school
            kids.Add(SuSection("Last day of school"));
            kids.Add(SuToggle("Stop the schedule on the last day of school, then clear it", suEndOn, delegate(bool v) { suEndOn = v; SuRedo(true); }));
            if (suEndOn)
            {
                string[] months = new string[12];
                Array.Copy(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames, months, 12);
                int dim = DateTime.DaysInMonth(suEndDate.Year, suEndDate.Month);
                string[] dayNames = new string[dim];
                for (int i = 0; i < dim; i++) dayNames[i] = (i + 1).ToString();
                int y0 = DateTime.Today.Year;
                string[] yearNames = new string[] { y0.ToString(), (y0 + 1).ToString(), (y0 + 2).ToString() };
                Button pm = UI.Btn(months[suEndDate.Month - 1] + "  ▾", "Ghost", null);
                Button pd = UI.Btn(suEndDate.Day + "  ▾", "Ghost", null);
                Button py = UI.Btn(suEndDate.Year + "  ▾", "Ghost", null);
                pm.Click += delegate
                {
                    SuPick(pm, months, suEndDate.Month - 1, delegate(int k)
                    {
                        int dd = Math.Min(suEndDate.Day, DateTime.DaysInMonth(suEndDate.Year, k + 1));
                        suEndDate = new DateTime(suEndDate.Year, k + 1, dd);
                        SuRedo(true);
                    });
                };
                pd.Click += delegate
                {
                    SuPick(pd, dayNames, suEndDate.Day - 1, delegate(int k) { suEndDate = new DateTime(suEndDate.Year, suEndDate.Month, k + 1); SuRedo(true); });
                };
                py.Click += delegate
                {
                    SuPick(py, yearNames, Array.IndexOf(yearNames, suEndDate.Year.ToString()), delegate(int k)
                    {
                        int yy = y0 + k;
                        int dd = Math.Min(suEndDate.Day, DateTime.DaysInMonth(yy, suEndDate.Month));
                        suEndDate = new DateTime(yy, suEndDate.Month, dd);
                        SuRedo(true);
                    });
                };
                StackPanel dateRow = UI.Row(pm, pd, py);
                pm.Margin = new Thickness(0, 6, 10, 0);
                pd.Margin = new Thickness(0, 6, 10, 0);
                py.Margin = new Thickness(0, 6, 0, 0);
                kids.Add(dateRow);
                kids.Add(SuNote("After this day your schedule is cleared. You set it up again next year."));
            }

            SuFooter(kids, "Next", delegate
            {
                string err = SuGridCheck();
                if (err.Length > 0) { suError = err; return false; }
                SuGridApply();
                return true;
            });
        }
    }
}
