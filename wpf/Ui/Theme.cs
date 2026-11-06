using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CampusMidnight
{
    class ThemeDef
    {
        public string Name, LightName = ""; public Color Base, Accent, Base2, Accent2, LightAccent; public double Lift; public bool Gradient, HasLight, Adaptive;   // Adaptive: grades, statuses and tags keep their own meaning colours
        public string Shown(bool dark) { return Name == "Custom" ? "Custom (beta)" : (dark || LightName.Length == 0 ? Name : LightName); }
        public ThemeDef Semantic() { Adaptive = true; return this; }
        public ThemeDef Light(string lightName, byte r, byte g, byte b) { LightName = lightName; LightAccent = Color.FromRgb(r, g, b); HasLight = true; return this; }
        public ThemeDef(string n, byte br, byte bg, byte bb, byte ar, byte ag, byte ab, double lift)
        {
            Name = n; Base = Color.FromRgb(br, bg, bb); Accent = Color.FromRgb(ar, ag, ab); Lift = lift;
            Base2 = Base; Accent2 = Accent;
        }

        // a gradient theme: the page fades from Base to Base2 and filled buttons from Accent to Accent2
        public ThemeDef(string n, byte br, byte bg, byte bb, byte b2r, byte b2g, byte b2b, byte ar, byte ag, byte ab, byte a2r, byte a2g, byte a2b, double lift)
            : this(n, br, bg, bb, ar, ag, ab, lift)
        {
            Base2 = Color.FromRgb(b2r, b2g, b2b); Accent2 = Color.FromRgb(a2r, a2g, a2b); Gradient = true;
        }
    }

    // Same palette maths as the WinForms build: every colour comes from a base and an accent.
    // Colours are pushed into Application.Resources so every element using DynamicResource follows live.
    static class Theme
    {
        public static Color Bg0, Bg1, Bg2, Bg3, Bg4, Bg5, T1, T2, T3, Line, Line2, Acc, AccHover, AccDown, AccText, AccSoft, AccDeep, AccOn, Div;
        public static Color Good, Ok, Warn, WarnSoft, Bad, BadSoft;
        public static bool Dark = true;
        public static string Current = "Midnight", Mode = "system";
        public static bool NumbersOnly;
        public static Color BgEnd, Acc2;
        public static bool Gradient;

        public static readonly List<ThemeDef> All = new List<ThemeDef>(new ThemeDef[]
        {
            new ThemeDef("Midnight", 0,0,0, 245,246,250, 1.0).Light("Daylight", 22,24,30), new ThemeDef("Campus", 7,17,9, 96,142,40, 1.1).Light("", 146,200,65), new ThemeDef("Adaptive", 12,14,19, 128,140,235, 1.0).Semantic(), new ThemeDef("Custom", 20,22,28, 122,162,255, 1.0), new ThemeDef("Nebula", 13,9,26, 160,110,255, 1.15),
            new ThemeDef("Ember", 22,10,6, 255,106,40, 1.1), new ThemeDef("Glacier", 8,16,24, 110,205,250, 1.1),
            new ThemeDef("Jade", 6,16,13, 40,200,140, 1.1), new ThemeDef("Orchid", 22,9,22, 220,100,220, 1.1),
            new ThemeDef("Saffron", 20,15,6, 245,180,30, 1.1), new ThemeDef("Cherry", 22,7,12, 235,60,90, 1.1),
            new ThemeDef("Lagoon", 5,17,20, 30,200,210, 1.1), new ThemeDef("Dusk", 17,13,24, 255,150,120, 1.1),
            new ThemeDef("Matcha", 12,16,9, 170,205,90, 1.1), new ThemeDef("Cobalt", 7,10,26, 60,100,255, 1.15),
            new ThemeDef("Bronze", 18,14,10, 205,150,90, 1.05), new ThemeDef("Steel", 14,17,21, 130,160,200, 1.0),
            new ThemeDef("Aurora", 5,26,38, 34,10,70, 40,220,170, 110,120,255, 1.1),
            new ThemeDef("Sunrise", 44,8,26, 72,34,6, 255,90,120, 255,170,60, 1.1),
            new ThemeDef("Nightfall", 6,12,50, 52,8,84, 70,140,255, 190,90,255, 1.1),
        });

        public static Color? CustomText;

        public static bool TryHex(string s, out Color c)
        {
            c = Colors.Black;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length != 6) return false;
            int v;
            if (!int.TryParse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }

        // the Custom theme's colours come from the settings: background, accent and text
        public static void SetCustom(string baseHex, string accentHex, string textHex)
        {
            ThemeDef d = Find("Custom");
            Color c;
            if (TryHex(baseHex, out c)) d.Base = c;
            if (TryHex(accentHex, out c)) { d.Accent = c; d.Accent2 = c; }
            d.Base2 = d.Base;
            CustomText = TryHex(textHex, out c) ? (Color?)c : null;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static ThemeDef Find(string name)
        {
            if (string.Equals(name, "Neon", StringComparison.OrdinalIgnoreCase)) name = "Campus";   // the old Neon theme became Campus
            foreach (ThemeDef d in All) if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) || (d.LightName.Length > 0 && string.Equals(d.LightName, name, StringComparison.OrdinalIgnoreCase))) return d;
            return All[0];
        }

        public static bool SystemIsDark()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
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

        public static string GradeStr(string letter, double? pct)
        {
            if (NumbersOnly) return pct.HasValue ? Math.Round(pct.Value).ToString() : "–";
            return string.IsNullOrEmpty(letter) ? "–" : letter;
        }

        public static Color GradeColor(double? p)
        {
            if (!p.HasValue) return T3;
            if (p.Value >= 90) return Good;
            if (p.Value >= 80) return Ok;
            if (p.Value >= 70) return Warn;
            return Bad;
        }

        static readonly Color[] Tags = new Color[]
        {
            Color.FromRgb(90,150,240), Color.FromRgb(52,176,168), Color.FromRgb(150,120,235), Color.FromRgb(228,108,150),
            Color.FromRgb(88,186,118), Color.FromRgb(232,160,70), Color.FromRgb(78,178,220), Color.FromRgb(198,118,198)
        };

        public static Color Tag(string s)
        {
            if (string.IsNullOrEmpty(s)) return Tags[0];
            int h = 17;
            foreach (char c in s.ToLowerInvariant()) h = unchecked(h * 31 + c);
            if (h < 0) h = -(h + 1);
            return Tags[h % Tags.Length];
        }

        public static void Apply(string name, string mode)
        {
            Mode = mode == "light" || mode == "dark" ? mode : "system";
            Compute(name, ResolveDark(Mode));
            Push();
        }

        static Color[] SaveAll()
        {
            return new Color[] { Bg0, Bg1, Bg2, Bg3, Bg4, Bg5, T1, T2, T3, Line, Line2, Acc, AccHover, AccDown, AccText, AccSoft, AccDeep, Good, Ok, Warn, WarnSoft, Bad, BadSoft, AccOn, Div };
        }

        static void RestoreAll(Color[] c)
        {
            Bg0 = c[0]; Bg1 = c[1]; Bg2 = c[2]; Bg3 = c[3]; Bg4 = c[4]; Bg5 = c[5]; T1 = c[6]; T2 = c[7]; T3 = c[8]; Line = c[9]; Line2 = c[10];
            Acc = c[11]; AccHover = c[12]; AccDown = c[13]; AccText = c[14]; AccSoft = c[15]; AccDeep = c[16]; Good = c[17]; Ok = c[18];
            Warn = c[19]; WarnSoft = c[20]; Bad = c[21]; BadSoft = c[22]; AccOn = c[23]; Div = c[24];
        }

        // the four colours that make up a theme's little preview picture (current theme is left untouched)
        public static void Preview(ThemeDef d, out Color bg, out Color surface, out Color accent, out Color text)
        {
            Color[] keep = SaveAll();
            string keepName = Current;
            bool keepDark = Dark;
            bool keepGrad = Gradient;
            Color keepEnd = BgEnd, keepAcc2 = Acc2;
            Compute(d.Name, Dark);
            bg = Bg0; surface = Bg2; accent = Acc; text = T1;
            RestoreAll(keep);
            Current = keepName;
            Dark = keepDark;
            Gradient = keepGrad; BgEnd = keepEnd; Acc2 = keepAcc2;
        }

        static void Compute(string name, bool dark)
        {
            ThemeDef d = Find(name);
            Current = d.Name;
            Dark = dark;
            Color white = Colors.White, black = Colors.Black;
            Acc = d.Accent;
            Gradient = d.Gradient;
            Acc2 = d.Accent2;
            if (Dark)
            {
                Color tint = Mix(Acc, white, 0.5);
                double k = d.Lift;
                Bg0 = d.Base;
                Bg1 = Mix(Bg0, tint, 0.030 * k); Bg2 = Mix(Bg0, tint, 0.055 * k); Bg3 = Mix(Bg0, tint, 0.090 * k);
                Bg4 = Mix(Bg0, tint, 0.135 * k); Bg5 = Mix(Bg0, tint, 0.190 * k);
                Line = Mix(Bg0, tint, 0.115 * Math.Min(k, 1.6)); Line2 = Mix(Bg0, tint, 0.200 * Math.Min(k, 1.6));
                T1 = Mix(Color.FromRgb(236, 238, 243), tint, 0.05); T2 = Mix(T1, Bg0, 0.28); T3 = Mix(T1, Bg0, 0.50);
                AccHover = Mix(Acc, white, 0.18); AccDown = Mix(Acc, black, 0.20); AccText = Mix(Acc, white, 0.50);
                AccSoft = Mix(Bg0, Acc, 0.20); AccDeep = Mix(Bg0, Acc, 0.11);
                Good = AccText; Ok = Mix(AccText, T2, 0.45);
                Warn = Color.FromRgb(232, 180, 95); WarnSoft = Mix(Bg0, Warn, 0.17);
                Bad = Color.FromRgb(240, 141, 134); BadSoft = Mix(Bg0, Bad, 0.18);
                BgEnd = d.Base2;
            }
            else
            {
                Acc = d.HasLight ? d.LightAccent : Mix(d.Accent, black, d.Name == "Steel" ? 0.3 : 0.12);
                Bg0 = Mix(Color.FromRgb(240, 242, 247), Acc, d.HasLight ? 0.02 : 0.035);
                Bg1 = white; Bg2 = white; Bg3 = Mix(Bg0, white, 0.55); Bg4 = Mix(Bg0, black, 0.05); Bg5 = Mix(Bg0, black, 0.10);
                Line = Mix(Bg0, black, 0.09); Line2 = Mix(Bg0, black, 0.17);
                T1 = Color.FromRgb(20, 24, 32); T2 = Mix(T1, white, 0.35); T3 = Mix(T1, white, 0.55);
                AccHover = Mix(Acc, black, 0.12); AccDown = Mix(Acc, black, 0.25); AccText = Mix(Acc, black, 0.05);
                AccSoft = Mix(white, Acc, 0.13); AccDeep = Mix(white, Acc, 0.07);
                Good = AccText; Ok = Mix(AccText, T2, 0.30);
                Warn = Color.FromRgb(176, 104, 6); WarnSoft = Color.FromRgb(252, 239, 214);
                Bad = Color.FromRgb(196, 48, 43); BadSoft = Color.FromRgb(252, 230, 228);
                BgEnd = Mix(Color.FromRgb(240, 242, 247), d.Accent2, 0.07);
                Acc2 = d.HasLight ? Acc : Mix(d.Accent2, black, 0.12);
            }
            Div = Dark ? Mix(Bg2, Colors.Black, 0.55) : Mix(white, Acc, 0.09);
            if (d.Name == "Custom" && Dark && CustomText.HasValue)
            {
                T1 = CustomText.Value; T2 = Mix(T1, Bg0, 0.28); T3 = Mix(T1, Bg0, 0.50);
            }
            if (d.Adaptive)
            {
                // the accent stays quiet; what the data means picks its colour: high grades green, low ones red
                if (Dark) { Good = Color.FromRgb(98, 214, 138); Ok = Color.FromRgb(176, 216, 96); Warn = Color.FromRgb(240, 176, 64); Bad = Color.FromRgb(244, 112, 108); }
                else { Good = Color.FromRgb(20, 132, 66); Ok = Color.FromRgb(96, 136, 12); Warn = Color.FromRgb(176, 104, 6); Bad = Color.FromRgb(196, 48, 43); }
                WarnSoft = Mix(Bg0, Warn, Dark ? 0.17 : 0.14); BadSoft = Mix(Bg0, Bad, Dark ? 0.18 : 0.14);
            }
            AccOn = (d.Name == "Midnight" && Dark) ? Color.FromRgb(12, 13, 17) : white;   // only Midnight has a light accent in dark mode
        }

        static void Set(string key, Color c)
        {
            ResourceDictionary r = Application.Current.Resources;
            r[key] = new SolidColorBrush(c);
            r[key + "C"] = c;
        }

        public static double ShadowOp { get { return Dark ? 0.4 : 0.14; } }

        static void Push()
        {
            Application.Current.Resources["ShadowOp"] = ShadowOp;
            Application.Current.Resources["ShadowC"] = Dark ? Colors.Black : Color.FromRgb(40, 50, 75);
            Set("Bg0", Bg0); Set("Bg1", Bg1); Set("Bg2", Bg2); Set("Bg3", Bg3); Set("Bg4", Bg4); Set("Bg5", Bg5);
            Set("T1", T1); Set("T2", T2); Set("T3", T3); Set("Line", Line); Set("Line2", Line2);
            Set("Acc", Acc); Set("AccHover", AccHover); Set("AccDown", AccDown); Set("AccText", AccText);
            Set("AccSoft", AccSoft); Set("AccDeep", AccDeep); Set("Good", Good); Set("Ok", Ok);
            Set("AccOn", AccOn); Set("Div", Div); Set("Warn", Warn); Set("WarnSoft", WarnSoft); Set("Bad", Bad); Set("BadSoft", BadSoft);
            ResourceDictionary r = Application.Current.Resources;
            if (Gradient)
            {
                LinearGradientBrush page = new LinearGradientBrush(Bg0, BgEnd, new Point(0, 0), new Point(1, 1));
                page.Freeze();
                r["Bg0"] = page;
                LinearGradientBrush ab = new LinearGradientBrush(Acc, Acc2, new Point(0, 0), new Point(1, 0));
                ab.Freeze();
                r["AccGrad"] = ab;
                // sidebar and cards let the gradient show through
                r["Bg1"] = new SolidColorBrush(Color.FromArgb(150, Bg1.R, Bg1.G, Bg1.B));
                r["CardBg"] = new SolidColorBrush(Color.FromArgb(165, Bg2.R, Bg2.G, Bg2.B));
            }
            else
            {
                r["AccGrad"] = new SolidColorBrush(Acc);
                r["CardBg"] = new SolidColorBrush(Bg2);
            }
        }

        // the end colours of a gradient theme's preview
        public static void PreviewEnds(ThemeDef d, out Color bgEnd, out Color acc2)
        {
            Color[] keep = SaveAll();
            string keepName = Current;
            bool keepDark = Dark, keepGrad = Gradient;
            Color keepEnd = BgEnd, keepAcc2 = Acc2;
            Compute(d.Name, Dark);
            bgEnd = BgEnd; acc2 = Acc2;
            RestoreAll(keep);
            Current = keepName; Dark = keepDark; Gradient = keepGrad; BgEnd = keepEnd; Acc2 = keepAcc2;
        }
    }
}
