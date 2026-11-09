using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FramePlay.Themes;

/// <summary>
/// .mntheme import/export, the same JSON MarkNotes writes, so themes move between the two apps:
/// { "format": "marknotes-theme", "version": 1, "name", "author", "base", "vars", "css" }.
/// </summary>
public static partial class ThemeFiles
{
    private const string Format = "marknotes-theme";

    [GeneratedRegex(@"^[^;{}<>\\]*$")]
    private static partial Regex SafeValue();

    public static string NewId() => "custom-" + Guid.NewGuid().ToString("N")[..7];

    public static void Export(ThemeDefinition theme, string path)
    {
        var vars = new JsonObject();
        foreach (var kv in ThemeManager.Resolve(theme)) vars[kv.Key] = kv.Value;
        foreach (var kv in theme.Vars) vars[kv.Key] = kv.Value; // keep MarkNotes-only tokens (code colours) too
        var doc = new JsonObject
        {
            ["format"] = Format,
            ["version"] = 1,
            ["name"] = theme.Name,
            ["author"] = theme.Author,
            ["base"] = theme.Base,
            ["vars"] = vars,
            ["css"] = theme.Css ?? "",
        };
        File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Reads a theme file the way MarkNotes does: known shape only, safe values, a fresh id.</summary>
    public static ThemeDefinition? Import(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length > 8 * 1024 * 1024) return null;
        JsonNode? root;
        try { root = JsonNode.Parse(File.ReadAllText(path)); }
        catch { return null; }
        if (root is not JsonObject o) return null;
        var format = o["format"]?.GetValue<string>();
        if (format != null && format != Format) return null;
        if (o["vars"] is not JsonObject vars) return null;

        var t = new ThemeDefinition
        {
            Id = NewId(),
            Name = Trim(o["name"]?.GetValue<string>(), 80) ?? Path.GetFileNameWithoutExtension(path),
            Author = Trim(o["author"]?.GetValue<string>(), 80) ?? "",
            Base = o["base"]?.GetValue<string>() == "light" ? "light" : "dark",
            Css = Trim(o["css"]?.GetValue<string>(), 60000) ?? "",
            Custom = true,
        };
        foreach (var kv in vars)
        {
            if (kv.Value is not JsonValue v || !v.TryGetValue<string>(out var s)) continue;
            if (s.Length > 4000 || !SafeValue().IsMatch(s)) continue;
            if (s.Contains("url(", StringComparison.OrdinalIgnoreCase) && !s.Contains("url(data:", StringComparison.OrdinalIgnoreCase)) continue;
            t.Vars[kv.Key] = s;
        }
        return t;
    }

    private static string? Trim(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
