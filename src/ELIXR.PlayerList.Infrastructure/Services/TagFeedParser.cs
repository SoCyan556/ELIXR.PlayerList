using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ELIXR.PlayerList.Core.Models;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Remote rich text is parsed as data, never forwarded as HTML or executable CSS.
public static class TagFeedParser
{
    private static readonly Regex Markup = new("<[^>]*>|\\[/?(?:b|i|u|s|size|color|font)[^\\]]*\\]", RegexOptions.IgnoreCase);
    private static readonly Regex Prefix = new(@"^\.(SCROLL|CYLN|LBL|RAIN|SR|PULSE|JUMP|SHAKE|GT|BLINK|GLITCH)\.", RegexOptions.IgnoreCase);
    private static readonly Regex ColorToken = new("<color\\s*=\\s*[\"']?([^>\"']+)[\"']?\\s*>|</color\\s*>|\\[color\\s*=\\s*[\"']?([^\\]\"']+)[\"']?\\s*\\]|\\[/color\\s*\\]", RegexOptions.IgnoreCase);
    public static JsonElement Property(JsonElement item, params string[] names)
    { foreach (var name in names) if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)) return value; return default; }
    public static string Text(JsonElement item, params string[] names) => Property(item, names) is var p && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
    private static bool Flag(JsonElement item, params string[] names) => Property(item, names) is var p && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n) && n != 0 || p.ValueKind == JsonValueKind.String && new[] { "1", "true", "yes", "y", "on" }.Contains(p.GetString()?.Trim().ToLowerInvariant()));
    private static double Number(JsonElement item, string name, double fallback) => Property(item, name) is var p && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n) && double.IsFinite(n) ? n : fallback;
    public static bool ValidId(string id, string prefix) => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && id.Length == prefix.Length + 36 && Guid.TryParseExact(id[prefix.Length..], "D", out _);
    public static string? Color(string? value)
    {
        value = value?.Trim();
        if (value is null) return null;
        if (Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")) return value;
        var named = System.Drawing.Color.FromName(value.Equals("grey", StringComparison.OrdinalIgnoreCase) ? "gray" : value);
        return named.IsKnownColor && !named.IsSystemColor ? $"#{named.R:X2}{named.G:X2}{named.B:X2}" : null;
    }
    public static string Animation(string value) => value.Trim().ToLowerInvariant().Replace("_", "-") switch
    {
        "rain" or "rainbow" or "sr" or "smooth-rainbow" => "rain",
        "glitch" or "glitch-lite" => "glitch", "shake" or "shake-lite" => "shake",
        "blink" => "blink", "pulse" => "pulse", _ => ""
    };
    private static string Clean(string value) => Regex.Replace(Markup.Replace(WebUtility.HtmlDecode(value), ""), @"\s+", " ").Trim();
    private static string Canonical(string value) => value.ToLowerInvariant() switch
    { "mal" => "Malicious", "e-dater" => "EDater", "known ripper/reuploader" => "Ripper/Reuploader", "avatar/world creator" or "world/avatar creator" => "World & Avi Creator", _ => value };
    private static TagCategory Category(string category, string name)
    {
        if (category.Equals("group", StringComparison.OrdinalIgnoreCase)) return TagCategory.Group;
        var normalized = Regex.Replace(name.ToLowerInvariant(), @"[^a-z]", "");
        return category.ToLowerInvariant() is "malicious" or "client_user" or "modder" or "suspicious" ||
            normalized is "malicious" or "knowncrasher" or "clientuser" or "clientmod" or "modder" or "avatarripper" or "ripperreuploader" or "pedo" or "possiblepedo" or "suspicious"
            ? TagCategory.Malicious : TagCategory.Custom;
    }
    private static int FewPriority(string name, bool big) => name.ToLowerInvariant() switch
    { "malicious" => 700, "known crasher" => 600, "ripper/reuploader" => 500, "pedo" => 400, "possible pedo" => 390, "modder" => 300, _ => big ? 200 : 100 };
    private static PlayerTag? ParseTag(JsonElement item, string source, bool big = false, bool enlarged = false)
    {
        var raw = item.ValueKind == JsonValueKind.String ? item.GetString()! : Text(item, "text", "tag_text", "Tag", "PlateBigText");
        if (raw.Length > 8192) raw = raw[..8192];
        raw = WebUtility.HtmlDecode(raw).Trim();
        var animation = "";
        var first = true;
        while (Prefix.Match(raw) is { Success: true } prefix)
        { if (first) animation = Animation(prefix.Groups[1].Value); first = false; raw = raw[prefix.Length..].TrimStart(); }
        var name = Canonical(Clean(raw));
        if (name.Length == 0) return null;
        if (name.Length > 240) name = name[..240];
        var color = Color(Text(item, "color", "tag_color", "Color")) ?? Color(Regex.Match(raw, @"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b").Value);
        var matches = ColorToken.Matches(raw);
        var runs = new List<TagColorRun>();
        var stack = new Stack<string?>();
        string? current = color;
        var offset = 0;
        foreach (Match match in matches)
        {
            // Preserve spaces between separately colored runs, unlike the normalized label.
            var untrimmed = Markup.Replace(WebUtility.HtmlDecode(raw[offset..match.Index]), "");
            if (untrimmed.Length > 0) runs.Add(new(untrimmed, current));
            if (match.Groups[1].Success || match.Groups[2].Success) { stack.Push(current); current = Color(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value) ?? current; color ??= current; }
            else current = stack.Count > 0 ? stack.Pop() : color;
            offset = match.Index + match.Length;
        }
        if (matches.Count > 0 && offset < raw.Length) runs.Add(new(Markup.Replace(WebUtility.HtmlDecode(raw[offset..]), ""), current));
        // Canonical renames must not be undone by rich-text segments.
        if (Clean(raw) != name) runs.Clear();
        var gradient = Property(item, "gradient");
        var colors = gradient.ValueKind == JsonValueKind.Array ? gradient.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => Color(x.GetString())).OfType<string>().Take(16).ToArray() : [];
        var explicitAnimation = Text(item, "animation");
        if (explicitAnimation.Length > 0) animation = Animation(explicitAnimation);
        if (big || enlarged || Flag(item, "fallback_static")) animation = "";
        var priority = source == "FewTags" ? FewPriority(name, big) : (int)Math.Clamp(Number(item, "priority", 100), -10000, 10000);
        return new(name, Category(Text(item, "category"), name), color ?? "#AA66FF", colors, animation,
            Math.Clamp(Number(item, "speed", 1), .05, 5), Math.Clamp(Number(item, "amplitude", 1), .1, 6),
            Flag(item, "rainbow"), runs.Count > 0 ? runs : null, source, priority);
    }
    public static IReadOnlyDictionary<string, IReadOnlyList<PlayerTag>> Parse(string json, string source)
    {
        using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'), new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        var records = root.ValueKind == JsonValueKind.Array ? root : Property(root, "records");
        if (records.ValueKind != JsonValueKind.Array) throw new JsonException("The tag feed does not contain records.");
        var result = new Dictionary<string, IReadOnlyList<PlayerTag>>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.EnumerateArray())
        {
            if (!Flag(record, "active", "Active")) continue;
            var group = source == "Groups";
            var id = Text(record, group ? "groupId" : "userId", "UserID");
            if (!ValidId(id, group ? "grp_" : "usr_")) continue;
            var tags = new List<PlayerTag>();
            void Add(JsonElement value, bool big = false, bool enlarged = false)
            {
                if (value.ValueKind == JsonValueKind.Array) { foreach (var element in value.EnumerateArray()) Add(element, big, enlarged); }
                else if (value.ValueKind is JsonValueKind.Object or JsonValueKind.String && ParseTag(value, source, big, enlarged) is { } tag)
                    tags.Add(group ? tag with { Category = TagCategory.Group, GroupId = id, GroupName = Text(record, "groupName").Trim() } : tag);
            }
            if (source == "FewTags")
            {
                if (Flag(record, "Malicious")) tags.Add(new("Malicious", TagCategory.Malicious, "#FF4D4F", Source: source, Priority: 700));
                if (Flag(record, "TextActive") || Flag(record, "BigTextActive")) Add(Property(record, "Tag"), enlarged: Flag(record, "BigTextActive"));
                if (Flag(record, "BigTextActive")) Add(Property(record, "PlateBigText"), true);
            }
            else Add(Property(record, "tags", "Tags"));
            // The original lookup replaces an earlier active record for the same ID.
            var merged = tags.OrderByDescending(t => t.Priority).DistinctBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
            result[id] = (source == "FewTags" ? merged.Take(6) : merged.Take(64)).ToArray();
        }
        return result;
    }
}
