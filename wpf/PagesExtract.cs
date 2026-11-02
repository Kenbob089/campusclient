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

        readonly HashSet<string> exportSel = new HashSet<string>();
        bool exportInit;
        int exportFormat;
        LiveHost exportHost;

        List<ExpSched> ExportList()
        {
            List<ExpSched> list = new List<ExpSched>();
            foreach (BellSchedule b in cfg.Schedules) list.Add(FromBell(b, "Your schedules"));
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

        string ExportText(List<ExpSched> chosen, int fmt)
        {
            StringBuilder sb = new StringBuilder();
            if (fmt == 1)
            {
                sb.AppendLine("Schedule,Source,Period,Start,End");
                foreach (ExpSched e in chosen)
                    foreach (string[] r in e.Rows) sb.AppendLine(Csv(e.Name) + "," + Csv(e.Source) + "," + Csv(r[0]) + "," + Csv(r[1]) + "," + Csv(r[2]));
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
                    sb.AppendLine("  ] }" + (i < chosen.Count - 1 ? "," : ""));
                }
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
            if (all.Count == 0)
            {
                p.Children.Add(PageKit.Notice("There are no schedules to export yet. Set up your bell schedule first.", false));
                Button setup = UI.Btn("Set up bell schedule", "Pill", delegate { StartScheduleEdit(); });
                setup.HorizontalAlignment = HorizontalAlignment.Left;
                p.Children.Add(setup);
                return p;
            }
            WrapPanel top = new WrapPanel();
            Button all1 = UI.Btn("Select all", "Ghost", delegate { foreach (ExpSched e in all) exportSel.Add(e.Key); exportHost.Refresh(); });
            Button none = UI.Btn("Select none", "Ghost", delegate { exportSel.Clear(); exportHost.Refresh(); });
            all1.Margin = new Thickness(0, 0, 8, 8); none.Margin = new Thickness(0, 0, 8, 8);
            top.Children.Add(all1); top.Children.Add(none);
            p.Children.Add(top);

            string group = null;
            foreach (ExpSched e in all)
            {
                if (e.Source != group) { group = e.Source; p.Children.Add(PageKit.Section(group)); }
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
                card.Margin = new Thickness(0, 0, 0, 10);
                p.Children.Add(card);
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
            int n = ChosenSchedules().Count;
            exportStatus.Text = n == 0 ? "Tick the schedules you want." : n + (n == 1 ? " schedule selected." : " schedules selected.");
        }

        void DoExport(bool copy)
        {
            List<ExpSched> chosen = ChosenSchedules();
            if (chosen.Count == 0) { if (exportStatus != null) exportStatus.Text = "Tick at least one schedule first."; return; }
            string text = ExportText(chosen, exportFormat);
            if (copy)
            {
                try { Clipboard.SetText(text); if (exportStatus != null) exportStatus.Text = "Copied " + chosen.Count + (chosen.Count == 1 ? " schedule." : " schedules."); }
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
            if (!exportInit) { exportInit = true; foreach (ExpSched e in ExportList()) exportSel.Add(e.Key); }
            exportHost = Live(delegate { return ExportBody(); });
            return Body(exportHost);
        }
    }
}
