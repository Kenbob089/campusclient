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
        TextBlock importStatus;

        UIElement ImportBlock()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(PageKit.Section("Import schedules"));
            p.Children.Add(UI.Wrap("Bring in schedules saved with Export (text, spreadsheet or JSON). They are added to Your schedules; nothing is overwritten.", 13, "T3"));
            Button file = UI.Btn("Import from file", "Pill", delegate { DoImportFile(); });
            Button clip = UI.Btn("Paste from clipboard", "Ghost", delegate { DoImportClipboard(); });
            file.Margin = new Thickness(0, 0, 8, 0);
            StackPanel bar = UI.Row(file, clip);
            bar.Margin = new Thickness(0, 14, 0, 0);
            p.Children.Add(bar);
            importStatus = UI.Text("", 12.5, "T3", false);
            importStatus.Margin = new Thickness(2, 14, 0, 0);
            p.Children.Add(importStatus);
            return p;
        }

        void DoImportFile()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Schedule files (*.txt;*.csv;*.json)|*.txt;*.csv;*.json|All files (*.*)|*.*";
            if (dlg.ShowDialog(this) != true) return;
            string text;
            try { text = File.ReadAllText(dlg.FileName); }
            catch (Exception) { importStatus.Text = "Could not read that file."; return; }
            ImportText(text, Path.GetFileName(dlg.FileName));
        }

        void DoImportClipboard()
        {
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch (Exception) { }
            if (string.IsNullOrWhiteSpace(text)) { importStatus.Text = "The clipboard has no text."; return; }
            ImportText(text, "the clipboard");
        }

        void ImportText(string text, string from)
        {
            List<BellSchedule> got;
            try { got = ParseSchedules(text); }
            catch (Exception) { got = new List<BellSchedule>(); }
            if (got.Count == 0) { importStatus.Text = "No schedules found in " + from + ". Use a file made by Export."; return; }
            foreach (BellSchedule b in got)
            {
                string name = b.Name, baseName = name;
                int n = 2;
                while (cfg.Schedules.Exists(delegate(BellSchedule x) { return string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase); }))
                    name = baseName + " (" + (n++) + ")";
                b.Name = name;
                b.SortByTime();
                cfg.Schedules.Add(b);
                exportSel.Add("Your schedules|" + b.Name + "|" + b.Id);
            }
            cfg.Save();
            if (exportHost != null) exportHost.Refresh();
            if (importStatus != null) importStatus.Text = "Imported " + got.Count + (got.Count == 1 ? " schedule" : " schedules") + " from " + from + ".";
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
