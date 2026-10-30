using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CampusMidnight
{
    // Shared drawing helpers for pictures.
    static class Draw
    {
        // a picture cropped to fill a circle (or initials on an accent disc when there is no picture)
        public static void Avatar(Graphics g, Bitmap photo, string name, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (photo != null)
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(r);
                    GraphicsState st = g.Save();
                    g.SetClip(p, CombineMode.Intersect);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Max((float)r.Width / photo.Width, (float)r.Height / photo.Height);
                    int dw = (int)Math.Ceiling(photo.Width * scale), dh = (int)Math.Ceiling(photo.Height * scale);
                    g.DrawImage(photo, new Rectangle(r.X + (r.Width - dw) / 2, r.Y + (r.Height - dh) / 2, dw, dh));
                    g.Restore(st);
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Theme.AccSoft)) { g.FillEllipse(b, r); }
                Font f = r.Width >= Theme.S(60) ? Theme.Big : Theme.Semi;
                Theme.DrawText(g, Theme.Initials(name), f, Theme.AccText, r, false, true);
            }
            using (Pen pen = new Pen(Theme.Line2, 1.5f)) { g.DrawEllipse(pen, r); }
        }

        // ---- logos --------------------------------------------------------------------------------
        static bool IsBackground(byte a, byte r, byte g, byte b)
        {
            return a < 20 || (r >= 244 && g >= 244 && b >= 244);
        }

        // cuts away transparent or white margins so the logo fills its tile
        public static Bitmap Trim(Bitmap src)
        {
            try
            {
                int w = src.Width, h = src.Height;
                Rectangle all = new Rectangle(0, 0, w, h);
                byte[] buf;
                int stride;
                using (Bitmap copy = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics cg = Graphics.FromImage(copy)) { cg.DrawImage(src, 0, 0, w, h); }
                    System.Drawing.Imaging.BitmapData bd = copy.LockBits(all, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    stride = bd.Stride;
                    buf = new byte[stride * h];
                    System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                    copy.UnlockBits(bd);
                }
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;
                        if (!IsBackground(buf[i + 3], buf[i + 2], buf[i + 1], buf[i]))
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
                if (maxX < 0) return src;
                int pad = 2;
                minX = Math.Max(0, minX - pad); minY = Math.Max(0, minY - pad);
                maxX = Math.Min(w - 1, maxX + pad); maxY = Math.Min(h - 1, maxY + pad);
                Rectangle crop = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                if (crop.Width < 8 || crop.Height < 8 || (crop.Width == w && crop.Height == h)) return src;
                Bitmap result = new Bitmap(crop.Width, crop.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics rg = Graphics.FromImage(result))
                {
                    rg.DrawImage(src, new Rectangle(0, 0, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
                }
                src.Dispose();
                return result;
            }
            catch (Exception) { return src; }
        }

        static readonly Dictionary<Bitmap, bool> lightCache = new Dictionary<Bitmap, bool>();

        // true when the logo's own colours are mostly white/pale, so it needs a dark tile
        static bool IsPale(Bitmap b)
        {
            lock (lightCache)
            {
                bool v;
                if (lightCache.TryGetValue(b, out v)) return v;
                double sum = 0; int n = 0;
                try
                {
                    for (int yy = 0; yy < 24; yy++)
                    {
                        for (int xx = 0; xx < 24; xx++)
                        {
                            Color c = b.GetPixel(xx * (b.Width - 1) / 23, yy * (b.Height - 1) / 23);
                            if (IsBackground(c.A, c.R, c.G, c.B)) continue;
                            sum += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                            n++;
                        }
                    }
                }
                catch (Exception) { }
                v = n > 8 && sum / n > 200;
                if (lightCache.Count > 8) lightCache.Clear();
                lightCache[b] = v;
                return v;
            }
        }

        public static Color TileColor(Bitmap logo)
        {
            if (logo == null) return Theme.AccSoft;
            return IsPale(logo) ? Color.FromArgb(34, 38, 50) : Color.FromArgb(244, 246, 250);
        }

        static void FitInto(Graphics g, Bitmap logo, Rectangle inner)
        {
            float scale = Math.Min((float)inner.Width / logo.Width, (float)inner.Height / logo.Height);
            int dw = Math.Max(1, (int)(logo.Width * scale)), dh = Math.Max(1, (int)(logo.Height * scale));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(logo, new Rectangle(inner.X + (inner.Width - dw) / 2, inner.Y + (inner.Height - dh) / 2, dw, dh));
        }

        // the school logo in a tile (square, used in the sidebar)
        public static void Logo(Graphics g, Bitmap logo, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (logo != null)
            {
                Theme.FillRound(g, r, Theme.S(12), TileColor(logo));
                FitInto(g, logo, Rectangle.Inflate(r, -Theme.S(6), -Theme.S(6)));
                Theme.StrokeRound(g, r, Theme.S(12), Theme.Line2, 1f);
            }
            else
            {
                Theme.FillRound(g, r, Theme.S(12), Theme.AccSoft);
                Theme.DrawText(g, "C", Theme.Big, Theme.AccText, r, false, true);
            }
        }

        // a tile as wide as the logo needs (up to maxW), right-aligned at "right"; returns the width used
        public static int LogoRight(Graphics g, Bitmap logo, int right, int y, int h, int maxW)
        {
            if (logo == null)
            {
                Logo(g, null, new Rectangle(right - h, y, h, h));
                return h;
            }
            int pad = Theme.S(8);
            double aspect = (double)logo.Width / logo.Height;
            aspect = Math.Max(1.0, Math.Min(3.4, aspect));
            int w = (int)((h - pad * 2) * aspect) + pad * 2;
            w = Math.Max(h, Math.Min(maxW, w));
            Rectangle r = new Rectangle(right - w, y, w, h);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRound(g, r, Theme.S(12), TileColor(logo));
            FitInto(g, logo, Rectangle.Inflate(r, -pad, -pad));
            Theme.StrokeRound(g, r, Theme.S(12), Theme.Line2, 1f);
            return w;
        }

        // a window/taskbar icon built from the logo (null if the logo is too wide to read at icon size)
        public static Icon MakeIcon(Bitmap logo)
        {
            if (logo == null || (double)logo.Width / logo.Height > 1.9) return null;
            try
            {
                using (Bitmap bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath p = Theme.Round(new Rectangle(1, 1, 62, 62), 14))
                    using (SolidBrush b = new SolidBrush(TileColor(logo)))
                    {
                        g.FillPath(b, p);
                    }
                    FitInto(g, logo, new Rectangle(7, 7, 50, 50));
                    return Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch (Exception) { return null; }
        }
    }

    // ------------------------------------------------------------------ small dark popup menu
    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Bg3; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Bg3; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Bg3; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Bg3; } }
        public override Color MenuBorder { get { return Theme.Line2; } }
        public override Color MenuItemBorder { get { return Theme.Bg5; } }
        public override Color MenuItemSelected { get { return Theme.Bg5; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Bg5; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Bg5; } }
        public override Color SeparatorDark { get { return Theme.Line2; } }
    }

    static class DarkMenu
    {
        public class Entry
        {
            public string Text = "", Glyph = "";
            public bool Checked, Separator, Header;
            public Action Run;
            public static Entry Sep() { Entry e = new Entry(); e.Separator = true; return e; }
            public static Entry Head(string t) { Entry e = new Entry(); e.Header = true; e.Text = t; return e; }
            public static Entry Item(string text, string glyph, bool chk, Action run) { Entry e = new Entry(); e.Text = text; e.Glyph = glyph; e.Checked = chk; e.Run = run; return e; }
        }

        // a drop-down menu with icons, headings and separators, opening under (and right-aligned to) a button
        public static void Popup(Control anchor, List<Entry> entries)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.ShowImageMargin = true;
            m.BackColor = Theme.Bg3;
            m.ForeColor = Theme.T1;
            m.Font = Theme.Body;
            m.Padding = new Padding(Theme.S(4));
            foreach (Entry e in entries)
            {
                if (e.Separator) { ToolStripSeparator sp = new ToolStripSeparator(); sp.BackColor = Theme.Bg3; sp.ForeColor = Theme.Line2; m.Items.Add(sp); continue; }
                if (e.Header)
                {
                    ToolStripLabel hl = new ToolStripLabel(e.Text.ToUpperInvariant());
                    hl.ForeColor = Theme.T3; hl.BackColor = Theme.Bg3; hl.Font = Theme.SemiSmall;
                    m.Items.Add(hl);
                    continue;
                }
                ToolStripMenuItem it = new ToolStripMenuItem(e.Text);
                it.ForeColor = e.Checked ? Theme.AccText : Theme.T1;
                it.BackColor = Theme.Bg3;
                it.Padding = new Padding(Theme.S(2), Theme.S(5), Theme.S(2), Theme.S(5));
                if (e.Glyph.Length > 0)
                {
                    int sz = Theme.S(18);
                    Bitmap bmp = new Bitmap(sz, sz);
                    using (Graphics g = Graphics.FromImage(bmp)) { Icons.Draw(g, e.Glyph, new Rectangle(0, 0, sz, sz), e.Checked ? Theme.AccText : Theme.T2); }
                    it.Image = bmp;
                }
                if (e.Checked) it.Font = Theme.Semi;
                Action run = e.Run;
                it.Click += delegate { if (run != null) run(); };
                m.Items.Add(it);
            }
            m.Closed += delegate
            {
                m.BeginInvoke(new MethodInvoker(delegate
                {
                    foreach (ToolStripItem ti in m.Items) if (ti.Image != null) ti.Image.Dispose();
                    m.Dispose();
                }));
            };
            m.Show(anchor, new Point(anchor.Width, anchor.Height + Theme.S(6)), ToolStripDropDownDirection.BelowLeft);
        }

        public static void Pick(Control anchor, string[] options, int selected, Action<int> chosen)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.ShowImageMargin = false;
            m.BackColor = Theme.Bg3;
            m.ForeColor = Theme.T1;
            m.Font = Theme.Body;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem it = new ToolStripMenuItem((i == selected ? "✓  " : "     ") + options[i]);
                it.ForeColor = Theme.T1;
                it.BackColor = Theme.Bg3;
                it.Click += delegate { chosen(idx); };
                m.Items.Add(it);
            }
            m.Closed += delegate { m.BeginInvoke(new MethodInvoker(delegate { m.Dispose(); })); };
            m.Show(anchor, new Point(0, anchor.Height + Theme.S(4)));
        }
    }

    // ------------------------------------------------------------------ a card that holds other controls
    class PlainCard : CardBase
    {
        public string Heading = "";
        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (Heading.Length > 0)
                Theme.DrawText(g, Heading.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, new Rectangle(r.X + Theme.S(20), r.Y, r.Width - Theme.S(40), Theme.S(48)));
        }
    }

    // ------------------------------------------------------------------ plain text in a card
    class TextCard : CardBase
    {
        public string Heading = "", Body = "";
        public Font BodyFont = Theme.Body;
        public Color BodyColor = Theme.T1;

        int HeadH { get { return Heading.Length > 0 ? Theme.S(44) : Theme.S(14); } }

        public int PreferredHeight(int width)
        {
            return HeadH + Theme.WrapHeight(Body, BodyFont, width - Theme.S(44)) + Theme.S(22);
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(22);
            if (Heading.Length > 0)
                Theme.DrawText(g, Heading.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, HeadH));
            Theme.DrawWrapped(g, Body, BodyFont, BodyColor, new Rectangle(r.X + pad, r.Y + HeadH, r.Width - pad * 2, r.Height - HeadH - Theme.S(8)));
        }
    }

    // ------------------------------------------------------------------ profile
    class ProfileCard : CardBase
    {
        public Student S;
        public Bitmap Photo, Logo;
        Rectangle idChip = Rectangle.Empty;
        bool copied;
        System.Windows.Forms.Timer reset;

        public static int HeightFor() { return Theme.S(144); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = (!idChip.IsEmpty && idChip.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (S == null || idChip.IsEmpty || !idChip.Contains(e.Location) || S.Number.Length == 0) return;
            try { Clipboard.SetText(S.Number); } catch (Exception) { return; }
            copied = true;
            Invalidate();
            if (reset == null)
            {
                reset = new System.Windows.Forms.Timer();
                reset.Interval = 1600;
                reset.Tick += delegate { reset.Stop(); copied = false; Invalidate(); };
            }
            reset.Stop();
            reset.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && reset != null) reset.Dispose();
            base.Dispose(disposing);
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (S == null) return;
            int pad = Theme.S(26);
            int d = Theme.S(92);
            Rectangle av = new Rectangle(r.X + pad, r.Y + (r.Height - d) / 2, d, d);
            Draw.Avatar(g, Photo, S.Name, av);

            int x = av.Right + Theme.S(22);
            int rightEdge = r.Right - pad;
            if (r.Width > Theme.S(560))
            {
                int ls = Theme.S(60);
                int lwid = Draw.LogoRight(g, Logo, rightEdge, r.Y + Theme.S(22), ls, Theme.S(210));
                rightEdge -= lwid + Theme.S(16);
            }
            Theme.DrawText(g, S.Name, Theme.Title, Theme.T1, new Rectangle(x, r.Y + Theme.S(20), rightEdge - x, Theme.S(40)));
            string line = S.Line();
            Theme.DrawText(g, line.Length > 0 ? line : "Student", Theme.Body, Theme.T2, new Rectangle(x, r.Y + Theme.S(60), rightEdge - x, Theme.S(24)));

            // chips
            idChip = Rectangle.Empty;
            int cy = r.Y + Theme.S(94), ch = Theme.S(28), cx = x;
            if (S.Number.Length > 0)
            {
                string t = copied ? "Copied" : "ID  " + S.Number;
                int w = Theme.PillWidth(t, Theme.SemiSmall) + Theme.S(4);
                idChip = new Rectangle(cx, cy, w, ch);
                Theme.DrawPill(g, t, Theme.SemiSmall, copied ? Theme.AccSoft : Theme.Bg4, copied ? Theme.AccText : Theme.T1, cx, cy, ch);
                cx += w + Theme.S(8);
            }
            if (S.StateId.Length > 0)
                cx += Theme.DrawPill(g, "State ID  " + S.StateId, Theme.SemiSmall, Theme.Bg4, Theme.T1, cx, cy, ch) + Theme.S(8);
            if (S.Year.Length > 0 && cx + Theme.S(120) < rightEdge)
                cx += Theme.DrawPill(g, S.Year, Theme.SemiSmall, Theme.Bg4, Theme.T2, cx, cy, ch) + Theme.S(8);
            if (S.ClassOf.Length > 0 && cx + Theme.S(120) < rightEdge)
                Theme.DrawPill(g, S.ClassOf, Theme.SemiSmall, Theme.Bg4, Theme.T2, cx, cy, ch);
        }
    }

    // ------------------------------------------------------------------ bar chart
    class Bar
    {
        public string Label = "";    // "Mon|28" draws two lines
        public double Value;
        public Color Fill = Theme.Acc;
        public string Text = "";     // shown above the bar (defaults to the value)
    }

    class BarChart : CardBase
    {
        public string Heading = "";
        public string EmptyText = "";
        public List<Bar> Bars = new List<Bar>();

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(20);
            Theme.DrawText(g, Heading.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, Theme.S(48)));
            double max = 0;
            foreach (Bar b in Bars) if (b.Value > max) max = b.Value;
            if (Bars.Count == 0 || max <= 0)
            {
                Theme.DrawText(g, EmptyText.Length > 0 ? EmptyText : "Nothing to show yet.", Theme.Body, Theme.T3,
                    new Rectangle(r.X + pad, r.Y + Theme.S(48), r.Width - pad * 2, Theme.S(40)));
                return;
            }
            int labelH = Theme.S(38), topH = Theme.S(48), valH = Theme.S(18);
            Rectangle area = new Rectangle(r.X + pad, r.Y + topH + valH, r.Width - pad * 2, r.Height - topH - valH - labelH - Theme.S(8));
            int n = Bars.Count;
            float slot = (float)area.Width / n;
            int bw = Math.Max(Theme.S(6), Math.Min(Theme.S(44), (int)(slot * 0.62f)));
            using (Pen grid = new Pen(Theme.Line))
            {
                g.DrawLine(grid, area.X, area.Bottom, area.Right, area.Bottom);
            }
            for (int i = 0; i < n; i++)
            {
                Bar b = Bars[i];
                int cx = area.X + (int)(slot * i + slot / 2);
                int bh = b.Value <= 0 ? Theme.S(3) : Math.Max(Theme.S(4), (int)(area.Height * (b.Value / max)));
                Rectangle br = new Rectangle(cx - bw / 2, area.Bottom - bh, bw, bh);
                Theme.FillRound(g, br, Math.Min(Theme.S(6), bw / 2), b.Value <= 0 ? Theme.Bg4 : b.Fill);
                string vt = b.Text.Length > 0 ? b.Text : (b.Value > 0 ? Theme.Fmt(b.Value) : "");
                if (vt.Length > 0)
                    Theme.DrawText(g, vt, Theme.SemiSmall, Theme.T2, new Rectangle(cx - (int)slot / 2, br.Y - valH, (int)slot, valH), false, true);
                string[] parts = b.Label.Split('|');
                Theme.DrawText(g, parts[0], Theme.Small, Theme.T3, new Rectangle(cx - (int)slot / 2, area.Bottom + Theme.S(4), (int)slot, Theme.S(16)), false, true);
                if (parts.Length > 1)
                    Theme.DrawText(g, parts[1], Theme.SemiSmall, Theme.T2, new Rectangle(cx - (int)slot / 2, area.Bottom + Theme.S(19), (int)slot, Theme.S(16)), false, true);
            }
        }
    }

    // ------------------------------------------------------------------ line chart
    class LineChart : CardBase
    {
        public string Heading = "";
        public string EmptyText = "Not enough graded work yet.";
        public List<DateTime> Xs = new List<DateTime>();
        public List<double> Ys = new List<double>();

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(20);
            Theme.DrawText(g, Heading.ToUpperInvariant(), Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, Theme.S(48)));
            if (Ys.Count < 2)
            {
                Theme.DrawText(g, EmptyText, Theme.Body, Theme.T3, new Rectangle(r.X + pad, r.Y + Theme.S(48), r.Width - pad * 2, Theme.S(40)));
                return;
            }
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (double v in Ys) { if (v < lo) lo = v; if (v > hi) hi = v; }
            double ymin = Math.Max(0, Math.Floor((lo - 4) / 10.0) * 10);
            double ymax = Math.Max(100, Math.Ceiling(hi / 10.0) * 10);
            if (ymax - ymin < 20) ymin = ymax - 20;
            int left = Theme.S(38);
            Rectangle area = new Rectangle(r.X + pad + left, r.Y + Theme.S(54), r.Width - pad * 2 - left, r.Height - Theme.S(54) - Theme.S(40));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int steps = 4;
            using (Pen grid = new Pen(Theme.Line))
            {
                for (int i = 0; i <= steps; i++)
                {
                    int gy = area.Y + (int)(area.Height * i / (double)steps);
                    g.DrawLine(grid, area.X, gy, area.Right, gy);
                    double val = ymax - (ymax - ymin) * i / steps;
                    Theme.DrawText(g, Math.Round(val) + "%", Theme.Small, Theme.T3, new Rectangle(r.X + pad - Theme.S(4), gy - Theme.S(10), left, Theme.S(20)), true, false);
                }
            }
            int n = Ys.Count;
            PointF[] pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                float px = area.X + (n == 1 ? 0 : area.Width * i / (float)(n - 1));
                float py = (float)(area.Bottom - area.Height * (Ys[i] - ymin) / (ymax - ymin));
                pts[i] = new PointF(px, Math.Max(area.Y, Math.Min(area.Bottom, py)));
            }
            PointF[] fill = new PointF[n + 2];
            Array.Copy(pts, fill, n);
            fill[n] = new PointF(pts[n - 1].X, area.Bottom);
            fill[n + 1] = new PointF(pts[0].X, area.Bottom);
            using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(area.X, area.Y, area.Width, Math.Max(2, area.Height)),
                Color.FromArgb(70, Theme.Acc), Color.FromArgb(0, Theme.Acc), 90f))
            {
                g.FillPolygon(lg, fill);
            }
            using (Pen line = new Pen(Theme.Acc, 2.4f))
            {
                line.LineJoin = LineJoin.Round;
                g.DrawLines(line, pts);
            }
            int dot = Theme.S(4);
            using (SolidBrush db = new SolidBrush(Theme.Acc))
            using (SolidBrush hole = new SolidBrush(Theme.Bg2))
            {
                for (int i = 0; i < n; i++)
                {
                    if (n > 28 && i != n - 1 && i != 0) continue;
                    g.FillEllipse(hole, pts[i].X - dot - 1, pts[i].Y - dot - 1, dot * 2 + 2, dot * 2 + 2);
                    g.FillEllipse(db, pts[i].X - dot + 1, pts[i].Y - dot + 1, dot * 2 - 2, dot * 2 - 2);
                }
            }
            Theme.DrawText(g, Xs[0].ToString("MMM d"), Theme.Small, Theme.T3, new Rectangle(area.X, area.Bottom + Theme.S(8), Theme.S(100), Theme.S(20)));
            Theme.DrawText(g, Xs[n - 1].ToString("MMM d"), Theme.Small, Theme.T3, new Rectangle(area.Right - Theme.S(100), area.Bottom + Theme.S(8), Theme.S(100), Theme.S(20)), true, false);
            Theme.DrawText(g, "Latest " + Math.Round(Ys[n - 1]) + "%", Theme.SemiSmall, Theme.AccText,
                new Rectangle(r.Right - pad - Theme.S(160), r.Y, Theme.S(160), Theme.S(48)), true, false);
        }
    }

    // ------------------------------------------------------------------ a class period
    class PeriodCard : CardBase
    {
        public RosterItem R;
        public Course C;
        public string TimeText = "";
        public string Badge = "";   // "Now" / "Next"
        public bool Dropped;
        public string EndText = "";

        public PeriodCard() { Hoverable = true; }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (R == null) return;
            int pad = Theme.S(18);
            Color tag = Theme.Tag(R.Course);
            Theme.FillRound(g, new Rectangle(r.X + pad - Theme.S(4), r.Y + Theme.S(16), Theme.S(5), r.Height - Theme.S(32)), Theme.S(3), tag);
            int x = r.X + pad + Theme.S(16);
            int right = r.Right - pad;

            string per = R.Period.Length == 0 ? "" : (Regex.IsMatch(R.Period, @"^\d+$") ? "PERIOD " + R.Period : R.Period.ToUpperInvariant());
            int lw = Theme.Measure(per, Theme.SemiSmall).Width;
            Theme.DrawText(g, per, Theme.SemiSmall, Theme.T3, new Rectangle(x, r.Y + Theme.S(12), lw + 4, Theme.S(24)));
            int bx = x + lw + Theme.S(10);
            if (Dropped)
            {
                bx += Theme.DrawPill(g, "Dropped", Theme.SemiSmall, Theme.BadSoft, Theme.Bad, bx, r.Y + Theme.S(14), Theme.S(20)) + Theme.S(8);
            }
            else if (Badge.Length > 0)
            {
                bool now = Badge == "Now";
                bx += Theme.DrawPill(g, Badge, Theme.SemiSmall, now ? Theme.Acc : Theme.AccSoft, now ? Color.White : Theme.AccText, bx, r.Y + Theme.S(14), Theme.S(20)) + Theme.S(8);
            }
            if (TimeText.Length > 0)
                Theme.DrawText(g, TimeText, Theme.SemiSmall, Theme.T2, new Rectangle(bx, r.Y + Theme.S(12), right - bx, Theme.S(24)), true, false);

            Theme.DrawText(g, R.Course, Theme.Mid, Dropped ? Theme.T3 : Theme.T1, new Rectangle(x, r.Y + Theme.S(38), right - x, Theme.S(30)));

            string sub = R.Teacher;
            if (R.Room.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + R.Room;
            int gw = 0;
            if (C != null && (C.Percent.HasValue || C.Letter.Length > 0))
            {
                string gt = Theme.NumbersOnly ? (C.Percent.HasValue ? Math.Round(C.Percent.Value) + "%" : "") : (C.Letter.Length > 0 ? C.Letter : "") + (C.Percent.HasValue ? (C.Letter.Length > 0 ? "  " : "") + Math.Round(C.Percent.Value) + "%" : "");
                gw = Theme.PillWidth(gt, Theme.SemiSmall);
                Theme.DrawPill(g, gt, Theme.SemiSmall, Theme.Bg4, Theme.GradeColor(C.Percent), right - gw, r.Y + r.Height - Theme.S(40), Theme.S(24));
            }
            if (Dropped && EndText.Length > 0) sub = EndText + (sub.Length > 0 ? "  ·  " + sub : "");
            Theme.DrawText(g, sub, Theme.Small, Dropped ? Theme.T3 : Theme.T2, new Rectangle(x, r.Y + r.Height - Theme.S(42), right - x - gw - Theme.S(10), Theme.S(28)));
        }
    }

    // ------------------------------------------------------------------ theme choice
    class SwatchCard : CardBase
    {
        public ThemeDef Def;
        public bool Selected;
        Color bg, surface, accent, text;
        bool ready;

        public SwatchCard() { Hoverable = true; }

        protected override Color BorderColor { get { return Selected ? Theme.Acc : base.BorderColor; } }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            if (Def == null) return;
            if (!ready) { Theme.Preview(Def, out bg, out surface, out accent, out text); ready = true; }
            int pad = Theme.S(12);
            Rectangle pv = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - Theme.S(52));
            Theme.FillRound(g, pv, Theme.S(9), bg);
            Rectangle card = new Rectangle(pv.X + Theme.S(8), pv.Y + Theme.S(8), pv.Width - Theme.S(16), pv.Height - Theme.S(16));
            Theme.FillRound(g, card, Theme.S(6), surface);
            Theme.FillRound(g, new Rectangle(card.X + Theme.S(8), card.Y + Theme.S(8), Theme.S(34), Theme.S(5)), Theme.S(2), text);
            Theme.FillRound(g, new Rectangle(card.X + Theme.S(8), card.Y + Theme.S(18), Theme.S(54), Theme.S(4)), Theme.S(2), Theme.Mix(text, surface, 0.6));
            Theme.FillRound(g, new Rectangle(card.Right - Theme.S(36), card.Bottom - Theme.S(18), Theme.S(28), Theme.S(10)), Theme.S(5), accent);
            Theme.StrokeRound(g, pv, Theme.S(9), Theme.Line2, 1f);
            Theme.DrawText(g, Def.Name, Theme.Semi, Selected ? Theme.AccText : Theme.T1,
                new Rectangle(r.X + pad + Theme.S(2), r.Bottom - Theme.S(36), r.Width - pad * 2 - Theme.S(24), Theme.S(28)));
            if (Selected)
            {
                int s = Theme.S(18);
                Rectangle cr = new Rectangle(r.Right - pad - s, r.Bottom - Theme.S(32), s, s);
                Theme.FillRound(g, cr, s / 2, Theme.Acc);
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new Point[]
                    {
                        new Point(cr.X + s * 27 / 100, cr.Y + s * 52 / 100),
                        new Point(cr.X + s * 44 / 100, cr.Y + s * 68 / 100),
                        new Point(cr.X + s * 74 / 100, cr.Y + s * 33 / 100)
                    });
                }
            }
        }
    }

    // ------------------------------------------------------------------ "what if" grade calculator
    class WhatIfCard : CardBase
    {
        public List<Course> Courses = new List<Course>();
        public List<Assign> Assigns = new List<Assign>();
        int sel;
        readonly DarkInput earned, possible, goal;
        readonly PillButton pick;

        public static int HeightFor() { return Theme.S(326); }

        public WhatIfCard(double goalPct)
        {
            pick = new PillButton();
            pick.Glyph = "";
            pick.Click += delegate
            {
                if (Courses.Count == 0) return;
                string[] names = new string[Courses.Count];
                for (int i = 0; i < names.Length; i++) names[i] = Courses[i].Name;
                DarkMenu.Pick(pick, names, sel, delegate(int i) { sel = i; UpdateLabel(); Invalidate(); });
            };
            earned = new DarkInput(false, false);
            earned.Placeholder = "e.g. 18";
            possible = new DarkInput(false, false);
            possible.Placeholder = "e.g. 20";
            goal = new DarkInput(false, false);
            goal.Text = Theme.Fmt(goalPct);
            earned.Box.TextChanged += delegate { Invalidate(); };
            possible.Box.TextChanged += delegate { Invalidate(); };
            goal.Box.TextChanged += delegate { Invalidate(); };
            Controls.Add(pick);
            Controls.Add(earned);
            Controls.Add(possible);
            Controls.Add(goal);
        }

        public double GoalValue
        {
            get { double? v = ParseNum(goal.Text); return v.HasValue ? v.Value : 90; }
        }

        public void Ready()
        {
            UpdateLabel();
            Relayout();
        }

        void UpdateLabel()
        {
            pick.Text = Courses.Count == 0 ? "No classes" : Courses[Math.Min(sel, Courses.Count - 1)].Name;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        void Relayout()
        {
            int pad = Theme.S(20), gap = Theme.S(14);
            int w = Width - pad * 2;
            pick.SetBounds(pad, Theme.S(50), w, Theme.S(40));
            int cw = (w - gap * 2) / 3;
            int y = Theme.S(128);
            earned.SetBounds(pad, y, cw, Theme.S(44));
            possible.SetBounds(pad + cw + gap, y, cw, Theme.S(44));
            goal.SetBounds(pad + (cw + gap) * 2, y, cw, Theme.S(44));
        }

        static double? ParseNum(string t)
        {
            double v;
            if (t == null) return null;
            if (double.TryParse(t.Trim().Replace(',', '.').TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        void Totals(Course c, out double e, out double t)
        {
            e = 0; t = 0;
            string key = Norm.Key(c.Name);
            foreach (Assign a in Assigns)
            {
                if (a.Status != "graded" || !a.Score.HasValue || !a.Total.HasValue || a.Total.Value <= 0) continue;
                string ak = Norm.Key(a.Course);
                if (ak.Length == 0 || !(ak == key || ak.Contains(key) || key.Contains(ak))) continue;
                e += a.Score.Value; t += a.Total.Value;
            }
        }

        protected override void PaintContent(Graphics g, Rectangle r)
        {
            int pad = Theme.S(20);
            Theme.DrawText(g, "WHAT-IF CALCULATOR", Theme.SemiSmall, Theme.T3, new Rectangle(r.X + pad, r.Y, r.Width - pad * 2, Theme.S(48)));
            int cw = (r.Width - pad * 2 - Theme.S(14) * 2) / 3;
            Theme.DrawText(g, "SCORE YOU GET", Theme.SemiSmall, Theme.T3, new Rectangle(pad, Theme.S(102), cw, Theme.S(22)));
            Theme.DrawText(g, "OUT OF", Theme.SemiSmall, Theme.T3, new Rectangle(pad + cw + Theme.S(14), Theme.S(102), cw, Theme.S(22)));
            Theme.DrawText(g, "YOUR GOAL %", Theme.SemiSmall, Theme.T3, new Rectangle(pad + (cw + Theme.S(14)) * 2, Theme.S(102), cw, Theme.S(22)));
            if (Courses.Count == 0) return;
            Course c = Courses[Math.Min(sel, Courses.Count - 1)];
            double e, t;
            Totals(c, out e, out t);
            int y = Theme.S(190);
            int w = r.Width - pad * 2;
            string now = "Campus shows " + (c.Letter.Length > 0 ? c.Letter + "  " : "") + (c.Percent.HasValue ? Theme.Fmt(c.Percent) + "%" : "no grade yet");
            if (t > 0) now += "   ·   graded points so far: " + Theme.Fmt(e) + " / " + Theme.Fmt(t) + "  (" + Theme.Fmt(e / t * 100) + "%)";
            Theme.DrawText(g, now, Theme.Small, Theme.T2, new Rectangle(pad, y, w, Theme.S(22)));
            y += Theme.S(30);

            double? x = ParseNum(earned.Text), p = ParseNum(possible.Text), goalV = ParseNum(goal.Text);
            if (x.HasValue && p.HasValue && p.Value > 0)
            {
                double np = (e + x.Value) / (t + p.Value) * 100;
                double d = t > 0 ? np - e / t * 100 : 0;
                string line = "With " + Theme.Fmt(x) + " / " + Theme.Fmt(p) + " your points-based grade becomes " + Theme.Fmt(np) + "%";
                if (t > 0) line += "  (" + (d >= 0 ? "+" : "") + Theme.Fmt(d) + ")";
                Theme.DrawText(g, line, Theme.Semi, Theme.GradeColor(np), new Rectangle(pad, y, w, Theme.S(26)));
            }
            else
            {
                Theme.DrawText(g, "Type a score and what it is out of to see what happens to this class.", Theme.Body, Theme.T3, new Rectangle(pad, y, w, Theme.S(26)));
            }
            y += Theme.S(34);
            if (goalV.HasValue && p.HasValue && p.Value > 0 && t > 0)
            {
                double need = goalV.Value / 100.0 * (t + p.Value) - e;
                string line;
                Color col = Theme.T1;
                if (need <= 0) { line = "You would stay at or above " + Theme.Fmt(goalV) + "% even with a zero on this one."; col = Theme.Good; }
                else if (need > p.Value) { line = "To reach " + Theme.Fmt(goalV) + "% you would need " + Theme.Fmt(need) + " points, more than this " + Theme.Fmt(p) + "-point assignment offers."; col = Theme.Warn; }
                else { line = "To reach " + Theme.Fmt(goalV) + "% you need " + Theme.Fmt(need) + " of " + Theme.Fmt(p) + "  (" + Theme.Fmt(need / p.Value * 100) + "%)."; }
                Theme.DrawWrapped(g, line, Theme.Body, col, new Rectangle(pad, y, w, Theme.S(48)));
            }
            Theme.DrawWrapped(g, "An estimate from raw points. Your teacher's category weights can make Campus' number differ.", Theme.Small, Theme.T3,
                new Rectangle(pad, r.Bottom - Theme.S(44), w, Theme.S(36)));
        }
    }
}
