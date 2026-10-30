using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace CampusMidnight
{
    // A dark, fixed-size window with a scrolling page inside. Used for class, assignment and message details.
    class DarkForm : Form
    {
        protected ScrollPanel Page;
        protected int Fx, Fw, Fy;

        public DarkForm(string title)
        {
            Text = title;
            BackColor = Theme.Bg0;
            ForeColor = Theme.T1;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            int hMax = Screen.PrimaryScreen.WorkingArea.Height - Theme.S(90);
            ClientSize = new Size(Theme.S(680), Math.Min(Theme.S(800), hMax));
            Page = new ScrollPanel();
            Page.Dock = DockStyle.Fill;
            Controls.Add(Page);
            Fx = Theme.S(28);
            Fw = ClientSize.Width - Theme.S(56) - SystemInformation.VerticalScrollBarWidth;
            Fy = Theme.S(24);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected Label Lbl(string text, Font f, Color c, int h)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.AutoEllipsis = true;
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = Theme.Bg0;
            l.SetBounds(Fx, Fy, Fw, h);
            Page.Controls.Add(l);
            Fy += h;
            return l;
        }

        protected Label Wrapped(string text, Font f, Color c)
        {
            int h = Theme.WrapHeight(text, f, Fw) + Theme.S(6);
            Label l = Lbl(text, f, c, h);
            l.AutoEllipsis = false;
            return l;
        }

        protected void Gap(int px) { Fy += Theme.S(px); }

        protected T Add<T>(T c, int h) where T : Control
        {
            c.SetBounds(Fx, Fy, Fw, h);
            Page.Controls.Add(c);
            Fy += h;
            return c;
        }

        protected void Finish()
        {
            Page.AutoScrollMinSize = new Size(0, Fy + Theme.S(28));
        }

        protected void Buttons(params PillButton[] btns)
        {
            int x = Fx;
            foreach (PillButton b in btns)
            {
                int w = b.PreferredWidth();
                b.SetBounds(x, Fy, w, Theme.S(40));
                Page.Controls.Add(b);
                x += w + Theme.S(10);
            }
            Fy += Theme.S(52);
        }
    }

    // ------------------------------------------------------------------ the big score panel
    class ScoreCard : CardBase
    {
        public Assign A;

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (A == null) return;
            int pad = Theme.S(24);
            Color col = Theme.GradeColor(A.Percent);
            string big;
            if (A.Score.HasValue) big = Theme.Fmt(A.Score) + (A.Total.HasValue ? " / " + Theme.Fmt(A.Total) : "");
            else if (A.ScoreText.Length > 0) big = A.ScoreText;
            else big = "–";
            if (A.Status == "missing" && !A.Score.HasValue) big = "Missing";
            Theme.DrawText(g, big, Theme.Huge, A.Score.HasValue || A.ScoreText.Length > 0 ? col : Theme.T2,
                new Rectangle(r.X + pad, r.Y + Theme.S(14), r.Width / 2, Theme.S(54)));
            string right = "";
            if (A.Percent.HasValue) right = Math.Round(A.Percent.Value) + "%  " + Norm.LetterFor(A.Percent);
            Theme.DrawText(g, right, Theme.Big, col, new Rectangle(r.X + r.Width / 2, r.Y + Theme.S(14), r.Width / 2 - pad, Theme.S(54)), true, false);

            Rectangle track = new Rectangle(r.X + pad, r.Y + Theme.S(80), r.Width - pad * 2, Theme.S(8));
            Theme.FillRound(g, track, Theme.S(4), Theme.Bg4);
            if (A.Percent.HasValue)
            {
                double p = Math.Max(2, Math.Min(100, A.Percent.Value));
                int fw = (int)(track.Width * p / 100.0);
                if (fw >= 8) Theme.FillRound(g, new Rectangle(track.X, track.Y, fw, track.Height), Theme.S(4), col);
            }
            string status;
            switch (A.Status)
            {
                case "graded": status = "Graded" + (A.Scored.HasValue ? " on " + A.Scored.Value.ToString("MMM d") : ""); break;
                case "missing": status = "Marked missing by your teacher"; break;
                case "exempt": status = "Exempt, not counted in your grade"; break;
                case "submitted": status = "Turned in, waiting to be graded"; break;
                case "ungraded": status = "Not graded yet"; break;
                default: status = "Not due yet"; break;
            }
            Theme.DrawText(g, status, Theme.Small, Theme.T2, new Rectangle(r.X + pad, r.Y + Theme.S(96), r.Width - pad * 2, Theme.S(24)));
        }
    }

    // ------------------------------------------------------------------ one assignment
    class AssignForm : DarkForm
    {
        public AssignForm(Assign a, Course course, Action openCampus) : base(a.Name)
        {
            Panel strip = new Panel();
            strip.BackColor = Theme.Tag(a.Course);
            strip.SetBounds(Fx, Fy + Theme.S(4), Theme.S(5), Theme.S(18));
            Page.Controls.Add(strip);
            Label cl = new Label();
            cl.AutoSize = false; cl.UseMnemonic = false; cl.AutoEllipsis = true;
            cl.Text = a.Course; cl.Font = Theme.Semi; cl.ForeColor = Theme.T2; cl.BackColor = Theme.Bg0;
            cl.SetBounds(Fx + Theme.S(14), Fy, Fw - Theme.S(14), Theme.S(26));
            Page.Controls.Add(cl);
            Fy += Theme.S(32);
            Wrapped(a.Name, Theme.Big, Theme.T1);
            Gap(10);

            ScoreCard sc = new ScoreCard();
            sc.A = a;
            Add(sc, Theme.S(132));
            Gap(16);

            ListCard dc = new ListCard();
            dc.Heading = "Details";
            dc.RowHeight = Theme.S(44);
            AddRow(dc, "Due", a.Due.HasValue ? a.Due.Value.ToString("dddd, MMMM d, yyyy") : "No due date");
            if (a.Assigned.HasValue) AddRow(dc, "Assigned", a.Assigned.Value.ToString("dddd, MMMM d, yyyy"));
            if (a.Category.Length > 0) AddRow(dc, "Category", a.Category);
            if (a.Scoring.Length > 0) AddRow(dc, "Scoring", a.Scoring);
            if (a.Multiplier.HasValue && Math.Abs(a.Multiplier.Value - 1) > 0.001) AddRow(dc, "Weight", "×" + Theme.Fmt(a.Multiplier));
            if (a.Term.Length > 0) AddRow(dc, "Term", a.Term);
            if (course != null && course.Teacher.Length > 0) AddRow(dc, "Teacher", course.Teacher);
            List<string> flags = new List<string>();
            if (a.Late) flags.Add("Late");
            if (a.Missing) flags.Add("Missing");
            if (a.TurnedIn) flags.Add("Turned in");
            if (a.Dropped) flags.Add("Dropped from grade");
            if (a.Incomplete) flags.Add("Incomplete");
            if (a.Cheated) flags.Add("Cheating flag");
            if (a.HasUpload) flags.Add("Accepts uploads");
            if (flags.Count > 0) AddRow(dc, "Flags", string.Join(", ", flags.ToArray()));
            Add(dc, dc.PreferredHeight());
            Gap(16);

            TextCard cm = new TextCard();
            cm.Heading = "Teacher comments";
            cm.Body = a.Comments.Length > 0 ? a.Comments : "No comments on this assignment.";
            cm.BodyColor = a.Comments.Length > 0 ? Theme.T1 : Theme.T3;
            Add(cm, cm.PreferredHeight(Fw));
            Gap(16);
            if (a.Feedback.Length > 0)
            {
                TextCard fb = new TextCard();
                fb.Heading = "Feedback";
                fb.Body = a.Feedback;
                Add(fb, fb.PreferredHeight(Fw));
                Gap(16);
            }
            if (a.Description.Length > 0)
            {
                TextCard ds = new TextCard();
                ds.Heading = "Description";
                ds.Body = a.Description;
                Add(ds, ds.PreferredHeight(Fw));
                Gap(16);
            }

            PillButton open = new PillButton();
            open.Text = "Open in Campus";
            open.Glyph = "";
            open.Click += delegate { if (openCampus != null) openCampus(); };
            PillButton copy = new PillButton();
            copy.Text = "Copy summary";
            copy.Glyph = "";
            Assign aa = a;
            copy.Click += delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(aa.Name + " (" + aa.Course + ")");
                    if (aa.Due.HasValue) sb.AppendLine("Due: " + aa.Due.Value.ToString("yyyy-MM-dd"));
                    if (aa.Score.HasValue) sb.AppendLine("Score: " + Theme.Fmt(aa.Score) + " / " + Theme.Fmt(aa.Total) + (aa.Percent.HasValue ? " (" + Theme.Fmt(aa.Percent) + "%)" : ""));
                    if (aa.Comments.Length > 0) sb.AppendLine("Comments: " + aa.Comments);
                    Clipboard.SetText(sb.ToString());
                    copy.Text = "Copied";
                }
                catch (Exception) { copy.Text = "Could not copy"; }
            };
            Buttons(open, copy);
            Finish();
        }

        static void AddRow(ListCard lc, string k, string v)
        {
            Item it = new Item();
            it.Title = k;
            it.Right = v;
            it.RightColor = Theme.T1;
            lc.Items.Add(it);
        }
    }

    // ------------------------------------------------------------------ one inbox message or announcement
    class MessageForm : DarkForm
    {
        public MessageForm(Msg m, Action openCampus) : base(m.Subject.Length > 0 ? m.Subject : "Message")
        {
            Lbl(m.Kind == "notice" ? "ANNOUNCEMENT" : "MESSAGE", Theme.SemiSmall, Theme.AccText, Theme.S(24));
            Wrapped(m.Subject, Theme.Big, Theme.T1);
            string meta = m.From;
            if (m.Date.HasValue) meta += (meta.Length > 0 ? "  ·  " : "") + m.Date.Value.ToString("dddd, MMMM d, yyyy");
            if (meta.Length > 0) Lbl(meta, Theme.Body, Theme.T2, Theme.S(28));
            Gap(14);
            TextCard body = new TextCard();
            body.Body = m.Body.Length > 0 ? m.Body : "This message has no text.";
            body.BodyColor = m.Body.Length > 0 ? Theme.T1 : Theme.T3;
            Add(body, body.PreferredHeight(Fw));
            Gap(16);
            PillButton open = new PillButton();
            open.Text = "Open in Campus";
            open.Glyph = "";
            open.Click += delegate { if (openCampus != null) openCampus(); };
            PillButton copy = new PillButton();
            copy.Text = "Copy text";
            copy.Glyph = "";
            string txt = m.Body;
            copy.Click += delegate
            {
                try { Clipboard.SetText(txt); copy.Text = "Copied"; }
                catch (Exception) { copy.Text = "Could not copy"; }
            };
            Buttons(open, copy);
            Finish();
        }
    }

    // ------------------------------------------------------------------ one class
    class CourseForm : DarkForm
    {
        readonly Course course;
        readonly Action openCampus;

        public CourseForm(Course c, List<Assign> list, Action openCampusAction, Action<int> setLevel) : base(c.Name)
        {
            course = c;
            openCampus = openCampusAction;
            Wrapped(c.Name, Theme.Title, Theme.T1);
            string meta = c.Teacher;
            if (c.Room.Length > 0) meta += (meta.Length > 0 ? "  ·  " : "") + "Room " + c.Room;
            if (c.Term.Length > 0) meta += (meta.Length > 0 ? "  ·  " : "") + c.Term;
            Lbl(meta, Theme.Body, Theme.T2, Theme.S(26));
            if (c.Dropped)
            {
                Gap(4);
                NoticeCard dn = new NoticeCard();
                dn.Text2 = "You dropped this class" + (c.EndDate.HasValue ? " on " + c.EndDate.Value.ToString("MMMM d, yyyy") : "")
                    + ". It is left out of your GPA and averages, and its work is hidden from Home.";
                Add(dn, dn.PreferredHeight(Fw));
            }
            Gap(8);
            Label big = Lbl((string.IsNullOrEmpty(c.Letter) ? "–" : c.Letter) + (c.Percent.HasValue ? "   " + Theme.Fmt(c.Percent) + "%" : ""),
                Theme.Huge, Theme.GradeColor(c.Percent), Theme.S(56));
            Gap(8);

            if (!c.Dropped && setLevel != null)
            {
                Lbl("COUNTS IN WEIGHTED GPA AS", Theme.SemiSmall, Theme.T3, Theme.S(22));
                Segmented lv = new Segmented();
                lv.SetOptions(new string[] { "Regular", "Honors (+0.5)", "AP / dual (+1.0)" }, Math.Max(0, Math.Min(2, c.Level)));
                lv.Changed += delegate
                {
                    int chosen = lv.Selected;
                    course.Level = chosen;
                    setLevel(chosen);
                };
                lv.Location = new Point(Fx, Fy);
                Page.Controls.Add(lv);
                Fy += lv.Height + Theme.S(18);
            }

            // trend of graded work in this class
            List<Assign> graded = list.FindAll(delegate(Assign a) { return a.Status == "graded" && a.Percent.HasValue && a.Due.HasValue; });
            graded.Sort(delegate(Assign a, Assign b) { return a.Due.Value.CompareTo(b.Due.Value); });
            if (graded.Count >= 2)
            {
                LineChart lc = new LineChart();
                lc.Heading = "Scores over time";
                foreach (Assign a in graded) { lc.Xs.Add(a.Due.Value); lc.Ys.Add(a.Percent.Value); }
                Add(lc, Theme.S(230));
                Gap(16);
            }

            if (c.Tasks.Count > 1)
            {
                ListCard tc = new ListCard();
                tc.Heading = "By grading period";
                foreach (TaskGrade t in c.Tasks)
                {
                    Item it = new Item();
                    it.Title = t.Name.Length > 0 ? t.Name : "Grade";
                    it.Right = Rows.GradeText(t);
                    it.RightColor = Theme.GradeColor(t.Percent);
                    tc.Items.Add(it);
                }
                Add(tc, tc.PreferredHeight());
                Gap(16);
            }

            ListCard ac = new ListCard();
            ac.Heading = "Assignments  ·  click one for details and comments";
            ac.EmptyText = "No assignments found for this class.";
            ac.Clickable = true;
            Rows.SortAssigns(list, false);
            int n = 0;
            foreach (Assign a in list)
            {
                if (n++ >= 200) break;
                Item it = Rows.ForAssign(a);
                it.Tag = a;
                ac.Items.Add(it);
            }
            ac.ItemClick += delegate(Item it)
            {
                Assign a = it.Tag as Assign;
                if (a == null) return;
                using (AssignForm f = new AssignForm(a, course, openCampus)) { f.ShowDialog(this); }
            };
            Add(ac, ac.PreferredHeight());
            Finish();
        }
    }
}
