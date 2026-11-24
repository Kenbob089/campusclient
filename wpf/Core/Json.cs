using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace CampusMidnight
{
    // Small helpers over the framework's built-in JSON parser (objects come back as
    // Dictionary<string, object>, arrays as object[] / ArrayList).
    static class J
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.TrimStart();
            if (t.Length == 0 || (t[0] != '{' && t[0] != '[')) return null;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 400 }))
                    return Conv(doc.RootElement);
            }
            catch (Exception) { return null; }
        }

        // objects become Dictionary<string, object>, arrays List<object>, numbers long or double
        static object Conv(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    foreach (JsonProperty p in e.EnumerateObject()) d[p.Name] = Conv(p.Value);
                    return d;
                case JsonValueKind.Array:
                    List<object> l = new List<object>();
                    foreach (JsonElement x in e.EnumerateArray()) l.Add(Conv(x));
                    return l;
                case JsonValueKind.String: return e.GetString();
                case JsonValueKind.Number:
                    long lv;
                    if (e.TryGetInt64(out lv)) return lv;
                    return e.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }

        public static Dictionary<string, object> D(object x) { return x as Dictionary<string, object>; }
        public static IList L(object x) { return x as IList; }
        public static bool Container(object v) { return v is IList || v is Dictionary<string, object>; }

        // first non-empty value among the given keys
        public static object Get(Dictionary<string, object> d, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                object v;
                if (d.TryGetValue(keys[i], out v) && v != null)
                {
                    string s = v as string;
                    if (s != null && s.Length == 0) continue;
                    return v;
                }
            }
            return null;
        }

        public static string Str(Dictionary<string, object> d, params string[] keys)
        {
            object v = Get(d, keys);
            if (v == null || Container(v)) return null;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double? Num(object v)
        {
            if (v == null || Container(v)) return null;
            string s = v as string;
            if (s != null)
            {
                s = Regex.Replace(s, @"[^0-9.\-]", "");
                double r;
                if (s.Length > 0 && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
                return null;
            }
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return null; }
        }

        // a number only if the whole text is a number ("8.0", "85%"); "2.5/4" or "B+" give null
        public static double? StrictNum(object v)
        {
            if (v == null || Container(v)) return null;
            if (v is string)
            {
                string s = ((string)v).Trim();
                if (!Regex.IsMatch(s, @"^-?\d+(\.\d+)?\s*%?$")) return null;
                double r;
                if (double.TryParse(s.TrimEnd('%', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
                return null;
            }
            if (v is bool) return null;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return null; }
        }

        // first key whose value is a clean number
        public static double? NumFirst(Dictionary<string, object> d, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                object v;
                if (d.TryGetValue(keys[i], out v))
                {
                    double? n = StrictNum(v);
                    if (n.HasValue) return n;
                }
            }
            return null;
        }

        public static bool Has(Dictionary<string, object> d, params string[] keys)
        {
            return Get(d, keys) != null;
        }

        // plain text from an HTML snippet
        public static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string s = Regex.Replace(html, @"<\s*(br|/p|/div|/li|/tr|/h\d)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<\s*li\b[^>]*>", "\u2022 ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(script|style)\b.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = System.Net.WebUtility.HtmlDecode(s);
            s = s.Replace("\r", "");
            s = Regex.Replace(s, @"[ \t\u00a0]+", " ");
            s = Regex.Replace(s, @"\n[ ]+", "\n");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        public static bool Bool(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            string s = v as string;
            if (s != null)
            {
                s = s.Trim().ToLowerInvariant();
                return s == "true" || s == "1" || s == "y" || s == "yes";
            }
            double? n = Num(v);
            return n.HasValue && n.Value == 1;
        }

        public static DateTime? Date(object v)
        {
            if (v == null) return null;
            if (v is DateTime) return ((DateTime)v).Date;
            if (!(v is string) && !(v is bool) && !Container(v))
            {
                double? n = StrictNum(v);
                if (n.HasValue && n.Value > 1000000000)
                {
                    double secs = n.Value > 100000000000.0 ? n.Value / 1000.0 : n.Value;
                    try { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secs).ToLocalTime().Date; }
                    catch (Exception) { return null; }
                }
                return null;
            }
            string s = v as string;
            if (s == null) return null;
            s = s.Trim();
            if (s.Length == 0) return null;
            DateTime d;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out d)) return d.Date;
            return null;
        }

        // A privacy-safe description of a document: key names and value types only, never values.
        public static string Shape(object v, int depth)
        {
            StringBuilder sb = new StringBuilder();
            ShapeInto(sb, v, depth);
            return sb.ToString();
        }

        static void ShapeInto(StringBuilder sb, object v, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            IList l = v as IList;
            if (l != null)
            {
                if (l.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[").Append(l.Count).Append(" x ");
                if (depth >= 5) sb.Append("..."); else ShapeInto(sb, l[0], depth + 1);
                sb.Append("]");
                return;
            }
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d != null)
            {
                if (depth >= 5) { sb.Append("{...}"); return; }
                sb.Append("{");
                int n = 0;
                foreach (KeyValuePair<string, object> kv in d)
                {
                    if (n++ >= 40) { sb.Append(",..."); break; }
                    if (n > 1) sb.Append(", ");
                    sb.Append(kv.Key).Append(": ");
                    ShapeInto(sb, kv.Value, depth + 1);
                }
                sb.Append("}");
                return;
            }
            if (v is string) sb.Append("string");
            else if (v is bool) sb.Append("bool");
            else sb.Append("number");
        }
    }
}
