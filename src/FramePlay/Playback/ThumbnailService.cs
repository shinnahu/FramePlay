using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Interop;
using static FramePlay.Interop.LibMpv;

namespace FramePlay.Playback;

/// <summary>
/// Seek-bar thumbnails from a second, hidden mpv instance using mpv's software renderer: a fast keyframe
/// seek, then the frame is scaled straight into a small bitmap. Only the newest request is served.
/// </summary>
public sealed class ThumbnailService : IDisposable
{
    private readonly Dispatcher _ui;
    private readonly Thread _thread;
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly Dictionary<string, BitmapSource> _cache = new();
    private readonly LinkedList<string> _lru = new();
    private Job? _request;
    private volatile bool _stop;
    private IntPtr _mpv, _render;
    private RenderUpdateFn? _updateFn;
    private string? _loaded;

    private sealed record Job(string Path, double Time, int Width, int Height, Action<double, BitmapSource?> Done);

    public ThumbnailService(Dispatcher ui)
    {
        _ui = ui;
        _thread = new Thread(Loop) { IsBackground = true, Name = "FramePlay thumbnails", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    private static string Key(string path, double t, int w) => $"{path}|{(int)Math.Floor(t)}|{w}";

    /// <summary>Calls <paramref name="done"/> on the UI thread with the frame near <paramref name="seconds"/>.</summary>
    public void Get(string path, double seconds, int width, int height, Action<double, BitmapSource?> done)
    {
        var key = Key(path, seconds, width);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var hit))
            {
                _lru.Remove(key);
                _lru.AddFirst(key);
                done(seconds, hit);
                return;
            }
            _request = new Job(path, seconds, width, height, done);
        }
        _wake.Set();
    }

    private void Loop()
    {
        try { Init(); }
        catch (Exception ex) { Log.Error("Thumbnails init", ex); return; }

        while (!_stop)
        {
            _wake.WaitOne();
            if (_stop) break;
            Job? req;
            lock (_gate) { req = _request; _request = null; }
            if (req == null) continue;
            BitmapSource? bmp = null;
            try { bmp = Render(req); }
            catch (Exception ex) { Log.Error("Thumbnail", ex); }
            if (bmp != null)
            {
                lock (_gate)
                {
                    var key = Key(req.Path, req.Time, req.Width);
                    _cache[key] = bmp;
                    _lru.AddFirst(key);
                    while (_lru.Count > 400)
                    {
                        _cache.Remove(_lru.Last!.Value);
                        _lru.RemoveLast();
                    }
                }
            }
            var done = req.Done;
            var t = req.Time;
            _ui.BeginInvoke(DispatcherPriority.Background, () => done(t, bmp));
        }
    }

    private void Set(string name, string value) => mpv_set_option_string(_mpv, Utf8(name), Utf8(value));

    private void Init()
    {
        _mpv = mpv_create();
        foreach (var (k, v) in new[]
        {
            ("config", "no"), ("load-scripts", "no"), ("ytdl", "no"), ("osc", "no"), ("terminal", "no"),
            ("vo", "libmpv"), ("hwdec", "auto-copy"), ("aid", "no"), ("sid", "no"), ("ao", "null"), ("sub-auto", "no"),
            ("load-stats-overlay", "no"), ("load-console", "no"), ("load-select", "no"), ("load-positioning", "no"),
            ("load-commands", "no"), ("load-auto-profiles", "no"), ("demuxer-max-bytes", "8MiB"),
            ("pause", "yes"), ("idle", "yes"), ("keep-open", "always"), ("hr-seek", "no"),
            ("vd-lavc-skiploopfilter", "all"), ("vd-lavc-fast", "yes"), ("vd-lavc-threads", "2"),
            ("demuxer-readahead-secs", "0"), ("cache", "no"), ("sws-scaler", "bilinear"), ("msg-level", "all=error"),
        }) Set(k, v);
        int r = mpv_initialize(_mpv);
        if (r < 0) throw new InvalidOperationException("thumbnail mpv_initialize: " + ErrorString(r));

        var api = Marshal.StringToCoTaskMemUTF8("sw");
        try
        {
            var p = new[] { new RenderParam { Type = RenderParamType.ApiType, Data = api }, new RenderParam { Type = RenderParamType.Invalid } };
            r = mpv_render_context_create(out _render, _mpv, p);
            if (r < 0) throw new InvalidOperationException("thumbnail render context: " + ErrorString(r));
        }
        finally { Marshal.FreeCoTaskMem(api); }
        _updateFn = _ => _frameReady.Set();
        mpv_render_context_set_update_callback(_render, _updateFn, IntPtr.Zero);
    }

    private void Command(params string[] args)
    {
        var a = AllocArgs(args);
        try { mpv_command(_mpv, a); } finally { FreeArgs(a); }
    }

    /// <summary>Waits for an mpv event (draining others) up to the timeout.</summary>
    private bool WaitFor(EventId id, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            double left = (deadline - Environment.TickCount64) / 1000.0;
            if (left <= 0) return false;
            var ev = Marshal.PtrToStructure<Event>(mpv_wait_event(_mpv, left));
            if (ev.EventId == id) return true;
            if (ev.EventId == EventId.EndFile && id == EventId.FileLoaded)
            {
                var ef = Marshal.PtrToStructure<EventEndFile>(ev.Data);
                if (ef.Reason == EndFileReason.Error) return false;
            }
            if (ev.EventId == EventId.Shutdown) return false;
        }
    }

    private BitmapSource? Render(Job req)
    {
        var t = req.Time.ToString("0.###", CultureInfo.InvariantCulture);
        _frameReady.Reset();
        if (!string.Equals(_loaded, req.Path, StringComparison.OrdinalIgnoreCase))
        {
            mpv_set_property_string(_mpv, Utf8("start"), Utf8(t));
            Command("loadfile", req.Path, "replace");
            if (!WaitFor(EventId.FileLoaded, 4000)) { _loaded = null; return null; }
            _loaded = req.Path;
            mpv_set_property_string(_mpv, Utf8("start"), Utf8("none"));
        }
        else
        {
            Command("seek", t, "absolute+keyframes");
        }
        if (!WaitFor(EventId.PlaybackRestart, 3000)) return null;
        if (!_frameReady.WaitOne(1500)) return null;
        if ((mpv_render_context_update(_render) & 1) == 0)
        {
            // The frame may arrive right after the restart event.
            if (!_frameReady.WaitOne(500)) return null;
            mpv_render_context_update(_render);
        }

        int w = Math.Max(16, req.Width & ~1), h = Math.Max(16, req.Height & ~1);
        int stride = ((w * 4) + 63) / 64 * 64;
        IntPtr buffer = Marshal.AllocHGlobal(stride * h + 64);
        IntPtr aligned = (IntPtr)(((long)buffer + 63) / 64 * 64);
        IntPtr size = Marshal.AllocHGlobal(8);
        IntPtr fmt = Marshal.StringToCoTaskMemUTF8("bgr0");
        IntPtr pstride = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteInt32(size, 0, w);
            Marshal.WriteInt32(size, 4, h);
            Marshal.WriteIntPtr(pstride, (IntPtr)stride);
            var p = new[]
            {
                new RenderParam { Type = RenderParamType.SwSize, Data = size },
                new RenderParam { Type = RenderParamType.SwFormat, Data = fmt },
                new RenderParam { Type = RenderParamType.SwStride, Data = pstride },
                new RenderParam { Type = RenderParamType.SwPointer, Data = aligned },
                new RenderParam { Type = RenderParamType.Invalid },
            };
            int r = mpv_render_context_render(_render, p);
            if (r < 0) { Log.Warn("thumbnail render: " + ErrorString(r)); return null; }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, aligned, stride * h, stride);
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(size);
            Marshal.FreeCoTaskMem(fmt);
            Marshal.FreeHGlobal(pstride);
        }
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        _thread.Join(1500);
        if (_render != IntPtr.Zero) { mpv_render_context_free(_render); _render = IntPtr.Zero; }
        if (_mpv != IntPtr.Zero) { mpv_terminate_destroy(_mpv); _mpv = IntPtr.Zero; }
    }
}
