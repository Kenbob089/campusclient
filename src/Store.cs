using System;
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

        static string FilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CampusMidnight");
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            return Path.Combine(dir, "settings.txt");
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
                    "stay=" + (Stay ? "1" : "0")
                };
                File.WriteAllLines(FilePath(), lines, Encoding.UTF8);
            }
            catch (Exception) { }
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
