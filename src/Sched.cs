using System;
using System.Collections.Generic;
using System.Globalization;

namespace CampusMidnight
{
    // One block of today: a class, lunch, a free period...
    class Slot
    {
        public Bell B;
        public RosterItem R;
        public Course C;
        public string Title = "";
        public bool IsClass, Free;
    }

    class DayPlan
    {
        public DateTime Date;
        public bool School;
        public string DayLabel = "";
        public string LabelFrom = "";       // "you set it" / "Campus calendar"
        public BellSchedule Sched;
        public List<Slot> Slots = new List<Slot>();
        public string WhyNot = "";          // shown when there is no school today
    }

    // Works out what the school day looks like from the bell schedules, the day rotation and the class list.
    static class Sched
    {
        public static string[] Labels(Settings cfg)
        {
            List<string> l = new List<string>();
            foreach (string p in (cfg.RotLabels ?? "").Split(','))
            {
                string t = p.Trim();
                if (t.Length > 0 && !l.Contains(t)) l.Add(t);
            }
            return l.ToArray();
        }

        public static string DayName(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            if (label.IndexOf("day", StringComparison.OrdinalIgnoreCase) >= 0) return label;
            int n;
            if (int.TryParse(label, out n)) return "Day " + label;
            return label + " Day";
        }

        // every day label in play: the ones you typed, or the ones Campus gave each school day
        public static string[] AllLabels(Settings cfg, Snapshot snap)
        {
            if (cfg.RotOn) return Labels(cfg);
            List<string> l = new List<string>();
            if (snap != null) foreach (CalDay c in snap.CalDays) if (c.Label.Length > 0 && !l.Contains(c.Label)) l.Add(c.Label);
            l.Sort(StringComparer.OrdinalIgnoreCase);
            return l.ToArray();
        }

        static bool HasCampusCalendar(Snapshot snap)
        {
            return snap != null && snap.CalDays.Count >= 20;
        }

        public static bool IsSchoolDay(Settings cfg, Snapshot snap, DateTime d)
        {
            d = d.Date;
            string dtype;
            if (cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out dtype)) return dtype != "none";
            if (cfg.NoSchool.Contains(d.ToString("yyyy-MM-dd"))) return false;
            if (HasCampusCalendar(snap))
            {
                DateTime lo = DateTime.MaxValue, hi = DateTime.MinValue;
                foreach (CalDay c in snap.CalDays) { if (c.Date < lo) lo = c.Date; if (c.Date > hi) hi = c.Date; }
                if (d >= lo && d <= hi)
                {
                    foreach (CalDay c in snap.CalDays) if (c.Date == d) return c.School;
                    return false;
                }
            }
            return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday;
        }

        // the rotation label (A, B, ...) for a date, or "" when there is none
        public static string LabelFor(Settings cfg, Snapshot snap, DateTime d, out string from)
        {
            from = "";
            d = d.Date;
            if (!IsSchoolDay(cfg, snap, d)) return "";
            if (cfg.RotOn)
            {
                string[] labels = Labels(cfg);
                DateTime anchor;
                if (labels.Length == 0 || !DateTime.TryParseExact(cfg.RotAnchor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anchor)) return "";
                int steps = 0, guard = 0;
                DateTime x = anchor;
                if (d >= anchor)
                {
                    while (x < d && guard++ < 900) { x = x.AddDays(1); if (IsSchoolDay(cfg, snap, x)) steps++; }
                }
                else
                {
                    while (x > d && guard++ < 900) { if (IsSchoolDay(cfg, snap, x)) steps--; x = x.AddDays(-1); }
                }
                int n = labels.Length;
                int idx = (((cfg.RotIndex + steps) % n) + n) % n;
                from = "set by you";
                return labels[idx];
            }
            if (snap != null)
            {
                foreach (CalDay c in snap.CalDays)
                {
                    if (c.Date == d && c.Label.Length > 0) { from = "from the Campus calendar"; return c.Label; }
                }
            }
            return "";
        }

