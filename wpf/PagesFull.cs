using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CampusMidnight
{
    // Full screen mode for the Now page (F11): a big time-left card, a corner menu, an info widget and the day's schedule below
    sealed partial class MainWindow
    {
        bool fsOn, fsSched;
        Grid fsRoot, fsStage, fsPage1, fsPage2;
        TextBlock fsClock;
        ContentControl fsHeroHost, fsTilesHost, fsTimelineHost;
        Border fsMenuCard, fsInfoCard, fsArrow, fsHint, fsInfoBtn, fsMenuBtn;
        TextBlock fsArrowGlyph, fsInfoLabel;
        RingMeter fsRing;
        DispatcherTimer fsTimer;
        string fsKey = "";
        DateTime fsEscDown = DateTime.MinValue;
        WindowStyle fsPrevStyle; WindowState fsPrevState; ResizeMode fsPrevResize;

        static readonly FontFamily FsIcons = new FontFamily("Segoe MDL2 Assets");

        // returns true when the key was used
        bool FsKey(KeyEventArgs ke)
        {
            if (ke.Key == Key.F11)
            {
                if (fsOn) ExitFs();
                else if (view == "now") EnterFs();
                else return false;
                return true;
            }
            if (fsOn && ke.Key == Key.Escape)
            {
                if (!ke.IsRepeat) fsEscDown = DateTime.Now;
                else if ((DateTime.Now - fsEscDown).TotalMilliseconds >= 600) ExitFs();
                return true;
            }
            return false;
        }

        Border FsCircle(string glyph, double size)
        {
            Border b = new Border();
            b.Width = size; b.Height = size; b.CornerRadius = new CornerRadius(size / 2);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.SetResourceReference(Border.BackgroundProperty, "Bg2");
            b.SetResourceReference(Border.BorderBrushProperty, "Line2");
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = Theme.ShadowOp, Color = Theme.Dark ? Colors.Black : Color.FromRgb(40, 50, 75) };
            TextBlock t = new TextBlock();
            t.Text = glyph; t.FontFamily = FsIcons; t.FontSize = size * 0.4;
            t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            t.SetResourceReference(TextBlock.ForegroundProperty, "T1");
            b.Child = t;
            b.MouseEnter += delegate { b.SetResourceReference(Border.BackgroundProperty, "Bg3"); };
            b.MouseLeave += delegate { b.SetResourceReference(Border.BackgroundProperty, "Bg2"); };
            return b;
        }

        Border FsPanelCard(UIElement child)
        {
            Border c = new Border();
            c.CornerRadius = new CornerRadius(16);
            c.BorderThickness = new Thickness(1);
            c.Padding = new Thickness(14);
            c.SetResourceReference(Border.BackgroundProperty, "Bg2");
            c.SetResourceReference(Border.BorderBrushProperty, "Line2");
            c.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = Theme.ShadowOp, Color = Theme.Dark ? Colors.Black : Color.FromRgb(40, 50, 75) };
            c.Child = child;
            return c;
        }

        // pops a card in or out from a corner
        void FsPop(Border card, bool show)
        {
            if (show)
            {
                card.Visibility = Visibility.Visible;
                ScaleTransform st = new ScaleTransform(0.8, 0.8);
                card.RenderTransform = st;
                CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
                card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            }
            else if (card.Visibility == Visibility.Visible)
            {
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(130));
                fade.Completed += delegate { card.Visibility = Visibility.Collapsed; };
                card.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }

        void FsCloseCards()
        {
            if (fsMenuCard != null) FsPop(fsMenuCard, false);
            if (fsInfoCard != null) FsPop(fsInfoCard, false);
        }

        void EnterFs()
        {
            if (fsOn || snap == null || cfg.Schedules.Count == 0) return;
            fsOn = true; fsSched = false; fsKey = "";
            fsPrevStyle = WindowStyle; fsPrevState = WindowState; fsPrevResize = ResizeMode;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;

            fsRoot = new Grid();
            fsRoot.SetResourceReference(Grid.BackgroundProperty, "Bg0");
            Panel.SetZIndex(fsRoot, 300);
            Grid.SetColumnSpan(fsRoot, 2);
            Grid.SetRowSpan(fsRoot, 2);

            fsStage = new Grid();
            fsStage.ClipToBounds = true;
            fsPage1 = new Grid(); fsPage2 = new Grid();
            fsPage1.RenderTransform = new TranslateTransform(0, 0);
            fsPage2.RenderTransform = new TranslateTransform(0, 0);
            fsHeroHost = new ContentControl();
            fsHeroHost.HorizontalAlignment = HorizontalAlignment.Center;
            fsHeroHost.VerticalAlignment = VerticalAlignment.Center;
            fsHeroHost.Margin = new Thickness(60, 40, 60, 100);
            fsPage1.Children.Add(fsHeroHost);
            fsTimelineHost = new ContentControl();
            fsTimelineHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            fsTimelineHost.VerticalAlignment = VerticalAlignment.Stretch;
            fsTimelineHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            fsTimelineHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            fsTimelineHost.Margin = new Thickness(70, 90, 70, 100);
            fsPage2.Children.Add(fsTimelineHost);
            fsStage.Children.Add(fsPage1);
            fsStage.Children.Add(fsPage2);
            fsStage.SizeChanged += delegate { FsPlace(false); };
            fsRoot.Children.Add(fsStage);

            fsTilesHost = new ContentControl();

            // bottom left: menu
            fsMenuBtn = FsCircle("", 48);
            fsMenuBtn.HorizontalAlignment = HorizontalAlignment.Left; fsMenuBtn.VerticalAlignment = VerticalAlignment.Bottom;
            fsMenuBtn.Margin = new Thickness(24, 0, 0, 24);
            fsMenuBtn.MouseLeftButtonUp += delegate
            {
                bool open = fsMenuCard.Visibility == Visibility.Visible;
                FsCloseCards();
                if (!open) FsPop(fsMenuCard, true);
            };
            Button exit = UI.Btn("Exit Fullscreen", "Ghost", delegate { ExitFs(); });
            Button setBtn = UI.Btn("Settings", "Ghost", delegate { FsCloseCards(); FsSettings(); });
            setBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            exit.HorizontalAlignment = HorizontalAlignment.Stretch;
            exit.Margin = new Thickness(0, 6, 0, 0);
            fsMenuCard = FsPanelCard(UI.Col(setBtn, exit));
            fsMenuCard.HorizontalAlignment = HorizontalAlignment.Left; fsMenuCard.VerticalAlignment = VerticalAlignment.Bottom;
            fsMenuCard.Margin = new Thickness(24, 0, 0, 82);
            fsMenuCard.RenderTransformOrigin = new Point(0, 1);
            fsMenuCard.Visibility = Visibility.Collapsed;

            // bottom right: info
            fsInfoLabel = new TextBlock();
            fsInfoLabel.Text = "Show additional info"; fsInfoLabel.FontSize = 13.5; fsInfoLabel.FontWeight = FontWeights.SemiBold;
            fsInfoLabel.Margin = new Thickness(18, 0, 4, 0); fsInfoLabel.VerticalAlignment = VerticalAlignment.Center;
            fsInfoLabel.Visibility = Visibility.Collapsed;
            fsInfoLabel.SetResourceReference(TextBlock.ForegroundProperty, "T1");
            TextBlock iT = new TextBlock();
            iT.Text = "i"; iT.FontSize = 20; iT.FontWeight = FontWeights.SemiBold; iT.FontStyle = FontStyles.Italic;
            iT.Width = 46; iT.TextAlignment = TextAlignment.Center; iT.VerticalAlignment = VerticalAlignment.Center;
            iT.SetResourceReference(TextBlock.ForegroundProperty, "T1");
            StackPanel iRow = new StackPanel(); iRow.Orientation = Orientation.Horizontal;
            iRow.Children.Add(fsInfoLabel); iRow.Children.Add(iT);
            fsInfoBtn = new Border();
            fsInfoBtn.Height = 48; fsInfoBtn.MinWidth = 48; fsInfoBtn.CornerRadius = new CornerRadius(24);
            fsInfoBtn.BorderThickness = new Thickness(1); fsInfoBtn.Cursor = Cursors.Hand;
            fsInfoBtn.SetResourceReference(Border.BackgroundProperty, "Bg2");
            fsInfoBtn.SetResourceReference(Border.BorderBrushProperty, "Line2");
            fsInfoBtn.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = Theme.ShadowOp, Color = Theme.Dark ? Colors.Black : Color.FromRgb(40, 50, 75) };
            fsInfoBtn.Child = iRow;
            fsInfoBtn.HorizontalAlignment = HorizontalAlignment.Right; fsInfoBtn.VerticalAlignment = VerticalAlignment.Bottom;
            fsInfoBtn.Margin = new Thickness(0, 0, 24, 24);
            fsInfoBtn.MouseEnter += delegate
            {
                fsInfoLabel.Visibility = Visibility.Visible;
                fsInfoLabel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                fsInfoBtn.SetResourceReference(Border.BackgroundProperty, "Bg3");
            };
            fsInfoBtn.MouseLeave += delegate { fsInfoLabel.Visibility = Visibility.Collapsed; fsInfoBtn.SetResourceReference(Border.BackgroundProperty, "Bg2"); };
            fsInfoBtn.MouseLeftButtonUp += delegate
            {
                bool open = fsInfoCard.Visibility == Visibility.Visible;
                FsCloseCards();
                if (!open) FsPop(fsInfoCard, true);
            };
            fsTilesHost.MinWidth = 440;
            fsInfoCard = FsPanelCard(fsTilesHost);
            fsInfoCard.HorizontalAlignment = HorizontalAlignment.Right; fsInfoCard.VerticalAlignment = VerticalAlignment.Bottom;
            fsInfoCard.Margin = new Thickness(0, 0, 24, 82);
            fsInfoCard.RenderTransformOrigin = new Point(1, 1);
            fsInfoCard.Visibility = Visibility.Collapsed;

            // bottom middle: arrow to the schedule
            fsArrowGlyph = new TextBlock();
            fsArrowGlyph.Text = ""; fsArrowGlyph.FontFamily = FsIcons; fsArrowGlyph.FontSize = 18;
            fsArrowGlyph.HorizontalAlignment = HorizontalAlignment.Center; fsArrowGlyph.VerticalAlignment = VerticalAlignment.Center;
            fsArrowGlyph.SetResourceReference(TextBlock.ForegroundProperty, "T1");
            fsArrow = FsCircle("", 46);
            fsArrow.Child = fsArrowGlyph;
            fsArrow.HorizontalAlignment = HorizontalAlignment.Center; fsArrow.VerticalAlignment = VerticalAlignment.Bottom;
            fsArrow.Margin = new Thickness(0, 0, 0, 24);
            fsArrow.MouseLeftButtonUp += delegate { FsToggleSched(); };

            // hint
            TextBlock ht = new TextBlock();
            ht.Text = "To exit full screen press, hold [esc] or press [F11]"; ht.FontSize = 13;
            ht.SetResourceReference(TextBlock.ForegroundProperty, "T2");
            fsHint = new Border();
            fsHint.CornerRadius = new CornerRadius(18); fsHint.Padding = new Thickness(18, 9, 18, 9);
            fsHint.BorderThickness = new Thickness(1);
            fsHint.SetResourceReference(Border.BackgroundProperty, "Bg2");
            fsHint.SetResourceReference(Border.BorderBrushProperty, "Line2");
            fsHint.Child = ht;
            fsHint.HorizontalAlignment = HorizontalAlignment.Center; fsHint.VerticalAlignment = VerticalAlignment.Bottom;
            fsHint.Margin = new Thickness(0, 0, 0, 84);
            fsHint.IsHitTestVisible = false;
            fsHint.Opacity = 0;

            fsClock = new TextBlock();
            fsClock.FontSize = 30; fsClock.FontWeight = FontWeights.SemiBold;
            fsClock.VerticalAlignment = VerticalAlignment.Top;
            fsClock.Margin = new Thickness(36, 26, 36, 0);
            fsClock.IsHitTestVisible = false;
            fsClock.SetResourceReference(TextBlock.ForegroundProperty, "T2");
            FsClockPlace();
            fsRoot.Children.Add(fsClock);
            fsRoot.Children.Add(fsMenuBtn); fsRoot.Children.Add(fsMenuCard);
            fsRoot.Children.Add(fsInfoBtn); fsRoot.Children.Add(fsInfoCard);
            fsRoot.Children.Add(fsArrow); fsRoot.Children.Add(fsHint);
            // clicking empty space closes the little cards
            fsRoot.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                DependencyObject o = e.OriginalSource as DependencyObject;
                while (o != null) { if (o == fsMenuCard || o == fsInfoCard || o == fsMenuBtn || o == fsInfoBtn) return; o = (o is Visual || o is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(o) : LogicalTreeHelper.GetParent(o); }
                FsCloseCards();
            };
            root.Children.Add(fsRoot);

            FsRebuild();
            fsTimer = new DispatcherTimer();
            fsTimer.Interval = TimeSpan.FromSeconds(1);
            fsTimer.Tick += delegate { FsTick(); };
            fsTimer.Start();
            Log.Write("full screen on");

            if (!cfg.FsSeen) FsSplash(); else FsShowHint();
        }

        void ExitFs() { ExitFs(true); }

        void ExitFs(bool refresh)
        {
            if (!fsOn) return;
            fsOn = false;
            try { if (fsTimer != null) fsTimer.Stop(); } catch (Exception) { }
            try { root.Children.Remove(fsRoot); } catch (Exception) { }
            fsRoot = null; fsRing = null;
            WindowState = WindowState.Normal;
            WindowStyle = fsPrevStyle;
            ResizeMode = fsPrevResize;
            WindowState = fsPrevState;
            Log.Write("full screen off");
            if (refresh && view == "now") ShowView(false);
        }

        void FsShowHint()
        {
            DoubleAnimationUsingKeyFrames k = new DoubleAnimationUsingKeyFrames();
            k.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            k.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));
            k.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5000))));
            k.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5600))));
            fsHint.BeginAnimation(UIElement.OpacityProperty, k);
        }

        // puts the two pages where they belong; animated when the schedule slides in or out
        void FsPlace(bool animate)
        {
            if (fsStage == null) return;
            double h = fsStage.ActualHeight;
            TranslateTransform t1 = (TranslateTransform)fsPage1.RenderTransform, t2 = (TranslateTransform)fsPage2.RenderTransform;
            double y1 = fsSched ? -h : 0, y2 = fsSched ? 0 : h;
            if (!animate)
            {
                t1.BeginAnimation(TranslateTransform.YProperty, null); t2.BeginAnimation(TranslateTransform.YProperty, null);
                t1.Y = y1; t2.Y = y2;
                return;
            }
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            t1.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y1, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease });
            t2.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y2, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease });
        }

        void FsToggleSched()
        {
            FsCloseCards();
            fsSched = !fsSched;
            FsPlace(true);
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fadeOut.Completed += delegate
            {
                if (fsArrow == null) return;
                fsArrowGlyph.Text = fsSched ? "" : "";
                fsArrow.VerticalAlignment = fsSched ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                fsArrow.Margin = fsSched ? new Thickness(0, 24, 0, 0) : new Thickness(0, 0, 0, 24);
                fsArrow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            };
            fsArrow.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ------------------------------------------------------------ content
        void FsRebuild()
        {
            DayPlan plan = Sched.Plan(cfg, snap, term, DateTime.Today);
            TimeSpan now = DateTime.Now.TimeOfDay;
            fsKey = Sched.StateKey(plan, now);
            fsHeroHost.Content = FsHero(plan, now);
            fsTilesHost.Content = FsTiles(plan, now);
            fsTimelineHost.Content = plan.School && plan.Slots.Count > 0 ? FsSchedule(plan, now) : (UIElement)PageKit.Notice("There is no schedule to show today.", false);
        }

        void FsClockPlace()
        {
            if (fsClock == null) return;
            fsClock.Visibility = cfg.FsClock ? Visibility.Visible : Visibility.Collapsed;
            fsClock.HorizontalAlignment = cfg.FsClockPos == 0 ? HorizontalAlignment.Left : (cfg.FsClockPos == 1 ? HorizontalAlignment.Center : HorizontalAlignment.Right);
            fsClock.Text = DateTime.Now.ToString("h:mm tt");
        }

        // the small settings window of full screen mode: one page, General
        void FsSettings()
        {
            Grid dim = new Grid();
            dim.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
            Panel.SetZIndex(dim, 60);
            StackPanel col = new StackPanel();
            Grid head = UI.Cols(1, 0);
            UI.At(head, UI.Text("Settings", 22, "T1", true), 0);
            Button close = UI.Btn("\u2715", "Flat", delegate { if (fsRoot != null) fsRoot.Children.Remove(dim); });
            UI.At(head, close, 1);
            col.Children.Add(head);
            StackPanel tabs = new StackPanel();
            tabs.Width = 150;
            tabs.Margin = new Thickness(0, 0, 22, 0);
            ContentControl body = new ContentControl();
            Grid layout = UI.Cols(0, 1);
            layout.Margin = new Thickness(0, 16, 0, 0);
            UI.At(layout, tabs, 0);
            UI.At(layout, body, 1);
            col.Children.Add(layout);
            Action<int> showPage = null;
            showPage = delegate(int pg)
            {
                tabs.Children.Clear();
                string[] names = new string[] { "General", "Appearance" };
                for (int i = 0; i < names.Length; i++)
                {
                    int ix = i;
                    Button tb = UI.Btn(names[i], i == pg ? "Pill" : "Ghost", delegate { showPage(ix); });
                    tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                    tb.HorizontalContentAlignment = HorizontalAlignment.Left;
                    tb.Margin = new Thickness(0, 0, 0, 8);
                    tabs.Children.Add(tb);
                }
                StackPanel page = new StackPanel();
                if (pg == 0)
                {
                    page.Children.Add(UI.Margin(UI.Text("CLOCK", 11.5, "T3", true), 2, 4, 0, 6));
                    page.Children.Add(Toggle("Show clock", cfg.FsClock, delegate(bool v) { cfg.FsClock = v; cfg.Save(); FsClockPlace(); }));
                    page.Children.Add(UI.Margin(UI.Text("Clock location", 13.5, "T1", false), 2, 12, 0, 8));
                    string[] places = new string[] { "Top left", "Top middle", "Top right" };
                    Button pick = UI.Btn(places[cfg.FsClockPos] + "  \u25BE", "Ghost", null);
                    pick.HorizontalAlignment = HorizontalAlignment.Left;
                    pick.Click += delegate
                    {
                        List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
                        for (int k = 0; k < places.Length; k++)
                        {
                            int kk = k;
                            items.Add(new KeyValuePair<string, RoutedEventHandler>((cfg.FsClockPos == k ? "\u2713  " : "     ") + places[k], delegate
                            {
                                cfg.FsClockPos = kk; cfg.Save(); FsClockPlace();
                                pick.Content = places[kk] + "  \u25BE";
                            }));
                        }
                        UI.OpenMenu(pick, UI.Menu(items.ToArray()));
                    };
                    page.Children.Add(pick);
                }
                else
                {
                    string[] modeKeys = new string[] { "system", "light", "dark" };
                    page.Children.Add(UI.Margin(UI.Text("MODE", 11.5, "T3", true), 2, 4, 0, 8));
                    page.Children.Add(SuSeg(new string[] { "System", "Light", "Dark" }, Math.Max(0, Array.IndexOf(modeKeys, cfg.Mode)), delegate(int k) { ChangeMode(modeKeys[k]); }));
                    page.Children.Add(UI.Margin(UI.Text("ACCENT THEME", 11.5, "T3", true), 2, 18, 0, 8));
                    WrapPanel sw = new WrapPanel();
                    foreach (ThemeDef d in Theme.All)
                    {
                        if (d.Name == "Custom" && !cfg.Unlocked) continue;
                        UIElement sc = SwatchCard(d);
                        ((UIElement)sc).MouseLeftButtonUp += delegate { Dispatcher.BeginInvoke(new Action(delegate { showPage(1); })); };
                        sw.Children.Add(sc);
                    }
                    ScrollViewer sv = new ScrollViewer();
                    sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    sv.MaxHeight = 380;
                    sv.Content = sw;
                    page.Children.Add(sv);
                }
                body.Content = page;
            };
            showPage(0);
            Border card = FsPanelCard(col);
            card.Padding = new Thickness(28);
            card.Width = 860;
            card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center;
            dim.Children.Add(card);
            dim.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.OriginalSource == dim && fsRoot != null) fsRoot.Children.Remove(dim); };
            fsRoot.Children.Add(dim);
            UI.FadeIn(card, 0);
        }

        void FsTick()
        {
            if (!fsOn || snap == null) return;
            if (fsClock != null && cfg.FsClock) fsClock.Text = DateTime.Now.ToString("h:mm tt");
            try
            {
                DayPlan plan = Sched.Plan(cfg, snap, term, DateTime.Today);
                TimeSpan now = DateTime.Now.TimeOfDay;
                if (Sched.StateKey(plan, now) != fsKey) { FsRebuild(); return; }
                int cur, next;
                string ph = Sched.Phase(plan, now, out cur, out next);
                if (fsRing == null) return;
                TimeSpan left; double frac;
                if (ph == "class")
                {
                    Slot s = plan.Slots[cur];
                    TimeSpan total = s.B.End - s.B.Start;
                    left = s.B.End - now;
                    frac = total.TotalSeconds > 0 ? (total - left).TotalSeconds / total.TotalSeconds : 0;
                }
                else if ((ph == "between" || ph == "before") && next >= 0)
                {
                    left = plan.Slots[next].B.Start - now;
                    frac = fsRing.Frac;
                }
                else return;
                string big, small; CountText(left, out big, out small);
                fsRing.Frac = Math.Max(0, Math.Min(1, frac)); fsRing.Text = big; fsRing.SubText = small;
                fsRing.InvalidateVisual();
            }
            catch (Exception ex) { Log.Error("full screen tick", ex); }
        }

        UIElement FsHero(DayPlan plan, TimeSpan now)
        {
            int cur, next;
            string ph = Sched.Phase(plan, now, out cur, out next);
            RingMeter ring = new RingMeter();
            ring.Width = 380; ring.Height = 380; ring.Thickness = 20; ring.BigSize = 76;
            ring.Margin = new Thickness(0, 0, 70, 0);
            ring.VerticalAlignment = VerticalAlignment.Center;
            fsRing = null;
            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            info.MaxWidth = 620;
            if (ph == "class")
            {
                Slot s = plan.Slots[cur];
                TimeSpan total = s.B.End - s.B.Start, left = s.B.End - now;
                string big, small; CountText(left, out big, out small);
                ring.Frac = total.TotalSeconds > 0 ? (total - left).TotalSeconds / total.TotalSeconds : 0;
                ring.Fill = Theme.Acc; ring.Text = big; ring.SubText = small;
                fsRing = ring;
                Border kind = UI.Chip(s.IsClass ? "IN CLASS" : (s.Free ? "FREE PERIOD" : "NOW"), "Acc", "AccOn");
                ((TextBlock)kind.Child).SetResourceReference(TextBlock.ForegroundProperty, "AccOn");
                kind.HorizontalAlignment = HorizontalAlignment.Left;
                info.Children.Add(kind);
                info.Children.Add(UI.Margin(UI.Wrap(s.Title, 46, "T1"), 0, 16, 0, 4));
                string sub = "";
                if (s.R != null) { sub = s.R.Teacher ?? ""; if (!string.IsNullOrEmpty(s.R.Room)) sub += (sub.Length > 0 ? "  ·  " : "") + "Room " + s.R.Room; }
                else sub = s.B.Text();
                if (sub.Length > 0) info.Children.Add(UI.Wrap(sub, 20, "T2"));
                info.Children.Add(UI.Margin(UI.Text(Bell.Clock(s.B.Start) + "  →  " + Bell.Clock(s.B.End), 17, "T3", false), 0, 14, 0, 0));
                string nt = next >= 0 ? "Next:  " + plan.Slots[next].Title + "  ·  " + Bell.ClockShort(plan.Slots[next].B.Start) : "Last block of the day";
                info.Children.Add(UI.Margin(UI.Wrap(nt, 16, "T2"), 0, 8, 0, 0));
            }
            else if ((ph == "between" || ph == "before") && next >= 0)
            {
                Slot n = plan.Slots[next];
                TimeSpan left = n.B.Start - now;
                double frac = 0;
                if (ph == "between")
                {
                    TimeSpan prevEnd = TimeSpan.Zero;
                    foreach (Slot sl in plan.Slots) if (sl.B.End <= now && sl.B.End > prevEnd) prevEnd = sl.B.End;
                    double span = (n.B.Start - prevEnd).TotalSeconds;
                    frac = span > 0 ? (span - left.TotalSeconds) / span : 0;
                }
                string big, small; CountText(left, out big, out small);
                ring.Frac = frac; ring.Fill = Theme.AccText; ring.Text = big; ring.SubText = small;
                fsRing = ring;
                Border chip = UI.Chip(ph == "before" ? "SCHOOL STARTS SOON" : "PASSING TIME", "AccSoft", "AccText");
                chip.HorizontalAlignment = HorizontalAlignment.Left;
                info.Children.Add(chip);
                info.Children.Add(UI.Margin(UI.Text("Up next", 15, "T3", true), 0, 16, 0, 0));
                info.Children.Add(UI.Margin(UI.Wrap(n.Title, 46, "T1"), 0, 2, 0, 4));
                string sub = "Starts " + Bell.Clock(n.B.Start);
                if (n.R != null)
                {
                    string t = n.R.Teacher ?? "";
                    if (!string.IsNullOrEmpty(n.R.Room)) t += (t.Length > 0 ? "  ·  " : "") + "Room " + n.R.Room;
                    if (t.Length > 0) sub = t + "  ·  " + sub.ToLowerInvariant();
                }
                info.Children.Add(UI.Wrap(sub, 20, "T2"));
            }
            else
            {
                DateTime nx = Sched.NextSchoolDay(cfg, snap, DateTime.Today);
                string nextText = "Next school day: " + nx.ToString("dddd, MMM d") + ".";
                if (ph == "after") { ring.Frac = 1; ring.Fill = Theme.Good; ring.Text = "✓"; ring.BigSize = 110; }
                else { ring.Frac = 0; ring.Fill = Theme.T3; ring.Text = "–"; ring.BigSize = 110; }
                info.Children.Add(UI.Text(ph == "after" ? "School is out" : (!plan.School ? "No school today" : "Nothing scheduled"), 46, "T1", true));
                info.Children.Add(UI.Margin(UI.Wrap(!plan.School && plan.WhyNot.Length > 0 ? plan.WhyNot + " " + nextText : nextText, 20, "T2"), 0, 10, 0, 0));
            }
            Grid g = UI.Cols(0, 1);
            UI.At(g, ring, 0);
            UI.At(g, info, 1);
            return g;
        }

        // today's schedule, large, spread over the whole screen with no box around it
        UIElement FsSchedule(DayPlan plan, TimeSpan now)
        {
            int cur, next;
            Sched.Phase(plan, now, out cur, out next);
            Grid g = new Grid();
            g.HorizontalAlignment = HorizontalAlignment.Stretch;
            g.VerticalAlignment = VerticalAlignment.Center;
            g.MaxWidth = 1300;
            for (int i = 0; i < plan.Slots.Count; i++)
            {
                Slot s = plan.Slots[i];
                bool done = s.B.End <= now, isNow = i == cur;
                RowDefinition rd = new RowDefinition();
                rd.Height = new GridLength(1, GridUnitType.Star);
                rd.MaxHeight = 120;
                g.RowDefinitions.Add(rd);
                Grid row = UI.Cols(0, 0, 1, 0);
                row.VerticalAlignment = VerticalAlignment.Center;
                row.Margin = new Thickness(0, 7, 0, 7);
                StackPanel times = new StackPanel();
                times.Width = 150;
                TextBlock a = UI.Text(Bell.ClockShort(s.B.Start), 24, isNow ? "AccText" : (done ? "T3" : "T1"), true);
                a.HorizontalAlignment = HorizontalAlignment.Right;
                TextBlock b = UI.Text(Bell.ClockShort(s.B.End), 17, "T3", false);
                b.HorizontalAlignment = HorizontalAlignment.Right;
                times.Children.Add(a); times.Children.Add(b);
                UI.At(row, times, 0);
                Border bar = new Border();
                bar.Width = 6; bar.CornerRadius = new CornerRadius(3);
                bar.Margin = new Thickness(24, 4, 24, 4);
                if (isNow) bar.SetResourceReference(Border.BackgroundProperty, "Acc");
                else bar.Background = new SolidColorBrush(done ? Theme.Line2 : (s.IsClass ? Theme.Tag(s.Title) : Theme.Line2));
                UI.At(row, bar, 1);
                StackPanel mid = new StackPanel();
                mid.VerticalAlignment = VerticalAlignment.Center;
                mid.Children.Add(UI.Text(s.Title, 26, done || !s.IsClass ? "T3" : "T1", s.IsClass && !done));
                string sub = s.B.Label.Length > 0 && s.IsClass ? (s.R != null && s.R.Period.Length > 0 ? PeriodText(s.R) : s.B.Label) : "";
                if (s.R != null)
                {
                    string t = s.R.Teacher ?? "";
                    if (!string.IsNullOrEmpty(s.R.Room)) t += (t.Length > 0 ? "  \u00B7  " : "") + "Room " + s.R.Room;
                    if (t.Length > 0) sub = sub.Length > 0 ? sub + "  \u00B7  " + t : t;
                }
                if (sub.Length > 0) mid.Children.Add(UI.Text(sub, 17, "T3", false));
                UI.At(row, mid, 2);
                if (isNow || i == next)
                {
                    Border ch = UI.Chip(isNow ? "Now" : "Next", isNow ? "Acc" : "AccSoft", isNow ? "AccOn" : "AccText");
                    if (isNow) ((TextBlock)ch.Child).SetResourceReference(TextBlock.ForegroundProperty, "AccOn");
                    ch.VerticalAlignment = VerticalAlignment.Center;
                    UI.At(row, ch, 3);
                }
                Grid.SetRow(row, i);
                g.Children.Add(row);
            }
            return g;
        }

        UIElement FsTiles(DayPlan plan, TimeSpan now)
        {
            UniformGrid ug = new UniformGrid();
            ug.Columns = 2;
            List<UIElement> tiles = new List<UIElement>();
            if (!cfg.NoLetters)
                tiles.Add(PageKit.Tile(plan.DayLabel.Length > 0 ? plan.DayLabel : "–", null,
                    plan.DayLabel.Length > 0 ? Sched.DayName(plan.DayLabel) + (plan.LabelFrom.Length > 0 ? ", " + plan.LabelFrom : "") : "Day", null));
            tiles.Add(PageKit.Tile(plan.Sched != null ? (plan.SetName.Length > 0 ? plan.SetName : plan.Sched.Name) : "–", null, "Schedule today", null));
            tiles.Add(PageKit.Tile(Sched.ClassesLeft(plan, now).ToString(), null, "Classes left today", null));
            tiles.Add(PageKit.Tile(plan.Slots.Count > 0 ? Bell.ClockShort(plan.Slots[plan.Slots.Count - 1].B.End) : "–", null, "School ends", null));
            foreach (UIElement t in tiles)
            {
                Border c = UI.Card(t, 14);
                c.Margin = new Thickness(5);
                ug.Children.Add(c);
            }
            return ug;
        }

        // ------------------------------------------------------------ first-time welcome and tour
        void FsSplash()
        {
            cfg.FsSeen = true; cfg.Save();
            Grid dim = new Grid();
            dim.Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
            Panel.SetZIndex(dim, 50);
            TextBlock title = UI.Text("Welcome to full screen", 22, "T1", true);
            TextBlock body = UI.Wrap("A calm view of your class and the time left, right on the Now page.", 14, "T2");
            body.Margin = new Thickness(0, 8, 0, 0);
            StackPanel col = new StackPanel();
            col.Children.Add(title); col.Children.Add(body);
            Button skip = UI.Btn("Skip", "Flat", null);
            Button go = UI.Btn("Show me around", "Pill", null);
            go.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(skip, go);
            btns.HorizontalAlignment = HorizontalAlignment.Right;
            btns.Margin = new Thickness(0, 22, 0, 0);
            col.Children.Add(btns);
            Border card = FsPanelCard(col);
            card.Padding = new Thickness(28);
            card.Width = 460;
            card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center;
            dim.Children.Add(card);
            fsRoot.Children.Add(dim);
            UI.FadeIn(card, 0);

            Action finish = delegate
            {
                FsCloseCards();
                if (fsRoot != null) fsRoot.Children.Remove(dim);
                if (fsOn) FsShowHint();
            };
            string[][] steps = new string[][]
            {
                new string[] { "Time left", "The big card shows what is happening now and counts down to the end of it." },
                new string[] { "Menu", "Open it to leave full screen. F11 does the same." },
                new string[] { "Info", "Your day, today's schedule, classes left and when school ends." },
                new string[] { "Arrow", "Press it to slide up to today's schedule, and again to come back." }
            };
            int step = -1;
            Action show = null;
            show = delegate
            {
                step++;
                FsCloseCards();
                if (step >= steps.Length) { finish(); return; }
                // from here on the screen is not dimmed, so each piece can be seen
                dim.Background = null;   // no background means clicks pass through to the buttons under it, and the card stays clickable
                title.Text = steps[step][0];
                body.Text = steps[step][1];
                ((Button)btns.Children[1]).Content = step == steps.Length - 1 ? "Done" : "Next";
                FrameworkElement target = null;
                if (step == 0)
                {
                    card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Bottom;
                    card.Margin = new Thickness(0, 0, 0, 140);
                    target = fsHeroHost;
                }
                else if (step == 1)
                {
                    card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Bottom;
                    card.Margin = new Thickness(24, 0, 0, 160);
                    FsPop(fsMenuCard, true);
                    target = fsMenuBtn;
                }
                else if (step == 2)
                {
                    card.HorizontalAlignment = HorizontalAlignment.Right; card.VerticalAlignment = VerticalAlignment.Bottom;
                    card.Margin = new Thickness(0, 0, 24, 330);
                    FsPop(fsInfoCard, true);
                    target = fsInfoBtn;
                }
                else
                {
                    card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Bottom;
                    card.Margin = new Thickness(0, 0, 0, 96);
                    target = fsArrow;
                }
                UI.FadeIn(card, 0);
                if (target != null && step > 0)
                {
                    ScaleTransform pulse = new ScaleTransform(1, 1);
                    target.RenderTransformOrigin = new Point(0.5, 0.5);
                    target.RenderTransform = pulse;
                    DoubleAnimation pa = new DoubleAnimation(1, 1.3, TimeSpan.FromMilliseconds(450)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(3) };
                    pulse.BeginAnimation(ScaleTransform.ScaleXProperty, pa);
                    pulse.BeginAnimation(ScaleTransform.ScaleYProperty, pa);
                }
            };
            skip.Click += delegate { finish(); };
            go.Click += delegate { show(); };
        }
    }
}
