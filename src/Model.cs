using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CampusMidnight
{
    class Student { public string Id, First, Last, Name; }

    class TaskGrade { public string Name, Letter; public double? Percent; }

    class Course
    {
        public string Name, Section, Teacher, Room, Term, Letter, TaskName;
        public double? Percent;
        public List<TaskGrade> Tasks = new List<TaskGrade>();
    }

    class Assign
    {
        public string Name, Course, Term, Status;
        public DateTime? Due;
        public double? Score, Total, Percent;
        public bool Missing, Late, Exempt;
    }

    class RosterItem { public string Course, Teacher, Room, Period, Term; public int Order; }

    class AttRow { public DateTime Date; public string Status, Period, Course; }

    class DiagRow { public string Key, Path, Error, Shape; public int Status, Bytes; public bool Ok; }

    class Snapshot
    {
        public DateTime At = DateTime.Now;
        public List<Student> Students = new List<Student>();
        public List<Course> Courses = new List<Course>();
        public List<string> Terms = new List<string>();
        public List<Assign> Assigns = new List<Assign>();
        public List<RosterItem> Roster = new List<RosterItem>();
        public List<AttRow> Attendance = new List<AttRow>();
        public List<DiagRow> Diag = new List<DiagRow>();

        public bool HasAny { get { return Courses.Count > 0 || Assigns.Count > 0 || Roster.Count > 0; } }

        public string DefaultTerm()
        {
            for (int i = Terms.Count - 1; i >= 0; i--)
            {
                string t = Terms[i];
                foreach (Course c in Courses)
                {
                    if (c.Term == t && (c.Percent.HasValue || !string.IsNullOrEmpty(c.Letter))) return t;
                }
            }
            return Terms.Count > 0 ? Terms[Terms.Count - 1] : "";
        }

        public List<Course> CoursesFor(string term)
        {
            List<Course> r = new List<Course>();
            foreach (Course c in Courses)
            {
                if (string.IsNullOrEmpty(term) || c.Term == term) r.Add(c);
            }
            return r;
        }

        public static double? Average(List<Course> cs)
        {
            double sum = 0; int n = 0;
            foreach (Course c in cs)
            {
                if (c.Percent.HasValue) { sum += c.Percent.Value; n++; }
            }
            if (n == 0) return null;
            return Math.Round(sum / n, 1);
        }
    }

    // Turns Campus' JSON into the small objects above. Districts differ, so every walker looks for the
    // fields it knows by name anywhere in the document instead of trusting one exact layout.
    static class Norm
    {
        public static string LetterFor(double? p)
        {
            if (!p.HasValue) return "";
            double v = p.Value;
            if (v >= 93) return "A";
            if (v >= 90) return "A-";
            if (v >= 87) return "B+";
            if (v >= 83) return "B";
            if (v >= 80) return "B-";
            if (v >= 77) return "C+";
            if (v >= 73) return "C";
            if (v >= 70) return "C-";
            if (v >= 67) return "D+";
            if (v >= 60) return "D";
            return "F";
        }

        public static string Key(string s)
        {
            if (s == null) return "";
            return Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
        }

        public static Snapshot Build(Dictionary<string, object> r)
        {
            Snapshot s = new Snapshot();
            object v;
            if (r.TryGetValue("students", out v)) WalkStudents(v, 0, s.Students);
            if (r.TryGetValue("grades", out v)) WalkGrades(v, "", s, new Dictionary<string, bool>());
            if (r.TryGetValue("assignments", out v)) WalkAssign(v, 0, s.Assigns);
            if (r.TryGetValue("roster", out v)) WalkRoster(v, 0, s.Roster, new Dictionary<string, bool>());
            if (r.TryGetValue("attendance", out v)) WalkAttendance(v, 0, s.Attendance);
            s.Roster.Sort(delegate(RosterItem a, RosterItem b)
            {
                int c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.Compare(a.Course, b.Course, StringComparison.OrdinalIgnoreCase);
            });
            s.Attendance.Sort(delegate(AttRow a, AttRow b) { return b.Date.CompareTo(a.Date); });
            return s;
        }

        // ---------------------------------------------------------------- students
        static void WalkStudents(object n, int depth, List<Student> outp)
        {
            if (n == null || depth > 4) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkStudents(x, depth + 1, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string id = J.Str(d, "personID");
            string fn = J.Str(d, "preferredFirstName", "firstName");
            string ln = J.Str(d, "lastName");
            if (id != null && (fn != null || ln != null))
            {
                Student st = new Student();
                st.Id = id; st.First = fn ?? ""; st.Last = ln ?? "";
                st.Name = (st.First + " " + st.Last).Trim();
                outp.Add(st);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkStudents(kv.Value, depth + 1, outp);
            }
        }

        // ---------------------------------------------------------------- grades
        static void WalkGrades(object n, string term, Snapshot s, Dictionary<string, bool> seen)
        {
            if (n == null) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkGrades(x, term, s, seen);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string tn = J.Str(d, "termName");
            if (!string.IsNullOrEmpty(tn)) term = tn;
            string cn = J.Str(d, "courseName");
            if (!string.IsNullOrEmpty(cn))
            {
                IList tasks = null;
                string[] tkeys = new string[] { "gradingTasks", "tasks", "grades", "gradeTasks" };
                for (int i = 0; i < tkeys.Length && tasks == null; i++)
                {
                    object tv;
                    if (d.TryGetValue(tkeys[i], out tv)) tasks = tv as IList;
                }
                Course c = new Course();
                c.Name = cn;
                c.Section = J.Str(d, "sectionID") ?? "";
                c.Teacher = J.Str(d, "teacherDisplay", "teacherName", "teacher") ?? "";
                c.Room = J.Str(d, "roomName", "room") ?? "";
                c.Term = term ?? "";
                c.TaskName = "";
                c.Letter = "";
                if (tasks != null)
                {
                    foreach (object t in tasks)
                    {
                        Dictionary<string, object> td = J.D(t);
                        if (td == null) continue;
                        TaskGrade tg = new TaskGrade();
                        tg.Name = J.Str(td, "taskName", "gradingTaskName", "name") ?? "";
                        tg.Letter = J.Str(td, "progressScore", "letterGrade", "grade", "score") ?? "";
                        tg.Percent = J.Num(J.Get(td, "progressPercent", "percent", "percentage", "scorePercent"));
                        if (tg.Name.Length > 0 || tg.Letter.Length > 0 || tg.Percent.HasValue) c.Tasks.Add(tg);
                    }
                }
                TaskGrade cur = null;
                for (int i = c.Tasks.Count - 1; i >= 0; i--)
                {
                    if (c.Tasks[i].Percent.HasValue || c.Tasks[i].Letter.Length > 0) { cur = c.Tasks[i]; break; }
                }
                if (cur != null)
                {
                    c.Percent = cur.Percent;
                    c.TaskName = cur.Name;
                    c.Letter = (cur.Letter.Length > 0 && !Regex.IsMatch(cur.Letter, @"^\d")) ? cur.Letter : LetterFor(cur.Percent);
                }
                else c.Letter = "";
                string key = c.Section + "|" + c.Term + "|" + c.Name;
                if (!seen.ContainsKey(key))
                {
                    seen[key] = true;
                    if (c.Term.Length > 0 && !s.Terms.Contains(c.Term)) s.Terms.Add(c.Term);
                    s.Courses.Add(c);
                }
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkGrades(kv.Value, term, s, seen);
            }
        }

        // ---------------------------------------------------------------- assignments
        static void WalkAssign(object n, int depth, List<Assign> outp)
        {
            if (n == null || depth > 8) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkAssign(x, depth + 1, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string name = J.Str(d, "assignmentName");
            if (!string.IsNullOrEmpty(name))
            {
                Assign a = new Assign();
                a.Name = name;
                a.Course = J.Str(d, "courseName", "sectionName", "course") ?? "";
                a.Term = J.Str(d, "termName", "term") ?? "";
                a.Due = J.Date(J.Get(d, "dueDate", "due", "dueDateTime", "dueOn"));
                a.Score = J.Num(J.Get(d, "score", "scorePoints", "earnedPoints", "points"));
                a.Total = J.Num(J.Get(d, "totalPoints", "pointsPossible", "possiblePoints", "possible", "scoreTotal"));
                a.Percent = J.Num(J.Get(d, "scorePercentage", "scorePercent", "percent", "percentage"));
                if (!a.Percent.HasValue && a.Score.HasValue && a.Total.HasValue && a.Total.Value > 0)
                    a.Percent = Math.Round(a.Score.Value / a.Total.Value * 100, 1);
                a.Missing = J.Bool(J.Get(d, "missing", "isMissing"));
                a.Late = J.Bool(J.Get(d, "late", "isLate"));
                a.Exempt = J.Bool(J.Get(d, "exempt", "isExempt"));
                if (a.Missing) a.Status = "missing";
                else if (a.Exempt) a.Status = "exempt";
                else if (a.Score.HasValue) a.Status = "graded";
                else if (a.Due.HasValue && a.Due.Value < DateTime.Today) a.Status = "ungraded";
                else a.Status = "upcoming";
                outp.Add(a);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkAssign(kv.Value, depth + 1, outp);
            }
        }

        // ---------------------------------------------------------------- roster
        static void WalkRoster(object n, int depth, List<RosterItem> outp, Dictionary<string, bool> seen)
        {
            if (n == null || depth > 8) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkRoster(x, depth + 1, outp, seen);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string cn = J.Str(d, "courseName");
            if (!string.IsNullOrEmpty(cn))
            {
                IList placements = null;
                object pv;
                if (d.TryGetValue("sectionPlacements", out pv)) placements = pv as IList;
                if (placements == null && d.TryGetValue("placements", out pv)) placements = pv as IList;
                List<string> periods = new List<string>();
                List<string> terms = new List<string>();
                if (placements != null)
                {
                    foreach (object p in placements)
                    {
                        Dictionary<string, object> pd = J.D(p);
                        if (pd == null) continue;
                        string per = J.Str(pd, "periodName", "periodSchedule", "period", "periodSequence");
                        string tm = J.Str(pd, "termName", "term");
                        if (tm == null)
                        {
                            Dictionary<string, object> td = J.D(J.Get(pd, "term"));
                            if (td != null) tm = J.Str(td, "termName", "name");
                        }
                        if (per != null && !periods.Contains(per)) periods.Add(per);
                        if (tm != null && !terms.Contains(tm)) terms.Add(tm);
                    }
                }
                string key = (J.Str(d, "sectionID") ?? "") + "|" + cn;
                if (!seen.ContainsKey(key))
                {
                    seen[key] = true;
                    RosterItem ri = new RosterItem();
                    ri.Course = cn;
                    ri.Teacher = J.Str(d, "teacherDisplay", "teacherName", "teacher") ?? "";
                    ri.Room = J.Str(d, "roomName", "room") ?? "";
                    ri.Period = string.Join(" / ", periods.ToArray());
                    ri.Term = string.Join(", ", terms.ToArray());
                    Match m = Regex.Match(ri.Period, @"\d+");
                    ri.Order = m.Success ? int.Parse(m.Value) : 999;
                    outp.Add(ri);
                }
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkRoster(kv.Value, depth + 1, outp, seen);
            }
        }

        // ---------------------------------------------------------------- attendance
        static void WalkAttendance(object n, int depth, List<AttRow> outp)
        {
            if (n == null || depth > 6) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkAttendance(x, depth + 1, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            DateTime? date = J.Date(J.Get(d, "date", "attendanceDate", "calendarDate", "absentDate"));
            string status = J.Str(d, "description", "status", "code", "attendanceCode", "absenceType", "excuseDescription", "excuse");
            if (date.HasValue && status != null)
            {
                AttRow r = new AttRow();
                r.Date = date.Value;
                r.Status = status;
                r.Period = J.Str(d, "periodName", "period") ?? "";
                r.Course = J.Str(d, "courseName", "course") ?? "";
                outp.Add(r);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkAttendance(kv.Value, depth + 1, outp);
            }
        }
    }
}
