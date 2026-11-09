namespace FramePlay.Themes;

/// <summary>
/// The themes from MarkNotes (src/web/src/themes.js, version 2.5.1), with every token resolved, plus a
/// "VLC Redesign" theme with the colours of Juxtopposed's design.
/// </summary>
public static class BuiltInThemes
{
    private static Dictionary<string, string> V(string bg0, string bg1, string bg2, string bg3, string bgInput, string bgHover,
        string bgActive, string bgModal, string border, string text, string textMuted, string textStrong, string accent,
        string accentText, string link, string danger, string success, string warning, string selection, string radius) => new()
    {
        ["bg-0"] = bg0, ["bg-1"] = bg1, ["bg-2"] = bg2, ["bg-3"] = bg3, ["bg-input"] = bgInput, ["bg-hover"] = bgHover,
        ["bg-active"] = bgActive, ["bg-modal"] = bgModal, ["border"] = border, ["text"] = text, ["text-muted"] = textMuted,
        ["text-strong"] = textStrong, ["accent"] = accent, ["accent-text"] = accentText, ["link"] = link, ["danger"] = danger,
        ["success"] = success, ["warning"] = warning, ["selection"] = selection, ["radius"] = radius,
    };

    public static readonly Dictionary<string, string> DarkBase =
        V("#1e1f22", "#2b2d31", "#313338", "#2b2d31", "#1e1f22", "#393c41", "#404249", "#313338", "#3f4147", "#dbdee1", "#949ba4", "#f2f3f5",
          "#5865f2", "#ffffff", "#00a8fc", "#f23f43", "#23a55a", "#f0b232", "rgba(88,101,242,0.35)", "8px");

    public static readonly Dictionary<string, string> LightBase =
        V("#e3e5e8", "#f2f3f5", "#ffffff", "#f2f3f5", "#e3e5e8", "#e8e9ec", "#d7d9dd", "#ffffff", "#d4d6db", "#313338", "#5c5e66", "#060607",
          "#5865f2", "#ffffff", "#006ce7", "#da373c", "#248046", "#b8860b", "rgba(88,101,242,0.25)", "8px");

    private static ThemeDefinition T(string id, string name, string @base, Dictionary<string, string> vars, string author = "MarkNotes") =>
        new() { Id = id, Name = name, Base = @base, Vars = vars, Author = author };

    public static readonly IReadOnlyList<ThemeDefinition> All = new List<ThemeDefinition>
    {
        T("dark", "Dark", "dark", new Dictionary<string, string>(DarkBase)),
        T("midnight", "Midnight (AMOLED)", "dark",
            V("#000000", "#0a0a0c", "#0e0f11", "#141518", "#141518", "#1b1c20", "#24262b", "#111214", "#202227", "#dbdee1", "#949ba4", "#f2f3f5",
              "#5865f2", "#ffffff", "#00a8fc", "#f23f43", "#23a55a", "#f0b232", "rgba(88,101,242,0.35)", "8px")),
        T("light", "Light", "light", new Dictionary<string, string>(LightBase)),
        T("neon", "Neon Arcade", "dark",
            V("#06060c", "#0c0c18", "#10101f", "#141428", "#0a0a14", "#1a1a33", "#24244a", "#11112a", "#2a2a55", "#e4e4ff", "#8b8bc7", "#ffffff",
              "#00e5ff", "#001018", "#ff3ec9", "#f23f43", "#23a55a", "#f0b232", "rgba(0,229,255,0.25)", "8px")),
        T("yorha", "YoRHa", "light",
            V("#b9b29a", "#cdc6ad", "#dad4bb", "#d2cbb1", "#cbc4a9", "#c4bd9f", "#4e4b42", "#d6d0b6", "#a8a18a", "#4e4b42", "#7d7765", "#3a3830",
              "#4e4b42", "#dad4bb", "#8a5a2b", "#b33a2c", "#55693a", "#9a6b1d", "rgba(78,75,66,0.25)", "0px")),
        T("eva01", "EVA-01", "dark",
            V("#130a1c", "#1d0f2b", "#241336", "#2b1840", "#160b22", "#33204b", "#43295f", "#22122f", "#4a2f66", "#e7dcff", "#a58fc7", "#ffffff",
              "#7dff4f", "#102006", "#ff9a3c", "#ff4d5e", "#23a55a", "#ff9a3c", "rgba(125,255,79,0.22)", "3px")),
        T("ps2", "PS2 System", "dark",
            V("#000004", "#030719", "#050b24", "#08102e", "#02051a", "#0c1a45", "#12286a", "#061032", "#16306e", "#c9d8ff", "#6f86c4", "#ffffff",
              "#4f9dff", "#000814", "#8fc2ff", "#f23f43", "#23a55a", "#f0b232", "rgba(79,157,255,0.3)", "8px")),
        T("ps1", "PS1 Memory Card", "dark",
            V("#1a1a1a", "#2a2a2e", "#34343a", "#3c3c44", "#222226", "#45454f", "#55556a", "#2e2e34", "#5a5a66", "#e6e6e6", "#a0a0aa", "#ffffff",
              "#7CB2E8", "#0e1a28", "#7CB2E8", "#FF6666", "#40E2A0", "#FF6666", "rgba(124,178,232,0.32)", "2px")),
        T("dracula", "Dracula", "dark",
            V("#191a21", "#21222c", "#282a36", "#2d2f3d", "#1e1f29", "#343746", "#44475a", "#282a36", "#44475a", "#f8f8f2", "#9ea8d0", "#ffffff",
              "#bd93f9", "#1b1c24", "#8be9fd", "#ff5555", "#50fa7b", "#ffb86c", "rgba(189,147,249,0.3)", "8px")),
        T("vlc-redesign", "VLC Redesign", "dark",
            V("#0e0e0e", "#191919", "#1b1b1b", "#222222", "#000000", "#262626", "#303030", "#1b1b1b", "#2a2a2a", "#cfcfcf", "#8f8f8f", "#eaeaea",
              "#d37918", "#161616", "#fc9729", "#c42b1c", "#3ba55c", "#fc9729", "rgba(211,121,24,0.3)", "6px"), "FramePlay"),
    };
}
