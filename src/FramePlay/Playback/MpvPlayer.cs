using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Interop;
using static FramePlay.Interop.LibMpv;

namespace FramePlay.Playback;

public sealed record TrackInfo(
    long Id, string Type, string? Title, string? Lang, string? Codec, bool IsDefault, bool IsForced,
    bool IsExternal, string? ExternalFilename, bool Selected, int MainSelection, bool IsImage,
    double DemuxFps, int Width, int Height, int Channels, int SampleRate)
{
    /// <summary>"English", "Commentary (English)", or the file name when nothing better is known.</summary>
    public string Label
    {
        get
        {
            string? lang = LanguageName(Lang);
            string? title = string.IsNullOrWhiteSpace(Title) ? null : Title!.Trim();
            // mpv titles external files after the file name ("en.srt"), which says nothing new.
            if (title != null && IsExternal && ExternalFilename != null &&
                System.IO.Path.GetFileName(ExternalFilename).EndsWith(title, StringComparison.OrdinalIgnoreCase))
                title = null;
            string label = title ?? lang ?? (ExternalFilename != null ? System.IO.Path.GetFileName(ExternalFilename) : "#" + Id);
            if (title != null && lang != null && !title.Contains(lang, StringComparison.OrdinalIgnoreCase)) label += " (" + lang + ")";
            return label;
        }
    }

    private static readonly Dictionary<string, string?> LanguageNames = new(StringComparer.OrdinalIgnoreCase);

    // ISO 639-2/B codes (common in MKV files) that differ from the /T codes .NET knows.
    private static readonly Dictionary<string, string> BibliographicCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fre"] = "fr", ["ger"] = "de", ["chi"] = "zh", ["dut"] = "nl", ["cze"] = "cs", ["gre"] = "el", ["per"] = "fa",
        ["rum"] = "ro", ["slo"] = "sk", ["alb"] = "sq", ["arm"] = "hy", ["baq"] = "eu", ["bur"] = "my", ["geo"] = "ka",
        ["ice"] = "is", ["mac"] = "mk", ["mao"] = "mi", ["may"] = "ms", ["tib"] = "bo", ["wel"] = "cy",
    };

    /// <summary>The language's own name ("English", "Español") for an ISO 639 code, or null when unknown.</summary>
    public static string? LanguageName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        if (code is "und" or "zxx" or "mul" or "mis") return null;
        lock (LanguageNames)
        {
            if (LanguageNames.TryGetValue(code, out var cached)) return cached;
            string lookup = BibliographicCodes.TryGetValue(code, out var t) ? t : code;
            string? name = null;
            foreach (var c in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures))
            {
                if (string.Equals(c.TwoLetterISOLanguageName, lookup, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.ThreeLetterISOLanguageName, lookup, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name, lookup, StringComparison.OrdinalIgnoreCase))
                {
                    var n = c.NativeName;
                    if (n.Length > 0) name = char.ToUpper(n[0], c) + n[1..];
                    break;
                }
            }
            name ??= code.ToUpperInvariant();
            LanguageNames[code] = name;
            return name;
        }
    }
}

/// <summary>Thin, thread-aware wrapper around one libmpv instance. Events are raised on the UI thread.</summary>
public sealed class MpvPlayer : IDisposable
{
    private IntPtr _ctx;
    private Thread? _events;
    private readonly Dispatcher _ui;

    private readonly object _gate = new();
    private readonly Dictionary<string, object?> _pending = new();
    private bool _drainQueued;

    public event Action<string, object?>? PropertyChanged;
    public event Action? StartFile;
    public event Action? FileLoaded;
    public event Action<EndFileReason, int>? EndFile;
    public event Action? PlaybackRestart;
    public event Action? VideoReconfig;
    public event Action<ulong, int>? CommandReply;
    public event Action? Shutdown;

    public MpvPlayer(Dispatcher ui) => _ui = ui;

