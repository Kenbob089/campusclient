using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NoctuaSetup
{
    // All the install / uninstall work. Everything is per-user (no admin).
    static class Inst
    {
        // The app is published to this release by .github/workflows/build-wpf.yml
        public const string ExeUrl = "https://github.com/Kenbob089/campusclient/releases/download/wpf-latest/Noctua.exe";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Noctua";

        public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Noctua");
        public static string Exe => Path.Combine(Dir, "Noctua.exe");
        public static string Uninstaller => Path.Combine(Dir, "Uninstall.exe");
        public static string StartLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Noctua.lnk");
        public static string DeskLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Noctua.lnk");
        public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Noctua");

        public static void CloseRunning()
        {
            foreach (var p in Process.GetProcessesByName("Noctua"))
            {
                try
                {
                    string path = null;
                    try { path = p.MainModule.FileName; } catch { }
                    // only touch the installed copy (or one we cannot identify)
                    if (path != null && !string.Equals(path, Exe, StringComparison.OrdinalIgnoreCase)) continue;
                    p.CloseMainWindow();
                    if (!p.WaitForExit(3000)) { p.Kill(); p.WaitForExit(3000); }
                }
                catch { }
            }
        }

        // Where the exe comes from: a Noctua.exe beside the installer (offline use) or GitHub.
        public static string LocalCopy()
        {
            try
            {
                string me = Environment.ProcessPath;
                string d = me == null ? null : Path.GetDirectoryName(me);
                if (d == null) return null;
                string f = Path.Combine(d, "Noctua.exe");
                if (File.Exists(f) && !string.Equals(Path.GetFullPath(f), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch { }
            return null;
        }

        public static async Task Download(string tmp, Action<double, string> report)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NoctuaSetup/1.0");
                using (var r = await http.GetAsync(ExeUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!r.IsSuccessStatusCode)
                        throw new Exception("GitHub answered " + (int)r.StatusCode + " (" + r.ReasonPhrase + "). The release may not exist yet or the repository may be private.");
                    long total = r.Content.Headers.ContentLength ?? -1;
                    using (var s = await r.Content.ReadAsStreamAsync())
                    using (var f = File.Create(tmp))
                    {
                        var buf = new byte[81920]; long got = 0; int n; int tick = 0;
                        while ((n = await s.ReadAsync(buf, 0, buf.Length)) > 0)
                        {
                            await f.WriteAsync(buf, 0, n); got += n;
                            if ((++tick & 7) == 0 || got == total)
                                report(total > 0 ? (double)got / total : 0, "Downloading Noctua…  " + (got / 1048576) + (total > 0 ? " / " + (total / 1048576) : "") + " MB");
                        }
                    }
                }
            }
            if (new FileInfo(tmp).Length < 1000000) throw new Exception("The download looks incomplete.");
        }

        public static async Task Install(bool desktop, Action<double, string> report)
        {
            report(0, "Preparing…");
            await Task.Run(() => CloseRunning());
            Directory.CreateDirectory(Dir);

            string tmp = Path.Combine(Dir, "Noctua.exe.download");
            string local = LocalCopy();
            if (local != null) { report(0.5, "Copying Noctua…"); File.Copy(local, tmp, true); }
            else await Download(tmp, report);

            report(1, "Installing…");
            File.Move(tmp, Exe, true);

            // keep a copy of this installer as the uninstaller
            string me = Environment.ProcessPath;
            if (me != null && !string.Equals(Path.GetFullPath(me), Path.GetFullPath(Uninstaller), StringComparison.OrdinalIgnoreCase))
                File.Copy(me, Uninstaller, true);

            Link(StartLink, Exe, Dir, "Noctua");
            if (desktop) Link(DeskLink, Exe, Dir, "Noctua");

            string ver = "1.0";
            try { ver = FileVersionInfo.GetVersionInfo(Exe).FileVersion ?? ver; } catch { }
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                k.SetValue("DisplayName", "Noctua");
                k.SetValue("DisplayVersion", ver);
                k.SetValue("Publisher", "Noctua");
                k.SetValue("InstallLocation", Dir);
                k.SetValue("DisplayIcon", Exe + ",0");
                k.SetValue("UninstallString", "\"" + Uninstaller + "\" /uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord);
            }
        }

        public static void Uninstall(bool deleteData)
        {
            CloseRunning();
            try { File.Delete(StartLink); } catch { }
            try { File.Delete(DeskLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); } catch { }
            try { if (File.Exists(Exe)) File.Delete(Exe); } catch { }
            if (deleteData) { try { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, true); } catch { } }
        }

        // The uninstaller runs from the folder it removes, so the folder goes after it exits.
        public static void RemoveFolderAfterExit()
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + Dir + "\"")
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
                Process.Start(psi);
            }
            catch { }
        }

        public static void Launch()
        {
            try { Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true, WorkingDirectory = Dir }); } catch { }
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
