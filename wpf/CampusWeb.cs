using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CampusMidnight
{
    // Campus itself, inside Noctua: a window with the real portal, already signed in.
    // It also learns which addresses the portal uses for messages, documents and attendance.
    sealed partial class MainWindow
    {
        Window campusWin;
        WebView2 campusWv;
        bool webDiscBusy;

        // page: "" (home), a page name like documents, or a full path starting with /
        string CampusUrl(string page)
        {
            if (!string.IsNullOrEmpty(page) && page.StartsWith("/")) return api.Origin + page;
            string role = api.Portal == "parents" ? "parent" : "student";
            string pg = string.IsNullOrEmpty(page) ? "home" : page;
            string u = api.Origin + "/campus/nav-wrapper/" + role + "/portal/" + role + "/" + pg;
            if (!string.IsNullOrEmpty(api.AppName)) u += "?appName=" + Uri.EscapeDataString(api.AppName);
            return u;
        }

        static readonly Regex CapWanted = new Regex("attend|absen|document|messag|inbox|messenger|notice|announce|behavior|fee|health", RegexOptions.IgnoreCase);

        // makes the web view ready: own profile folder, the signed-in cookies, and a listener that notes portal addresses
        async Task PrepareWeb(WebView2 wv)
        {
            string data = Path.Combine(Disk.Dir(), "webview");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, data);
            await wv.EnsureCoreWebView2Async(env);
            string host = new Uri(api.Origin).Host;
            Api a = api;
            foreach (Cookie c in a.CookieList())
            {
                try
                {
                    CoreWebView2Cookie wc = wv.CoreWebView2.CookieManager.CreateCookie(c.Name, c.Value, host, string.IsNullOrEmpty(c.Path) ? "/" : c.Path);
                    wc.IsSecure = c.Secure; wc.IsHttpOnly = c.HttpOnly;
                    wv.CoreWebView2.CookieManager.AddOrUpdateCookie(wc);
                }
                catch (Exception) { }
            }
            wv.CoreWebView2.WebResourceResponseReceived += delegate(object s2, CoreWebView2WebResourceResponseReceivedEventArgs e2)
            {
                try
                {
                    Uri u = new Uri(e2.Request.Uri);
                    if (!string.Equals(u.Host, host, StringComparison.OrdinalIgnoreCase)) return;
                    string pq = u.AbsolutePath;
                    int sc = e2.Response.StatusCode;
                    if (sc < 200 || sc >= 300) return;
                    if (!(pq.Contains("/api/") || pq.Contains("/resources/"))) return;
                    if (pq.EndsWith(".js") || pq.EndsWith(".css") || pq.EndsWith(".png") || pq.EndsWith(".svg") || pq.EndsWith(".json") || pq.Contains("/nav-wrapper/assets/")) return;
                    // addresses that need a query (dates, ids) are noted with it first
                    if (u.Query.Length > 1 && CapWanted.IsMatch(pq)) a.Capture(pq + u.Query);
                    a.Capture(pq);
                }
                catch (Exception) { }
            };
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
            win.Closed += delegate
            {
                campusWin = null; campusWv = null;
                try
                {
                    if (api != null && api.CapturedNew > 0)
                    {
                        api.CapturedNew = 0;
                        api.ResetDiscovery();
                        RefreshData(false);
                    }
                }
                catch (Exception) { }
            };
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

            Button back = UI.IconBtn("", 14, delegate { try { if (wv.CanGoBack) wv.GoBack(); } catch (Exception) { } });
            Button fwd = UI.IconBtn("", 14, delegate { try { if (wv.CanGoForward) wv.GoForward(); } catch (Exception) { } });
            Button rel = UI.IconBtn("", 14, delegate { try { wv.Reload(); } catch (Exception) { } });
            Button home = UI.IconBtn("", 14, delegate { try { if (wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(CampusUrl("")); } catch (Exception) { } });
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
                    await PrepareWeb(wv);
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

        readonly System.Threading.SemaphoreSlim msgGate = new System.Threading.SemaphoreSlim(1, 1);

        // Campus fills a message page in with script after it loads, so a plain download only gets the empty form.
        // This opens the page out of sight in the web view, waits for the words to appear and reads them.
        async Task<string> WebMessageBody(Msg m)
        {
            if (api == null || api.IsDemo || string.IsNullOrEmpty(api.LoginUrl)) return "";
            List<string> urls = new List<string>();
            if (!string.IsNullOrEmpty(m.Url) && m.Url.StartsWith("/")) urls.Add(api.Origin + m.Url);
            List<string> ids = new List<string>();
            foreach (string x in new string[] { m.RecipientId, m.ContextId, m.Id }) if (!string.IsNullOrEmpty(x) && !ids.Contains(x)) ids.Add(x);
            foreach (string rid in ids)
                urls.Add(api.Origin + "/campus/portal/messageView.xsl?x=messenger.MessengerEngine-getMessageRecipientView&messageID=" + Uri.EscapeDataString(m.Id)
                    + "&messageRecipientID=" + Uri.EscapeDataString(rid) + "&processMessageID=" + Uri.EscapeDataString(m.Id) + "&portal=2&hybrid=webviewModal&lang=en");
            string result = "";
            await msgGate.WaitAsync();
            Window w = new Window();
            try
            {
                w.Width = 900; w.Height = 700;
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = -12000; w.Top = -12000;
                w.ShowInTaskbar = false; w.ShowActivated = false; w.WindowStyle = WindowStyle.None;
                WebView2 wv = new WebView2();
                w.Content = wv;
                w.Show();
                await PrepareWeb(wv);
                const string script = "(function(){function t(d){try{return d.body?d.body.innerText:''}catch(e){return ''}}var s=t(document);var f=document.querySelectorAll('iframe');for(var i=0;i<f.length;i++){try{s+='\\n'+t(f[i].contentDocument)}catch(e){}}return s;})()";
                foreach (string u in urls)
                {
                    TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> h = delegate { done.TrySetResult(true); };
                    wv.CoreWebView2.NavigationCompleted += h;
                    wv.CoreWebView2.Navigate(u);
                    await Task.WhenAny(done.Task, Task.Delay(15000));
                    wv.CoreWebView2.NavigationCompleted -= h;
                    string last = "";
                    for (int i = 0; i < 16; i++)
                    {
                        await Task.Delay(700);
                        string raw = await wv.CoreWebView2.ExecuteScriptAsync(script);
                        string txt = "";
                        try { txt = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? ""; } catch (Exception) { }
                        string clean = Api.MessageText(txt);
                        if (clean.Length > 0 && clean == last) { result = clean; break; }   // stopped changing: it has finished filling in
                        last = clean;
                    }
                    if (result.Length > 0) break;
                }
            }
            catch (Exception ex) { Log.Write("message page: " + ex.Message); }
            try { w.Close(); } catch (Exception) { }
            msgGate.Release();
            try
            {
                if (api != null && api.CapturedNew > 0) { api.CapturedNew = 0; api.ResetDiscovery(); }
            }
            catch (Exception) { }
            return result;
        }

        // once in a while, opens the attendance, documents and message pages out of sight so the app learns which addresses they use
        async void WebDiscover()
        {
            if (api == null || api.IsDemo || webDiscBusy || campusWin != null) return;
            string stamp = Disk.At("webdisc-" + Disk.SafeName(api.Host) + ".txt");
            try { if (File.Exists(stamp) && (DateTime.Now - File.GetLastWriteTime(stamp)).TotalHours < 12) return; } catch (Exception) { }
            webDiscBusy = true;
            Window w = new Window();
            try
            {
                w.Width = 900; w.Height = 700;
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = -12000; w.Top = -12000;
                w.ShowInTaskbar = false;
                w.ShowActivated = false;
                w.WindowStyle = WindowStyle.None;
                WebView2 wv = new WebView2();
                w.Content = wv;
                w.Show();
                await PrepareWeb(wv);
                string[] pages = new string[] { "attendance", "documents", "message-center" };
                foreach (string pg in pages)
                {
                    TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> h = delegate { done.TrySetResult(true); };
                    wv.CoreWebView2.NavigationCompleted += h;
                    wv.CoreWebView2.Navigate(CampusUrl(pg));
                    await Task.WhenAny(done.Task, Task.Delay(20000));
                    wv.CoreWebView2.NavigationCompleted -= h;
                    await Task.Delay(6000);
                }
                try { File.WriteAllText(stamp, DateTime.Now.ToString("o")); } catch (Exception) { }
            }
            catch (Exception ex) { Log.Write("web discovery: " + ex.Message); }
            try { w.Close(); } catch (Exception) { }
            webDiscBusy = false;
            try
            {
                if (api != null && api.CapturedNew > 0)
                {
                    api.CapturedNew = 0;
                    api.ResetDiscovery();
                    RefreshData(false);
                }
            }
            catch (Exception) { }
        }
    }
}
