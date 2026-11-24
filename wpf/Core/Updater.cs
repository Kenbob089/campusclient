using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CampusMidnight
{
    // Looks for a newer Noctua on the GitHub release, downloads it beside the running exe and swaps it in.
    // Windows lets a running exe be renamed, so the swap is: Noctua.exe -> Noctua.exe.<pid>.old, new file -> Noctua.exe,
    // start the new one (it waits for this process to exit), then close this one.
    static class Updater
    {
        public const string BaseUrl = "https://github.com/Kenbob089/campusclient/releases/download/wpf-latest/";

        public static bool JustUpdated;          // this run was started by an update
        public static bool StartHidden;          // ...and Noctua was in the tray before it

        public static Version CurrentV;
        public static Version LatestV;
        public static string Latest = "";        // as shown
        public static bool Checking, Downloading, Ready;
        public static double Progress;
        public static string Error = "";
        public static string Changelog = "";     // newest list from GitHub (empty = not fetched, use the built-in one)
        public static DateTime LastCheck = DateTime.MinValue;
        public static event Action Changed;

        static DateTime lastRaise = DateTime.MinValue;

        static Updater()
        {
            CurrentV = Norm(ReadVersion(Environment.ProcessPath));
            if (CurrentV == null) CurrentV = new Version(1, 0, 0, 0);
        }

        public static Version Norm(Version v)
        {
            if (v == null) return null;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        static Version ReadVersion(string path)
        {
            try
            {
                string s = FileVersionInfo.GetVersionInfo(path).FileVersion;
                Version v;
                if (!string.IsNullOrEmpty(s) && Version.TryParse(s, out v)) return v;
            }
            catch (Exception) { }
            return null;
        }

        public static string Show(Version v)
        {
            if (v == null) return "";
            return v.Revision > 0 ? v.ToString(4) : v.ToString(3);
        }

        public static string CurrentText { get { return Show(CurrentV); } }
        public static bool Available { get { return LatestV != null && LatestV > CurrentV; } }

        static void Raise(bool force)
        {
            if (!force && (DateTime.Now - lastRaise).TotalMilliseconds < 250) return;
            lastRaise = DateTime.Now;
            Action a = Changed;
            if (a != null) { try { a(); } catch (Exception) { } }
        }

        static HttpClient Http()
        {
            HttpClient h = new HttpClient();
            h.Timeout = TimeSpan.FromMinutes(15);
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Noctua/" + CurrentText);
            h.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return h;
        }

        // must be called on the UI thread so the events come back on it
        public static async Task Check()
        {
            if (Checking) return;
            Checking = true; Error = "";
            Raise(true);
            try
            {
                using (HttpClient h = Http())
                {
                    h.Timeout = TimeSpan.FromSeconds(15);
                    using (HttpResponseMessage r = await h.GetAsync(BaseUrl + "version.txt"))
                    {
                        if (!r.IsSuccessStatusCode)
                            throw new Exception("GitHub answered " + (int)r.StatusCode + (r.StatusCode == System.Net.HttpStatusCode.NotFound ? " (the wpf-latest release has no version.txt yet: run the Noctua build once)" : ""));
                        string s = (await r.Content.ReadAsStringAsync()).Trim();
                        Version v;
                        if (!Version.TryParse(s, out v)) throw new Exception("version.txt is not a version number");
                        LatestV = Norm(v);
                        Latest = Show(LatestV);
                        LastCheck = DateTime.Now;
                    }
                    try
                    {
                        string c = await h.GetStringAsync(BaseUrl + "changelog.txt");
                        if (c.Trim().Length > 0) Changelog = c;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception ex)
            {
                Error = "Couldn't check for updates: " + ex.Message;
                Log.Write("update check failed: " + ex.Message);
            }
            Checking = false;
            Raise(true);
        }

        public static async Task Download()
        {
            if (Downloading || Ready || !Available) return;
            Downloading = true; Progress = 0; Error = "";
            Raise(true);
            string exe = Environment.ProcessPath;
            string tmp = exe + ".new";
            try
            {
                using (HttpClient h = Http())
                using (HttpResponseMessage r = await h.GetAsync(BaseUrl + "Noctua.exe", HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!r.IsSuccessStatusCode) throw new Exception("GitHub answered " + (int)r.StatusCode);
                    long total = r.Content.Headers.ContentLength ?? -1;
                    using (Stream s = await r.Content.ReadAsStreamAsync())
                    using (FileStream f = File.Create(tmp))
                    {
                        byte[] buf = new byte[81920]; long got = 0; int n;
                        while ((n = await s.ReadAsync(buf, 0, buf.Length)) > 0)
                        {
                            await f.WriteAsync(buf, 0, n); got += n;
                            if (total > 0) { Progress = (double)got / total; Raise(false); }
                        }
                    }
                }
                if (new FileInfo(tmp).Length < 1000000) throw new Exception("the download looks incomplete");
                Ready = true;
            }
            catch (Exception ex)
            {
                Error = "The update could not be downloaded. " + (ex is UnauthorizedAccessException ? "Noctua can't write to its own folder." : ex.Message);
                Log.Write("update download failed: " + ex.Message);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
            }
            Downloading = false;
            Raise(true);
        }

        // swaps the files and starts the new copy. The caller closes this one afterwards.
        public static bool Apply(bool hidden)
        {
            string exe = Environment.ProcessPath;
            string nw = exe + ".new";
            string old = exe + "." + Process.GetCurrentProcess().Id + ".old";
            if (!Ready || !File.Exists(nw)) return false;
            bool moved = false;
            try
            {
                File.Move(exe, old);
                moved = true;
                File.Move(nw, exe);
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.UseShellExecute = false;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                psi.Arguments = "--updated --wait " + Process.GetCurrentProcess().Id + (hidden ? " --hidden" : "");
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Error = "The update could not be installed: " + ex.Message;
                Log.Write("update apply failed: " + ex.Message);
                try { if (moved && !File.Exists(exe) && File.Exists(old)) File.Move(old, exe); } catch (Exception) { }
                Raise(true);
                return false;
            }
        }

        // leftovers from a finished update
        public static void Cleanup()
        {
            Task.Run(delegate
            {
                try
                {
                    System.Threading.Thread.Sleep(6000);
                    string exe = Environment.ProcessPath;
                    string dir = Path.GetDirectoryName(exe);
                    foreach (string f in Directory.GetFiles(dir, Path.GetFileName(exe) + ".*.old"))
                    {
                        try { File.Delete(f); } catch (Exception) { }
                    }
                }
                catch (Exception) { }
            });
        }

        // the list shown by "What's new": GitHub's if we have it, otherwise the one built into this version
        public static string ChangelogText()
        {
            if (Changelog.Length > 0) return Changelog;
            try
            {
                using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("changelog.txt"))
                {
                    if (s != null) using (StreamReader r = new StreamReader(s)) return r.ReadToEnd();
                }
            }
            catch (Exception) { }
            return "No change list is available.";
        }

        // ---- is the person away? (no keyboard or mouse for this long, anywhere in Windows)
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO p);

        public static double IdleSeconds()
        {
            try
            {
                LASTINPUTINFO i = new LASTINPUTINFO();
                i.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref i)) return 0;
                uint now = unchecked((uint)Environment.TickCount);
                return unchecked(now - i.dwTime) / 1000.0;
            }
            catch (Exception) { return 0; }
        }
    }
}
