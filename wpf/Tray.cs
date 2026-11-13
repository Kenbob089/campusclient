using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace CampusMidnight
{
    // tray icon, "starts soon" / "ends soon" reminders, keep-running-in-tray
    sealed partial class MainWindow
    {
        System.Windows.Forms.NotifyIcon tray;
        bool quitting, trayHinted;
        Window trayHost;
        DayPlan notifyPlan;
        DateTime notifyPlanAt;
        readonly HashSet<string> fired = new HashSet<string>();

        partial void SuTrayChanged() { ApplyTray(false); }
        // reminders and one-off messages go to the notification centre as well as Windows
        void Toast(string title, string body) { Notify(title, body, "now"); }
        partial void SuToast(string title, string body) { Toast(title, body); }

        System.Drawing.Icon BuildTrayIcon()
        {
            try
            {
                string custom = Disk.At("icon.png");
                if (File.Exists(custom))
                {
                    using (System.Drawing.Image im = System.Drawing.Image.FromFile(custom))
                    using (System.Drawing.Bitmap ib = new System.Drawing.Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(ib))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(im, new System.Drawing.Rectangle(0, 0, 64, 64));
                        }
                        return System.Drawing.Icon.FromHandle(ib.GetHicon());
                    }
                }
            }
            catch (Exception) { }
            try
            {
                System.Windows.Resources.StreamResourceInfo ri = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico", UriKind.Absolute));
                if (ri != null) return new System.Drawing.Icon(ri.Stream);
            }
            catch (Exception) { }
            return System.Drawing.SystemIcons.Application;
        }

        void TrayIconChanged()
        {
            if (tray != null) { try { tray.Icon = BuildTrayIcon(); } catch (Exception) { } }
        }

        void ApplyTray(bool force)
        {
            bool want = !cfg.NoTray && (force || cfg.NotifyStart || cfg.NotifyEnd || cfg.TrayOnClose);
            if (want && tray == null)
            {
                tray = new System.Windows.Forms.NotifyIcon();
                tray.Text = "Noctua";
                tray.Icon = BuildTrayIcon();
                tray.MouseUp += delegate(object ts, System.Windows.Forms.MouseEventArgs te)
                {
                    if (te.Button != System.Windows.Forms.MouseButtons.Right) return;
                    Dispatcher.BeginInvoke(new Action(ShowTrayMenu));
                };
                tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowFromTray)); };
            }
            if (tray != null) tray.Visible = want;
        }

        // an invisible helper window gives the popup menu somewhere to live (and close from)
        void ShowTrayMenu()
        {
            if (trayHost == null)
            {
                trayHost = new Window();
                trayHost.WindowStyle = WindowStyle.None;
                trayHost.AllowsTransparency = true;
                trayHost.Background = System.Windows.Media.Brushes.Transparent;
                trayHost.ShowInTaskbar = false;
                trayHost.Width = 1; trayHost.Height = 1;
                trayHost.Left = -10000; trayHost.Top = -10000;
                trayHost.Topmost = true;
            }
            trayHost.Show();
            trayHost.Activate();
            ContextMenu m = UI.Menu(
                new KeyValuePair<string, RoutedEventHandler>("Open Noctua", delegate { ShowFromTray(); }),
                new KeyValuePair<string, RoutedEventHandler>("Quit", delegate { quitting = true; Close(); }));
            System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
            m.PlacementTarget = null;
            m.Placement = PlacementMode.AbsolutePoint;
            // the cursor is in physical pixels, popups are placed in device-independent units
            System.Windows.DpiScale ds = System.Windows.Media.VisualTreeHelper.GetDpi(trayHost);
            m.HorizontalOffset = p.X / ds.DpiScaleX;
            m.VerticalOffset = p.Y / ds.DpiScaleY;
            m.MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height - 40);
            m.Closed += delegate { trayHost.Hide(); };
            m.IsOpen = true;
        }

        void ShowFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Maximized;
            Activate();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (cfg.TrayOnClose && !quitting && tray != null)
            {
                e.Cancel = true;
                Hide();
                if (!trayHinted)
                {
                    trayHinted = true;
                    try { tray.ShowBalloonTip(4000, "Still running", "Noctua keeps running here so it can remind you about classes. Right-click the icon to quit.", System.Windows.Forms.ToolTipIcon.Info); } catch (Exception) { }
                }
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch (Exception) { }
            try { if (trayHost != null) trayHost.Close(); } catch (Exception) { }
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        // the Windows notification itself
        void PushToast(string title, string body) { PushToast(title, body, ""); }

        StackPanel toastHost;

        // the same message as a card in the top right corner of the app
        void ShowInAppToast(string title, string body, string go)
        {
            if (toastHost == null)
            {
                toastHost = new StackPanel();
                toastHost.HorizontalAlignment = HorizontalAlignment.Right;
                toastHost.VerticalAlignment = VerticalAlignment.Top;
                toastHost.Margin = new Thickness(0, 72, 20, 0);
                toastHost.Width = 340;
                Panel.SetZIndex(toastHost, 200);
                Grid.SetColumnSpan(toastHost, 2);
            }
            if (toastHost.Parent == null) root.Children.Add(toastHost);
            while (toastHost.Children.Count >= 3) toastHost.Children.RemoveAt(0);
            StackPanel tx = new StackPanel();
            tx.Children.Add(UI.Text(title, 13.5, "T1", true));
            if (body.Length > 0) tx.Children.Add(UI.Margin(UI.Wrap(body, 12.5, "T2"), 0, 3, 0, 0));
            Border card = new Border();
            card.Padding = new Thickness(14, 12, 14, 12);
            card.Margin = new Thickness(0, 0, 0, 10);
            card.CornerRadius = new CornerRadius(12);
            card.BorderThickness = new Thickness(1);
            card.Cursor = go.Length > 0 ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
            card.SetResourceReference(Border.BackgroundProperty, "Bg2");
            card.SetResourceReference(Border.BorderBrushProperty, "Line2");
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = Theme.ShadowOp, Color = Theme.Dark ? System.Windows.Media.Colors.Black : System.Windows.Media.Color.FromRgb(40, 50, 75) };
            card.Child = tx;
            Action close = delegate { if (toastHost != null) toastHost.Children.Remove(card); };
            card.MouseLeftButtonUp += delegate { close(); if (go.Length > 0) OpenNote(go); };
            toastHost.Children.Add(card);
            UI.FadeIn(card, 0);
            System.Windows.Threading.DispatcherTimer life = new System.Windows.Threading.DispatcherTimer();
            life.Interval = TimeSpan.FromSeconds(8);
            life.Tick += delegate { life.Stop(); close(); };
            life.Start();
        }

        void PushToast(string title, string body, string go)
        {
            if (fsOn) { Log.Write("notification: full screen"); FsToast(title, body, go ?? ""); return; }
            if (cfg.NoTray) { Log.Write("notification: in the app"); ShowInAppToast(title, body, go ?? ""); return; }
            ApplyTray(true);
            if (tray == null) return;
            Log.Write("notification: " + (title.Length > 0 ? "sent" : "empty"));
            try { tray.ShowBalloonTip(10000, title, body, System.Windows.Forms.ToolTipIcon.Info); } catch (Exception ex) { Log.Error("notification", ex); }
        }

        // once a second: fires "starts soon" / "ends soon" reminders for today's classes
        void CheckNotify()
        {
            if (snap == null || (!cfg.NotifyStart && !cfg.NotifyEnd) || cfg.Schedules.Count == 0) return;
            DateTime nowDt = DateTime.Now;
            if (notifyPlan == null || notifyPlan.Date != nowDt.Date || (nowDt - notifyPlanAt).TotalSeconds > 60)
            {
                notifyPlan = Sched.Plan(cfg, snap, term, nowDt.Date);
                notifyPlanAt = nowDt;
            }
            if (!notifyPlan.School) return;
            TimeSpan now = nowDt.TimeOfDay;
            int lead = cfg.NotifyLead;
            for (int i = 0; i < notifyPlan.Slots.Count; i++)
            {
                Slot sl = notifyPlan.Slots[i];
                if (!sl.IsClass) continue;
                string where = sl.R != null && sl.R.Room.Length > 0 ? "  ·  Room " + sl.R.Room : "";
                if (cfg.NotifyStart)
                {
                    TimeSpan t = sl.B.Start - TimeSpan.FromMinutes(lead);
                    string id = nowDt.ToString("yyyyMMdd") + "s" + i;
                    if (now >= t && now < t + TimeSpan.FromMinutes(2) && now < sl.B.End && fired.Add(id))
                        Toast(sl.Title, (lead > 0 ? "Starts in " + lead + " min" : "Starting now") + "  ·  " + Bell.Clock(sl.B.Start) + where);
                }
                if (cfg.NotifyEnd)
                {
                    TimeSpan t = sl.B.End - TimeSpan.FromMinutes(lead);
                    string id = nowDt.ToString("yyyyMMdd") + "e" + i;
                    if (now >= t && now < t + TimeSpan.FromMinutes(2) && now < sl.B.End + TimeSpan.FromMinutes(1) && fired.Add(id))
                        Toast(sl.Title, (lead > 0 ? "Ends in " + lead + " min" : "Class is over") + "  ·  " + Bell.Clock(sl.B.End));
                }
            }
            if (fired.Count > 400) fired.Clear();
        }
    }
}
