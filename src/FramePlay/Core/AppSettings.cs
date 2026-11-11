using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FramePlay.Themes;

namespace FramePlay.Core;

public sealed class SubtitleStyle
{
    /// <summary>Vertical position, 0 (top) to 100 (bottom), like mpv's sub-pos. The UI shows it as the height above the bottom (100 - Position).</summary>
    public double Position { get; set; } = 95;
    public string Font { get; set; } = "Segoe UI Semibold";
    public double FontSize { get; set; } = 44;
    /// <summary>Size multiplier in picture in picture, where the small window makes subtitles small (1.35 = 135 %).</summary>
    public double PipScale { get; set; } = 1.35;
    public double OutlineSize { get; set; } = 2.5;
    public string TextColor { get; set; } = "#FFFFFF";
    public string OutlineColor { get; set; } = "#000000";
    public bool Shadow { get; set; } = false;
    // Colours use CSS order, #RRGGBBAA (alpha last), like MarkNotes themes.
    public string ShadowColor { get; set; } = "#000000a0";
    public double ShadowOffset { get; set; } = 2;
    public bool Background { get; set; } = false;
    public string BackgroundColor { get; set; } = "#000000a6";
    /// <summary>Text encoding for subtitle files without a BOM: "auto" or an encoding name such as "cp1252".</summary>
    public string Encoding { get; set; } = "auto";
    /// <summary>Apply this style to .ass/.ssa subtitles too (they normally keep their own styling).</summary>
    public bool OverrideAss { get; set; } = false;
}

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

public sealed class AppSettings
{
    // General
    public string Language { get; set; } = "auto";
    /// <summary>The Setup language last applied (see <see cref="InstallerLanguage"/>), so it's applied only once.</summary>
    public string? InstallerLanguageApplied { get; set; }
    public bool TopmostWhilePlayingVideo { get; set; } = false;
    public bool SingleInstance { get; set; } = false;
    public bool SingleInstanceFromFileManager { get; set; } = true;
    public bool PauseOnLastFrame { get; set; } = false;
    public bool ResumePosition { get; set; } = true;
    public bool AutoPipOnMinimize { get; set; } = false;

    // Playback state that is remembered between runs
    public double Volume { get; set; } = 100;
    public bool Muted { get; set; }
    public bool Shuffle { get; set; }
    /// <summary>"off", "all" (loop the queue) or "one" (loop the current item).</summary>
    public string Loop { get; set; } = "off";
    public bool QueueOpen { get; set; }
    /// <summary>"time", "timecode" or "frames".</summary>
    public string TimeDisplay { get; set; } = "time";
    public bool ShowRemainingTime { get; set; }

    // Screenshots
    public string ScreenshotFolder { get; set; } = "";
    public bool ScreenshotWithSubtitles { get; set; } = false;

    // Subtitles
    public SubtitleStyle Subtitles { get; set; } = new();
    public string PreferredSubtitleLanguages { get; set; } = "";
    public string PreferredAudioLanguages { get; set; } = "";

    // MIDI
    public string Soundfont { get; set; } = "Classic_Station.sf2";

    // Hotkeys: action id -> gestures ("Ctrl+Right, L"). Only changed actions are stored.
    public Dictionary<string, string> Hotkeys { get; set; } = new();

    // Appearance
    public string Theme { get; set; } = "dark";
    /// <summary>An accent colour (#rrggbb) that replaces the theme's own; null keeps the theme's.</summary>
    public string? AccentColor { get; set; }
    public List<ThemeDefinition> CustomThemes { get; set; } = new();

    // Windows
    public WindowBounds? Window { get; set; }
    public WindowBounds? Pip { get; set; }

    // ---------------------------------------------------------------------------------------------
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The language picked in Setup ("en" or "es"), which the installer stores in the registry. FramePlay adopts it
    /// after an install, and again only when a later Setup run picks a different one, so a language chosen in
    /// Settings afterwards is kept.
    /// </summary>
    public bool ApplyInstallerLanguage()
    {
        string? lang = null;
        try
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                using var key = hive.OpenSubKey(@"Software\FramePlay");
                if (key?.GetValue("Language") is string s && s is "en" or "es") { lang = s; break; }
            }
        }
        catch { /* no access: keep the current language */ }
        if (lang == null || lang == InstallerLanguageApplied) return false;
        Language = lang;
        InstallerLanguageApplied = lang;
        return true;
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Error("Settings.Load", ex);
            try { File.Copy(AppPaths.SettingsFile, AppPaths.SettingsFile + ".broken", true); } catch { }
        }
        return new AppSettings();
    }

    public void Save()
    {
        try { AppPaths.WriteAtomic(AppPaths.SettingsFile, JsonSerializer.Serialize(this, Json)); }
        catch (Exception ex) { Log.Error("Settings.Save", ex); }
    }

    /// <summary>Reads only the single-instance switches, before the UI starts.</summary>
    public static (bool single, bool fromFileManager) PeekInstanceOptions()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile)) return (false, true);
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.SettingsFile),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var r = doc.RootElement;
            bool single = r.TryGetProperty("singleInstance", out var a) && a.ValueKind == JsonValueKind.True;
            bool fm = !r.TryGetProperty("singleInstanceFromFileManager", out var b) || b.ValueKind != JsonValueKind.False;
            return (single, fm);
        }
        catch { return (false, true); }
    }
}
