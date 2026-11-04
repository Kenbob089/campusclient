using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CampusMidnight
{
    class Student
    {
        public string Id = "", First = "", Last = "", Middle = "", Name = "", Number = "", StateId = "", Grade = "", School = "", Calendar = "", CalendarId = "", StructureId = "";
        public int EndYear;

        public string GradeText()
        {
            if (Grade.Length == 0) return "";
            return Grade.Length <= 2 ? "Grade " + Grade.TrimStart('0') : Grade;
        }

        public string Line()
        {
            string t = GradeText();
            if (School.Length > 0) t += (t.Length > 0 ? "  \u00b7  " : "") + School;
            return t;
        }

        // graduation year: the school year's end year plus the years left until 12th grade
        public int GradYear
        {
            get
            {
                if (EndYear < 1900) return 0;
                string g = Grade.Trim().TrimStart('0');
                int n;
                if (g.Length > 0 && int.TryParse(g, out n) && n >= 1 && n <= 12) return EndYear + (12 - n);
                return 0;
            }
        }

        public string ClassOf { get { int y = GradYear; return y > 0 ? "Class of " + y : ""; } }
        public string Year { get { return EndYear > 1900 ? (EndYear - 1) + "\u2013" + EndYear : ""; } }
    }

    class TaskGrade { public string Name, Letter; public double? Percent; }

    class Course
    {
        public string Name, Section, Teacher, Room, Term, Letter, TaskName;
        public double? Percent;
        public bool Dropped;
        public DateTime? EndDate;
        public int Level;   // 0 regular, 1 honors, 2 AP / IB / dual credit
        public List<TaskGrade> Tasks = new List<TaskGrade>();
    }

    class Assign
    {
        public string Id = "", SectionId = "", Name = "", Course = "", Term = "", Status = "", ScoreText = "", Comments = "", Feedback = "";
        public string Description = "", Category = "", Scoring = "";
        public DateTime? Due, Assigned, Scored;
        public double? Score, Total, Percent, Multiplier;
        public bool Missing, Late, Exempt, TurnedIn, Dropped, Incomplete, Cheated, HasUpload, InDropped;
    }

    class Msg
    {
        public string Id = "", Subject = "", From = "", Body = "", Kind = "message", Url = "", ContextId = "", RecipientId = "";
        public DateTime? Date;
        public bool Read = true;
    }

    class Doc
    {
        public string Id = "", Name = "", Url = "", Type = "", Size = "";
        public DateTime? Date;
    }

    class RosterItem { public string Course, Teacher, Room, Period, Term, SectionId = ""; public int Order; public bool Dropped; public DateTime? EndDate; public List<Placement> Places = new List<Placement>(); }

    class CalDay { public DateTime Date; public string Label = "", SchedId = "", DayId = ""; public bool School = true; }
    // where a class meets: which period of which period schedule, in which term
    class Placement { public string Term = "", Name = "", SchedName = "", SchedId = "", PeriodId = ""; public int Seq; }

    class AttRow { public DateTime Date; public string Status, Period, Course; }

    class DiagRow { public string Key, Path, Error, Shape; public int Status, Bytes, Records; public bool Ok; }

    class Snapshot
    {
        public DateTime At = DateTime.Now;
        public List<Student> Students = new List<Student>();
        public List<Course> Courses = new List<Course>();
        public List<string> Terms = new List<string>();
        public List<Assign> Assigns = new List<Assign>();
        public List<RosterItem> Roster = new List<RosterItem>();
        public List<AttRow> Attendance = new List<AttRow>();
        public List<Msg> Messages = new List<Msg>();
        public List<Msg> Notices = new List<Msg>();
        public List<Doc> Docs = new List<Doc>();
        public List<DiagRow> Diag = new List<DiagRow>();
        public string DiscoverNote = "";
        public Dictionary<string, bool> Modules = new Dictionary<string, bool>();   // what the school has switched on in the portal
        public List<string> DiscoverLog = new List<string>();
        public List<CalDay> CalDays = new List<CalDay>();
        public List<BellSchedule> AutoBells = new List<BellSchedule>();
        public string CalStats = "";
        Dictionary<DateTime, CalDay> calIdx;
        int calIdxCount = -1;
        public DateTime CalLo = DateTime.MaxValue, CalHi = DateTime.MinValue;

        // the calendar day for a date, found without scanning the whole list
        public CalDay CalOn(DateTime d)
        {
            if (calIdx == null || calIdxCount != CalDays.Count)
            {
                calIdx = new Dictionary<DateTime, CalDay>();
                CalLo = DateTime.MaxValue; CalHi = DateTime.MinValue;
                foreach (CalDay c in CalDays)
                {
                    calIdx[c.Date.Date] = c;
                    if (c.Date < CalLo) CalLo = c.Date;
                    if (c.Date > CalHi) CalHi = c.Date;
                }
                calIdxCount = CalDays.Count;
            }
            CalDay r;
            return calIdx.TryGetValue(d.Date, out r) ? r : null;
        }
        public Dictionary<string, string> SchedNames = new Dictionary<string, string>();

        // names the period schedules from the class placements, then labels each school day with its schedule's name
        public void LabelDays()
        {
            SchedNames.Clear();
            foreach (RosterItem r in Roster)
                foreach (Placement p in r.Places)
                    if (p.SchedId.Length > 0 && p.SchedName.Length > 0) SchedNames[p.SchedId] = p.SchedName;
            Dictionary<string, int> ids = new Dictionary<string, int>();
            Dictionary<string, int> dayIds = new Dictionary<string, int>();
            foreach (CalDay c in CalDays)
            {
                if (c.SchedId.Length > 0) ids[c.SchedId] = 1;
                if (c.DayId.Length > 0) dayIds[c.DayId] = 1;
            }
            CalStats = "calendar days=" + CalDays.Count + " distinctPeriodSchedules=" + ids.Count + " distinctDayIds=" + dayIds.Count + " namedSchedules=" + SchedNames.Count;
            if (ids.Count < 2 || ids.Count > 12) return;
            foreach (CalDay c in CalDays)
            {
                string nm;
                if (c.Label.Length == 0 && c.SchedId.Length > 0 && SchedNames.TryGetValue(c.SchedId, out nm)) c.Label = nm;
            }
        }

        // classes the student dropped, and the work that belongs to them
        public void Link()
        {
            foreach (Assign a in Assigns)
            {
                a.InDropped = false;
                Course c = CourseFor(a.SectionId, a.Course);
                if (c != null && c.Dropped) a.InDropped = true;
            }
            foreach (RosterItem r in Roster)
            {
                if (r.SectionId.Length == 0) continue;
                foreach (Course c in Courses)
                {
                    if (c.Section == r.SectionId && c.Dropped) { r.Dropped = true; if (!r.EndDate.HasValue) r.EndDate = c.EndDate; break; }
                }
            }
        }

        public Course CourseFor(string sectionId, string name)
        {
            Course any = null;
            if (!string.IsNullOrEmpty(sectionId))
            {
                foreach (Course c in Courses)
                {
                    if (c.Section == sectionId) { if (any == null || !c.Dropped) any = c; }
                }
                if (any != null) return any;
            }
            string key = Norm.Key(name);
            if (key.Length == 0) return null;
            foreach (Course c in Courses)
            {
                string ck = Norm.Key(c.Name);
                if (ck.Length > 0 && (ck == key || ck.Contains(key) || key.Contains(ck))) { if (any == null) any = c; }
            }
            return any;
        }

        public List<Course> Active(string term)
        {
            return CoursesFor(term).FindAll(delegate(Course c) { return !c.Dropped; });
        }

        public bool HasAny { get { return Courses.Count > 0 || Assigns.Count > 0 || Roster.Count > 0; } }

        public Dictionary<string, DateTime[]> TermDates = new Dictionary<string, DateTime[]>();

        // the term that is happening today; before the year starts the first one, after it ends the last one
        public string CurrentTerm()
        {
            DateTime today = DateTime.Today;
            string latestStarted = null;
            DateTime latestStart = DateTime.MinValue;
            foreach (string t in Terms)
            {
                DateTime[] r;
                if (!TermDates.TryGetValue(t, out r)) continue;
                if (today >= r[0] && today <= r[1]) return t;
                if (r[0] <= today && r[0] >= latestStart) { latestStart = r[0]; latestStarted = t; }
            }
            return latestStarted;
        }

        public string DefaultTerm()
        {
            string now = CurrentTerm();
            if (now != null) return now;
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
                if (string.IsNullOrEmpty(term) || c.Term == term)
                {
                    if (GoneIn(c.Name, c.Section, c.Dropped, c.EndDate, term)) continue;
                    r.Add(c);
                }
            }
            return r;
        }

        // a class dropped before a quarter starts has no place in that quarter, unless Campus holds a grade or work for it there
        public bool GoneIn(string course, string section, bool dropped, DateTime? end, string term)
        {
            if (!dropped || !end.HasValue || string.IsNullOrEmpty(term)) return false;
            DateTime[] r;
            if (!TermDates.TryGetValue(term, out r) || r[0].Date <= end.Value.Date) return false;
            section = section ?? "";
            string key = Norm.Key(course ?? "");
            foreach (Course c in Courses)
            {
                if (c.Term != term) continue;
                bool same = (section.Length > 0 && c.Section == section) || Norm.Key(c.Name ?? "") == key;
                if (same && (c.Percent.HasValue || !string.IsNullOrEmpty(c.Letter) || c.Tasks.Count > 0)) return false;
            }
            foreach (Assign a in Assigns)
            {
                bool same = section.Length > 0 && a.SectionId.Length > 0 ? a.SectionId == section : Norm.Key(a.Course ?? "") == key;
                if (!same) continue;
                DateTime? when = a.Due.HasValue ? a.Due : a.Assigned;
                if (when.HasValue && when.Value.Date >= r[0].Date && when.Value.Date <= r[1].Date) return false;
            }
            return true;
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
            if (s.Terms.Count > 0) s.Courses.RemoveAll(delegate(Course c) { return c.Term.Length == 0; }); // top-level duplicates of the per-term lists
            if (r.TryGetValue("assignments", out v)) WalkAssign(v, 0, s.Assigns);
            if (r.TryGetValue("roster", out v)) WalkRoster(v, 0, s.Roster, new Dictionary<string, bool>());
            if (r.TryGetValue("attendance", out v)) WalkAttendance(v, 0, s.Attendance);
            if (r.TryGetValue("calendar", out v)) s.CalDays = CalendarDays(v);
            if (r.TryGetValue("messages", out v)) s.Messages = Messages(v, "message");
            if (r.TryGetValue("notices", out v)) s.Notices = Messages(v, "notice");
            if (r.TryGetValue("documents", out v)) s.Docs = Documents(v);
            s.Roster.Sort(delegate(RosterItem a, RosterItem b)
            {
                int c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.Compare(a.Course, b.Course, StringComparison.OrdinalIgnoreCase);
            });
            s.Attendance.Sort(delegate(AttRow a, AttRow b) { return b.Date.CompareTo(a.Date); });
            s.Messages.Sort(CompareMsg);
            s.Notices.Sort(CompareMsg);
            if (r.TryGetValue("bells", out v)) s.AutoBells = BellSchedules(v);
            s.Link();
            s.LabelDays();
            foreach (BellSchedule ab in s.AutoBells)
            {
                string nm0;
                if (ab.Id.Length > 0 && s.SchedNames.TryGetValue(ab.Id, out nm0)) ab.Name = nm0;
            }
            return s;
        }

        // ---------------------------------------------------------------- students
        public static List<Student> Students(object root)
        {
            List<Student> l = new List<Student>();
            WalkStudents(root, 0, l);
            return l;
        }

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
                st.Middle = J.Str(d, "middleName") ?? "";
                st.Name = (st.First + " " + st.Last).Trim();
                st.Number = J.Str(d, "studentNumber", "localID", "studentID") ?? "";
                st.StateId = J.Str(d, "stateID", "stateId") ?? "";
                IList en = J.L(J.Get(d, "enrollments"));
                Dictionary<string, object> pick = null;
                if (en != null)
                {
                    foreach (object eo in en)
                    {
                        Dictionary<string, object> ed = J.D(eo);
                        if (ed == null) continue;
                        if (pick == null) pick = ed;
                        if (!J.Has(ed, "endDate") && (!ed.ContainsKey("showOnPortal") || J.Bool(ed["showOnPortal"]))) { pick = ed; break; }
                    }
                }
                if (pick != null)
                {
                    st.Grade = J.Str(pick, "grade") ?? "";
                    st.School = J.Str(pick, "schoolName") ?? "";
                    st.Calendar = J.Str(pick, "calendarName", "structureName") ?? "";
                    st.CalendarId = J.Str(pick, "calendarID") ?? "";
                    st.StructureId = J.Str(pick, "structureID") ?? "";
                    double? ey = J.StrictNum(J.Get(pick, "endYear"));
                    if (ey.HasValue) st.EndYear = (int)ey.Value;
                }
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
            if (!string.IsNullOrEmpty(tn))
            {
                term = tn;
                DateTime? ts = J.Date(J.Get(d, "startDate")), te = J.Date(J.Get(d, "endDate"));
                if (ts.HasValue && te.HasValue && !s.TermDates.ContainsKey(tn)) s.TermDates[tn] = new DateTime[] { ts.Value.Date, te.Value.Date };
            }
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
                c.Dropped = J.Bool(J.Get(d, "dropped"));
                c.EndDate = J.Date(J.Get(d, "endDate"));
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
                a.Id = J.Str(d, "objectSectionID", "assignmentID", "id") ?? "";
                a.SectionId = J.Str(d, "sectionID") ?? "";
                a.Course = J.Str(d, "courseName", "sectionName", "course") ?? "";
                a.Term = J.Str(d, "termName", "term") ?? "";
                a.Due = J.Date(J.Get(d, "dueDate", "due", "dueDateTime", "dueOn"));
                a.Assigned = J.Date(J.Get(d, "assignedDate", "assigned"));
                a.Scored = J.Date(J.Get(d, "scoreModifiedDate"));
                a.ScoreText = J.Str(d, "score", "scoreLetter", "scoreLabel") ?? "";
                a.Score = J.NumFirst(d, "scorePoints", "earnedPoints", "points", "score");
                a.Total = J.NumFirst(d, "totalPoints", "pointsPossible", "possiblePoints", "possible", "scoreTotal");
                a.Percent = J.NumFirst(d, "scorePercentage", "scorePercent", "percent", "percentage");
                a.Multiplier = J.NumFirst(d, "multiplier");
                if (!a.Percent.HasValue && a.Score.HasValue && a.Total.HasValue && a.Total.Value > 0)
                    a.Percent = Math.Round(a.Score.Value / a.Total.Value * 100, 1);
                a.Comments = J.StripHtml(J.Str(d, "comments", "comment", "teacherComments") ?? "");
                a.Feedback = J.StripHtml(J.Str(d, "feedback", "studentFeedback") ?? "");
                a.Description = J.StripHtml(J.Str(d, "description", "assignmentDescription", "detail", "details", "instructions") ?? "");
                a.Category = J.Str(d, "categoryName", "gradingCategory", "groupName", "category", "activityName") ?? "";
                a.Scoring = J.Str(d, "scoringType") ?? "";
                a.Missing = J.Bool(J.Get(d, "missing", "isMissing"));
                a.Late = J.Bool(J.Get(d, "late", "isLate"));
                a.Exempt = J.Bool(J.Get(d, "exempt", "isExempt"));
                a.TurnedIn = J.Bool(J.Get(d, "turnedIn"));
                a.Dropped = J.Bool(J.Get(d, "dropped"));
                a.Incomplete = J.Bool(J.Get(d, "incomplete"));
                a.Cheated = J.Bool(J.Get(d, "cheated"));
                a.HasUpload = J.Bool(J.Get(d, "upload", "wysiwygSubmission", "driveSubmission"));
                bool hasScore = a.Score.HasValue || a.ScoreText.Length > 0;
                if (a.Missing) a.Status = "missing";
                else if (a.Exempt) a.Status = "exempt";
                else if (hasScore) a.Status = "graded";
                else if (a.TurnedIn) a.Status = "submitted";
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
                List<Placement> places = new List<Placement>();
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
                        if (per != null)
                        {
                            Placement pl = new Placement();
                            pl.Name = per;
                            pl.Term = tm ?? "";
                            pl.SchedName = J.Str(pd, "periodScheduleName") ?? "";
                            pl.SchedId = J.Str(pd, "periodScheduleID") ?? "";
                            pl.PeriodId = J.Str(pd, "periodID") ?? "";
                            int sq; int.TryParse(J.Str(pd, "periodSequence", "periodScheduleSequence") ?? "0", out sq);
                            pl.Seq = sq;
                            bool dup = false;
                            foreach (Placement q in places) if (q.Name == pl.Name && q.Term == pl.Term && q.SchedId == pl.SchedId) dup = true;
                            if (!dup) places.Add(pl);
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
                    ri.SectionId = J.Str(d, "sectionID") ?? "";
                    ri.Dropped = J.Bool(J.Get(d, "dropped"));
                    ri.EndDate = J.Date(J.Get(d, "endDate"));
                    ri.Teacher = J.Str(d, "teacherDisplay", "teacherName", "teacher") ?? "";
                    ri.Room = J.Str(d, "roomName", "room") ?? "";
                    ri.Places = places;
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

        // ---------------------------------------------------------------- messages / announcements
        static int CompareMsg(Msg a, Msg b)
        {
            DateTime da = a.Date.HasValue ? a.Date.Value : DateTime.MinValue;
            DateTime db = b.Date.HasValue ? b.Date.Value : DateTime.MinValue;
            return db.CompareTo(da);
        }

        static bool HasChildRecords(Dictionary<string, object> d)
        {
            foreach (KeyValuePair<string, object> kv in d)
            {
                IList l = kv.Value as IList;
                if (l != null && l.Count > 0 && J.D(l[0]) != null) return true;
            }
            return false;
        }

        static string PersonName(object v)
        {
            if (v == null) return null;
            Dictionary<string, object> pd = J.D(v);
            if (pd == null) return J.Container(v) ? null : Convert.ToString(v);
            string dn = J.Str(pd, "displayName", "name", "fullName");
            if (dn != null) return dn;
            string f = J.Str(pd, "firstName") ?? "";
            string l = J.Str(pd, "lastName") ?? "";
            string both = (f + " " + l).Trim();
            return both.Length > 0 ? both : null;
        }

        public static List<Msg> Messages(object root, string kind)
        {
            List<Msg> list = new List<Msg>();
            WalkMessages(root, 0, list, kind);
            return list;
        }

        static void WalkMessages(object n, int depth, List<Msg> outp, string kind)
        {
            if (n == null || depth > 7) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkMessages(x, depth + 1, outp, kind);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string subj = J.Str(d, "subject", "title", "messageSubject", "headline", "name");
            string body = J.Str(d, "body", "message", "content", "text", "messageBody", "description", "summary", "html");
            object dv = J.Get(d, "sentDate", "date", "createdDate", "timestamp", "postDate", "startDate", "created", "sent", "dateSent", "modifiedDate", "displayDate", "createDate");
            if (subj == null && kind == "notice" && body != null && J.Has(d, "text"))
            {
                // a Campus announcement: only a date, the school's name and the text (html)
                string plain = J.StripHtml(body).Replace("\r", "");
                string first = "";
                foreach (string ln in plain.Split('\n')) if (ln.Trim().Length > 0) { first = ln.Trim(); break; }
                if (first.Length > 90) first = first.Substring(0, 90).TrimEnd() + "…";
                subj = first.Length > 0 ? first : (J.Str(d, "schoolName") ?? "Announcement");
            }
            if (subj != null && (body != null || dv != null) && !HasChildRecords(d))
            {
                Msg m = new Msg();
                m.Kind = kind;
                m.Id = J.Str(d, "messageID", "noticeID", "announcementID", "id", "_id") ?? "";
                m.Subject = J.StripHtml(subj);
                m.Body = J.StripHtml(body ?? "");
                m.Date = J.Date(dv);
                m.Url = J.Str(d, "url") ?? "";
                m.ContextId = J.Str(d, "contextID") ?? "";
                m.RecipientId = J.Str(d, "messageRecipientID", "recipientID") ?? "";
                if (kind == "notice") m.From = J.Str(d, "schoolName") ?? "";
                if (m.From.Length == 0) m.From = PersonName(J.Get(d, "senderName", "from", "sender", "author", "authorName", "createdBy", "fromName", "userName", "postedBy")) ?? "";
                object rv = J.Get(d, "read", "isRead", "viewed", "seen");
                object uv = J.Get(d, "unread", "isUnread", "isNew", "newMessage");
                if (rv != null) m.Read = J.Bool(rv);
                else if (uv != null) m.Read = !J.Bool(uv);
                outp.Add(m);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkMessages(kv.Value, depth + 1, outp, kind);
            }
        }

        // ---------------------------------------------------------------- documents
        public static List<Doc> Documents(object root)
        {
            List<Doc> list = new List<Doc>();
            WalkDocs(root, 0, list);
            return list;
        }

        static void WalkDocs(object n, int depth, List<Doc> outp)
        {
            if (n == null || depth > 7) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkDocs(x, depth + 1, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string name = J.Str(d, "documentName", "fileName", "filename", "name", "title", "displayName", "description");
            bool idish = J.Has(d, "documentID", "documentId", "id", "fileID", "fileId", "uuid", "attachmentID", "url", "link", "downloadUrl", "path");
            if (name != null && idish && !HasChildRecords(d))
            {
                Doc x = new Doc();
                x.Id = J.Str(d, "documentID", "documentId", "fileID", "fileId", "attachmentID", "id", "uuid") ?? "";
                x.Name = J.StripHtml(name);
                x.Url = J.Str(d, "url", "link", "downloadUrl", "href", "path") ?? "";
                x.Date = J.Date(J.Get(d, "createdDate", "date", "uploadDate", "modifiedDate", "createDate", "created", "lastModified"));
                x.Type = J.Str(d, "fileType", "contentType", "mimeType", "extension", "documentType", "type") ?? "";
                x.Size = J.Str(d, "fileSize", "size", "length") ?? "";
                outp.Add(x);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkDocs(kv.Value, depth + 1, outp);
            }
        }

        // used by the endpoint prober: does this JSON hold any records of the wanted kind?
        public static int CountFor(string key, object parsed)
        {
            if (key == "messages") return Messages(parsed, "message").Count;
            if (key == "notices") return Messages(parsed, "notice").Count;
            if (key == "documents") return Documents(parsed).Count;
            if (key == "calendar") return CalendarDays(parsed).Count;
            if (key == "attendance")
            {
                List<AttRow> rows = new List<AttRow>();
                WalkAttendance(parsed, 0, rows);
                return rows.Count;
            }
            return 1;
        }

        public static bool IsEmptyContainer(object parsed)
        {
            IList l = J.L(parsed);
            if (l != null) return l.Count == 0;
            Dictionary<string, object> d = J.D(parsed);
            if (d != null)
            {
                if (d.Count == 0) return true;
                foreach (KeyValuePair<string, object> kv in d)
                {
                    IList kl = kv.Value as IList;
                    if (kl != null && kl.Count > 0) return false;
                }
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- bell times (period schedules)
        static bool Tod(string t, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            if (string.IsNullOrEmpty(t)) return false;
            Match m = Regex.Match(t, @"(\d{1,2}):(\d{2})");
            if (!m.Success) return false;
            int h = int.Parse(m.Groups[1].Value), mi = int.Parse(m.Groups[2].Value);
            string low = t.ToLowerInvariant();
            if (low.Contains("pm") && h < 12) h += 12;
            if (low.Contains("am") && h == 12) h = 0;
            if (h > 23 || mi > 59) return false;
            ts = new TimeSpan(h, mi, 0);
            return true;
        }

        public static List<BellSchedule> BellSchedules(object root)
        {
            List<BellSchedule> list = new List<BellSchedule>();
            WalkBells(root, 0, "", "", list);
            list.RemoveAll(delegate(BellSchedule b) { return b.Entries.Count == 0; });
            foreach (BellSchedule b in list) b.SortByTime();
            return list;
        }

        static void WalkBells(object n, int depth, string sid, string sname, List<BellSchedule> outp)
        {
            if (n == null || depth > 7) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkBells(x, depth + 1, sid, sname, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            string id = J.Str(d, "periodScheduleID", "scheduleID") ?? sid;
            string nm = J.Str(d, "periodScheduleName", "scheduleName", "scheduleStructureName") ?? sname;
            string s = J.Str(d, "startTime", "beginTime", "start", "periodStart");
            string e = J.Str(d, "endTime", "finishTime", "end", "periodEnd");
            TimeSpan a, b;
            if (s != null && e != null && Tod(s, out a) && Tod(e, out b) && b > a)
            {
                BellSchedule bs = null;
                foreach (BellSchedule x in outp) if (x.Id == id && (nm.Length == 0 || x.Name == nm || x.Name.StartsWith("Schedule "))) { bs = x; break; }
                if (bs == null)
                {
                    bs = new BellSchedule();
                    bs.Id = id;
                    bs.Name = nm.Length > 0 ? nm : (id.Length > 0 ? "Schedule " + id : "Regular");
                    outp.Add(bs);
                }
                string label = J.Str(d, "periodName", "name", "abbreviation", "periodSequence", "sequence") ?? (bs.Entries.Count + 1).ToString();
                foreach (Bell ex in bs.Entries) if (ex.Label == label && ex.Start == a && ex.End == b) return;
                Bell bl = new Bell();
                bl.Label = label; bl.Start = a; bl.End = b;
                object ni = J.Get(d, "isNonInstructional");
                bl.NonInstr = ni != null && (ni is bool || ni is string) && J.Bool(ni);
                bs.Entries.Add(bl);
                return;
            }
            string own = J.Str(d, "name");
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkBells(kv.Value, depth + 1, id, nm.Length > 0 ? nm : (own ?? ""), outp);
            }
        }

        // ---------------------------------------------------------------- school calendar
        public static List<CalDay> CalendarDays(object root)
        {
            List<CalDay> list = new List<CalDay>();
            WalkCal(root, 0, list);
            return list;
        }

        static void WalkCal(object n, int depth, List<CalDay> outp)
        {
            if (n == null || depth > 6) return;
            IList l = J.L(n);
            if (l != null)
            {
                foreach (object x in l) WalkCal(x, depth + 1, outp);
                return;
            }
            Dictionary<string, object> d = J.D(n);
            if (d == null) return;
            DateTime? dt = J.Date(J.Get(d, "date", "calendarDate", "dayDate", "day", "instructionalDate"));
            if (dt.HasValue)
            {
                CalDay c = new CalDay();
                c.Date = dt.Value;
                c.SchedId = J.Str(d, "periodScheduleID") ?? "";
                c.DayId = J.Str(d, "dayID") ?? "";
                string lab = J.Str(d, "rotationDayName", "rotationDay", "dayRotationName", "dayName", "dayLabel", "scheduleDayName", "scheduleDay", "rotationName", "dayRotation");
                c.Label = lab != null ? lab.Trim() : "";
                object ins = J.Get(d, "isSchoolDay", "instructional", "isInstructional", "instructionalDay", "schoolDay");
                if (ins != null && (ins is bool || ins is string)) c.School = J.Bool(ins);
                outp.Add(c);
                return;
            }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (J.Container(kv.Value)) WalkCal(kv.Value, depth + 1, outp);
            }
        }
    }
}
