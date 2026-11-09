using System.Windows.Input;

namespace FramePlay.Input;

/// <summary>A key plus modifiers. Stored as text like "Ctrl+Shift+E" or "Shift+Period" (punctuation is spelled out).</summary>
public readonly record struct Gesture(ModifierKeys Modifiers, Key Key)
{
    private static readonly Dictionary<Key, string> Names = new()
    {
        [Key.OemPeriod] = "Period", [Key.OemComma] = "Comma", [Key.OemPlus] = "Plus", [Key.OemMinus] = "Minus",
        [Key.Return] = "Enter", [Key.Escape] = "Esc", [Key.Back] = "Backspace", [Key.Next] = "PageDown", [Key.Prior] = "PageUp",
        [Key.OemQuestion] = "Slash", [Key.OemSemicolon] = "Semicolon", [Key.OemQuotes] = "Quote",
        [Key.OemOpenBrackets] = "OpenBracket", [Key.Oem6] = "CloseBracket", [Key.Oem5] = "Backslash",
    };

    private static readonly Dictionary<Key, string> Pretty = new()
    {
        [Key.OemPeriod] = ".", [Key.OemComma] = ",", [Key.OemPlus] = "+", [Key.OemMinus] = "-", [Key.OemQuestion] = "/",
        [Key.OemSemicolon] = ";", [Key.OemQuotes] = "'", [Key.OemOpenBrackets] = "[", [Key.Oem6] = "]", [Key.Oem5] = "\\",
        [Key.Left] = "←", [Key.Right] = "→", [Key.Up] = "↑", [Key.Down] = "↓",
    };

    private static string Name(Key k) =>
        Names.TryGetValue(k, out var n) ? n : k is >= Key.D0 and <= Key.D9 ? ((int)(k - Key.D0)).ToString() : k.ToString();

    private string Join(Func<Key, string> keyText)
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(keyText(Key));
        return string.Join("+", parts);
    }

    /// <summary>Text for settings.json.</summary>
    public override string ToString() => Join(Name);

    /// <summary>Text for the UI, with symbols for punctuation and arrows.</summary>
    public string Display => Join(k => Pretty.TryGetValue(k, out var p) ? p : Name(k));

    public static bool TryParse(string text, out Gesture g)
    {
        g = default;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var mods = ModifierKeys.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= ModifierKeys.Control; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "win": mods |= ModifierKeys.Windows; break;
                default: return false;
            }
        }
        var last = parts[^1];
        var named = Names.FirstOrDefault(kv => string.Equals(kv.Value, last, StringComparison.OrdinalIgnoreCase));
        Key key;
        if (named.Value != null) key = named.Key;
        else if (last.Length == 1 && char.IsDigit(last[0])) key = Key.D0 + (last[0] - '0');
        else if (!Enum.TryParse(last, true, out key)) return false;
        g = new Gesture(mods, key);
        return true;
    }

    public static List<Gesture> ParseList(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => TryParse(p, out var g) ? g : (Gesture?)null).OfType<Gesture>().ToList();
}

public sealed record HotkeyAction(string Id, string Group, string DefaultGestures);

/// <summary>Every bindable action with its default keys (YouTube and Premiere Pro conventions).</summary>
public static class HotkeyCatalog
{
    public static readonly IReadOnlyList<HotkeyAction> Actions =
    [
        new("play_pause", "playback", "Space, K, MediaPlayPause"),
        new("stop", "playback", "MediaStop"),
        new("next", "playback", "Shift+N, MediaNextTrack"),
        new("previous", "playback", "Shift+P, MediaPreviousTrack"),
        new("frame_forward", "frames", "Right, Period"),
        new("frame_back", "frames", "Left, Comma"),
        new("frames_forward_5", "frames", "Shift+Right"),
        new("frames_back_5", "frames", "Shift+Left"),
        new("seek_forward_5", "seek", "Ctrl+Right"),
        new("seek_back_5", "seek", "Ctrl+Left"),
        new("seek_forward_10", "seek", "L"),
        new("seek_back_10", "seek", "J"),
        new("go_start", "seek", "Home, 0"),
        new("go_end", "seek", "End"),
        new("volume_up", "audio", "Up"),
        new("volume_down", "audio", "Down"),
        new("mute", "audio", "M"),
        new("speed_up", "playback", "Shift+Period"),
        new("speed_down", "playback", "Shift+Comma"),
        new("speed_reset", "playback", "Shift+0"),
        new("fullscreen", "view", "F, F11"),
        new("exit_fullscreen", "view", "Esc"),
        new("pip", "view", "I"),
        new("on_top", "view", "Ctrl+T"),
        new("queue", "view", "Ctrl+L"),
        new("random", "playback", "Shift+R"),
        new("subtitles_toggle", "subtitles", "C"),
        new("sub_delay_minus", "subtitles", "Z"),
        new("sub_delay_plus", "subtitles", "X"),
        new("sub_pos_up", "subtitles", "Shift+Up"),
        new("sub_pos_down", "subtitles", "Shift+Down"),
        new("audio_delay_minus", "audio", "Shift+Z"),
        new("audio_delay_plus", "audio", "Shift+X"),
        new("screenshot", "tools", "Ctrl+Shift+E, F12"),
        new("open_file", "tools", "Ctrl+O"),
        new("open_folder", "tools", "Ctrl+Shift+O"),
        new("settings", "tools", "Ctrl+Comma"),
    ];
}

/// <summary>Maps key presses to action ids, using the defaults plus the user's changes from Settings.</summary>
public sealed class HotkeyMap
{
    private readonly Dictionary<Gesture, string> _map = new();

    public HotkeyMap(IReadOnlyDictionary<string, string> overrides) => Rebuild(overrides);

    public static string GesturesFor(string id, IReadOnlyDictionary<string, string> overrides) =>
        overrides.TryGetValue(id, out var s) ? s : HotkeyCatalog.Actions.First(a => a.Id == id).DefaultGestures;

    public void Rebuild(IReadOnlyDictionary<string, string> overrides)
    {
        _map.Clear();
        foreach (var a in HotkeyCatalog.Actions)
            foreach (var g in Gesture.ParseList(GesturesFor(a.Id, overrides)))
                _map.TryAdd(g, a.Id);
    }

    public string? Lookup(Key key, ModifierKeys mods)
    {
        if (_map.TryGetValue(new Gesture(mods, key), out var id)) return id;
        // Number keys 1-9 jump to 10 %…90 % (YouTube), unless bound to something else above.
        if (mods == ModifierKeys.None && key is >= Key.D1 and <= Key.D9) return "percent_" + (int)(key - Key.D0);
        if (mods == ModifierKeys.None && key is >= Key.NumPad1 and <= Key.NumPad9) return "percent_" + (int)(key - Key.NumPad0);
        return null;
    }
}
