using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NoctuaSetup
{
    // All the install / uninstall work. Everything is per-user (no admin).
    //
    //   %LOCALAPPDATA%\Programs\Noctua\
    //       App\Noctua.exe, App\Uninstall.exe      (replaced on every update)
    //       Data\...                               (settings, never touched by updates)
    //       installed.marker                       (tells Noctua to keep its data in Data\)
    static class Inst
    {
        static Inst()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        // this exe's own path (Environment.ProcessPath does not exist on .NET Framework)
        public static string Self
        {
            get { try { return System.Reflection.Assembly.GetEntryAssembly().Location; } catch { return null; } }
        }

        public const string BaseUrl = "https://github.com/Kenbob089/campusclient/releases/download/wpf-latest/";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Noctua";
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Root => Path.Combine(Local, "Programs", "Noctua");
        public static string AppDir => Path.Combine(Root, "App");
        public static string Exe => Path.Combine(AppDir, "Noctua.exe");
        public static string Uninstaller => Path.Combine(AppDir, "Uninstall.exe");
        public static string DataDir => Path.Combine(Root, "Data");
        public static string Marker => Path.Combine(Root, "installed.marker");
        public static string LegacyExe => Path.Combine(Root, "Noctua.exe");           // first installer put the exe straight in Root
        public static string LegacyUninstaller => Path.Combine(Root, "Uninstall.exe");
        public static string RoamingDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Noctua");
        public static string StartLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Noctua.lnk");
        public static string DeskLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Noctua.lnk");

        public static string InstalledExe => File.Exists(Exe) ? Exe : (File.Exists(LegacyExe) ? LegacyExe : null);
        public static bool IsLegacyLayout => !File.Exists(Exe) && File.Exists(LegacyExe);

        // ---- versions
        public static Version Norm(Version v)
        {
            return v == null ? null : new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        public static string Show(Version v)
        {
            if (v == null) return "";
            return v.Revision > 0 ? v.ToString(4) : v.ToString(3);
        }

        public static Version FileVer(string path)
        {
            try
            {
                string s = FileVersionInfo.GetVersionInfo(path).FileVersion;
                Version v;
                if (!string.IsNullOrEmpty(s) && Version.TryParse(s, out v)) return Norm(v);
            }
            catch { }
            return null;
        }

        public static async Task<Version> GetLatest()
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("NoctuaSetup/2.0");
                    http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    string s = (await http.GetStringAsync(BaseUrl + "version.txt")).Trim();
                    Version v;
                    if (Version.TryParse(s, out v)) return Norm(v);
                }
            }
            catch { }
            return null;
        }

        // null = fine, otherwise the reason it is not a usable Noctua.exe
        public static string CheckExe(string path)
        {
            try
            {
                if (!File.Exists(path)) return "That file doesn't exist.";
                if (new FileInfo(path).Length < 1000000) return "That file is too small to be Noctua.";
                var fi = FileVersionInfo.GetVersionInfo(path);
                bool named = Path.GetFileName(path).StartsWith("Noctua", StringComparison.OrdinalIgnoreCase);
                bool product = (fi.ProductName ?? "").IndexOf("Noctua", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!named && !product) return "That doesn't look like Noctua.exe.";
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static async Task<string> GetChangelog()
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("NoctuaSetup/2.1");
                    http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    return await http.GetStringAsync(BaseUrl + "changelog.txt");
                }
            }
            catch { return null; }
        }

        // a Noctua.exe sitting next to the installer, if any
        public static string BesideInstaller()
        {
            try
            {
                string me = Self;
                string d = me == null ? null : Path.GetDirectoryName(me);
                if (d == null) return null;
                string f = Path.Combine(d, "Noctua.exe");
                if (File.Exists(f) && !SamePath(f, Exe) && CheckExe(f) == null) return f;
            }
            catch { }
            return null;
        }

        static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }

        // ---- the running app
        static bool Ours(Process p)
        {
            try
            {
                string path = p.MainModule.FileName;
                return SamePath(path, Exe) || SamePath(path, LegacyExe) || path.StartsWith(Exe + ".", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static bool IsRunning()
        {
            foreach (var p in Process.GetProcessesByName("Noctua"))
            {
                bool ours = Ours(p);
                p.Dispose();
                if (ours) return true;
            }
            return false;
        }

        public static void CloseRunning()
        {
            foreach (var p in Process.GetProcessesByName("Noctua"))
            {
                try
                {
                    if (!Ours(p)) continue;
                    p.CloseMainWindow();
                    if (!p.WaitForExit(3000)) { p.Kill(); p.WaitForExit(4000); }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        // ---- file work
        public static async Task Download(string tmp, Action<double, string> report)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NoctuaSetup/2.0");
                using (var r = await http.GetAsync(BaseUrl + "Noctua.exe", HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!r.IsSuccessStatusCode)
                        throw new Exception("GitHub answered " + (int)r.StatusCode + " (" + r.ReasonPhrase + "). The release may not exist yet or the repository may be private. You can also install from a file you already have.");
                    long total = r.Content.Headers.ContentLength ?? -1;
                    using (var s = await r.Content.ReadAsStreamAsync())
                    using (var f = File.Create(tmp))
                    {
                        var buf = new byte[81920]; long got = 0; int n; int tick = 0;
                        while ((n = await s.ReadAsync(buf, 0, buf.Length)) > 0)
                        {
                            await f.WriteAsync(buf, 0, n); got += n;
                            if ((++tick & 15) == 0 || got == total)
                                report(total > 0 ? (double)got / total : 0, (got / 1048576) + (total > 0 ? " of " + (total / 1048576) : "") + " MB");
                        }
                    }
                }
            }
            if (new FileInfo(tmp).Length < 1000000) throw new Exception("The download looks incomplete.");
        }

        static void CopyFile(string from, string to, Action<double> report)
        {
            using (var s = File.OpenRead(from))
            using (var d = File.Create(to))
            {
                var buf = new byte[262144]; long got = 0, total = s.Length; int n; int tick = 0;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    d.Write(buf, 0, n); got += n;
                    if ((++tick & 7) == 0) report(total > 0 ? (double)got / total : 0);
                }
            }
        }

        static void MoveOver(string from, string to)
        {
            for (int i = 0; ; i++)
            {
                try { if (File.Exists(to)) File.Delete(to); File.Move(from, to); return; }
                catch (IOException) when (i < 15) { System.Threading.Thread.Sleep(300); }
                catch (UnauthorizedAccessException) when (i < 15) { System.Threading.Thread.Sleep(300); }
            }
        }

        // report(fraction 0..1, headline, detail). Runs on a worker thread.
        public static Task Install(string localSource, bool desktop, bool startMenu, bool runAtLogin, Action<double, string, string> report)
        {
            return Task.Run(async delegate
            {
                report(0.02, "Getting ready…", "");
                if (IsRunning())
                {
                    report(0.03, "Closing Noctua…", "Noctua is open, so setup is closing it before it updates.");
                    CloseRunning();
                }
                Directory.CreateDirectory(AppDir);

                string tmp = Exe + ".download";
                if (localSource != null)
                    await Task.Run(() => CopyFile(localSource, tmp, f => report(0.08 + 0.8 * f, "Copying Noctua…", "From " + localSource)));
                else
                    await Download(tmp, (f, t) => report(0.08 + 0.8 * f, "Downloading Noctua…", t));

                report(0.9, "Installing…", "");
                // the first installer kept everything straight in the folder: tidy that up
                foreach (string old in new[] { LegacyExe, LegacyUninstaller }) { try { if (File.Exists(old)) File.Delete(old); } catch { } }
                try { foreach (string o in Directory.GetFiles(AppDir, "*.old")) { try { File.Delete(o); } catch { } } } catch { }
                MoveOver(tmp, Exe);
                File.WriteAllText(Marker, "Noctua per-user install. Noctua keeps its settings in the Data folder next to App.\r\n");

                // keep a copy of this installer as the uninstaller
                string me = Self;
                if (me != null && !SamePath(me, Uninstaller))
                {
                    try { File.Copy(me, Uninstaller, true); } catch { }
                }

                report(0.95, "Adding shortcuts…", "");
                if (startMenu) Link(StartLink, Exe, AppDir, "Noctua"); else { try { File.Delete(StartLink); } catch { } }
                if (desktop) Link(DeskLink, Exe, AppDir, "Noctua"); else { try { File.Delete(DeskLink); } catch { } }

                using (var rk = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (runAtLogin) rk.SetValue("Noctua", "\"" + Exe + "\""); else rk.DeleteValue("Noctua", false);
                }

                string ver = Show(FileVer(Exe));
                using (var k = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    k.SetValue("DisplayName", "Noctua");
                    k.SetValue("DisplayVersion", ver.Length > 0 ? ver : "1.0");
                    k.SetValue("Publisher", "Noctua");
                    k.SetValue("InstallLocation", Root);
                    k.SetValue("DisplayIcon", Exe + ",0");
                    k.SetValue("UninstallString", "\"" + Uninstaller + "\" /uninstall");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord);
                }
                report(1, "Done", "");
            });
        }

        // returns true when the app folder still has to be removed after this process exits
        public static Task Uninstall(bool deleteData, Action<double, string, string> report)
        {
            return Task.Run(delegate
            {
                report(0.1, "Closing Noctua…", "");
                CloseRunning();
                report(0.3, "Removing shortcuts…", "");
                try { File.Delete(StartLink); } catch { }
                try { File.Delete(DeskLink); } catch { }
                try { using (var rk = Registry.CurrentUser.OpenSubKey(RunKey, true)) { if (rk != null) rk.DeleteValue("Noctua", false); } } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); } catch { }
                report(0.6, "Removing Noctua…", "");
                try { if (File.Exists(Exe)) File.Delete(Exe); } catch { }
                try { if (File.Exists(LegacyExe)) File.Delete(LegacyExe); } catch { }
                if (deleteData)
                {
                    report(0.8, "Deleting your data…", "");
                    try { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, true); } catch { }
                }
                report(1, "Done", "");
            });
        }

        // The uninstaller runs from the folder it removes, so that folder goes after it exits.
        public static void RemoveFolderAfterExit(bool deletedData)
        {
            try
            {
                string cmd = "ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + AppDir + "\"" +
                             " & del /q \"" + LegacyUninstaller + "\" & del /q \"" + Marker + "\"" +
                             " & rmdir \"" + Root + "\"";
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
                Process.Start(psi);
            }
            catch { }
        }

        // off the UI thread: the first start of a fresh exe can take a moment (Windows scans it)
        public static Task LaunchAsync()
        {
            return Task.Run(delegate
            {
                try
                {
                    string exe = InstalledExe;
                    if (exe == null) return;
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
                }
                catch { }
            });
        }

        static void Link(string path, string target, string wd, string desc)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic sh = Activator.CreateInstance(t);
            dynamic l = sh.CreateShortcut(path);
            l.TargetPath = target;
            l.WorkingDirectory = wd;
            l.Description = desc;
            l.IconLocation = target + ",0";
            l.Save();
        }
    }
}
