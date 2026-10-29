using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CampusMidnight
{
    // Remembers the sign-in link and username. The password is saved only if the user ticks
    // "Stay signed in", and then only encrypted with Windows DPAPI for this Windows account.
    class Settings
    {
        public string Url = "";
        public string User = "";
        public string EncPass = "";
        public bool Stay;
        public string Theme = "Midnight";
        public int Interval = 15;       // auto-refresh minutes, 0 = off
        public string Bells = "";       // one "period start-end" per line, joined with ';'
        public double Goal = 90;

        static string FilePath()
        {
            return Disk.At("settings.txt");
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string p = FilePath();
                if (!File.Exists(p)) return s;
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i);
                    string v = line.Substring(i + 1);
                    if (k == "url") s.Url = v;
                    else if (k == "user") s.User = v;
                    else if (k == "pass") s.EncPass = v;
                    else if (k == "stay") s.Stay = v == "1";
                    else if (k == "theme" && v.Length > 0) s.Theme = v;
                    else if (k == "interval") { int iv; if (int.TryParse(v, out iv) && iv >= 0 && iv <= 240) s.Interval = iv; }
                    else if (k == "bells") s.Bells = v;
                    else if (k == "goal") { double gv; if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gv) && gv > 0 && gv <= 100) s.Goal = gv; }
                }
            }
            catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try
            {
                string[] lines = new string[]
                {
                    "url=" + Url,
                    "user=" + User,
                    "pass=" + (Stay ? EncPass : ""),
                    "stay=" + (Stay ? "1" : "0"),
                    "theme=" + Theme,
                    "interval=" + Interval,
                    "bells=" + Bells.Replace("\r", "").Replace("\n", ";"),
                    "goal=" + Goal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                };
                File.WriteAllLines(FilePath(), lines, Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }

    // everything the app keeps lives in %APPDATA%\CampusMidnight
    static class Disk
    {
        public static string Dir()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CampusMidnight");
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            return dir;
        }

        public static string At(string name)
        {
            return Path.Combine(Dir(), name);
        }

        public static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "file";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Length > 80 ? s.Substring(0, 80) : s;
        }
    }

    // class periods and their times, typed by the student (Campus does not expose bell times)
    class Bell
    {
        public string Key;
        public TimeSpan Start, End;

        public static string KeyOf(string period)
        {
            if (string.IsNullOrEmpty(period)) return "";
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(period, @"\d+");
            if (m.Success) return int.Parse(m.Value).ToString();
            return Norm.Key(period);
        }

        static bool ParseTime(string t, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(t.Trim().ToLowerInvariant(), @"^(\d{1,2})(?::(\d{2}))?\s*([ap])?m?$");
            if (!m.Success) return false;
            int h = int.Parse(m.Groups[1].Value);
            int min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (m.Groups[3].Success)
            {
                bool pm = m.Groups[3].Value == "p";
                if (h == 12) h = pm ? 12 : 0; else if (pm) h += 12;
            }
            else if (h >= 1 && h <= 6) h += 12; // "1:05" with no am/pm means the afternoon
            if (h > 23 || min > 59) return false;
            ts = new TimeSpan(h, min, 0);
            return true;
        }

        public static List<Bell> Parse(string text)
        {
            List<Bell> list = new List<Bell>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string raw in text.Replace("\r", "").Split(new char[] { ';', '\n' }))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(line,
                    @"^(.+?)\s+(\d{1,2}(?::\d{2})?\s*[ap]?m?)\s*(?:-|\u2013|to)\s*(\d{1,2}(?::\d{2})?\s*[ap]?m?)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                TimeSpan a, b;
                if (!ParseTime(m.Groups[2].Value, out a) || !ParseTime(m.Groups[3].Value, out b)) continue;
                if (b <= a) b = b + TimeSpan.FromHours(12);
                Bell bl = new Bell();
                bl.Key = KeyOf(m.Groups[1].Value);
                bl.Start = a; bl.End = b;
                list.Add(bl);
            }
            return list;
        }

        public static Bell Find(List<Bell> list, string period)
        {
            string k = KeyOf(period);
            if (k.Length == 0) return null;
            foreach (Bell b in list) if (b.Key == k) return b;
            return null;
        }

        public string Text()
        {
            return Clock(Start) + " \u2013 " + Clock(End);
        }

        static string Clock(TimeSpan t)
        {
            return DateTime.Today.Add(t).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant().Replace(" am", "a").Replace(" pm", "p");
        }
    }

    static class Vault
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CampusMidnight.v1");

        public static string Protect(string plain)
        {
            try
            {
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch (Exception) { return ""; }
        }

        public static string Unprotect(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                byte[] raw = ProtectedData.Unprotect(Convert.FromBase64String(b64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch (Exception) { return null; }
        }
    }
}
