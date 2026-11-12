using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        // the classes of the quarter on screen, with every quarter's marks merged when Campus names none for it
        List<AttCourse> AttendanceForTerm()
        {
            List<AttCourse> pick = new List<AttCourse>();
            foreach (AttCourse c in snap.AttCourses)
                if (string.Equals(c.Term, term, StringComparison.OrdinalIgnoreCase)) pick.Add(c);
            if (pick.Count == 0) pick = new List<AttCourse>(snap.AttCourses);
            // one row per class name
            List<AttCourse> merged = new List<AttCourse>();
            foreach (AttCourse c in pick)
            {
                AttCourse m = merged.Find(delegate(AttCourse x) { return string.Equals(x.Name, c.Name, StringComparison.OrdinalIgnoreCase); });
                if (m == null) { m = new AttCourse(); m.Name = c.Name; m.Term = c.Term; merged.Add(m); }
                m.Events.AddRange(c.Events);
            }
            foreach (AttCourse m in merged)
                m.Events.Sort(delegate(AttEvent a, AttEvent b) { return (b.Date ?? DateTime.MinValue).CompareTo(a.Date ?? DateTime.MinValue); });
            return merged;
        }

        UIElement AttTile(string big, string label, string fg)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Text(big, 30, fg, true));
            p.Children.Add(UI.Margin(UI.Text(label, 12, "T3", false), 0, 2, 0, 0));
            return UI.Card(p, 18);
        }

        UIElement PageAttendance()
        {
            List<UIElement> kids = new List<UIElement>();
            if (snap.AttCourses.Count == 0)
            {
                kids.Add(PageKit.Notice(ModuleNote("attend", "attendance records"), false));
                Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusPage("attendance"); });
                open.HorizontalAlignment = HorizontalAlignment.Left;
                kids.Add(open);
                return Body(kids.ToArray());
            }
            List<AttCourse> cs = AttendanceForTerm();
            int ab = 0, ta = 0, er = 0, pr = 0;
            foreach (AttCourse c in cs) { ab += c.Count("Absent"); ta += c.Count("Tardy"); er += c.Count("Early release"); pr += c.Count("Present"); }
            pageSub = term.Length > 0 ? term : "";

            Grid tiles = UI.Cols(1, 1, 1, 1);
            UIElement t1;
            if (cfg.AbsDays)
            {
                // days with at least one absence, counting each date once
                HashSet<DateTime> daysOff = new HashSet<DateTime>();
                foreach (AttCourse c in cs) foreach (AttEvent e in c.Events) if (e.Kind == "Absent" && e.Date.HasValue) daysOff.Add(e.Date.Value.Date);
                t1 = AttTile(daysOff.Count.ToString(), "days absent", daysOff.Count > 0 ? "Bad" : "Good");
            }
            else t1 = AttTile(ab.ToString(), ab == 1 ? "absence" : "absences", ab > 0 ? "Bad" : "Good");
            UIElement t2 = AttTile(ta.ToString(), ta == 1 ? "tardy" : "tardies", ta > 0 ? "Warn" : "Good");
            UIElement t3 = AttTile(er.ToString(), "early releases", "T1");
            UIElement t4 = AttTile(pr.ToString(), pr == 1 ? "event marked present" : "events marked present", "AccText");
            ((FrameworkElement)t1).Margin = new Thickness(0, 0, 6, 14);
            ((FrameworkElement)t2).Margin = new Thickness(6, 0, 6, 14);
            ((FrameworkElement)t3).Margin = new Thickness(6, 0, 6, 14);
            ((FrameworkElement)t4).Margin = new Thickness(6, 0, 0, 14);
            UI.At(tiles, t1, 0); UI.At(tiles, t2, 1); UI.At(tiles, t3, 2); UI.At(tiles, t4, 3);
            kids.Add(tiles);

            List<ItemSpec> rows = new List<ItemSpec>();
            foreach (AttCourse c0 in cs)
            {
                AttCourse c = c0;
                int a = c.Count("Absent"), t = c.Count("Tardy"), e = c.Count("Early release"), p = c.Count("Present");
                ItemSpec it = new ItemSpec();
                it.Title = c.Name;
                List<string> parts = new List<string>();
                parts.Add(a + (a == 1 ? " absence" : " absences"));
                parts.Add(t + (t == 1 ? " tardy" : " tardies"));
                if (e > 0) parts.Add(e + " early");
                it.Sub = string.Join("  ·  ", parts.ToArray());
                if (p > 0) it.Right = p + (p == 1 ? " event" : " events");
                if (a > 0) { it.Badge = "Absent " + a; it.BadgeBg = "BadSoft"; it.BadgeFg = "Bad"; }
                else if (t > 0) { it.Badge = "Tardy " + t; it.BadgeBg = "WarnSoft"; it.BadgeFg = "Warn"; }
                it.Click = delegate { OpenAttendance(c); };
                rows.Add(it);
            }
            kids.Add(PageKit.ListCard("By class  ·  click one for the details", "", rows));
            return Body(kids.ToArray());
        }

        // a window with the full record for one class
        void OpenAttendance(AttCourse c)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Text(c.Name, 22, "T1", true));
            string scope = string.IsNullOrEmpty(c.Term) ? "" : c.Term + "  ·  ";
            p.Children.Add(UI.Margin(UI.Text(scope + "attendance record", 13.5, "T3", false), 0, 2, 0, 16));

            // period totals, split the way Campus splits them
            string[] kinds = new string[] { "Absent", "Tardy", "Early release" };
            string[] groups = new string[] { "Excused", "Unexcused", "Exempt", "Unknown" };
            StackPanel tbl = new StackPanel();
            tbl.Children.Add(AttTableRow(new string[] { "", "ABSENT", "TARDY", "EARLY" }, true, false));
            foreach (string g in groups)
            {
                string[] cells = new string[4];
                cells[0] = g;
                for (int k = 0; k < kinds.Length; k++)
                {
                    int n = 0;
                    foreach (AttEvent e in c.Events) if (e.Kind == kinds[k] && Attend.Excuse(e.Status) == g) n++;
                    cells[k + 1] = n.ToString();
                }
                tbl.Children.Add(AttTableRow(cells, false, g == groups[groups.Length - 1]));
            }
            Border tcard = UI.Card(tbl, 0);
            tcard.Margin = new Thickness(0, 0, 0, 16);
            p.Children.Add(UI.Margin(UI.Text("PERIOD TOTALS", 11.5, "T3", true), 2, 0, 0, 8));
            p.Children.Add(tcard);

            bool any = false;
            foreach (string kind in new string[] { "Absent", "Tardy", "Early release", "Present" })
            {
                List<ItemSpec> items = new List<ItemSpec>();
                foreach (AttEvent e in c.Events)
                {
                    if (e.Kind != kind) continue;
                    ItemSpec it = new ItemSpec();
                    it.Lead = AttTime(e.Time);
                    it.Title = e.Date.HasValue ? e.Date.Value.ToString("dddd M/d/yyyy") : "Date not given";
                    List<string> sub = new List<string>();
                    if (e.Title.Length > 0) sub.Add(e.Title);
                    if (e.Comment.Length > 0) sub.Add(e.Comment);
                    if (sub.Count == 0 && e.Period.Length > 0) sub.Add("Period " + e.Period);
                    it.Sub = string.Join("  ·  ", sub.ToArray());
                    if (e.Status.Length > 0)
                    {
                        it.Badge = e.Status.ToUpperInvariant();
                        string x = Attend.Excuse(e.Status);
                        if (x == "Unexcused") { it.BadgeBg = "BadSoft"; it.BadgeFg = "Bad"; }
                    }
                    items.Add(it);
                }
                if (items.Count == 0) continue;
                any = true;
                UIElement card = PageKit.ListCard(kind + "  ·  " + items.Count, "", items);
                ((FrameworkElement)card).Margin = new Thickness(0, 0, 0, 14);
                p.Children.Add(card);
            }
            if (!any) p.Children.Add(UI.Margin(UI.Text("Nothing is marked for this class: no absences, tardies or early releases.", 13.5, "T3", false), 2, 4, 0, 0));
            NewWindow(c.Name, p, 700, 760).ShowDialog();
        }

        UIElement AttTableRow(string[] cells, bool head, bool last)
        {
            Grid g = UI.Cols(2, 1, 1, 1);
            g.Margin = new Thickness(0);
            for (int i = 0; i < cells.Length; i++)
            {
                TextBlock t = head ? UI.Text(cells[i], 11.5, "T3", true) : UI.Text(cells[i], 13.5, i == 0 ? "T1" : "T2", false);
                t.Margin = new Thickness(18, head ? 11 : 12, 8, head ? 11 : 12);
                UI.At(g, t, i);
            }
            Border b = new Border();
            b.Child = g;
            b.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
            b.SetResourceReference(Border.BorderBrushProperty, "Line");
            if (head) { b.SetResourceReference(Border.BackgroundProperty, "Bg3"); b.CornerRadius = new CornerRadius(13, 13, 0, 0); }
            return b;
        }

        static string AttTime(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            DateTime d;
            if (DateTime.TryParse(raw, out d)) return d.ToString("h:mm");
            return raw.Length > 5 ? raw.Substring(0, 5) : raw;
        }
    }
}