    public bool IsAlive => _ctx != IntPtr.Zero;

    public void Create()
    {
        _ctx = mpv_create();
        if (_ctx == IntPtr.Zero) throw new InvalidOperationException("mpv_create failed");
    }

    public bool SetOption(string name, string value)
    {
        int r = mpv_set_option_string(_ctx, Utf8(name), Utf8(value));
        if (r < 0) Log.Warn($"mpv option {name}={value}: {ErrorString(r)}");
        return r >= 0;
    }

    public void Initialize(string logLevel = "warn")
    {
        int r = mpv_initialize(_ctx);
        if (r < 0) throw new InvalidOperationException("mpv_initialize: " + ErrorString(r));
        mpv_request_log_messages(_ctx, Utf8(logLevel));
        _events = new Thread(EventLoop) { IsBackground = true, Name = "mpv events" };
        _events.Start();
    }

    public void Observe(string name, Format format) => mpv_observe_property(_ctx, 0, Utf8(name), format);

    // ---- commands ----
    public int Command(params string[] args)
    {
        if (_ctx == IntPtr.Zero) return -1;
        var a = AllocArgs(args);
        try
        {
            int r = mpv_command(_ctx, a);
            if (r < 0) Log.Warn($"mpv command {string.Join(' ', args)}: {ErrorString(r)}");
            return r;
        }
        finally { FreeArgs(a); }
    }

    public void CommandAsync(ulong reply, params string[] args)
    {
        if (_ctx == IntPtr.Zero) return;
        var a = AllocArgs(args);
        try
        {
            int r = mpv_command_async(_ctx, reply, a);
            if (r < 0) Log.Warn($"mpv async {string.Join(' ', args)}: {ErrorString(r)}");
        }
        finally { FreeArgs(a); }
    }

    // ---- properties ----
    public bool SetString(string name, string value)
    {
        if (_ctx == IntPtr.Zero) return false;
        int r = mpv_set_property_string(_ctx, Utf8(name), Utf8(value));
        if (r < 0) Log.Warn($"mpv set {name}={value}: {ErrorString(r)}");
        return r >= 0;
    }

    public void SetDouble(string name, double value)
    {
        if (_ctx == IntPtr.Zero) return;
        int r = mpv_set_property(_ctx, Utf8(name), Format.Double, ref value);
        if (r < 0) Log.Warn($"mpv set {name}={value}: {ErrorString(r)}");
    }

    public void SetFlag(string name, bool value)
    {
        if (_ctx == IntPtr.Zero) return;
        int v = value ? 1 : 0;
        int r = mpv_set_property(_ctx, Utf8(name), Format.Flag, ref v);
        if (r < 0) Log.Warn($"mpv set {name}={value}: {ErrorString(r)}");
    }

    public string? GetString(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        var p = mpv_get_property_string(_ctx, Utf8(name));
        if (p == IntPtr.Zero) return null;
        try { return FromUtf8(p); } finally { mpv_free(p); }
    }

    public double? GetDouble(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        return mpv_get_property(_ctx, Utf8(name), Format.Double, out double v) >= 0 ? v : null;
    }

    public long? GetLong(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        return mpv_get_property(_ctx, Utf8(name), Format.Int64, out long v) >= 0 ? v : null;
    }

    public bool? GetFlag(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        return mpv_get_property(_ctx, Utf8(name), Format.Flag, out int v) >= 0 ? v != 0 : null;
    }

