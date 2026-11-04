using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CampusMidnight
{
    class AuthException : Exception
    {
        public AuthException() : base("Your Campus session ended. Please sign in again.") { }
    }

    // Talks only to the single Campus server you signed in to (the origin is fixed at sign-in).
    class Api
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

        readonly CookieContainer jar = new CookieContainer();

        // addresses the in-app Campus window saw the portal use (messages, documents...): next refresh tries them
        readonly object capLock = new object();
        HashSet<string> captured;
        public int CapturedNew;

        string CapFile() { return Disk.At("captured-" + Disk.SafeName(Host) + ".txt"); }

        public void Capture(string pathAndQuery)
        {
            lock (capLock)
            {
                if (captured == null)
                {
                    captured = new HashSet<string>();
                    try { if (File.Exists(CapFile())) foreach (string l in File.ReadAllLines(CapFile())) if (l.Length > 0) captured.Add(l); } catch (Exception) { }
                }
                if (captured.Contains(pathAndQuery)) return;
                captured.Add(pathAndQuery);
                CapturedNew++;
                try { File.AppendAllText(CapFile(), pathAndQuery + "\n"); } catch (Exception) { }
            }
            Log.Write("campus window: portal address seen " + pathAndQuery);
        }

        public void ResetDiscovery()
        {
            lock (discLock) { discovered = null; }
        }

        List<string> CapturedPaths()
        {
            List<string> r = new List<string>();
            try { if (File.Exists(CapFile())) foreach (string l in File.ReadAllLines(CapFile())) if (l.StartsWith("/") && !r.Contains(l)) r.Add(l); } catch (Exception) { }
            return r;
        }

        // the signed-in session's cookies, so the in-app Campus window can start already signed in
        public List<Cookie> CookieList()
        {
            List<Cookie> l = new List<Cookie>();
            foreach (string path in new string[] { "/", "/campus/", "/campus/nav-wrapper/" })
            {
                try
                {
                    foreach (Cookie c in jar.GetCookies(new Uri(Origin + path)))
                    {
                        bool dup = false;
                        foreach (Cookie x in l) if (x.Name == c.Name && x.Path == c.Path) { dup = true; break; }
                        if (!dup) l.Add(c);
                    }
                }
                catch (Exception) { }
            }
            return l;
        }
        public readonly string Origin;
        public readonly string Host;
        public readonly string LoginUrl;
        public readonly string AppName;
        public readonly string Portal;

        public bool IsDemo { get { return Demo.IsDemoHost(Host); } }

        public Api(string origin, string loginUrl, string appName, string portal)
        {
            Origin = origin;
            Host = new Uri(origin).Host;
            LoginUrl = loginUrl;
            AppName = appName;
            Portal = portal;
        }

        static Api()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 12288); }
            catch (Exception)
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch (Exception) { }
            }
            ServicePointManager.Expect100Continue = false;
        }

        // ---------------------------------------------------------------- link parsing
        public static bool ParseLoginUrl(string input, out string origin, out string app, out string portal, out string error)
        {
            origin = null; app = null; portal = "students"; error = null;
            string s = (input ?? "").Trim();
            if (s.Length == 0) { error = "Paste the link you normally use to sign in to Campus."; return false; }
            if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                s = "https://" + s;
            Uri u;
            if (!Uri.TryCreate(s, UriKind.Absolute, out u) || !u.Host.Contains("."))
            {
                error = "That does not look like a web address.";
                return false;
            }
            if (u.Scheme != Uri.UriSchemeHttps)
            {
                error = "The link must start with https://";
                return false;
            }
            origin = u.GetLeftPart(UriPartial.Authority);
            string path = u.AbsolutePath;
            if (path.IndexOf("/parents/", StringComparison.OrdinalIgnoreCase) >= 0) portal = "parents";
            string[] segs = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = segs.Length - 1; i >= 0; i--)
            {
                if (segs[i].EndsWith(".jsp", StringComparison.OrdinalIgnoreCase))
                {
                    app = segs[i].Substring(0, segs[i].Length - 4);
                    break;
                }
            }
            if (string.IsNullOrEmpty(app))
            {
                string q = u.Query;
                int qi = q.IndexOf("appName=", StringComparison.OrdinalIgnoreCase);
                if (qi >= 0)
                {
                    string rest = q.Substring(qi + 8);
                    int amp = rest.IndexOf('&');
                    app = Uri.UnescapeDataString(amp >= 0 ? rest.Substring(0, amp) : rest);
                }
            }
            if (string.IsNullOrEmpty(app)) app = u.Host.Split('.')[0];
            return true;
        }

        // ---------------------------------------------------------------- HTTP
        byte[] RequestRaw(string method, string url, string body, out int status, out string ctype)
        {
            Uri target = new Uri(url);
            if (!string.Equals(target.Host, Host, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Blocked request to another site.");
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(target);
            rq.Method = method;
            rq.CookieContainer = jar;
            rq.UserAgent = UA;
            rq.Accept = "application/json, text/plain, text/html, image/*, */*";
            rq.Timeout = 30000;
            rq.ReadWriteTimeout = 30000;
            rq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            rq.AllowAutoRedirect = true;
            if (body != null)
            {
                byte[] b = Encoding.UTF8.GetBytes(body);
                rq.ContentType = "application/x-www-form-urlencoded";
                rq.ContentLength = b.Length;
                using (Stream s = rq.GetRequestStream()) { s.Write(b, 0, b.Length); }
            }
            HttpWebResponse rs = null;
            try { rs = (HttpWebResponse)rq.GetResponse(); }
            catch (WebException ex)
            {
                rs = ex.Response as HttpWebResponse;
                if (rs == null) throw;
            }
            using (rs)
            {
                status = (int)rs.StatusCode;
                ctype = rs.ContentType ?? "";
                bool sameHost = string.Equals(rs.ResponseUri.Host, Host, StringComparison.OrdinalIgnoreCase);
                if (!sameHost) { status = 401; return new byte[0]; } // bounced to another site (e.g. a sign-in page)
                using (Stream st = rs.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[16384];
                    int n;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, n);
                        if (ms.Length > 30 * 1024 * 1024) break;
                    }
                    return ms.ToArray();
                }
            }
        }

        static string AsText(byte[] b)
        {
            if (b == null || b.Length == 0) return "";
            int skip = (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) ? 3 : 0;
            return Encoding.UTF8.GetString(b, skip, b.Length - skip);
        }

        string Request(string method, string url, string body, out int status)
        {
            string ct;
            return AsText(RequestRaw(method, url, body, out status, out ct));
        }

        string Absolute(string pathOrUrl)
        {
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return pathOrUrl;
            if (!pathOrUrl.StartsWith("/")) pathOrUrl = "/campus/" + pathOrUrl;
            return Origin + pathOrUrl;
        }

        // the text of one message: Campus shows it on a page of its own, so read that page and keep the words
        public string MessageBody(string messageId, string recipientId, string contextId, string url)
        {
            List<string> tries = new List<string>();
            if (!string.IsNullOrEmpty(url) && (url.StartsWith("/") || url.StartsWith(Origin, StringComparison.OrdinalIgnoreCase))) tries.Add(url);
            List<string> ids = new List<string>();
            foreach (string x in new string[] { recipientId, contextId, messageId }) if (!string.IsNullOrEmpty(x) && !ids.Contains(x)) ids.Add(x);
            foreach (string rid in ids)
                tries.Add("/campus/portal/messageView.xsl?x=messenger.MessengerEngine-getMessageRecipientView&messageID=" + Uri.EscapeDataString(messageId)
                    + "&messageRecipientID=" + Uri.EscapeDataString(rid) + "&processMessageID=" + Uri.EscapeDataString(messageId) + "&portal=2&hybrid=webviewModal&lang=en");
            foreach (string t in tries)
            {
                try
                {
                    int st;
                    string html = Fetch(t, out st);
                    if (st < 200 || st >= 300 || html.Length == 0) continue;
                    html = Regex.Replace(html, @"<(script|style|head)[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    string plain = J.StripHtml(html).Trim();
                    if (plain.Length > 15) return plain;
                }
                catch (Exception) { }
            }
            return "";
        }

        public string Fetch(string path, out int status)
        {
            return Request("GET", Absolute(path), null, out status);
        }

        // true when the school's server can be reached at all (any answer, even an error page, counts)
        public bool Reachable()
        {
            if (IsDemo) return !DemoOffline;
            try { if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false; } catch (Exception) { }
            try
            {
                HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(Origin + "/");
                rq.Method = "HEAD";
                rq.Timeout = 6000;
                rq.AllowAutoRedirect = false;
                using (WebResponse r = rq.GetResponse()) { return true; }
            }
            catch (WebException ex) { return ex.Response != null; }
            catch (Exception) { return false; }
        }

        public static bool DemoOffline;   // lets the demo school pretend the internet is down

        public bool CheckAuth()
        {
            if (IsDemo) return true;
            try
            {
                int st;
                string text = Fetch("/campus/api/portal/students", out st);
                return st >= 200 && st < 300 && J.Parse(text) != null;
            }
            catch (Exception) { return false; }
        }

        // ---------------------------------------------------------------- sign in
        public bool SignIn(string user, string pass, out string error)
        {
            error = null;
            if (IsDemo)
            {
                if (user == Demo.User && pass == Demo.Pass) return true;
                error = "Campus did not accept that username and password.";
                return false;
            }
            try
            {
                int st;
                try { Request("GET", LoginUrl, null, out st); } catch (WebException) { }
                string body = "username=" + Uri.EscapeDataString(user)
                    + "&password=" + Uri.EscapeDataString(pass)
                    + "&appName=" + Uri.EscapeDataString(AppName)
                    + "&portalLoginPage=" + Portal
                    + "&url=nav-wrapper&lang=en";
                string resp = Request("POST", Origin + "/campus/verify.jsp", body, out st);
                if (CheckAuth()) return true;
                string low = (resp ?? "").ToLowerInvariant();
                if (low.Contains("password-error") || low.Contains("invalid") || low.Contains("incorrect") || low.Contains("locked"))
                    error = "Campus did not accept that username and password.";
                else
                    error = "Sign-in did not complete. Check the link, username and password. If your school uses Google or Microsoft sign-in, use the link below the button.";
                return false;
            }
            catch (WebException)
            {
                error = "Could not reach your school's Campus server. Check the link and your internet connection.";
                return false;
            }
        }

        // For schools that use single sign-on: reuse a session cookie copied from a signed-in browser.
        public void UseCookieHeader(string header)
        {
            string h = (header ?? "").Trim();
            if (h.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase)) h = h.Substring(7);
            foreach (string part in h.Split(';'))
            {
                int i = part.IndexOf('=');
                if (i <= 0) continue;
                string name = part.Substring(0, i).Trim();
                string val = part.Substring(i + 1).Trim();
                if (name.Length == 0) continue;
                try { jar.Add(new Cookie(name, val, "/", Host)); } catch (Exception) { }
            }
        }

        // ---------------------------------------------------------------- data
        class Cat
        {
            public string Key, Pattern;
            public string[] Guesses;
            public bool NeedsRecords; // accept only when real records were read out of the reply
            public Cat(string key, string pattern, bool records, params string[] guesses)
            {
                Key = key; Pattern = pattern; NeedsRecords = records; Guesses = guesses;
            }
        }

        static readonly Cat[] Cats = new Cat[]
        {
            new Cat("students", null, false, "/campus/api/portal/students"),
            new Cat("roster", null, false,
                "/campus/resources/portal/roster?_expand=%7BsectionPlacements-%7Bterm%7D%7D",
                "/campus/resources/portal/roster",
                "/campus/api/portal/roster"),
            new Cat("grades", null, false, "/campus/resources/portal/grades", "/campus/api/portal/grades"),
            new Cat("assignments", null, false, "/campus/api/portal/assignment/listView", "/campus/resources/portal/assignments"),
            new Cat("attendance", "attend|absen", true,
                "/campus/resources/portal/attendance",
                "/campus/api/portal/attendance",
                "/campus/resources/portal/attendanceSummary",
                "/campus/resources/portal/attendance/summary",
                "/campus/resources/portal/student/attendance",
                "/campus/api/portal/students/attendance",
                "/campus/resources/portal/attendance/student",
                "/campus/api/portal/attendance/summary",
                "/campus/resources/portal/student/attendanceSummary"),
            new Cat("messages", "messag|inbox|messenger|mailbox", true,
                "/campus/resources/portal/messages",
                "/campus/api/portal/messages",
                "/campus/api/portal/messenger/inbox",
                "/campus/api/portal/inbox",
                "/campus/resources/portal/messageCenter",
                "/campus/api/messenger/inbox",
                "/campus/resources/messenger/inbox",
                "/campus/api/messenger/v2/inbox",
                "/campus/api/portal/messenger",
                "/campus/resources/portal/messenger/inbox",
                "/campus/api/portal/notifications",
                "/campus/resources/prism/portal/notifications"),
            new Cat("notices", "notice|announce|bulletin", true,
                "/campus/resources/portal/notices",
                "/campus/api/portal/notices",
                "/campus/api/portal/announcements",
                "/campus/resources/portal/announcements",
                "/campus/resources/portal/noticeBoard",
                "/campus/resources/portal/userNotice",
                "/campus/api/portal/userNotice",
                "/campus/api/portal/userNotices",
                "/campus/resources/portal/userNotices"),
            new Cat("documents", "document", true,
                "/campus/resources/portal/documents",
                "/campus/api/portal/documents",
                "/campus/api/portal/student/documents",
                "/campus/resources/portal/student/documents",
                "/campus/resources/portal/studentDocuments",
                "/campus/api/portal/documents/list",
                "/campus/resources/portal/document",
                "/campus/api/portal/student/document",
                "/campus/api/portal/studentDocuments",
                "/campus/resources/documents"),
            new Cat("calendar", null, true, "/campus/resources/calendar/instructionalDay?calendarID={cal}")
        };

        static string WithPerson(string path, string personId, string key)
        {
            if (string.IsNullOrEmpty(personId) || key == "students") return path;
            return path + (path.IndexOf('?') >= 0 ? "&" : "?") + "personID=" + Uri.EscapeDataString(personId);
        }

        // ---------------------------------------------------------------- finding the portal's own addresses
        // The Campus web app is a single-page app: every address it calls is written inside its script files.
        // Reading them (only from your own school's server) tells this app where messages, documents and
        // so on actually live for your district.
        string bellPath;
        bool bellsTried;
        readonly List<DiagRow> bellRows = new List<DiagRow>();
        void AddBellRow(DiagRow r)
        {
            foreach (DiagRow x in bellRows)
                if (x.Path == r.Path && x.Status == r.Status && x.Records == r.Records && x.Ok == r.Ok) return;
            bellRows.Add(r);
        }
        readonly Dictionary<string, object> bellCache = new Dictionary<string, object>();
        List<string> discovered;
        readonly object discLock = new object();
        string discNote = "";
        readonly List<string> discLog = new List<string>();

        public string DiscoverNote { get { return discNote; } }

        public string[] DiscoveredPaths()
        {
            lock (discLock) { return discovered == null ? new string[0] : discovered.ToArray(); }
        }

        public List<string> DiscoverLogLines()
        {
            lock (discLock) { return new List<string>(discLog); }
        }

        static string Pth(string url)
        {
            try { Uri u = new Uri(url); return u.AbsolutePath; } catch (Exception) { return url; }
        }

        static void AddScriptRefs(string text, Uri baseUri, string host, List<string> into)
        {
            foreach (Match m in Regex.Matches(text, @"(?:src|href)\s*=\s*[""']([^""']+?\.m?js(?:\?[^""']*)?)[""']", RegexOptions.IgnoreCase))
            {
                try
                {
                    Uri u = new Uri(baseUri, m.Groups[1].Value);
                    string us = u.ToString();
                    if (string.Equals(u.Host, host, StringComparison.OrdinalIgnoreCase) && !into.Contains(us)) into.Add(us);
                }
                catch (Exception) { }
            }
        }

        List<string> Discovered()
        {
            lock (discLock)
            {
                if (discovered != null) return discovered;
                discovered = new List<string>();
                discovered.AddRange(CapturedPaths());
                try
                {
                    string cache = Disk.At("endpoints-" + Disk.SafeName(Host) + ".txt");
                    if (File.Exists(cache) && (DateTime.Now - File.GetLastWriteTime(cache)).TotalDays < 1)
                    {
                        discovered.AddRange(File.ReadAllLines(cache));
                        if (discovered.Count > 0)
                        {
                            discNote = discovered.Count + " portal addresses (saved from an earlier run)";
                            return discovered;
                        }
                    }
                    string role = Portal == "parents" ? "parent" : "student";
                    List<string> pages = new List<string>();
                    pages.Add("/campus/nav-wrapper/" + role + "/portal/" + role + "/home");
                    pages.Add("/campus/nav-wrapper/");
                    pages.Add(LoginUrl);
                    List<string> scripts = new List<string>();
                    foreach (string page in pages)
                    {
                        try
                        {
                            int st;
                            string html = Fetch(page, out st);
                            Uri pageUri = new Uri(Absolute(page));
                            Uri baseUri = pageUri;
                            Match bm = Regex.Match(html, @"<base\s[^>]*href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                            if (bm.Success) { try { baseUri = new Uri(pageUri, bm.Groups[1].Value); } catch (Exception) { } }
                            int before = scripts.Count;
                            AddScriptRefs(html, baseUri, Host, scripts);
                            discLog.Add("page " + Pth(pageUri.ToString()) + " -> HTTP " + st + ", " + html.Length + " bytes, base=" + Pth(baseUri.ToString())
                                + ", " + (scripts.Count - before) + " script files");
                        }
                        catch (Exception ex) { discLog.Add("page " + Pth(Absolute(page)) + " -> " + ex.Message); }
                    }
                    // lists of every file the portal ships (Angular / webpack / vite builds)
                    string[] manifests = new string[] { "/campus/nav-wrapper/ngsw.json", "/campus/nav-wrapper/asset-manifest.json", "/campus/nav-wrapper/manifest.json" };
                    foreach (string mf in manifests)
                    {
                        try
                        {
                            int st;
                            string txt = Fetch(mf, out st);
                            if (st >= 200 && st < 300 && txt.Length > 0)
                            {
                                Uri mu = new Uri(Origin + "/campus/nav-wrapper/");
                                int before = scripts.Count;
                                foreach (Match m in Regex.Matches(txt, @"""([^""\s]+?\.m?js)"""))
                                {
                                    try
                                    {
                                        Uri u = new Uri(mu, m.Groups[1].Value);
                                        string us = u.ToString();
                                        if (string.Equals(u.Host, Host, StringComparison.OrdinalIgnoreCase) && !scripts.Contains(us)) scripts.Add(us);
                                    }
                                    catch (Exception) { }
                                }
                                discLog.Add("manifest " + mf + " -> HTTP " + st + ", " + (scripts.Count - before) + " script files");
                            }
                            else discLog.Add("manifest " + mf + " -> HTTP " + st);
                        }
                        catch (Exception ex) { discLog.Add("manifest " + mf + " -> " + ex.Message); }
                    }
                    Dictionary<string, string> texts = new Dictionary<string, string>();
                    Dictionary<string, int> codes = new Dictionary<string, int>();
                    FetchMany(scripts, texts, codes, 40);
                    int okFirst = texts.Count;
                    foreach (string su in scripts.GetRange(0, Math.Min(scripts.Count, 40)))
                    {
                        int c;
                        codes.TryGetValue(su, out c);
                        discLog.Add("script " + Pth(su) + " -> HTTP " + c + (texts.ContainsKey(su) ? ", " + texts[su].Length + " chars" : ""));
                    }
                    // second wave: lazy-loaded chunks named (or hash-mapped) inside the first files
                    List<string> chunks = new List<string>();
                    foreach (KeyValuePair<string, string> kv in new List<KeyValuePair<string, string>>(texts))
                    {
                        Uri su = new Uri(kv.Key);
                        Uri dir = new Uri(su, ".");
                        foreach (Match m in Regex.Matches(kv.Value, @"[""']([A-Za-z0-9_\-./]{3,90}\.m?js)[""']"))
                        {
                            try
                            {
                                Uri u = new Uri(dir, m.Groups[1].Value);
                                string us = u.ToString();
                                if (string.Equals(u.Host, Host, StringComparison.OrdinalIgnoreCase) && !texts.ContainsKey(us) && !chunks.Contains(us)) chunks.Add(us);
                            }
                            catch (Exception) { }
                        }
                        foreach (Match m in Regex.Matches(kv.Value, @"[""']?([A-Za-z0-9_\-]{1,60})[""']?\s*:\s*[""']([0-9a-f]{8,24})[""']"))
                        {
                            try
                            {
                                Uri u = new Uri(dir, m.Groups[1].Value + "." + m.Groups[2].Value + ".js");
                                string us = u.ToString();
                                if (!texts.ContainsKey(us) && !chunks.Contains(us)) chunks.Add(us);
                            }
                            catch (Exception) { }
                        }
                        if (chunks.Count > 300) break;
                    }
                    int asked = Math.Min(chunks.Count, 140);
                    FetchMany(chunks, texts, codes, 140);
                    discLog.Add("chunk files: asked for " + asked + ", got " + (texts.Count - okFirst));
                    HashSet<string> found = new HashSet<string>();
                    foreach (KeyValuePair<string, string> kv in texts)
                    {
                        foreach (Match m in Regex.Matches(kv.Value, @"/(?:api|resources)/[A-Za-z0-9_\-./]+"))
                        {
                            string p = m.Value;
                            if (!p.StartsWith("/campus/")) p = "/campus" + p;
                            p = p.TrimEnd('.', '/');
                            if (p.Length > 110 || p.Length < 14) continue;
                            if (Regex.IsMatch(p, @"\.(js|css|png|svg|html|jpg|gif|map)$", RegexOptions.IgnoreCase)) continue;
                            found.Add(p);
                        }
                    }
                    discovered.AddRange(found);
                    discovered.Sort(StringComparer.Ordinal);
                    discNote = discovered.Count + " portal addresses read from " + texts.Count + " script files";
                    if (discovered.Count > 0) File.WriteAllLines(cache, discovered.ToArray());
                }
                catch (Exception ex)
                {
                    discNote = "address discovery failed: " + ex.Message;
                }
                return discovered;
            }
        }

        void FetchMany(List<string> urls, Dictionary<string, string> into, Dictionary<string, int> codes, int max)
        {
            List<Thread> threads = new List<Thread>();
            object gate = new object();
            int next = 0;
            int count = Math.Min(max, urls.Count);
            for (int w = 0; w < 8; w++)
            {
                Thread t = new Thread(delegate()
                {
                    while (true)
                    {
                        int i;
                        lock (gate) { i = next++; }
                        if (i >= count) return;
                        try
                        {
                            int st; string ct;
                            byte[] b = RequestRaw("GET", urls[i], null, out st, out ct);
                            lock (gate) { codes[urls[i]] = st; }
                            if (st >= 200 && st < 300 && b.Length > 0 && b.Length < 8 * 1024 * 1024)
                            {
                                string txt = AsText(b);
                                lock (gate) { into[urls[i]] = txt; }
                            }
                        }
                        catch (Exception) { }
                    }
                });
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
            foreach (Thread t in threads) t.Join();
        }

        // ---------------------------------------------------------------- load everything
        public Snapshot LoadAll(string personId)
        {
            if (IsDemo) return Demo.Build();
            Dictionary<string, object> results = new Dictionary<string, object>();
            List<DiagRow> diag = new List<DiagRow>();
            object sync = new object();
            List<Thread> threads = new List<Thread>();

            foreach (Cat cat in Cats)
            {
                if (cat.Key == "calendar") continue;
                Cat c = cat;
                Thread t = new Thread(delegate()
                {
                    List<string> tried = new List<string>();
                    List<string> order = new List<string>(c.Guesses);
                    bool discoveredAdded = false;
                    for (int i = 0; i < order.Count; i++)
                    {
                        string path = order[i];
                        bool fromGuess = i < c.Guesses.Length;
                        tried.Add(path);
                        DiagRow row = new DiagRow();
                        row.Key = c.Key;
                        row.Path = path.Split('?')[0];
                        object parsed = null;
                        bool accept = false;
                        try
                        {
                            int st;
                            string text = Fetch(WithPerson(path, personId, c.Key), out st);
                            row.Status = st;
                            row.Bytes = text.Length;
                            if (st >= 200 && st < 300) parsed = J.Parse(text);
                            row.Ok = parsed != null;
                            if (parsed != null)
                            {
                                row.Shape = J.Shape(parsed, 0);
                                if (!c.NeedsRecords) accept = true;
                                else
                                {
                                    int n = Norm.CountFor(c.Key, parsed);
                                    row.Records = n;
                                    accept = n > 0 || (fromGuess && Norm.IsEmptyContainer(parsed));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            row.Error = ex.Message;
                        }
                        lock (sync)
                        {
                            diag.Add(row);
                            if (accept) results[c.Key] = parsed;
                        }
                        if (accept) return;
                        if (i == order.Count - 1 && !discoveredAdded && c.Pattern != null)
                        {
                            discoveredAdded = true;
                            List<string> all = Discovered();
                            int added = 0;
                            foreach (string d in all)
                            {
                                if (added >= 8) break;
                                if (!Regex.IsMatch(d, c.Pattern, RegexOptions.IgnoreCase)) continue;
                                if (tried.Contains(d)) continue;
                                order.Add(d);
                                added++;
                            }
                        }
                    }
                });
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
            foreach (Thread t in threads) t.Join();

            if (results.Count == 0 && !CheckAuth()) throw new AuthException();

            // the school calendar (school days, sometimes the A/B day label) needs the student's calendar id
            try
            {
                object sv;
                if (results.TryGetValue("students", out sv))
                {
                    string cal = null;
                    foreach (Student st0 in Norm.Students(sv))
                    {
                        if (st0.CalendarId.Length > 0 && (cal == null || st0.Id == personId)) cal = st0.CalendarId;
                    }
                    if (cal != null)
                    {
                        DiagRow row = new DiagRow();
                        row.Key = "calendar";
                        row.Path = "/campus/resources/calendar/instructionalDay";
                        int st;
                        string text = Fetch("/campus/resources/calendar/instructionalDay?calendarID=" + Uri.EscapeDataString(cal), out st);
                        row.Status = st;
                        row.Bytes = text.Length;
                        object parsed = (st >= 200 && st < 300) ? J.Parse(text) : null;
                        row.Ok = parsed != null;
                        if (parsed != null)
                        {
                            row.Shape = J.Shape(parsed, 0);
                            row.Records = Norm.CountFor("calendar", parsed);
                            results["calendar"] = parsed;
                        }
                        diag.Add(row);
                    }
                }
            }
            catch (Exception ex)
            {
                DiagRow row = new DiagRow();
                row.Key = "calendar";
                row.Path = "/campus/resources/calendar/instructionalDay";
                row.Error = ex.Message;
                diag.Add(row);
            }

            // bell times: Campus does not document where they live, so try the likely places once per sign-in
            try
            {
                string calId = null;
                object sv2;
                if (results.TryGetValue("students", out sv2))
                    foreach (Student st1 in Norm.Students(sv2))
                        if (st1.CalendarId.Length > 0 && (calId == null || st1.Id == personId)) calId = st1.CalendarId;
                string firstSched = null;
                object calParsed;
                if (results.TryGetValue("calendar", out calParsed))
                {
                    List<CalDay> cds = Norm.CalendarDays(calParsed);
                    if (cds.Count > 0) firstSched = cds[0].SchedId;
                }
                if (calId != null && !bellsTried)
                {
                    string[] cands = bellPath != null ? new string[] { bellPath } : new string[]
                    {
                        "/campus/resources/calendar/periodSchedule?calendarID={cal}",
                        "/campus/resources/calendar/period?calendarID={cal}",
                        "/campus/resources/calendar/periods?calendarID={cal}",
                        "/campus/resources/schedule/periodSchedule?calendarID={cal}",
                        "/campus/resources/calendar/bellSchedule?calendarID={cal}",
                        "/campus/api/calendar/periodSchedule?calendarID={cal}",
                        "/campus/resources/course/period?calendarID={cal}",
                        "/campus/resources/calendar/period?periodScheduleID={sched}",
                        "/campus/resources/calendar/periodSchedule/{sched}",
                        "/campus/resources/calendar/periodSchedule?periodScheduleID={sched}",
                        "/campus/resources/schedule/period?periodScheduleID={sched}",
                        "/campus/api/calendar/period?periodScheduleID={sched}"
                    };
                    foreach (string cand in cands)
                    {
                        if (cand.Contains("{sched}") && string.IsNullOrEmpty(firstSched)) continue;
                        string path = cand.Replace("{cal}", Uri.EscapeDataString(calId)).Replace("{sched}", Uri.EscapeDataString(firstSched ?? ""));
                        int bst;
                        string btext = Fetch(path, out bst);
                        DiagRow brow = new DiagRow();
                        brow.Key = "bells";
                        brow.Path = cand.Replace("?calendarID={cal}", "").Replace("{sched}", "<id>");
                        brow.Status = bst;
                        brow.Bytes = btext.Length;
                        object bparsed = (bst >= 200 && bst < 300) ? J.Parse(btext) : null;
                        brow.Ok = bparsed != null;
                        if (bparsed != null)
                        {
                            brow.Shape = J.Shape(bparsed, 0);
                            brow.Records = Norm.BellSchedules(bparsed).Count;
                            if (brow.Records > 0) { results["bells"] = bparsed; bellPath = cand; }
                        }
                        AddBellRow(brow);
                        if (results.ContainsKey("bells")) break;
                    }
                    if (!results.ContainsKey("bells")) bellsTried = true;
                }
                // the working endpoint is per schedule id, so fetch every schedule the calendar uses (cached after the first time)
                if (bellPath != null && bellPath.Contains("{sched}") && calParsed != null)
                {
                    List<object> allBells = new List<object>();
                    List<string> seen = new List<string>();
                    foreach (CalDay cd in Norm.CalendarDays(calParsed))
                    {
                        if (cd.SchedId.Length == 0 || seen.Contains(cd.SchedId)) continue;
                        seen.Add(cd.SchedId);
                        object got;
                        if (!bellCache.TryGetValue(cd.SchedId, out got))
                        {
                            int bst2;
                            string bt2 = Fetch(bellPath.Replace("{sched}", Uri.EscapeDataString(cd.SchedId)), out bst2);
                            got = (bst2 >= 200 && bst2 < 300) ? J.Parse(bt2) : null;
                            if (got != null) bellCache[cd.SchedId] = got;
                        }
                        if (got != null) allBells.Add(got);
                    }
                    if (allBells.Count > 0)
                    {
                        results["bells"] = allBells;
                        DiagRow br2 = new DiagRow();
                        br2.Key = "bells";
                        br2.Path = "period schedules fetched (all ids)";
                        br2.Ok = true;
                        br2.Records = allBells.Count;
                        AddBellRow(br2);
                    }
                }
                foreach (DiagRow br in bellRows) diag.Add(br);
            }
            catch (Exception) { }

            // which parts of the portal the school has switched on (a 404 can simply mean a module is off)
            Dictionary<string, bool> mods = new Dictionary<string, bool>();
            try
            {
                object sv4;
                if (results.TryGetValue("students", out sv4))
                    foreach (Student s4 in Norm.Students(sv4))
                    {
                        if (s4.StructureId.Length == 0 || (s4.Id != personId && mods.Count > 0)) continue;
                        int dst;
                        string dtext = Fetch("/campus/api/portal/displayOptions/" + Uri.EscapeDataString(s4.StructureId) + "?personID=" + Uri.EscapeDataString(s4.Id), out dst);
                        DiagRow drow = new DiagRow();
                        drow.Key = "modules"; drow.Path = "/campus/api/portal/displayOptions/<structure>";
                        drow.Status = dst; drow.Bytes = dtext.Length;
                        object dparsed = (dst >= 200 && dst < 300) ? J.Parse(dtext) : null;
                        drow.Ok = dparsed != null;
                        Dictionary<string, object> dd = dparsed as Dictionary<string, object>;
                        if (dd != null)
                        {
                            foreach (KeyValuePair<string, object> kv in dd)
                                if (kv.Value is bool) mods[kv.Key] = (bool)kv.Value;
                            drow.Records = mods.Count;
                        }
                        diag.Add(drow);
                    }
            }
            catch (Exception) { }

            Snapshot snap = Norm.Build(results);
            snap.Modules = mods;
            diag.Sort(delegate(DiagRow a, DiagRow b) { return string.CompareOrdinal(a.Key, b.Key); });
            snap.Diag = diag;
            snap.DiscoverNote = discNote;
            snap.DiscoverLog = DiscoverLogLines();
            snap.At = DateTime.Now;
            return snap;
        }

        // ---------------------------------------------------------------- pictures and files
        // picture size read from the file header (png, jpeg, gif); false for anything else
        public static bool ImageSize(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b == null || b.Length < 32) return false;
            if (b[0] == 0x89 && b[1] == 0x50)
            {
                w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return w > 0 && h > 0;
            }
            if (b[0] == 0x47 && b[1] == 0x49)
            {
                w = b[6] | (b[7] << 8); h = b[8] | (b[9] << 8);
                return w > 0 && h > 0;
            }
            if (b[0] == 0xFF && b[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    int m = b[i + 1];
                    if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)
                    {
                        h = (b[i + 5] << 8) | b[i + 6];
                        w = (b[i + 7] << 8) | b[i + 8];
                        return w > 0 && h > 0;
                    }
                    int len = (b[i + 2] << 8) | b[i + 3];
                    i += 2 + Math.Max(2, len);
                }
            }
            return false;
        }

        byte[] TryImage(string pathOrUrl)
        {
            try
            {
                int st; string ct;
                byte[] b = RequestRaw("GET", Absolute(pathOrUrl), null, out st, out ct);
                if (st < 200 || st >= 300) return null;
                int w, h;
                return ImageSize(b, out w, out h) ? b : null;
            }
            catch (Exception) { return null; }
        }

        static bool Big(byte[] b, int min)
        {
            int w, h;
            return b != null && ImageSize(b, out w, out h) && w >= min && h >= min;
        }

        public byte[] FetchPhoto(string personId, out string source)
        {
            if (IsDemo) { source = "demo"; return null; }
            source = "";
            if (string.IsNullOrEmpty(personId)) return null;
            string first = "/campus/personPicture.jsp?personID=" + Uri.EscapeDataString(personId);
            byte[] b = TryImage(first);
            if (Big(b, 24)) { source = first.Split('?')[0]; return b; }
            int extra = 0;
            foreach (string d in Discovered())
            {
                if (extra >= 4) break;
                if (!Regex.IsMatch(d, "picture|photo", RegexOptions.IgnoreCase) || d.Contains("{")) continue;
                extra++;
                string cand = d + "?personID=" + Uri.EscapeDataString(personId);
                b = TryImage(cand);
                if (Big(b, 24)) { source = d; return b; }
            }
            return null;
        }

        // the logo the school's own sign-in page shows
        public byte[] FetchLogo(out string source)
        {
            if (IsDemo) { source = "demo"; return null; }
            source = "";
            List<string> cands = new List<string>();
            try
            {
                int st;
                string html = Fetch(LoginUrl, out st);
                Uri baseUri = new Uri(LoginUrl);
                List<string> icons = new List<string>();
                foreach (Match m in Regex.Matches(html, @"<img\b[^>]*>", RegexOptions.IgnoreCase))
                {
                    if (m.Value.IndexOf("logo", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Match sm = Regex.Match(m.Value, @"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (sm.Success) AddResolved(cands, baseUri, sm.Groups[1].Value);
                }
                foreach (Match m in Regex.Matches(html, @"<link\b[^>]*>", RegexOptions.IgnoreCase))
                {
                    if (!Regex.IsMatch(m.Value, @"rel\s*=\s*[""'][^""']*(apple-touch-icon|icon)", RegexOptions.IgnoreCase)) continue;
                    Match hm = Regex.Match(m.Value, @"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (hm.Success) AddResolved(icons, baseUri, hm.Groups[1].Value);
                }
                cands.AddRange(icons);
            }
            catch (Exception) { }
            foreach (string c in cands)
            {
                byte[] b = TryImage(c);
                if (Big(b, 24)) { source = c; return b; }
            }
            return null;
        }

        static void AddResolved(List<string> list, Uri baseUri, string rel)
        {
            try
            {
                if (rel.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
                if (rel.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return; // GDI+ cannot draw SVG
                string u = new Uri(baseUri, rel).ToString();
                if (!list.Contains(u)) list.Add(u);
            }
            catch (Exception) { }
        }

        // saves a document from Campus into a temp folder and returns the file path (or null)
        public string Download(string pathOrUrl, string name)
        {
            if (IsDemo) return null;
            try
            {
                int st; string ct;
                byte[] b = RequestRaw("GET", Absolute(pathOrUrl), null, out st, out ct);
                if (st < 200 || st >= 300 || b.Length == 0) return null;
                string ext = Path.GetExtension(name ?? "");
                if (ext.Length == 0 || ext.Length > 6)
                {
                    string c = (ct ?? "").ToLowerInvariant();
                    ext = c.Contains("pdf") ? ".pdf" : c.Contains("png") ? ".png" : c.Contains("jpeg") ? ".jpg"
                        : c.Contains("wordprocessing") ? ".docx" : c.Contains("msword") ? ".doc" : c.Contains("html") ? ".html"
                        : c.Contains("text/plain") ? ".txt" : c.Contains("spreadsheet") ? ".xlsx" : "";
                }
                string dir = Path.Combine(Path.GetTempPath(), "CampusMidnight");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, Disk.SafeName(Path.GetFileNameWithoutExtension(name ?? "document")) + ext);
                File.WriteAllBytes(file, b);
                return file;
            }
            catch (Exception) { return null; }
        }
    }
}
