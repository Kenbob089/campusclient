using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        DateTime calMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selDate = DateTime.Today;
        string schedTerm = "";

        string DayTypeText(DateTime d)
        {
            string t;
            if (!cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out t)) return "Automatic";
            return t == "none" ? "No school" : t;
        }

        void SetDayType(DateTime d, string type)
        {
            string key = d.ToString("yyyy-MM-dd");
            if (type == null) cfg.DayTypes.Remove(key); else cfg.DayTypes[key] = type;
            cfg.Save();
            ShowView(false);
        }

        static string ShortDay(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            int sp = label.IndexOf(' ');
            string w = sp > 0 ? label.Substring(0, sp) : label;
            return w.Length > 3 ? w.Substring(0, 3) : w;
        }

        UIElement ScheduleCalendar()
        {
            StackPanel cal = new StackPanel();
            cal.Width = 360;
            Grid nav = UI.Cols(1, 0, 0);
            nav.Margin = new Thickness(0, 0, 0, 10);
            UI.At(nav, UI.Text(calMonth.ToString("MMMM yyyy"), 16, "T1", true), 0);
            Button prev = UI.IconBtn("", 12, delegate { calMonth = calMonth.AddMonths(-1); ShowView(false); });
            Button nxt = UI.IconBtn("", 12, delegate { calMonth = calMonth.AddMonths(1); ShowView(false); });
            UI.At(nav, prev, 1);
            UI.At(nav, nxt, 2);
            cal.Children.Add(nav);

            System.Windows.Controls.Primitives.UniformGrid heads = new System.Windows.Controls.Primitives.UniformGrid();
            heads.Columns = 7;
            foreach (string d in new string[] { "S", "M", "T", "W", "T", "F", "S" })
            {
                TextBlock t = UI.Text(d, 11.5, "T3", true);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 0, 0, 6);
                heads.Children.Add(t);
            }
            cal.Children.Add(heads);

            System.Windows.Controls.Primitives.UniformGrid days = new System.Windows.Controls.Primitives.UniformGrid();
            days.Columns = 7;
            int lead = (int)calMonth.DayOfWeek;
            DateTime start = calMonth.AddDays(-lead);
            int count = ((lead + DateTime.DaysInMonth(calMonth.Year, calMonth.Month) + 6) / 7) * 7;
            for (int i = 0; i < count; i++)
            {
                DateTime d = start.AddDays(i);
                bool inMonth = d.Month == calMonth.Month;
                bool school = Sched.IsSchoolDay(cfg, snap, d);
                string label = "";
                bool alt = false;
                if (school)
                {
                    string from;
                    label = ShortDay(Sched.LabelFor(cfg, snap, d, out from));
                    string dt;
                    if (cfg.DayTypes.TryGetValue(d.ToString("yyyy-MM-dd"), out dt) && dt != "Regular" && dt != cfg.RegularName) alt = true;
                    if (cfg.DayLetters.ContainsKey(d.ToString("yyyy-MM-dd"))) alt = true;
                }
                bool sel = d.Date == selDate.Date;
                bool today = d.Date == DateTime.Today;
                StackPanel cell = new StackPanel();
                cell.HorizontalAlignment = HorizontalAlignment.Center;
                cell.VerticalAlignment = VerticalAlignment.Center;
                TextBlock num = UI.Text(d.Day.ToString(), 13, sel ? "T1" : (today ? "AccText" : (school ? "T1" : "T3")), sel || today);
                num.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(num);
                TextBlock lb = UI.Text(label.Length > 0 ? label : " ", 10.5, alt ? "Warn" : "AccText", true);
                lb.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(lb);
                Border b = new Border();
                b.Height = 48;
                b.Margin = new Thickness(1);
                b.CornerRadius = new CornerRadius(9);
                b.Child = cell;
                b.Opacity = inMonth ? 1 : 0.4;
                DateTime dd = d;
                Hover(b, sel ? "AccSoft" : "Bg2", sel ? "AccSoft" : "Bg3", delegate
                {
                    selDate = dd;
                    if (dd.Month != calMonth.Month || dd.Year != calMonth.Year) calMonth = new DateTime(dd.Year, dd.Month, 1);
                    ShowView(false);
                });
                if (today && !sel) { b.BorderThickness = new Thickness(1); b.SetResourceReference(Border.BorderBrushProperty, "Acc"); }
                days.Children.Add(b);
            }
            cal.Children.Add(days);
            Button todayBtn = UI.Btn("Today", "Ghost", delegate { selDate = DateTime.Today; calMonth = new DateTime(selDate.Year, selDate.Month, 1); ShowView(false); });
            todayBtn.HorizontalAlignment = HorizontalAlignment.Left;
            todayBtn.Margin = new Thickness(0, 12, 0, 0);
            cal.Children.Add(todayBtn);
            Border calCard = UI.Card(cal, 18);
            calCard.VerticalAlignment = VerticalAlignment.Top;
            return calCard;
        }

        UIElement PageSched()
        {
            List<UIElement> kids = new List<UIElement>();
            Border calCard = (Border)ScheduleCalendar();

            // the picked day
            DayPlan sp = Sched.Plan(cfg, snap, term, selDate);
            DateTime sd = selDate;
            StackPanel right = new StackPanel();
            right.Children.Add(UI.Text(sd.ToString("dddd, MMMM d") + (sp.DayLabel.Length > 0 ? "  ·  " + Sched.DayName(sp.DayLabel) : ""), 18, "T1", true));
            WrapPanel btns = new WrapPanel();
            btns.Margin = new Thickness(0, 12, 0, 12);
            Button dd = UI.Btn("Schedule: " + DayTypeText(sd) + "  ▾", "Ghost", null);
            dd.Margin = new Thickness(0, 0, 10, 0);
            dd.Click += delegate
            {
                string cur = DayTypeText(sd);
                List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
                items.Add(new KeyValuePair<string, RoutedEventHandler>("#This day", null));
                items.Add(new KeyValuePair<string, RoutedEventHandler>((cur == "Automatic" ? "✓  " : "") + "Automatic", delegate { SetDayType(sd, null); }));
                items.Add(new KeyValuePair<string, RoutedEventHandler>((cur == "No school" ? "✓  " : "") + "No school", delegate { SetDayType(sd, "none"); }));
                items.Add(new KeyValuePair<string, RoutedEventHandler>((cur == cfg.RegularName || cur == "Regular" ? "✓  " : "") + cfg.RegularName, delegate { SetDayType(sd, cfg.RegularName); }));
                foreach (GridSet gx in cfg.Sets)
                {
                    string gname = gx.Name;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>((cur == gname ? "✓  " : "") + gname, delegate { SetDayType(sd, gname); }));
                }
                foreach (string vn in new List<string>(cfg.Variants.Keys))
                {
                    string name = vn;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>((cur == name ? "✓  " : "") + name, delegate { SetDayType(sd, name); }));
                }
                items.Add(new KeyValuePair<string, RoutedEventHandler>("-", null));
                items.Add(new KeyValuePair<string, RoutedEventHandler>("#Saved schedules", null));
                items.Add(new KeyValuePair<string, RoutedEventHandler>("Add or edit schedules…", delegate { StartScheduleEdit(); }));
                foreach (string vn in new List<string>(cfg.Variants.Keys))
                {
                    string name = vn;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>("Edit " + name, delegate { EditVariant(name); }));
                    items.Add(new KeyValuePair<string, RoutedEventHandler>("Delete " + name, delegate { DeleteVariant(name); }));
                }
                UI.OpenMenu(dd, UI.Menu(items.ToArray()));
            };
            btns.Children.Add(dd);
            Button chg = UI.Btn("Change times and days", "Ghost", delegate { StartScheduleEdit(); });
            btns.Children.Add(chg);
            Button more = UI.Btn("\u2022\u2022\u2022", "Ghost", null);
            more.Margin = new Thickness(10, 0, 0, 0);
            more.ToolTip = "More";
            more.Click += delegate
            {
                string key = sd.ToString("yyyy-MM-dd");
                string ov;
                bool has = cfg.DayLetters.TryGetValue(key, out ov);
                List<KeyValuePair<string, RoutedEventHandler>> mi = new List<KeyValuePair<string, RoutedEventHandler>>();
                if (Sched.IsSchoolDay(cfg, snap, sd))
                {
                    mi.Add(new KeyValuePair<string, RoutedEventHandler>("#Override day (only " + sd.ToString("MMM d") + ")", null));
                    if (has) mi.Add(new KeyValuePair<string, RoutedEventHandler>("Back to the normal day", delegate { cfg.DayLetters.Remove(key); cfg.Save(); ShowView(false); }));
                    foreach (string lab in Sched.AllLabels(cfg, snap))
                    {
                        string lx = lab;
                        mi.Add(new KeyValuePair<string, RoutedEventHandler>((has && ov == lx ? "\u2713  " : "") + Sched.DayName(lx), delegate { cfg.DayLetters[key] = lx; cfg.Save(); ShowView(false); }));
                    }
                    mi.Add(new KeyValuePair<string, RoutedEventHandler>("-", null));
                }
                mi.Add(new KeyValuePair<string, RoutedEventHandler>("Export schedules", delegate { Go("extract"); }));
                UI.OpenMenu(more, UI.Menu(mi.ToArray()));
            };
            btns.Children.Add(more);
            right.Children.Add(btns);

            List<ItemSpec> rows = new List<ItemSpec>();
            foreach (Slot s in sp.Slots)
            {
                ItemSpec it = new ItemSpec();
                it.Lead = s.B.Label.Length <= 3 ? s.B.Label : "";
                it.Title = s.Title;
                it.Sub = s.B.Text();
                if (s.IsClass) it.Strip = Theme.Tag(s.Title);
                Slot ss = s;
                if (s.C != null) it.Click = delegate { OpenCourse(ss.C); };
                rows.Add(it);
            }
            right.Children.Add(PageKit.ListCard("Classes that day",
                sp.School ? "No schedule set up for this day yet." : (sp.WhyNot.Length > 0 ? sp.WhyNot : "No school."), rows));

            bool wide = CW() >= 900;
            if (wide)
            {
                calCard.Margin = new Thickness(0, 0, 24, 0);
                Grid layout = UI.Cols(0, 1);
                UI.At(layout, calCard, 0);
                UI.At(layout, right, 1);
                kids.Add(layout);
            }
            else
            {
                calCard.Margin = new Thickness(0, 0, 0, 16);
                calCard.HorizontalAlignment = HorizontalAlignment.Left;
                kids.Add(calCard);
                kids.Add(right);
            }

            if (snap.Roster.Count == 0) { kids.Add(PageKit.Notice(Rows.NoData, false)); return Body(kids.ToArray()); }
            kids.Add(PageKit.Section("All classes"));

            if (string.IsNullOrEmpty(schedTerm) || !snap.Terms.Contains(schedTerm)) schedTerm = term;
            Sched.Scope = snap.Roster;
            List<RosterItem> items2 = snap.Roster.FindAll(delegate(RosterItem r) { return Sched.TermMatches(r, schedTerm) && !snap.GoneIn(r.Course, r.SectionId, r.Dropped, r.EndDate, schedTerm); });
            if (items2.Count == 0) items2 = snap.Roster;

            if (snap.Terms.Count > 1)
            {
                List<string> terms = new List<string>();
                for (int i = Math.Max(0, snap.Terms.Count - 6); i < snap.Terms.Count; i++) terms.Add(snap.Terms[i]);
                kids.Add(SegBar(terms.ToArray(), Math.Max(0, terms.IndexOf(schedTerm)), delegate(int k) { schedTerm = terms[k]; Dispatcher.BeginInvoke(new Action(delegate { ShowView(false); })); }));
            }

            BellSchedule bs = cfg.ForDate(DateTime.Today);
            if (bs == null)
            {
                kids.Add(PageKit.Notice("Set up your schedule once and this page shows start and end times and marks what is happening now and next.", false));
                Button setup = UI.Btn("Set up bell schedule", "Pill", delegate { StartScheduleEdit(); });
                setup.HorizontalAlignment = HorizontalAlignment.Left;
                setup.Margin = new Thickness(0, 4, 0, 14);
                kids.Add(setup);
            }

            TimeSpan now = DateTime.Now.TimeOfDay;
            bool school = Sched.IsSchoolDay(cfg, snap, DateTime.Today);
            DayPlan todayPlan = Sched.Plan(cfg, snap, term, DateTime.Today);
            int pcur, pnext;
            Sched.Phase(todayPlan, now, out pcur, out pnext);

            List<RosterItem> shown = new List<RosterItem>();
            foreach (RosterItem ri in items2) if (!ri.Dropped) shown.Add(ri);
            foreach (RosterItem ri in items2) if (ri.Dropped) shown.Add(ri);

            string dayForTimes = todayPlan.DayKey.Length > 0 ? todayPlan.DayKey : todayPlan.DayLabel;
            BellSchedule tsched = bs;
            string dsName;
            if (todayPlan.Sched != null && todayPlan.SetName != cfg.RegularName) tsched = todayPlan.Sched;
            else if (dayForTimes.Length > 0 && cfg.DayScheds.TryGetValue(dayForTimes, out dsName) && cfg.Find(dsName) != null) tsched = cfg.Find(dsName);

            System.Windows.Controls.Primitives.UniformGrid ug = new System.Windows.Controls.Primitives.UniformGrid();
            ug.Columns = Math.Max(1, (CW() + 16) / (330 + 16));
            ug.Margin = new Thickness(0, 0, -16, 0);
            foreach (RosterItem ri in shown)
            {
                string timeText = "", badge = "";
                if (tsched != null && !ri.Dropped)
                {
                    foreach (Bell b in tsched.Entries)
                    {
                        bool nn;
                        if (Sched.ClassFor(cfg, snap, schedTerm, tsched.Name, b, dayForTimes, out nn) != ri) continue;
                        timeText = b.Text();
                        if (school && todayPlan.Slots.Count > 0)
                        {
                            if (pcur >= 0 && todayPlan.Slots[pcur].B == b) badge = "Now";
                            else if (pnext >= 0 && todayPlan.Slots[pnext].B == b) badge = "Next";
                        }
                        break;
                    }
                }
                ug.Children.Add(PeriodCardUi(ri, Sched.CourseOf(snap, ri, schedTerm), timeText, badge));
            }
            kids.Add(ug);
            return Body(kids.ToArray());
        }

        // how a class's period is written: the way Campus names it, or Period 1, Period 2 from your own schedule
        string PeriodText(RosterItem r)
        {
            if (cfg.PeriodNames == 1)
            {
                string slug = Settings.Slug(r.Course);
                List<int> nums = new List<int>();
                foreach (KeyValuePair<string, string> kv in cfg.PeriodMap)
                {
                    if (kv.Value != slug || !kv.Key.EndsWith("#")) continue;
                    int n;
                    if (int.TryParse(kv.Key.Substring(0, kv.Key.Length - 1), out n) && !nums.Contains(n)) nums.Add(n);
                }
                nums.Sort();
                if (nums.Count == 1) return "Period " + nums[0];
                if (nums.Count > 1) return "Periods " + string.Join(", ", nums.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray());
            }
            if (r.Period.Length == 0) return "";
            return Regex.IsMatch(r.Period, @"^\d+$") ? "Period " + r.Period : r.Period;
        }

        UIElement PeriodCardUi(RosterItem r, Course c, string timeText, string badge)
        {
            StackPanel t = new StackPanel();
            string per = PeriodText(r).ToUpperInvariant();
            StackPanel top = new StackPanel();
            top.Orientation = Orientation.Horizontal;
            top.Children.Add(UI.Text(per, 11.5, "T3", true));
            if (r.Dropped) { FrameworkElement ch = UI.Chip("Dropped", "BadSoft", "Bad"); ch.Margin = new Thickness(10, 0, 0, 0); top.Children.Add(ch); }
            else if (badge.Length > 0)
            {
                FrameworkElement ch = badge == "Now" ? UI.Chip(badge, "Acc", "AccOn") : UI.Chip(badge, "AccSoft", "AccText");
                ch.Margin = new Thickness(10, 0, 0, 0);
                top.Children.Add(ch);
            }
            if (timeText.Length > 0) { TextBlock tt = UI.Text(timeText, 12, "T2", true); tt.Margin = new Thickness(10, 0, 0, 0); top.Children.Add(tt); }
            t.Children.Add(top);
            TextBlock nm = UI.Text(r.Course, 16, r.Dropped ? "T3" : "T1", true);
            nm.TextTrimming = TextTrimming.CharacterEllipsis;
            nm.Margin = new Thickness(0, 6, 0, 6);
            t.Children.Add(nm);
            string sub = r.Teacher ?? "";
            if (!string.IsNullOrEmpty(r.Room)) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + r.Room;
            if (r.Dropped && r.EndDate.HasValue) sub = "Dropped " + r.EndDate.Value.ToString("MMM d") + (sub.Length > 0 ? "  ·  " + sub : "");
            Grid foot = UI.Cols(1, 0);
            TextBlock st = UI.Text(sub, 12.5, r.Dropped ? "T3" : "T2", false);
            st.TextTrimming = TextTrimming.CharacterEllipsis;
            UI.At(foot, st, 0);
            if (c != null && (c.Percent.HasValue || c.Letter.Length > 0))
            {
                FrameworkElement g = UI.Chip(Theme.GradeStr(c.Letter, c.Percent), "Bg4", "T1");
                ((TextBlock)((Border)g).Child).Foreground = new SolidColorBrush(Theme.GradeColor(c.Percent));
                g.Margin = new Thickness(8, 0, 0, 0);
                UI.At(foot, g, 1);
            }
            t.Children.Add(foot);
            Grid wrap = UI.Cols(0, 1);
            Border bar = new Border();
            bar.Width = 5; bar.CornerRadius = new CornerRadius(3); bar.Margin = new Thickness(0, 0, 14, 0);
            bar.Background = new SolidColorBrush(Theme.Tag(r.Course));
            UI.At(wrap, bar, 0);
            UI.At(wrap, t, 1);
            Border card = UI.Card(wrap, 18);
            card.Margin = new Thickness(0, 0, 16, 16);
            Course cc = c;
            if (cc != null) { Hover(card, "Bg2", "Bg3", delegate { OpenCourse(cc); }); UI.Lift(card); }
            return card;
        }
    }
}
