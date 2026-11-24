using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        DispatcherTimer updTimer, updIdle;
        LiveHost updHost;
        string fsUpdTold = "";

        // called once from the constructor
        void InitUpdates()
        {
            Updater.Cleanup();
            Updater.Changed += OnUpdChanged;

            Loaded += delegate
            {
                if (Updater.JustUpdated)
                {
                    DispatcherTimer d = new DispatcherTimer();
                    d.Interval = TimeSpan.FromSeconds(2);
                    d.Tick += delegate
                    {
                        d.Stop();
                        Notify("Update complete", "Noctua is now version " + Updater.CurrentText + ".", "");
                    };
                    d.Start();
                }
                DispatcherTimer first = new DispatcherTimer();
                first.Interval = TimeSpan.FromSeconds(8);
                first.Tick += delegate { first.Stop(); CheckUpdates(); };
                first.Start();
            };

            updTimer = new DispatcherTimer();
            updTimer.Interval = TimeSpan.FromMinutes(10);
            updTimer.Tick += delegate { CheckUpdates(); };
            updTimer.Start();

            updIdle = new DispatcherTimer();
            updIdle.Interval = TimeSpan.FromSeconds(20);
            updIdle.Tick += delegate { TryAutoApply(); };
            updIdle.Start();
        }

        void OnUpdChanged()
        {
            if (updHost == null) return;
            try { updHost.Refresh(); } catch (Exception) { }
        }

        // every 10 minutes, and when "Check now" is pressed
        async void CheckUpdates()
        {
            try
            {
                await Updater.Check();
                if (!Updater.Available) return;
                if (cfg.UpdNotified != Updater.Latest)
                {
                    cfg.UpdNotified = Updater.Latest;
                    cfg.Save();
                    Notify("Update available", "Noctua " + Updater.Latest + " is available. " +
                        (cfg.AutoUpdate ? "It will update automatically once you're not using it, then open again." : "Open Settings to update."), "settings");
                }
                if (cfg.AutoUpdate && !Updater.Ready && !Updater.Downloading) await Updater.Download();
            }
            catch (Exception ex) { Log.Error("updates", ex); }
        }

        // the person is away (or Noctua is hidden in the tray): swap in the new version and re-open
        void TryAutoApply()
        {
            try
            {
                if (!cfg.AutoUpdate || !Updater.Ready || !Updater.Available || Updater.Downloading) return;
                bool inUse = IsVisible && WindowState != WindowState.Minimized && IsActive;
                if (Updater.IdleSeconds() < (inUse ? 180 : 60)) return;
                if (fsOn)
                {
                    // full screen Now mode is left running on purpose: tell the person instead of restarting it
                    if (fsUpdTold != Updater.Latest)
                    {
                        fsUpdTold = Updater.Latest;
                        Notify("Update ready", "Noctua " + Updater.Latest + " is ready, but you're in full screen so it wasn't installed. Leave full screen and it will update once you're inactive, or use Settings > Updates.", "settings");
                    }
                    return;
                }
                ApplyUpdateNow();
            }
            catch (Exception ex) { Log.Error("auto update", ex); }
        }

        void ApplyUpdateNow()
        {
            bool hidden = !IsVisible && cfg.TrayOnClose && !cfg.NoTray;
            try { cfg.Save(); } catch (Exception) { }
            if (!Updater.Apply(hidden)) return;
            quitting = true;
            Close();
        }

        async void UpdateNowClicked()
        {
            try
            {
                if (!Updater.Ready) await Updater.Download();
                if (Updater.Ready) ApplyUpdateNow();
            }
            catch (Exception ex) { Log.Error("update now", ex); }
        }

        void OpenChangelog()
        {
            StackPanel p = new StackPanel();
            foreach (string raw in Updater.ChangelogText().Replace("\r", "").Split('\n'))
            {
                string l = raw.TrimEnd();
                if (l.Length == 0) continue;
                if (l.StartsWith("- ")) p.Children.Add(UI.Margin(UI.Wrap("\u2022  " + l.Substring(2), 13, "T2"), 8, 3, 0, 0));
                else p.Children.Add(UI.Margin(UI.Text(l, 15, "T1", true), 0, 16, 0, 2));
            }
            NewWindow("What's new in Noctua", p, 620, 640).Show();
        }

        UIElement UpdatesSection()
        {
            updHost = Live(delegate
            {
                StackPanel p = new StackPanel();
                p.Children.Add(UI.Text("Noctua " + Updater.CurrentText + (Disk.Installed ? "  ·  installed" : "  ·  portable"), 14, "T1", true));

                string status;
                if (Updater.Downloading) status = "Downloading version " + Updater.Latest + "…  " + Math.Round(Updater.Progress * 100) + "%";
                else if (Updater.Checking) status = "Checking for updates…";
                else if (Updater.Ready) status = "Version " + Updater.Latest + " is ready. Noctua restarts to finish.";
                else if (Updater.Available) status = "Version " + Updater.Latest + " is available.";
                else if (Updater.LastCheck != DateTime.MinValue) status = "You're up to date.";
                else status = "Not checked yet.";
                p.Children.Add(UI.Margin(UI.Text(status, 13.5, Updater.Available ? "AccText" : "T2", false), 0, 4, 0, 0));
                if (Updater.Error.Length > 0) p.Children.Add(UI.Margin(UI.Wrap(Updater.Error, 12.5, "Bad"), 0, 4, 0, 0));
                if (Updater.LastCheck != DateTime.MinValue)
                    p.Children.Add(UI.Margin(UI.Text("Last checked " + Updater.LastCheck.ToString("h:mm tt") + ". Noctua checks every 10 minutes.", 12, "T3", false), 0, 4, 0, 0));
                else
                    p.Children.Add(UI.Margin(UI.Text("Noctua checks every 10 minutes.", 12, "T3", false), 0, 4, 0, 0));

                Button check = UI.Btn("Check now", "Ghost", delegate { CheckUpdates(); });
                check.IsEnabled = !Updater.Checking && !Updater.Downloading;
                check.Margin = new Thickness(0, 0, 8, 0);
                StackPanel row = UI.Row(check);
                if (Updater.Available)
                {
                    Button now = UI.Btn("Update now", "Pill", delegate { UpdateNowClicked(); });
                    now.IsEnabled = !Updater.Downloading;
                    row.Children.Add(now);
                }
                Button wn = UI.Btn("What's new", "Ghost", delegate { OpenChangelog(); });
                wn.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(wn);
                p.Children.Add(UI.Margin(row, 0, 12, 0, 6));
                p.Children.Add(Toggle("Update automatically when I'm not using Noctua", cfg.AutoUpdate, delegate(bool v)
                {
                    cfg.AutoUpdate = v;
                    if (v && Updater.Available && !Updater.Ready && !Updater.Downloading) Updater.Download();
                }));
                return p;
            });
            return updHost;
        }
    }
}
