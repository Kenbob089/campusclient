using System;
using System.Collections.Generic;

namespace CampusMidnight
{
    // A pretend school for trying the app. It is only ever used when the sign-in link is example.com.
    static class Demo
    {
        public const string User = "demo";
        public const string User2 = "demo2";   // the same pretend school, with a student who is struggling
        public const string Pass = "midnight";
        public static bool Bad;
        static readonly double[] BadBase = new double[] { 58, 64, 52, 47, 71, 66, 83 };

        public static bool IsDemoHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            host = host.ToLowerInvariant();
            return host == "example.com" || host.EndsWith(".example.com");
        }

        static readonly string[][] Classes = new string[][]
        {
            // name, teacher, room, period, base percent
            new string[] { "AP Calculus AB", "Ms. Okafor", "214", "1", "94" },
            new string[] { "Honors English 11", "Mr. Delgado", "108", "2", "91" },
            new string[] { "AP US History", "Mrs. Lindqvist", "305", "3", "88" },
            new string[] { "Chemistry", "Dr. Patel", "Lab 2", "4", "96" },
            new string[] { "Spanish III", "Sra. Moreno", "112", "5", "92" },
            new string[] { "Digital Art", "Ms. Harlow", "Studio A", "6", "99" },
            new string[] { "Physical Education", "Coach Brandt", "Gym", "7", "100" }
        };

        static int loads;

        static readonly Dictionary<string, string[]> Topics = new Dictionary<string, string[]>
        {
            { "AP Calculus AB", new string[] { "Limits", "Derivative rules", "Chain rule", "Related rates", "Optimization", "Integrals" } },
            { "Honors English 11", new string[] { "The Great Gatsby ch. 3", "Thesis workshop", "Vocabulary unit 5", "Rhetorical analysis", "Journal entry", "Gatsby essay" } },
            { "AP US History", new string[] { "Colonial America", "Constitution reading", "DBQ practice", "Reconstruction", "Gilded Age notes", "Unit 4 review" } },
            { "Chemistry", new string[] { "Mole conversions", "Stoichiometry", "Titration lab", "Periodic trends", "Gas laws", "Bonding" } },
            { "Spanish III", new string[] { "Preterite vs imperfect", "Vocabulario 4", "Oral presentation", "Lectura", "Subjunctive intro", "Listening check" } },
            { "Digital Art", new string[] { "Color wheel", "Perspective sketch", "Logo draft", "Photo edit", "Portfolio piece", "Critique" } },
            { "Physical Education", new string[] { "Fitness log", "Mile run", "Volleyball unit", "Skills check", "Health reading", "Participation" } }
        };
        static string TopicFor(string course, int j)
        {
            string[] t;
            if (Topics.TryGetValue(course, out t)) return t[j % t.Length];
            return "Assignment " + (j + 1);
        }

        public static Snapshot Build()
        {
            Snapshot s = BuildBase();
            // each later refresh brings one new assignment, and the one before it gets graded, so notifications can be tried out
            // like a real school day: something new turns up now and then, not every refresh
            int n = Math.Min(loads / 5, 6);   // one every 5 refreshes
            loads++;
            for (int i = 1; i <= n; i++)
            {
                Assign a = new Assign();
                a.Id = "X" + i; a.Name = i == 1 ? "Quiz: Derivative rules" : (i == 2 ? "Homework: Related rates" : "Quiz: Optimization"); a.Category = "Quiz"; a.Course = "AP Calculus AB"; a.Total = 10;
                Course c = s.Courses.Find(delegate(Course x) { return x.Name == a.Course && x.Term == s.CurrentTerm(); }) ?? s.Courses[0];
                a.SectionId = c.Section; a.Term = c.Term;
                a.Due = DateTime.Today.AddDays(i == n ? 3 : -1); a.Assigned = DateTime.Today.AddDays(-2);
                if (i < n) { a.Score = 9; a.Percent = 90; a.ScoreText = "9/10"; a.Status = "graded"; a.TurnedIn = true; a.Scored = DateTime.Today; }
                else a.Status = "upcoming";
                s.Assigns.Add(a);
            }
            return s;
        }

