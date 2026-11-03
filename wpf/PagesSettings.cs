using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CampusMidnight
{
    // Settings and Diagnostics, laid out like the WinForms app
    sealed partial class MainWindow
    {
        // a region of a page that rebuilds itself without touching the rest of the page
        sealed class LiveHost : ContentControl
        {
            public Func<UIElement> Build;
            public void Refresh() { Content = Build(); }
        }

        static LiveHost Live(Func<UIElement> build)
        {
            LiveHost h = new LiveHost();
            h.Build = build;
            h.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            h.Refresh();
            return h;
        }

        LiveHost themeHost, navHost, picHost;

        CheckBox Toggle(string text, bool on, Action<bool> set)
        {
            CheckBox c = new CheckBox();
            c.Content = text;
            c.IsChecked = on;
            c.Margin = new Thickness(0, 6, 0, 6);
            c.Click += delegate { set(c.IsChecked == true); cfg.Save(); };
            return c;
        }

        UIElement Section(string title, params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading(title));
            foreach (UIElement k in kids) p.Children.Add(k);
            return UI.Card(p, 20);
        }

        static UIElement Gap(double h)
        {
            Border b = new Border();
            b.Height = h;
            return b;
        }

        UIElement SwatchCard(ThemeDef d)
        {
            Color bg, surface, accent, text;
            Theme.Preview(d, out bg, out surface, out accent, out text);
            bool sel = string.Equals(d.Name, Theme.Current, StringComparison.OrdinalIgnoreCase);

            Grid prev = new Grid();
            prev.Height = 78;
            Border pb = new Border();
            if (d.Gradient)
            {
                Color bgEnd, acc2;
                Theme.PreviewEnds(d, out bgEnd, out acc2);
                pb.Background = new LinearGradientBrush(bg, bgEnd, new Point(0, 0), new Point(1, 1));
            }
            else pb.Background = new SolidColorBrush(bg);
            pb.CornerRadius = new CornerRadius(10);
            Grid inner = UI.Cols(0, 1);
            inner.Margin = new Thickness(10);
            Border sf = new Border();
            sf.Width = 46;
            sf.Background = new SolidColorBrush(surface);
            sf.CornerRadius = new CornerRadius(6);
            sf.BorderBrush = new SolidColorBrush(Theme.Mix(bg, text, 0.14));
            sf.BorderThickness = new Thickness(1);
            UI.At(inner, sf, 0);
            StackPanel lines = new StackPanel();
            lines.Margin = new Thickness(10, 4, 0, 0);
            Border l1 = new Border(); l1.Height = 8; l1.Width = 70; l1.CornerRadius = new CornerRadius(4); l1.HorizontalAlignment = HorizontalAlignment.Left;
            if (d.Gradient)
            {
                Color bgEnd2, acc22;
                Theme.PreviewEnds(d, out bgEnd2, out acc22);
                l1.Background = new LinearGradientBrush(accent, acc22, new Point(0, 0), new Point(1, 0));
            }
            else l1.Background = new SolidColorBrush(accent);
            Border l2 = new Border(); l2.Height = 6; l2.Width = 52; l2.CornerRadius = new CornerRadius(3); l2.HorizontalAlignment = HorizontalAlignment.Left;
            l2.Background = new SolidColorBrush(Theme.Mix(bg, text, 0.45));
            l2.Margin = new Thickness(0, 8, 0, 0);
            lines.Children.Add(l1); lines.Children.Add(l2);
            UI.At(inner, lines, 1);
            pb.Child = inner;
            prev.Children.Add(pb);

            StackPanel p = new StackPanel();
            p.Children.Add(prev);
            TextBlock nm = UI.Text(d.Name, 13.5, sel ? "AccText" : "T1", true);
            nm.Margin = new Thickness(2, 10, 0, 0);
            p.Children.Add(nm);

            Border card = UI.Card(p, 10);
            card.Width = 168;
            card.Margin = new Thickness(0, 0, 14, 14);
            if (sel) { card.SetResourceReference(Border.BorderBrushProperty, "Acc"); card.BorderThickness = new Thickness(2); }
            string name = d.Name;
            Hover(card, "Bg2", "Bg3", delegate { ChangeTheme(name); });
            UI.Lift(card);
            return card;
        }

        UIElement NavEditor()
        {
            List<string[]> all = NavItems(true);
            List<string> hidden = new List<string>(cfg.NavHide.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            StackPanel col = new StackPanel();
            for (int i = 0; i < all.Count; i++)
            {
                string[] d = all[i];
                int idx = i;
                bool locked = d[0] == "settings";
                Grid g = UI.Cols(0, 1, 0, 0, 0);
                g.Margin = new Thickness(0, 4, 0, 4);
                TextBlock ic = UI.Icon(d[2], 16, "T2");
                ic.Width = 34;
                UI.At(g, ic, 0);
                TextBlock nm = UI.Text(d[1], 14, "T1", false);
                nm.VerticalAlignment = VerticalAlignment.Center;
                UI.At(g, nm, 1);
                CheckBox vis = new CheckBox();
                vis.Content = "Show";
                vis.IsChecked = locked || !hidden.Contains(d[0]);
                vis.IsEnabled = !locked;
                vis.Margin = new Thickness(0, 0, 18, 0);
                vis.VerticalAlignment = VerticalAlignment.Center;
                string key = d[0];
                vis.Click += delegate
                {
                    if (vis.IsChecked == true) hidden.Remove(key); else if (!hidden.Contains(key)) hidden.Add(key);
                    SaveNav(all, hidden);
                };
                UI.At(g, vis, 2);
                Button up = UI.Btn("", "Ghost", delegate
                {
                    string[] t = all[idx]; all[idx] = all[idx - 1]; all[idx - 1] = t;
                    SaveNav(all, hidden);
                });
                up.FontFamily = (FontFamily)Application.Current.Resources["Icons"];
                up.Padding = new Thickness(12, 8, 12, 8);
                up.IsEnabled = i > 0;
                up.Margin = new Thickness(0, 0, 6, 0);
                UI.At(g, up, 3);
                Button dn = UI.Btn("", "Ghost", delegate
                {
                    string[] t = all[idx]; all[idx] = all[idx + 1]; all[idx + 1] = t;
                    SaveNav(all, hidden);
                });
                dn.FontFamily = (FontFamily)Application.Current.Resources["Icons"];
                dn.Padding = new Thickness(12, 8, 12, 8);
                dn.IsEnabled = i < all.Count - 1;
                UI.At(g, dn, 4);
                col.Children.Add(g);
            }
            Button reset = UI.Btn("Reset sidebar", "Ghost", delegate { cfg.NavOrder = ""; cfg.NavHide = ""; cfg.Save(); BuildNavItems(); if (navHost != null) navHost.Refresh(); });
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            reset.Margin = new Thickness(0, 10, 0, 0);
            col.Children.Add(reset);
            return UI.Card(col, 20);
        }

        // one picture setting in its own card: a preview, what it is using right now and the buttons for it
        UIElement PicCard(string title, string status, UIElement preview, params Button[] btns)
        {
            Grid g = UI.Cols(0, 1);
            FrameworkElement pv = (FrameworkElement)preview;
            pv.Margin = new Thickness(0, 0, 20, 0);
            pv.VerticalAlignment = VerticalAlignment.Top;
            UI.At(g, pv, 0);
            StackPanel info = new StackPanel();
            info.Children.Add(UI.Text(title, 15, "T1", true));
            info.Children.Add(UI.Margin(UI.Text(status, 12.5, "T2", false), 0, 2, 0, 12));
            WrapPanel row = new WrapPanel();
            foreach (Button b in btns) { b.Margin = new Thickness(0, 0, 8, 8); row.Children.Add(b); }
            info.Children.Add(row);
            UI.At(g, info, 1);
            Border card = UI.Card(g, 20);
            card.Margin = new Thickness(0, 0, 0, 14);
            return card;
        }

        UIElement PhotoPreview()
        {
            Border b = new Border();
            b.Width = 76; b.Height = 76;
            b.CornerRadius = new CornerRadius(38);
            if (photo != null) { ImageBrush ib = new ImageBrush(photo); ib.Stretch = Stretch.UniformToFill; b.Background = ib; }
            else
            {
                b.SetResourceReference(Border.BackgroundProperty, "AccSoft");
                TextBlock it = UI.Text(Rows.Initials(current != null ? current.Name : ""), 26, "AccText", true);
                it.HorizontalAlignment = HorizontalAlignment.Center; it.VerticalAlignment = VerticalAlignment.Center;
                b.Child = it;
            }
            return b;
        }

        UIElement PageSettings()
        {
            List<UIElement> kids = new List<UIElement>();

            kids.Add(PageKit.Section("Appearance"));
            string[] modeKeys = new string[] { "system", "light", "dark" };
            kids.Add(SuSeg(new string[] { "System", "Light", "Dark" }, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)), delegate(int k) { ChangeMode(modeKeys[k]); }));

            kids.Add(PageKit.Section("Accent theme"));
            themeHost = Live(delegate
            {
                StackPanel tp = new StackPanel();
                Button themesBtn = UI.Btn(cfg.ThemesOpen ? "Hide themes" : "Show all themes", "Ghost", delegate { cfg.ThemesOpen = !cfg.ThemesOpen; cfg.Save(); themeHost.Refresh(); });
                themesBtn.HorizontalAlignment = HorizontalAlignment.Left;
                themesBtn.Margin = new Thickness(0, 0, 0, 14);
                tp.Children.Add(themesBtn);
                if (cfg.ThemesOpen)
                {
                    WrapPanel sw = new WrapPanel();
                    foreach (ThemeDef d in Theme.All) sw.Children.Add(SwatchCard(d));
                    tp.Children.Add(sw);
                }
                else tp.Children.Add(UI.Text("Current accent: " + Theme.Current, 13.5, "T2", false));
                return tp;
            });
            kids.Add(themeHost);

            kids.Add(PageKit.Section("Grades"));
            kids.Add(Toggle("Count dropped classes in my GPA", cfg.CountDropped, delegate(bool v) { cfg.CountDropped = v; }));
            kids.Add(Toggle("Show a rounded number (92) instead of a letter grade (A-)", cfg.NumbersOnly, delegate(bool v) { cfg.NumbersOnly = v; Theme.NumbersOnly = v; }));

            kids.Add(PageKit.Section("Notifications"));
            kids.Add(Toggle("Notify me before a class starts", cfg.NotifyStart, delegate(bool v) { cfg.NotifyStart = v; cfg.Save(); ApplyTray(false); }));
            kids.Add(Toggle("Notify me before a class ends", cfg.NotifyEnd, delegate(bool v) { cfg.NotifyEnd = v; cfg.Save(); ApplyTray(false); }));
            kids.Add(Toggle("Keep running in the system tray when I close the window (so reminders keep working)", cfg.TrayOnClose, delegate(bool v) { cfg.TrayOnClose = v; cfg.Save(); ApplyTray(false); }));
            kids.Add(Toggle("Tell me when there is a new assignment or grade (checked every minute)", cfg.NotifyNew, delegate(bool v) { cfg.NotifyNew = v; }));
            kids.Add(UI.Margin(UI.Text("HOW EARLY", 11.5, "T3", true), 2, 10, 0, 8));
            int[] leads = new int[] { 0, 1, 2, 5, 10, 15 };
            kids.Add(SuSeg(new string[] { "On time", "1 min", "2 min", "5 min", "10 min", "15 min" }, Math.Max(0, Array.IndexOf(leads, cfg.NotifyLead)), delegate(int k) { cfg.NotifyLead = leads[k]; cfg.Save(); }));
            Button test = UI.Btn("Send a test notification", "Ghost", delegate { Toast("Noctua", "Notifications are working. You will see class reminders here."); });
            test.HorizontalAlignment = HorizontalAlignment.Left;
            test.Margin = new Thickness(0, 14, 0, 0);
            kids.Add(test);

            kids.Add(PageKit.Section("Sidebar"));
            kids.Add(UI.Margin(UI.Text("Reorder the buttons or switch them off. Settings is always available.", 13.5, "T2", false), 0, -4, 0, 12));
            navHost = Live(delegate { return NavEditor(); });
            kids.Add(navHost);

            kids.Add(PageKit.Section("Pictures"));
            picHost = Live(delegate
            {
                StackPanel pp = new StackPanel();
                bool ownPhoto = File.Exists(Disk.At("photo.png"));
                Button ph1 = UI.Btn("Upload photo", "Ghost", delegate { CropPicture("photo"); });
                Button ph2 = UI.Btn("Campus photo", "Ghost", delegate { RemoveCustom("photo.png", true); });
                Button ph3 = UI.Btn(cfg.HidePhoto ? "Show photo" : "Hide photo", "Ghost", delegate { cfg.HidePhoto = !cfg.HidePhoto; cfg.Save(); UpdateTop(); picHost.Refresh(); });
                pp.Children.Add(PicCard("Profile photo", ownPhoto ? "Using your own picture." : (photo != null ? "Using the photo from Campus." : "No photo yet."), PhotoPreview(), ph1, ph2, ph3));

                bool ownIcon = File.Exists(Disk.At("icon.png"));
                Button ic1 = UI.Btn("Upload", "Ghost", delegate { CropPicture("icon"); });
                Button ic2 = UI.Btn("Campus logo", "Ghost", delegate { UseCampusLogoAsIcon(); });
                Button ic3 = UI.Btn("Noctua logo", "Ghost", delegate { UseMoonAsIcon(); });
                pp.Children.Add(PicCard("App icon", ownIcon ? "Using your own icon in the taskbar and tray." : "Using the Noctua logo in the taskbar and tray.", AppMark(76), ic1, ic2, ic3));

                bool ownLogo = File.Exists(Disk.At("logo.png"));
                Button lg1 = UI.Btn("Upload", "Ghost", delegate { CropPicture("logo"); });
                Button lg2 = UI.Btn("Campus logo", "Ghost", delegate { RemoveCustom("logo.png", false); });
                Button lg3 = UI.Btn("Noctua logo", "Ghost", delegate { UseMoonAsLogo(); });
                pp.Children.Add(PicCard("School logo", ownLogo ? "Using your own logo in the sidebar." : (logo != null ? "Using the logo from your school's Campus site in the sidebar." : "No school logo found yet."), LogoMark(76), lg1, lg2, lg3));
                return pp;
            });
            kids.Add(picHost);

            kids.Add(PageKit.Section("Schedule and rotating days"));
            string rot = cfg.RotOn ? "Rotating days: " + cfg.RotLabels + "." : "Rotating days are off.";
            string sched = cfg.GridDays.Count == 0
                ? "No schedule yet. Set up your letter days and times once to see what is happening now, what is next and how long is left."
                : cfg.GridDays.Count + " letter day" + (cfg.GridDays.Count == 1 ? "" : "s") + " (" + string.Join(", ", cfg.GridDays.ToArray()) + "), " + cfg.GridStarts.Count + " rows.  " + rot;
            Button guide = UI.Btn("Schedule setup", "Pill", delegate { StartScheduleEdit(); });
            guide.HorizontalAlignment = HorizontalAlignment.Left;
            guide.Margin = new Thickness(0, 14, 0, 0);
            StackPanel sc = new StackPanel();
            sc.Children.Add(UI.Wrap(sched, 13.5, "T2"));
            sc.Children.Add(guide);
            kids.Add(UI.Card(sc, 20));

            kids.Add(PageKit.Section("Refresh"));
            int[] mins = new int[] { 5, 15, 30, 0 };
            kids.Add(SuSeg(new string[] { "Every 5 min", "Every 15 min", "Every 30 min", "Off" }, Math.Max(0, Array.IndexOf(mins, cfg.Interval)), delegate(int k) { cfg.Interval = mins[k]; cfg.Save(); ApplyInterval(); }));

            kids.Add(PageKit.Section("More"));
            Button d1 = UI.Btn("Diagnostics", "Ghost", delegate { Go("debug"); });
            Button d2 = UI.Btn("Open data folder", "Ghost", delegate { try { Process.Start(new ProcessStartInfo(Disk.Dir()) { UseShellExecute = true }); } catch (Exception) { } });
            Button d3 = UI.Btn("Sign out", "Ghost", delegate { SignOut(); });
            d1.Margin = new Thickness(0, 0, 8, 0); d2.Margin = new Thickness(0, 0, 8, 0);
            kids.Add(UI.Row(d1, d2, d3));

            TextBlock ver = UI.Text("Noctua  \u00B7  version " + AppVersion, 11, "T3", false);
            ver.Margin = new Thickness(2, 28, 0, 0);
            ver.Opacity = 0.8;
            kids.Add(ver);

            return Body(kids.ToArray());
        }

        public const int Updates = 17;
        public static readonly string AppVersion = "1.0." + Updates;

        // ------------------------------------------------------------ diagnostics
        string ReportText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Noctua diagnostics");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("F"));
            sb.AppendLine("Contains endpoint names, HTTP status and field names only. No grades, names or other values.");
            sb.AppendLine();
            if (snap != null)
            {
                foreach (DiagRow d in snap.Diag)
                {
                    sb.AppendLine(d.Key + "  " + d.Path + "  status=" + d.Status + "  ok=" + d.Ok + "  bytes=" + d.Bytes
                        + (d.Records > 0 ? "  records=" + d.Records : "")
                        + (d.Error != null ? "  error=" + d.Error : ""));
                    if (d.Shape != null) sb.AppendLine(d.Shape);
                    sb.AppendLine();
                }
                if (snap.Modules.Count > 0)
                {
                    List<string> mo = new List<string>();
                    foreach (KeyValuePair<string, bool> kv in snap.Modules) mo.Add(kv.Key + "=" + (kv.Value ? "on" : "off"));
                    mo.Sort();
                    sb.AppendLine("Portal modules (what the school switched on): " + string.Join(", ", mo.ToArray()));
                }
                sb.AppendLine("Address discovery: " + (snap.DiscoverNote.Length > 0 ? snap.DiscoverNote : "not needed"));
                sb.AppendLine("Calendar: " + snap.CalStats);
                foreach (string dl2 in ScheduleDebug()) sb.AppendLine(dl2);
                sb.AppendLine("Bell schedules found on the site: " + snap.AutoBells.Count + "; classes with period placements: " + snap.Roster.FindAll(delegate(RosterItem q) { return q.Places.Count > 0; }).Count);
                foreach (string dl in snap.DiscoverLog) sb.AppendLine("  discovery: " + dl);
                sb.AppendLine(photo != null ? "Photo: loaded" : "Photo: none");
                sb.AppendLine(logo != null ? "Logo: loaded" : "Logo: none");
                sb.AppendLine("Parsed: students=" + snap.Students.Count + " courses=" + snap.Courses.Count + " terms=" + snap.Terms.Count
                    + " assignments=" + snap.Assigns.Count + " roster=" + snap.Roster.Count + " attendance=" + snap.Attendance.Count
                    + " messages=" + snap.Messages.Count + " notices=" + snap.Notices.Count + " documents=" + snap.Docs.Count
                    + " dropped=" + snap.Courses.FindAll(delegate(Course c) { return c.Dropped; }).Count
                    + " calendarDays=" + snap.CalDays.Count);
                if (current != null)
                    sb.AppendLine("Profile: id=" + (current.Number.Length > 0 ? "yes" : "no") + " grade=" + (current.Grade.Length > 0 ? "yes" : "no")
                        + " school=" + (current.School.Length > 0 ? "yes" : "no"));
                if (api != null)
                {
                    string[] disc = api.DiscoveredPaths();
                    int shown = 0;
                    sb.AppendLine();
                    sb.AppendLine("Portal addresses that look relevant:");
                    foreach (string p in disc)
                    {
                        if (shown >= 60) break;
                        if (Regex.IsMatch(p, "messag|inbox|notice|announce|document|attend|absen|picture|photo|logo|assign|calendar|schedule", RegexOptions.IgnoreCase))
                        {
                            sb.AppendLine(p);
                            shown++;
                        }
                    }
                }
            }
            else sb.AppendLine("No data loaded yet.");
            sb.AppendLine();
            sb.AppendLine("Settings: mode=" + cfg.Mode + " theme=" + cfg.Theme + " rail=" + (cfg.RailCollapsed ? "collapsed" : "open") + " notify=" + cfg.NotifyStart + "/" + cfg.NotifyEnd + "/" + cfg.NotifyLead
                + " dropGpa=" + cfg.CountDropped + " numbers=" + cfg.NumbersOnly + " schedules=" + cfg.Schedules.Count + " periodMap=" + cfg.PeriodMap.Count
                + " dayScheds=" + cfg.DayScheds.Count + " rotation=" + cfg.RotOn + " setupDone=" + cfg.SetupDone);
            sb.AppendLine();
            sb.AppendLine("App log (last 80 lines):");
            foreach (string ln in Log.Tail(80)) sb.AppendLine(ln);
            return sb.ToString();
        }

        // structure only: which periods and schedule names exist and how today was worked out, never class names
        List<string> ScheduleDebug()
        {
            List<string> l = new List<string>();
            try
            {
                l.Add("Schedule debug:");
                DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
                l.Add("  today=" + DateTime.Today.ToString("yyyy-MM-dd ddd") + " schoolDay=" + p.School + " dayLabel='" + p.DayLabel + "' (" + p.LabelFrom + ") bellSchedule='" + (p.Sched != null ? p.Sched.Name : "none") + "' slots=" + p.Slots.Count);
                foreach (Slot s in p.Slots)
                {
                    string src;
                    string v;
                    if (cfg.PeriodMap.TryGetValue(Settings.PeriodKey("", s.B.Label, p.DayLabel), out v) || cfg.PeriodMap.TryGetValue(Settings.PeriodKey("", s.B.Label, ""), out v)) src = "you chose";
                    else src = s.IsClass ? "from Campus" : "no class";
                    l.Add("  period '" + s.B.Label + "' " + s.B.Text() + " -> " + (s.IsClass ? "class" : (s.Free ? "free" : "block")) + " (" + src + ")");
                }
                l.Add("  roster placements (period names and schedule names only):");
                foreach (RosterItem r in snap.Roster)
                {
                    List<string> ps = new List<string>();
                    foreach (Placement pl in r.Places) ps.Add("[" + pl.Term + ": " + pl.Name + " / " + pl.SchedName + "]");
                    l.Add("    dropped=" + r.Dropped + " " + string.Join(" ", ps.ToArray()));
                }
                string[] labels = Sched.AllLabels(cfg, snap);
                l.Add("  day labels (" + labels.Length + "): " + string.Join(", ", labels));
            }
            catch (Exception ex) { l.Add("  could not build schedule debug: " + ex.Message); Log.Error("ScheduleDebug", ex); }
            return l;
        }

        UIElement PageDebug()
        {
            Button copy = UI.Btn("Copy report", "Pill", null);
            copy.Click += delegate
            {
                try { Clipboard.SetText(ReportText()); copy.Content = "Copied"; }
                catch (Exception) { copy.Content = "Could not copy"; }
            };
            Button again = UI.Btn("Run again", "Ghost", delegate { RefreshData(); });
            Button log = UI.Btn("Open log file", "Ghost", delegate { try { Process.Start(new ProcessStartInfo(Log.PathOf()) { UseShellExecute = true }); } catch (Exception) { } });
            again.Margin = new Thickness(8, 0, 8, 0);
            StackPanel bar = UI.Row(copy, again, log);
            bar.Margin = new Thickness(0, 0, 0, 16);

            List<ItemSpec> items = new List<ItemSpec>();
            if (snap != null)
            {
                foreach (DiagRow d in snap.Diag)
                {
                    ItemSpec it = new ItemSpec();
                    it.Title = d.Key + "   " + d.Path;
                    it.Sub = d.Error != null ? d.Error : "HTTP " + d.Status + "  ·  " + Math.Round(d.Bytes / 1024.0, 1) + " KB" + (d.Records > 0 ? "  ·  " + d.Records + " records" : "");
                    it.Badge = d.Ok ? "OK" : "Failed";
                    it.BadgeBg = d.Ok ? "AccSoft" : "BadSoft";
                    it.BadgeFg = d.Ok ? "AccText" : "Bad";
                    items.Add(it);
                }
                ItemSpec p = new ItemSpec();
                p.Title = "Parsed";
                p.Sub = snap.Students.Count + " students  ·  " + snap.Courses.Count + " courses  ·  " + snap.Assigns.Count
                    + " assignments  ·  " + snap.Roster.Count + " in schedule  ·  " + snap.Messages.Count + " messages  ·  "
                    + snap.Notices.Count + " announcements  ·  " + snap.Docs.Count + " documents";
                items.Add(p);
            }
            UIElement main = PageKit.ListCard("", "Nothing yet. Press Refresh.", items);

            List<ItemSpec> recent = new List<ItemSpec>();
            string[] tail = Log.Tail(14);
            for (int i = tail.Length - 1; i >= 0; i--)
            {
                ItemSpec it = new ItemSpec();
                it.Title = tail[i].Length > 150 ? tail[i].Substring(0, 150) + "…" : tail[i];
                it.Bold = false;
                recent.Add(it);
            }
            UIElement rec = PageKit.ListCard("Recent activity (also in the copied report)", "Nothing logged yet.", recent);
            ((FrameworkElement)rec).Margin = new Thickness(0, 18, 0, 0);
            return Body(bar, main, rec);
        }
    }
}
