using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CampusMidnight
{
    // Hand-drawn line icons (24 x 24 grid). They do not depend on any installed icon font, so they look the same everywhere.
    // The old Segoe MDL2 code points are still what the rest of the app passes in; this maps them to a drawing.
    static class Icons
    {
        static readonly HashSet<string> known = new HashSet<string>(new string[]
        {
            "", "", "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", ""
        });

        public static bool Has(string glyph) { return glyph != null && known.Contains(glyph); }

        struct Ctx
        {
            public float S, Ox, Oy;
            public PointF P(float x, float y) { return new PointF(Ox + x * S, Oy + y * S); }
            public RectangleF R(float x, float y, float w, float h) { return new RectangleF(Ox + x * S, Oy + y * S, w * S, h * S); }
        }

        static void Line(Graphics g, Pen p, Ctx c, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = c.P(xy[i * 2], xy[i * 2 + 1]);
            if (pts.Length == 2) g.DrawLine(p, pts[0], pts[1]);
            else g.DrawLines(p, pts);
        }

        static void Poly(Graphics g, Pen p, Ctx c, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = c.P(xy[i * 2], xy[i * 2 + 1]);
            g.DrawPolygon(p, pts);
        }

        static void Box(Graphics g, Pen p, Ctx c, float x, float y, float w, float h, float rad)
        {
            RectangleF r = c.R(x, y, w, h);
            float d = Math.Max(0.1f, rad * 2 * c.S);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddArc(r.X, r.Y, d, d, 180, 90);
                gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();
                g.DrawPath(p, gp);
            }
        }

        static void Dot(Graphics g, Brush b, Ctx c, float x, float y, float r)
        {
            RectangleF q = c.R(x - r, y - r, r * 2, r * 2);
            g.FillEllipse(b, q);
        }

        // draws the icon inside r; returns false when the glyph has no drawing
        public static bool Draw(Graphics g, string glyph, Rectangle r, Color color)
        {
            if (!Has(glyph)) return false;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Ctx c = new Ctx();
            float side = Math.Min(r.Width, r.Height);
            c.S = side / 24f;
            c.Ox = r.X + (r.Width - side) / 2f;
            c.Oy = r.Y + (r.Height - side) / 2f;
            using (Pen p = new Pen(color, Math.Max(1.3f, 1.75f * c.S)))
            using (Pen thick = new Pen(color, Math.Max(2f, 3.2f * c.S)))
            using (SolidBrush b = new SolidBrush(color))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                thick.StartCap = LineCap.Round; thick.EndCap = LineCap.Round; thick.LineJoin = LineJoin.Round;
                switch (glyph)
                {
                    case "": // home
                        Line(g, p, c, 3, 11, 12, 3, 21, 11);
                        Line(g, p, c, 5.5f, 9.5f, 5.5f, 20, 18.5f, 20, 18.5f, 9.5f);
                        Line(g, p, c, 10, 20, 10, 14, 14, 14, 14, 20);
                        break;
                    case "": // clock
                        g.DrawEllipse(p, c.R(3, 3, 18, 18));
                        Line(g, p, c, 12, 7, 12, 12, 16, 14.5f);
                        break;
                    case "": // bars
                        Line(g, thick, c, 5.5f, 20, 5.5f, 13);
                        Line(g, thick, c, 12, 20, 12, 5);
                        Line(g, thick, c, 18.5f, 20, 18.5f, 9);
                        break;
                    case "": // checklist
                        Box(g, p, c, 4, 3, 16, 18, 3);
                        Line(g, p, c, 8, 9, 10, 11, 14, 7);
                        Line(g, p, c, 8, 15.5f, 16, 15.5f);
                        break;
                    case "": // calendar
                        Box(g, p, c, 3, 5, 18, 16, 3);
                        Line(g, p, c, 3, 10, 21, 10);
                        Line(g, p, c, 8, 3, 8, 7);
                        Line(g, p, c, 16, 3, 16, 7);
                        Dot(g, b, c, 8, 14, 1.2f); Dot(g, b, c, 12, 14, 1.2f); Dot(g, b, c, 16, 14, 1.2f);
                        break;
                    case "": // envelope
                        Box(g, p, c, 3, 5, 18, 14, 3);
                        Line(g, p, c, 3.5f, 7.5f, 12, 13.5f, 20.5f, 7.5f);
                        break;
                    case "": // document
                        Poly(g, p, c, 6, 3, 14, 3, 19, 8, 19, 21, 6, 21);
                        Line(g, p, c, 14, 3, 14, 8, 19, 8);
                        Line(g, p, c, 9, 13, 16, 13);
                        Line(g, p, c, 9, 17, 16, 17);
                        break;
                    case "": // sliders
                        Line(g, p, c, 4, 7, 20, 7); Line(g, p, c, 4, 12, 20, 12); Line(g, p, c, 4, 17, 20, 17);
                        Dot(g, b, c, 9, 7, 2.4f); Dot(g, b, c, 15, 12, 2.4f); Dot(g, b, c, 8, 17, 2.4f);
                        break;
                    case "": // gear
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                            Line(g, thick, c, 12 + cs * 7.6f, 12 + sn * 7.6f, 12 + cs * 9.6f, 12 + sn * 9.6f);
                        }
                        g.DrawEllipse(p, c.R(5.8f, 5.8f, 12.4f, 12.4f));
                        g.DrawEllipse(p, c.R(9.3f, 9.3f, 5.4f, 5.4f));
                        break;
                    case "": // refresh
                    {
                        RectangleF rc = c.R(4.5f, 4.5f, 15, 15);
                        g.DrawArc(p, rc, -50, 270);
                        double ang = 220 * Math.PI / 180;
                        float ex = 12 + 7.5f * (float)Math.Cos(ang), ey = 12 + 7.5f * (float)Math.Sin(ang);
                        float dx = (float)(-Math.Sin(ang)), dy = (float)Math.Cos(ang);
                        float nx = -dy, ny = dx;
                        PointF[] head = new PointF[] { c.P(ex + dx * 3.4f, ey + dy * 3.4f), c.P(ex + nx * 3f, ey + ny * 3f), c.P(ex - nx * 3f, ey - ny * 3f) };
                        g.FillPolygon(b, head);
                        break;
                    }
                    case "": // open externally
                        Line(g, p, c, 10, 5, 5, 5, 5, 19, 19, 19, 19, 14);
                        Line(g, p, c, 14, 4, 20, 4, 20, 10);
                        Line(g, p, c, 20, 4, 11, 13);
                        break;
                    case "": // sign out
                        Line(g, p, c, 10, 4, 4, 4, 4, 20, 10, 20);
                        Line(g, p, c, 9, 12, 20, 12);
                        Line(g, p, c, 16, 8, 20, 12, 16, 16);
                        break;
                    case "": // moon
                        using (GraphicsPath moon = new GraphicsPath())
                        using (GraphicsPath cut = new GraphicsPath())
                        {
                            moon.AddEllipse(c.R(4, 3.5f, 16.5f, 16.5f));
                            cut.AddEllipse(c.R(9, 0.5f, 15, 15));
                            using (Region reg = new Region(moon))
                            {
                                reg.Exclude(cut);
                                g.FillRegion(b, reg);
                            }
                        }
                        break;
                    case "": // sun
                        g.DrawEllipse(p, c.R(7.5f, 7.5f, 9, 9));
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                            Line(g, p, c, 12 + cs * 7.2f, 12 + sn * 7.2f, 12 + cs * 9.8f, 12 + sn * 9.8f);
                        }
                        break;
                    case "": // menu
                        Line(g, p, c, 4, 6.5f, 20, 6.5f); Line(g, p, c, 4, 12, 20, 12); Line(g, p, c, 4, 17.5f, 20, 17.5f);
                        break;
                    case "": // more
                        Dot(g, b, c, 5, 12, 1.9f); Dot(g, b, c, 12, 12, 1.9f); Dot(g, b, c, 19, 12, 1.9f);
                        break;
                    case "": Line(g, p, c, 9, 5, 16, 12, 9, 19); break;
                    case "": Line(g, p, c, 5, 9, 12, 16, 19, 9); break;
                    case "": Line(g, p, c, 5, 15, 12, 8, 19, 15); break;
                    case "": // picture
                        Box(g, p, c, 3, 5, 18, 14, 3);
                        Dot(g, b, c, 8.5f, 10, 1.8f);
                        Line(g, p, c, 3.5f, 17.5f, 9, 13, 13, 16.5f, 16, 14, 20.5f, 17.5f);
                        break;
                    case "": Line(g, thick, c, 5, 12.5f, 10, 17.5f, 19, 7); break;
                    case "": Line(g, p, c, 2, 12, 7, 12, 10, 5, 14, 19, 17, 12, 22, 12); break;
                    case "": // copy
                        Box(g, p, c, 4, 3.5f, 11, 12.5f, 2.5f);
                        Box(g, p, c, 9, 8.5f, 11, 12.5f, 2.5f);
                        break;
                    case "": // folder
                        Poly(g, p, c, 3, 6.5f, 9, 6.5f, 11.5f, 9.5f, 21, 9.5f, 21, 19, 3, 19);
                        break;
                    case "": // trash
                        Line(g, p, c, 4, 7, 20, 7);
                        Line(g, p, c, 9, 7, 9, 4, 15, 4, 15, 7);
                        Line(g, p, c, 6, 7, 7, 20, 17, 20, 18, 7);
                        Line(g, p, c, 10.5f, 11, 10.5f, 16); Line(g, p, c, 13.5f, 11, 13.5f, 16);
                        break;
                    case "": Line(g, p, c, 12, 5, 12, 19); Line(g, p, c, 5, 12, 19, 12); break;
                    case "": // pencil
                        Poly(g, p, c, 4, 20, 5, 15, 16, 4, 20, 8, 9, 19);
                        Line(g, p, c, 13.5f, 6.5f, 17.5f, 10.5f);
                        break;
                    case "": // import
                        Line(g, p, c, 12, 4, 12, 15);
                        Line(g, p, c, 7, 10.5f, 12, 15.5f, 17, 10.5f);
                        Line(g, p, c, 4, 15, 4, 20, 20, 20, 20, 15);
                        break;
                    case "": // sparkle
                        Poly(g, p, c, 12, 3, 14, 10, 21, 12, 14, 14, 12, 21, 10, 14, 3, 12, 10, 10);
                        break;
                    case "": // bell
                        Line(g, p, c, 5, 17, 6.5f, 15, 6.5f, 10);
                        g.DrawArc(p, c.R(6.5f, 4, 11, 11), 180, 180);
                        Line(g, p, c, 17.5f, 10, 17.5f, 15, 19, 17, 5, 17);
                        Line(g, p, c, 10, 20, 14, 20);
                        break;
                }
            }
            g.SmoothingMode = old;
            return true;
        }
    }
}
