using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CampusMidnight
{
    // a small colour picker built in code: a square for saturation and brightness, and a bar for the hue
    static class ColorPick
    {
        public static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            v = max;
            s = max <= 0 ? 0 : d / max;
            if (d <= 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
            if (h < 0) h += 360;
        }

        public static Color FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        public static string Hex(Color c)
        {
            return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2");
        }

        public static UIElement Build(Color start, Action<Color> changed)
        {
            double h, s, v;
            ToHsv(start, out h, out s, out v);
            const double W = 240, H = 150, BarH = 16;

            Canvas sq = new Canvas();
            sq.Width = W; sq.Height = H; sq.ClipToBounds = true; sq.Cursor = Cursors.Cross;
            sq.Background = Brushes.Transparent;
            Rectangle hueFill = new Rectangle();
            hueFill.Width = W; hueFill.Height = H;
            Rectangle white = new Rectangle();
            white.Width = W; white.Height = H;
            white.Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0);
            Rectangle black = new Rectangle();
            black.Width = W; black.Height = H;
            black.Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90);
            Ellipse dot = new Ellipse();
            dot.Width = 14; dot.Height = 14;
            dot.Stroke = Brushes.White; dot.StrokeThickness = 2;
            dot.IsHitTestVisible = false;
            sq.Children.Add(hueFill); sq.Children.Add(white); sq.Children.Add(black); sq.Children.Add(dot);

            Canvas bar = new Canvas();
            bar.Width = W; bar.Height = BarH; bar.Cursor = Cursors.Hand;
            bar.Margin = new Thickness(0, 10, 0, 0);
            Rectangle rainbow = new Rectangle();
            rainbow.Width = W; rainbow.Height = BarH; rainbow.RadiusX = 5; rainbow.RadiusY = 5;
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0); lg.EndPoint = new Point(1, 0);
            for (int i = 0; i <= 6; i++) lg.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
            rainbow.Fill = lg;
            Rectangle knob = new Rectangle();
            knob.Width = 5; knob.Height = BarH + 4; knob.RadiusX = 2; knob.RadiusY = 2;
            knob.Fill = Brushes.White; knob.Stroke = Brushes.Black; knob.StrokeThickness = 1;
            knob.IsHitTestVisible = false;
            Canvas.SetTop(knob, -2);
            bar.Children.Add(rainbow); bar.Children.Add(knob);

            Action place = delegate
            {
                hueFill.Fill = new SolidColorBrush(FromHsv(h, 1, 1));
                Canvas.SetLeft(dot, s * W - 7);
                Canvas.SetTop(dot, (1 - v) * H - 7);
                Canvas.SetLeft(knob, h / 360.0 * W - 2.5);
            };
            place();

            bool dragSq = false, dragBar = false;
            Action<Point> setSq = delegate(Point p)
            {
                s = Math.Max(0, Math.Min(1, p.X / W));
                v = 1 - Math.Max(0, Math.Min(1, p.Y / H));
                place();
                changed(FromHsv(h, s, v));
            };
            Action<Point> setBar = delegate(Point p)
            {
                h = Math.Max(0, Math.Min(359.9, p.X / W * 360.0));
                place();
                changed(FromHsv(h, s, v));
            };
            sq.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { dragSq = true; sq.CaptureMouse(); setSq(e.GetPosition(sq)); };
            sq.MouseMove += delegate(object o, MouseEventArgs e) { if (dragSq) setSq(e.GetPosition(sq)); };
            sq.MouseLeftButtonUp += delegate { dragSq = false; sq.ReleaseMouseCapture(); };
            bar.MouseLeftButtonDown += delegate(object o, MouseButtonEventArgs e) { dragBar = true; bar.CaptureMouse(); setBar(e.GetPosition(bar)); };
            bar.MouseMove += delegate(object o, MouseEventArgs e) { if (dragBar) setBar(e.GetPosition(bar)); };
            bar.MouseLeftButtonUp += delegate { dragBar = false; bar.ReleaseMouseCapture(); };

            StackPanel p0 = new StackPanel();
            p0.HorizontalAlignment = HorizontalAlignment.Left;
            p0.Margin = new Thickness(100, 10, 0, 4);
            p0.Children.Add(sq);
            p0.Children.Add(bar);
            return p0;
        }
    }
}
