using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CampusMidnight
{
    // ---------------------------------------------------------------- small dialogs
    class NameForm : DarkForm
    {
        public string Value = "";
        readonly DarkInput box;

        public NameForm(string title, string caption, string initial) : base(title)
        {
            ClientSize = new Size(Theme.S(480), Theme.S(230));
            Fw = ClientSize.Width - Theme.S(56);
            Lbl(caption, Theme.Body, Theme.T2, Theme.S(26));
            Gap(8);
            box = new DarkInput(false, false);
            box.Text = initial;
            Add(box, Theme.S(44));
            Gap(18);
            PillButton ok = new PillButton(); ok.Text = "Save"; ok.Kind = 1;
            ok.Click += delegate { Done(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
            box.Box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Done(); }
            };
            Shown += delegate { box.Box.Focus(); box.Box.SelectAll(); };
        }

        void Done()
        {
            string t = box.Text.Trim();
            if (t.Length == 0 || t.IndexOfAny(new char[] { '|', ';', '~', '=' }) >= 0) { box.Invalid = true; box.Invalidate(); return; }
            Value = t;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ---------------------------------------------------------------- the Now page
    partial class MainForm
    {
        // ============================================================ Now
        void BuildNow()
        {
            Header("Now", DateTime.Now.ToString("dddd, MMMM d"));
            if (snap == null)
            {
                AddLabel("Loading your classes…", Theme.Body, Theme.T2, X0(), vy, CW(), Theme.S(28));
                vy += Theme.S(40);
                return;
            }
            if (cfg.Schedules.Count == 0)
            {
                AddNotice("Set up your schedule once and this page shows your current class, a live countdown and what comes next.");
                AddButton("Set up my schedule", "\uE713", 1, X0(), vy, delegate { StartScheduleEdit(); });
                vy += Theme.S(56);
                return;
            }

            plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            NowHero hero = new NowHero();
            hero.Plan = plan;
            hero.DayText = plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) : "";
            DateTime nx = Sched.NextSchoolDay(cfg, snap, DateTime.Today);
            string nl, nf;
            nl = Sched.LabelFor(cfg, snap, nx, out nf);
            string nextText = "Next school day: " + nx.ToString("dddd, MMM d") + (nl.Length > 0 ? "  ·  " + Sched.DayName(nl) : "") + ".";
            if (!plan.School)
            {
                hero.Headline = "No school today";
                hero.Detail = plan.WhyNot + " " + nextText;
            }
            else if (plan.Slots.Count == 0)
            {
                hero.Headline = "Nothing scheduled";
                hero.Detail = "The bell schedule for today has no periods yet.";
            }
            else hero.Detail = nextText;
            liveHero = hero;
            AddCard(hero, X0(), vy, CW(), NowHero.HeightFor());
            vy += NowHero.HeightFor() + Theme.S(16);

            int gap = Theme.S(16);
            int cols = CW() >= Theme.S(900) ? 4 : 2;
            int tw = (CW() - gap * (cols - 1)) / cols;
            int th = Theme.S(100);
            TimeSpan now = DateTime.Now.TimeOfDay;
            TileCard t1 = new TileCard();
            t1.Big = plan.DayLabel.Length > 0 ? plan.DayLabel : "–";
            t1.Label = plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) + (plan.LabelFrom.Length > 0 ? ", " + plan.LabelFrom : "") : "Day (turn on rotating days to see it)";
            TileCard t2 = new TileCard();
            t2.Big = plan.Sched != null ? plan.Sched.Name : "–";
            t2.Label = "Bell schedule today";
            TileCard t3 = new TileCard();
            t3.Big = Sched.ClassesLeft(plan, now).ToString();
            t3.Label = "Classes left today";
            TileCard t4 = new TileCard();
            t4.Big = plan.Slots.Count > 0 ? Bell.ClockShort(plan.Slots[plan.Slots.Count - 1].B.End) : "–";
            t4.Label = "School ends";
            TileCard[] tl = new TileCard[] { t1, t2, t3, t4 };
            for (int i = 0; i < tl.Length; i++)
                AddCard(tl[i], X0() + (i % cols) * (tw + gap), vy + (i / cols) * (th + gap), tw, th);
            vy += ((tl.Length + cols - 1) / cols) * (th + gap);

            if (plan.School && plan.Slots.Count > 0)
            {
                NowTimeline tline = new NowTimeline();
                tline.Plan = plan;
                tline.SlotClick += delegate(Slot s) { if (s.C != null) OpenCourse(s.C); };
                liveTimeline = tline;
                AddCard(tline, X0(), vy, CW(), tline.PreferredHeight());
                vy += tline.PreferredHeight() + gap;
            }
            AddButton("Schedule setup", "\uE713", 0, X0(), vy, delegate { StartScheduleEdit(); });
            vy += Theme.S(56);
        }

        bool redoPending;

        // rebuild the page once the current event has finished; ignores repeats (focus changes while the page is torn down)
        void Redo()
        {
            if (redoPending || !IsHandleCreated) return;
            redoPending = true;
            BeginInvoke(new MethodInvoker(delegate
            {
                try { ShowView(true); }
                finally { redoPending = false; }
            }));
        }
    }
}
