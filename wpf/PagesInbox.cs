using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace CampusMidnight
{
    sealed partial class MainWindow
    {
        string inboxTab = "message";

        void OpenCampusPage(string page) { OpenSite(); }

        ItemSpec MsgRow(Msg m)
        {
            ItemSpec it = new ItemSpec();
            it.Title = m.Subject.Length > 0 ? m.Subject : "(no subject)";
            string prev = m.Body.Replace("\n", " ").Replace("\r", " ");
            if (prev.Length > 110) prev = prev.Substring(0, 110) + "…";
            string sub = m.From;
            if (prev.Length > 0) sub += (sub.Length > 0 ? "  ·  " : "") + prev;
            it.Sub = sub;
            it.Dot = !m.Read;
            if (m.Date.HasValue) { it.Right = Rows.DayLabel(m.Date.Value); it.RightFg = "T3"; }
            if (m.Kind == "notice") { it.Badge = "Announcement"; it.BadgeBg = "AccSoft"; it.BadgeFg = "AccText"; }
            Msg mm = m;
            it.Click = delegate { OpenMessage(mm); };
            return it;
        }

        UIElement PageInbox()
        {
            List<UIElement> kids = new List<UIElement>();
            string[] keys = new string[] { "message", "notice" };
            string[] names = new string[] { "Messages (" + snap.Messages.Count + ")", "Announcements (" + snap.Notices.Count + ")" };
            kids.Add(SegBar(names, inboxTab == "notice" ? 1 : 0, delegate(int k)
            {
                inboxTab = keys[k];
                Dispatcher.BeginInvoke(new Action(delegate { ShowView(false); }));
            }));
            List<Msg> list = inboxTab == "notice" ? snap.Notices : snap.Messages;
            if (list.Count == 0)
            {
                kids.Add(PageKit.Notice(inboxTab == "notice"
                    ? "No announcements were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Original site shows them in the meantime."
                    : "No messages were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Original site shows them in the meantime.", false));
                Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusPage("message-center"); });
                open.HorizontalAlignment = HorizontalAlignment.Left;
                kids.Add(open);
                return Body(kids.ToArray());
            }
            int unread = 0;
            foreach (Msg m in list) if (!m.Read) unread++;
            List<ItemSpec> rows = new List<ItemSpec>();
            for (int i = 0; i < list.Count && i < 200; i++) rows.Add(MsgRow(list[i]));
            kids.Add(PageKit.ListCard(list.Count + (list.Count == 1 ? " item" : " items") + (unread > 0 ? "  ·  " + unread + " unread" : ""), "", rows));
            return Body(kids.ToArray());
        }

        UIElement PageDocs()
        {
            List<UIElement> kids = new List<UIElement>();
            if (snap.Docs.Count == 0)
            {
                kids.Add(PageKit.Notice("No documents were found. If you expect some, your school's portal may serve them from an address this app has not matched yet: open Settings > Diagnostics > Copy report and send it over. Open in Campus shows them in the meantime.", false));
                Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusPage("documents"); });
                open.HorizontalAlignment = HorizontalAlignment.Left;
                kids.Add(open);
                return Body(kids.ToArray());
            }
            List<ItemSpec> rows = new List<ItemSpec>();
            foreach (Doc d in snap.Docs)
            {
                ItemSpec it = new ItemSpec();
                it.Title = d.Name;
                string sub = d.Type;
                if (d.Date.HasValue) sub += (sub.Length > 0 ? "  ·  " : "") + d.Date.Value.ToString("MMM d, yyyy");
                it.Sub = sub;
                Doc dd = d;
                it.Click = delegate { OpenDoc(dd); };
                rows.Add(it);
            }
            kids.Add(PageKit.ListCard(snap.Docs.Count + (snap.Docs.Count == 1 ? " document" : " documents") + "  ·  click to open", "", rows));
            return Body(kids.ToArray());
        }

        void OpenMessage(Msg m)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(UI.Text(m.Kind == "notice" ? "ANNOUNCEMENT" : "MESSAGE", 11.5, "AccText", true));
            p.Children.Add(UI.Margin(UI.Wrap(m.Subject.Length > 0 ? m.Subject : "(no subject)", 22, "T1"), 0, 4, 0, 4));
            string meta = m.From;
            if (m.Date.HasValue) meta += (meta.Length > 0 ? "  ·  " : "") + m.Date.Value.ToString("dddd, MMMM d, yyyy");
            if (meta.Length > 0) p.Children.Add(UI.Margin(UI.Wrap(meta, 13.5, "T2"), 0, 0, 0, 14));
            p.Children.Add(TextCardUi("", m.Body.Length > 0 ? m.Body : "This message has no text.", m.Body.Length == 0));
            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.Margin = new Thickness(0, 8, 0, 0);
            Button open = UI.Btn("Open in Campus", "Pill", delegate { OpenCampusPage("message-center"); });
            Button copy = UI.Btn("Copy text", "Ghost", null);
            string txt = m.Body;
            copy.Margin = new Thickness(10, 0, 0, 0);
            copy.Click += delegate
            {
                try { Clipboard.SetText(txt); copy.Content = "Copied"; }
                catch (Exception) { copy.Content = "Could not copy"; }
            };
            btns.Children.Add(open);
            btns.Children.Add(copy);
            p.Children.Add(btns);
            bool wasUnread = !m.Read;
            if (wasUnread) { m.Read = true; UpdateBadges(); }
            NewWindow(m.Subject.Length > 0 ? m.Subject : "Message", p, 640, 640).ShowDialog();
            if (wasUnread && view == "inbox") ShowView(false);
        }

        void OpenDoc(Doc d)
        {
            if (api == null) return;
            if (d.Url.Length == 0) { OpenCampusPage("documents"); return; }
            Api a = api;
            stampText = "Opening " + d.Name + "…";
            stampBad = false;
            UpdateTop();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                string file = a.Download(d.Url, d.Name);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (file != null)
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo(file);
                            psi.UseShellExecute = true;
                            Process.Start(psi);
                            stampText = snap != null ? "Updated " + snap.At.ToString("h:mm tt") : "";
                        }
                        catch (Exception) { stampText = "Could not open that file."; stampBad = true; }
                    }
                    else { stampText = "Could not download that document."; stampBad = true; }
                    UpdateTop();
                }));
            });
        }
    }
}
