using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CampusMidnight
{
    // Everything below is custom-painted with GDI+ so the whole UI is dark and consistent without
    // relying on the light-themed built-in Windows controls.

    class ScrollPanel : Panel
    {
        public ScrollPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Theme.Bg0;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkScroll(Handle);
        }
    }

    abstract class CardBase : Control
    {
        protected bool Hover;
        public bool Hoverable;

        protected CardBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Theme.Bg2;
            ForeColor = Theme.T1;
        }

        protected virtual Color FillColor { get { return (Hover && Hoverable) ? Theme.Bg3 : Theme.Bg2; } }
        protected virtual Color BorderColor { get { return (Hover && Hoverable) ? Theme.Line2 : Theme.Line; } }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Hoverable) { Hover = true; Invalidate(); Cursor = Cursors.Hand; }
            ScrollPanel sp = Parent as ScrollPanel;
            Form f = FindForm();
            bool typing = f != null && f.ActiveControl is TextBox; // never steal focus from a text box
            if (sp != null && !sp.ContainsFocus && !typing) sp.Focus(); // so the mouse wheel scrolls the page
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (Hover) { Hover = false; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg0);
            if (Width < 4 || Height < 4) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(r, Theme.S(14)))
            using (SolidBrush b = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            PaintContent(g, new Rectangle(1, 1, Width - 2, Height - 2));
        }

        protected abstract void PaintContent(Graphics g, Rectangle r);
    }

    // ------------------------------------------------------------------ stat tile
    class TileCard : CardBase
    {
        public string Big = "", Label = "", RingText = "";
        public Color BigColor = Theme.T1;
        public double Ring = -1;

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(20);
            int x = r.X + pad;
            if (Ring >= 0)
            {
                int d = Theme.S(58);
                Rectangle rr = new Rectangle(x, r.Y + (r.Height - d) / 2, d, d);
                int pw = Theme.S(6);
                using (Pen track = new Pen(Theme.Bg4, pw))
                using (Pen arc = new Pen(Theme.Acc, pw))
                {
                    arc.StartCap = LineCap.Round;
                    arc.EndCap = LineCap.Round;
                    Rectangle ar = Rectangle.Inflate(rr, -pw / 2, -pw / 2);
                    g.DrawEllipse(track, ar);
                    float sweep = (float)(Math.Max(0, Math.Min(100, Ring)) * 3.6);
                    if (sweep > 1f) g.DrawArc(arc, ar, -90f, sweep);
                }
                Theme.DrawText(g, RingText, Theme.SemiSmall, Theme.T1, rr, false, true);
                x += d + Theme.S(16);
            }
            Theme.DrawText(g, Big, Theme.Big, BigColor, new Rectangle(x, r.Y + Theme.S(14), r.Right - x - pad, Theme.S(40)));
            Theme.DrawText(g, Label, Theme.Small, Theme.T2, new Rectangle(x, r.Y + Theme.S(54), r.Right - x - pad, Theme.S(24)));
        }
    }

    // ------------------------------------------------------------------ list card
    class Item
    {
        public string Title = "", Sub = "", Lead = "", Right = "", Badge = "";
        public Color RightColor = Theme.T2;
        public Color BadgeBack = Theme.Bg4, BadgeFore = Theme.T1;
        public bool Header;
    }

    class ListCard : CardBase
    {
        public string Heading = "";
        public string EmptyText = "Nothing here.";
        public List<Item> Items = new List<Item>();

        static int RowH { get { return Theme.S(56); } }
        static int HeaderRowH { get { return Theme.S(38); } }
        static int HeadH { get { return Theme.S(48); } }

        public int PreferredHeight()
        {
            int h = string.IsNullOrEmpty(Heading) ? Theme.S(10) : HeadH;
            if (Items.Count == 0) return h + Theme.S(52) + Theme.S(8);
            foreach (Item it in Items) h += it.Header ? HeaderRowH : RowH;
            return h + Theme.S(10);
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(20);
            int y = r.Y;
            if (!string.IsNullOrEmpty(Heading))
            {
                Theme.DrawText(g, Heading.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, y, r.Width - pad * 2, HeadH));
                y += HeadH;
            }
            else y += Theme.S(10);

            if (Items.Count == 0)
            {
                Theme.DrawText(g, EmptyText, Theme.Body, Theme.T3, new Rectangle(r.X + pad, y, r.Width - pad * 2, Theme.S(52)));
                return;
            }

            bool first = true;
            using (Pen line = new Pen(Theme.Line))
            {
                foreach (Item it in Items)
                {
                    if (it.Header)
                    {
                        Theme.DrawText(g, it.Title, Theme.SemiSmall, Theme.T2, new Rectangle(r.X + pad, y + Theme.S(6), r.Width - pad * 2, HeaderRowH - Theme.S(6)));
                        y += HeaderRowH;
                        first = true;
                        continue;
                    }
                    if (!first) g.DrawLine(line, r.X + pad, y, r.Right - pad, y);
                    first = false;

                    int x = r.X + pad;
                    int right = r.Right - pad;
                    int rowH = RowH;

                    if (it.Lead.Length > 0)
                    {
                        int ph = Theme.S(24);
                        int pw = Theme.DrawPill(g, it.Lead, Theme.SemiSmall, Theme.Bg4, Theme.T1, x, y + (rowH - ph) / 2, ph);
                        x += pw + Theme.S(14);
                    }
                    if (it.Badge.Length > 0)
                    {
                        int ph = Theme.S(24);
                        int pw = Theme.PillWidth(it.Badge, Theme.SemiSmall);
                        Theme.DrawPill(g, it.Badge, Theme.SemiSmall, it.BadgeBack, it.BadgeFore, right - pw, y + (rowH - ph) / 2, ph);
                        right -= pw + Theme.S(14);
                    }
                    if (it.Right.Length > 0)
                    {
                        int tw = Theme.Measure(it.Right, Theme.Body).Width + Theme.S(4);
                        Theme.DrawText(g, it.Right, Theme.Body, it.RightColor, new Rectangle(right - tw, y, tw, rowH), true, false);
                        right -= tw + Theme.S(14);
                    }
                    int w = Math.Max(Theme.S(40), right - x);
                    if (it.Sub.Length > 0)
                    {
                        Theme.DrawText(g, it.Title, Theme.Semi, Theme.T1, new Rectangle(x, y + Theme.S(8), w, Theme.S(22)));
                        Theme.DrawText(g, it.Sub, Theme.Small, Theme.T2, new Rectangle(x, y + Theme.S(30), w, Theme.S(20)));
                    }
                    else
                    {
                        Theme.DrawText(g, it.Title, Theme.Semi, Theme.T1, new Rectangle(x, y, w, rowH));
                    }
                    y += rowH;
                }
            }
        }
    }

    // ------------------------------------------------------------------ class card (grades)
    class CourseCard : CardBase
    {
        public Course Data;

        public CourseCard()
        {
            Hoverable = true;
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (Data == null) return;
            int pad = Theme.S(20);
            string letter = string.IsNullOrEmpty(Data.Letter) ? "–" : Data.Letter;
            Color gc = Theme.GradeColor(Data.Percent);
            int lw = Theme.S(84);
            Theme.DrawText(g, letter, Theme.Huge, gc, new Rectangle(r.Right - pad - lw, r.Y + Theme.S(12), lw, Theme.S(48)), true, false);
            int tw = r.Width - pad * 2 - lw - Theme.S(8);
            Theme.DrawText(g, Data.Name, Theme.Semi, Theme.T1, new Rectangle(r.X + pad, r.Y + Theme.S(16), tw, Theme.S(24)));
            string meta = Data.Teacher;
            if (Data.Room.Length > 0) meta += (meta.Length > 0 ? "  ·  " : "") + "Room " + Data.Room;
            Theme.DrawText(g, meta, Theme.Small, Theme.T2, new Rectangle(r.X + pad, r.Y + Theme.S(42), tw, Theme.S(20)));

            int by = r.Y + Theme.S(78);
            Rectangle track = new Rectangle(r.X + pad, by, r.Width - pad * 2, Theme.S(6));
            Theme.FillRound(g, track, Theme.S(3), Theme.Bg4);
            if (Data.Percent.HasValue)
            {
                double p = Math.Max(2, Math.Min(100, Data.Percent.Value));
                int fw = (int)(track.Width * p / 100.0);
                if (fw >= 6) Theme.FillRound(g, new Rectangle(track.X, track.Y, fw, track.Height), Theme.S(3), gc);
            }
            Theme.DrawText(g, Data.TaskName.Length > 0 ? Data.TaskName : "Current", Theme.Small, Theme.T2,
                new Rectangle(r.X + pad, by + Theme.S(12), tw, Theme.S(20)));
            Theme.DrawText(g, Data.Percent.HasValue ? Theme.Fmt(Data.Percent) + "%" : "No grade yet", Theme.Small, Theme.T2,
                new Rectangle(r.Right - pad - Theme.S(120), by + Theme.S(12), Theme.S(120), Theme.S(20)), true, false);
        }
    }

    // ------------------------------------------------------------------ message card
    class NoticeCard : CardBase
    {
        public string Text2 = "";

        protected override Color FillColor { get { return Theme.AccDeep; } }
        protected override Color BorderColor { get { return Theme.AccSoft; } }

        public int PreferredHeight(int width)
        {
            return Theme.WrapHeight(Text2, Theme.Body, width - Theme.S(44)) + Theme.S(36);
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            Theme.DrawWrapped(g, Text2, Theme.Body, Theme.T1, new Rectangle(r.X + Theme.S(22), r.Y + Theme.S(18), r.Width - Theme.S(44), r.Height - Theme.S(20)));
        }
    }

    // ------------------------------------------------------------------ segmented control
    class Segmented : Control
    {
        public string[] Options = new string[0];
        public int Selected;
        public event EventHandler Changed;

        public Segmented()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Height = Theme.S(40);
        }

        public void SetOptions(string[] opts, int selected)
        {
            Options = opts;
            Selected = selected;
            int w = Theme.S(8);
            foreach (string o in opts) w += Theme.Measure(o, Theme.Semi).Width + Theme.S(30);
            Width = w;
            Invalidate();
        }

        List<Rectangle> Slots()
        {
            List<Rectangle> list = new List<Rectangle>();
            int x = Theme.S(4);
            foreach (string o in Options)
            {
                int w = Theme.Measure(o, Theme.Semi).Width + Theme.S(30);
                list.Add(new Rectangle(x, Theme.S(4), w, Height - Theme.S(8)));
                x += w;
            }
            return list;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg0);
            Rectangle all = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(all, Theme.S(12)))
            using (SolidBrush b = new SolidBrush(Theme.Bg2))
            using (Pen pen = new Pen(Theme.Line))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            List<Rectangle> slots = Slots();
            for (int i = 0; i < slots.Count; i++)
            {
                bool on = i == Selected;
                if (on) Theme.FillRound(g, slots[i], Theme.S(9), Theme.Bg5);
                Theme.DrawText(g, Options[i], Theme.Semi, on ? Theme.T1 : Theme.T2, slots[i], false, true);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            List<Rectangle> slots = Slots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Contains(e.Location))
                {
                    if (i != Selected)
                    {
                        Selected = i;
                        Invalidate();
                        if (Changed != null) Changed(this, EventArgs.Empty);
                    }
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ left rail button
    class NavButton : Control
    {
        public string Glyph = "", Caption = "";
        public bool On;
        bool hover;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg1);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (On) Theme.FillRound(g, r, Theme.S(14), Theme.AccSoft);
            else if (hover) Theme.FillRound(g, r, Theme.S(14), Theme.Bg3);
            Color c = On ? Theme.AccText : (hover ? Theme.T1 : Theme.T2);
            if (Theme.HasIcons)
            {
                Theme.DrawText(g, Glyph, Theme.IconFont, c, new Rectangle(0, Theme.S(6), Width, Theme.S(30)), false, true);
                Theme.DrawText(g, Caption, Theme.Small, c, new Rectangle(0, Height - Theme.S(26), Width, Theme.S(20)), false, true);
            }
            else
            {
                Theme.DrawText(g, Caption, Theme.Semi, c, new Rectangle(0, 0, Width, Height), false, true);
            }
        }
    }

    // ------------------------------------------------------------------ button
    class PillButton : Control
    {
        public bool Primary;
        bool hover, down;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.S(38);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg0);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, fore;
            if (!Enabled) { fill = Theme.Bg3; fore = Theme.T3; }
            else if (Primary) { fill = down ? Theme.AccDown : (hover ? Theme.AccHover : Theme.Acc); fore = Color.White; }
            else { fill = down ? Theme.Bg3 : (hover ? Theme.Bg5 : Theme.Bg4); fore = Theme.T1; }
            using (GraphicsPath p = Theme.Round(r, Theme.S(10)))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
                if (Focused && Enabled)
                {
                    using (Pen fp = new Pen(Theme.AccText, 2f)) { g.DrawPath(fp, p); }
                }
            }
            Theme.DrawText(g, Text, Theme.Semi, fore, r, false, true);
        }
    }

    // ------------------------------------------------------------------ text box
    class DarkInput : Control
    {
        public readonly TextBox Box = new TextBox();
        readonly bool multi;
        string cue;

        public string Placeholder
        {
            get { return cue; }
            set
            {
                cue = value;
                if (Box.IsHandleCreated && !string.IsNullOrEmpty(cue)) Native.SetCue(Box, cue);
            }
        }

        public DarkInput(bool multiline, bool password)
        {
            multi = multiline;
            Box.HandleCreated += delegate { if (!string.IsNullOrEmpty(cue)) Native.SetCue(Box, cue); };
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Bg3;
            Box.ForeColor = Theme.T1;
            Box.Font = Theme.Body;
            Box.Multiline = multiline;
            Box.UseSystemPasswordChar = password;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
            Height = multiline ? Theme.S(92) : Theme.S(44);
        }

        public override string Text
        {
            get { return Box.Text; }
            set { Box.Text = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int m = Theme.S(12);
            if (multi) Box.SetBounds(m, Theme.S(9), Math.Max(10, Width - m * 2), Math.Max(10, Height - Theme.S(18)));
            else
            {
                int bh = Box.PreferredHeight;
                Box.SetBounds(m, (Height - bh) / 2, Math.Max(10, Width - m * 2), bh);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Box.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg2);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(r, Theme.S(10)))
            using (SolidBrush b = new SolidBrush(Theme.Bg3))
            using (Pen pen = new Pen(Box.Focused ? Theme.Acc : Theme.Line2, Box.Focused ? 2f : 1f))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
        }
    }

    // ------------------------------------------------------------------ check box
    class DarkCheck : Control
    {
        public bool Checked;
        public event EventHandler Changed;

        public DarkCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Height = Theme.S(28);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg2);
            int s = Theme.S(20);
            Rectangle box = new Rectangle(0, (Height - s) / 2, s, s);
            if (Checked)
            {
                Theme.FillRound(g, box, Theme.S(6), Theme.Acc);
                using (Pen pen = new Pen(Color.White, 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new Point[]
                    {
                        new Point(box.X + s * 25 / 100, box.Y + s * 52 / 100),
                        new Point(box.X + s * 43 / 100, box.Y + s * 70 / 100),
                        new Point(box.X + s * 76 / 100, box.Y + s * 32 / 100)
                    });
                }
            }
            else
            {
                using (GraphicsPath p = Theme.Round(new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1), Theme.S(6)))
                using (SolidBrush b = new SolidBrush(Theme.Bg3))
                using (Pen pen = new Pen(Theme.Line2))
                {
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }
            }
            Theme.DrawText(g, Text, Theme.Body, Theme.T2, new Rectangle(s + Theme.S(10), 0, Width - s - Theme.S(10), Height));
        }
    }

    // ------------------------------------------------------------------ sign-in card
    class LoginCard : CardBase
    {
        protected override void PaintContent(Graphics g, Rectangle r) { }
    }
}
