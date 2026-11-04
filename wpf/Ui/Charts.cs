using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CampusMidnight
{
    class BarItem { public string Label = "", Tip = ""; public double Value; public Color Fill; }

    // base: a drawn card with a heading and a hover tooltip that follows the mouse
    abstract class ChartBase : FrameworkElement
    {
        public string Heading = "", EmptyText = "Nothing to show yet.";
        protected int hot = -1;

        protected ChartBase()
        {
            Cursor = Cursors.Arrow;
        }

        protected FormattedText Ft(string s, double size, Color c, bool semi)
        {
            FormattedText f = new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily(semi ? "Segoe UI Semibold" : "Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                size, new SolidColorBrush(c), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            return f;
        }

        protected void Frame(DrawingContext dc)
        {
            Rect r = new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
            dc.DrawRoundedRectangle(new SolidColorBrush(Theme.Bg2), new Pen(new SolidColorBrush(Theme.Line), 1), r, 14, 14);
            if (Heading.Length > 0) dc.DrawText(Ft(Heading.ToUpperInvariant(), 11.5, Theme.T3, true), new Point(20, 16));
        }

        System.Windows.Controls.Primitives.Popup pop;
        TextBlock popText;

        protected void SetTip(int idx, string tip)
        {
            if (idx != hot)
            {
                hot = idx;
                if (idx >= 0) ShowTip(tip); else HideTip();
                InvalidateVisual();
            }
        }

        void ShowTip(string s)
        {
            if (pop == null)
            {
                Border b = new Border();
                b.Background = new SolidColorBrush(Theme.Bg4);
                b.BorderBrush = new SolidColorBrush(Theme.Line2);
                b.BorderThickness = new Thickness(1);
                b.CornerRadius = new CornerRadius(8);
                b.Padding = new Thickness(10, 7, 10, 7);
                b.Margin = new Thickness(6, 4, 16, 18);
                b.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = Theme.ShadowOp, Color = Theme.Dark ? Colors.Black : Color.FromRgb(40, 50, 75) };
                popText = new TextBlock();
                popText.Foreground = new SolidColorBrush(Theme.T1);
                popText.FontSize = 12.5;
                b.Child = popText;
                pop = new System.Windows.Controls.Primitives.Popup();
                pop.AllowsTransparency = true;
                pop.IsHitTestVisible = false;
                pop.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                pop.PlacementTarget = this;
                pop.Child = b;
            }
            ((Border)pop.Child).Background = new SolidColorBrush(Theme.Bg4);
            ((Border)pop.Child).BorderBrush = new SolidColorBrush(Theme.Line2);
            popText.Foreground = new SolidColorBrush(Theme.T1);
            popText.Text = s;
            MoveTip();
            pop.IsOpen = true;
        }

        void HideTip()
        {
            if (pop != null) pop.IsOpen = false;
        }

        void MoveTip()
        {
            if (pop == null) return;
            Point p = Mouse.GetPosition(this);
            pop.HorizontalOffset = p.X + 14;
            pop.VerticalOffset = p.Y + 16;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (hot >= 0) MoveTip();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            SetTip(-1, "");
        }
    }

    sealed class BarChart : ChartBase
    {
        public List<BarItem> Bars = new List<BarItem>();
        double left = 20, top = 52, bottom = 40;

        int IndexAt(Point p)
        {
            if (Bars.Count == 0) return -1;
            double w = (ActualWidth - left * 2) / Bars.Count;
            int i = (int)((p.X - left) / w);
            if (i < 0 || i >= Bars.Count || p.Y < top - 6 || p.Y > ActualHeight - 8) return -1;
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.GetPosition(this));
            SetTip(i, i >= 0 ? Bars[i].Tip : "");
        }

        protected override void OnRender(DrawingContext dc)
        {
            Frame(dc);
            double max = 0;
            foreach (BarItem b in Bars) max = Math.Max(max, b.Value);
            if (Bars.Count == 0 || max <= 0)
            {
                dc.DrawText(Ft(EmptyText, 13, Theme.T3, false), new Point(20, ActualHeight / 2 - 8));
                return;
            }
            double w = (ActualWidth - left * 2) / Bars.Count;
            double h = ActualHeight - top - bottom;
            for (int i = 0; i < Bars.Count; i++)
            {
                BarItem b = Bars[i];
                double bh = Math.Max(3, h * b.Value / max);
                double bw = Math.Min(w * 0.62, 46);
                double x = left + w * i + (w - bw) / 2;
                Color c = b.Fill;
                if (hot >= 0 && hot != i) c.A = 120;
                dc.DrawRoundedRectangle(new SolidColorBrush(c), null, new Rect(x, top + h - bh, bw, bh), 5, 5);
                string[] parts = b.Label.Split('|');
                FormattedText l1 = Ft(parts[0], 11.5, hot == i ? Theme.T1 : Theme.T3, true);
                dc.DrawText(l1, new Point(left + w * i + (w - l1.Width) / 2, top + h + 8));
                if (parts.Length > 1)
                {
                    FormattedText l2 = Ft(parts[1], 11, Theme.T3, false);
                    dc.DrawText(l2, new Point(left + w * i + (w - l2.Width) / 2, top + h + 22));
                }
                if (b.Value > 0)
                {
                    FormattedText v = Ft(((int)b.Value).ToString(), 12, Theme.T1, true);
                    dc.DrawText(v, new Point(left + w * i + (w - v.Width) / 2, top + h - bh - 18));
                }
            }
        }
    }

    sealed class LineChart : ChartBase
    {
        public List<DateTime> Xs = new List<DateTime>();
        public List<double> Ys = new List<double>();
        public List<string> Names = new List<string>();
        double left = 46, right = 24, top = 50, bottom = 34;
        double lo, hi;

        Point Pt(int i)
        {
            double w = ActualWidth - left - right, h = ActualHeight - top - bottom;
            double x = Ys.Count == 1 ? left + w / 2 : left + w * i / (double)(Ys.Count - 1);
            double y = top + h * (1 - (Ys[i] - lo) / (hi - lo));
            return new Point(x, y);
        }

        void Range()
        {
            double mn = 100, mx = 0;
            foreach (double y in Ys) { mn = Math.Min(mn, y); mx = Math.Max(mx, y); }
            lo = Math.Max(0, Math.Floor((mn - 5) / 10) * 10);
            hi = Math.Max(lo + 10, Math.Ceiling((mx + 1) / 10) * 10);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Ys.Count == 0) return;
            Range();
            Point m = e.GetPosition(this);
            int best = -1; double bd = 28;
            for (int i = 0; i < Ys.Count; i++)
            {
                double d = Math.Abs(Pt(i).X - m.X);
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) { SetTip(-1, ""); return; }
            string nm = best < Names.Count ? Names[best] + "\n" : "";
            SetTip(best, nm + Math.Round(Ys[best], 1).ToString("0.#", CultureInfo.InvariantCulture) + "%\n" + Xs[best].ToString("ddd, MMM d"));
        }

        protected override void OnRender(DrawingContext dc)
        {
            Frame(dc);
            if (Ys.Count < 2) { dc.DrawText(Ft("Not enough graded work to chart yet.", 13, Theme.T3, false), new Point(20, ActualHeight / 2 - 8)); return; }
            Range();
            double w = ActualWidth - left - right, h = ActualHeight - top - bottom;
            Pen grid = new Pen(new SolidColorBrush(Theme.Line), 1);
            for (double v = lo; v <= hi + 0.01; v += 10)
            {
                double y = top + h * (1 - (v - lo) / (hi - lo));
                dc.DrawLine(grid, new Point(left, y), new Point(left + w, y));
                FormattedText t = Ft(((int)v).ToString(), 11, Theme.T3, false);
                dc.DrawText(t, new Point(left - 10 - t.Width, y - t.Height / 2));
            }
            StreamGeometry sg = new StreamGeometry();
            using (StreamGeometryContext c = sg.Open())
            {
                c.BeginFigure(Pt(0), false, false);
                for (int i = 1; i < Ys.Count; i++) c.LineTo(Pt(i), true, true);
            }
            sg.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Theme.Acc), 2.5) { LineJoin = PenLineJoin.Round }, sg);
            for (int i = 0; i < Ys.Count; i++)
            {
                bool on = i == hot;
                dc.DrawEllipse(new SolidColorBrush(on ? Theme.T1 : Theme.Acc), new Pen(new SolidColorBrush(Theme.Bg2), 2), Pt(i), on ? 6 : 3.5, on ? 6 : 3.5);
            }
            FormattedText a = Ft(Xs[0].ToString("MMM d"), 11, Theme.T3, false);
            dc.DrawText(a, new Point(left, ActualHeight - 24));
            FormattedText b = Ft(Xs[Xs.Count - 1].ToString("MMM d"), 11, Theme.T3, false);
            dc.DrawText(b, new Point(left + w - b.Width, ActualHeight - 24));
        }
    }
}
