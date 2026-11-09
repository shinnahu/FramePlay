using System.IO;

namespace FramePlay.Core;

internal static class Log
{
    private static readonly object Gate = new();

    public static void Info(string msg) => Write(AppPaths.LogFile, "INFO", msg);

    public static readonly DateTime ProcessStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;

    public static readonly bool TimingEnabled = Environment.GetEnvironmentVariable("FRAMEPLAY_TIMING") == "1";

    /// <summary>Startup timing: milliseconds since the process started (logged only with FRAMEPLAY_TIMING=1).</summary>
    public static void Timing(string step)
    {
        if (TimingEnabled)
            Write(AppPaths.LogFile, "TIME", $"{(DateTime.Now - ProcessStart).TotalMilliseconds,6:0} ms  {step}");
    }
    public static void Warn(string msg) => Write(AppPaths.LogFile, "WARN", msg);
    public static void Error(string where, Exception? ex) => Write(AppPaths.LogFile, "ERROR", where + ": " + ex);
    public static void Mpv(string level, string prefix, string text) => Write(AppPaths.MpvLogFile, level, $"[{prefix}] {text.TrimEnd()}");

    private static void Write(string file, string level, string msg)
    {
        if (string.IsNullOrEmpty(AppPaths.LocalDir)) return;
        try
        {
            lock (Gate)
            {
                var fi = new FileInfo(file);
                if (fi.Exists && fi.Length > 2_000_000) fi.Delete();
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch { /* logging must never crash the app */ }
    }
}
