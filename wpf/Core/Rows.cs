using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CampusMidnight
{
    // grade maths and list helpers shared by the pages (no drawing in here)
    static class Rows
    {
        public const string NoData =
            "You are signed in, but nothing readable came back. Your school's portal may use different addresses than this app expects. " +
            "Open Settings, then Diagnostics, copy the report and send it over so the app can be adjusted.";

        public static string DayLabel(DateTime d)
        {
            int diff = (int)Math.Round((d.Date - DateTime.Today).TotalDays);
            if (diff == 0) return "Today";
            if (diff == 1) return "Tomorrow";
            if (diff == -1) return "Yesterday";
            return d.ToString("dddd, MMM d");
        }

        public static int DueCompare(Assign a, Assign b, bool asc)
        {
            if (a.Due.HasValue && b.Due.HasValue)
            {
                int c = a.Due.Value.CompareTo(b.Due.Value);
                if (c == 0) return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return asc ? c : -c;
            }
            if (a.Due.HasValue) return -1;
            if (b.Due.HasValue) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static void SortAssigns(List<Assign> list, bool asc)
        {
            list.Sort(delegate(Assign a, Assign b) { return DueCompare(a, b, asc); });
        }

        public static bool IsUpcoming(Assign a)
        {
            if (a.InDropped) return false;
            if (a.Status == "upcoming") return true;
            return a.Status == "submitted" && a.Due.HasValue && a.Due.Value >= DateTime.Today;
        }

        public static double? Points(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return null;
            switch (letter.Trim().ToUpperInvariant())
            {
                case "A+": case "A": return 4.0;
                case "A-": return 3.7;
                case "B+": return 3.3;
                case "B": return 3.0;
                case "B-": return 2.7;
                case "C+": return 2.3;
                case "C": return 2.0;
                case "C-": return 1.7;
                case "D+": return 1.3;
                case "D": return 1.0;
                case "D-": return 0.7;
                case "F": return 0.0;
            }
            return null;
        }

        public static int AutoLevel(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (Regex.IsMatch(name, @"\b(AP|IB|DE|dual|college|advanced placement)\b", RegexOptions.IgnoreCase)) return 2;
            if (Regex.IsMatch(name, @"\b(honors?|hon|pre-?ap|accelerated|gifted)\b", RegexOptions.IgnoreCase)) return 1;
            return 0;
        }

        public static int LevelOf(Settings cfg, Course c)
        {
            int v = cfg.LevelFor(c.Name);
            return v >= 0 ? v : AutoLevel(c.Name);
        }

        public static string LevelName(int level)
        {
            return level >= 2 ? "AP / IB / dual credit" : (level == 1 ? "Honors" : "Regular");
        }

        public static double? Gpa(List<Course> cs, bool weighted)
        {
            double sum = 0; int n = 0;
            foreach (Course c in cs)
            {
                string l = !string.IsNullOrEmpty(c.Letter) ? c.Letter : Norm.LetterFor(c.Percent);
                double? p = Points(l);
                if (!p.HasValue) continue;
                double v = p.Value;
                if (weighted && v > 0) v += c.Level >= 2 ? 1.0 : (c.Level == 1 ? 0.5 : 0.0);
                sum += v; n++;
            }
            return n == 0 ? (double?)null : Math.Round(sum / n, 2);
        }

        public static string G2(double? v)
        {
            return v.HasValue ? v.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "–";
        }

        public static string Fmt(double? n)
        {
            if (!n.HasValue) return "";
            return Math.Round(n.Value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string[] parts = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string s = parts[0].Substring(0, 1);
            if (parts.Length > 1) s += parts[parts.Length - 1].Substring(0, 1);
            return s.ToUpperInvariant();
        }

        public static List<Assign> AssignsFor(Snapshot snap, Course c)
        {
            List<Assign> list = new List<Assign>();
            if (snap == null) return list;
            string key = Norm.Key(c.Name);
            foreach (Assign a in snap.Assigns)
            {
                if (c.Section.Length > 0 && a.SectionId.Length > 0)
                {
                    if (a.SectionId == c.Section) list.Add(a);
                    continue;
                }
                string ak = Norm.Key(a.Course);
                if (ak.Length > 0 && (ak == key || ak.Contains(key) || key.Contains(ak))) list.Add(a);
            }
            return list;
        }
    }
}
