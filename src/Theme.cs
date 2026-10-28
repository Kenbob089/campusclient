using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusMidnight
{
    // Design tokens follow dark-theme guidance: near-black surfaces (not pure black), depth from lighter
    // surfaces plus hairlines instead of shadows, one desaturated blue accent, AA-contrast text.
    static class Theme
    {
        public static readonly Color Bg0 = Color.FromArgb(11, 13, 18);
        public static readonly Color Bg1 = Color.FromArgb(16, 19, 26);
        public static readonly Color Bg2 = Color.FromArgb(21, 25, 34);
        public static readonly Color Bg3 = Color.FromArgb(27, 32, 43);
        public static readonly Color Bg4 = Color.FromArgb(35, 41, 56);
        public static readonly Color Bg5 = Color.FromArgb(44, 51, 69);
        public static readonly Color T1 = Color.FromArgb(232, 234, 239);
        public static readonly Color T2 = Color.FromArgb(170, 177, 190);
        public static readonly Color T3 = Color.FromArgb(122, 130, 145);
        public static readonly Color Line = Color.FromArgb(36, 41, 53);
        public static readonly Color Line2 = Color.FromArgb(56, 63, 80);
        public static readonly Color Acc = Color.FromArgb(55, 122, 230);
        public static readonly Color AccHover = Color.FromArgb(82, 142, 240);
        public static readonly Color AccDown = Color.FromArgb(40, 100, 200);
        public static readonly Color AccText = Color.FromArgb(142, 184, 250);
        public static readonly Color AccSoft = Color.FromArgb(25, 39, 62);
        public static readonly Color AccDeep = Color.FromArgb(17, 27, 45);
        public static readonly Color Good = Color.FromArgb(127, 180, 255);
        public static readonly Color Ok = Color.FromArgb(155, 183, 230);
        public static readonly Color Warn = Color.FromArgb(232, 180, 95);
        public static readonly Color WarnSoft = Color.FromArgb(58, 47, 26);
        public static readonly Color Bad = Color.FromArgb(240, 141, 134);
        public static readonly Color BadSoft = Color.FromArgb(58, 31, 34);

        public static float Scale = 1f;
        public static Font Body, Small, Semi, SemiSmall, Title, Big, Huge, IconFont;
        public static bool HasIcons;

        public static int S(int v)
        {
            return (int)Math.Round(v * Scale);
        }

        public static void Init()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                Scale = g.DpiX / 96f;
            }
            if (Scale < 1f) Scale = 1f;
            Body = new Font("Segoe UI", 10f);
            Small = new Font("Segoe UI", 9f);
            Semi = new Font("Segoe UI Semibold", 10.5f);
            SemiSmall = new Font("Segoe UI Semibold", 9f);
            Title = new Font("Segoe UI Semibold", 22f);
            Big = new Font("Segoe UI Semibold", 20f);
            Huge = new Font("Segoe UI Semibold", 26f);
            HasIcons = false;
            try
            {
                using (InstalledFontCollection ifc = new InstalledFontCollection())
                {
                    foreach (FontFamily ff in ifc.Families)
                    {
                        if (ff.Name == "Segoe MDL2 Assets") HasIcons = true;
                    }
                }
            }
            catch (Exception) { }
            IconFont = new Font(HasIcons ? "Segoe MDL2 Assets" : "Segoe UI", 15f);
        }

        public static Color GradeColor(double? p)
        {
            if (!p.HasValue) return T3;
            if (p.Value >= 90) return Good;
            if (p.Value >= 80) return Ok;
            if (p.Value >= 70) return Warn;
            return Bad;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            d = Math.Max(2, Math.Min(d, Math.Min(r.Width, r.Height)));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
            }
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r, bool right, bool center)
        {
            TextFormatFlags fl = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine
                | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
            if (right) fl |= TextFormatFlags.Right;
            else if (center) fl |= TextFormatFlags.HorizontalCenter;
            else fl |= TextFormatFlags.Left;
            TextRenderer.DrawText(g, s == null ? "" : s, f, r, c, fl);
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            DrawText(g, s, f, c, r, false, false);
        }

        public static Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s == null ? "" : s, f, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        public static int WrapHeight(string s, Font f, int width)
        {
            if (width < 10) width = 10;
            return TextRenderer.MeasureText(s == null ? "" : s, f, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
        }

        public static void DrawWrapped(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s == null ? "" : s, f, r, c,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
        }

        public static int PillWidth(string text, Font f)
        {
            return Measure(text, f).Width + S(18);
        }

        // draws a rounded label chip; returns its width
        public static int DrawPill(Graphics g, string text, Font f, Color back, Color fore, int x, int y, int h)
        {
            int w = PillWidth(text, f);
            Rectangle r = new Rectangle(x, y, w, h);
            FillRound(g, r, h / 2, back);
            DrawText(g, text, f, fore, r, false, true);
            return w;
        }

        public static string Fmt(double? n)
        {
            if (!n.HasValue) return "";
            return Math.Round(n.Value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    static class Native
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idlist);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr w, IntPtr l);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        static extern IntPtr SendMessageStr(IntPtr hWnd, int msg, IntPtr w, string l);

        // grey hint text shown inside an empty text box (EM_SETCUEBANNER)
        public static void SetCue(TextBox b, string text)
        {
            try { SendMessageStr(b.Handle, 0x1501, (IntPtr)1, text); }
            catch (Exception) { }
        }

        public static void DarkTitle(IntPtr h)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(h, 20, ref on, 4) != 0) DwmSetWindowAttribute(h, 19, ref on, 4);
                int cap = 0x00120D0B; // COLORREF 0x00BBGGRR for #0B0D12 (Windows 11)
                DwmSetWindowAttribute(h, 35, ref cap, 4);
            }
            catch (Exception) { }
        }

        public static void DarkScroll(IntPtr h)
        {
            try { SetWindowTheme(h, "DarkMode_Explorer", null); }
            catch (Exception) { }
        }

        public static void SetRedraw(Control c, bool on)
        {
            try
            {
                SendMessage(c.Handle, 0x000B, on ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
                if (on) c.Invalidate(true);
            }
            catch (Exception) { }
        }
    }
}