        public static DateTime NextSchoolDay(Settings cfg, Snapshot snap, DateTime after)
        {
            DateTime d = after.Date.AddDays(1);
            for (int i = 0; i < 60; i++)
            {
                if (IsSchoolDay(cfg, snap, d)) return d;
                d = d.AddDays(1);
            }
            return after.Date.AddDays(1);
        }

        public static bool TermMatches(RosterItem r, string t)
        {
            if (string.IsNullOrEmpty(t) || r.Term.Length == 0) return true;
            return r.Term.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool CourseApplies(Settings cfg, string course, string label)
        {
            if (string.IsNullOrEmpty(label)) return true;
            string v;
            if (cfg.CourseDays.TryGetValue(Settings.Slug(course), out v) && v.Length > 0)
            {
                foreach (string p in v.Split('+')) if (string.Equals(p.Trim(), label, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            return true;
        }

        // does this class meet on this letter day? Uses your grid and period choices; classes without a period set are shown every day
        public static bool MeetsOn(Settings cfg, RosterItem ri, string label)
        {
            if (string.IsNullOrEmpty(label) || cfg.GridDays.Count == 0) return Applies(cfg, ri, label);
            string col = null;
            foreach (string d in cfg.GridDays) if (string.Equals(d, label, StringComparison.OrdinalIgnoreCase)) col = d;
            if (col == null)
            {
                int sp = label.IndexOf(' ');
                if (sp > 0) foreach (string d in cfg.GridDays) if (string.Equals(d, label.Substring(0, sp), StringComparison.OrdinalIgnoreCase)) col = d;
            }
            List<string> cells;
            if (col == null || !cfg.GridCells.TryGetValue(col, out cells)) return true;
            string slug = Settings.Slug(ri.Course);
            bool mapped = false;
            foreach (KeyValuePair<string, string> kv in cfg.PeriodMap)
            {
                if (kv.Value != slug || !kv.Key.EndsWith("#")) continue;
                mapped = true;
                if (cells.Contains(kv.Key.Substring(0, kv.Key.Length - 1))) return true;
            }
            return !mapped;
        }

        // does this class meet on a day with this label? Your own setting wins, then what Campus says about its placement
        public static bool Applies(Settings cfg, RosterItem ri, string label)
        {
            if (string.IsNullOrEmpty(label)) return true;
            string v;
            if (cfg.CourseDays.TryGetValue(Settings.Slug(ri.Course), out v) && v.Length > 0) return CourseApplies(cfg, ri.Course, label);
            if (cfg.RotOn) return true;
            bool named = false;
            foreach (Placement p in ri.Places)
            {
                if (p.SchedName.Length == 0) continue;
                named = true;
                if (Norm.Key(p.SchedName) == Norm.Key(label)) return true;
            }
            if (!named) return true;
            // if none of this student's classes use a schedule with this name, the name tells us nothing about their classes
            if (!LabelIsUsedByClasses(label, ri)) return true;
            return false;
        }

        // true if any class (not just this one) has a placement in a schedule called "label"
        static bool LabelIsUsedByClasses(string label, RosterItem self)
        {
            string k = Norm.Key(label);
            // only the classes of the same snapshot are looked at; ri.Places of all roster items come from the same load
            foreach (RosterItem other in Scope)
                foreach (Placement p in other.Places)
                    if (p.SchedName.Length > 0 && Norm.Key(p.SchedName) == k) return true;
            return false;
        }

        public static List<RosterItem> Scope = new List<RosterItem>();

        static bool PeriodMatches(RosterItem ri, string key, string term)
        {
            if (ri.Places.Count == 0) return Bell.KeyOf(ri.Period) == key;
            foreach (Placement p in ri.Places)
            {
                if (term.Length > 0 && p.Term.Length > 0 && p.Term.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0 && term.IndexOf(p.Term, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Bell.KeyOf(p.Name) == key) return true;
            }
            return false;
        }

        // the class assigned to a period: your own choice first, then what Campus says
        public static RosterItem ClassFor(Settings cfg, Snapshot snap, string term, string schedName, Bell b, string day, out bool chosenNone)
        {
            chosenNone = false;
            string v;
            if (!cfg.PeriodMap.TryGetValue(Settings.PeriodKey(schedName, b.Label, day), out v))
                cfg.PeriodMap.TryGetValue(Settings.PeriodKey(schedName, b.Label, ""), out v);
            if (v != null)
            {
                if (v == "-") { chosenNone = true; return null; }
                foreach (RosterItem ri in snap.Roster)
                    if (!ri.Dropped && Settings.Slug(ri.Course) == v && TermMatches(ri, term)) return ri;
            }
            // nothing is guessed: a period only has a class when you assigned one (or pressed Auto fill in the setup)
            return null;
        }

        public static Course CourseOf(Snapshot snap, RosterItem r, string term)
        {
            if (snap == null) return null;
            Course any = null;
            foreach (Course c in snap.Courses)
            {
                if (r.SectionId.Length > 0 && c.Section == r.SectionId)
                {
                    if (c.Term == term) return c;
                    if (any == null) any = c;
                }
            }
            if (any != null) return any;
            return snap.CourseFor("", r.Course);
        }

        // a saved schedule (half day...) puts its own times on the grid's rows; rows it leaves blank are skipped that day
        public static BellSchedule UserTimes(Settings cfg, BellSchedule baseSched, List<string> times)
        {
            BellSchedule v = new BellSchedule();
            v.Name = baseSched.Name; v.Id = baseSched.Id;
            foreach (Bell b in baseSched.Entries)
            {
                int row = -1;
                for (int i = 0; i < cfg.GridStarts.Count; i++)
                {
                    TimeSpan t;
                    if (Bell.TryTime(cfg.GridStarts[i], out t) && t == b.Start) { row = i; break; }
                }
                if (row < 0 || row >= times.Count) continue;
                string[] pr = times[row].Split('-');
                TimeSpan a, e;
                if (pr.Length != 2 || !Bell.TryTime(pr[0], out a) || !Bell.TryTime(pr[1], out e) || e <= a) continue;
                Bell nb = new Bell();
                nb.Label = b.Label; nb.Start = a; nb.End = e;
                v.Entries.Add(nb);
            }
            return v;
        }

        // early dismissal, late arrival...: Campus gives that day its own times, so move the grid's rows onto them
        static BellSchedule VariantTimes(Snapshot snap, BellSchedule baseSched, DateTime date)
        {
            string sid = "";
            foreach (CalDay c in snap.CalDays) if (c.Date == date.Date) { sid = c.SchedId; break; }
            if (sid.Length == 0) return baseSched;
            BellSchedule auto = null;
            foreach (BellSchedule ab in snap.AutoBells) if (ab.Id == sid) { auto = ab; break; }
            if (auto == null) return baseSched;
            List<Bell> instr = new List<Bell>();
            foreach (Bell x in auto.Entries) if (!x.NonInstr) instr.Add(x);
            instr.Sort(delegate(Bell x, Bell y) { return x.Start.CompareTo(y.Start); });
            List<Bell> mine = new List<Bell>(baseSched.Entries);
            mine.Sort(delegate(Bell x, Bell y) { return x.Start.CompareTo(y.Start); });
            if (instr.Count != mine.Count) return baseSched;
            BellSchedule v = new BellSchedule();
            v.Name = baseSched.Name; v.Id = sid;
            for (int i = 0; i < mine.Count; i++)
            {
                Bell nb = new Bell();
                nb.Label = mine[i].Label; nb.Start = instr[i].Start; nb.End = instr[i].End;
                v.Entries.Add(nb);
            }
            return v;
        }

        public static DayPlan Plan(Settings cfg, Snapshot snap, string term, DateTime date)
        {
            DayPlan p = new DayPlan();
            p.Date = date.Date;
            p.School = IsSchoolDay(cfg, snap, date);
            if (!p.School)
            {
                p.WhyNot = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) ? "It is the weekend." : "No school.";
                return p;
            }
            string from;
            p.DayLabel = LabelFor(cfg, snap, date, out from);
            p.LabelFrom = from;
            p.Sched = null;
            string ds;
            if (p.DayLabel.Length > 0 && cfg.DayScheds.TryGetValue(p.DayLabel, out ds) && cfg.OverrideDate != date.ToString("yyyy-MM-dd")) p.Sched = cfg.Find(ds);
            if (p.Sched == null && p.DayLabel.Length > 0)
            {
                p.Sched = cfg.Find(p.DayLabel);
                if (p.Sched == null)
                {
                    int sp = p.DayLabel.IndexOf(' ');
                    if (sp > 0) p.Sched = cfg.Find(p.DayLabel.Substring(0, sp));
                }
            }
            if (p.Sched == null) p.Sched = cfg.ForDate(date);
            string dayType;
            List<string> custom;
            if (p.Sched != null && cfg.DayTypes.TryGetValue(date.ToString("yyyy-MM-dd"), out dayType) && cfg.Variants.TryGetValue(dayType, out custom))
                p.Sched = UserTimes(cfg, p.Sched, custom);
            else if (p.Sched != null && snap != null) p.Sched = VariantTimes(snap, p.Sched, date);
            if (p.Sched == null || snap == null) return p;
            List<Bell> entries = new List<Bell>(p.Sched.Entries);
            entries.Sort(delegate(Bell a, Bell b) { return a.Start.CompareTo(b.Start); });
            foreach (Bell b in entries)
            {
                Slot s = new Slot();
                s.B = b;
                string key = b.Key;
                bool none;
                RosterItem pick = ClassFor(cfg, snap, term, p.Sched.Name, b, p.DayLabel, out none);
                bool numeric = !none && key.Length > 0 && char.IsDigit(key[0]);
                if (pick != null)
                {
                    s.R = pick;
                    s.C = CourseOf(snap, pick, term);
                    s.IsClass = true;
                    s.Title = pick.Course;
                }
                else if (numeric || none)
                {
                    s.Free = true;
                    s.Title = none ? "Free period" : "Period " + b.Label;
                }
                else s.Title = b.Label;
                p.Slots.Add(s);
            }
            return p;
        }

        // "class" (inside a block), "between", "before", "after" or "none"
        public static string Phase(DayPlan p, TimeSpan now, out int cur, out int next)
        {
            cur = -1; next = -1;
            if (p == null || !p.School || p.Slots.Count == 0) return "none";
            for (int i = 0; i < p.Slots.Count; i++)
            {
                Bell b = p.Slots[i].B;
                if (now >= b.Start && now < b.End) { cur = i; break; }
            }
            for (int i = 0; i < p.Slots.Count; i++)
            {
                if (p.Slots[i].B.Start > now && i != cur) { next = i; break; }
            }
            if (cur >= 0) return "class";
            if (next < 0) return "after";
            return now < p.Slots[0].B.Start ? "before" : "between";
        }

        public static string StateKey(DayPlan p, TimeSpan now)
        {
            int cur, next;
            string ph = Phase(p, now, out cur, out next);
            return (p == null ? "" : p.Date.ToString("yyyyMMdd")) + ph + cur + "/" + next;
        }

        public static string Span(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int mins = (int)Math.Ceiling(t.TotalMinutes);
            if (mins >= 60) return (mins / 60) + " h " + (mins % 60 == 0 ? "" : (mins % 60) + " min");
            return mins + " min";
        }

        public static string Countdown(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int total = (int)Math.Ceiling(t.TotalSeconds);
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (h > 0) return h + ":" + m.ToString("00") + ":" + s.ToString("00");
            return m + ":" + s.ToString("00");
        }

        public static int ClassesLeft(DayPlan p, TimeSpan now)
        {
            if (p == null) return 0;
            int n = 0;
            foreach (Slot s in p.Slots) if (s.IsClass && s.B.End > now) n++;
            return n;
        }
    }
}
