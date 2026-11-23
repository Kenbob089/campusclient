using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace CampusMidnight
{
    // Ctrl+Z on the setup screens: every change to the grid, the sets or the class choices can be taken back
    sealed partial class MainWindow
    {
        sealed class SuSnap
        {
            public List<string> Days, Start, End;
            public Dictionary<string, List<string>> Cells;
            public List<GridSet> Sets;
            public Dictionary<string, string> Periods;
            public int SetIdx, RotMode, RotDir;
            public bool NoLetters, NoRot, EndOn, UseCampus;
            public DateTime EndDate;
            public string TodayDay, FirstS, LastS, LunchS, Key;
        }

        readonly List<SuSnap> suHist = new List<SuSnap>();

        SuSnap SuTake()
        {
            SuSnap s = new SuSnap();
            s.Days = new List<string>(suDays); s.Start = new List<string>(suStart); s.End = new List<string>(suEnd);
            s.Cells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in suCells) s.Cells[kv.Key] = new List<string>(kv.Value);
            s.Sets = new List<GridSet>();
            foreach (GridSet g in suSets) s.Sets.Add(g.Copy(g.Name));
            s.Periods = new Dictionary<string, string>(cfg.PeriodMap);
            s.SetIdx = suSetIdx; s.RotMode = suRotMode; s.RotDir = suRotDir;
            s.NoLetters = suNoLetters; s.NoRot = suNoRot; s.EndOn = suEndOn; s.UseCampus = suUseCampus; s.EndDate = suEndDate;
            s.TodayDay = suTodayDay; s.FirstS = suFirstS; s.LastS = suLastS; s.LunchS = suLunchS;
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Join(",", s.Days.ToArray())).Append('|').Append(string.Join(",", s.Start.ToArray())).Append('|').Append(string.Join(",", s.End.ToArray())).Append('|');
            foreach (KeyValuePair<string, List<string>> kv in s.Cells) sb.Append(kv.Key).Append('=').Append(string.Join(",", kv.Value.ToArray())).Append(';');
            sb.Append('|');
            foreach (GridSet g in s.Sets) sb.Append(g.Name).Append(':').Append(g.Write()).Append('#');
            sb.Append('|');
            List<string> keys = new List<string>(s.Periods.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys) sb.Append(k).Append('=').Append(s.Periods[k]).Append(';');
            sb.Append('|').Append(s.SetIdx).Append(s.NoLetters).Append(s.NoRot).Append(s.EndOn).Append(s.UseCampus).Append(s.EndDate.Ticks).Append(s.TodayDay);
            s.Key = sb.ToString();
            return s;
        }

        // called whenever a setup page is drawn: remembers the state if it changed since last time
        void SuRemember()
        {
            try
            {
                SuSnap s = SuTake();
                if (suHist.Count > 0 && suHist[suHist.Count - 1].Key == s.Key) return;
                suHist.Add(s);
                if (suHist.Count > 80) suHist.RemoveAt(0);
            }
            catch (Exception) { }
        }

        void SuUndo()
        {
            SuRemember();
            if (suHist.Count < 2) return;
            suHist.RemoveAt(suHist.Count - 1);
            SuSnap s = suHist[suHist.Count - 1];
            suDays = new List<string>(s.Days); suStart = new List<string>(s.Start); suEnd = new List<string>(s.End);
            suCells = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> kv in s.Cells) suCells[kv.Key] = new List<string>(kv.Value);
            suSets = new List<GridSet>();
            foreach (GridSet g in s.Sets) suSets.Add(g.Copy(g.Name));
            cfg.PeriodMap.Clear();
            foreach (KeyValuePair<string, string> kv in s.Periods) cfg.PeriodMap[kv.Key] = kv.Value;
            suSetIdx = s.SetIdx; suRotMode = s.RotMode; suRotDir = s.RotDir;
            suNoLetters = s.NoLetters; suNoRot = s.NoRot; suEndOn = s.EndOn; suUseCampus = s.UseCampus; suEndDate = s.EndDate;
            suTodayDay = s.TodayDay; suFirstS = s.FirstS; suLastS = s.LastS; suLunchS = s.LunchS;
            suError = "";
            SuRedo(true);
        }

        // true when the key press was an undo that the setup screen handled
        bool SuHandleUndoKey(KeyEventArgs ke)
        {
            if (view != "setup" || ke.Key != Key.Z || Keyboard.Modifiers != ModifierKeys.Control) return false;
            if (Keyboard.FocusedElement is TextBox) return false;   // a text box has its own undo
            SuUndo();
            return true;
        }
    }
}
