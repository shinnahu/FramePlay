using System.IO;
using System.Text;

namespace FramePlay.Core;

/// <summary>Where FramePlay keeps its files. <c>--data-dir=&lt;dir&gt;</c> switches to a separate test profile.</summary>
public static class AppPaths
{
    public static string AppDir { get; } = AppContext.BaseDirectory;
    public static string ConfigDir { get; private set; } = "";
    public static string LocalDir { get; private set; } = "";

    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");
    public static string HistoryFile => Path.Combine(ConfigDir, "history.json");
    public static string LogFile => Path.Combine(LocalDir, "logs", "frameplay.log");
    public static string MpvLogFile => Path.Combine(LocalDir, "logs", "mpv.log");

    public static string DefaultScreenshotDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "FramePlay");

    public static void Init(string? dataDir)
    {
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            var root = Path.GetFullPath(dataDir);
            ConfigDir = Path.Combine(root, "config");
            LocalDir = Path.Combine(root, "local");
        }
        else
        {
            ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FramePlay");
            LocalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FramePlay");
        }
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(Path.Combine(LocalDir, "logs"));
    }

    /// <summary>Writes a file through a temporary copy so a crash never leaves it half-written.</summary>
    public static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
