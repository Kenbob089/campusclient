using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CampusMidnight
{
    // one row of a list card (like the WinForms ListCard items)
    class ItemSpec
    {
        public string Title = "", Sub = "", Right = "", RightFg = "T2", Badge = "", BadgeBg = "AccSoft", BadgeFg = "AccText", Lead = "", Icon = "";
        public Color? Strip;
        public Color? RightColor;
        public Action Click;
        public bool Dim, Bold = true, Dot, Header;
    }

    static class PageKit
    {
        public static UIElement ListCard(string heading, string empty, List<ItemSpec> items)
        {
            StackPanel col = new StackPanel();
            if (!string.IsNullOrEmpty(heading))
            {
                TextBlock h = UI.Text(heading.ToUpperInvariant(), 11.5, "T3", true);
                h.Margin = new Thickness(2, 0, 0, 8);
                col.Children.Add(h);
            }
            if (items.Count == 0 && !string.IsNullOrEmpty(empty))
            {
                TextBlock e = UI.Wrap(empty, 13.5, "T3");
                e.Margin = new Thickness(2, 6, 0, 6);
                col.Children.Add(e);
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Header)
                {
                    TextBlock hd = UI.Text(items[i].Title, 12.5, "AccText", true);
                    hd.Margin = new Thickness(6, i == 0 ? 0 : 14, 0, 4);
                    col.Children.Add(hd);
                    continue;
                }
                if (i > 0 && !items[i - 1].Header) col.Children.Add(Line());
                col.Children.Add(Row(items[i]));
            }
            return UI.Card(col, 18);
        }

        static Border Line()
        {
            Border b = new Border();
            b.Height = 1;
            b.SetResourceReference(Border.BackgroundProperty, "Line");
            b.Margin = new Thickness(0, 1, 0, 1);
            return b;
        }

        public static UIElement Row(ItemSpec it)
        {
            Grid g = UI.Cols(0, 1, 0, 0);
            g.Margin = new Thickness(0, 7, 0, 7);
            int c = 0;
            if (it.Strip.HasValue)
            {
                Border s = new Border();
                s.Width = 4; s.CornerRadius = new CornerRadius(2);
                s.Background = new SolidColorBrush(it.Strip.Value);
                s.Margin = new Thickness(0, 2, 12, 2);
                UI.At(g, s, 0);
            }
            else if (it.Lead.Length > 0 || it.Icon.Length > 0)
            {
                Border l = new Border();
                l.Width = 34; l.Height = 34; l.CornerRadius = new CornerRadius(9);
                l.SetResourceReference(Border.BackgroundProperty, "AccSoft");
                TextBlock lt = UI.Text(it.Icon.Length > 0 ? it.Icon : it.Lead, it.Icon.Length > 0 ? 16 : 13, "AccText", true);
                if (it.Icon.Length > 0) lt.FontFamily = new FontFamily("Segoe MDL2 Assets");
                lt.HorizontalAlignment = HorizontalAlignment.Center;
                lt.VerticalAlignment = VerticalAlignment.Center;
                l.Child = lt;
                l.Margin = new Thickness(0, 0, 12, 0);
                UI.At(g, l, 0);
            }
            else if (it.Dot)
            {
                Border dt = new Border();
                dt.Width = 9; dt.Height = 9; dt.CornerRadius = new CornerRadius(5);
                dt.SetResourceReference(Border.BackgroundProperty, "Acc");
                dt.Margin = new Thickness(0, 0, 12, 0);
                dt.VerticalAlignment = VerticalAlignment.Center;
                UI.At(g, dt, 0);
            }
            StackPanel mid = new StackPanel();
            mid.VerticalAlignment = VerticalAlignment.Center;
            mid.Children.Add(UI.Text(it.Title, 14, it.Dim ? "T3" : "T1", it.Bold && !it.Dim));
            if (it.Sub.Length > 0) mid.Children.Add(UI.Text(it.Sub, 12, "T3", false));
            UI.At(g, mid, 1);
            if (it.Badge.Length > 0)
            {
                Border ch = UI.Chip(it.Badge, it.BadgeBg, it.BadgeFg);
                ch.VerticalAlignment = VerticalAlignment.Center;
                ch.Margin = new Thickness(10, 0, 0, 0);
                UI.At(g, ch, 2);
            }
            if (it.Right.Length > 0)
            {
                TextBlock r = UI.Text(it.Right, 13.5, it.RightFg, true);
                if (it.RightColor.HasValue) r.Foreground = new SolidColorBrush(it.RightColor.Value);
                r.VerticalAlignment = VerticalAlignment.Center;
                r.Margin = new Thickness(12, 0, 0, 0);
                r.TextTrimming = TextTrimming.None;
                UI.At(g, r, 3);
            }
            Border wrap = new Border();
            wrap.CornerRadius = new CornerRadius(9);
            wrap.Padding = new Thickness(6, 0, 6, 0);
            wrap.Background = Brushes.Transparent;
            wrap.Child = g;
            if (it.Click != null)
            {
                wrap.Cursor = Cursors.Hand;
                Action go = it.Click;
                wrap.MouseEnter += delegate { wrap.SetResourceReference(Border.BackgroundProperty, "Bg3"); };
                wrap.MouseLeave += delegate { wrap.Background = Brushes.Transparent; };
                wrap.MouseLeftButtonUp += delegate { go(); };
            }
            return wrap;
        }

        // a stat tile: big number, caption, optional progress ring
        public static UIElement Tile(string big, Color? bigColor, string label, double? ring)
        {
            Grid g = UI.Cols(1, 0);
            StackPanel p = new StackPanel();
            p.VerticalAlignment = VerticalAlignment.Center;
            TextBlock b = UI.Text(big, 28, "T1", true);
            if (bigColor.HasValue) b.Foreground = new SolidColorBrush(bigColor.Value);
            p.Children.Add(b);
            TextBlock l = UI.Wrap(label, 12.5, "T2");
            l.Margin = new Thickness(0, 3, 0, 0);
            p.Children.Add(l);
            UI.At(g, p, 0);
            if (ring.HasValue)
            {
                RingMeter r = new RingMeter();
                r.Width = 52; r.Height = 52; r.Thickness = 6; r.Margin = new Thickness(10, 0, 0, 0);
                r.Frac = ring.Value / 100.0;
                r.Fill = bigColor.HasValue ? bigColor.Value : Theme.Acc;
                r.Text = Math.Round(ring.Value) + "%";
                r.TextSize = 12;
                UI.At(g, r, 1);
            }
            Border card = UI.Card(g, 20);
            card.MinHeight = 100;
            return card;
        }

        // tiles in a row (4 wide when there is room, otherwise 2)
        public static UIElement TileRow(int width, params UIElement[] tiles)
        {
            System.Windows.Controls.Primitives.UniformGrid ug = new System.Windows.Controls.Primitives.UniformGrid();
            ug.Columns = width >= 900 ? 4 : 2;
            foreach (UIElement t in tiles) { ((FrameworkElement)t).Margin = new Thickness(0, 0, 16, 16); ug.Children.Add(t); }
            ug.Margin = new Thickness(0, 0, -16, 0);
            return ug;
        }

        // two cards side by side when wide, stacked when narrow
        public static UIElement Pair(int width, UIElement a, UIElement b)
        {
            if (width >= 860)
            {
                Grid g = UI.Cols(1, 1);
                ((FrameworkElement)a).Margin = new Thickness(0, 0, 8, 16);
                ((FrameworkElement)b).Margin = new Thickness(8, 0, 0, 16);
                UI.At(g, a, 0); UI.At(g, b, 1);
                return g;
            }
            StackPanel p = new StackPanel();
            ((FrameworkElement)a).Margin = new Thickness(0, 0, 0, 16);
            ((FrameworkElement)b).Margin = new Thickness(0, 0, 0, 16);
            p.Children.Add(a); p.Children.Add(b);
            return p;
        }

        public static UIElement Notice(string text, bool bad)
        {
            TextBlock t = UI.Wrap(text, 13.5, bad ? "Bad" : "T2");
            Border b = UI.Card(t, 16);
            b.SetResourceReference(Border.BackgroundProperty, bad ? "BadSoft" : "Bg3");
            b.Margin = new Thickness(0, 0, 0, 16);
            return b;
        }

        public static TextBlock Section(string text)
        {
            TextBlock t = UI.Text(text, 17, "T1", true);
            t.Margin = new Thickness(0, 22, 0, 12);
            return t;
        }
    }

    // a circular progress ring with optional text in the middle
    sealed class RingMeter : FrameworkElement
    {
        public double Frac, Thickness = 8, TextSize = 14, BigSize = 0;
        public Color Fill = Colors.White;
        public string Text = "", SubText = "";

        public static readonly DependencyProperty ShownProperty = DependencyProperty.Register("Shown", typeof(double), typeof(RingMeter),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        bool intro, introStarted;

        public RingMeter()
        {
            bool wantIntro = UI.Intro;
            Loaded += delegate
            {
                if (introStarted || !wantIntro) return;
                introStarted = true;
                intro = true;
                DoubleAnimation a = new DoubleAnimation(0, Frac, TimeSpan.FromMilliseconds(900));
                a.BeginTime = TimeSpan.FromMilliseconds(150);
                a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                a.Completed += delegate { intro = false; InvalidateVisual(); };
                BeginAnimation(ShownProperty, a);
            };
        }

        protected override void OnRender(DrawingContext dc)
        {
            double d = Math.Min(ActualWidth, ActualHeight);
            double r = (d - Thickness) / 2;
            Point c = new Point(ActualWidth / 2, ActualHeight / 2);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Theme.Bg4), Thickness), c, r, r);
            double f = Math.Max(0, Math.Min(1, intro ? (double)GetValue(ShownProperty) : Frac));
            if (f > 0.004)
            {
                Pen arc = new Pen(new SolidColorBrush(Fill), Thickness);
                arc.StartLineCap = PenLineCap.Round; arc.EndLineCap = PenLineCap.Round;
                if (f >= 0.999) dc.DrawEllipse(null, arc, c, r, r);
                else
                {
                    double a = f * 2 * Math.PI;
                    Point p0 = new Point(c.X, c.Y - r);
                    Point p1 = new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
                    StreamGeometry sg = new StreamGeometry();
                    using (StreamGeometryContext ctx = sg.Open())
                    {
                        ctx.BeginFigure(p0, false, false);
                        ctx.ArcTo(p1, new Size(r, r), 0, f > 0.5, SweepDirection.Clockwise, true, false);
                    }
                    sg.Freeze();
                    dc.DrawGeometry(null, arc, sg);
                }
            }
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (Text.Length > 0)
            {
                double big = BigSize > 0 ? BigSize : TextSize;
                FormattedText ft = new FormattedText(Text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI Semibold"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), big, new SolidColorBrush(Theme.T1), dpi);
                double y = c.Y - ft.Height / 2 - (SubText.Length > 0 ? 9 : 0);
                dc.DrawText(ft, new Point(c.X - ft.Width / 2, y));
                if (SubText.Length > 0)
                {
                    FormattedText st = new FormattedText(SubText, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12.5, new SolidColorBrush(Theme.T2), dpi);
                    dc.DrawText(st, new Point(c.X - st.Width / 2, y + ft.Height));
                }
            }
        }
    }
}
