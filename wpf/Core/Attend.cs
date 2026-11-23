using System;
using System.Collections;
using System.Collections.Generic;

namespace CampusMidnight
{
    // one mark in a class's attendance record (an absence, a tardy, an early release, or "present" for an event)
    class AttEvent
    {
        public string Kind = "", Term = "", Title = "", Comment = "", Status = "", Time = "", Period = "";
        public DateTime? Date;
    }

    // one class's attendance for one quarter
    class AttCourse
    {
        public string Name = "", Term = "";
        public List<AttEvent> Events = new List<AttEvent>();
        public int Count(string kind) { int n = 0; foreach (AttEvent e in Events) if (e.Kind == kind) n++; return n; }
    }

    static class Attend
    {
        static readonly string[][] Lists = new string[][]
        {
            new string[] { "absentList", "Absent" },
            new string[] { "tardyList", "Tardy" },
            new string[] { "earlyReleaseList", "Early release" },
            new string[] { "presentList", "Present" }
        };

        // reads /portal/attendance/<enrollmentID>: terms, each with courses, each with absent/tardy/early/present lists
        public static List<AttCourse> Parse(object root, out string fieldNote)
        {
            List<AttCourse> res = new List<AttCourse>();
            fieldNote = "";
            List<Dictionary<string, object>> roots = new List<Dictionary<string, object>>();
            IList rl = J.L(root);
            if (rl != null) { foreach (object o in rl) { Dictionary<string, object> d = J.D(o); if (d != null) roots.Add(d); } }
            else { Dictionary<string, object> d = J.D(root); if (d != null) roots.Add(d); }
            foreach (Dictionary<string, object> r in roots)
            {
                IList terms = J.L(J.Get(r, "terms"));
                if (terms == null) continue;
                foreach (object to in terms)
                {
                    Dictionary<string, object> td = J.D(to);
                    if (td == null) continue;
                    string tname = J.Str(td, "termName") ?? "";
                    IList courses = J.L(J.Get(td, "courses"));
                    if (courses == null) continue;
                    foreach (object co in courses)
                    {
                        Dictionary<string, object> cd = J.D(co);
                        if (cd == null) continue;
                        if (cd.ContainsKey("dropped") && J.Bool(cd["dropped"])) continue;
                        AttCourse c = new AttCourse();
                        c.Name = J.Str(cd, "courseName") ?? "";
                        c.Term = tname;
                        foreach (string[] lst in Lists)
                        {
                            IList items = J.L(J.Get(cd, lst[0]));
                            if (items == null) continue;
                            foreach (object io in items)
                            {
                                Dictionary<string, object> d = J.D(io);
                                AttEvent e = new AttEvent();
                                e.Kind = lst[1]; e.Term = tname;
                                if (d != null)
                                {
                                    if (fieldNote.Length == 0) fieldNote = J.Shape(d, 0);
                                    e.Date = J.Date(J.Get(d, "date", "attendanceDate", "calendarDate", "eventDate", "absentDate", "periodDate", "startDate"));
                                    e.Time = J.Str(d, "startTime", "time", "periodStartTime", "timeStart") ?? "";
                                    e.Period = J.Str(d, "periodName", "period") ?? "";
                                    e.Title = J.Str(d, "description", "attendanceDescription", "eventDescription", "eventName", "event", "reason", "codeDescription", "name") ?? "";
                                    e.Comment = J.Str(d, "comments", "comment", "attendanceComments", "note") ?? "";
                                    e.Status = StatusWord(J.Str(d, "excuseDescription", "excuseName", "excuse", "status", "attendanceStatus", "excuseCode", "code") ?? "");
                                    if (e.Time.Length == 0 && d.ContainsKey("date")) { string raw = J.Str(d, "date") ?? ""; int ti = raw.IndexOf('T'); if (ti > 0 && raw.Length >= ti + 6) e.Time = raw.Substring(ti + 1, 5); }
                                }
                                c.Events.Add(e);
                            }
                        }
                        c.Events.Sort(delegate(AttEvent a, AttEvent b)
                        {
                            DateTime da = a.Date ?? DateTime.MinValue, db = b.Date ?? DateTime.MinValue;
                            return db.CompareTo(da);
                        });
                        res.Add(c);
                    }
                }
            }
            return res;
        }

        // Campus sometimes gives one letter (U, E, X): turn it into the word
        public static string StatusWord(string raw)
        {
            string s = (raw ?? "").Trim();
            switch (s.ToUpperInvariant())
            {
                case "U": return "Unexcused";
                case "E": return "Excused";
                case "X": case "EX": return "Exempt";
                case "K": case "?": return "Unknown";
            }
            return s;
        }

        // the groups an "excuse" text falls in, as Campus lists them
        public static string Excuse(string status)
        {
            string s = StatusWord(status).ToLowerInvariant();
            if (s.Contains("unexc")) return "Unexcused";
            if (s.Contains("exempt")) return "Exempt";
            if (s.Contains("exc")) return "Excused";
            return "Unknown";
        }
    }
}
