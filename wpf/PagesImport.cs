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

        // turns exported schedules back into the setup grid: one letter day per schedule, one row per distinct time slot
        void SuImportText(string text, string from)
        {
            List<BellSchedule> got;
            try { got = ParseSchedules(text); }
            catch (Exception) { got = new List<BellSchedule>(); }
            if (got.Count == 0) { suError = "No schedules found in " + from + ". Use a file made by Export."; SuRedo(true); return; }

            List<string> names = new List<string>();
            List<BellSchedule> use = new List<BellSchedule>();
            foreach (BellSchedule b in got)
            {
                string nm = b.Name, baseName = nm;
                int n = 2;
                while (names.Contains(nm)) nm = baseName + " " + (n++);
                names.Add(nm);
                b.Name = nm;
                b.SortByTime();
                use.Add(b);
            }

            List<TimeSpan[]> slots = new List<TimeSpan[]>();
            foreach (BellSchedule b in use)
                foreach (Bell e in b.Entries)
                {
                    bool have = false;
                    foreach (TimeSpan[] sl in slots) if (sl[0] == e.Start && sl[1] == e.End) { have = true; break; }
                    if (!have) slots.Add(new TimeSpan[] { e.Start, e.End });
                }
            slots.Sort(delegate(TimeSpan[] x, TimeSpan[] y) { int c = x[0].CompareTo(y[0]); return c != 0 ? c : x[1].CompareTo(y[1]); });

            suDays = new List<string>(names);
            suStart = new List<string>(); suEnd = new List<string>();
            foreach (TimeSpan[] sl in slots) { suStart.Add(SuHM(sl[0])); suEnd.Add(SuHM(sl[1])); }
            suCells = new Dictionary<string, List<string>>();
            foreach (BellSchedule b in use)
            {
                List<string> col = new List<string>();
                for (int i = 0; i < slots.Count; i++)
                {
                    string lab = "";
                    foreach (Bell e in b.Entries) if (e.Start == slots[i][0] && e.End == slots[i][1]) { lab = e.Label; break; }
                    col.Add(lab);
                }
                suCells[b.Name] = col;
            }
            SuGridNormalize();
            suTodayDay = suDays[0];
            suFirstS = "1"; suLastS = suStart.Count.ToString(); suLunchS = Math.Max(1, suStart.Count / 2).ToString();
            suError = "";
            suImportNote = "Imported " + suDays.Count + (suDays.Count == 1 ? " letter day" : " letter days") + " and " + suStart.Count + (suStart.Count == 1 ? " time slot" : " time slots") + " from " + from + ". Check the grid, then pick which letter it is today.";
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
                if (!by.TryGetValue(f[0], out b)) { b = NewImported(f[0]); by[f[0]] = b; res.Add(b); }
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
                    cur = NewImported(name);
                    res.Add(cur);
                }
            }
        }
    }
}