    public List<TrackInfo> GetTracks()
    {
        var list = new List<TrackInfo>();
        var json = GetString("track-list");
        if (string.IsNullOrEmpty(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var t in doc.RootElement.EnumerateArray())
            {
                string S(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                bool B(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                long L(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
                double D(string k) => t.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
                int main = t.TryGetProperty("main-selection", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetInt32() : -1;
                list.Add(new TrackInfo(
                    L("id"), S("type"), NullIfEmpty(S("title")), NullIfEmpty(S("lang")), NullIfEmpty(S("codec")),
                    B("default"), B("forced"), B("external"), NullIfEmpty(S("external-filename")), B("selected"), main,
                    B("image") || B("albumart"), D("demux-fps"), (int)L("demux-w"), (int)L("demux-h"),
                    (int)L("demux-channel-count"), (int)L("demux-samplerate")));
            }
        }
        catch (Exception ex) { Log.Error("GetTracks", ex); }
        return list;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    // ---- events ----
    private void EventLoop()
    {
        while (true)
        {
            IntPtr evPtr = mpv_wait_event(_ctx, -1);
            var ev = Marshal.PtrToStructure<Event>(evPtr);
            try
            {
                switch (ev.EventId)
                {
                    case EventId.None:
                        break;
                    case EventId.Shutdown:
                        Post(() => Shutdown?.Invoke());
                        return;
                    case EventId.PropertyChange:
                    {
                        var p = Marshal.PtrToStructure<EventProperty>(ev.Data);
                        string name = FromUtf8(p.Name) ?? "";
                        object? val = p.Format switch
                        {
                            Format.Double => Marshal.PtrToStructure<double>(p.Data),
                            Format.Flag => Marshal.ReadInt32(p.Data) != 0,
                            Format.Int64 => Marshal.ReadInt64(p.Data),
                            Format.String or Format.OsdString => FromUtf8(Marshal.ReadIntPtr(p.Data)),
                            _ => null,
                        };
                        QueueProperty(name, val);
                        break;
                    }
                    case EventId.StartFile: Post(() => StartFile?.Invoke()); break;
                    case EventId.FileLoaded: Post(() => FileLoaded?.Invoke()); break;
                    case EventId.EndFile:
                    {
                        var ef = Marshal.PtrToStructure<EventEndFile>(ev.Data);
                        var reason = ef.Reason; var err = ef.Error;
                        Post(() => EndFile?.Invoke(reason, err));
                        break;
                    }
                    case EventId.PlaybackRestart: Post(() => PlaybackRestart?.Invoke()); break;
                    case EventId.VideoReconfig: Post(() => VideoReconfig?.Invoke()); break;
                    case EventId.CommandReply:
                    {
                        ulong id = ev.ReplyUserdata; int err = ev.Error;
                        if (err < 0) Log.Warn($"mpv async command {id} failed: {ErrorString(err)}");
                        Post(() => CommandReply?.Invoke(id, err));
                        break;
                    }
                    case EventId.LogMessage:
                    {
                        var lm = Marshal.PtrToStructure<EventLogMessage>(ev.Data);
                        Log.Mpv(FromUtf8(lm.Level) ?? "?", FromUtf8(lm.Prefix) ?? "?", FromUtf8(lm.Text) ?? "");
                        break;
                    }
                }
            }
            catch (Exception ex) { Log.Error("mpv event " + ev.EventId, ex); }
        }
    }

    private void Post(Action a) => _ui.BeginInvoke(DispatcherPriority.Normal, a);

    private void QueueProperty(string name, object? value)
    {
        lock (_gate)
        {
            _pending[name] = value;
            if (_drainQueued) return;
            _drainQueued = true;
        }
        _ui.BeginInvoke(DispatcherPriority.Normal, DrainProperties);
    }

    private void DrainProperties()
    {
        KeyValuePair<string, object?>[] items;
        lock (_gate)
        {
            items = _pending.ToArray();
            _pending.Clear();
            _drainQueued = false;
        }
        foreach (var kv in items) PropertyChanged?.Invoke(kv.Key, kv.Value);
    }

    public void Dispose()
    {
        if (_ctx == IntPtr.Zero) return;
        try
        {
            Command("quit");
            _events?.Join(3000);
        }
        catch (Exception ex) { Log.Error("mpv quit", ex); }
        mpv_terminate_destroy(_ctx);
        _ctx = IntPtr.Zero;
    }
}
