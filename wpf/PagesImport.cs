using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    // Brings schedules back in from the files the export page makes (text, csv or json)
    sealed partial class MainWindow
    {
        string suImportNote = "";

        sealed class ImpDay { public BellSchedule B; public bool Sel = true; }
        sealed class ImpGroup { public string Name = ""; public List<ImpDay> Days = new List<ImpDay>(); }
        List<ImpGroup> suImp;
        int suImpMode;
        string suImpFrom = "";

        void SuImportFile()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Schedule files (*.txt;*.csv;*.json)|*.txt;*.csv;*.json|All files (*.*)|*.*";
            if (dlg.ShowDialog(this) != true) return;
            string text;
            try { text = File.ReadAllText(dlg.FileName); }
            catch (Exception) { suError = "Could not read that file."; SuRedo(true); return; }
            SuImportText(text, Path.GetFileName(dlg.FileName));
        }

        void SuImportClipboard()
        {
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch (Exception) { }
            if (string.IsNullOrWhiteSpace(text)) { suError = "The clipboard has no text."; SuRedo(true); return; }
            SuImportText(text, "the clipboard");
        }

        // reads the file and shows what is in it, so you can tick which schedules and letter days to bring in
        void SuImportText(string text, string from)
        {
            List<BellSchedule> got;
            try { got = ParseSchedules(text); }
            catch (Exception) { got = new List<BellSchedule>(); }
            if (got.Count == 0) { suError = "No schedules found in " + from + ". Use a file made by Export."; SuRedo(true); return; }
            List<ImpGroup> groups = new List<ImpGroup>();
            foreach (BellSchedule b in got)
            {
                string gn = string.IsNullOrWhiteSpace(b.Source) ? "Imported" : b.Source.Trim();
                ImpGroup g = null;
                foreach (ImpGroup x in groups) if (string.Equals(x.Name, gn, StringComparison.OrdinalIgnoreCase)) { g = x; break; }
                if (g == null) { g = new ImpGroup(); g.Name = gn; groups.Add(g); }
                string dn = b.Name, baseName = dn;
                int n = 2;
                while (g.Days.Exists(delegate(ImpDay y) { return string.Equals(y.B.Name, dn, StringComparison.OrdinalIgnoreCase); })) dn = baseName + " " + (n++);
                b.Name = dn;
                b.SortByTime();
                ImpDay d = new ImpDay();
                d.B = b;
                g.Days.Add(d);
            }
            suImp = groups;
            suImpFrom = from;
            suError = "";
            suImportNote = "";
            SuRedo(true);
        }

        UIElement SuImportPanel()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Text("Import from " + suImpFrom, 16, "T1", true));
            p.Children.Add(UI.Wrap("Tick the schedules and letter days you want. Anything you leave unticked is skipped.", 13, "T3"));
            foreach (ImpGroup grp in suImp)
            {
                ImpGroup g = grp;
                CheckBox gcb = new CheckBox();
                gcb.Content = g.Name + "   (" + g.Days.Count + (g.Days.Count == 1 ? " day)" : " days)");
                gcb.FontSize = 14.5;
                gcb.SetResourceReference(Control.ForegroundProperty, "T1");
                gcb.Margin = new Thickness(0, 16, 0, 6);
                gcb.IsChecked = g.Days.Exists(delegate(ImpDay y) { return y.Sel; });
                gcb.Click += delegate
                {
                    bool on = gcb.IsChecked == true;
                    foreach (ImpDay y in g.Days) y.Sel = on;
                    SuRedo(true);
                };
                p.Children.Add(gcb);
                foreach (ImpDay dd in g.Days)
                {
                    ImpDay d = dd;
                    CheckBox cb = new CheckBox();
                    cb.Content = d.B.Name + "   " + d.B.Entries.Count + (d.B.Entries.Count == 1 ? " period" : " periods");
                    cb.FontSize = 13.5;
                    cb.SetResourceReference(Control.ForegroundProperty, "T2");
                    cb.Margin = new Thickness(30, 4, 0, 4);
                    cb.IsChecked = d.Sel;
                    cb.Click += delegate
                    {
                        d.Sel = cb.IsChecked == true;
                        gcb.IsChecked = g.Days.Exists(delegate(ImpDay y) { return y.Sel; });
                    };
                    p.Children.Add(cb);
                }
            }
            TextBlock how = UI.Text("When a schedule has the same name as one you already have", 13, "T2", false);
            how.Margin = new Thickness(0, 18, 0, 6);
            p.Children.Add(how);
            p.Children.Add(SuSeg(new string[] { "Add it as a new schedule", "Replace the one I have" }, suImpMode, delegate(int k) { suImpMode = k; }));
            Button go = UI.Btn("Import selected", "Pill", delegate { SuImportApply(); });
            Button cancel = UI.Btn("Cancel", "Ghost", delegate { suImp = null; SuRedo(true); });
            go.Margin = new Thickness(0, 0, 8, 0);
            StackPanel bar = UI.Row(go, cancel);
            bar.Margin = new Thickness(0, 16, 0, 0);
            p.Children.Add(bar);
            Border card = UI.Card(p, 20);
            card.Margin = new Thickness(0, 0, 0, 16);
            return card;
        }

        // one schedule set from the chosen letter days: one column per day, one row per distinct time slot
        GridSet SuSetFromDays(string name, List<BellSchedule> days)
        {
            GridSet gs = new GridSet();
            gs.Name = name;
            List<TimeSpan[]> slots = new List<TimeSpan[]>();
            foreach (BellSchedule b in days)
                foreach (Bell e in b.Entries)
                {
                    bool have = false;
                    foreach (TimeSpan[] sl in slots) if (sl[0] == e.Start && sl[1] == e.End) { have = true; break; }
                    if (!have) slots.Add(new TimeSpan[] { e.Start, e.End });
                }
            slots.Sort(delegate(TimeSpan[] x, TimeSpan[] y) { int c = x[0].CompareTo(y[0]); return c != 0 ? c : x[1].CompareTo(y[1]); });
            foreach (TimeSpan[] sl in slots) { gs.Starts.Add(SuHM(sl[0])); gs.Ends.Add(SuHM(sl[1])); }
            foreach (BellSchedule b in days)
            {
                string dn = GridSet.G(b.Name);
                if (dn.Length == 0 || gs.Days.Contains(dn)) continue;
                gs.Days.Add(dn);
                List<string> col = new List<string>();
                for (int i = 0; i < slots.Count; i++)
                {
                    string lab = "";
                    foreach (Bell e in b.Entries) if (e.Start == slots[i][0] && e.End == slots[i][1]) { lab = GridSet.G(e.Label); break; }
                    col.Add(lab);
                }
                gs.Cells[dn] = col;
            }
            return gs;
        }

        void SuImportApply()
        {
            SuStash();
            int first = -1, added = 0, dayCount = 0;
            foreach (ImpGroup g in suImp)
            {
                List<BellSchedule> days = new List<BellSchedule>();
                foreach (ImpDay d in g.Days) if (d.Sel) days.Add(d.B);
                if (days.Count == 0) continue;
                GridSet ns = SuSetFromDays(GridSet.G(g.Name), days);
                if (ns.Days.Count == 0 || ns.Starts.Count == 0) continue;
                int ex = -1;
                for (int i = 0; i < suSets.Count; i++) if (string.Equals(suSets[i].Name, ns.Name, StringComparison.OrdinalIgnoreCase)) { ex = i; break; }
                int at;
                if (ex >= 0 && suImpMode == 1)
                {
                    ns.Name = suSets[ex].Name; ns.Auto = suSets[ex].Auto; ns.Weekday = suSets[ex].Weekday;
                    suSets[ex] = ns; at = ex;
                }
                else
                {
                    ns.Name = SuUniqueSetName(ns.Name);
                    suSets.Add(ns); at = suSets.Count - 1;
                }
                if (first < 0) first = at;
                added++; dayCount += ns.Days.Count;
            }
            if (added == 0) { suError = "Tick at least one letter day to import."; SuRedo(true); return; }
            suImp = null;
            suSetIdx = first;
            SuLoadFrom(suSets[first]);
            suError = "";
            suImportNote = "Imported " + added + (added == 1 ? " schedule" : " schedules") + " with " + dayCount + (dayCount == 1 ? " letter day" : " letter days") + ". Check the grid, then press Next.";
            SuRedo(false);
        }

        static BellSchedule NewImported(string name)
        {
            BellSchedule b = new BellSchedule();
            b.Name = string.IsNullOrWhiteSpace(name) ? "Imported" : name.Trim();
            return b;
        }

        static bool AddBell(BellSchedule b, string label, string start, string end)
        {
            TimeSpan a, z;
            if (!Bell.TryTime(start, out a) || !Bell.TryTime(end, out z)) return false;
            Bell e = new Bell();
            e.Label = (label ?? "").Trim(); e.Start = a; e.End = z;
            b.Entries.Add(e);
            return true;
        }

        static List<BellSchedule> ParseSchedules(string text)
        {
            string t = (text ?? "").Trim().TrimStart('﻿');
            List<BellSchedule> res = new List<BellSchedule>();
            if (t.Length == 0) return res;
            if (t[0] == '[' || t[0] == '{') ParseJson(t, res);
            else if (t.StartsWith("Schedule,", StringComparison.OrdinalIgnoreCase)) ParseCsv(t, res);
            else ParsePlain(t, res);
            res.RemoveAll(delegate(BellSchedule b) { return b.Entries.Count == 0; });
            return res;
        }

        static string JStr(System.Text.Json.JsonElement e, string key)
        {
            System.Text.Json.JsonElement v;
            if (e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(key, out v)) return v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : v.ToString();
            return "";
        }

        static void ParseJson(string t, List<BellSchedule> res)
        {
            using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(t))
            {
                List<System.Text.Json.JsonElement> items = new List<System.Text.Json.JsonElement>();
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array) foreach (System.Text.Json.JsonElement e in doc.RootElement.EnumerateArray()) items.Add(e);
                else items.Add(doc.RootElement);
                foreach (System.Text.Json.JsonElement it in items)
                {
                    BellSchedule b = NewImported(JStr(it, "name"));
                    b.Source = JStr(it, "source");
                    System.Text.Json.JsonElement per;
                    if (it.ValueKind == System.Text.Json.JsonValueKind.Object && it.TryGetProperty("periods", out per) && per.ValueKind == System.Text.Json.JsonValueKind.Array)
                        foreach (System.Text.Json.JsonElement r in per.EnumerateArray()) AddBell(b, JStr(r, "period"), JStr(r, "start"), JStr(r, "end"));
                    res.Add(b);
                }
            }
        }

        static List<string> CsvFields(string line)
        {
            List<string> f = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
                else if (c == '"') q = true;
                else if (c == ',') { f.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            f.Add(sb.ToString());
            return f;
        }

        static void ParseCsv(string t, List<BellSchedule> res)
        {
            string[] lines = t.Replace("\r", "").Split('\n');
            Dictionary<string, BellSchedule> by = new Dictionary<string, BellSchedule>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;
                List<string> f = CsvFields(lines[i]);
                if (f.Count < 5) continue;
                BellSchedule b;
                string ck = f[1] + "|" + f[0];
                if (!by.TryGetValue(ck, out b)) { b = NewImported(f[0]); b.Source = f[1]; by[ck] = b; res.Add(b); }
                AddBell(b, f[2], f[3], f[4]);
            }
        }

        static void ParsePlain(string t, List<BellSchedule> res)
        {
            Regex row = new Regex(@"^\s*(?:(.+?)\s+)?(\d{1,2}(?::\d{2})?\s*[ap]?\.?m?\.?)\s*[-–]\s*(\d{1,2}(?::\d{2})?\s*[ap]?\.?m?\.?)\s*$", RegexOptions.IgnoreCase);
            BellSchedule cur = null;
            foreach (string raw in t.Replace("\r", "").Split('\n'))
            {
                if (raw.Trim().Length == 0) continue;
                bool indented = raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t');
                Match m = row.Match(raw);
                if (m.Success && (indented || cur != null))
                {
                    if (cur == null) { cur = NewImported("Imported"); res.Add(cur); }
                    AddBell(cur, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                }
                else
                {
                    string name = Regex.Replace(raw.Trim(), @"\s*\([^)]*\)\s*$", "");
                    Match sm = Regex.Match(raw.Trim(), @"\(([^)]*)\)\s*$");
                    cur = NewImported(name);
                    if (sm.Success) cur.Source = sm.Groups[1].Value.Trim();
                    res.Add(cur);
                }
            }
        }
    }
}
