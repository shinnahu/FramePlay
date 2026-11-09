using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FramePlay.Core;

/// <summary>
/// Registers FramePlay with Windows for the current user (no admin rights): one ProgID per format, an
/// "Open with" entry and a Default apps entry. Windows itself makes the final choice: it opens its
/// Default apps page so the user confirms FramePlay there.
/// </summary>
public static class FileAssociations
{
    private const string ProgIdPrefix = "FramePlay.";
    private const string CapabilitiesKey = @"Software\FramePlay\Capabilities";

    [DllImport("shell32.dll")] private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

    private static string Describe(string ext) => ext switch
    {
        ".mid" or ".midi" => "MIDI file",
        ".mp3" or ".wav" or ".m4a" or ".flac" => ext.TrimStart('.').ToUpperInvariant() + " audio",
        _ => ext.TrimStart('.').ToUpperInvariant() + " video",
    };

    public static void Register()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("No process path");
        string open = $"\"{exe}\" \"%1\"";
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        foreach (var ext in MediaFormats.Associations)
        {
            var progId = ProgIdPrefix + ext.TrimStart('.');
            using (var k = classes.CreateSubKey(progId))
            {
                k.SetValue("", Describe(ext));
                using (var icon = k.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{exe}\",0");
                using (var verb = k.CreateSubKey(@"shell\open"))
                {
                    // Selecting several files and pressing Enter sends them to one FramePlay as a queue.
                    verb.SetValue("MultiSelectModel", "Player");
                    using var cmd = verb.CreateSubKey("command");
                    cmd.SetValue("", open);
                }
            }
            using (var e = classes.CreateSubKey(ext + @"\OpenWithProgids")) e.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
        }
        using (var app = classes.CreateSubKey(@"Applications\FramePlay.exe"))
        {
            app.SetValue("FriendlyAppName", "FramePlay");
            using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue("", open);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (var ext in MediaFormats.Associations) types.SetValue(ext, "");
        }
        using (var cap = Registry.CurrentUser.CreateSubKey(CapabilitiesKey))
        {
            cap.SetValue("ApplicationName", "FramePlay");
            cap.SetValue("ApplicationDescription", "A fast, frame-exact media player");
            cap.SetValue("ApplicationIcon", $"\"{exe}\",0");
            using var fa = cap.CreateSubKey("FileAssociations");
            foreach (var ext in MediaFormats.Associations) fa.SetValue(ext, ProgIdPrefix + ext.TrimStart('.'));
        }
        using (var reg = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications")) reg.SetValue("FramePlay", CapabilitiesKey);
        SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0, IntPtr.Zero, IntPtr.Zero);
        Log.Info("Registered file associations for " + exe);
    }

    /// <summary>Opens Windows' Default apps page for FramePlay so the user can confirm it.</summary>
    public static void OpenDefaultAppsSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=FramePlay") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
    }
}
