using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CampusMidnight
{
    class ThemeDef
    {
        public string Name; public Color Base, Accent; public double Lift;
        public ThemeDef(string n, byte br, byte bg, byte bb, byte ar, byte ag, byte ab, double lift)
        {
            Name = n; Base = Color.FromRgb(br, bg, bb); Accent = Color.FromRgb(ar, ag, ab); Lift = lift;
        }
    }

    // Same palette maths as the WinForms build: every colour comes from a base and an accent.
    // Colours are pushed into Application.Resources so every element using DynamicResource follows live.
    static class Theme
    {
        public static Color Bg0, Bg1, Bg2, Bg3, Bg4, Bg5, T1, T2, T3, Line, Line2, Acc, AccHover, AccDown, AccText, AccSoft, AccDeep;
        public static Color Good, Ok, Warn, WarnSoft, Bad, BadSoft;
        public static bool Dark = true;
        public static string Current = "Midnight", Mode = "system";
        public static bool NumbersOnly;

        public static readonly List<ThemeDef> All = new List<ThemeDef>(new ThemeDef[]
        {
            new ThemeDef("Midnight", 11,13,18, 55,122,230, 1.0), new ThemeDef("Ink", 0,0,0, 76,136,242, 2.0),
            new ThemeDef("Ocean", 8,16,19, 26,170,190, 1.1), new ThemeDef("Violet", 14,12,22, 132,102,236, 1.1),
            new ThemeDef("Rose", 20,12,16, 226,84,132, 1.1), new ThemeDef("Forest", 9,16,13, 48,168,108, 1.1),
            new ThemeDef("Graphite", 17,17,19, 150,160,178, 1.0), new ThemeDef("Sunset", 20,13,9, 240,130,60, 1.1),
            new ThemeDef("Crimson", 20,9,11, 214,56,66, 1.1), new ThemeDef("Mint", 8,17,15, 52,199,160, 1.1),
            new ThemeDef("Gold", 19,16,8, 224,172,40, 1.1), new ThemeDef("Sky", 9,15,22, 70,170,240, 1.1),
            new ThemeDef("Lavender", 16,14,24, 170,140,240, 1.1), new ThemeDef("Coral", 21,12,12, 244,118,102, 1.1),
            new ThemeDef("Lime", 12,17,8, 130,200,60, 1.1), new ThemeDef("Sakura", 21,13,18, 238,140,184, 1.1),
            new ThemeDef("Slate", 14,16,20, 110,140,190, 1.0), new ThemeDef("Teal", 7,17,18, 20,184,166, 1.1),
            new ThemeDef("Indigo", 11,12,24, 88,92,230, 1.1)
        });

        // the four colours of a theme's little preview picture (for the current light/dark mode)
        public static void Preview(ThemeDef d, out Color bg, out Color surface, out Color accent, out Color text)
        {
            Color white = Colors.White, black = Colors.Black;
            if (Dark)
            {
                Color tint = Mix(d.Accent, white, 0.5);
                bg = d.Base;
                surface = Mix(bg, tint, 0.055 * d.Lift);
                accent = d.Accent;
                text = Mix(Color.FromRgb(236, 238, 243), tint, 0.05);
            }
            else
            {
                accent = Mix(d.Accent, black, d.Name == "Graphite" ? 0.38 : 0.12);
                bg = Mix(Color.FromRgb(240, 242, 247), d.Accent, 0.035);
                surface = white;
                text = Color.FromRgb(20, 24, 32);
            }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static ThemeDef Find(string name)
        {
            foreach (ThemeDef d in All) if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
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
            return new Color[] { Bg0, Bg1, Bg2, Bg3, Bg4, Bg5, T1, T2, T3, Line, Line2, Acc, AccHover, AccDown, AccText, AccSoft, AccDeep, Good, Ok, Warn, WarnSoft, Bad, BadSoft };
        }

        static void RestoreAll(Color[] c)
        {
            Bg0 = c[0]; Bg1 = c[1]; Bg2 = c[2]; Bg3 = c[3]; Bg4 = c[4]; Bg5 = c[5]; T1 = c[6]; T2 = c[7]; T3 = c[8]; Line = c[9]; Line2 = c[10];
            Acc = c[11]; AccHover = c[12]; AccDown = c[13]; AccText = c[14]; AccSoft = c[15]; AccDeep = c[16]; Good = c[17]; Ok = c[18];
            Warn = c[19]; WarnSoft = c[20]; Bad = c[21]; BadSoft = c[22];
        }

        // the four colours that make up a theme's little preview picture (current theme is left untouched)
        public static void Preview(ThemeDef d, out Color bg, out Color surface, out Color accent, out Color text)
        {
            Color[] keep = SaveAll();
            string keepName = Current;
            bool keepDark = Dark;
            Compute(d.Name, Dark);
            bg = Bg0; surface = Bg2; accent = Acc; text = T1;
            RestoreAll(keep);
            Current = keepName;
            Dark = keepDark;
        }

        static void Compute(string name, bool dark)
        {
            ThemeDef d = Find(name);
            Current = d.Name;
            Dark = dark;
            Color white = Colors.White, black = Colors.Black;
            Acc = d.Accent;
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
            }
            else
            {
                Acc = Mix(d.Accent, black, d.Name == "Graphite" ? 0.38 : 0.12);
                Bg0 = Mix(Color.FromRgb(240, 242, 247), d.Accent, 0.035);
                Bg1 = white; Bg2 = white; Bg3 = Mix(Bg0, white, 0.55); Bg4 = Mix(Bg0, black, 0.05); Bg5 = Mix(Bg0, black, 0.10);
                Line = Mix(Bg0, black, 0.09); Line2 = Mix(Bg0, black, 0.17);
                T1 = Color.FromRgb(20, 24, 32); T2 = Mix(T1, white, 0.35); T3 = Mix(T1, white, 0.55);
                AccHover = Mix(Acc, black, 0.12); AccDown = Mix(Acc, black, 0.25); AccText = Mix(Acc, black, 0.05);
                AccSoft = Mix(white, Acc, 0.13); AccDeep = Mix(white, Acc, 0.07);
                Good = AccText; Ok = Mix(AccText, T2, 0.30);
                Warn = Color.FromRgb(176, 104, 6); WarnSoft = Color.FromRgb(252, 239, 214);
                Bad = Color.FromRgb(196, 48, 43); BadSoft = Color.FromRgb(252, 230, 228);
            }
        }

        static void Set(string key, Color c)
        {
            ResourceDictionary r = Application.Current.Resources;
            r[key] = new SolidColorBrush(c);
            r[key + "C"] = c;
        }

        static void Push()
        {
            Set("Bg0", Bg0); Set("Bg1", Bg1); Set("Bg2", Bg2); Set("Bg3", Bg3); Set("Bg4", Bg4); Set("Bg5", Bg5);
            Set("T1", T1); Set("T2", T2); Set("T3", T3); Set("Line", Line); Set("Line2", Line2);
            Set("Acc", Acc); Set("AccHover", AccHover); Set("AccDown", AccDown); Set("AccText", AccText);
            Set("AccSoft", AccSoft); Set("AccDeep", AccDeep); Set("Good", Good); Set("Ok", Ok);
            Set("Warn", Warn); Set("WarnSoft", WarnSoft); Set("Bad", Bad); Set("BadSoft", BadSoft);
        }
    }
}
