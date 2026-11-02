using System;
using System.Collections.Generic;

namespace CampusMidnight
{
    // A pretend school for trying the app. It is only ever used when the sign-in link is example.com.
    static class Demo
    {
        public const string User = "demo";
        public const string Pass = "midnight";

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

        public static Snapshot Build()
        {
            DateTime today = DateTime.Today;
            int sy = today.Month >= 8 ? today.Year : today.Year - 1;
            Random rnd = new Random(7);
            Snapshot s = new Snapshot();
            s.At = DateTime.Now;

            Student st = new Student();
            st.Id = "demo-1"; st.First = "Alex"; st.Last = "Rivera"; st.Name = "Alex Rivera";
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
                foreach (string[] c in Classes)
                {
                    double basePct = double.Parse(c[4]);
                    double pct = Math.Max(60, Math.Min(100, basePct + (rnd.NextDouble() * 4 - 2)));
                    pct = Math.Round(pct, 1);
                    Course co = new Course();
                    co.Name = c[0]; co.Teacher = c[1]; co.Room = c[2]; co.Term = t;
                    co.Section = "S" + c[3] + t; co.Percent = pct; co.Letter = Norm.LetterFor(pct); co.TaskName = t;
                    s.Courses.Add(co);
                    for (int k = 0; k <= ti; k++)
                    {
                        TaskGrade tg = new TaskGrade();
                        tg.Name = terms[k];
                        double tp = Math.Round(Math.Max(60, Math.Min(100, basePct + (rnd.NextDouble() * 6 - 3))), 1);
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
                        a.Name = kind + " " + (j + 1);
                        a.Category = kind;
                        a.Total = kind == "Test" ? 100 : (kind == "Quiz" ? 20 : 10);
                        DateTime due;
                        if (ti < curIdx) due = ranges[ti][0].AddDays(7 * (j + 1));
                        else due = today.AddDays(-18 + j * 4 + rnd.Next(0, 2));
                        a.Due = due; a.Assigned = due.AddDays(-5);
                        if (due.Date < today)
                        {
                            bool miss = ti == curIdx && j == 3 && c[0] == "AP US History";
                            if (miss) { a.Missing = true; a.Status = "missing"; a.ScoreText = "Missing"; }
                            else
                            {
                                double sc = Math.Round(a.Total.Value * Math.Max(0.6, Math.Min(1.0, pct / 100.0 + (rnd.NextDouble() * 0.1 - 0.05))), 0);
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

            // inbox
            Msg m1 = new Msg();
            m1.Id = "m1"; m1.Subject = "Welcome to Campus Midnight"; m1.From = "Campus Midnight";
            m1.Body = "This is a pretend school so you can look around. Nothing here is real, and nothing leaves your computer.\n\nSign in with your real Campus link to see your own classes.";
            m1.Date = DateTime.Now.AddHours(-2); m1.Read = false;
            s.Messages.Add(m1);
            Msg m2 = new Msg();
            m2.Id = "m2"; m2.Subject = "Chemistry lab moved to Thursday"; m2.From = "Dr. Patel";
            m2.Body = "Hi everyone, our lab is moving to Thursday this week because of the assembly. Please bring your goggles and your lab notebook.";
            m2.Date = DateTime.Now.AddDays(-1); m2.Read = true;
            s.Messages.Add(m2);
            Msg n1 = new Msg();
            n1.Id = "n1"; n1.Kind = "notice"; n1.Subject = "Picture day is next week"; n1.From = "Example High School";
            n1.Body = "School pictures will be taken during English class. Order forms are in the front office.";
            n1.Date = DateTime.Now.AddDays(-2); n1.Read = false;
            s.Notices.Add(n1);

            Doc d1 = new Doc(); d1.Id = "d1"; d1.Name = "Student handbook"; d1.Type = "PDF"; d1.Date = today.AddDays(-60);
            Doc d2 = new Doc(); d2.Id = "d2"; d2.Name = "Progress report"; d2.Type = "PDF"; d2.Date = today.AddDays(-14);
            s.Docs.Add(d1); s.Docs.Add(d2);
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
