using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CampusMidnight
{
    // The guided setup: bell times and rotating days, which class is in which period, look, extras.
    // Runs after the first sign-in and can be started again from Settings or the Schedule page.
    sealed partial class MainWindow
    {
        const int SuVersion = 7;
        const int SuSteps = 5;
        int suStep;
        bool suFirstRun, suEdit;
        string suError = "";
        ScrollViewer suScroll;
        double suScrollY;

        // other parts of the app can hook these (tray reminders, toast notifications); without an implementation the calls vanish
        partial void SuTrayChanged();
        partial void SuToast(string title, string body);

        // ------------------------------------------------------------ entry points
        public bool NeedsSetup()
        {
            return !cfg.SetupDone || cfg.SetupVer < SuVersion;
        }

        void SuInit()
        {
            suStep = 0;
            suEdit = false;
            suError = "";
            suScrollY = 0;
            suHist.Clear();
            SuGridLoad();
            Log.Write("setup started (first run: " + suFirstRun + ")");
        }

        public void StartSetup(bool firstRun)
        {
            suFirstRun = firstRun;
            SuInit();
            view = "setup";
            ShowView(true);
        }

        void StartSetupAt(int step)
        {
            suFirstRun = false;
            SuInit();
            suEdit = false;
            suStep = step;
            view = "setup";
            ShowView(true);
        }

        // change the schedule without going through the whole first-run setup
        public void StartScheduleEdit()
        {
            suFirstRun = false;
            SuInit();
            suEdit = true;
            view = "setup";
            ShowView(true);
        }

        void SuFinishEdit()
        {
            suEdit = false;
            suStep = 0;
            suScrollY = 0;
            cfg.Save();
            view = "schedule";
            ShowView(true);
        }

        void SuFinish(bool completed)
        {
            cfg.SetupDone = true;
            cfg.SetupVer = SuVersion;
            cfg.Save();
            Log.Write("setup " + (completed ? "finished" : "skipped") + " at step " + suStep);
            suFirstRun = false;
            suStep = 0;
            suScrollY = 0;
            view = "dashboard";
            ShowView(true);
        }

        // rebuild the current page; keep the scroll position unless the step changed
        void SuRedo(bool keepScroll)
        {
            if (keepScroll && suScroll != null && suError.Length == 0) suScrollY = suScroll.VerticalOffset; else suScrollY = 0;
            ShowView(false);
        }

        // ------------------------------------------------------------ the page
        UIElement PageSetup()
        {
            if (snap == null) return Center("Loading your classes first…");
            SuRemember();
            List<UIElement> kids = new List<UIElement>();
            switch (suStep)
            {
                case 0: SuPageGrid(kids); break;
                case 1: SuPageClasses(kids); break;
                case 2: SuPageLook(kids); break;
                case 3: SuPageExtras(kids); break;
                default: SuPageDone(kids); break;
            }
            return SuPage(kids);
        }

        UIElement SuPage(List<UIElement> kids)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(30, 10, 30, 30);
            p.MaxWidth = 1120;
            p.HorizontalAlignment = HorizontalAlignment.Left;
            foreach (UIElement k in kids) p.Children.Add(k);
            ScrollViewer sv = UI.Scroll(p);
            suScroll = sv;
            double y = suScrollY;
            if (y > 0) sv.Loaded += delegate { sv.UpdateLayout(); sv.ScrollToVerticalOffset(y); };
            if (suError.Length == 0) return sv;
            // the error sits above the scrolling area so it is always in view
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            UIElement n = SuNotice(suError, true);
            ((FrameworkElement)n).Margin = new Thickness(30, 10, 30, 4);
            Grid.SetRow(n, 0);
            Grid.SetRow(sv, 1);
            g.Children.Add(n);
            g.Children.Add(sv);
            return g;
        }

        // ------------------------------------------------------------ small building blocks
        UIElement SuHeader(string title, string sub)
        {
            StackPanel p = new StackPanel();
            p.Margin = new Thickness(0, 0, 0, 18);
            p.Children.Add(UI.Text(title, 24, "T1", true));
            if (!string.IsNullOrEmpty(sub))
            {
                TextBlock s = UI.Wrap(sub, 13.5, "T2");
                s.Margin = new Thickness(0, 6, 0, 0);
                s.MaxWidth = 780;
                s.HorizontalAlignment = HorizontalAlignment.Left;
                p.Children.Add(s);
            }
            return p;
        }

        void SuHead(List<UIElement> kids, string title, string sub)
        {
            if (suEdit)
            {
                kids.Add(SuHeader(suStep == 0 ? "Schedule setup" : "Classes by period", suStep == 0 ? "" : sub));
                return;
            }
            TextBlock step = UI.Text("STEP " + (suStep + 1) + " OF " + SuSteps, 12, "AccText", true);
            step.Margin = new Thickness(0, 0, 0, 8);
            kids.Add(step);
            FrameworkElement bar = (FrameworkElement)UI.Meter((suStep + 1) * 100.0 / SuSteps, Theme.Acc);
            bar.Margin = new Thickness(0, 0, 0, 20);
            kids.Add(bar);
            kids.Add(SuHeader(title, sub));
        }

        UIElement SuSection(string title)
        {
            TextBlock t = UI.Text(title, 15, "T1", true);
            t.Margin = new Thickness(0, 22, 0, 10);
            return t;
        }

        UIElement SuNote(string text)
        {
            TextBlock t = UI.Wrap(text, 12.5, "T3");
            t.Margin = new Thickness(0, 6, 0, 8);
            t.MaxWidth = 780;
            t.HorizontalAlignment = HorizontalAlignment.Left;
            return t;
        }

        UIElement SuNotice(string text, bool bad)
        {
            TextBlock t = UI.Wrap(text, 13, bad ? "Bad" : "AccText");
            Border b = new Border();
            b.CornerRadius = new CornerRadius(10);
            b.Padding = new Thickness(14, 10, 14, 10);
            b.Margin = new Thickness(0, 6, 0, 12);
            b.SetResourceReference(Border.BackgroundProperty, bad ? "BadSoft" : "AccDeep");
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Child = t;
            return b;
        }

        CheckBox SuToggle(string text, bool on, Action<bool> set)
        {
            CheckBox c = new CheckBox();
            c.Content = text;
            c.IsChecked = on;
            c.Margin = new Thickness(0, 6, 0, 6);
            c.Click += delegate { set(c.IsChecked == true); };
            return c;
        }

        // a row of connected choices: the selected one is filled
        UIElement SuSeg(string[] names, int sel, Action<int> changed)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Left;
            List<Button> btns = new List<Button>();
            for (int i = 0; i < names.Length; i++)
            {
                int ix = i;
                Button b = UI.Btn(names[i], i == sel ? "Pill" : "Ghost", null);
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Click += delegate
                {
                    for (int j = 0; j < btns.Count; j++) btns[j].Style = (Style)Application.Current.Resources[j == ix ? "Pill" : "Ghost"];
                    changed(ix);
                };
                btns.Add(b);
                row.Children.Add(b);
            }
            return row;
        }

        UIElement SuField(string caption, string value, double width, Action<string> set)
        {
            StackPanel p = new StackPanel();
            p.Width = width;
            p.Margin = new Thickness(0, 0, 14, 0);
            p.Children.Add(UI.Margin(UI.Text(caption.ToUpperInvariant(), 11.5, "T3", true), 2, 0, 0, 6));
            TextBox box = new TextBox();
            box.Text = value;
            box.TextChanged += delegate { set(box.Text); };
            p.Children.Add(box);
            return p;
        }

        void SuFooter(List<UIElement> kids, string nextText, Func<bool> onNext)
        {
            WrapPanel row = new WrapPanel();
            row.Margin = new Thickness(0, 14, 0, 0);
            if (suStep > 0)
            {
                Button back = UI.Btn("Back", "Ghost", delegate { suStep--; suError = ""; SuRedo(false); });
                back.Margin = new Thickness(0, 0, 10, 0);
                row.Children.Add(back);
            }
            Button next = UI.Btn(suEdit ? "Save" : nextText, "Pill", delegate
            {
                suError = "";
                if (onNext())
                {
                    if (suEdit) { SuFinishEdit(); return; }
                    suStep++;
                    SuRedo(false);
                }
                else SuRedo(true);
            });
            next.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(next);
            if (suEdit && suStep == 0)
            {
                Button more = UI.Btn("Save and set classes", "Ghost", delegate
                {
                    suError = "";
                    if (onNext()) { suStep++; SuRedo(false); }
                    else SuRedo(true);
                });
                more.Margin = new Thickness(0, 0, 10, 0);
                row.Children.Add(more);
            }
            Button cancel = UI.Btn(suFirstRun ? "Skip setup" : "Cancel", "Flat", delegate
            {
                if (suFirstRun) SuFinish(false);
                else
                {
                    view = suEdit ? "schedule" : "settings";
                    suEdit = false;
                    suStep = 0;
                    suError = "";
                    SuRedo(false);
                }
            });
            cancel.Padding = new Thickness(16, 9, 16, 9);
            cancel.FontSize = 13;
            row.Children.Add(cancel);
            kids.Add(row);
        }

        string[] SuCampusLabels()
        {
            List<string> l = new List<string>();
            if (snap != null)
            {
                List<CalDay> days = new List<CalDay>(snap.CalDays);
                days.Sort(delegate(CalDay a, CalDay b) { return a.Date.CompareTo(b.Date); });
                foreach (CalDay c in days) if (c.Label.Length > 0 && !l.Contains(c.Label)) l.Add(c.Label);
            }
            return l.ToArray();
        }

        List<string> SuTermCourses()
        {
            List<string> names = new List<string>();
            if (snap != null)
                foreach (RosterItem r in snap.Roster)
                    if (!r.Dropped && Sched.TermMatches(r, term) && !names.Contains(r.Course)) names.Add(r.Course);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        static string SuCourseByKey(List<string> courses, string key)
        {
            foreach (string c in courses) if (Settings.Slug(c) == key) return c;
            return null;
        }

        // ------------------------------------------------------------ step 2: classes
        // each numbered period of the grid gets one class. Only the Auto fill button reads the class placements from Campus.
        void SuPageClasses(List<UIElement> kids)
        {
            List<string> nums = SuGridNumbers();
            SuHead(kids, "Which class is in which period?", "Pick the class for each period number. It applies on every letter day. Nothing is filled in unless you press Auto fill.");
            if (nums.Count == 0)
            {
                kids.Add(SuNotice("Your grid has no numbered periods yet. Go back and put period numbers in the circles.", false));
                SuFooter(kids, "Next", delegate { return true; });
                return;
            }
            if (snap != null && snap.Terms.Count > 1)
            {
                // a class that only runs one semester has its own entry for that term, so pick the term here
                kids.Add(UI.Margin(UI.Text("QUARTER OR SEMESTER", 11.5, "T3", true), 2, 4, 0, 6));
                string[] tn = snap.Terms.ToArray();
                kids.Add(UI.Margin((FrameworkElement)SuSeg(tn, Math.Max(0, Array.IndexOf(tn, term)), delegate(int k) { term = tn[k]; SuRedo(true); }), 0, 0, 0, 12));
                kids.Add(SuNote("A period can have a different class in each quarter or semester. Pick the term, then the class for it."));
            }
            List<string> courses = SuTermCourses();
            if (courses.Count == 0) kids.Add(SuNotice("No classes were found for this term, so there is nothing to pick yet.", false));
            Button auto = UI.Btn("Auto fill from Campus", "Pill", delegate { SuAutoFill(); SuRedo(true); });
            auto.HorizontalAlignment = HorizontalAlignment.Left;
            auto.Margin = new Thickness(0, 0, 0, 12);
            kids.Add(auto);
            if (suAutoNote.Length > 0) kids.Add(SuNotice(suAutoNote, false));

            StackPanel list = new StackPanel();
            foreach (string num in nums)
            {
                string pnum = num;
                Grid row = UI.Cols(0, 1);
                row.Margin = new Thickness(0, 6, 0, 6);
                TextBlock lbl = UI.Text("Period " + pnum + "   " + SuGridTimesFor(pnum), 14, "T1", false);
                lbl.Width = 330;
                lbl.VerticalAlignment = VerticalAlignment.Center;
                UI.At(row, lbl, 0);

                string cur;
                cfg.PeriodMap.TryGetValue(Settings.PeriodKey("", pnum, ""), out cur);
                cur = cur ?? "";
                List<string> termSlugs = courses.ConvertAll<string>(delegate(string cn0) { return Settings.Slug(cn0); });
                string mine = "";   // the class of this term in this period
                foreach (string tk in cur.Split('+')) if (termSlugs.Contains(tk)) { mine = tk; break; }
                if (mine.Length == 0 && Settings.HasSlug(cur, "-")) mine = "-";
                cur = mine;
                string txt;
                if (cur == "-") txt = "Free period";
                else if (cur.Length > 0) txt = SuCourseByKey(courses, cur) ?? "Pick a class";
                else txt = "Not set";
                Button pb = UI.Btn(txt + "  ▾", cur.Length > 0 ? "Pill" : "Ghost", null);
                pb.HorizontalContentAlignment = HorizontalAlignment.Left;
                int curIdx = cur == "-" ? 1 : (cur.Length > 0 ? Math.Max(0, courses.FindIndex(delegate(string cn) { return Settings.Slug(cn) == cur; }) + 2) : 0);
                pb.Click += delegate
                {
                    string[] opts = new string[courses.Count + 2];
                    opts[0] = "Not set";
                    opts[1] = "Free period / no class";
                    for (int c = 0; c < courses.Count; c++) opts[c + 2] = courses[c];
                    SuPick(pb, opts, curIdx, delegate(int k)
                    {
                        string key = Settings.PeriodKey("", pnum, "");
                        string all;
                        cfg.PeriodMap.TryGetValue(key, out all);
                        string nv = Settings.WithSlug(all, termSlugs, k == 0 ? null : (k == 1 ? "-" : Settings.Slug(courses[k - 2])));
                        if (nv.Length == 0) cfg.PeriodMap.Remove(key); else cfg.PeriodMap[key] = nv;
                        cfg.Save();
                        SuRedo(true);
                    });
                };
                UI.At(row, pb, 1);
                list.Children.Add(row);
            }
            kids.Add(UI.Card(list, 18));
            SuFooter(kids, "Next", delegate { cfg.Save(); return true; });
        }

        // ------------------------------------------------------------ step 3: look
        void SuPageLook(List<UIElement> kids)
        {
            SuHead(kids, suFirstRun ? "Welcome to Noctua" : "Look and feel", "A few quick steps and your schedule, grades and reminders will be ready. You can change all of it later in Settings.");
            kids.Add(SuSection("Light or dark"));
            string[] modeNames = new string[] { "Match Windows", "Light", "Dark" };
            string[] modeKeys = new string[] { "system", "light", "dark" };
            kids.Add(SuSeg(modeNames, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)), delegate(int k)
            {
                cfg.Mode = modeKeys[k];
                cfg.Save();
                ApplyTheme();
                ShowShell();
            }));

            kids.Add(SuSection("Accent colour"));
            string[] names = new string[Theme.All.Count];
            for (int i = 0; i < names.Length; i++) names[i] = Theme.All[i].Shown(Theme.Dark);
            Button pick = UI.Btn(Theme.Find(Theme.Current).Shown(Theme.Dark) + "  ▾", "Ghost", null);
            pick.HorizontalAlignment = HorizontalAlignment.Left;
            pick.Click += delegate
            {
                SuPick(pick, names, Array.IndexOf(names, Theme.Find(Theme.Current).Shown(Theme.Dark)), delegate(int k)
                {
                    cfg.Theme = names[k];
                    cfg.Save();
                    ApplyTheme();
                    ShowShell();
                });
            };
            kids.Add(pick);
            SuFooter(kids, "Next", delegate { return true; });
        }

        // ------------------------------------------------------------ step 4: extras
        void SuPageExtras(List<UIElement> kids)
        {
            SuHead(kids, "Extras", "A few switches. Everything here can be changed later in Settings.");
            kids.Add(SuSection("Grades"));
            kids.Add(SuToggle("Count dropped classes in my GPA", cfg.CountDropped, delegate(bool v) { cfg.CountDropped = v; cfg.Save(); }));
            kids.Add(SuToggle("Show a rounded number (92) instead of a letter grade (A-)", cfg.NumbersOnly, delegate(bool v) { cfg.NumbersOnly = v; Theme.NumbersOnly = v; cfg.Save(); }));
            kids.Add(SuSection("Reminders"));
            kids.Add(SuToggle("Notify me before a class starts", cfg.NotifyStart, delegate(bool v) { cfg.NotifyStart = v; cfg.Save(); SuTrayChanged(); }));
            kids.Add(SuToggle("Notify me before a class ends", cfg.NotifyEnd, delegate(bool v) { cfg.NotifyEnd = v; cfg.Save(); SuTrayChanged(); }));
            kids.Add(SuToggle("Keep running in the system tray when I close the window", cfg.TrayOnClose, delegate(bool v) { cfg.TrayOnClose = v; cfg.Save(); SuTrayChanged(); }));
            int[] leads = new int[] { 0, 1, 2, 5, 10, 15 };
            UIElement lead = SuSeg(new string[] { "On time", "1 min", "2 min", "5 min", "10 min", "15 min" }, Math.Max(0, Array.IndexOf(leads, cfg.NotifyLead)), delegate(int k) { cfg.NotifyLead = leads[k]; cfg.Save(); });
            ((FrameworkElement)lead).Margin = new Thickness(0, 10, 0, 0);
            kids.Add(lead);
            kids.Add(SuSection("Honors and AP"));
            TextBlock ap = UI.Wrap("Weighted GPA guesses honors and AP from the class name. Open any class on the Grades page to correct it.", 13.5, "T2");
            ap.HorizontalAlignment = HorizontalAlignment.Left;
            ap.MaxWidth = 780;
            kids.Add(ap);
            SuFooter(kids, "Next", delegate { return true; });
        }

        // ------------------------------------------------------------ step 5: done
        void SuPageDone(List<UIElement> kids)
        {
            SuHead(kids, "You are set", "Here is how a typical day looks now. Change anything later under Settings, then Schedule setup.");
            DayPlan p = Sched.Plan(cfg, snap, term, DateTime.Today);
            if (!p.School) p = Sched.Plan(cfg, snap, term, Sched.NextSchoolDay(cfg, snap, DateTime.Today));
            StackPanel list = new StackPanel();
            list.Children.Add(Heading(p.Date.ToString("dddd") + (p.DayLabel.Length > 0 ? "  ·  " + Sched.DayName(p.DayLabel) : "")));
            if (p.Slots.Count == 0) list.Children.Add(UI.Text("No periods yet. Go back to the schedule step.", 13.5, "T3", false));
            foreach (Slot s in p.Slots) list.Children.Add(SlotRow(s, false));
            kids.Add(UI.Card(list, 20));
            WrapPanel row = new WrapPanel();
            row.Margin = new Thickness(0, 20, 0, 0);
            Button fin = UI.Btn("Finish", "Pill", delegate { SuFinish(true); });
            fin.Margin = new Thickness(0, 0, 10, 0);
            Button back = UI.Btn("Back", "Ghost", delegate { suStep--; SuRedo(false); });
            row.Children.Add(fin);
            row.Children.Add(back);
            kids.Add(row);
        }
    }
}
