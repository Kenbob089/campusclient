using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CampusMidnight
{
    // A small rolling log in %APPDATA%\Noctua\log.txt. Never records passwords, grades or message text.
    static class Log
    {
        static readonly object gate = new object();

        public static string PathOf() { return Disk.At("log.txt"); }

        public static void Write(string msg)
        {
            try
            {
                lock (gate)
                {
                    string p = PathOf();
                    try
                    {
                        FileInfo fi = new FileInfo(p);
                        if (fi.Exists && fi.Length > 400000)
                        {
                            string[] keep = File.ReadAllLines(p, Encoding.UTF8);
                            int from = Math.Max(0, keep.Length - 800);
                            List<string> tail = new List<string>();
                            for (int i = from; i < keep.Length; i++) tail.Add(keep[i]);
                            File.WriteAllLines(p, tail.ToArray(), Encoding.UTF8);
                        }
                    }
                    catch (Exception) { }
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception) { }
        }

        public static void Error(string where, Exception ex)
        {
            Write("ERROR in " + where + ": " + ex.GetType().Name + ": " + ex.Message);
        }

        public static string[] Tail(int n)
        {
            try
            {
                lock (gate)
                {
                    if (!File.Exists(PathOf())) return new string[0];
                    string[] all = File.ReadAllLines(PathOf(), Encoding.UTF8);
                    int from = Math.Max(0, all.Length - n);
                    string[] r = new string[all.Length - from];
                    Array.Copy(all, from, r, 0, r.Length);
                    return r;
                }
            }
            catch (Exception) { return new string[0]; }
        }
    }
}
