using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        UIElement Section(string title, params UIElement[] kids)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(Heading(title));
            foreach (UIElement k in kids) p.Children.Add(k);
            Border b = UI.Card(p, 20);
            b.Margin = new Thickness(0, 0, 0, 16);
            return b;
        }

        CheckBox Toggle(string text, bool on, Action<bool> set)
        {
            CheckBox c = new CheckBox();
            c.Content = text;
            c.IsChecked = on;
            c.Margin = new Thickness(0, 6, 0, 6);
            c.Click += delegate { set(c.IsChecked == true); cfg.Save(); };
            return c;
        }

        static UIElement Note(string s)
        {
            TextBlock t = UI.Wrap(s, 12.5, "T3");
            t.Margin = new Thickness(0, 4, 0, 8);
            return t;
        }

        void RestyleAll()
        {
            ApplyTheme();
            ShowShell();
        }

        UIElement PageSettings()
        {
            // appearance
            StackPanel modes = new StackPanel();
            modes.Orientation = Orientation.Horizontal;
            string[][] ms = new string[][] { new string[] { "system", "System" }, new string[] { "light", "Light" }, new string[] { "dark", "Dark" } };
            foreach (string[] m in ms)
            {
                string key = m[0];
                Button b = UI.Btn(m[1], cfg.Mode == key ? "Pill" : "Ghost", delegate { cfg.Mode = key; cfg.Save(); RestyleAll(); });
                b.Margin = new Thickness(0, 0, 8, 0);
                modes.Children.Add(b);
            }
            WrapPanel sw = new WrapPanel();
            sw.Margin = new Thickness(0, 14, 0, 0);
            foreach (ThemeDef d in Theme.All)
            {
                string name = d.Name;
                bool sel = string.Equals(cfg.Theme, name, StringComparison.OrdinalIgnoreCase);
                Border o = new Border();
                o.Width = 40; o.Height = 40; o.CornerRadius = new CornerRadius(20);
                o.Margin = new Thickness(0, 0, 10, 10);
                o.Cursor = System.Windows.Input.Cursors.Hand;
                o.BorderThickness = new Thickness(2);
                if (sel) o.SetResourceReference(Border.BorderBrushProperty, "T1"); else o.BorderBrush = Brushes.Transparent;
                Border inner = new Border();
                inner.Margin = new Thickness(3);
                inner.CornerRadius = new CornerRadius(17);
                inner.Background = new LinearGradientBrush(d.Accent, Theme.Mix(d.Base, d.Accent, 0.35), 45);
                o.Child = inner;
                o.ToolTip = name;
                o.MouseLeftButtonUp += delegate { cfg.Theme = name; cfg.Save(); RestyleAll(); };
                sw.Children.Add(o);
            }

            // grades
            Button iv = UI.Btn("Refresh: " + (cfg.Interval <= 0 ? "off" : "every " + cfg.Interval + " min") + "  ▾", "Ghost", null);
            iv.HorizontalAlignment = HorizontalAlignment.Left;
            iv.Margin = new Thickness(0, 8, 0, 0);
            iv.Click += delegate
            {
                List<KeyValuePair<string, RoutedEventHandler>> items = new List<KeyValuePair<string, RoutedEventHandler>>();
                foreach (int mins in new int[] { 0, 5, 15, 30, 60 })
                {
                    int mm = mins;
                    items.Add(new KeyValuePair<string, RoutedEventHandler>(mm == 0 ? "Off" : "Every " + mm + " minutes", delegate { cfg.Interval = mm; cfg.Save(); StartAutoRefresh(); ShowView(false); }));
                }
                UI.OpenMenu(iv, UI.Menu(items.ToArray()));
            };

            // pictures
            Button pick = UI.Btn("Choose my photo", "Ghost", delegate { PickImage("photo.png", delegate { photo = FromFile(Disk.At("photo.png")); }); });
            Button clear = UI.Btn("Remove my photo", "Ghost", delegate
            {
                try { File.Delete(Disk.At("photo.png")); } catch (Exception) { }
                photo = null; photoTried.Clear();
                if (api != null && current != null) LoadAssets(api, current);
                ShowView(false);
            });
            Button logoPick = UI.Btn("Choose school logo", "Ghost", delegate { PickImage("logo.png", delegate { logo = FromFile(Disk.At("logo.png")); }); });
            StackPanel picRow = UI.Row(pick, clear, logoPick);
            foreach (UIElement e in picRow.Children) ((FrameworkElement)e).Margin = new Thickness(0, 0, 8, 0);

            // account
            Button open = UI.Btn("Open data folder", "Ghost", delegate { try { Process.Start(new ProcessStartInfo(Disk.Dir()) { UseShellExecute = true }); } catch (Exception) { } });
            Button so = UI.Btn("Sign out", "Ghost", delegate { SignOut(); });
            StackPanel accRow = UI.Row(open, so);
            foreach (UIElement e in accRow.Children) ((FrameworkElement)e).Margin = new Thickness(0, 0, 8, 0);

            return Body(
                Section("Appearance", modes, sw),
                Section("Grades",
                    Toggle("Show numbers instead of letters", cfg.NumbersOnly, delegate(bool v) { cfg.NumbersOnly = v; Theme.NumbersOnly = v; ShowView(false); }),
                    Toggle("Count dropped classes in GPA", cfg.CountDropped, delegate(bool v) { cfg.CountDropped = v; ShowView(false); }),
                    iv),
                Section("Pictures", Toggle("Hide my photo", cfg.HidePhoto, delegate(bool v) { cfg.HidePhoto = v; ShowView(false); }),
                    Note("Pick any image. Cropping is still done in the WinForms app for now."), picRow),
                Section("Account", UI.Text(string.IsNullOrEmpty(cfg.User) ? "Signed in with a session cookie" : "Signed in as " + cfg.User, 13.5, "T2", false), UI.Margin(accRow, 0, 12, 0, 0)));
        }

        void PickImage(string saveAs, Action loaded)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*";
                if (dlg.ShowDialog(this) != true) return;
                File.Copy(dlg.FileName, Disk.At(saveAs), true);
                loaded();
                ShowView(false);
            }
            catch (Exception ex) { Log.Error("pick image", ex); }
        }
    }
}
