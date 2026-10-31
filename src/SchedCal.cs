using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CampusMidnight
{
    // month calendar: each day shows its letter; days off are dimmed and days on a saved schedule get a dot
    class MonthGrid : Control
    {
        public DateTime Month = DateTime.Today;
        public DateTime Selected = DateTime.Today;
        public Dictionary<DateTime, string> Letters = new Dictionary<DateTime, string>();
        public Dictionary<DateTime, string> Kinds = new Dictionary<DateTime, string>();   // "off", "alt" or ""
        public Action<DateTime> DayClick;
        public Action<int> MonthStep;
        DateTime hot = DateTime.MinValue;
        int hotNav;

        public MonthGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        static int HeadH { get { return Theme.S(54); } }
        static int WeekH { get { return Theme.S(26); } }
        static int RowH { get { return Theme.S(60); } }
        public static int PreferredHeight() { return HeadH + WeekH + RowH * 6 + Theme.S(14); }

        DateTime Start() { DateTime f = new DateTime(Month.Year, Month.Month, 1); return f.AddDays(-(int)f.DayOfWeek); }
        Rectangle PrevRect() { return new Rectangle(Width - Theme.S(100), Theme.S(8), Theme.S(40), Theme.S(38)); }
        Rectangle NextRect() { return new Rectangle(Width - Theme.S(54), Theme.S(8), Theme.S(40), Theme.S(38)); }

        DateTime DayAt(Point p)
        {
            int pad = Theme.S(10);
            if (p.Y < HeadH + WeekH || p.X < pad || p.X > Width - pad) return DateTime.MinValue;
            int cw = (Width - pad * 2) / 7;
            int col = (p.X - pad) / Math.Max(1, cw);
            int row = (p.Y - HeadH - WeekH) / RowH;
            if (col < 0 || col > 6 || row < 0 || row > 5) return DateTime.MinValue;
            return Start().AddDays(row * 7 + col);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            DateTime d = DayAt(e.Location);
            int nav = PrevRect().Contains(e.Location) ? 1 : (NextRect().Contains(e.Location) ? 2 : 0);
            if (d != hot || nav != hotNav) { hot = d; hotNav = nav; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hot = DateTime.MinValue; hotNav = 0; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (PrevRect().Contains(e.Location)) { if (MonthStep != null) MonthStep(-1); }
            else if (NextRect().Contains(e.Location)) { if (MonthStep != null) MonthStep(1); }
            else
            {
                DateTime d = DayAt(e.Location);
                if (d != DateTime.MinValue && DayClick != null) DayClick(d);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg0);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle all = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, all, Theme.S(14), Theme.Bg2);
            Theme.StrokeRound(g, all, Theme.S(14), Theme.Line, 1f);
            Theme.DrawText(g, Month.ToString("MMMM yyyy"), Theme.Mid, Theme.T1, new Rectangle(Theme.S(20), 0, Width / 2, HeadH));
            Theme.DrawText(g, "‹", Theme.Title, hotNav == 1 ? Theme.AccText : Theme.T2, PrevRect(), false, true);
            Theme.DrawText(g, "›", Theme.Title, hotNav == 2 ? Theme.AccText : Theme.T2, NextRect(), false, true);
            int pad = Theme.S(10);
            int cw = (Width - pad * 2) / 7;
            string[] wk = new string[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
            for (int i = 0; i < 7; i++)
                Theme.DrawText(g, wk[i], Theme.SemiSmall, Theme.T3, new Rectangle(pad + i * cw, HeadH, cw, WeekH), false, true);
            DateTime st = Start();
            for (int i = 0; i < 42; i++)
            {
                DateTime d = st.AddDays(i);
                int col = i % 7, row = i / 7;
                Rectangle cell = new Rectangle(pad + col * cw + 2, HeadH + WeekH + row * RowH + 2, cw - 4, RowH - 4);
                bool inMonth = d.Month == Month.Month;
                string kind, letter;
                Kinds.TryGetValue(d, out kind); Letters.TryGetValue(d, out letter);
                kind = kind ?? ""; letter = letter ?? "";
                bool sel = d == Selected.Date;
                if (sel) { Theme.FillRound(g, cell, Theme.S(10), Theme.AccSoft); Theme.StrokeRound(g, cell, Theme.S(10), Theme.Acc, 1.5f); }
                else if (hot == d) Theme.FillRound(g, cell, Theme.S(10), Theme.Bg3);
                Color num = !inMonth ? Theme.T3 : (kind == "off" ? Theme.T3 : Theme.T2);
                if (d == DateTime.Today)
                {
                    Rectangle ring = new Rectangle(cell.X + Theme.S(6), cell.Y + Theme.S(4), Theme.S(24), Theme.S(22));
                    Theme.FillRound(g, ring, Theme.S(11), Theme.Acc);
                    Theme.DrawText(g, d.Day.ToString(), Theme.SemiSmall, Color.White, ring, false, true);
                }
                else Theme.DrawText(g, d.Day.ToString(), Theme.SemiSmall, num, new Rectangle(cell.X + Theme.S(6), cell.Y + Theme.S(4), Theme.S(28), Theme.S(22)));
                Rectangle mid = new Rectangle(cell.X, cell.Y + Theme.S(18), cell.Width, cell.Height - Theme.S(20));
                if (kind == "off") Theme.DrawText(g, "–", Theme.Body, Theme.T3, mid, false, true);
                else if (letter.Length > 0) Theme.DrawText(g, letter, Theme.Semi, inMonth ? Theme.AccText : Theme.T3, mid, false, true);
                if (kind == "alt")
                    using (SolidBrush b = new SolidBrush(Theme.Warn)) g.FillEllipse(b, cell.Right - Theme.S(14), cell.Y + Theme.S(8), Theme.S(8), Theme.S(8));
            }
        }
    }

    // make or change a saved schedule: its own start and end for each row of the grid
    class VariantForm : DarkForm
    {
        public string NameText = "";
        public List<string> Times = new List<string>();
        readonly DarkInput iName;
        readonly List<DarkInput> iA = new List<DarkInput>(), iB = new List<DarkInput>();
        readonly string original;
        readonly ICollection<string> taken;

        public VariantForm(string existingName, List<string> starts, List<string> ends, List<string> current, ICollection<string> takenNames)
            : base(existingName == null ? "New saved schedule" : "Edit " + existingName)
        {
            original = existingName;
            taken = takenNames;
            Lbl("NAME", Theme.SemiSmall, Theme.T3, Theme.S(22));
            iName = new DarkInput(false, false);
            iName.Text = existingName ?? "Half day";
            Add(iName, Theme.S(44));
            Gap(14);
            Wrapped("Set the start and end for each row. Leave both empty to skip a row on this schedule.", Theme.Body, Theme.T2);
            Gap(6);
            int half = (Fw - Theme.S(12)) / 2;
            for (int i = 0; i < starts.Count; i++)
            {
                TimeSpan a0, b0;
                string reg = (Bell.TryTime(starts[i], out a0) ? Bell.Clock(a0) : starts[i]) + " - " + (Bell.TryTime(ends[i], out b0) ? Bell.Clock(b0) : ends[i]);
                Lbl("ROW " + (i + 1) + "   (regular " + reg + ")", Theme.SemiSmall, Theme.T3, Theme.S(22));
                DarkInput ia = new DarkInput(false, false), ib = new DarkInput(false, false);
                string startVal = "", endVal = "";
                if (current != null && i < current.Count)
                {
                    string[] pr = current[i].Split('-');
                    TimeSpan t1, t2;
                    if (pr.Length == 2 && Bell.TryTime(pr[0], out t1) && Bell.TryTime(pr[1], out t2)) { startVal = Bell.Clock(t1); endVal = Bell.Clock(t2); }
                }
                else { startVal = a0 == TimeSpan.Zero ? starts[i] : Bell.Clock(a0); endVal = Bell.Clock(b0); }
                ia.Text = startVal; ib.Text = endVal;
                ia.SetBounds(Fx, Fy, half, Theme.S(44));
                ib.SetBounds(Fx + half + Theme.S(12), Fy, half, Theme.S(44));
                Page.Controls.Add(ia); Page.Controls.Add(ib);
                iA.Add(ia); iB.Add(ib);
                Fy += Theme.S(54);
            }
            Gap(10);
            PillButton ok = new PillButton(); ok.Text = "Save"; ok.Kind = 1;
            ok.Click += delegate { Done(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
            Finish();
        }

        void Done()
        {
            string n = iName.Text.Trim();
            bool bad = n.Length == 0 || n.IndexOfAny(new char[] { '|', ';', '~', '=', '#', '^', ':' }) >= 0
                || string.Equals(n, "regular", StringComparison.OrdinalIgnoreCase) || string.Equals(n, "none", StringComparison.OrdinalIgnoreCase)
                || (n != original && taken.Contains(n));
            iName.Invalid = bad; iName.Invalidate();
            List<string> t = new List<string>();
            bool any = false;
            for (int i = 0; i < iA.Count; i++)
            {
                string sa = iA[i].Text.Trim(), sb = iB[i].Text.Trim();
                if (sa.Length == 0 && sb.Length == 0) { t.Add(""); iA[i].Invalid = false; iB[i].Invalid = false; }
                else
                {
                    TimeSpan a, b;
                    bool okA = Bell.TryTime(sa, out a), okB = Bell.TryTime(sb, out b);
                    bool okRow = okA && okB && b > a;
                    iA[i].Invalid = !okRow; iB[i].Invalid = !okRow;
                    if (!okRow) { bad = true; t.Add(""); }
                    else { t.Add(a.ToString(@"hh\:mm") + "-" + b.ToString(@"hh\:mm")); any = true; }
                }
                iA[i].Invalidate(); iB[i].Invalidate();
            }
            if (bad || !any) return;
            NameText = n; Times = t;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    partial class MainForm
    {
        DateTime schedSel = DateTime.MinValue, schedMonth = DateTime.MinValue;

        static string ShortDay(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            int sp = label.IndexOf(' ');
            string w = sp > 0 ? label.Substring(0, sp) : label;
            return w.Length > 3 ? w.Substring(0, 3) : w;
        }

        string DayTypeText(DateTime d)
        {
            string t;
            if (!cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out t)) return "Automatic";
            if (t == "none") return "No school";
            return t;
        }

        void SetDayType(DateTime d, string type)
        {
            string key = d.ToString("yyyy-MM-dd");
            if (type == null) cfg.DayTypes.Remove(key); else cfg.DayTypes[key] = type;
            cfg.Save();
            Redo();
        }

        void EditVariant(string old)
        {
            if (cfg.GridStarts.Count == 0) { StartScheduleEdit(); return; }
            List<string> ex = null;
            if (old != null) cfg.Variants.TryGetValue(old, out ex);
            using (VariantForm f = new VariantForm(old, cfg.GridStarts, cfg.GridEnds, ex, cfg.Variants.Keys))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                if (old != null && old != f.NameText)
                {
                    cfg.Variants.Remove(old);
                    List<string> keys = new List<string>(cfg.DayTypes.Keys);
                    foreach (string k in keys) if (cfg.DayTypes[k] == old) cfg.DayTypes[k] = f.NameText;
                }
                cfg.Variants[f.NameText] = f.Times;
                cfg.Save();
                Redo();
            }
        }

        void DeleteVariant(string name)
        {
            cfg.Variants.Remove(name);
            List<string> keys = new List<string>(cfg.DayTypes.Keys);
            foreach (string k in keys) if (cfg.DayTypes[k] == name) cfg.DayTypes.Remove(k);
            cfg.Save();
            Redo();
        }

        void BuildScheduleCalendar()
        {
            if (schedSel == DateTime.MinValue) { schedSel = DateTime.Today; schedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); }
            MonthGrid mg = new MonthGrid();
            mg.Month = schedMonth;
            mg.Selected = schedSel;
            DateTime first = new DateTime(schedMonth.Year, schedMonth.Month, 1);
            DateTime st = first.AddDays(-(int)first.DayOfWeek);
            for (int i = 0; i < 42; i++)
            {
                DateTime d = st.AddDays(i);
                string from;
                bool school = Sched.IsSchoolDay(cfg, snap, d);
                if (!school) { mg.Kinds[d] = "off"; continue; }
                mg.Letters[d] = ShortDay(Sched.LabelFor(cfg, snap, d, out from));
                string dt;
                if (cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out dt) && dt != "Regular") mg.Kinds[d] = "alt";
            }
            mg.DayClick = delegate(DateTime d)
            {
                schedSel = d;
                if (d.Month != schedMonth.Month || d.Year != schedMonth.Year) schedMonth = new DateTime(d.Year, d.Month, 1);
                Redo();
            };
            mg.MonthStep = delegate(int step) { schedMonth = schedMonth.AddMonths(step); Redo(); };
            int h = MonthGrid.PreferredHeight();
            AddCard(mg, X0(), vy, CW(), h);
            vy += h + Theme.S(18);

            // the picked day
            DayPlan dp = Sched.Plan(cfg, snap, term, schedSel);
            SectionTitle(schedSel.ToString("dddd, MMMM d") + (dp.DayLabel.Length > 0 ? "  ·  " + Sched.DayName(dp.DayLabel) : ""));
            DateTime sel = schedSel;
            PillButton dd = AddButton("Schedule: " + DayTypeText(sel), "", 0, X0(), vy, null);
            PillButton ddSelf = dd;
            dd.Click += delegate
            {
                string cur = DayTypeText(sel);
                List<DarkMenu.Entry> es = new List<DarkMenu.Entry>();
                es.Add(DarkMenu.Entry.Head("This day"));
                es.Add(DarkMenu.Entry.Item("Automatic", "", cur == "Automatic", delegate { SetDayType(sel, null); }));
                es.Add(DarkMenu.Entry.Item("No school", "", cur == "No school", delegate { SetDayType(sel, "none"); }));
                es.Add(DarkMenu.Entry.Item("Regular times", "", cur == "Regular", delegate { SetDayType(sel, "Regular"); }));
                foreach (string vn in new List<string>(cfg.Variants.Keys))
                {
                    string name = vn;
                    es.Add(DarkMenu.Entry.Item(name, "", cur == name, delegate { SetDayType(sel, name); }));
                }
                es.Add(DarkMenu.Entry.Sep());
                es.Add(DarkMenu.Entry.Head("Saved schedules"));
                es.Add(DarkMenu.Entry.Item("New saved schedule…", "", false, delegate { EditVariant(null); }));
                foreach (string vn in new List<string>(cfg.Variants.Keys))
                {
                    string name = vn;
                    es.Add(DarkMenu.Entry.Item("Edit " + name + "…", "", false, delegate { EditVariant(name); }));
                    es.Add(DarkMenu.Entry.Item("Delete " + name, "", false, delegate { DeleteVariant(name); }));
                }
                DarkMenu.Popup(ddSelf, es);
            };
            AddButton("Change times and days", "", 0, X0() + dd.Width + Theme.S(10), vy, delegate { StartScheduleEdit(); });
            vy += Theme.S(54);

            ListCard lc = new ListCard();
            lc.Heading = "Classes that day";
            lc.EmptyText = dp.School ? "No schedule set up for this day yet." : (dp.WhyNot.Length > 0 ? dp.WhyNot : "No school.");
            lc.Clickable = true;
            foreach (Slot s in dp.Slots)
            {
                Item it = new Item();
                it.Lead = s.B.Label.Length <= 3 ? s.B.Label : "";
                it.Title = s.Title;
                it.Sub = s.B.Text();
                it.Tag = s;
                if (s.IsClass) it.Strip = Theme.Tag(s.Title);
                lc.Items.Add(it);
            }
            lc.ItemClick += delegate(Item it) { Slot sl = it.Tag as Slot; if (sl != null && sl.C != null) OpenCourse(sl.C); };
            AddCard(lc, X0(), vy, CW(), lc.PreferredHeight());
            vy += lc.PreferredHeight() + Theme.S(26);
        }
    }
}
