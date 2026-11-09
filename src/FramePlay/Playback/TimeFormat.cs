using System.Globalization;

namespace FramePlay.Playback;

public enum TimeDisplayMode { Time, Timecode, Frames }

public static class TimeFormat
{
    public static TimeDisplayMode Parse(string? s) => s switch
    {
        "timecode" => TimeDisplayMode.Timecode,
        "frames" => TimeDisplayMode.Frames,
        _ => TimeDisplayMode.Time,
    };

    public static string ToSetting(TimeDisplayMode m) => m switch
    {
        TimeDisplayMode.Timecode => "timecode",
        TimeDisplayMode.Frames => "frames",
        _ => "time",
    };

    /// <summary>Frame index for a timestamp. A tiny epsilon keeps 0.0333333 at 30 fps on frame 1, not 0.</summary>
    public static long FrameIndex(double seconds, double fps) => fps > 0 ? (long)Math.Floor(seconds * fps + 1e-3) : 0;

    public static string Format(double seconds, TimeDisplayMode mode, double fps, bool longForm)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        switch (mode)
        {
            case TimeDisplayMode.Frames when fps > 0:
                return FrameIndex(seconds, fps).ToString(CultureInfo.InvariantCulture);
            case TimeDisplayMode.Timecode when fps > 0:
            {
                long whole = (long)Math.Floor(seconds + 1e-6);
                int frame = (int)Math.Floor((seconds - whole) * fps + 1e-3);
                int maxFrame = (int)Math.Ceiling(fps) - 1;
                if (frame > maxFrame) frame = maxFrame;
                var ts = TimeSpan.FromSeconds(whole);
                return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}:{frame:00}";
            }
            default:
                return Clock(seconds, longForm);
        }
    }

    public static string Clock(double seconds, bool longForm)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(Math.Floor(seconds));
        return longForm || ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes}:{ts.Seconds:00}";
    }

    /// <summary>"+0.250 s" style for delays.</summary>
    public static string Delay(double seconds) =>
        (seconds >= 0 ? "+" : "−") + Math.Abs(seconds).ToString("0.000", CultureInfo.InvariantCulture) + " s";
}
