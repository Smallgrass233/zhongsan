using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

/// <summary>JSON 读写的低层辅助。所有写操作都保持原有成员顺序，绝不新增/删除字段。</summary>
internal static class J
{
    public static readonly JsonSerializerOptions WriteOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static readonly char[] Sep = { ' ', '\n', '\r', '\t' };

    public static JsonObject Obj(JsonNode n) => n as JsonObject;

    /// <summary>取 root[key].GameObjects 数组。</summary>
    public static JsonArray GameObjects(JsonObject root, string key)
        => (root?[key] as JsonObject)?["GameObjects"] as JsonArray;

    public static JsonArray ArrayOf(JsonObject root, string key) => root?[key] as JsonArray;

    public static string Str(JsonObject o, string k)
    {
        if (o == null || !o.TryGetPropertyValue(k, out var v) || v is null) return "";
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) return s;
        return v.ToJsonString().Trim('"');
    }

    public static int Int(JsonObject o, string k, int d = 0)
    {
        if (o == null || !o.TryGetPropertyValue(k, out var v) || v is null) return d;
        if (v is JsonValue jv)
        {
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<long>(out var l)) return (int)l;
            if (jv.TryGetValue<double>(out var dd)) return (int)Math.Round(dd);
            if (jv.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) return p;
        }
        return d;
    }

    public static bool Bool(JsonObject o, string k, bool d = false)
    {
        if (o == null || !o.TryGetPropertyValue(k, out var v) || v is null) return d;
        if (v is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b)) return b;
            if (jv.TryGetValue<string>(out var s) && bool.TryParse(s, out var p)) return p;
            if (jv.TryGetValue<int>(out var i)) return i != 0;
        }
        return d;
    }

    public static bool Has(JsonObject o, string k) => o != null && o.ContainsKey(k);

    /// <summary>只在字段已存在时改写其值（保持顺序，不新增字段）。</summary>
    public static bool SetIfExists(JsonObject o, string k, JsonNode v)
    {
        if (o == null || !o.ContainsKey(k)) return false;
        o[k] = v;
        return true;
    }

    public static bool SetIfExists(JsonObject o, string k, string v) => SetIfExists(o, k, JsonValue.Create(v));

    public static bool SetIfExists(JsonObject o, string k, int v) => SetIfExists(o, k, JsonValue.Create(v));

    public static bool SetIfExists(JsonObject o, string k, bool v) => SetIfExists(o, k, JsonValue.Create(v));

    public static List<string> Tokens(string s)
        => string.IsNullOrEmpty(s) ? new List<string>() : s.Split(Sep, StringSplitOptions.RemoveEmptyEntries).ToList();

    public static List<int> Ids(string s)
    {
        var list = new List<int>();
        foreach (var t in Tokens(s))
            if (int.TryParse(t, out var id)) list.Add(id);
        return list;
    }

    /// <summary>从空格分隔串里剔除指定 ID，返回 (新串, 是否变化)。</summary>
    public static bool RemoveIds(string s, HashSet<int> remove, out string result)
    {
        var toks = Tokens(s);
        var kept = new List<string>(toks.Count);
        bool changed = false;
        foreach (var t in toks)
        {
            if (int.TryParse(t, out var id) && remove.Contains(id)) { changed = true; continue; }
            kept.Add(t);
        }
        result = changed ? string.Join(" ", kept) : s;
        return changed;
    }
}
