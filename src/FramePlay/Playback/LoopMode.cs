namespace FramePlay.Playback;

/// <summary>The loop button, like VLC's: no loop, loop the whole queue, or loop the current item.</summary>
public enum LoopMode { Off, All, One }

public static class LoopModes
{
    public static LoopMode Parse(string? s) => s switch
    {
        "all" => LoopMode.All,
        "one" => LoopMode.One,
        _ => LoopMode.Off,
    };

    public static string ToSetting(LoopMode m) => m switch
    {
        LoopMode.All => "all",
        LoopMode.One => "one",
        _ => "off",
    };

    /// <summary>The button's order: no loop → loop all → loop one → no loop.</summary>
    public static LoopMode Next(LoopMode m) => m switch
    {
        LoopMode.Off => LoopMode.All,
        LoopMode.All => LoopMode.One,
        _ => LoopMode.Off,
    };

    /// <summary>Tooltip and toast text for a mode.</summary>
    public static string Key(LoopMode m) => m switch
    {
        LoopMode.All => "tip.loopAll",
        LoopMode.One => "tip.loopOne",
        _ => "tip.loopOff",
    };
}
