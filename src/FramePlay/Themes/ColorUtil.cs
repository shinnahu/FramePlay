using System.Globalization;
using System.Windows.Media;

namespace FramePlay.Themes;

public static class ColorUtil
{
    /// <summary>Parses CSS-style colours: #rgb, #rrggbb, #rrggbbaa, rgb(r,g,b), rgba(r,g,b,a).</summary>
    public static bool TryParse(string? s, out Color c)
    {
        c = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        try
        {
            if (s.StartsWith('#'))
            {
                var h = s[1..];
                if (h.Length == 3) h = string.Concat(h.Select(ch => new string(ch, 2)));
                if (h.Length == 6)
                {
                    c = Color.FromRgb(Hex(h, 0), Hex(h, 2), Hex(h, 4));
                    return true;
                }
                if (h.Length == 8)
                {
                    // CSS order: RRGGBBAA
                    c = Color.FromArgb(Hex(h, 6), Hex(h, 0), Hex(h, 2), Hex(h, 4));
                    return true;
                }
                return false;
            }
            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int open = s.IndexOf('('), close = s.LastIndexOf(')');
                if (open < 0 || close < open) return false;
                var parts = s[(open + 1)..close].Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;
                byte r = Channel(parts[0]), g = Channel(parts[1]), b = Channel(parts[2]);
                byte a = 255;
                if (parts.Length >= 4)
                {
                    var ap = parts[3];
                    double av = ap.EndsWith('%') ? double.Parse(ap.TrimEnd('%'), CultureInfo.InvariantCulture) / 100 : double.Parse(ap, CultureInfo.InvariantCulture);
                    a = (byte)Math.Clamp(Math.Round(av * 255), 0, 255);
                }
                c = Color.FromArgb(a, r, g, b);
                return true;
            }
        }
        catch { }
        return false;
    }

    public static Color Parse(string? s, Color fallback) => TryParse(s, out var c) ? c : fallback;

    private static byte Hex(string h, int i) => byte.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber);

    private static byte Channel(string p) => p.EndsWith('%')
        ? (byte)Math.Clamp(Math.Round(double.Parse(p.TrimEnd('%'), CultureInfo.InvariantCulture) * 2.55), 0, 255)
        : (byte)Math.Clamp(Math.Round(double.Parse(p, CultureInfo.InvariantCulture)), 0, 255);

    public static string ToHex(Color c) => c.A == 255 ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : $"#{c.R:x2}{c.G:x2}{c.B:x2}{c.A:x2}";

    /// <summary>mpv colour syntax is #AARRGGBB (alpha first).</summary>
    public static string ToMpv(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp(Math.Round(alpha * 255), 0, 255), c.R, c.G, c.B);

    /// <summary>Mixes <paramref name="a"/> into <paramref name="b"/> by <paramref name="t"/> (0 = b, 1 = a).</summary>
    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A * t + b.A * (1 - t)), (byte)Math.Round(a.R * t + b.R * (1 - t)),
        (byte)Math.Round(a.G * t + b.G * (1 - t)), (byte)Math.Round(a.B * t + b.B * (1 - t)));

    /// <summary>Flattens a translucent colour onto an opaque background.</summary>
    public static Color Over(Color top, Color bottom) => Mix(Color.FromRgb(top.R, top.G, top.B), bottom, top.A / 255.0);

    public static double Luminance(Color c)
    {
        static double Lin(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static bool IsDark(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B < 140;
}
