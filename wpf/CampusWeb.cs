using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CampusMidnight
{
    // Campus itself, inside Noctua: a window with the real portal, already signed in
    sealed partial class MainWindow
    {
        Window campusWin;
        WebView2 campusWv;

        string CampusUrl(string page)
        {
            string role = api.Portal == "parents" ? "parent" : "student";
            string pg = string.IsNullOrEmpty(page) ? "home" : page;
            string u = api.Origin + "/campus/nav-wrapper/" + role + "/portal/" + role + "/" + pg;
            if (!string.IsNullOrEmpty(api.AppName)) u += "?appName=" + Uri.EscapeDataString(api.AppName);
            return u;
        }

        void OpenCampusWindow(string page)
        {
            if (api == null || api.IsDemo || string.IsNullOrEmpty(api.LoginUrl)) { OpenSiteExternal(); return; }
            if (campusWin != null)
            {
                try
                {
                    if (campusWv != null && campusWv.CoreWebView2 != null) campusWv.CoreWebView2.Navigate(CampusUrl(page));
                    campusWin.Activate();
                    return;
                }
                catch (Exception) { campusWin = null; }
            }

            Window win = new Window();
            campusWin = win;
            win.Title = "Campus";
            Window own = this;
            foreach (Window ow in Application.Current.Windows) if (ow != win && ow.IsActive && ow.IsVisible) { own = ow; break; }
            win.Owner = own;
            Rect wa = SystemParameters.WorkArea;
            win.Width = Math.Min(1180, wa.Width - 80); win.Height = Math.Min(820, wa.Height - 80);
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.FontFamily = new FontFamily("Segoe UI");
            win.UseLayoutRounding = true;
            win.SetResourceReference(Window.BackgroundProperty, "Bg0");
            try { win.Icon = Icon; } catch (Exception) { }
            win.SourceInitialized += delegate { try { ChromeColors(new WindowInteropHelper(win).Handle); } catch (Exception) { } };
            win.Closed += delegate { campusWin = null; campusWv = null; };
            win.PreviewKeyDown += delegate(object s0, KeyEventArgs e0) { if (e0.Key == Key.Escape) win.Close(); };

            Grid g = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = new GridLength(1, GridUnitType.Star);
            g.RowDefinitions.Add(r0); g.RowDefinitions.Add(r1);

            WebView2 wv = new WebView2();
            campusWv = wv;
            Grid.SetRow(wv, 1);
            wv.Visibility = Visibility.Hidden;
            TextBlock status = UI.Text("Opening Campus…", 14, "T2", false);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            status.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(status, 1);

            Button back = UI.IconBtn("\uE72B", 14, delegate { try { if (wv.CanGoBack) wv.GoBack(); } catch (Exception) { } });
            Button fwd = UI.IconBtn("\uE72A", 14, delegate { try { if (wv.CanGoForward) wv.GoForward(); } catch (Exception) { } });
            Button rel = UI.IconBtn("\uE72C", 14, delegate { try { wv.Reload(); } catch (Exception) { } });
            Button home = UI.IconBtn("\uE80F", 14, delegate { try { if (wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(CampusUrl("")); } catch (Exception) { } });
            home.ToolTip = "Campus home";
            Button ext = UI.Btn("Open in browser", "Ghost", delegate { OpenSiteExternal(); });
            back.ToolTip = "Back"; fwd.ToolTip = "Forward"; rel.ToolTip = "Reload";
            ext.VerticalAlignment = VerticalAlignment.Center;
            ext.Margin = new Thickness(8, 0, 0, 0);
            StackPanel bar = UI.Row(back, fwd, rel, home, ext);
            bar.Margin = new Thickness(8, 6, 8, 6);
            Grid.SetRow(bar, 0);
            g.Children.Add(bar); g.Children.Add(wv); g.Children.Add(status);
            win.Content = g;

            string url = CampusUrl(page);

            win.Loaded += async delegate
            {
                try
                {
                    string data = Path.Combine(Disk.Dir(), "webview");
                    CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, data);
                    await wv.EnsureCoreWebView2Async(env);
                    string host = new Uri(api.Origin).Host;
                    foreach (Cookie c in api.CookieList())
                    {
                        try
                        {
                            CoreWebView2Cookie wc = wv.CoreWebView2.CookieManager.CreateCookie(c.Name, c.Value, host, string.IsNullOrEmpty(c.Path) ? "/" : c.Path);
                            wc.IsSecure = c.Secure; wc.IsHttpOnly = c.HttpOnly;
                            wv.CoreWebView2.CookieManager.AddOrUpdateCookie(wc);
                        }
                        catch (Exception) { }
                    }
                    wv.CoreWebView2.NewWindowRequested += delegate(object s1, CoreWebView2NewWindowRequestedEventArgs e1) { e1.Handled = true; wv.CoreWebView2.Navigate(e1.Uri); };
                    wv.CoreWebView2.NavigationCompleted += delegate { status.Visibility = Visibility.Collapsed; wv.Visibility = Visibility.Visible; };
                    wv.CoreWebView2.Navigate(url);
                }
                catch (Exception ex)
                {
                    Log.Write("campus window: " + ex.Message);
                    try { win.Close(); } catch (Exception) { }
                    OpenSiteExternal();
                }
            };
            win.Show();
        }
    }
}
