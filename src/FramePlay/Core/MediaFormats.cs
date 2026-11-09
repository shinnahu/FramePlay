using System.IO;

namespace FramePlay.Core;

public enum MediaKind { Video, Audio, Midi, Subtitle, Unknown }

public static class MediaFormats
{
    // The formats FramePlay is built for come first; mpv plays the rest too.
    public static readonly string[] Video =
        [".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v", ".wmv", ".flv", ".ts", ".m2ts", ".mts", ".3gp", ".ogv", ".mpg", ".mpeg"];

    public static readonly string[] Audio =
        [".mp3", ".wav", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif", ".alac", ".mka"];

    public static readonly string[] Midi = [".mid", ".midi", ".kar", ".rmi"];

    public static readonly string[] Subtitles = [".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi"];

    /// <summary>Extensions FramePlay offers to open by default in Windows (the formats the user asked for).</summary>
    public static readonly string[] Associations =
        [".mp4", ".mkv", ".webm", ".mov", ".avi", ".mp3", ".wav", ".m4a", ".flac", ".mid", ".midi"];

    public static MediaKind KindOf(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (Array.IndexOf(Video, ext) >= 0) return MediaKind.Video;
        if (Array.IndexOf(Audio, ext) >= 0) return MediaKind.Audio;
        if (Array.IndexOf(Midi, ext) >= 0) return MediaKind.Midi;
        if (Array.IndexOf(Subtitles, ext) >= 0) return MediaKind.Subtitle;
        return MediaKind.Unknown;
    }

    public static bool IsPlayable(string path) => KindOf(path) is MediaKind.Video or MediaKind.Audio or MediaKind.Midi;

    public static string OpenDialogFilter(Func<string, string> t)
    {
        static string Pattern(IEnumerable<string> exts) => string.Join(";", exts.Select(e => "*" + e));
        var all = Video.Concat(Audio).Concat(Midi);
        return $"{t("filter.media")}|{Pattern(all)}|{t("filter.video")}|{Pattern(Video)}|{t("filter.audio")}|{Pattern(Audio.Concat(Midi))}|{t("filter.all")}|*.*";
    }

    public static string SubtitleDialogFilter(Func<string, string> t) =>
        $"{t("filter.subtitles")}|{string.Join(";", Subtitles.Select(e => "*" + e))}|{t("filter.all")}|*.*";

    /// <summary>Expands folders into their playable files (sorted naturally) and keeps files as they are.</summary>
    public static List<string> ExpandPaths(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var p in paths)
        {
            try
            {
                if (Directory.Exists(p))
                {
                    var files = Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                        .Where(IsPlayable)
                        .OrderBy(f => f, NaturalComparer.Instance);
                    result.AddRange(files);
                }
                else if (File.Exists(p) && (IsPlayable(p) || KindOf(p) == MediaKind.Unknown))
                {
                    result.Add(Path.GetFullPath(p));
                }
            }
            catch (Exception ex) { Log.Error("ExpandPaths " + p, ex); }
        }
        return result;
    }
}

/// <summary>Sorts "Episode 2" before "Episode 10", like File Explorer.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? "", y ?? "");
}