        static Snapshot BuildBase()
        {
            DateTime today = DateTime.Today;
            int sy = today.Month >= 8 ? today.Year : today.Year - 1;
            Random rnd = new Random(7);
            Snapshot s = new Snapshot();
            s.At = DateTime.Now;

            Student st = new Student();
            st.Id = "demo-1"; if (Bad) { st.First = "Sam"; st.Last = "Hollis"; st.Name = "Sam Hollis"; } else { st.First = "Alex"; st.Last = "Rivera"; st.Name = "Alex Rivera"; }
            st.Number = "1234567"; st.StateId = "9876543210"; st.Grade = "11"; st.School = "Example High School";
            st.Calendar = (sy % 100) + "-" + ((sy + 1) % 100) + " Example HS"; st.CalendarId = "1"; st.EndYear = sy + 1;
            s.Students.Add(st);

            // four quarters
            string[] terms = new string[] { "Q1", "Q2", "Q3", "Q4" };
            DateTime[][] ranges = new DateTime[][]
            {
                new DateTime[] { new DateTime(sy, 8, 24), new DateTime(sy, 10, 30) },
                new DateTime[] { new DateTime(sy, 11, 2), new DateTime(sy + 1, 1, 22) },
                new DateTime[] { new DateTime(sy + 1, 1, 25), new DateTime(sy + 1, 3, 26) },
                new DateTime[] { new DateTime(sy + 1, 3, 29), new DateTime(sy + 1, 6, 4) }
            };
            for (int i = 0; i < 4; i++) { s.Terms.Add(terms[i]); s.TermDates[terms[i]] = ranges[i]; }
            string cur = s.CurrentTerm() ?? "Q1";
            int curIdx = Math.Max(0, s.Terms.IndexOf(cur));

            int idc = 1;
            for (int ti = 0; ti <= curIdx; ti++)
            {
                string t = terms[ti];
                int ci = 0;
                foreach (string[] c in Classes)
                {
                    double floor = Bad ? 20 : 60;
                    double basePct = Bad ? BadBase[ci % BadBase.Length] : double.Parse(c[4]);
                    ci++;
                    double pct = Math.Max(floor, Math.Min(100, basePct + (rnd.NextDouble() * 4 - 2)));
                    pct = Math.Round(pct, 1);
                    Course co = new Course();
                    co.Name = c[0]; co.Teacher = c[1]; co.Room = c[2]; co.Term = t;
                    co.Section = "S" + c[3] + t; co.Percent = pct; co.Letter = Norm.LetterFor(pct); co.TaskName = t;
                    s.Courses.Add(co);
                    for (int k = 0; k <= ti; k++)
                    {
                        TaskGrade tg = new TaskGrade();
                        tg.Name = terms[k];
                        double tp = Math.Round(Math.Max(floor, Math.Min(100, basePct + (rnd.NextDouble() * 6 - 3))), 1);
                        tg.Percent = tp; tg.Letter = Norm.LetterFor(tp);
                        co.Tasks.Add(tg);
                    }
                    // work
                    string[] kinds = new string[] { "Homework", "Quiz", "Classwork", "Test", "Project", "Lab" };
                    int n = ti == curIdx ? 9 : 6;
                    for (int j = 0; j < n; j++)
                    {
                        Assign a = new Assign();
                        a.Id = "A" + (idc++);
                        a.SectionId = co.Section; a.Course = co.Name; a.Term = t;
                        string kind = kinds[rnd.Next(kinds.Length)];
                        a.Name = kind + ": " + TopicFor(co.Name, j + ti);
                        a.Category = kind;
                        a.Total = kind == "Test" ? 100 : (kind == "Quiz" ? 20 : 10);
                        DateTime due;
                        if (ti < curIdx) due = ranges[ti][0].AddDays(7 * (j + 1));
                        else due = today.AddDays(-18 + j * 4 + rnd.Next(0, 2));
                        a.Due = due; a.Assigned = due.AddDays(-5);
                        if (due.Date < today)
                        {
                            bool miss = Bad ? (j % 3 == 1 && (ti == curIdx || j == 4)) : (ti == curIdx && j == 3 && c[0] == "AP US History");
                            if (miss) { a.Missing = true; a.Status = "missing"; a.ScoreText = "Missing"; }
                            else
                            {
                                double sc = Math.Round(a.Total.Value * Math.Max(floor / 100.0, Math.Min(1.0, pct / 100.0 + (rnd.NextDouble() * 0.1 - 0.05))), 0);
                                a.Score = sc; a.Percent = Math.Round(sc / a.Total.Value * 100, 1);
                                a.ScoreText = sc + "/" + a.Total; a.Status = "graded"; a.Scored = due.AddDays(2); a.TurnedIn = true;
                            }
                        }
                        else
                        {
                            a.Status = "upcoming";
                            a.ScoreText = "";
                        }
                        a.Description = a.Name + " for " + co.Name + ".";
                        s.Assigns.Add(a);
                    }
                }
            }

            foreach (string[] c in Classes)
            {
                RosterItem r = new RosterItem();
                r.Course = c[0]; r.Teacher = c[1]; r.Room = c[2]; r.Period = c[3]; r.Term = "Q1, Q2, Q3, Q4";
                r.SectionId = "S" + c[3] + cur; r.Order = int.Parse(c[3]);
                s.Roster.Add(r);
            }
            // one class that was dropped, to show how that looks
            Course dr = new Course();
            dr.Name = "Study Hall"; dr.Teacher = "Mr. Whitfield"; dr.Room = "Library"; dr.Term = cur; dr.Section = "SH" + cur;
            dr.Dropped = true; dr.EndDate = today.AddDays(-20); dr.Percent = 100; dr.Letter = "A"; dr.TaskName = cur;
            s.Courses.Add(dr);

            // calendar: weekdays are school days, with Thanksgiving week and winter break off
            BellSchedule reg = Regular();
            reg.Id = "1";
            s.AutoBells.Add(reg);
            for (DateTime d = new DateTime(sy, 8, 24); d <= new DateTime(sy + 1, 6, 4); d = d.AddDays(1))
            {
                CalDay cd = new CalDay();
                cd.Date = d; cd.SchedId = "1"; cd.DayId = "1";
                bool weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                bool thanks = d >= new DateTime(sy, 11, 25) && d <= new DateTime(sy, 11, 27);
                bool winter = d >= new DateTime(sy, 12, 21) && d <= new DateTime(sy + 1, 1, 1);
                cd.School = !(weekend || thanks || winter);
                s.CalDays.Add(cd);
            }
            s.Link();
            s.LabelDays();

            // attendance: a few marks per class in every quarter so far
            for (int ta = 0; ta <= curIdx; ta++)
            {
                foreach (string[] c in Classes)
                {
                    AttCourse ac = new AttCourse();
                    ac.Name = c[0]; ac.Term = terms[ta];
                    DateTime lo = ranges[ta][0], hi = ta == curIdx ? today.AddDays(-1) : ranges[ta][1];
                    // a few whole days out sick: every class gets an absence on those dates
                    int fullDays = Bad ? 3 : (ta == 0 ? 1 : 0);
                    for (int fdi = 0; fdi < fullDays; fdi++)
                    {
                        DateTime fd = lo.AddDays(6 + fdi * 9 + ta * 2);
                        while (fd.DayOfWeek == DayOfWeek.Saturday || fd.DayOfWeek == DayOfWeek.Sunday) fd = fd.AddDays(1);
                        if (fd > hi) continue;
                        AttEvent fe = new AttEvent();
                        fe.Kind = "Absent"; fe.Term = terms[ta]; fe.Date = fd; fe.Time = (7 + int.Parse(c[3])) + ":00"; fe.Period = c[3];
                        fe.Title = Bad && fdi == 1 ? "Skipped school" : "Out sick";
                        fe.Status = Bad && fdi == 1 ? "Unexcused" : "Excused";
                        if (fe.Status == "Excused") fe.Comment = "Parent note on file";
                        ac.Events.Add(fe);
                    }
                    int span = Math.Max(1, (int)(hi - lo).TotalDays);
                    int per = int.Parse(c[3]);
                    string time = (7 + per) + ":00";
                    int nAbs = Bad ? rnd.Next(2, 6) : (rnd.Next(0, 5) == 0 ? 1 : 0);
                    int nTar = Bad ? rnd.Next(2, 7) : (rnd.Next(0, 4) == 0 ? 1 : 0);
                    string[] badWhy = new string[] { "Unexcused absence", "Skipped class", "Absent, no note", "Illness" };
                    for (int k = 0; k < nAbs + nTar; k++)
                    {
                        DateTime dd = lo.AddDays(rnd.Next(span));
                        while (dd.DayOfWeek == DayOfWeek.Saturday || dd.DayOfWeek == DayOfWeek.Sunday) dd = dd.AddDays(1);
                        AttEvent e = new AttEvent();
                        e.Term = terms[ta]; e.Date = dd; e.Time = time; e.Period = c[3];
                        if (k < nAbs)
                        {
                            e.Kind = "Absent";
                            e.Status = Bad ? (rnd.Next(3) == 0 ? "Excused" : "Unexcused") : "Excused";
                            e.Title = Bad ? badWhy[rnd.Next(badWhy.Length)] : "Illness";
                            if (e.Status == "Excused") e.Comment = "Parent called the office";
                        }
                        else
                        {
                            e.Kind = "Tardy";
                            e.Status = Bad && rnd.Next(2) == 0 ? "Unexcused" : "Excused";
                            e.Title = "Arrived late";
                            e.Comment = Bad ? "Came in after the bell" : "Doctor appointment";
                        }
                        ac.Events.Add(e);
                    }
                    if (!Bad && per == 5)
                    {
                        AttEvent ev = new AttEvent();
                        ev.Kind = "Present"; ev.Term = terms[ta]; ev.Date = lo.AddDays(Math.Min(span, 20)); ev.Time = time; ev.Title = "Field Trip"; ev.Comment = "Speaker Series"; ev.Status = "Exempt";
                        ac.Events.Add(ev);
                    }
                    ac.Events.Sort(delegate(AttEvent a1, AttEvent b1) { return (b1.Date ?? DateTime.MinValue).CompareTo(a1.Date ?? DateTime.MinValue); });
                    s.AttCourses.Add(ac);
                }
            }

            // inbox
            Msg m1 = new Msg();
            m1.Id = "m1"; m1.Subject = "Welcome to Noctua"; m1.From = "Noctua";
            m1.Body = "This is a pretend school so you can look around. Nothing here is real, and nothing leaves your computer.\n\nSign in with your real Campus link to see your own classes.";
            m1.Date = DateTime.Now.AddHours(-2); m1.Read = false;
            s.Messages.Add(m1);
            Msg m2 = new Msg();
            m2.Id = "m2"; m2.Subject = "Chemistry lab moved to Thursday"; m2.From = "Dr. Patel";
            m2.Body = "Hi everyone, our lab is moving to Thursday this week because of the assembly. Please bring your goggles and your lab notebook.";
            m2.Date = DateTime.Now.AddDays(-1); m2.Read = true;
            s.Messages.Add(m2);
            Msg m3 = new Msg();
            m3.Id = "m3"; m3.Subject = "AP US History: DBQ rewrite due Friday"; m3.From = "Mrs. Lindqvist";
            m3.Body = "Please revise your DBQ using the feedback I left on your essay and turn it in by Friday. Come see me at lunch if you want to go over it.";
            m3.Date = DateTime.Now.AddDays(-3); m3.Read = true;
            s.Messages.Add(m3);
            Msg m4 = new Msg();
            m4.Id = "m4"; m4.Subject = "Spanish III oral presentations next week"; m4.From = "Sra. Moreno";
            m4.Body = "Presentations start Monday. Sign up for a slot on the sheet outside my room. Remember: three minutes, no notes on the slides.";
            m4.Date = DateTime.Now.AddDays(-5); m4.Read = true;
            s.Messages.Add(m4);
            Msg n2 = new Msg();
            n2.Id = "n2"; n2.Kind = "notice"; n2.Subject = "Early dismissal on Wednesday"; n2.From = "Example High School";
            n2.Body = "Students will be released at 12:30 for teacher training. Buses will run at the early time.";
            n2.Date = DateTime.Now.AddDays(-4); n2.Read = true;
            s.Notices.Add(n2);
            Msg n1 = new Msg();
            n1.Id = "n1"; n1.Kind = "notice"; n1.Subject = "Picture day is next week"; n1.From = "Example High School";
            n1.Body = "School pictures will be taken during English class. Order forms are in the front office.";
            n1.Date = DateTime.Now.AddDays(-2); n1.Read = false;
            s.Notices.Add(n1);

            Doc d1 = new Doc(); d1.Id = "d1"; d1.Name = "Student handbook"; d1.Type = "PDF"; d1.Date = today.AddDays(-60);
            Doc d2 = new Doc(); d2.Id = "d2"; d2.Name = "Progress report"; d2.Type = "PDF"; d2.Date = today.AddDays(-14);
            Doc d3 = new Doc(); d3.Id = "d3"; d3.Name = "Course selection form.docx"; d3.Type = "DOCX"; d3.Date = today.AddDays(-30);
            Doc d4 = new Doc(); d4.Id = "d4"; d4.Name = "Lab data.xlsx"; d4.Type = "XLSX"; d4.Date = today.AddDays(-9);
            Doc d5 = new Doc(); d5.Id = "d5"; d5.Name = "Yearbook photo.jpg"; d5.Type = "JPG"; d5.Date = today.AddDays(-4);
            s.Docs.Add(d1); s.Docs.Add(d2); s.Docs.Add(d3); s.Docs.Add(d4); s.Docs.Add(d5);
            return s;
        }

        public static BellSchedule Regular()
        {
            BellSchedule b = new BellSchedule();
            b.Name = "Regular";
            string[] starts = new string[] { "8:00", "8:55", "9:50", "10:45", "12:15", "13:10", "14:05" };
            string[] ends = new string[] { "8:50", "9:45", "10:40", "11:35", "13:05", "14:00", "14:55" };
            for (int i = 0; i < starts.Length; i++)
            {
                Bell bl = new Bell();
                bl.Label = (i + 1).ToString();
                TimeSpan a, e;
                Bell.TryTime(starts[i], out a); Bell.TryTime(ends[i], out e);
                bl.Start = a; bl.End = e;
                b.Entries.Add(bl);
            }
            return b;
        }
    }
}
