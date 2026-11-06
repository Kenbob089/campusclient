using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    // Pick which bell schedules (the period times of a school day, not your classes) to take out of the app
    sealed partial class MainWindow
    {
        sealed class ExpSched
        {
            public string Key = "", Name = "", Source = "";
            public List<string[]> Rows = new List<string[]>();   // period, start, end
        }

        // the schedule settings that can travel with the schedules
        sealed class ExpSetting { public string Key = "", Label = "", Value = ""; }

        List<ExpSetting> SettingList()
        {
            List<ExpSetting> l = new List<ExpSetting>();
            ExpSetting s = new ExpSetting(); s.Key = "noletters"; s.Value = cfg.NoLetters ? "1" : "0";
            s.Label = "Letter days: " + (cfg.NoLetters ? "my school does not use them" : "my school uses them"); l.Add(s);
            s = new ExpSetting(); s.Key = "rotation"; s.Value = (cfg.RotOn ? "mine" : "campus") + "|" + (cfg.RotLabels ?? "").Replace("|", " ");
            s.Label = "Which letter day it is: " + (cfg.RotOn ? "set by me (" + cfg.RotLabels + ")" : "follow the Campus calendar"); l.Add(s);
            s = new ExpSetting();
            s.Key = "rotateperiods";
            s.Value = (cfg.NoRotation ? "0" : "1") + "|" + suRotMode + "|" + suRotDir + "|" + suFirstS.Replace("|", " ") + "|" + suLastS.Replace("|", " ") + "|" + suLunchS.Replace("|", " ");
            s.Label = "Rotating the periods: " + (cfg.NoRotation ? "set each letter day by hand" : "rotate them for me"); l.Add(s);
            if (cfg.SchoolEnd.Length > 0) { s = new ExpSetting(); s.Key = "schoolend"; s.Value = cfg.SchoolEnd; s.Label = "Last day of school: " + cfg.SchoolEnd; l.Add(s); }
            s = new ExpSetting(); s.Key = "periodnames"; s.Value = cfg.PeriodNames.ToString();
            s.Label = "Period names: " + (cfg.PeriodNames == 1 ? "Periods (1, 2, 3)" : "as Campus names them"); l.Add(s);
            if (cfg.Sets.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (GridSet g in cfg.Sets) parts.Add(g.Name.Replace("^", " ").Replace("~", " ") + "~" + g.Auto.Replace("^", " ").Replace("~", " ") + "~" + g.Weekday);
                s = new ExpSetting(); s.Key = "setrules"; s.Value = string.Join("^", parts.ToArray());
                s.Label = "When each extra schedule is used (" + cfg.Sets.Count + (cfg.Sets.Count == 1 ? " schedule)" : " schedules)"); l.Add(s);
            }
            if (cfg.PeriodMap.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, string> kv in cfg.PeriodMap) parts.Add(kv.Key.Replace("^", " ").Replace("=", " ") + "=" + kv.Value.Replace("^", " ").Replace("=", " "));
                s = new ExpSetting(); s.Key = "periodmap"; s.Value = string.Join("^", parts.ToArray());
                s.Label = "Which class is in which period (" + cfg.PeriodMap.Count + " periods)"; l.Add(s);
            }
            return l;
        }

        List<ExpSetting> ChosenSettings()
        {
            return SettingList().FindAll(delegate(ExpSetting e) { return exportSel.Contains("set|" + e.Key); });
        }

        readonly HashSet<string> exportSel = new HashSet<string>();
        bool exportInit;
        int exportFormat;
        LiveHost exportHost;

        List<ExpSched> ExportList()
        {
            List<ExpSched> list = new List<ExpSched>();
            // the main schedule, then every extra one (Half day...), each letter day on its own
            if (cfg.GridDays.Count > 0 && cfg.GridStarts.Count > 0)
            {
                GridSet main = new GridSet();
                main.Name = cfg.RegularName;
                main.Days = new List<string>(cfg.GridDays); main.Starts = new List<string>(cfg.GridStarts); main.Ends = new List<string>(cfg.GridEnds);
                foreach (KeyValuePair<string, List<string>> gc in cfg.GridCells) main.Cells[gc.Key] = new List<string>(gc.Value);
                foreach (string dn in main.Days) list.Add(FromBell(main.Build(dn), main.Name));
            }
            else foreach (BellSchedule b in cfg.Schedules) list.Add(FromBell(b, cfg.RegularName));
            foreach (GridSet gx in cfg.Sets)
                foreach (string dn2 in gx.Days) list.Add(FromBell(gx.Build(dn2), gx.Name));
            foreach (KeyValuePair<string, List<string>> v in cfg.Variants)
            {
                ExpSched e = new ExpSched();
                e.Source = "Saved day schedules"; e.Name = v.Key; e.Key = e.Source + "|" + e.Name;
                for (int i = 0; i < v.Value.Count; i++)
                {
                    string[] pr = v.Value[i].Split('-');
                    TimeSpan a, z;
                    if (pr.Length != 2 || !Bell.TryTime(pr[0], out a) || !Bell.TryTime(pr[1], out z)) continue;
                    string label = (i + 1).ToString();
                    e.Rows.Add(new string[] { label, Bell.Clock(a), Bell.Clock(z) });
                }
                list.Add(e);
            }
            if (snap != null) foreach (BellSchedule b in snap.AutoBells) list.Add(FromBell(b, "From Campus"));
            return list;
        }

        static ExpSched FromBell(BellSchedule b, string source)
        {
            ExpSched e = new ExpSched();
            e.Source = source; e.Name = b.Name; e.Key = source + "|" + b.Name + "|" + b.Id;
            List<Bell> es = new List<Bell>(b.Entries);
            es.Sort(delegate(Bell x, Bell y) { return x.Start.CompareTo(y.Start); });
            foreach (Bell x in es) e.Rows.Add(new string[] { x.Label, Bell.Clock(x.Start), Bell.Clock(x.End) });
            return e;
        }

        static string JsonStr(string s)
        {
            return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        string ExportText(List<ExpSched> chosen, List<ExpSetting> sets, int fmt)
        {
            StringBuilder sb = new StringBuilder();
            if (fmt == 1)
            {
                sb.AppendLine("Schedule,Source,Period,Start,End");
                foreach (ExpSched e in chosen)
                    foreach (string[] r in e.Rows) sb.AppendLine(Csv(e.Name) + "," + Csv(e.Source) + "," + Csv(r[0]) + "," + Csv(r[1]) + "," + Csv(r[2]));
                foreach (ExpSetting x in sets) sb.AppendLine("@setting," + Csv(x.Key) + "," + Csv(x.Value) + ",,");
            }
            else if (fmt == 2)
            {
                sb.AppendLine("[");
                for (int i = 0; i < chosen.Count; i++)
                {
                    ExpSched e = chosen[i];
                    sb.AppendLine("  { \"name\": " + JsonStr(e.Name) + ", \"source\": " + JsonStr(e.Source) + ", \"periods\": [");
                    for (int j = 0; j < e.Rows.Count; j++)
                        sb.AppendLine("    { \"period\": " + JsonStr(e.Rows[j][0]) + ", \"start\": " + JsonStr(e.Rows[j][1]) + ", \"end\": " + JsonStr(e.Rows[j][2]) + " }" + (j < e.Rows.Count - 1 ? "," : ""));
                    sb.AppendLine("  ] }" + (i < chosen.Count - 1 || sets.Count > 0 ? "," : ""));
                }
                for (int si = 0; si < sets.Count; si++)
                    sb.AppendLine("  { \"setting\": " + JsonStr(sets[si].Key) + ", \"value\": " + JsonStr(sets[si].Value) + " }" + (si < sets.Count - 1 ? "," : ""));
                sb.AppendLine("]");
            }
            else
            {
                foreach (ExpSched e in chosen)
                {
                    sb.AppendLine(e.Name + "  (" + e.Source + ")");
                    foreach (string[] r in e.Rows) sb.AppendLine("  " + (r[0].Length > 0 ? r[0] + "  " : "") + r[1] + " - " + r[2]);
                    sb.AppendLine();
                }
                if (sets.Count > 0)
                {
                    sb.AppendLine("Schedule settings");
                    foreach (ExpSetting x in sets) sb.AppendLine("@setting " + x.Key + "=" + x.Value + "    (" + x.Label + ")");
                }
            }
            return sb.ToString();
        }

        List<ExpSched> ChosenSchedules()
        {
            return ExportList().FindAll(delegate(ExpSched e) { return exportSel.Contains(e.Key); });
        }

        UIElement ExportBody()
        {
            List<ExpSched> all = ExportList();
            StackPanel p = new StackPanel();
            List<ExpSetting> setAll = SettingList();
            if (all.Count == 0 && setAll.Count == 0)
            {
                p.Children.Add(PageKit.Notice("There are no schedules to export yet. Set up your schedule first.", false));
                Button setup = UI.Btn("Set up bell schedule", "Pill", delegate { StartScheduleEdit(); });
                setup.HorizontalAlignment = HorizontalAlignment.Left;
                p.Children.Add(setup);
                return p;
            }
            WrapPanel top = new WrapPanel();
            Button all1 = UI.Btn("Select all", "Ghost", delegate { foreach (ExpSched e in all) exportSel.Add(e.Key); foreach (ExpSetting st in setAll) exportSel.Add("set|" + st.Key); exportHost.Refresh(); });
            Button none = UI.Btn("Select none", "Ghost", delegate { exportSel.Clear(); exportHost.Refresh(); });
            all1.Margin = new Thickness(0, 0, 8, 8); none.Margin = new Thickness(0, 0, 8, 8);
            top.Children.Add(all1); top.Children.Add(none);
            p.Children.Add(top);

            string group = null;
            foreach (ExpSched e in all)
            {
                if (e.Source != group)
                {
                    group = e.Source;
                    string gname = group;
                    List<ExpSched> members = all.FindAll(delegate(ExpSched q) { return q.Source == gname; });
                    CheckBox gh = new CheckBox();
                    gh.Content = gname + "   " + members.Count + (members.Count == 1 ? " day" : " days");
                    gh.FontSize = 15.5;
                    gh.FontWeight = FontWeights.SemiBold;
                    gh.SetResourceReference(Control.ForegroundProperty, "T1");
                    gh.Margin = new Thickness(0, 20, 0, 10);
                    gh.IsChecked = members.TrueForAll(delegate(ExpSched q) { return exportSel.Contains(q.Key); });
                    gh.Click += delegate
                    {
                        bool on = gh.IsChecked == true;
                        foreach (ExpSched mm in members) { if (on) exportSel.Add(mm.Key); else exportSel.Remove(mm.Key); }
                        exportHost.Refresh();
                    };
                    p.Children.Add(gh);
                }
                ExpSched ex = e;
                CheckBox cb = new CheckBox();
                cb.Content = e.Name;
                cb.FontSize = 14.5;
                cb.SetResourceReference(Control.ForegroundProperty, "T1");
                cb.IsChecked = exportSel.Contains(e.Key);
                cb.Click += delegate { if (cb.IsChecked == true) exportSel.Add(ex.Key); else exportSel.Remove(ex.Key); UpdateExportStatus(); };
                StringBuilder times = new StringBuilder();
                for (int i = 0; i < e.Rows.Count; i++)
                {
                    if (i > 0) times.Append("   ");
                    times.Append((e.Rows[i][0].Length > 0 ? e.Rows[i][0] + ": " : "") + e.Rows[i][1] + " - " + e.Rows[i][2]);
                }
                StackPanel inner = new StackPanel();
                inner.Children.Add(cb);
                inner.Children.Add(UI.Margin(UI.Wrap(e.Rows.Count == 0 ? "No times." : times.ToString(), 12.5, "T3"), 48, 4, 0, 0));
                Border card = UI.Card(inner, 16);
                card.Margin = new Thickness(26, 0, 0, 10);
                p.Children.Add(card);
            }

            if (setAll.Count > 0)
            {
                CheckBox sh = new CheckBox();
                sh.Content = "Schedule settings   " + setAll.Count + (setAll.Count == 1 ? " item" : " items");
                sh.FontSize = 15.5;
                sh.FontWeight = FontWeights.SemiBold;
                sh.SetResourceReference(Control.ForegroundProperty, "T1");
                sh.Margin = new Thickness(0, 20, 0, 10);
                sh.IsChecked = setAll.TrueForAll(delegate(ExpSetting q) { return exportSel.Contains("set|" + q.Key); });
                sh.Click += delegate
                {
                    bool on = sh.IsChecked == true;
                    foreach (ExpSetting q in setAll) { if (on) exportSel.Add("set|" + q.Key); else exportSel.Remove("set|" + q.Key); }
                    exportHost.Refresh();
                };
                p.Children.Add(sh);
                foreach (ExpSetting st in setAll)
                {
                    ExpSetting sx = st;
                    CheckBox sc = new CheckBox();
                    sc.Content = st.Label;
                    sc.FontSize = 14;
                    sc.SetResourceReference(Control.ForegroundProperty, "T1");
                    sc.IsChecked = exportSel.Contains("set|" + st.Key);
                    sc.Click += delegate { if (sc.IsChecked == true) exportSel.Add("set|" + sx.Key); else exportSel.Remove("set|" + sx.Key); UpdateExportStatus(); };
                    Border scard = UI.Card(sc, 14);
                    scard.Margin = new Thickness(26, 0, 0, 8);
                    p.Children.Add(scard);
                }
            }

            p.Children.Add(PageKit.Section("Save as"));
            p.Children.Add(SuSeg(new string[] { "Text", "Spreadsheet (.csv)", "JSON" }, exportFormat, delegate(int k) { exportFormat = k; }));
            exportStatus = UI.Text("", 12.5, "T3", false);
            exportStatus.Margin = new Thickness(2, 14, 0, 0);
            Button save = UI.Btn("Export selected", "Pill", delegate { DoExport(false); });
            Button copy = UI.Btn("Copy to clipboard", "Ghost", delegate { DoExport(true); });
            save.Margin = new Thickness(0, 0, 8, 0);
            StackPanel bar = UI.Row(save, copy);
            bar.Margin = new Thickness(0, 16, 0, 0);
            p.Children.Add(bar);
            p.Children.Add(exportStatus);
            UpdateExportStatus();
            return p;
        }

        TextBlock exportStatus;

        void UpdateExportStatus()
        {
            if (exportStatus == null) return;
            int n = ChosenSchedules().Count, ns = ChosenSettings().Count;
            exportStatus.Text = n + ns == 0 ? "Tick the schedules or settings you want." : n + (n == 1 ? " schedule" : " schedules") + (ns > 0 ? " and " + ns + (ns == 1 ? " setting" : " settings") : "") + " selected.";
        }

        void DoExport(bool copy)
        {
            List<ExpSched> chosen = ChosenSchedules();
            List<ExpSetting> chosenSets = ChosenSettings();
            if (chosen.Count + chosenSets.Count == 0) { if (exportStatus != null) exportStatus.Text = "Tick at least one schedule or setting first."; return; }
            string text = ExportText(chosen, chosenSets, exportFormat);
            if (copy)
            {
                try { Clipboard.SetText(text); if (exportStatus != null) exportStatus.Text = "Copied " + (chosen.Count + chosenSets.Count) + " items."; }
                catch (Exception) { if (exportStatus != null) exportStatus.Text = "Could not copy."; }
                return;
            }
            string[] ext = new string[] { "txt", "csv", "json" };
            string[] nm = new string[] { "Text file (*.txt)|*.txt", "Spreadsheet (*.csv)|*.csv", "JSON file (*.json)|*.json" };
            string f = AskSave("schedules." + ext[exportFormat], nm[exportFormat]);
            if (f == null) return;
            SaveText(f, text);
            if (exportStatus != null) exportStatus.Text = "Saved " + System.IO.Path.GetFileName(f) + ".";
        }

        UIElement PageExtract()
        {
            if (!exportInit) { exportInit = true; foreach (ExpSched e in ExportList()) exportSel.Add(e.Key); foreach (ExpSetting st0 in SettingList()) exportSel.Add("set|" + st0.Key); }
            exportHost = Live(delegate { return ExportBody(); });
            return Body(exportHost);
        }
    }
}
