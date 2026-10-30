using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CampusMidnight
{
    // ------------------------------------------------------------------ the big "right now" card
    class NowHero : CardBase
    {
        public DayPlan Plan;
        public string Headline = "", Detail = "";   // used when there is no school
        public string DayText = "";                  // e.g. "A Day"

        public static int HeightFor() { return Theme.S(236); }

        protected override Color FillColor { get { return Theme.Bg2; } }

        void Ring(Graphics g, Rectangle rr, double frac, Color arcColor)
        {
            int pw = Theme.S(11);
            using (Pen track = new Pen(Theme.Bg4, pw))
            using (Pen arc = new Pen(arcColor, pw))
            {
                arc.StartCap = LineCap.Round;
                arc.EndCap = LineCap.Round;
                Rectangle ar = Rectangle.Inflate(rr, -pw / 2, -pw / 2);
                g.DrawEllipse(track, ar);
                float sweep = (float)(Math.Max(0, Math.Min(1, frac)) * 360.0);
                if (sweep > 2f) g.DrawArc(arc, ar, -90f, sweep);
            }
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            // soft accent wash behind everything
            using (GraphicsPath clip = Theme.Round(r, Theme.S(14)))
            {
                g.SetClip(clip);
                using (LinearGradientBrush lg = new LinearGradientBrush(r, Theme.AccDeep, Theme.Bg2, 0f))
                {
                    g.FillRectangle(lg, r);
                }
                g.ResetClip();
            }
            int pad = Theme.S(28);
            int d = Theme.S(160);
            Rectangle rr = new Rectangle(r.X + pad, r.Y + (r.Height - d) / 2, d, d);
            int tx = rr.Right + Theme.S(32);
            int tw = r.Right - pad - tx;

            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            string ph = Sched.Phase(Plan, now, out cur, out next);

            if (ph == "class")
            {
                Slot s = Plan.Slots[cur];
                TimeSpan total = s.B.End - s.B.Start;
                TimeSpan left = s.B.End - now;
                double frac = total.TotalSeconds > 0 ? (total - left).TotalSeconds / total.TotalSeconds : 0;
                Ring(g, rr, frac, Theme.Acc);
                CenterCount(g, rr, left);
                string kind = s.IsClass ? "IN CLASS" : (s.Free ? "FREE PERIOD" : "NOW");
                int x = tx;
                x += Theme.DrawPill(g, kind, Theme.SemiSmall, Theme.Acc, Color.White, x, r.Y + Theme.S(34), Theme.S(24)) + Theme.S(10);
                string per = s.IsClass && s.R != null && s.R.Period.Length > 0 ? "Period " + s.R.Period : s.B.Label;
                Theme.DrawText(g, per, Theme.SemiSmall, Theme.T3, new Rectangle(x, r.Y + Theme.S(34), tw, Theme.S(24)));
                Theme.DrawText(g, s.Title, Theme.Title, Theme.T1, new Rectangle(tx, r.Y + Theme.S(66), tw, Theme.S(40)));
                string sub = "";
                if (s.R != null)
                {
                    sub = s.R.Teacher;
                    if (s.R.Room.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + s.R.Room;
                }
                else sub = s.B.Text();
                Theme.DrawText(g, sub, Theme.Body, Theme.T2, new Rectangle(tx, r.Y + Theme.S(108), tw, Theme.S(26)));
                // linear bar + times
                Rectangle bar = new Rectangle(tx, r.Y + Theme.S(148), tw, Theme.S(8));
                Theme.FillRound(g, bar, Theme.S(4), Theme.Bg4);
                int fw = (int)(bar.Width * Math.Max(0, Math.Min(1, frac)));
                if (fw >= 8) Theme.FillRound(g, new Rectangle(bar.X, bar.Y, fw, bar.Height), Theme.S(4), Theme.Acc);
                Theme.DrawText(g, Bell.Clock(s.B.Start), Theme.Small, Theme.T3, new Rectangle(tx, r.Y + Theme.S(162), Theme.S(120), Theme.S(22)));
                Theme.DrawText(g, "Ends " + Bell.Clock(s.B.End), Theme.Small, Theme.T2, new Rectangle(tx + tw - Theme.S(180), r.Y + Theme.S(162), Theme.S(180), Theme.S(22)), true, false);
                if (next >= 0)
                {
                    Slot n = Plan.Slots[next];
                    Theme.DrawText(g, "Next:  " + n.Title + "  ·  " + Bell.ClockShort(n.B.Start), Theme.Small, Theme.T2,
                        new Rectangle(tx, r.Y + Theme.S(190), tw, Theme.S(24)));
                }
                else Theme.DrawText(g, "Last block of the day", Theme.Small, Theme.T2, new Rectangle(tx, r.Y + Theme.S(190), tw, Theme.S(24)));
            }
            else if (ph == "between" || ph == "before")
            {
                Slot n = Plan.Slots[next];
                TimeSpan left = n.B.Start - now;
                double frac = 0;
                if (ph == "between")
                {
                    TimeSpan prevEnd = TimeSpan.Zero;
                    foreach (Slot sl in Plan.Slots) if (sl.B.End <= now && sl.B.End > prevEnd) prevEnd = sl.B.End;
                    double span = (n.B.Start - prevEnd).TotalSeconds;
                    frac = span > 0 ? (span - left.TotalSeconds) / span : 0;
                }
                Ring(g, rr, frac, Theme.AccText);
                CenterCount(g, rr, left);
                Theme.DrawPill(g, ph == "before" ? "SCHOOL STARTS SOON" : "PASSING TIME", Theme.SemiSmall, Theme.AccSoft, Theme.AccText, tx, r.Y + Theme.S(34), Theme.S(24));
                Theme.DrawText(g, "Up next", Theme.SemiSmall, Theme.T3, new Rectangle(tx, r.Y + Theme.S(68), tw, Theme.S(22)));
                Theme.DrawText(g, n.Title, Theme.Title, Theme.T1, new Rectangle(tx, r.Y + Theme.S(92), tw, Theme.S(40)));
                string sub = n.B.Text();
                if (n.R != null)
                {
                    sub = n.R.Teacher;
                    if (n.R.Room.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + n.R.Room;
                    sub += "  ·  starts " + Bell.Clock(n.B.Start);
                }
                else sub = "Starts " + Bell.Clock(n.B.Start);
                Theme.DrawText(g, sub, Theme.Body, Theme.T2, new Rectangle(tx, r.Y + Theme.S(136), tw, Theme.S(26)));
            }
            else if (ph == "after")
            {
                Ring(g, rr, 1.0, Theme.Good);
                Theme.DrawText(g, "✓", Theme.Huge, Theme.Good, rr, false, true);
                Theme.DrawPill(g, "DONE FOR TODAY", Theme.SemiSmall, Theme.AccSoft, Theme.AccText, tx, r.Y + Theme.S(34), Theme.S(24));
                Theme.DrawText(g, "School is out", Theme.Title, Theme.T1, new Rectangle(tx, r.Y + Theme.S(72), tw, Theme.S(40)));
                Theme.DrawWrapped(g, Detail, Theme.Body, Theme.T2, new Rectangle(tx, r.Y + Theme.S(120), tw, Theme.S(70)));
            }
            else
            {
                Ring(g, rr, 0, Theme.T3);
                Theme.DrawText(g, "–", Theme.Huge, Theme.T3, rr, false, true);
                Theme.DrawText(g, Headline.Length > 0 ? Headline : "Nothing scheduled", Theme.Title, Theme.T1, new Rectangle(tx, r.Y + Theme.S(60), tw, Theme.S(40)));
                Theme.DrawWrapped(g, Detail, Theme.Body, Theme.T2, new Rectangle(tx, r.Y + Theme.S(110), tw, Theme.S(90)));
            }
            if (DayText.Length > 0)
            {
                int w = Theme.PillWidth(DayText, Theme.SemiSmall) + Theme.S(8);
                Theme.DrawPill(g, DayText, Theme.SemiSmall, Theme.Bg4, Theme.T1, r.Right - pad - w + Theme.S(4), r.Y + Theme.S(18), Theme.S(24));
            }
        }

        // the number in the middle of the ring
        static void CenterCount(Graphics g, Rectangle rr, TimeSpan left)
        {
            string big, small;
            if (left.TotalMinutes >= 60)
            {
                int mins = (int)Math.Ceiling(left.TotalMinutes);
                big = (mins / 60) + ":" + (mins % 60).ToString("00");
                small = "hours left";
            }
            else if (left.TotalSeconds > 60)
            {
                big = ((int)Math.Ceiling(left.TotalMinutes)).ToString();
                small = "min left";
            }
            else
            {
                big = ((int)Math.Max(0, Math.Ceiling(left.TotalSeconds))).ToString();
                small = "sec left";
            }
            Theme.DrawText(g, big, Theme.Huge, Theme.T1, new Rectangle(rr.X, rr.Y + rr.Height / 2 - Theme.S(36), rr.Width, Theme.S(52)), false, true);
            Theme.DrawText(g, small, Theme.Small, Theme.T2, new Rectangle(rr.X, rr.Y + rr.Height / 2 + Theme.S(12), rr.Width, Theme.S(22)), false, true);
        }
    }

    // ------------------------------------------------------------------ today as a vertical timeline
    class NowTimeline : CardBase
    {
        public DayPlan Plan;
        public event Action<Slot> SlotClick;
        int hot = -1;

        static int RowH { get { return Theme.S(68); } }
        static int HeadH { get { return Theme.S(48); } }

        public int PreferredHeight()
        {
            int n = Plan == null ? 0 : Plan.Slots.Count;
            return HeadH + Math.Max(1, n) * RowH + Theme.S(10);
        }

        int RowAt(int y)
        {
            if (Plan == null) return -1;
            int i = (y - HeadH) / RowH;
            return (y >= HeadH && i >= 0 && i < Plan.Slots.Count) ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = RowAt(e.Y);
            if (i >= 0 && !Plan.Slots[i].IsClass) i = -1;
            if (i != hot) { hot = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = RowAt(e.Y);
            if (i >= 0 && Plan.Slots[i].IsClass && SlotClick != null) SlotClick(Plan.Slots[i]);
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(22);
            Theme.DrawText(g, "TODAY", Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, HeadH));
            if (Plan == null || Plan.Slots.Count == 0)
            {
                Theme.DrawText(g, "No bell schedule for today.", Theme.Body, Theme.T3, new Rectangle(r.X + pad, r.Y + HeadH, r.Width - pad * 2, RowH));
                return;
            }
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            Sched.Phase(Plan, now, out cur, out next);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int timeW = Theme.S(78);
            int lineX = r.X + pad + timeW + Theme.S(14);
            int y = r.Y + HeadH;
            for (int i = 0; i < Plan.Slots.Count; i++)
            {
                Slot s = Plan.Slots[i];
                bool done = s.B.End <= now;
                bool isNow = i == cur;
                if (i == hot) Theme.FillRound(g, new Rectangle(r.X + Theme.S(8), y + 1, r.Width - Theme.S(16), RowH - 2), Theme.S(10), Theme.Bg3);

                Color main = done ? Theme.T3 : Theme.T1;
                Theme.DrawText(g, Bell.ClockShort(s.B.Start), Theme.Semi, isNow ? Theme.AccText : (done ? Theme.T3 : Theme.T1),
                    new Rectangle(r.X + pad, y + Theme.S(12), timeW, Theme.S(22)), true, false);
                Theme.DrawText(g, Bell.ClockShort(s.B.End), Theme.Small, Theme.T3,
                    new Rectangle(r.X + pad, y + Theme.S(34), timeW, Theme.S(20)), true, false);

                // rail
                using (Pen rail = new Pen(done ? Theme.Line2 : Theme.Line, 2f))
                {
                    if (i > 0) g.DrawLine(rail, lineX, y, lineX, y + RowH / 2 - Theme.S(8));
                    if (i < Plan.Slots.Count - 1) g.DrawLine(rail, lineX, y + RowH / 2 + Theme.S(8), lineX, y + RowH);
                }
                int ds = isNow ? Theme.S(14) : Theme.S(10);
                Rectangle dot = new Rectangle(lineX - ds / 2, y + RowH / 2 - ds / 2, ds, ds);
                if (isNow)
                {
                    using (SolidBrush glow = new SolidBrush(Color.FromArgb(60, Theme.Acc))) { g.FillEllipse(glow, Rectangle.Inflate(dot, Theme.S(5), Theme.S(5))); }
                    using (SolidBrush b = new SolidBrush(Theme.Acc)) { g.FillEllipse(b, dot); }
                }
                else if (done)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Line2)) { g.FillEllipse(b, dot); }
                }
                else
                {
                    using (SolidBrush hole = new SolidBrush(Theme.Bg2)) { g.FillEllipse(hole, dot); }
                    using (Pen p = new Pen(Theme.AccText, 2f)) { g.DrawEllipse(p, dot); }
                }

                int x = lineX + Theme.S(22);
                int right = r.Right - pad;
                string chip = isNow ? "Now" : (i == next ? "Next" : (done ? "Done" : ""));
                if (chip.Length > 0)
                {
                    int pw = Theme.PillWidth(chip, Theme.SemiSmall);
                    Color back = isNow ? Theme.Acc : (i == next ? Theme.AccSoft : Theme.Bg4);
                    Color fore = isNow ? Color.White : (i == next ? Theme.AccText : Theme.T3);
                    Theme.DrawPill(g, chip, Theme.SemiSmall, back, fore, right - pw, y + (RowH - Theme.S(24)) / 2, Theme.S(24));
                    right -= pw + Theme.S(12);
                }
                if (s.IsClass)
                {
                    Color tag = Theme.Tag(s.R.Course);
                    Theme.FillRound(g, new Rectangle(x, y + Theme.S(14), Theme.S(4), RowH - Theme.S(28)), Theme.S(2), done ? Theme.Line2 : tag);
                    x += Theme.S(14);
                    string per = s.R.Period.Length > 0 ? "Period " + s.R.Period : s.B.Label;
                    string sub = per;
                    if (s.R.Teacher.Length > 0) sub += "  ·  " + s.R.Teacher;
                    if (s.R.Room.Length > 0) sub += "  ·  Room " + s.R.Room;
                    Theme.DrawText(g, s.Title, Theme.Semi, main, new Rectangle(x, y + Theme.S(11), Math.Max(10, right - x), Theme.S(24)));
                    Theme.DrawText(g, sub, Theme.Small, Theme.T3, new Rectangle(x, y + Theme.S(35), Math.Max(10, right - x), Theme.S(22)));
                }
                else
                {
                    Theme.DrawText(g, s.Title, Theme.Semi, done ? Theme.T3 : Theme.T2, new Rectangle(x, y, Math.Max(10, right - x), RowH));
                }
                y += RowH;
            }
        }
    }

    // ------------------------------------------------------------------ small "now" card for the Home page
    class NowStrip : CardBase
    {
        public DayPlan Plan;

        public NowStrip() { Hoverable = true; }

        public static int HeightFor() { return Theme.S(92); }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(22);
            TimeSpan now = DateTime.Now.TimeOfDay;
            int cur, next;
            string ph = Sched.Phase(Plan, now, out cur, out next);
            string kicker, title, right = "";
            double frac = -1;
            if (ph == "class")
            {
                Slot s = Plan.Slots[cur];
                kicker = "NOW";
                title = s.Title;
                right = Sched.Span(s.B.End - now) + " left";
                TimeSpan total = s.B.End - s.B.Start;
                frac = total.TotalSeconds > 0 ? (now - s.B.Start).TotalSeconds / total.TotalSeconds : 0;
            }
            else if (ph == "between" || ph == "before")
            {
                Slot n = Plan.Slots[next];
                kicker = ph == "before" ? "SCHOOL STARTS" : "UP NEXT";
                title = n.Title;
                right = "in " + Sched.Span(n.B.Start - now);
            }
            else if (ph == "after") { kicker = "TODAY"; title = "School is out"; }
            else { kicker = "TODAY"; title = Plan != null && !Plan.School ? "No school" : "Set up your bell schedule"; right = Plan != null && !Plan.School ? "" : "Open"; }
            Theme.DrawText(g, kicker, Theme.SemiSmall, Theme.AccText, new Rectangle(r.X + pad, r.Y + Theme.S(14), Theme.S(160), Theme.S(20)));
            Theme.DrawText(g, title, Theme.Mid, Theme.T1, new Rectangle(r.X + pad, r.Y + Theme.S(34), r.Width - pad * 2 - Theme.S(180), Theme.S(30)));
            if (right.Length > 0)
                Theme.DrawText(g, right, Theme.Semi, Theme.T1, new Rectangle(r.Right - pad - Theme.S(180), r.Y + Theme.S(34), Theme.S(180), Theme.S(30)), true, false);
            if (frac >= 0)
            {
                Rectangle bar = new Rectangle(r.X + pad, r.Y + r.Height - Theme.S(20), r.Width - pad * 2, Theme.S(6));
                Theme.FillRound(g, bar, Theme.S(3), Theme.Bg4);
                int fw = (int)(bar.Width * Math.Max(0, Math.Min(1, frac)));
                if (fw >= 6) Theme.FillRound(g, new Rectangle(bar.X, bar.Y, fw, bar.Height), Theme.S(3), Theme.Acc);
            }
        }
    }
}
