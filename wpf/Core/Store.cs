using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CampusMidnight
{
    // Remembers the sign-in link and username. The password is saved only if the user ticks
    // "Stay signed in", and then only encrypted with Windows DPAPI for this Windows account.
    class Settings
    {
        public string Url = "";
        public string User = "";
        public string EncPass = "";
        public bool Stay;
        public string Theme = "Midnight";
        public int Interval = 15;       // auto-refresh minutes, 0 = off
        public string Bells = "";       // legacy plain-text bell times
        public double Goal = 90;
        public string Mode = "system";  // system | light | dark
        public bool RailCollapsed;
        public bool SetupDone, ThemesOpen = true, HidePhoto;
        public bool NoRotation;          // student turned rotating letter days off
        public string SchoolEnd = "";    // yyyy-MM-dd, last day of school; "" = not set
        public int SetupVer;             // bumped when the setup is redesigned so it shows once more
        // the schedule grid: columns are letter days, rows are time slots, cells are a period number or a block name
        public List<string> GridDays = new List<string>();
        public List<string> GridStarts = new List<string>(), GridEnds = new List<string>();   // "08:00" per row
        public Dictionary<string, List<string>> GridCells = new Dictionary<string, List<string>>();
        // saved alternate time sets (half day, assembly...): name -> "HH:mm-HH:mm" per grid row, "" = row skipped that day
        public Dictionary<string, List<string>> Variants = new Dictionary<string, List<string>>();
        // date (yyyy-MM-dd) -> "none" (no school) or the name of a schedule
        public Dictionary<string, string> DayTypes = new Dictionary<string, string>();
        public bool CountDropped;        // count dropped classes in GPA
        public bool NumbersOnly;         // show a rounded number instead of a letter
        public string CustomBase = "#14161c", CustomAccent = "#7aa2ff", CustomText = "";   // the Custom (beta) theme
        public bool NotifyStart = true, NotifyEnd = true, TrayOnClose, NoTray, Legacy, Unlocked, FsSeen, AbsDays;
        public Dictionary<string, string> DayLetters = new Dictionary<string, string>();   // date -> letter day set by hand for that one day
        public double Zoom = 1.0;
        public List<string> Countdowns = new List<string>();   // "name~yyyy-MM-dd"
        public int PeriodNames;      // 0 = as Campus names them, 1 = Period 1, Period 2
        public string RegularName = "Regular";
        public List<GridSet> Sets = new List<GridSet>();     // extra schedules (half day...) besides the main one
        public bool NoLetters;                                // the school does not use letter days
        public bool NotifyNew = true;     // tell me when a new assignment or grade shows up
        public int NotifyLead = 2;       // minutes before a class starts
        public string NavOrder = "", NavHide = "";
        public Dictionary<string, string> PeriodMap = new Dictionary<string, string>();   // "schedule~period~day" -> course slug ("-" = none)
        public Dictionary<string, string> DayScheds = new Dictionary<string, string>();   // day label -> schedule name
        public List<BellSchedule> Schedules = new List<BellSchedule>();
        public string[] WeekMap = new string[7];      // schedule name per weekday, "" = the first schedule
        public string OverrideDate = "", OverrideName = "";
        public bool RotOn;
        public string RotLabels = "A,B";
        public string RotAnchor = "";                 // yyyy-MM-dd
        public int RotIndex;
        public List<string> NoSchool = new List<string>();      // yyyy-MM-dd, typed by the student
        public Dictionary<string, string> CourseDays = new Dictionary<string, string>();   // course key -> day label
        public Dictionary<string, int> Levels = new Dictionary<string, int>();             // course key -> 0/1/2

        public Settings()
        {
            for (int i = 0; i < WeekMap.Length; i++) WeekMap[i] = "";
        }

        static string Clean(string t)
        {
            return (t ?? "").Replace("|", " ").Replace(";", " ").Replace("~", " ").Replace("=", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        public static string Slug(string course)
        {
            return Clean(Norm.Key(course));
        }

        // a period can hold several classes, one for each quarter or semester ("english+biology"); these read and change that list
        public static bool HasSlug(string value, string slug)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (string t in value.Split('+')) if (t == slug) return true;
            return false;
        }

        // the list with this term's own classes taken out and `chosen` put in (null: just take them out)
        public static string WithSlug(string value, List<string> termSlugs, string chosen)
        {
            List<string> keep = new List<string>();
            if (!string.IsNullOrEmpty(value))
                foreach (string t in value.Split('+')) if (t.Length > 0 && t != "-" && !termSlugs.Contains(t) && !keep.Contains(t)) keep.Add(t);
            if (chosen != null && !keep.Contains(chosen)) keep.Add(chosen);
            if (chosen == "-" && keep.Count > 1) keep.Remove("-");
            return string.Join("+", keep.ToArray());
        }

        public static string PeriodKey(string schedule, string period, string day)
        {
            // the same period label means the same thing in every schedule, so the schedule name is not part of the key
            return Clean(period) + "#" + Clean(day);
        }

        public BellSchedule Find(string name)
        {
            foreach (BellSchedule b in Schedules) if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        public BellSchedule ForDate(DateTime d)
        {
            if (Schedules.Count == 0) return null;
            if (OverrideDate == d.ToString("yyyy-MM-dd") && OverrideName.Length > 0)
            {
                BellSchedule o = Find(OverrideName);
                if (o != null) return o;
            }
            string n = WeekMap[(int)d.DayOfWeek];
            BellSchedule w = n.Length > 0 ? Find(n) : null;
            return w ?? Schedules[0];
        }

        public int LevelFor(string course)
        {
            int v;
            if (Levels.TryGetValue(Slug(course), out v)) return v;
            return -1;
        }

        static string FilePath()
        {
            return Disk.At("settings.txt");
        }

        public static Settings Load() { return LoadFrom(FilePath()); }

        // the real schedule is parked here while a demo account is signed in
        public static string StashPath() { return Disk.At("schedule-stash.txt"); }

        public void StashSchedule()
        {
            try { File.Copy(FilePath(), StashPath(), true); } catch (Exception) { }
        }

        // brings the parked schedule back; false when there was nothing parked
        public bool RestoreSchedule()
        {
            try
            {
                if (!File.Exists(StashPath())) return false;
                Settings o = LoadFrom(StashPath());
                GridDays = o.GridDays; GridStarts = o.GridStarts; GridEnds = o.GridEnds; GridCells = o.GridCells;
                Variants = o.Variants; DayTypes = o.DayTypes; NoLetters = o.NoLetters; RegularName = o.RegularName;
                Sets = o.Sets; PeriodMap = o.PeriodMap; DayScheds = o.DayScheds; Schedules = o.Schedules;
                WeekMap = o.WeekMap; OverrideDate = o.OverrideDate; OverrideName = o.OverrideName;
                RotOn = o.RotOn; RotLabels = o.RotLabels; RotAnchor = o.RotAnchor; RotIndex = o.RotIndex;
                NoSchool = o.NoSchool; CourseDays = o.CourseDays; Levels = o.Levels; NoRotation = o.NoRotation;
                SchoolEnd = o.SchoolEnd; SetupDone = o.SetupDone; SetupVer = o.SetupVer; DayLetters = o.DayLetters;
                PeriodNames = o.PeriodNames;
                File.Delete(StashPath());
                return true;
            }
            catch (Exception) { return false; }
        }

        static Settings LoadFrom(string p)
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(p)) return s;
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i);
                    string v = line.Substring(i + 1);
                    if (k == "url") s.Url = v;
                    else if (k == "user") s.User = v;
                    else if (k == "pass") s.EncPass = v;
                    else if (k == "stay") s.Stay = v == "1";
                    else if (k == "theme" && v.Length > 0) s.Theme = v;
                    else if (k == "ctheme") { string[] ct = v.Split('|'); if (ct.Length >= 3) { s.CustomBase = ct[0]; s.CustomAccent = ct[1]; s.CustomText = ct[2]; } }
                    else if (k == "interval") { int iv; if (int.TryParse(v, out iv) && iv >= 0 && iv <= 240) s.Interval = iv; }
                    else if (k == "bells") s.Bells = v;
                    else if (k == "mode" && (v == "light" || v == "dark" || v == "system")) s.Mode = v;
                    else if (k == "rail") s.RailCollapsed = v == "1";
                    else if (k == "sched") s.ReadSchedule(v);
                    else if (k == "week") { string[] w = v.Split('|'); for (int wi = 0; wi < 7 && wi < w.Length; wi++) s.WeekMap[wi] = w[wi]; }
                    else if (k == "ovr") { string[] o = v.Split('|'); if (o.Length == 2) { s.OverrideDate = o[0]; s.OverrideName = o[1]; } }
                    else if (k == "rot") { string[] r = v.Split('|'); if (r.Length >= 4) { s.RotOn = r[0] == "1"; s.RotLabels = r[1]; s.RotAnchor = r[2]; int ri; if (int.TryParse(r[3], out ri)) s.RotIndex = ri; } }
                    else if (k == "noschool") { foreach (string ds in v.Split(';')) if (ds.Length > 0) s.NoSchool.Add(ds); }
                    else if (k == "cdays") { foreach (string pr in v.Split('|')) { int ti = pr.IndexOf('~'); if (ti > 0) s.CourseDays[pr.Substring(0, ti)] = pr.Substring(ti + 1); } }
                    else if (k == "levels") { foreach (string pr in v.Split('|')) { int ti = pr.IndexOf('~'); int lv; if (ti > 0 && int.TryParse(pr.Substring(ti + 1), out lv)) s.Levels[pr.Substring(0, ti)] = lv; } }
                    else if (k == "setup") { string[] su = v.Split('|'); s.SetupDone = su[0] == "1"; if (su.Length > 1) s.ThemesOpen = su[1] != "0"; }
                    else if (k == "gdays") { foreach (string gd in v.Split(';')) if (gd.Length > 0 && !s.GridDays.Contains(gd)) s.GridDays.Add(gd); }
                    else if (k == "grows") { foreach (string gr in v.Split(';')) { string[] ge = gr.Split('-'); if (ge.Length == 2) { s.GridStarts.Add(ge[0]); s.GridEnds.Add(ge[1]); } } }
                    else if (k.StartsWith("gcell:")) { s.GridCells[k.Substring(6)] = new List<string>(v.Split(';')); }
                    else if (k == "setupver") { int sv; if (int.TryParse(v, out sv)) s.SetupVer = sv; }
                    else if (k.StartsWith("var:")) { s.Variants[k.Substring(4)] = new List<string>(v.Split(';')); }
                    else if (k == "dtype") { foreach (string pr in v.Split('^')) { int ti = pr.IndexOf('~'); if (ti > 0) s.DayTypes[pr.Substring(0, ti)] = pr.Substring(ti + 1); } }
                    else if (k == "dletter") { foreach (string pr in v.Split('^')) { int ti = pr.IndexOf('~'); if (ti > 0) s.DayLetters[pr.Substring(0, ti)] = pr.Substring(ti + 1); } }
                    else if (k == "notray") s.NoTray = v == "1";
                    else if (k == "legacy") s.Legacy = v == "1";
                    else if (k == "unlock") s.Unlocked = v == "1";
                    else if (k == "fsseen") s.FsSeen = v == "1";
                    else if (k == "absdays") s.AbsDays = v == "1";
                    else if (k == "countdowns") { foreach (string pr in v.Split('^')) if (pr.Contains("~")) s.Countdowns.Add(pr); }
                    else if (k == "schoolend") s.SchoolEnd = v;
                    else if (k == "norot") s.NoRotation = v == "1";
                    else if (k == "hidepfp") s.HidePhoto = v == "1";
                    else if (k == "dropgpa") s.CountDropped = v == "1";
                    else if (k == "numbers") s.NumbersOnly = v == "1";
                    else if (k == "notifynew") s.NotifyNew = v != "0";
                    else if (k == "pnames") { int pn; if (int.TryParse(v, out pn) && (pn == 0 || pn == 1)) s.PeriodNames = pn; }
                    else if (k == "regname") { if (v.Trim().Length > 0) s.RegularName = v.Trim(); }
                    else if (k == "noletters") s.NoLetters = v == "1";
                    else if (k.StartsWith("gset:")) { GridSet gsr = GridSet.Read(k.Substring(5), v); if (gsr != null) s.Sets.Add(gsr); }
                    else if (k == "zoom") { double zv; if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out zv) && zv >= 0.6 && zv <= 2.0) s.Zoom = zv; }
                    else if (k == "notify") { string[] nv = v.Split('|'); if (nv.Length >= 3) { s.NotifyStart = nv[0] == "1"; s.NotifyEnd = nv[1] == "1"; int lead; if (int.TryParse(nv[2], out lead) && lead >= 0 && lead <= 60) s.NotifyLead = lead; if (nv.Length > 3) s.TrayOnClose = nv[3] == "1"; } }
                    else if (k == "navorder") s.NavOrder = v;
                    else if (k == "navhide") s.NavHide = v;
                    else if (k == "pmap") { foreach (string pr in v.Split('^')) { int ti = pr.LastIndexOf('~'); if (ti > 0) s.PeriodMap[pr.Substring(0, ti)] = pr.Substring(ti + 1); } }
                    else if (k == "dscheds") { foreach (string pr in v.Split('^')) { int ti = pr.IndexOf('~'); if (ti > 0) s.DayScheds[pr.Substring(0, ti)] = pr.Substring(ti + 1); } }
                    else if (k == "goal") { double gv; if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gv) && gv > 0 && gv <= 100) s.Goal = gv; }
                }
            }
            catch (Exception) { }
            if (s.Schedules.Count == 0 && s.Bells.Length > 0)
            {
                BellSchedule legacy = new BellSchedule();
                legacy.Name = "Regular";
                legacy.Entries = Bell.Parse(s.Bells);
                if (legacy.Entries.Count > 0) s.Schedules.Add(legacy);
            }
            return s;
        }

        // "Name|Label;08:05;08:55|Label;..."
        void ReadSchedule(string v)
        {
            string[] parts = v.Split('|');
            if (parts.Length < 1 || parts[0].Length == 0) return;
            BellSchedule b = new BellSchedule();
            b.Name = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split(';');
                if (f.Length < 3) continue;
                TimeSpan a, e;
                if (!Bell.TryTime(f[1], out a) || !Bell.TryTime(f[2], out e)) continue;
                Bell bl = new Bell();
                bl.Label = f[0]; bl.Start = a; bl.End = e;
                b.Entries.Add(bl);
            }
            Schedules.Add(b);
        }

        public void Save()
        {
            Sched.Invalidate();
            try
            {
                List<string> extra = new List<string>();
                foreach (BellSchedule b in Schedules)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder("sched=" + Clean(b.Name));
                    foreach (Bell e in b.Entries)
                        sb.Append("|" + Clean(e.Label) + ";" + e.Start.ToString(@"hh\:mm") + ";" + e.End.ToString(@"hh\:mm"));
                    extra.Add(sb.ToString());
                }
                extra.Add("week=" + string.Join("|", WeekMap));
                extra.Add("ovr=" + OverrideDate + "|" + OverrideName);
                extra.Add("rot=" + (RotOn ? "1" : "0") + "|" + Clean(RotLabels) + "|" + RotAnchor + "|" + RotIndex);
                extra.Add("noschool=" + string.Join(";", NoSchool.ToArray()));
                List<string> cd = new List<string>();
                foreach (KeyValuePair<string, string> kv in CourseDays) cd.Add(kv.Key + "~" + Clean(kv.Value));
                extra.Add("cdays=" + string.Join("|", cd.ToArray()));
                List<string> lv = new List<string>();
                foreach (KeyValuePair<string, int> kv in Levels) lv.Add(kv.Key + "~" + kv.Value);
                extra.Add("levels=" + string.Join("|", lv.ToArray()));
                extra.Add("mode=" + Mode);
                extra.Add("setup=" + (SetupDone ? "1" : "0") + "|" + (ThemesOpen ? "1" : "0"));
                extra.Add("gdays=" + string.Join(";", GridDays.ConvertAll<string>(delegate(string x) { return Clean(x); }).ToArray()));
                List<string> grr = new List<string>();
                for (int gi = 0; gi < GridStarts.Count && gi < GridEnds.Count; gi++) grr.Add(GridStarts[gi] + "-" + GridEnds[gi]);
                extra.Add("grows=" + string.Join(";", grr.ToArray()));
                foreach (KeyValuePair<string, List<string>> gc in GridCells)
                    extra.Add("gcell:" + Clean(gc.Key) + "=" + string.Join(";", gc.Value.ConvertAll<string>(delegate(string x) { return Clean(x); }).ToArray()));
                extra.Add("setupver=" + SetupVer);
                foreach (KeyValuePair<string, List<string>> vv in Variants) extra.Add("var:" + Clean(vv.Key) + "=" + string.Join(";", vv.Value.ToArray()));
                List<string> dts = new List<string>();
                foreach (KeyValuePair<string, string> kv in DayTypes) dts.Add(kv.Key + "~" + Clean(kv.Value));
                extra.Add("dtype=" + string.Join("^", dts.ToArray()));
                List<string> dls = new List<string>();
                foreach (KeyValuePair<string, string> kv in DayLetters) dls.Add(kv.Key + "~" + Clean(kv.Value));
                extra.Add("dletter=" + string.Join("^", dls.ToArray()));
                extra.Add("ctheme=" + Clean(CustomBase) + "|" + Clean(CustomAccent) + "|" + Clean(CustomText));
                extra.Add("notray=" + (NoTray ? "1" : "0"));
                extra.Add("legacy=" + (Legacy ? "1" : "0"));
                extra.Add("unlock=" + (Unlocked ? "1" : "0"));
                extra.Add("fsseen=" + (FsSeen ? "1" : "0"));
                extra.Add("absdays=" + (AbsDays ? "1" : "0"));
                extra.Add("countdowns=" + string.Join("^", Countdowns.ToArray()));
                extra.Add("schoolend=" + SchoolEnd);
                extra.Add("norot=" + (NoRotation ? "1" : "0"));
                extra.Add("hidepfp=" + (HidePhoto ? "1" : "0"));
                extra.Add("dropgpa=" + (CountDropped ? "1" : "0"));
                extra.Add("numbers=" + (NumbersOnly ? "1" : "0"));
                extra.Add("notifynew=" + (NotifyNew ? "1" : "0"));
                extra.Add("pnames=" + PeriodNames);
                extra.Add("regname=" + Clean(RegularName));
                extra.Add("noletters=" + (NoLetters ? "1" : "0"));
                foreach (GridSet gsw in Sets) extra.Add("gset:" + GridSet.G(gsw.Name) + "=" + gsw.Write());
                extra.Add("zoom=" + Zoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                extra.Add("notify=" + (NotifyStart ? "1" : "0") + "|" + (NotifyEnd ? "1" : "0") + "|" + NotifyLead + "|" + (TrayOnClose ? "1" : "0"));
                extra.Add("navorder=" + NavOrder);
                extra.Add("navhide=" + NavHide);
                List<string> pm = new List<string>();
                foreach (KeyValuePair<string, string> kv in PeriodMap) pm.Add(Clean(kv.Key) + "~" + Clean(kv.Value));
                extra.Add("pmap=" + string.Join("^", pm.ToArray()));
                List<string> ds = new List<string>();
                foreach (KeyValuePair<string, string> kv in DayScheds) ds.Add(Clean(kv.Key) + "~" + Clean(kv.Value));
                extra.Add("dscheds=" + string.Join("^", ds.ToArray()));
                extra.Add("rail=" + (RailCollapsed ? "1" : "0"));
                string[] lines0 = new string[]
                {
                    "url=" + Url,
                    "user=" + User,
                    "pass=" + (Stay ? EncPass : ""),
                    "stay=" + (Stay ? "1" : "0"),
                    "theme=" + Theme,
                    "interval=" + Interval,
                    "bells=",
                    "goal=" + Goal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                };
                List<string> all = new List<string>(lines0);
                all.AddRange(extra);
                string[] lines = all.ToArray();
                File.WriteAllLines(FilePath(), lines, Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }

    // everything the app keeps lives in %APPDATA%\Noctua
    static class Disk
    {
        public static string Dir()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(root, "Noctua");
            try
            {
                // carry the old folder over once
                string old = Path.Combine(root, "CampusMidnight");
                if (!Directory.Exists(dir) && Directory.Exists(old)) Directory.Move(old, dir);
            }
            catch (Exception) { }
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            return dir;
        }

        public static string At(string name)
        {
            return Path.Combine(Dir(), name);
        }

        public static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "file";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Length > 80 ? s.Substring(0, 80) : s;
        }
    }

    // one timed block of the school day: a class period, lunch, homeroom...
    class Bell
    {
        public string Label = "";
        public TimeSpan Start, End;
        public bool NonInstr;

        public string Key { get { return KeyOf(Label); } }

        public static string KeyOf(string period)
        {
            if (string.IsNullOrEmpty(period)) return "";
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(period, @"\d+");
            if (m.Success) return int.Parse(m.Value).ToString();
            return Norm.Key(period);
        }

        public static bool TryTime(string t, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            if (t == null) return false;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(t.Trim().ToLowerInvariant().Replace(".", ""), @"^(\d{1,2})(?::(\d{2}))?\s*([ap])?m?$");
            if (!m.Success) return false;
            int h = int.Parse(m.Groups[1].Value);
            int min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (m.Groups[3].Success)
            {
                bool pm = m.Groups[3].Value == "p";
                if (h == 12) h = pm ? 12 : 0; else if (pm) h += 12;
            }
            else if (h >= 1 && h <= 6) h += 12; // "1:05" with no am/pm means the afternoon
            if (h > 23 || min > 59) return false;
            ts = new TimeSpan(h, min, 0);
            return true;
        }

        public static string Clock(TimeSpan t)
        {
            return DateTime.Today.Add(t).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ClockShort(TimeSpan t)
        {
            return Clock(t);
        }

        public string Text()
        {
            return ClockShort(Start) + " \u2013 " + ClockShort(End);
        }

        // older versions kept bell times as plain text lines like "1  8:05am-8:55am"
        public static List<Bell> Parse(string text)
        {
            List<Bell> list = new List<Bell>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string raw in text.Replace("\r", "").Split(new char[] { ';', '\n' }))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(line,
                    @"^(.+?)\s+(\d{1,2}(?::\d{2})?\s*[ap]?m?)\s*(?:-|\u2013|to)\s*(\d{1,2}(?::\d{2})?\s*[ap]?m?)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                TimeSpan a, b;
                if (!TryTime(m.Groups[2].Value, out a) || !TryTime(m.Groups[3].Value, out b)) continue;
                if (b <= a) b = b + TimeSpan.FromHours(12);
                Bell bl = new Bell();
                bl.Label = m.Groups[1].Value.Trim();
                bl.Start = a; bl.End = b;
                list.Add(bl);
            }
            return list;
        }
    }

    // one complete schedule: letter days across, time slots down, a period in each cell (Regular, Half day, ...)
    class GridSet
    {
        public string Name = "Regular", Auto = "";   // Auto: use it when Campus calls the day this ("Early Dismissal")
        public int Weekday = -1;                      // or use it every Monday, Tuesday ... (0 = Sunday); -1 = never
        public List<string> Days = new List<string>(), Starts = new List<string>(), Ends = new List<string>();
        public Dictionary<string, List<string>> Cells = new Dictionary<string, List<string>>();

        public static string G(string s)
        {
            return (s ?? "").Replace("|", " ").Replace(";", " ").Replace("~", " ").Replace("=", " ").Replace(",", " ").Replace("^", " ").Replace(":", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        public GridSet Copy(string name)
        {
            GridSet c = new GridSet();
            c.Name = name; c.Auto = Auto; c.Weekday = Weekday;
            c.Days = new List<string>(Days); c.Starts = new List<string>(Starts); c.Ends = new List<string>(Ends);
            foreach (KeyValuePair<string, List<string>> kv in Cells) c.Cells[kv.Key] = new List<string>(kv.Value);
            return c;
        }

        public string Write()
        {
            List<string> rows = new List<string>();
            for (int i = 0; i < Starts.Count && i < Ends.Count; i++) rows.Add(Starts[i] + "-" + Ends[i]);
            List<string> cells = new List<string>();
            foreach (string d in Days)
            {
                List<string> col;
                if (!Cells.TryGetValue(d, out col)) col = new List<string>();
                cells.Add(G(d) + "~" + string.Join(",", col.ConvertAll<string>(delegate(string x) { return G(x); }).ToArray()));
            }
            return G(Auto) + "," + Weekday + "|" + string.Join(";", Days.ConvertAll<string>(delegate(string x) { return G(x); }).ToArray())
                + "|" + string.Join(";", rows.ToArray()) + "|" + string.Join("^", cells.ToArray());
        }

        public static GridSet Read(string name, string v)
        {
            string[] p = v.Split('|');
            if (p.Length < 4 || name.Trim().Length == 0) return null;
            GridSet g = new GridSet();
            g.Name = name.Trim();
            string[] meta = p[0].Split(',');
            g.Auto = meta[0];
            int wd;
            if (meta.Length > 1 && int.TryParse(meta[1], out wd)) g.Weekday = wd;
            foreach (string d in p[1].Split(';')) if (d.Length > 0 && !g.Days.Contains(d)) g.Days.Add(d);
            foreach (string r in p[2].Split(';')) { string[] se = r.Split('-'); if (se.Length == 2) { g.Starts.Add(se[0]); g.Ends.Add(se[1]); } }
            foreach (string c in p[3].Split('^'))
            {
                int t = c.IndexOf('~');
                if (t <= 0) continue;
                g.Cells[c.Substring(0, t)] = new List<string>(c.Substring(t + 1).Split(','));
            }
            foreach (string d in g.Days)
            {
                List<string> col;
                if (!g.Cells.TryGetValue(d, out col)) { col = new List<string>(); g.Cells[d] = col; }
                while (col.Count < g.Starts.Count) col.Add("");
                while (col.Count > g.Starts.Count) col.RemoveAt(col.Count - 1);
            }
            return g.Days.Count > 0 && g.Starts.Count > 0 ? g : null;
        }

        // the bell schedule for one letter day of this set (a set with one column is used for every day)
        public BellSchedule ScheduleFor(string label)
        {
            string day = null;
            string first = (label ?? "").Trim();
            int sp = first.IndexOf(' ');
            if (sp > 0) first = first.Substring(0, sp);
            string whole = (label ?? "").Trim();
            foreach (string d in Days) if (string.Equals(d, whole, StringComparison.OrdinalIgnoreCase)) { day = d; break; }
            if (day == null) foreach (string d in Days) if (string.Equals(d, first, StringComparison.OrdinalIgnoreCase)) { day = d; break; }
            if (day == null) foreach (string d in Days) if (d.StartsWith(first + " ", StringComparison.OrdinalIgnoreCase)) { day = d; break; }
            if (day == null && Days.Count > 0) day = Days[0];
            return day == null ? null : Build(day);
        }

        public BellSchedule Build(string day)
        {
            BellSchedule b = new BellSchedule();
            b.Name = day; b.Source = Name;
            List<string> col;
            if (!Cells.TryGetValue(day, out col)) return b;
            for (int i = 0; i < col.Count && i < Starts.Count && i < Ends.Count; i++)
            {
                if (col[i].Trim().Length == 0) continue;
                TimeSpan a, z;
                if (!Bell.TryTime(Starts[i], out a) || !Bell.TryTime(Ends[i], out z)) continue;
                Bell e = new Bell();
                e.Label = col[i].Trim(); e.Start = a; e.End = z;
                b.Entries.Add(e);
            }
            return b;
        }
    }

    class BellSchedule
    {
        public string Source = "";   // which schedule set it came from (only used by import and export)
        public string Name = "Regular";
        public string Id = "";     // Campus period schedule id when imported
        public List<Bell> Entries = new List<Bell>();

        public void SortByTime()
        {
            Entries.Sort(delegate(Bell a, Bell b) { return a.Start.CompareTo(b.Start); });
        }

        public BellSchedule Copy(string name)
        {
            BellSchedule c = new BellSchedule();
            c.Name = name;
            c.Id = Id;
            foreach (Bell b in Entries)
            {
                Bell n = new Bell();
                n.Label = b.Label; n.Start = b.Start; n.End = b.End;
                c.Entries.Add(n);
            }
            return c;
        }

        // makes a school day: N classes of the same length, with an optional lunch after one of them
        public static BellSchedule Generate(string name, TimeSpan first, int classMin, int passMin, int count, int lunchAfter, int lunchMin)
        {
            BellSchedule s = new BellSchedule();
            s.Name = name;
            TimeSpan t = first;
            for (int i = 1; i <= count; i++)
            {
                Bell b = new Bell();
                b.Label = i.ToString();
                b.Start = t;
                b.End = t + TimeSpan.FromMinutes(classMin);
                s.Entries.Add(b);
                t = b.End + TimeSpan.FromMinutes(passMin);
                if (lunchAfter == i && lunchMin > 0)
                {
                    Bell l = new Bell();
                    l.Label = "Lunch";
                    l.Start = b.End;
                    l.End = b.End + TimeSpan.FromMinutes(lunchMin);
                    s.Entries.Add(l);
                    t = l.End + TimeSpan.FromMinutes(passMin);
                }
            }
            return s;
        }
    }

    static class Vault
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CampusMidnight.v1");

        public static string Protect(string plain)
        {
            try
            {
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch (Exception) { return ""; }
        }

        public static string Unprotect(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                byte[] raw = ProtectedData.Unprotect(Convert.FromBase64String(b64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch (Exception) { return null; }
        }
    }
}
