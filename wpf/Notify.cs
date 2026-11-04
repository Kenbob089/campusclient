using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CampusMidnight
{
    // the in-app notification centre, the once-a-minute check for new work and grades, and the offline pill
    sealed partial class MainWindow
    {
        sealed class NoteItem
        {
            public DateTime At = DateTime.Now;
            public string Title = "", Body = "", Go = "";
            public bool Read;
        }

        readonly List<NoteItem> notes = new List<NoteItem>();
        bool notesLoaded;
        Button bellButton;
        Border bellBadge;
        TextBlock bellBadgeText;
        Popup centre;
        Border offlinePill;
        TextBlock offlineText;
        readonly DispatcherTimer watch = new DispatcherTimer();
        bool offline, reconnecting;
        string snapFor = "";

        // ------------------------------------------------------------ storage
        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n"); }
        static string Unesc(string s)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    sb.Append(s[i] == 'n' ? '\n' : (s[i] == 't' ? '\t' : s[i]));
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        void LoadNotes()
        {
            if (notesLoaded) return;
            notesLoaded = true;
            try
            {
                string p = Disk.At("notifications.txt");
                if (!File.Exists(p)) return;
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string[] f = line.Split('\t');
                    if (f.Length < 5) continue;
                    NoteItem n = new NoteItem();
                    long ticks;
                    if (!long.TryParse(f[0], out ticks)) continue;
                    n.At = new DateTime(ticks);
                    n.Read = f[1] == "1";
                    n.Go = f[2];
                    n.Title = Unesc(f[3]);
                    n.Body = Unesc(f[4]);
                    notes.Add(n);
                }
            }
            catch (Exception ex) { Log.Error("loading notifications", ex); }
        }

        void SaveNotes()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (NoteItem n in notes) sb.AppendLine(n.At.Ticks + "\t" + (n.Read ? "1" : "0") + "\t" + n.Go + "\t" + Esc(n.Title) + "\t" + Esc(n.Body));
                File.WriteAllText(Disk.At("notifications.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        int UnreadNotes()
        {
            int n = 0;
            foreach (NoteItem x in notes) if (!x.Read) n++;
            return n;
        }

        // ------------------------------------------------------------ raising a notification
        // goes in the notification centre and to Windows, like the class reminders always did
        void Notify(string title, string body, string go)
        {
            AddNote(title, body, go);
            PushToast(title, body);
        }

        void AddNote(string title, string body, string go)
        {
            LoadNotes();
            NoteItem n = new NoteItem();
            n.Title = title; n.Body = body; n.Go = go;
            notes.Insert(0, n);
            if (notes.Count > 80) notes.RemoveRange(80, notes.Count - 80);
            SaveNotes();
            UpdateBell();
        }

        // ------------------------------------------------------------ the bell in the top bar
        FrameworkElement BuildTopRight()
        {
            LoadNotes();
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;

            offlinePill = new Border();
            offlinePill.CornerRadius = new CornerRadius(14);
            offlinePill.Height = 28;
            offlinePill.VerticalAlignment = VerticalAlignment.Center;
            offlinePill.Padding = new Thickness(10, 0, 12, 0);
            offlinePill.Margin = new Thickness(0, 0, 8, 0);
            offlinePill.Cursor = Cursors.Hand;
            offlinePill.SetResourceReference(Border.BackgroundProperty, "BadSoft");
            offlinePill.Visibility = Visibility.Collapsed;
            StackPanel pr = new StackPanel();
            pr.Orientation = Orientation.Horizontal;
            pr.VerticalAlignment = VerticalAlignment.Center;
            TextBlock ri = UI.Icon("\uE72C", 12, "Bad");
            ri.Margin = new Thickness(0, 0, 6, 0);
            ri.VerticalAlignment = VerticalAlignment.Center;
            offlineText = UI.Text("Failed to load", 12, "Bad", true);
            offlineText.VerticalAlignment = VerticalAlignment.Center;
            pr.Children.Add(ri); pr.Children.Add(offlineText);
            offlinePill.Child = pr;
            offlinePill.MouseLeftButtonUp += delegate { Reconnect(); };
            offlinePill.ToolTip = "Reload and sign in again";
            sp.Children.Add(offlinePill);

            Grid bellHost = new Grid();
            bellButton = UI.IconBtn("\uE7ED", 16, null);
            System.Windows.Shapes.Path bell = new System.Windows.Shapes.Path();
            bell.Data = Geometry.Parse("M12,3 C8.5,3 6.5,5.7 6.5,9 L6.5,12.5 C6.5,14 5.5,15.2 4.5,16.2 L19.5,16.2 C18.5,15.2 17.5,14 17.5,12.5 L17.5,9 C17.5,5.7 15.5,3 12,3 Z M10,19 C10.3,20.2 11,21 12,21 C13,21 13.7,20.2 14,19");
            bell.StrokeThickness = 1.7;
            bell.StrokeLineJoin = PenLineJoin.Round;
            bell.StrokeStartLineCap = PenLineCap.Round;
            bell.StrokeEndLineCap = PenLineCap.Round;
            bell.Fill = Brushes.Transparent;
            bell.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "T2");
            bell.Stretch = Stretch.Uniform;
            bell.Width = 18; bell.Height = 18;
            bellButton.Content = bell;
            bellButton.VerticalAlignment = VerticalAlignment.Center;
            bellButton.Click += delegate { ToggleCentre(); };
            bellButton.ToolTip = "Notifications";
            bellHost.Children.Add(bellButton);
            bellBadge = new Border();
            bellBadge.CornerRadius = new CornerRadius(8);
            bellBadge.MinWidth = 16; bellBadge.Height = 16;
            bellBadge.Padding = new Thickness(4, 0, 4, 0);
            bellBadge.HorizontalAlignment = HorizontalAlignment.Right;
            bellBadge.VerticalAlignment = VerticalAlignment.Top;
            bellBadge.Margin = new Thickness(0, 2, 0, 0);
            bellBadge.IsHitTestVisible = false;
            bellBadge.SetResourceReference(Border.BackgroundProperty, "Acc");
            bellBadgeText = UI.Text("", 10.5, "T1", true);
            bellBadgeText.Foreground = Brushes.White;
            bellBadgeText.HorizontalAlignment = HorizontalAlignment.Center;
            bellBadgeText.VerticalAlignment = VerticalAlignment.Center;
            bellBadge.Child = bellBadgeText;
            bellHost.Children.Add(bellBadge);
            sp.Children.Add(bellHost);
            UpdateBell();
            return sp;
        }

        void UpdateBell()
        {
            if (bellBadge == null) return;
            int n = UnreadNotes();
            bellBadge.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
            bellBadgeText.Text = n > 9 ? "9+" : n.ToString();
            if (offlinePill != null)
            {
                offlinePill.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
                offlineText.Text = reconnecting ? "Reloading…" : "Failed to load";
            }
        }

        static string Ago(DateTime t)
        {
            TimeSpan d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "Just now";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " min ago";
            if (d.TotalHours < 24 && t.Date == DateTime.Today) return (int)d.TotalHours + " h ago";
            if (t.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
            return t.ToString("MMM d");
        }

        void ToggleCentre()
        {
            if (centre != null && centre.IsOpen) { centre.IsOpen = false; return; }
            BuildCentre();
        }

        void BuildCentre()
        {
            LoadNotes();
            if (centre == null)
            {
                centre = new Popup();
                centre.StaysOpen = false;
                centre.AllowsTransparency = true;
                centre.Placement = PlacementMode.Bottom;
                centre.HorizontalOffset = 0;
            }
            centre.PlacementTarget = bellButton;
            centre.IsOpen = false;

            StackPanel col = new StackPanel();
            Grid head = UI.Cols(1, 0, 0);
            head.Margin = new Thickness(4, 0, 0, 10);
            UI.At(head, UI.Text("Notifications", 16, "T1", true), 0);
            Button mark = UI.Btn("Mark all read", "Flat", delegate { foreach (NoteItem x in notes) x.Read = true; SaveNotes(); UpdateBell(); BuildCentre(); });
            mark.Padding = new Thickness(8, 4, 8, 4);
            mark.Foreground = (Brush)Application.Current.Resources["AccText"];
            mark.FontSize = 12.5;
            Button clear = UI.Btn("Clear", "Flat", delegate { notes.Clear(); SaveNotes(); UpdateBell(); BuildCentre(); });
            clear.Padding = new Thickness(8, 4, 8, 4);
            clear.Foreground = (Brush)Application.Current.Resources["T3"];
            clear.FontSize = 12.5;
            UI.At(head, mark, 1);
            UI.At(head, clear, 2);
            col.Children.Add(head);

            StackPanel list = new StackPanel();
            if (notes.Count == 0)
            {
                TextBlock e = UI.Wrap("Nothing yet. New assignments, new grades and class reminders will show up here.", 13, "T3");
                e.Margin = new Thickness(4, 14, 4, 18);
                list.Children.Add(e);
            }
            foreach (NoteItem n in notes)
            {
                NoteItem ni = n;
                Grid g = UI.Cols(0, 1);
                Border dot = new Border();
                dot.Width = 8; dot.Height = 8; dot.CornerRadius = new CornerRadius(4);
                dot.Margin = new Thickness(0, 6, 10, 0);
                dot.VerticalAlignment = VerticalAlignment.Top;
                if (!n.Read) dot.SetResourceReference(Border.BackgroundProperty, "Acc");
                UI.At(g, dot, 0);
                StackPanel tx = new StackPanel();
                Grid tl = UI.Cols(1, 0);
                UI.At(tl, UI.Text(n.Title, 13.5, "T1", true), 0);
                TextBlock ag = UI.Text(Ago(n.At), 11.5, "T3", false);
                ag.Margin = new Thickness(10, 1, 0, 0);
                UI.At(tl, ag, 1);
                tx.Children.Add(tl);
                if (n.Body.Length > 0) tx.Children.Add(UI.Margin(UI.Wrap(n.Body, 12.5, "T2"), 0, 2, 0, 0));
                UI.At(g, tx, 1);
                Border row = new Border();
                row.CornerRadius = new CornerRadius(9);
                row.Padding = new Thickness(8, 8, 8, 8);
                row.Background = Brushes.Transparent;
                row.Child = g;
                row.Cursor = Cursors.Hand;
                row.MouseEnter += delegate { row.SetResourceReference(Border.BackgroundProperty, "Bg3"); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                row.MouseLeftButtonUp += delegate
                {
                    ni.Read = true; SaveNotes(); UpdateBell();
                    centre.IsOpen = false;
                    if (ni.Go.Length > 0 && snap != null) Go(ni.Go);
                };
                list.Children.Add(row);
            }
            ScrollViewer sv = UI.Scroll(list);
            sv.MaxHeight = Math.Max(240, Math.Min(480, SystemParameters.WorkArea.Height - 160));
            col.Children.Add(sv);

            Border card = new Border();
            card.Width = 380;
            card.Padding = new Thickness(14);
            card.Margin = new Thickness(12, 6, 26, 30);
            card.CornerRadius = new CornerRadius(14);
            card.SetResourceReference(Border.BackgroundProperty, "Bg2");
            card.SetResourceReference(Border.BorderBrushProperty, "Line2");
            card.BorderThickness = new Thickness(1);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 4, Opacity = Theme.ShadowOp, Color = Theme.Dark ? Colors.Black : Color.FromRgb(40, 50, 75) };
            card.Child = col;
            centre.Child = card;
            // line the right edge of the panel up with the bell
            centre.HorizontalOffset = bellButton.ActualWidth - 380 - 12 - 26 + 14;
            centre.IsOpen = true;
            UI.FadeIn(card, 0);
        }

        // ------------------------------------------------------------ what changed since last time
        static string AKey(Assign a)
        {
            return a.Id.Length > 0 ? "id:" + a.Id : a.Course + "|" + a.Name + "|" + (a.Due.HasValue ? a.Due.Value.ToString("yyyyMMdd") : "");
        }

        static string Fingerprint(Snapshot s)
        {
            if (s == null) return "";
            double sum = 0;
            foreach (Course c in s.Courses) if (c.Percent.HasValue) sum += c.Percent.Value;
            int graded = 0;
            foreach (Assign a in s.Assigns) if (a.Status == "graded") graded++;
            return s.Courses.Count + "|" + Math.Round(sum, 2) + "|" + s.Assigns.Count + "|" + graded + "|" + s.Messages.Count + "|" + s.Notices.Count + "|" + s.Docs.Count;
        }

        // new work, newly graded work and new messages between two loads
        List<string[]> Changes(Snapshot oldS, Snapshot newS)
        {
            List<string[]> found = new List<string[]>();   // title, body, view
            if (oldS == null || newS == null || !oldS.HasAny) return found;
            Dictionary<string, Assign> before = new Dictionary<string, Assign>();
            foreach (Assign a in oldS.Assigns) before[AKey(a)] = a;
            List<string[]> fresh = new List<string[]>(), graded = new List<string[]>();
            foreach (Assign a in newS.Assigns)
            {
                if (a.InDropped) continue;
                Assign o;
                string due = a.Due.HasValue ? "  ·  due " + a.Due.Value.ToString("MMM d") : "";
                if (!before.TryGetValue(AKey(a), out o))
                {
                    if (a.Status == "graded" && a.ScoreText.Length > 0) graded.Add(new string[] { "Graded: " + a.Name, a.Course + "  ·  " + a.ScoreText + (a.Percent.HasValue ? "  (" + Rows.Fmt(a.Percent) + "%)" : ""), "assignments" });
                    else fresh.Add(new string[] { "New assignment: " + a.Name, a.Course + due, "assignments" });
                }
                else if (o.Status != "graded" && a.Status == "graded" && a.ScoreText.Length > 0)
                    graded.Add(new string[] { "Graded: " + a.Name, a.Course + "  ·  " + a.ScoreText + (a.Percent.HasValue ? "  (" + Rows.Fmt(a.Percent) + "%)" : ""), "assignments" });
                else if (o.Status == "graded" && a.Status == "graded" && o.ScoreText != a.ScoreText && a.ScoreText.Length > 0)
                    graded.Add(new string[] { "Score changed: " + a.Name, a.Course + "  ·  " + o.ScoreText + " to " + a.ScoreText, "assignments" });
            }
            AddGrouped(found, fresh, " new assignments", "assignments");
            AddGrouped(found, graded, " assignments graded", "grades");
            HashSet<string> seen = new HashSet<string>();
            foreach (Msg m in oldS.Messages) seen.Add(m.Id);
            foreach (Msg m in oldS.Notices) seen.Add(m.Id);
            List<string[]> msgs = new List<string[]>();
            foreach (Msg m in newS.Messages) if (m.Id.Length > 0 && !seen.Contains(m.Id)) msgs.Add(new string[] { "New message: " + (m.Subject.Length > 0 ? m.Subject : "(no subject)"), m.From, "inbox" });
            foreach (Msg m in newS.Notices) if (m.Id.Length > 0 && !seen.Contains(m.Id)) msgs.Add(new string[] { "New announcement: " + (m.Subject.Length > 0 ? m.Subject : "(no subject)"), m.From, "inbox" });
            AddGrouped(found, msgs, " new messages", "inbox");
            return found;
        }

        static void AddGrouped(List<string[]> into, List<string[]> items, string plural, string view)
        {
            if (items.Count == 0) return;
            if (items.Count <= 3) { into.AddRange(items); return; }
            into.Add(new string[] { items.Count + plural, items[0][0] + " and more", view });
        }

        // ------------------------------------------------------------ loading
        static bool LooksOffline(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                WebException we = e as WebException;
                if (we != null && (we.Status == WebExceptionStatus.ConnectFailure || we.Status == WebExceptionStatus.NameResolutionFailure
                    || we.Status == WebExceptionStatus.Timeout || we.Status == WebExceptionStatus.ProxyNameResolutionFailure
                    || we.Status == WebExceptionStatus.SendFailure || we.Status == WebExceptionStatus.ReceiveFailure
                    || we.Status == WebExceptionStatus.ConnectionClosed)) return true;
                if (e is System.Net.Sockets.SocketException) return true;
            }
            return false;
        }

        void SetOffline(bool v)
        {
            if (offline == v && !reconnecting) { UpdateBell(); return; }
            offline = v;
            if (!v) reconnecting = false;
            Log.Write(v ? "offline: keeping the data already loaded" : "back online");
            UpdateBell();
        }

        // the red pill: check the connection, sign in again if the session ended, then refresh
        void Reconnect()
        {
            if (reconnecting || api == null) return;
            reconnecting = true;
            UpdateBell();
            Api a = api;
            Task.Run(delegate
            {
                bool reach = false, auth = false;
                try { reach = a.Reachable(); auth = reach && a.CheckAuth(); } catch (Exception) { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    reconnecting = false;
                    if (a != api) return;
                    if (!reach) { UpdateBell(); return; }
                    if (!auth) { SetOffline(false); SessionEnded(); return; }
                    SetOffline(false);
                    RefreshData(false);
                }));
            });
        }

        void StartWatch()
        {
            watch.Interval = TimeSpan.FromSeconds(30);
            watch.Tick -= WatchTick;
            watch.Tick += WatchTick;
            watch.Start();
        }

        void WatchTick(object sender, EventArgs e)
        {
            if (api == null) return;
            RefreshData(true);
        }
    }
}
