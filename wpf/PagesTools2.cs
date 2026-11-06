using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        UIElement Pair(UIElement a, UIElement b)
        {
            Grid g = UI.Cols(1, 1);
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 8, 16);
            ((FrameworkElement)b).Margin = new Thickness(8, 0, 0, 16);
            UI.At(g, a, 0); UI.At(g, b, 1);
            return g;
        }

        // ------------------------------------------------------------ countdown
        UIElement CountdownCard()
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading("Countdown"));
            DateTime today = DateTime.Today;
            HashSet<DateTime> school = new HashSet<DateTime>();
            DateTime lastDay = today;
            if (snap != null) foreach (CalDay d in snap.CalDays) { if (d.School) { school.Add(d.Date.Date); if (d.Date.Date > lastDay) lastDay = d.Date.Date; } }
            DateTime[] tr;
            if (snap != null && snap.TermDates.TryGetValue(term, out tr) && tr[1].Date >= today)
            {
                int left = 0;
                foreach (DateTime d in school) if (d >= today && d <= tr[1].Date) left++;
                p.Children.Add(CountLine(left > 0 ? left.ToString() : ((int)(tr[1].Date - today).TotalDays).ToString(), (left > 0 ? "school days left in " : "days left in ") + term, "ends " + tr[1].ToString("MMM d")));
            }
            if (school.Count > 0 && lastDay > today)
            {
                int left2 = 0;
                foreach (DateTime d in school) if (d >= today) left2++;
                p.Children.Add(CountLine(left2.ToString(), "school days left this year", "last day " + lastDay.ToString("MMM d")));
                for (DateTime d = today.AddDays(1); d < lastDay; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || school.Contains(d)) continue;
                    p.Children.Add(CountLine(((int)(d - today).TotalDays).ToString(), (((int)(d - today).TotalDays) == 1 ? "day" : "days") + " until the next day off", d.ToString("dddd, MMM d")));
                    break;
                }
            }
            if (p.Children.Count == 1) p.Children.Add(UI.Margin(UI.Text("The school calendar has not loaded yet.", 13.5, "T3", false), 0, 12, 0, 0));
            return UI.Card(p, 20);
        }

        UIElement CountLine(string big, string label, string sub)
        {
            Grid g = UI.Cols(0, 1);
            g.Margin = new Thickness(0, 14, 0, 0);
            TextBlock n = UI.Text(big, 30, "AccText", true);
            n.MinWidth = 70;
            n.VerticalAlignment = VerticalAlignment.Center;
            UI.At(g, n, 0);
            StackPanel s = new StackPanel();
            s.VerticalAlignment = VerticalAlignment.Center;
            s.Children.Add(UI.Text(label, 13.5, "T1", true));
            s.Children.Add(UI.Text(sub, 12, "T3", false));
            UI.At(g, s, 1);
            return g;
        }

    }
}
