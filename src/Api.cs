using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
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
        public readonly string Origin;
        public readonly string Host;
        public readonly string LoginUrl;
        public readonly string AppName;
        public readonly string Portal;

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
        string Request(string method, string url, string body, out int status)
        {
            Uri target = new Uri(url);
            if (!string.Equals(target.Host, Host, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Blocked request to another site.");
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(target);
            rq.Method = method;
            rq.CookieContainer = jar;
            rq.UserAgent = UA;
            rq.Accept = "application/json, text/plain, text/html, */*";
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
                bool sameHost = string.Equals(rs.ResponseUri.Host, Host, StringComparison.OrdinalIgnoreCase);
                if (!sameHost) { status = 401; return ""; } // bounced to another site (e.g. a sign-in page)
                using (Stream st = rs.GetResponseStream())
                using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public string Fetch(string path, out int status)
        {
            return Request("GET", Origin + path, null, out status);
        }

        public bool CheckAuth()
        {
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
        static readonly KeyValuePair<string, string[]>[] Endpoints = new KeyValuePair<string, string[]>[]
        {
            new KeyValuePair<string, string[]>("students", new string[] { "/campus/api/portal/students" }),
            new KeyValuePair<string, string[]>("roster", new string[] {
                "/campus/resources/portal/roster?_expand=%7BsectionPlacements-%7Bterm%7D%7D",
                "/campus/resources/portal/roster",
                "/campus/api/portal/roster" }),
            new KeyValuePair<string, string[]>("grades", new string[] { "/campus/resources/portal/grades", "/campus/api/portal/grades" }),
            new KeyValuePair<string, string[]>("assignments", new string[] { "/campus/api/portal/assignment/listView", "/campus/resources/portal/assignments" }),
            new KeyValuePair<string, string[]>("attendance", new string[] {
                "/campus/resources/portal/attendance",
                "/campus/api/portal/attendance",
                "/campus/resources/portal/attendanceSummary" })
        };

        static string WithPerson(string path, string personId, string key)
        {
            if (string.IsNullOrEmpty(personId) || key == "students") return path;
            return path + (path.IndexOf('?') >= 0 ? "&" : "?") + "personID=" + Uri.EscapeDataString(personId);
        }

        public Snapshot LoadAll(string personId)
        {
            Dictionary<string, object> results = new Dictionary<string, object>();
            List<DiagRow> diag = new List<DiagRow>();
            object sync = new object();
            List<Thread> threads = new List<Thread>();

            foreach (KeyValuePair<string, string[]> ep in Endpoints)
            {
                string key = ep.Key;
                string[] paths = ep.Value;
                Thread t = new Thread(delegate()
                {
                    for (int i = 0; i < paths.Length; i++)
                    {
                        DiagRow row = new DiagRow();
                        row.Key = key;
                        row.Path = paths[i].Split('?')[0];
                        object parsed = null;
                        try
                        {
                            int st;
                            string text = Fetch(WithPerson(paths[i], personId, key), out st);
                            row.Status = st;
                            row.Bytes = text.Length;
                            if (st >= 200 && st < 300) parsed = J.Parse(text);
                            row.Ok = parsed != null;
                            if (parsed != null) row.Shape = J.Shape(parsed, 0);
                        }
                        catch (Exception ex)
                        {
                            row.Error = ex.Message;
                        }
                        lock (sync)
                        {
                            diag.Add(row);
                            if (parsed != null) results[key] = parsed;
                        }
                        if (parsed != null) return;
                    }
                });
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
            foreach (Thread t in threads) t.Join();

            if (results.Count == 0 && !CheckAuth()) throw new AuthException();

            Snapshot snap = Norm.Build(results);
            diag.Sort(delegate(DiagRow a, DiagRow b) { return string.CompareOrdinal(a.Key, b.Key); });
            snap.Diag = diag;
            snap.At = DateTime.Now;
            return snap;
        }
    }
}
