using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FramePlay.Core;

namespace FramePlay.Themes;

/// <summary>Applies a theme by replacing the brush resources that every window uses through DynamicResource.</summary>
public static class ThemeManager
{
    public static readonly string[] Tokens =
    [
        "bg-0", "bg-1", "bg-2", "bg-3", "bg-input", "bg-hover", "bg-active", "bg-modal", "border",
        "text", "text-muted", "text-strong", "accent", "accent-text", "seek", "volume", "toggle",
        "link", "danger", "success", "warning", "selection", "radius",
    ];

    /// <summary>FramePlay's own colour roles; a theme without them (every MarkNotes theme) uses its accent.</summary>
    private static readonly string[] AccentRoles = ["seek", "volume", "toggle"];

    public static ThemeDefinition Current { get; private set; } = BuiltInThemes.All[0];
    public static bool IsDark { get; private set; } = true;
    public static Color Bg0 { get; private set; }
    public static Color TextColor { get; private set; }
    public static event Action? Changed;

    public static IEnumerable<ThemeDefinition> AllThemes(AppSettings s) => BuiltInThemes.All.Concat(s.CustomThemes);

    public static ThemeDefinition Find(AppSettings s, string? id) =>
        AllThemes(s).FirstOrDefault(t => t.Id == id) ?? BuiltInThemes.All[0];

    public static Dictionary<string, string> Resolve(ThemeDefinition t)
    {
        var d = new Dictionary<string, string>(t.Base == "light" ? BuiltInThemes.LightBase : BuiltInThemes.DarkBase);
        foreach (var kv in t.Vars)
            if (!string.IsNullOrWhiteSpace(kv.Value)) d[kv.Key] = kv.Value;
        foreach (var role in AccentRoles)
            if (!d.TryGetValue(role, out var value) || string.IsNullOrWhiteSpace(value)) d[role] = d["accent"];
        return d;
    }

    /// <summary>
    /// Accent picked in Settings › Appearance (#rrggbb), or null for the theme's own. It replaces the accent
    /// and the colours that follow it (seek bar, volume, buttons that are on, selection) in every theme.
    /// </summary>
    public static string? AccentOverride { get; set; }

    /// <summary>Ready-made accents offered in Settings, in hue order.</summary>
    public static readonly string[] AccentPresets =
        ["#ff4b4b", "#ffa14b", "#ffd84b", "#9bf200", "#00f2ea", "#4b9dff", "#9b6bff", "#f200e8", "#ff4b8d"];

    /// <summary>The theme's colours with <see cref="AccentOverride"/> on top: what the player shows.</summary>
    public static Dictionary<string, string> ResolveShown(ThemeDefinition t)
    {
        var d = Resolve(t);
        if (!ColorUtil.TryParse(AccentOverride, out var c)) return d;
        c = Color.FromRgb(c.R, c.G, c.B);
        var hex = ColorUtil.ToHex(c);
        foreach (var role in AccentRoles) d[role] = hex;
        d["accent"] = hex;
        // Text and icons on the accent: white where it reads (3:1, like the default blurple), else near-black.
        d["accent-text"] = ColorUtil.Contrast(Colors.White, c) >= 3 ? "#ffffff" : "#141414";
        d["selection"] = ColorUtil.ToHex(ColorUtil.WithAlpha(c, t.Base == "light" ? 0.25 : 0.35));
        return d;
    }

    public static double RadiusOf(Dictionary<string, string> v)
    {
        var s = v.GetValueOrDefault("radius", "8px").Trim().Replace("px", "");
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? Math.Clamp(r, 0, 24) : 8;
    }

    /// <param name="withAccent">False in the theme editor, which shows the theme's own colours.</param>
    public static void Apply(ThemeDefinition t, bool withAccent = true)
    {
        var v = withAccent ? ResolveShown(t) : Resolve(t);
        var fallback = t.Base == "light" ? BuiltInThemes.LightBase : BuiltInThemes.DarkBase;
        Color C(string k) => ColorUtil.Parse(v.GetValueOrDefault(k), ColorUtil.Parse(fallback.GetValueOrDefault(k, fallback["accent"]), Colors.Magenta));
        var res = Application.Current.Resources;

        void Set(string key, Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            res[key + "Brush"] = b;
            res[key + "Color"] = c;
        }

        Color bg0 = C("bg-0"), bg1 = C("bg-1"), bg2 = C("bg-2"), bgInput = C("bg-input"), bgActive = C("bg-active");
        Color text = C("text"), textMuted = C("text-muted"), textStrong = C("text-strong"), accent = C("accent"), accentText = C("accent-text");

        Set("Bg0", bg0);
        Set("Bg1", bg1);
        Set("Bg2", bg2);
        Set("Bg3", C("bg-3"));
        Set("BgInput", bgInput);
        Set("BgHover", C("bg-hover"));
        Set("BgActive", bgActive);
        Set("BgModal", C("bg-modal"));
        Set("Line", C("border"));
        Set("Text", text);
        Set("TextMuted", textMuted);
        Set("TextStrong", textStrong);
        Set("Accent", accent);
        Set("AccentText", accentText);
        Set("Link", C("link"));
        Set("Danger", C("danger"));
        Set("Success", C("success"));
        Set("Warning", C("warning"));
        Set("Selection", C("selection"));
        Color seek = C("seek");
        Set("Seek", seek);
        Set("SeekDim", ColorUtil.Over(ColorUtil.WithAlpha(seek, 0.38), bgInput));
        Set("Volume", C("volume"));
        Set("Toggle", C("toggle"));

        // Derived shades (the same recipes MarkNotes uses in app.css).
        Set("HoverTint", ColorUtil.WithAlpha(text, 0.07));
        Set("PressTint", ColorUtil.WithAlpha(text, 0.12));
        Set("AccentHover", ColorUtil.Mix(Colors.Black, accent, 0.12));
        Set("AccentSoft", ColorUtil.WithAlpha(accent, 0.22));
        Set("AccentDim", ColorUtil.Over(ColorUtil.WithAlpha(accent, 0.38), bgInput));
        Set("MenuLine", ColorUtil.WithAlpha(C("border"), 0.6));
        Set("ScrollThumb", ColorUtil.WithAlpha(textMuted, 0.35));
        Set("ScrollThumbHover", ColorUtil.WithAlpha(textMuted, 0.55));
        Set("SwitchOff", Color.FromRgb(0x80, 0x84, 0x8e));
        Set("Overlay", ColorUtil.WithAlpha(bg1, 0.94));
        Set("OverlayStrong", ColorUtil.WithAlpha(bg0, 0.96));
        Set("Scrim", Color.FromArgb(0x99, 0, 0, 0));
        Set("CloseHover", Color.FromRgb(0xC4, 0x2B, 0x1C));
        // Text on the selected row: whichever of text-strong / accent-text reads better on bg-active.
        Set("SelectedText", ColorUtil.Contrast(textStrong, bgActive) >= ColorUtil.Contrast(accentText, bgActive) ? textStrong : accentText);

        double r = RadiusOf(v);
        res["Radius"] = r;
        res["CornerRadius"] = new CornerRadius(r);
        res["CornerRadiusSmall"] = new CornerRadius(Math.Max(0, r - 3));
        res["CornerRadiusLarge"] = new CornerRadius(r == 0 ? 0 : r + 4);
        res["ShadowOpacity"] = t.Base == "light" ? 0.18 : 0.45;

        Current = t;
        IsDark = t.Base != "light";
        Bg0 = bg0;
        TextColor = text;
        Changed?.Invoke();
    }
}
