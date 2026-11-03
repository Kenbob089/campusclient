using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CampusMidnight
{
    // small builders so the pages read like layout, not plumbing. Colours are resource keys, so they follow the theme live.
    static class UI
    {
        public static TextBlock Text(string s, double size, string fg, bool semi)
        {
            TextBlock t = new TextBlock();
            t.Text = s;
            t.FontSize = size;
            t.FontFamily = new FontFamily(semi ? "Segoe UI Semibold" : "Segoe UI");
            t.SetResourceReference(TextBlock.ForegroundProperty, fg);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            return t;
        }

        public static TextBlock Wrap(string s, double size, string fg)
        {
            TextBlock t = Text(s, size, fg, false);
            t.TextWrapping = TextWrapping.Wrap;
            t.TextTrimming = TextTrimming.None;
            return t;
        }

        public static TextBlock Icon(string glyph, double size, string fg)
        {
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = size;
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Icons");
            t.SetResourceReference(TextBlock.ForegroundProperty, fg);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            return t;
        }

        public static Border Card(UIElement child, double pad)
        {
            Border b = new Border();
            b.SetResourceReference(Border.BackgroundProperty, "Bg2");
            b.SetResourceReference(Border.BorderBrushProperty, "Line");
            b.BorderThickness = new Thickness(1);
            b.CornerRadius = new CornerRadius(14);
            b.Padding = new Thickness(pad);
            b.Child = child;
            return b;
        }

        public static Border Chip(string s, string bg, string fg)
        {
            Border b = new Border();
            b.SetResourceReference(Border.BackgroundProperty, bg);
            b.CornerRadius = new CornerRadius(8);
            b.Padding = new Thickness(9, 3, 9, 3);
            b.Child = Text(s, 12, fg, true);
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.VerticalAlignment = VerticalAlignment.Center;
            return b;
        }

        public static Border Bar(string color)
        {
            Border b = new Border();
            b.Width = 4;
            b.CornerRadius = new CornerRadius(2);
            b.SetResourceReference(Border.BackgroundProperty, color);
            return b;
        }

        public static Border Dot(Color c, double size)
        {
            Border b = new Border();
            b.Width = size; b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            b.Background = new SolidColorBrush(c);
            return b;
        }

        public static Button Btn(string text, string style, RoutedEventHandler click)
        {
            Button b = new Button();
            b.Content = text;
            b.Style = (Style)Application.Current.Resources[style];
            if (click != null) b.Click += click;
            return b;
        }

        public static Button IconBtn(string glyph, double size, RoutedEventHandler click)
        {
            Button b = new Button();
            b.Style = (Style)Application.Current.Resources["Flat"];
            b.Content = Icon(glyph, size, "T2");
            b.Width = 38; b.Height = 38;
            b.Padding = new Thickness(0);
            b.HorizontalContentAlignment = HorizontalAlignment.Center;
            if (click != null) b.Click += click;
            return b;
        }

        public static TextBox Input(string placeholder, string text)
        {
            TextBox t = new TextBox();
            t.Tag = placeholder;
            t.Text = text ?? "";
            return t;
        }

        public static StackPanel Col(params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            foreach (UIElement k in kids) p.Children.Add(k);
            return p;
        }

        public static StackPanel Row(params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            p.Orientation = Orientation.Horizontal;
            foreach (UIElement k in kids) p.Children.Add(k);
            return p;
        }

        public static T Margin<T>(T e, double l, double t, double r, double b) where T : FrameworkElement
        {
            e.Margin = new Thickness(l, t, r, b);
            return e;
        }

        public static Grid Cols(params double[] stars)
        {
            Grid g = new Grid();
            foreach (double s in stars)
            {
                ColumnDefinition c = new ColumnDefinition();
                c.Width = s > 0 ? new GridLength(s, GridUnitType.Star) : GridLength.Auto;
                g.ColumnDefinitions.Add(c);
            }
            return g;
        }

        public static T At<T>(Grid g, T e, int col) where T : UIElement
        {
            Grid.SetColumn(e, col);
            g.Children.Add(e);
            return e;
        }

        // gentle fade + rise used when a page changes
        public static void FadeIn(FrameworkElement e) { FadeIn(e, 0); }

        public static void FadeIn(FrameworkElement e, int delayMs)
        {
            e.Opacity = 0;
            TranslateTransform tt = new TranslateTransform(0, 14);
            e.RenderTransform = tt;
            TimeSpan begin = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
            fade.BeginTime = begin;
            e.BeginAnimation(UIElement.OpacityProperty, fade);
            DoubleAnimation up = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(380));
            up.BeginTime = begin;
            up.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            tt.BeginAnimation(TranslateTransform.YProperty, up);
        }

        // cards rise a little when the mouse is over them
        public static void Lift(FrameworkElement e)
        {
            TranslateTransform tt = new TranslateTransform();
            e.RenderTransform = tt;
            e.MouseEnter += delegate
            {
                DoubleAnimation a = new DoubleAnimation(-3, TimeSpan.FromMilliseconds(140));
                a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                tt.BeginAnimation(TranslateTransform.YProperty, a);
            };
            e.MouseLeave += delegate
            {
                DoubleAnimation a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                tt.BeginAnimation(TranslateTransform.YProperty, a);
            };
        }

        // true only while a page is being opened (not rebuilt), so bars and rings play their intro once
        public static bool Intro;

        // move an existing bar made by Meter without rebuilding it
        public static void SetMeter(UIElement meter, double pct)
        {
            Grid g = meter as Grid;
            if (g == null || g.Children.Count < 2) return;
            Grid fill = g.Children[1] as Grid;
            if (fill == null || fill.ColumnDefinitions.Count < 2) return;
            pct = Math.Max(0, Math.Min(100, pct));
            fill.ColumnDefinitions[0].Width = new GridLength(Math.Max(0.001, pct), GridUnitType.Star);
            fill.ColumnDefinitions[1].Width = new GridLength(Math.Max(0.001, 100 - pct), GridUnitType.Star);
        }

        // wide progress bar, value 0..100
        public static UIElement Meter(double pct, Color c)
        {
            Grid g = new Grid();
            g.Height = 6;
            Border back = new Border();
            back.CornerRadius = new CornerRadius(3);
            back.SetResourceReference(Border.BackgroundProperty, "Bg4");
            g.Children.Add(back);
            Grid fill = Cols(Math.Max(0.001, Math.Min(100, pct)), Math.Max(0.001, 100 - Math.Min(100, pct)));
            Border f = new Border();
            f.CornerRadius = new CornerRadius(3);
            f.Background = new SolidColorBrush(c);
            At(fill, f, 0);
            fill.RenderTransformOrigin = new Point(0, 0.5);
            ScaleTransform sc = new ScaleTransform(Intro ? 0 : 1, 1);
            fill.RenderTransform = sc;
            DoubleAnimation grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700));
            grow.BeginTime = TimeSpan.FromMilliseconds(120);
            grow.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (Intro) fill.Loaded += delegate { sc.BeginAnimation(ScaleTransform.ScaleXProperty, grow); };
            else sc.ScaleX = 1;
            g.Children.Add(fill);
            return g;
        }

        public static ScrollViewer Scroll(UIElement child)
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Content = child;
            return sv;
        }

        // "-" is a divider line, a leading "#" is a small grey heading
        public static ContextMenu Menu(params KeyValuePair<string, RoutedEventHandler>[] items)
        {
            ContextMenu m = new ContextMenu();
            foreach (KeyValuePair<string, RoutedEventHandler> kv in items)
            {
                if (kv.Key == "-") { m.Items.Add(new Separator()); continue; }
                MenuItem mi = new MenuItem();
                if (kv.Key.StartsWith("#"))
                {
                    mi.Header = kv.Key.Substring(1).ToUpperInvariant();
                    mi.IsEnabled = false;
                    mi.FontSize = 11;
                    mi.SetResourceReference(Control.ForegroundProperty, "T3");
                }
                else mi.Header = kv.Key;
                if (kv.Value != null) mi.Click += kv.Value;
                m.Items.Add(mi);
            }
            return m;
        }

        public static void OpenMenu(FrameworkElement anchor, ContextMenu m)
        {
            m.MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height - 80);
            m.PlacementTarget = anchor;
            m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            m.IsOpen = true;
        }
    }
}
