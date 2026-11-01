using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusMidnight
{
    class ThemeDef
    {
        public string Name;
        public bool Dark = true;
        public Color Base, Accent;
        public double Lift = 1.0;
        public ThemeDef(string name, bool dark, Color b, Color a, double lift)
        {
            Name = name; Dark = dark; Base = b; Accent = a; Lift = lift;
        }
    }

    // Design tokens follow dark-theme guidance: near-black surfaces (not pure black), depth from lighter
    // surfaces plus hairlines instead of shadows, one desaturated accent, AA-contrast text.
    // Every colour is derived from two inputs per theme (a base and an accent) in Apply().
    static class Theme
    {
        public static Color Bg0 = Color.FromArgb(11, 13, 18);
        public static Color Bg1 = Color.FromArgb(16, 19, 26);
        public static Color Bg2 = Color.FromArgb(21, 25, 34);
        public static Color Bg3 = Color.FromArgb(27, 32, 43);
        public static Color Bg4 = Color.FromArgb(35, 41, 56);
        public static Color Bg5 = Color.FromArgb(44, 51, 69);
        public static Color T1 = Color.FromArgb(232, 234, 239);
        public static Color T2 = Color.FromArgb(170, 177, 190);
        public static Color T3 = Color.FromArgb(122, 130, 145);
        public static Color Line = Color.FromArgb(36, 41, 53);
        public static Color Line2 = Color.FromArgb(56, 63, 80);
        public static Color Acc = Color.FromArgb(55, 122, 230);
        public static Color AccHover = Color.FromArgb(82, 142, 240);
        public static Color AccDown = Color.FromArgb(40, 100, 200);
        public static Color AccText = Color.FromArgb(142, 184, 250);
        public static Color AccSoft = Color.FromArgb(25, 39, 62);
        public static Color AccDeep = Color.FromArgb(17, 27, 45);
        public static Color Good = Color.FromArgb(127, 180, 255);
        public static Color Ok = Color.FromArgb(155, 183, 230);
        public static Color Warn = Color.FromArgb(232, 180, 95);
        public static Color WarnSoft = Color.FromArgb(58, 47, 26);
        public static Color Bad = Color.FromArgb(240, 141, 134);
        public static Color BadSoft = Color.FromArgb(58, 31, 34);
        public static bool Dark = true;
        public static string Current = "Midnight";

        public static readonly List<ThemeDef> All = new List<ThemeDef>(new ThemeDef[]
        {
            new ThemeDef("Midnight", true, Color.FromArgb(11, 13, 18), Color.FromArgb(55, 122, 230), 1.0),
            new ThemeDef("Ink", true, Color.FromArgb(0, 0, 0), Color.FromArgb(76, 136, 242), 2.0),
            new ThemeDef("Ocean", true, Color.FromArgb(8, 16, 19), Color.FromArgb(26, 170, 190), 1.1),
            new ThemeDef("Violet", true, Color.FromArgb(14, 12, 22), Color.FromArgb(132, 102, 236), 1.1),
            new ThemeDef("Rose", true, Color.FromArgb(20, 12, 16), Color.FromArgb(226, 84, 132), 1.1),
            new ThemeDef("Forest", true, Color.FromArgb(9, 16, 13), Color.FromArgb(48, 168, 108), 1.1),
            new ThemeDef("Graphite", true, Color.FromArgb(17, 17, 19), Color.FromArgb(150, 160, 178), 1.0),
            new ThemeDef("Sunset", true, Color.FromArgb(20, 13, 9), Color.FromArgb(240, 130, 60), 1.1),
            new ThemeDef("Crimson", true, Color.FromArgb(20, 9, 11), Color.FromArgb(214, 56, 66), 1.1),
            new ThemeDef("Mint", true, Color.FromArgb(8, 17, 15), Color.FromArgb(52, 199, 160), 1.1),
            new ThemeDef("Gold", true, Color.FromArgb(19, 16, 8), Color.FromArgb(224, 172, 40), 1.1),
            new ThemeDef("Sky", true, Color.FromArgb(9, 15, 22), Color.FromArgb(70, 170, 240), 1.1),
            new ThemeDef("Lavender", true, Color.FromArgb(16, 14, 24), Color.FromArgb(170, 140, 240), 1.1),
            new ThemeDef("Coral", true, Color.FromArgb(21, 12, 12), Color.FromArgb(244, 118, 102), 1.1),
            new ThemeDef("Lime", true, Color.FromArgb(12, 17, 8), Color.FromArgb(130, 200, 60), 1.1),
            new ThemeDef("Sakura", true, Color.FromArgb(21, 13, 18), Color.FromArgb(238, 140, 184), 1.1),
            new ThemeDef("Slate", true, Color.FromArgb(14, 16, 20), Color.FromArgb(110, 140, 190), 1.0),
            new ThemeDef("Teal", true, Color.FromArgb(7, 17, 18), Color.FromArgb(20, 184, 166), 1.1),
            new ThemeDef("Indigo", true, Color.FromArgb(11, 12, 24), Color.FromArgb(88, 92, 230), 1.1)
        });

        public static string Mode = "system"; // system | light | dark
        public static bool NumbersOnly;        // show 92 instead of A-

        // a letter grade, or the rounded whole number when the student prefers numbers
        public static string GradeStr(string letter, double? pct)
        {
            if (NumbersOnly) return pct.HasValue ? Math.Round(pct.Value).ToString() : "\u2013";
            return string.IsNullOrEmpty(letter) ? "\u2013" : letter;
        }

        // Windows' own light/dark app setting
        public static bool SystemIsDark()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                if (v is int) return (int)v == 0;
            }
            catch (Exception) { }
            return true;
        }

        public static bool ResolveDark(string mode)
        {
            if (mode == "light") return false;
            if (mode == "dark") return true;
            return SystemIsDark();
        }

        public static float Scale = 1f;
        public static Font Body, Small, Semi, SemiSmall, Title, Big, Huge, IconFont, IconSmall, Mid;
        public static bool HasIcons;

        public static int S(int v)
        {
            return (int)Math.Round(v * Scale);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static ThemeDef Find(string name)
        {
            foreach (ThemeDef d in All)
            {
                if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
            }
            return All[0];
        }

        public static void Apply(string name, string mode)
        {
            Mode = mode == "light" || mode == "dark" ? mode : "system";
            Apply(name, ResolveDark(Mode));
        }

        public static void Apply(string name)
        {
            Apply(name, Dark);
        }

        public static void Apply(string name, bool dark)
        {
            ThemeDef d = Find(name);
            Current = d.Name;
            Dark = dark;
            Color white = Color.White, black = Color.Black;
            Acc = d.Accent;
            if (dark)
            {
                Color tint = Mix(Acc, white, 0.5);
                double k = d.Lift;
                Bg0 = d.Base;
                Bg1 = Mix(Bg0, tint, 0.030 * k);
                Bg2 = Mix(Bg0, tint, 0.055 * k);
                Bg3 = Mix(Bg0, tint, 0.090 * k);
                Bg4 = Mix(Bg0, tint, 0.135 * k);
                Bg5 = Mix(Bg0, tint, 0.190 * k);
                Line = Mix(Bg0, tint, 0.115 * Math.Min(k, 1.6));
                Line2 = Mix(Bg0, tint, 0.200 * Math.Min(k, 1.6));
                T1 = Mix(Color.FromArgb(236, 238, 243), tint, 0.05);
                T2 = Mix(T1, Bg0, 0.28);
                T3 = Mix(T1, Bg0, 0.50);
                AccHover = Mix(Acc, white, 0.18);
                AccDown = Mix(Acc, black, 0.20);
                AccText = Mix(Acc, white, 0.50);
                AccSoft = Mix(Bg0, Acc, 0.20);
                AccDeep = Mix(Bg0, Acc, 0.11);
                Good = AccText;
                Ok = Mix(AccText, T2, 0.45);
                Warn = Color.FromArgb(232, 180, 95);
                WarnSoft = Mix(Bg0, Warn, 0.17);
                Bad = Color.FromArgb(240, 141, 134);
                BadSoft = Mix(Bg0, Bad, 0.18);
            }
            else
            {
                Acc = Mix(d.Accent, black, d.Name == "Graphite" ? 0.38 : 0.12);
                Bg0 = Mix(Color.FromArgb(240, 242, 247), d.Accent, 0.035);
                Bg1 = white;
                Bg2 = white;
                Bg3 = Mix(Bg0, white, 0.55);
                Bg4 = Mix(Bg0, black, 0.05);
                Bg5 = Mix(Bg0, black, 0.10);
                Line = Mix(Bg0, black, 0.09);
                Line2 = Mix(Bg0, black, 0.17);
                T1 = Color.FromArgb(20, 24, 32);
                T2 = Mix(T1, white, 0.35);
                T3 = Mix(T1, white, 0.55);
                AccHover = Mix(Acc, black, 0.12);
                AccDown = Mix(Acc, black, 0.25);
                AccText = Mix(Acc, black, 0.05);
                AccSoft = Mix(white, Acc, 0.13);
                AccDeep = Mix(white, Acc, 0.07);
                Good = AccText;
                Ok = Mix(AccText, T2, 0.30);
                Warn = Color.FromArgb(176, 104, 6);
                WarnSoft = Color.FromArgb(252, 239, 214);
                Bad = Color.FromArgb(196, 48, 43);
                BadSoft = Color.FromArgb(252, 230, 228);
            }
        }

        // the four colours that make up a theme's little preview picture
        public static void Preview(ThemeDef d, out Color bg, out Color surface, out Color accent, out Color text)
        {
            string keep = Current;
            Apply(d.Name);
            bg = Bg0; surface = Bg2; accent = Acc; text = T1;
            Apply(keep);
        }

        static readonly Color[] TagColors = new Color[]
        {
            Color.FromArgb(90, 150, 240), Color.FromArgb(52, 176, 168), Color.FromArgb(150, 120, 235), Color.FromArgb(228, 108, 150),
            Color.FromArgb(88, 186, 118), Color.FromArgb(232, 160, 70), Color.FromArgb(78, 178, 220), Color.FromArgb(198, 118, 198)
        };

        // a stable accent colour for a name (used for class colour strips)
        public static Color Tag(string s)
        {
            if (string.IsNullOrEmpty(s)) return TagColors[0];
            int h = 17;
            foreach (char c in s.ToLowerInvariant()) h = h * 31 + c;
            if (h < 0) h = -(h + 1);
            return TagColors[h % TagColors.Length];
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
            Mid = new Font("Segoe UI Semibold", 13f);
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
            IconSmall = new Font(HasIcons ? "Segoe MDL2 Assets" : "Segoe UI", 11f);
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

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color line, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(line, width))
            {
                g.DrawPath(pen, p);
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

        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string[] parts = name.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string s = parts[0].Substring(0, 1);
            if (parts.Length > 1) s += parts[parts.Length - 1].Substring(0, 1);
            return s.ToUpperInvariant();
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

        // asks Windows 11 for rounded corners on a borderless window; false on older Windows
        public static bool RoundCorners(IntPtr h)
        {
            try { int pref = 2; return DwmSetWindowAttribute(h, 33, ref pref, 4) == 0; }
            catch (Exception) { return false; }
        }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr h);

        public static void DarkTitle(IntPtr h)
        {
            try
            {
                int on = Theme.Dark ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref on, 4) != 0) DwmSetWindowAttribute(h, 19, ref on, 4);
                int cap = Theme.Bg0.R | (Theme.Bg0.G << 8) | (Theme.Bg0.B << 16); // COLORREF 0x00BBGGRR (Windows 11)
                DwmSetWindowAttribute(h, 35, ref cap, 4);
                int txt = Theme.T1.R | (Theme.T1.G << 8) | (Theme.T1.B << 16);
                DwmSetWindowAttribute(h, 36, ref txt, 4);
            }
            catch (Exception) { }
        }

        public static void DarkScroll(IntPtr h)
        {
            try { SetWindowTheme(h, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null); }
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
