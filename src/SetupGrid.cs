using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CampusMidnight
{
    // Step 1 of the setup: the rotation grid. Columns are letter days, rows are time slots, circles are periods.
    partial class MainForm
    {
        const int SetupVersion = 7;
        List<string> gDays = new List<string>();
        List<string> gStart = new List<string>(), gEnd = new List<string>();
        Dictionary<string, List<string>> gCells = new Dictionary<string, List<string>>();
        string wFirstS = "1", wLastS = "7", wLunchS = "4";
        int wRotMode, wRotDir;
        bool wUseCampus;
        string wTodayDay = "";
        string wAutoNote = "";
        bool wNoRot, wEndOn;
        DateTime wEnd = DateTime.Today;

        static string HM(TimeSpan t) { return t.ToString(@"hh\:mm"); }

        void GridLoad()
        {
            gDays = new List<string>(cfg.GridDays);
            gStart = new List<string>(cfg.GridStarts);
            gEnd = new List<string>(cfg.GridEnds);
            gCells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in cfg.GridCells) gCells[kv.Key] = new List<string>(kv.Value);
            if (gDays.Count == 0 || gStart.Count == 0) GridSeed();
            GridNormalize();
            wAutoNote = "";
            wRotMode = 0; wRotDir = 0;
            wFirstS = "1"; wLastS = gStart.Count.ToString(); wLunchS = Math.Max(1, gStart.Count / 2).ToString();
            wUseCampus = !cfg.RotOn && CampusLabelsInOrder().Length >= 2;
            wNoRot = cfg.NoRotation;
            DateTime pe;
            wEndOn = cfg.SchoolEnd.Length > 0 && DateTime.TryParseExact(cfg.SchoolEnd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out pe);
            if (wEndOn) wEnd = pe;
            else wEnd = new DateTime(DateTime.Today.Month >= 8 ? DateTime.Today.Year + 1 : DateTime.Today.Year, 5, 31);
            wTodayDay = "";
            if (cfg.RotOn && snap != null)
            {
                string from;
                string lab = Sched.LabelFor(cfg, snap, DateTime.Today, out from);
                if (gDays.Contains(lab)) wTodayDay = lab;
            }
            if (wTodayDay.Length == 0 && gDays.Count > 0) wTodayDay = gDays[0];
        }

        // letters and times from Campus when it shared them, else a plain A/B day with seven periods
        void GridSeed()
        {
            gDays.Clear(); gStart.Clear(); gEnd.Clear(); gCells.Clear();
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
            gDays.AddRange(letters);
            if (src != null) GridTimesFrom(src);
            if (gStart.Count == 0)
            {
                TimeSpan t = new TimeSpan(8, 0, 0);
                for (int i = 0; i < 7; i++)
                {
                    gStart.Add(HM(t));
                    t = t.Add(TimeSpan.FromMinutes(50));
                    gEnd.Add(HM(t));
                    t = t.Add(TimeSpan.FromMinutes(5));
                }
            }
            foreach (string d in gDays)
            {
                List<string> c = new List<string>();
                for (int i = 0; i < gStart.Count; i++) c.Add((i + 1).ToString());
                gCells[d] = c;
            }
        }

        void GridTimesFrom(BellSchedule src)
        {
            List<Bell> ib = new List<Bell>();
            foreach (Bell b in src.Entries) if (!b.NonInstr) ib.Add(b);
            ib.Sort(delegate(Bell x, Bell y) { return x.Start.CompareTo(y.Start); });
            if (ib.Count == 0) return;
            gStart.Clear(); gEnd.Clear();
            foreach (Bell b in ib) { gStart.Add(HM(b.Start)); gEnd.Add(HM(b.End)); }
        }

        void GridNormalize()
        {
            while (gEnd.Count < gStart.Count) gEnd.Add(gStart[gEnd.Count]);
            while (gEnd.Count > gStart.Count) gEnd.RemoveAt(gEnd.Count - 1);
            foreach (string d in gDays)
            {
                List<string> c;
                if (!gCells.TryGetValue(d, out c)) { c = new List<string>(); gCells[d] = c; }
                while (c.Count < gStart.Count) c.Add("");
                while (c.Count > gStart.Count) c.RemoveAt(c.Count - 1);
            }
            List<string> gone = new List<string>();
            foreach (string k in gCells.Keys) if (!gDays.Contains(k)) gone.Add(k);
            foreach (string k in gone) gCells.Remove(k);
        }

        static bool IsNum(string s)
        {
            int n;
            return s != null && int.TryParse(s, out n) && n > 0;
        }

        List<string> GridNumbers()
        {
            List<int> nums = new List<int>();
            foreach (KeyValuePair<string, List<string>> kv in gCells)
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

        static string Short(string hm)
        {
            TimeSpan t;
            if (!Bell.TryTime(hm, out t)) return hm;
            return DateTime.Today.Add(t).ToString("h:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        string GridTimesFor(string num)
        {
            foreach (string d in gDays)
            {
                List<string> c;
                if (!gCells.TryGetValue(d, out c)) continue;
                for (int i = 0; i < c.Count && i < gStart.Count; i++)
                    if (c[i] == num) return Short(gStart[i]) + " - " + Short(gEnd[i]) + "  (" + d + ")";
            }
            return "";
        }

        string GridCheck()
        {
            if (gDays.Count == 0) return "Add at least one letter day.";
            if (gStart.Count == 0) return "Add at least one row.";
            for (int i = 0; i < gStart.Count; i++)
            {
                TimeSpan a, b;
                if (!Bell.TryTime(gStart[i], out a) || !Bell.TryTime(gEnd[i], out b) || b <= a) return "Row " + (i + 1) + " has a start or end time that is not valid. Click its time to fix it.";
            }
            return "";
        }

        // turns the grid into the bell schedules the rest of the app reads
        void GridApply()
        {
            GridNormalize();
            cfg.GridDays = new List<string>(gDays);
            cfg.GridStarts = new List<string>(gStart);
            cfg.GridEnds = new List<string>(gEnd);
            cfg.GridCells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in gCells) cfg.GridCells[kv.Key] = new List<string>(kv.Value);
            cfg.Schedules.Clear();
            cfg.DayScheds.Clear();
            foreach (string d in gDays)
            {
                BellSchedule bs = new BellSchedule();
                bs.Name = d;
                List<string> c = gCells[d];
                for (int i = 0; i < c.Count; i++)
                {
                    if (c[i].Trim().Length == 0) continue;
                    TimeSpan a, e;
                    if (!Bell.TryTime(gStart[i], out a) || !Bell.TryTime(gEnd[i], out e)) continue;
                    Bell b = new Bell();
                    b.Label = c[i].Trim(); b.Start = a; b.End = e;
                    bs.Entries.Add(b);
                }
                cfg.Schedules.Add(bs);
                cfg.DayScheds[d] = d;
            }
            cfg.RotLabels = string.Join(",", gDays.ToArray());
            cfg.NoRotation = wNoRot;
            cfg.SchoolEnd = wEndOn ? wEnd.ToString("yyyy-MM-dd") : "";
            cfg.RotOn = !wUseCampus;
            // on weekends and days off the letter is not changed; the saved one keeps counting
            bool keepAnchor = cfg.RotOn && cfg.RotAnchor.Length > 0 && !Sched.IsSchoolDay(cfg, snap, DateTime.Today);
            if (cfg.RotOn && !keepAnchor)
            {
                DateTime a0 = DateTime.Today;
                if (!Sched.IsSchoolDay(cfg, snap, a0)) a0 = Sched.NextSchoolDay(cfg, snap, a0);
                cfg.RotAnchor = a0.ToString("yyyy-MM-dd");
                cfg.RotIndex = Math.Max(0, gDays.IndexOf(wTodayDay));
            }
            // class choices are per period number now; drop the old per-day and per-time choices
            List<string> old = new List<string>();
            foreach (string k in cfg.PeriodMap.Keys) if (!k.EndsWith("#")) old.Add(k);
            foreach (string k in old) cfg.PeriodMap.Remove(k);
            cfg.Save();
            Log.Write("setup: grid saved, days=" + string.Join(",", gDays.ToArray()) + " rows=" + gStart.Count + " rotation=" + cfg.RotOn);
        }

        // the period number a class sits in, found from where Campus puts it on that letter day's grid column
        string NumberFor(Placement p)
        {
            string letter = null;
            foreach (string d in gDays) if (string.Equals(d, p.SchedName, StringComparison.OrdinalIgnoreCase)) { letter = d; break; }
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
            List<string> col = gCells[letter];
            for (int i = 0; i < gStart.Count && i < col.Count; i++)
            {
                TimeSpan rs;
                if (!Bell.TryTime(gStart[i], out rs)) continue;
                if (Math.Abs((rs - start).TotalMinutes) <= 4 && IsNum(col[i])) return col[i];
            }
            return null;
        }

        // only runs when the Auto fill button is pressed
        void AutoFillClasses()
        {
            wAutoNote = "";
            if (snap == null) return;
            GridNormalize();
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
                    num = NumberFor(p);
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
            wAutoNote = "Filled " + filled + " period" + (filled == 1 ? "" : "s") + " from Campus." + (missed.Count > 0 ? " Could not place: " + string.Join(", ", missed.ToArray()) + ". Pick those by hand." : "");
            Log.Write("setup: " + wAutoNote);
        }

        // moves the periods inside a group of rows; blocks like M and Lunch stay where they are
        static List<string> RotateCells(List<string> src, int shift, int mode, int first, int last, int lunchRow)
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
                foreach (int i in g) if (IsNum(src[i])) idx.Add(i);
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

        void FillOtherDays()
        {
            int f, l, lu;
            if (!int.TryParse(wFirstS.Trim(), out f)) f = 1;
            if (!int.TryParse(wLastS.Trim(), out l)) l = gStart.Count;
            if (!int.TryParse(wLunchS.Trim(), out lu)) lu = gStart.Count / 2;
            f = Math.Max(1, f); l = Math.Min(gStart.Count, Math.Max(f, l)); lu = Math.Max(f, Math.Min(l, lu));
            List<string> baseCells = gCells[gDays[0]];
            for (int di = 1; di < gDays.Count; di++)
                gCells[gDays[di]] = RotateCells(baseCells, wRotDir == 0 ? di : -di, wRotMode, f - 1, l - 1, lu);
        }

        // ------------------------------------------------------------ the screen
        void WizGrid()
        {
            WizHead("Your schedule", "Each column is a letter day and each row is a time slot. Click a circle to choose its period, click a time to change it, click a letter to rename it.");
            if (snap != null && snap.AutoBells.Count > 0 && cfg.GridDays.Count == 0)
                AddNotice("The letters and times below came from Campus. Change anything that is not right.");

            GridCard gc = new GridCard();
            gc.Days = gDays; gc.Starts = gStart; gc.Ends = gEnd; gc.Cells = gCells;
            GridCard gself = gc;
            gc.CellClick = delegate(int c, int r, Point at)
            {
                List<string> opts = new List<string>();
                int nmax = Math.Max(9, gStart.Count);
                for (int n = 1; n <= nmax; n++) opts.Add("Period " + n);
                opts.Add("M"); opts.Add("Lunch"); opts.Add("Custom…"); opts.Add("Empty");
                string cur = gCells[gDays[c]][r];
                int sel = IsNum(cur) ? int.Parse(cur) - 1 : -1;
                if (sel >= nmax) sel = -1;
                string[] arr = opts.ToArray();
                int day = c, row = r;
                DarkMenu.PickAt(gself, at, arr, sel, delegate(int k)
                {
                    List<string> col = gCells[gDays[day]];
                    if (k < nmax) col[row] = (k + 1).ToString();
                    else if (k == nmax) col[row] = "M";
                    else if (k == nmax + 1) col[row] = "Lunch";
                    else if (k == nmax + 3) col[row] = "";
                    else
                    {
                        using (NameForm f = new NameForm("Name this block", "For example Advisory, Homeroom or Flex.", IsNum(col[row]) ? "" : col[row]))
                            if (f.ShowDialog(this) == DialogResult.OK) col[row] = f.Value;
                    }
                    Redo();
                });
            };
            gc.TimeClick = delegate(int r)
            {
                using (TimeForm f = new TimeForm(r + 1, gStart[r], gEnd[r]))
                    if (f.ShowDialog(this) == DialogResult.OK) { gStart[r] = f.StartText; gEnd[r] = f.EndText; Redo(); }
            };
            gc.HeadClick = delegate(int c, Point at)
            {
                int day = c;
                DarkMenu.PickAt(gself, at, new string[] { "Rename", "Move left", "Remove" }, -1, delegate(int k)
                {
                    string old = gDays[day];
                    if (k == 0)
                    {
                        using (NameForm f = new NameForm("Rename letter day", "What is this day called? A, B, Day 1...", old))
                        {
                            if (f.ShowDialog(this) != DialogResult.OK) return;
                            if (f.Value != old && gDays.Contains(f.Value)) { wError = "There is already a day called " + f.Value + "."; Redo(); return; }
                            gDays[day] = f.Value;
                            gCells[f.Value] = gCells[old];
                            gCells.Remove(old);
                            if (wTodayDay == old) wTodayDay = f.Value;
                        }
                    }
                    else if (k == 1 && day > 0) { string t = gDays[day - 1]; gDays[day - 1] = gDays[day]; gDays[day] = t; }
                    else if (k == 2 && gDays.Count > 1) { gDays.RemoveAt(day); gCells.Remove(old); if (wTodayDay == old) wTodayDay = gDays[0]; }
                    Redo();
                });
            };
            int gh = gc.PreferredHeight();
            AddCard(gc, X0(), vy, CW(), gh);
            vy += gh + Theme.S(14);

            int x = X0();
            PillButton b1 = AddButton("Add row", "", 0, x, vy, delegate
            {
                TimeSpan e0 = new TimeSpan(15, 0, 0);
                if (gEnd.Count > 0) Bell.TryTime(gEnd[gEnd.Count - 1], out e0);
                TimeSpan s0 = e0.Add(TimeSpan.FromMinutes(5));
                gStart.Add(HM(s0)); gEnd.Add(HM(s0.Add(TimeSpan.FromMinutes(50))));
                GridNormalize(); Redo();
            });
            x += b1.Width + Theme.S(10);
            PillButton b2 = AddButton("Remove last row", "", 0, x, vy, delegate
            {
                if (gStart.Count > 1) { gStart.RemoveAt(gStart.Count - 1); gEnd.RemoveAt(gEnd.Count - 1); GridNormalize(); Redo(); }
            });
            x += b2.Width + Theme.S(10);
            {
            PillButton b3 = AddButton("Add letter day", "", 0, x, vy, delegate
            {
                string nx = "A";
                if (gDays.Count > 0 && gDays[gDays.Count - 1].Length == 1 && char.IsLetter(gDays[gDays.Count - 1][0])) nx = ((char)(gDays[gDays.Count - 1][0] + 1)).ToString();
                else nx = (gDays.Count + 1).ToString();
                using (NameForm f = new NameForm("New letter day", "What is the new day called?", nx))
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    if (gDays.Contains(f.Value)) { wError = "There is already a day called " + f.Value + "."; Redo(); return; }
                    gDays.Add(f.Value);
                    gCells[f.Value] = new List<string>(gCells[gDays[0]]);
                }
                Redo();
            });
            x += b3.Width + Theme.S(10);
            }
            if (snap != null && snap.AutoBells.Count > 0)
            {
                AddButton("Import times from Campus", "", 0, x, vy, delegate
                {
                    BellSchedule src = null;
                    foreach (BellSchedule ab in snap.AutoBells) if (gDays.Count > 0 && ab.Name == gDays[0]) { src = ab; break; }
                    if (src == null) src = snap.AutoBells[0];
                    GridTimesFrom(src);
                    GridNormalize();
                    Redo();
                });
            }
            vy += Theme.S(64);

            {
            SectionTitle("Which letter is it today?");
            Segmented src2 = new Segmented();
            bool campusOk = CampusLabelsInOrder().Length >= 2;
            if (campusOk)
            {
                src2.SetOptions(new string[] { "Follow the Campus calendar", "I set it myself" }, wUseCampus ? 0 : 1);
                src2.Changed += delegate { wUseCampus = src2.Selected == 0; Redo(); };
                src2.Location = new Point(X0(), vy);
                host.Controls.Add(src2);
                vy += src2.Height + Theme.S(14);
            }
            else wUseCampus = false;
            if (!wUseCampus)
            {
                AddLabel("Today (or the next school day) is:", Theme.Body, Theme.T2, X0(), vy + Theme.S(8), Theme.S(250), Theme.S(26));
                string[] dn = gDays.ToArray();
                PillButton dp = AddButton(wTodayDay + " day", "", 0, X0() + Theme.S(256), vy, null);
                PillButton dps = dp;
                bool offToday = !Sched.IsSchoolDay(cfg, snap, DateTime.Today);
                dp.Enabled = !offToday;
                dp.Click += delegate
                {
                    DarkMenu.Pick(dps, dn, Array.IndexOf(dn, wTodayDay), delegate(int k) { wTodayDay = dn[k]; Redo(); });
                };
                vy += Theme.S(58);
                AddLabel(!Sched.IsSchoolDay(cfg, snap, DateTime.Today) ? "Today is not a school day, so the letter can't be changed right now." : "After that the letters follow your columns in order and skip weekends and days off.", Theme.Small, Theme.T3, X0(), vy - Theme.S(8), CW(), Theme.S(24));
                vy += Theme.S(30);
            }
            vy += Theme.S(10);

            SectionTitle("Rotating the periods");
            AddToggle("Rotate the periods for me (turn off to set every letter day by hand)", !wNoRot, delegate(bool v) { wNoRot = !v; Redo(); });
            vy += Theme.S(8);
            if (!wNoRot)
            {
            AddLabel("Type the periods for the first day, then let the rest shift automatically. Blocks like M and Lunch stay put. You can still click any circle afterwards.", Theme.Body, Theme.T2, X0(), vy - Theme.S(6), CW(), Theme.S(44));
            vy += Theme.S(52);
            Segmented rm = new Segmented();
            rm.SetOptions(new string[] { "Whole day", "Before and after lunch" }, wRotMode);
            rm.Changed += delegate { wRotMode = rm.Selected; Redo(); };
            rm.Location = new Point(X0(), vy);
            host.Controls.Add(rm);
            vy += rm.Height + Theme.S(12);
            Segmented rd = new Segmented();
            rd.SetOptions(new string[] { "Periods move later each day", "Periods move earlier each day" }, wRotDir);
            rd.Changed += delegate { wRotDir = rd.Selected; };
            rd.Location = new Point(X0(), vy);
            host.Controls.Add(rd);
            vy += rd.Height + Theme.S(14);
            int gap = Theme.S(14);
            int fw = Math.Min(Theme.S(200), (CW() - gap * 2) / 3);
            WizField("Rotate from row", wFirstS, X0(), fw, delegate(string v) { wFirstS = v; });
            WizField("to row", wLastS, X0() + fw + gap, fw, delegate(string v) { wLastS = v; });
            if (wRotMode == 1) WizField("Lunch after row", wLunchS, X0() + (fw + gap) * 2, fw, delegate(string v) { wLunchS = v; });
            vy += Theme.S(78);
            AddButton("Fill the other days", "", 1, X0(), vy, delegate { FillOtherDays(); Redo(); });
            vy += Theme.S(64);
            }

            }

            SectionTitle("Last day of school");
            AddToggle("Stop the schedule on the last day of school, then clear it", wEndOn, delegate(bool v) { wEndOn = v; Redo(); });
            if (wEndOn)
            {
                string[] months = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;
                string[] mn = new string[12];
                Array.Copy(months, mn, 12);
                int dim = DateTime.DaysInMonth(wEnd.Year, wEnd.Month);
                string[] dn2 = new string[dim];
                for (int i = 0; i < dim; i++) dn2[i] = (i + 1).ToString();
                int y0 = DateTime.Today.Year;
                string[] yn = new string[] { y0.ToString(), (y0 + 1).ToString(), (y0 + 2).ToString() };
                PillButton pm = AddButton(mn[wEnd.Month - 1], "", 0, X0(), vy, null);
                PillButton pd = AddButton(wEnd.Day.ToString(), "", 0, X0() + pm.Width + Theme.S(10), vy, null);
                PillButton py = AddButton(wEnd.Year.ToString(), "", 0, X0() + pm.Width + pd.Width + Theme.S(20), vy, null);
                PillButton pms = pm, pds = pd, pys = py;
                pm.Click += delegate
                {
                    DarkMenu.Pick(pms, mn, wEnd.Month - 1, delegate(int k)
                    {
                        int dd = Math.Min(wEnd.Day, DateTime.DaysInMonth(wEnd.Year, k + 1));
                        wEnd = new DateTime(wEnd.Year, k + 1, dd); Redo();
                    });
                };
                pd.Click += delegate
                {
                    DarkMenu.Pick(pds, dn2, wEnd.Day - 1, delegate(int k) { wEnd = new DateTime(wEnd.Year, wEnd.Month, k + 1); Redo(); });
                };
                py.Click += delegate
                {
                    DarkMenu.Pick(pys, yn, Array.IndexOf(yn, wEnd.Year.ToString()), delegate(int k)
                    {
                        int yy = y0 + k;
                        int dd = Math.Min(wEnd.Day, DateTime.DaysInMonth(yy, wEnd.Month));
                        wEnd = new DateTime(yy, wEnd.Month, dd); Redo();
                    });
                };
                vy += Theme.S(54);
                AddLabel("After this day your schedule is cleared. You set it up again next year.", Theme.Small, Theme.T3, X0(), vy - Theme.S(6), CW(), Theme.S(24));
                vy += Theme.S(30);
            }
            vy += Theme.S(10);

            WizFooter("Next", delegate
            {
                string err = GridCheck();
                if (err.Length > 0) { wError = err; return false; }
                GridApply();
                return true;
            });
        }
    }

    // draws the letter-day grid
    class GridCard : Control
    {
        public List<string> Days, Starts, Ends;
        public Dictionary<string, List<string>> Cells;
        public Action<int, int, Point> CellClick;
        public Action<int> TimeClick;
        public Action<int, Point> HeadClick;
        int hotC = -1, hotR = -1;

        public GridCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        int LeftW { get { return Theme.S(150); } }
        int HeadH { get { return Theme.S(46); } }
        int RowH { get { return Theme.S(56); } }

        public int PreferredHeight() { return HeadH + Starts.Count * RowH + Theme.S(14); }

        int ColW()
        {
            if (Days.Count == 0) return 0;
            return Math.Min(Theme.S(140), (Width - LeftW - Theme.S(14)) / Days.Count);
        }

        void Hit(Point p, out int col, out int row)
        {
            col = -1; row = -1;
            int cw = ColW();
            if (p.X >= LeftW && cw > 0) { int c = (p.X - LeftW) / cw; if (c >= 0 && c < Days.Count) col = c; }
            if (p.Y >= HeadH) { int r = (p.Y - HeadH) / RowH; if (r >= 0 && r < Starts.Count) row = r; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int c, r;
            Hit(e.Location, out c, out r);
            if (c != hotC || r != hotR) { hotC = c; hotR = r; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotC = -1; hotR = -1; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int c, r;
            Hit(e.Location, out c, out r);
            int cw = ColW();
            Point at = new Point(Math.Max(0, e.X), Math.Max(0, e.Y));
            if (e.Y < HeadH && c >= 0) { if (HeadClick != null) HeadClick(c, at); }
            else if (e.X < LeftW && r >= 0) { if (TimeClick != null) TimeClick(r); }
            else if (c >= 0 && r >= 0) { if (CellClick != null) CellClick(c, r, at); }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg0);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle all = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, all, Theme.S(14), Theme.Bg2);
            Theme.StrokeRound(g, all, Theme.S(14), Theme.Line, 1f);
            int cw = ColW();
            for (int c = 0; c < Days.Count; c++)
            {
                Rectangle hr = new Rectangle(LeftW + c * cw, 0, cw, HeadH);
                Theme.DrawText(g, Days[c], Theme.Semi, hotC == c && hotR < 0 ? Theme.AccText : Theme.T1, hr, false, true);
            }
            g.DrawLine(new Pen(Theme.Line), Theme.S(12), HeadH, Width - Theme.S(12), HeadH);
            int d = Theme.S(38);
            for (int r = 0; r < Starts.Count; r++)
            {
                int y = HeadH + r * RowH;
                Rectangle tr = new Rectangle(Theme.S(18), y, LeftW - Theme.S(18), RowH);
                Theme.DrawText(g, Short(Starts[r]) + " - " + Short(Ends[r]), Theme.Small, Theme.T2, tr);
                for (int c = 0; c < Days.Count; c++)
                {
                    List<string> col;
                    string v = "";
                    if (Cells.TryGetValue(Days[c], out col) && r < col.Count) v = col[r];
                    Rectangle cell = new Rectangle(LeftW + c * cw, y, cw, RowH);
                    Rectangle circ = new Rectangle(cell.X + (cw - d) / 2, y + (RowH - d) / 2, d, d);
                    bool hot = hotC == c && hotR == r;
                    int num;
                    bool isNum = int.TryParse(v, out num) && num > 0;
                    if (isNum)
                    {
                        using (SolidBrush b = new SolidBrush(hot ? Theme.AccHover : Theme.Acc)) g.FillEllipse(b, circ);
                        Theme.DrawText(g, v, Theme.Semi, Color.White, circ, false, true);
                    }
                    else if (v.Length > 0)
                    {
                        using (SolidBrush b = new SolidBrush(hot ? Theme.Bg5 : Theme.Bg4)) g.FillEllipse(b, circ);
                        Theme.DrawText(g, v.Length > 5 ? v.Substring(0, 4) + "…" : v, v.Length > 2 ? Theme.SemiSmall : Theme.Semi, Theme.T3, circ, false, true);
                    }
                    else
                    {
                        using (Pen p = new Pen(hot ? Theme.AccText : Theme.Line2, 1.5f)) g.DrawEllipse(p, circ);
                    }
                }
            }
        }

        static string Short(string hm)
        {
            TimeSpan t;
            if (!Bell.TryTime(hm, out t)) return hm;
            return DateTime.Today.Add(t).ToString("h:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // change when a row starts and ends
    class TimeForm : DarkForm
    {
        public string StartText = "", EndText = "";
        readonly DarkInput iStart, iEnd;

        public TimeForm(int row, string start, string end) : base("Row " + row + " times")
        {
            ClientSize = new Size(Theme.S(480), Theme.S(330));
            Fw = ClientSize.Width - Theme.S(56);
            Lbl("When does this row start and end? For example 8:00 AM and 8:50 AM.", Theme.Body, Theme.T2, Theme.S(26));
            Gap(10);
            Lbl("STARTS", Theme.SemiSmall, Theme.T3, Theme.S(22));
            iStart = new DarkInput(false, false);
            TimeSpan t;
            iStart.Text = Bell.TryTime(start, out t) ? Bell.Clock(t) : start;
            Add(iStart, Theme.S(44));
            Gap(10);
            Lbl("ENDS", Theme.SemiSmall, Theme.T3, Theme.S(22));
            iEnd = new DarkInput(false, false);
            iEnd.Text = Bell.TryTime(end, out t) ? Bell.Clock(t) : end;
            Add(iEnd, Theme.S(44));
            Gap(18);
            PillButton ok = new PillButton(); ok.Text = "Save"; ok.Kind = 1;
            ok.Click += delegate { Done(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
        }

        void Done()
        {
            TimeSpan a, b;
            bool okA = Bell.TryTime(iStart.Text, out a), okB = Bell.TryTime(iEnd.Text, out b);
            iStart.Invalid = !okA; iEnd.Invalid = !okB;
            iStart.Invalidate(); iEnd.Invalidate();
            if (!okA || !okB) return;
            if (b <= a) { iEnd.Invalid = true; iEnd.Invalidate(); return; }
            StartText = a.ToString(@"hh\:mm"); EndText = b.ToString(@"hh\:mm");
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
