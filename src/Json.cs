using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CampusMidnight
{
    // Small helpers over the framework's built-in JSON parser (objects come back as
    // Dictionary<string, object>, arrays as object[] / ArrayList).
    static class J
    {
        static readonly JavaScriptSerializer Ser = MakeSer();

        static JavaScriptSerializer MakeSer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 400;
            return s;
        }

        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.TrimStart();
            if (t.Length == 0 || (t[0] != '{' && t[0] != '[')) return null;
            try { return Ser.DeserializeObject(text); }
            catch (Exception) { return null; }
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
