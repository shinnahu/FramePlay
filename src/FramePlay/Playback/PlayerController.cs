using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Interop;
using FramePlay.Localization;
using FramePlay.Playback.Midi;
using FramePlay.Themes;

namespace FramePlay.Playback;

/// <summary>
/// Owns the playback engines (mpv for audio/video, MeltySynth for MIDI), the queue and everything
/// FramePlay remembers per file. Windows bind to its properties; all members run on the UI thread.
/// </summary>
public sealed class PlayerController : INotifyPropertyChanged, IDisposable
{
    private const ulong ScreenshotReply = 1001;

    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _midiClock;
    private readonly DispatcherTimer _saveTimer;
    private readonly Random _rng = new();
    private readonly List<QueueItem> _shuffleHistory = new();
    private MidiPlayer? _midi;
    private bool _fileReady;
    private bool _restoring;
    private string? _pendingScreenshot;
    private bool _handledEof;

    public AppSettings Settings { get; }
    public History History { get; }
    public MpvPlayer Mpv { get; }
    public ObservableCollection<QueueItem> Queue { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Short status messages ("Volume: 80%"). The window shows them over the video or in a toast.</summary>
    public event Action<string>? Toast;
    public event Action? TracksChanged;
    public event Action? VideoSizeChanged;
    /// <summary>A new item started (or playback stopped). The window switches between video, audio and idle views.</summary>
    public event Action? MediaChanged;
    /// <summary>mpv created its video window; the host disables its input so clicks reach FramePlay.</summary>
    public event Action? VideoOutputReady;

    public PlayerController(Dispatcher ui, AppSettings settings, History history)
    {
        _ui = ui;
        Settings = settings;
        History = history;
        Mpv = new MpvPlayer(ui);
        _volume = settings.Volume;
        _muted = settings.Muted;
        _shuffle = settings.Shuffle;

        _midiClock = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(100) };
        _midiClock.Tick += (_, _) => { if (_midi != null) Position = _midi.Position; };

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(5) };
        _saveTimer.Tick += (_, _) => SaveMemory(false);
        _saveTimer.Start();
    }

    // =============================================================================================
    // State
    // =============================================================================================
    private QueueItem? _current;
    public QueueItem? Current { get => _current; private set { if (_current != value) { if (_current != null) _current.IsCurrent = false; _current = value; if (value != null) value.IsCurrent = true; OnChanged(); } } }

    private bool _isIdle = true;
    public bool IsIdle { get => _isIdle; private set { if (Set(ref _isIdle, value)) OnChanged(nameof(IsPlaying)); } }

    private bool _isPaused = true;
    public bool IsPaused { get => _isPaused; private set { if (Set(ref _isPaused, value)) OnChanged(nameof(IsPlaying)); } }

    public bool IsPlaying => !_isIdle && !_isPaused;

    private double _position;
    public double Position { get => _position; private set => Set(ref _position, value); }

    private double _duration;
    public double Duration { get => _duration; private set => Set(ref _duration, value); }

    private double _volume;
    public double Volume { get => _volume; private set => Set(ref _volume, value); }

    private bool _muted;
    public bool Muted { get => _muted; private set => Set(ref _muted, value); }

    private double _speed = 1.0;
    public double Speed { get => _speed; private set => Set(ref _speed, value); }

    private bool _hasVideo;
    public bool HasVideo { get => _hasVideo; private set => Set(ref _hasVideo, value); }

    private bool _isMidi;
    public bool IsMidi { get => _isMidi; private set => Set(ref _isMidi, value); }

    private double _fps;
    public double Fps { get => _fps; private set => Set(ref _fps, value); }

    private int _videoWidth, _videoHeight;
    public int VideoWidth => _videoWidth;
    public int VideoHeight => _videoHeight;

    private string _title = "FramePlay";
    public string Title { get => _title; private set => Set(ref _title, value); }

    private string? _artist;
    public string? Artist { get => _artist; private set => Set(ref _artist, value); }

    private string? _album;
    public string? Album { get => _album; private set => Set(ref _album, value); }

    private double _subDelay, _audioDelay;
    public double SubtitleDelay { get => _subDelay; private set => Set(ref _subDelay, value); }
    public double AudioDelay { get => _audioDelay; private set => Set(ref _audioDelay, value); }

    private bool _shuffle;
    public bool Shuffle { get => _shuffle; set { if (Set(ref _shuffle, value)) { Settings.Shuffle = value; _shuffleHistory.Clear(); } } }

    private bool _eof;
    public bool EndReached { get => _eof; private set => Set(ref _eof, value); }

    public List<TrackInfo> Tracks { get; private set; } = new();
    public string AudioTrackId { get; private set; } = "no";
    public string SubtitleTrackId { get; private set; } = "no";
    public string SecondarySubtitleTrackId { get; private set; } = "no";

    public int CurrentIndex => _current == null ? -1 : Queue.IndexOf(_current);

    // =============================================================================================
    // Startup
    // =============================================================================================
    public void Initialize(IntPtr wid)
    {
        // mpv doesn't create its cache folders itself.
        Directory.CreateDirectory(Path.Combine(AppPaths.LocalDir, "cache", "shaders"));
        Directory.CreateDirectory(Path.Combine(AppPaths.LocalDir, "cache", "icc"));
        Mpv.Create();
        var s = Settings;
        var opts = new List<(string, string)>
        {
            ("wid", wid.ToInt64().ToString(CultureInfo.InvariantCulture)),
            ("config", "no"), ("load-scripts", "no"), ("ytdl", "no"), ("osc", "no"), ("terminal", "no"),
            ("input-default-bindings", "no"), ("input-vo-keyboard", "no"), ("input-cursor", "no"), ("cursor-autohide", "no"),
            ("idle", "yes"), ("keep-open", "yes"), ("force-window", "no"), ("audio-display", "no"),
            ("vo", "gpu-next"), ("gpu-api", "d3d11"), ("hwdec", "auto-safe"),
            // Compiled shaders are cached, so only the very first start pays for compiling them.
            ("gpu-shader-cache", "yes"), ("gpu-shader-cache-dir", Path.Combine(AppPaths.LocalDir, "cache", "shaders")),
            ("icc-cache-dir", Path.Combine(AppPaths.LocalDir, "cache", "icc")),
            // Exact seeks; frames before the target may be dropped without drawing them (mpv's default),
            // which makes stepping back much faster. Safe here: FramePlay adds no filters that change timing.
            ("hr-seek", "yes"), ("hr-seek-framedrop", "yes"),
            ("volume-max", "200"), ("volume", s.Volume.ToString(CultureInfo.InvariantCulture)), ("mute", s.Muted ? "yes" : "no"),
            ("audio-client-name", "FramePlay"), ("title", "FramePlay"),
            ("osd-level", "1"), ("osd-bar", "no"), ("osd-on-seek", "no"), ("osd-duration", "1300"),
            ("osd-font", "Segoe UI Semibold"), ("osd-font-size", "30"), ("osd-border-size", "1.5"), ("osd-margin-x", "22"), ("osd-margin-y", "18"),
            ("screenshot-format", "png"), ("screenshot-png-compression", "7"), ("screenshot-high-bit-depth", "yes"), ("screenshot-tag-colorspace", "yes"),
            ("sub-auto", "fuzzy"), ("sub-file-paths", "subs;Subs;subtitles;Subtitles"), ("demuxer-mkv-subtitle-preroll", "yes"),
            ("msg-level", "all=warn"),
        };
        if (!string.IsNullOrWhiteSpace(s.PreferredAudioLanguages)) opts.Add(("alang", s.PreferredAudioLanguages));
        if (!string.IsNullOrWhiteSpace(s.PreferredSubtitleLanguages)) opts.Add(("slang", s.PreferredSubtitleLanguages));
        foreach (var (k, v) in opts) Mpv.SetOption(k, v);
        // mpv's built-in helper scripts and its own drag handling aren't used; skipping them speeds up startup.
        foreach (var k in new[] { "load-stats-overlay", "load-console", "load-select", "load-positioning", "load-commands",
                                  "load-auto-profiles", "drag-and-drop", "input-builtin-dragging", "input-builtin-drag-and-drop" })
            Mpv.SetOption(k, "no");
        Mpv.SetOption("media-controls", "yes"); // Windows media overlay and lock-screen controls (newer mpv builds)
        // FRAMEPLAY_MPV_LOG=v (or debug) writes mpv's detailed log to logs\mpv.log for troubleshooting.
        Mpv.Initialize(Environment.GetEnvironmentVariable("FRAMEPLAY_MPV_LOG") ?? "warn");

        Mpv.Observe("pause", LibMpv.Format.Flag);
        Mpv.Observe("time-pos", LibMpv.Format.Double);
        Mpv.Observe("duration", LibMpv.Format.Double);
        Mpv.Observe("eof-reached", LibMpv.Format.Flag);
        Mpv.Observe("idle-active", LibMpv.Format.Flag);
        Mpv.Observe("volume", LibMpv.Format.Double);
        Mpv.Observe("mute", LibMpv.Format.Flag);
        Mpv.Observe("speed", LibMpv.Format.Double);
        Mpv.Observe("track-list", LibMpv.Format.None);
        Mpv.Observe("aid", LibMpv.Format.String);
        Mpv.Observe("sid", LibMpv.Format.String);
        Mpv.Observe("secondary-sid", LibMpv.Format.String);
        Mpv.Observe("sub-delay", LibMpv.Format.Double);
        Mpv.Observe("audio-delay", LibMpv.Format.Double);
        Mpv.Observe("media-title", LibMpv.Format.String);
        Mpv.Observe("container-fps", LibMpv.Format.Double);
        Mpv.Observe("estimated-vf-fps", LibMpv.Format.Double);
        Mpv.Observe("dwidth", LibMpv.Format.Int64);
        Mpv.Observe("dheight", LibMpv.Format.Int64);
        Mpv.Observe("vo-configured", LibMpv.Format.Flag);

        Mpv.PropertyChanged += OnMpvProperty;
        Mpv.FileLoaded += OnFileLoaded;
        Mpv.EndFile += OnEndFile;
        Mpv.VideoReconfig += () => VideoOutputReady?.Invoke();
        Mpv.PlaybackRestart += OnPlaybackRestart;
        Mpv.CommandReply += OnCommandReply;

        ApplySubtitleStyle();
    }

    // =============================================================================================
    // mpv → state
    // =============================================================================================
    private void OnMpvProperty(string name, object? value)
    {
        if (_isMidi && name is "pause" or "time-pos" or "duration" or "eof-reached" or "idle-active") return;
        switch (name)
        {
            case "pause": IsPaused = value is true; break;
            case "time-pos":
                if (value is not double t) break;
                if (_stepTarget < 0) { Position = t; break; }
                // Frame stepping: the display keeps showing the target; a frame-step has landed once its frame shows.
                if (_stepInFlight && _stepExpect >= 0 && TimeFormat.FrameIndex(t, Fps) >= _stepExpect) OnStepLanded();
                break;
            case "duration": Duration = value is double d ? d : 0; if (_current != null && Duration > 0) _current.Duration = Duration; break;
            case "eof-reached":
                EndReached = value is true;
                if (EndReached && !_handledEof) { _handledEof = true; OnEndReached(); }
                break;
            case "idle-active":
                if (value is true && !_isMidi) { IsIdle = true; Position = 0; Duration = 0; HasVideo = false; }
                else if (value is false) IsIdle = false;
                break;
            case "volume": if (value is double v && !_isMidi) Volume = v; break;
            case "mute": if (value is bool m && !_isMidi) Muted = m; break;
            case "speed": if (value is double sp) Speed = sp; break;
            case "track-list": RefreshTracks(); break;
            case "aid": AudioTrackId = value as string ?? "no"; RememberTracks(); TracksChanged?.Invoke(); break;
            case "sid": SubtitleTrackId = value as string ?? "no"; RememberTracks(); TracksChanged?.Invoke(); break;
            case "secondary-sid": SecondarySubtitleTrackId = value as string ?? "no"; RememberTracks(); TracksChanged?.Invoke(); break;
            case "sub-delay": if (value is double sd) { SubtitleDelay = sd; RememberDelays(); } break;
            case "audio-delay": if (value is double ad) { AudioDelay = ad; RememberDelays(); } break;
            case "media-title": if (value is string mt && !string.IsNullOrWhiteSpace(mt) && !_isMidi) UpdateTitle(mt); break;
            case "container-fps": if (value is double cf && cf > 0) Fps = cf; break;
            case "estimated-vf-fps": if (value is double ef && ef > 0 && Fps <= 0) Fps = ef; break;
            case "dwidth": _videoWidth = value is long w ? (int)w : 0; VideoSizeChanged?.Invoke(); break;
            case "dheight": _videoHeight = value is long h ? (int)h : 0; VideoSizeChanged?.Invoke(); break;
            case "vo-configured":
                if (value is true)
                {
                    Log.Timing("video output configured");
                    GpuWarmup.Release(); // mpv has its own Direct3D device now
                    VideoOutputReady?.Invoke();
                }
                break;
        }
    }

    private void RefreshTracks()
    {
        Tracks = Mpv.GetTracks();
        HasVideo = Tracks.Any(t => t.Type == "video" && !t.IsImage);
        TracksChanged?.Invoke();
    }

    private void UpdateTitle(string mediaTitle)
    {
        if (_current == null) return;
        var artist = Mpv.GetString("metadata/by-key/artist") ?? Mpv.GetString("metadata/by-key/ARTIST");
        var album = Mpv.GetString("metadata/by-key/album") ?? Mpv.GetString("metadata/by-key/ALBUM");
        Artist = string.IsNullOrWhiteSpace(artist) ? null : artist;
        Album = string.IsNullOrWhiteSpace(album) ? null : album;
        Title = mediaTitle;
        _current.Title = Artist != null && _current.Kind == MediaKind.Audio ? $"{Artist} - {mediaTitle}" : mediaTitle;
    }

    private static bool _loggedFirstFile;

    private void OnFileLoaded()
    {
        if (!_loggedFirstFile)
        {
            _loggedFirstFile = true;
            var since = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            Log.Info($"First file loaded {since.TotalMilliseconds:0} ms after the process started");
        }
        if (_current == null) return;
        _fileReady = false;
        _restoring = true;
        _handledEof = false;
        EndReached = false;
        Fps = Mpv.GetDouble("container-fps") ?? 0;
        RefreshTracks();

        var mem = History.Get(_current.Path);
        if (mem != null)
        {
            // External files the user added last time.
            foreach (var f in mem.ExternalSubtitles ?? new()) if (File.Exists(f)) Mpv.Command("sub-add", f, "auto");
            foreach (var f in mem.ExternalAudio ?? new()) if (File.Exists(f)) Mpv.Command("audio-add", f, "auto");
            if (mem.ExternalSubtitles?.Count > 0 || mem.ExternalAudio?.Count > 0) RefreshTracks();
            RestoreTrack("aid", mem.AudioTrack);
            RestoreTrack("sid", mem.SubtitleTrack);
            RestoreTrack("secondary-sid", mem.SecondarySubtitleTrack);
            Mpv.SetDouble("sub-delay", mem.SubtitleDelay);
            Mpv.SetDouble("audio-delay", mem.AudioDelay);
            double dur = Mpv.GetDouble("duration") ?? 0;
            if (Settings.ResumePosition && mem.Position > 5 && (dur <= 0 || mem.Position < dur - 10))
            {
                Mpv.Command("seek", mem.Position.ToString(CultureInfo.InvariantCulture), "absolute+exact");
                Toast?.Invoke(Loc.F("toast.resumed", TimeFormat.Clock(mem.Position, false)));
            }
        }
        else
        {
            Mpv.SetDouble("sub-delay", 0);
            Mpv.SetDouble("audio-delay", 0);
        }
        _restoring = false;
        _fileReady = true;
        var m = History.GetOrAdd(_current.Path);
        m.LastPlayed = DateTime.Now;
        m.Duration = Mpv.GetDouble("duration") ?? 0;
        MediaChanged?.Invoke();
    }

    /// <summary>Selects a remembered track. Internal tracks are stored by id, added files as "file:path".</summary>
    private void RestoreTrack(string prop, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return;
        if (stored.StartsWith("file:", StringComparison.Ordinal))
        {
            var path = stored[5..];
            var type = prop == "aid" ? "audio" : "sub";
            var t = Tracks.FirstOrDefault(x => x.Type == type && x.IsExternal && string.Equals(x.ExternalFilename, path, StringComparison.OrdinalIgnoreCase));
            if (t != null) Mpv.SetString(prop, t.Id.ToString(CultureInfo.InvariantCulture));
        }
        else Mpv.SetString(prop, stored);
    }

    private string StoredTrack(string type, string id)
    {
        if (id is "no" or "auto" or "") return id == "" ? "no" : id;
        var t = Tracks.FirstOrDefault(x => x.Type == type && x.Id.ToString(CultureInfo.InvariantCulture) == id);
        return t is { IsExternal: true, ExternalFilename: not null } ? "file:" + t.ExternalFilename : id;
    }

    private void RememberTracks()
    {
        if (!_fileReady || _restoring || _current == null || _isMidi) return;
        var m = History.GetOrAdd(_current.Path);
        m.AudioTrack = StoredTrack("audio", AudioTrackId);
        m.SubtitleTrack = StoredTrack("sub", SubtitleTrackId);
        m.SecondarySubtitleTrack = StoredTrack("sub", SecondarySubtitleTrackId);
    }

    private void RememberDelays()
    {
        if (!_fileReady || _restoring || _current == null || _isMidi) return;
        var m = History.GetOrAdd(_current.Path);
        m.SubtitleDelay = _subDelay;
        m.AudioDelay = _audioDelay;
    }

    private void OnEndFile(LibMpv.EndFileReason reason, int error)
    {
        if (reason == LibMpv.EndFileReason.Error && _current != null && !_isMidi)
        {
            Toast?.Invoke(Loc.F("toast.cantPlay", _current.FileName));
            Log.Warn($"mpv could not play {_current.Path}: {LibMpv.ErrorString(error)}");
            var failed = _current;
            if (HasNext()) Next(auto: true);
            else { Current = null; MediaChanged?.Invoke(); }
            _ = failed;
        }
    }

    private void OnCommandReply(ulong id, int error)
    {
        if (id != ScreenshotReply) return;
        var path = _pendingScreenshot;
        _pendingScreenshot = null;
        Toast?.Invoke(error >= 0 && path != null ? Loc.F("toast.screenshot", Path.GetFileName(path)) : Loc.T("toast.screenshotFailed"));
    }

    /// <summary>
    /// Keep the last frame on screen when the queue runs out instead of stopping. Picture in picture sets
    /// it, so the small window stays (paused on the end) rather than the player window coming back.
    /// </summary>
    public bool HoldLastFrameAtEnd { get; set; }

    /// <summary>End of the current item: pause on the last frame (if enabled for videos) or move on.</summary>
    private void OnEndReached()
    {
        if (_current == null) return;
        var mem = History.GetOrAdd(_current.Path);
        mem.Position = 0; // finished: start from the beginning next time
        if (HasVideo && (Settings.PauseOnLastFrame || (HoldLastFrameAtEnd && !HasNext())))
        {
            if (_isMidi) _midi?.Pause(); else Mpv.SetFlag("pause", true);
            IsPaused = true;
            return;
        }
        if (HasNext()) Next(auto: true);
        else Stop();
    }

    // =============================================================================================
    // Queue
    // =============================================================================================
    /// <summary>Adds files (folders are expanded). With playNow, playback jumps to the first new item.</summary>
    public void Enqueue(IEnumerable<string> paths, bool playNow, bool replace = false)
    {
        var files = MediaFormats.ExpandPaths(paths);
        if (files.Count == 0) return;
        // Subtitle files dropped on a playing video become subtitle tracks.
        var subs = files.Where(f => MediaFormats.KindOf(f) == MediaKind.Subtitle).ToList();
        if (subs.Count > 0 && _current != null && !_isMidi)
        {
            foreach (var s in subs) AddSubtitleFile(s);
            files = files.Except(subs).ToList();
            if (files.Count == 0) return;
        }
        if (replace)
        {
            SaveMemory(true);
            Queue.Clear();
            _shuffleHistory.Clear();
        }
        int first = Queue.Count;
        foreach (var f in files) Queue.Add(new QueueItem(f));
        if (playNow || _current == null) PlayIndex(first);
    }

    public bool HasNext() => Shuffle ? Queue.Count > 1 || (Queue.Count == 1 && _current == null) : CurrentIndex + 1 < Queue.Count;
    public bool HasPrevious() => Shuffle ? _shuffleHistory.Count > 0 : CurrentIndex > 0;

    public void PlayIndex(int index)
    {
        if (index < 0 || index >= Queue.Count) return;
        Play(Queue[index]);
    }

    public void Play(QueueItem item)
    {
        CancelStepping();
        SaveMemory(true);
        if (Shuffle && _current != null && _current != item) _shuffleHistory.Add(_current);
        Current = item;
        Title = item.Title;
        Artist = null;
        Album = null;
        EndReached = false;
        _handledEof = false;
        _fileReady = false;
        Position = 0;
        Duration = item.Duration;

        if (item.Kind == MediaKind.Midi)
        {
            StartMidi(item);
            return;
        }
        if (_isMidi) StopMidi();
        IsMidi = false;
        IsIdle = false;
        Fps = 0;
        HasVideo = item.Kind == MediaKind.Video;
        Mpv.SetFlag("pause", false);
        // Mirrored here: when mpv was already unpaused (after the end of the queue, or a MIDI file) no
        // "pause" change follows, and the player would think it is still paused.
        IsPaused = false;
        Mpv.CommandAsync(0, "loadfile", item.Path, "replace");
        Log.Timing("loadfile sent");
        MediaChanged?.Invoke();
    }

    public void Next(bool auto = false)
    {
        if (Queue.Count == 0) return;
        if (Shuffle)
        {
            var candidates = Queue.Where(q => q != _current && !_shuffleHistory.Contains(q)).ToList();
            if (candidates.Count == 0)
            {
                _shuffleHistory.Clear();
                candidates = Queue.Where(q => q != _current).ToList();
            }
            if (candidates.Count == 0) { if (!auto) Seek(0); return; }
            Play(candidates[_rng.Next(candidates.Count)]);
            return;
        }
        int i = CurrentIndex;
        if (i + 1 < Queue.Count) PlayIndex(i + 1);
    }

    public void Previous()
    {
        if (Shuffle && _shuffleHistory.Count > 0)
        {
            var prev = _shuffleHistory[^1];
            _shuffleHistory.RemoveAt(_shuffleHistory.Count - 1);
            var cur = _current;
            Current = null; // so Play() doesn't push the current item back onto the history
            Play(prev);
            _ = cur;
            return;
        }
        int i = CurrentIndex;
        if (i > 0) PlayIndex(i - 1);
        else Seek(0);
    }

    public void RemoveFromQueue(QueueItem item)
    {
        bool wasCurrent = item == _current;
        int idx = Queue.IndexOf(item);
        Queue.Remove(item);
        _shuffleHistory.Remove(item);
        if (wasCurrent)
        {
            if (Queue.Count == 0) Stop();
            else PlayIndex(Math.Min(idx, Queue.Count - 1));
        }
    }

    public void ClearQueue()
    {
        Stop();
        Queue.Clear();
        _shuffleHistory.Clear();
    }

    public void MoveInQueue(QueueItem item, int newIndex)
    {
        int old = Queue.IndexOf(item);
        if (old < 0) return;
        newIndex = Math.Clamp(newIndex, 0, Queue.Count - 1);
        if (old != newIndex) Queue.Move(old, newIndex);
    }

    // =============================================================================================
    // Transport
    // =============================================================================================
    public void PlayPause()
    {
        if (_current == null)
        {
            if (Queue.Count > 0) PlayIndex(0);
            return;
        }
        if (_isMidi)
        {
            if (_midi == null) return;
            if (_midi.IsPaused) { if (EndReached) { _midi.Seek(0); EndReached = false; _handledEof = false; } _midi.Play(); }
            else _midi.Pause();
            IsPaused = _midi.IsPaused;
            return;
        }
        if (IsIdle) { Play(_current); return; }
        CancelStepping();
        // mpv's own state decides, so play/pause always toggles what is really happening.
        bool paused = Mpv.GetFlag("pause") ?? IsPaused;
        if (EndReached && paused)
        {
            // Paused on the last frame: play again from the start.
            _handledEof = false;
            Mpv.Command("seek", "0", "absolute+exact");
        }
        Mpv.SetFlag("pause", !paused);
        IsPaused = !paused;
    }

    public void Pause()
    {
        if (_isMidi) { _midi?.Pause(); IsPaused = true; }
        else if (!IsIdle) Mpv.SetFlag("pause", true);
    }

    public void Stop()
    {
        CancelStepping();
        SaveMemory(true);
        if (_isMidi) StopMidi();
        Mpv.SetFlag("pause", true); // mpv agrees with the play button while idle
        Mpv.Command("stop");
        Current = null;
        IsIdle = true;
        IsPaused = true;
        Position = 0;
        Duration = 0;
        HasVideo = false;
        Title = "FramePlay";
        Artist = null;
        Album = null;
        Tracks = new();
        TracksChanged?.Invoke();
        MediaChanged?.Invoke();
    }

    /// <summary>Absolute seek. exact=false is used while dragging (keyframes, fast).</summary>
    public void Seek(double seconds, bool exact = true)
    {
        if (_current == null) return;
        CancelStepping();
        seconds = Math.Max(0, Duration > 0 ? Math.Min(seconds, Duration) : seconds);
        _handledEof = false;
        if (_isMidi) { _midi?.Seek(seconds); Position = seconds; EndReached = false; return; }
        Mpv.CommandAsync(0, "seek", seconds.ToString("0.######", CultureInfo.InvariantCulture), exact ? "absolute+exact" : "absolute+keyframes");
        Position = seconds;
    }

    /// <summary>Jumps to the last frame (not past it, so the queue doesn't advance), like End in Premiere.</summary>
    public void SeekToEnd()
    {
        if (_current == null || Duration <= 0) return;
        Seek(Math.Max(0, Duration - (Fps > 0 ? 1.0 / Fps : 0.05)));
    }

    // ---------------------------------------------------------------------------------------------
    // Jumps (arrow keys, J / L), YouTube style. Holding the key or pressing it again quickly adds the new
    // jump to where the previous one was heading instead of waiting for it to land, so the jumps add up
    // fast. While the key repeats, quick keyframe seeks show where it's going; one exact seek follows
    // once it's let go.
    // ---------------------------------------------------------------------------------------------
    private double _jumpTarget = double.NaN, _jumpTotal;
    private long _jumpAt;
    private DispatcherTimer? _jumpSettle;

    public void Jump(double seconds)
    {
        if (_current == null) return;
        long now = Environment.TickCount64;
        bool chained = !double.IsNaN(_jumpTarget) && now - _jumpAt < 600;
        double from = chained ? _jumpTarget : Position;
        if (!chained) _jumpTotal = 0;
        // Never past the last frame, so a jump doesn't end the item and move the queue on.
        double end = Duration > 0 ? Math.Max(0, Duration - (Fps > 0 ? 1.0 / Fps : 0.05)) : double.MaxValue;
        double target = Math.Clamp(from + seconds, 0, end);
        _jumpTotal += target - from;
        _jumpTarget = target;
        _jumpAt = now;

        Seek(target, exact: !chained || _isMidi);
        if (chained && !_isMidi)
        {
            if (_jumpSettle == null)
            {
                _jumpSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
                _jumpSettle.Tick += (_, _) =>
                {
                    _jumpSettle!.Stop();
                    if (_current != null && !double.IsNaN(_jumpTarget)) Seek(_jumpTarget, exact: true);
                };
            }
            _jumpSettle.Stop();
            _jumpSettle.Start();
        }
        string amount = Math.Abs(_jumpTotal).ToString("0.#", CultureInfo.CurrentCulture);
        Toast?.Invoke((_jumpTotal < 0 ? "−" : "+") + amount + " s");
    }

    public void SeekRelative(double delta)
    {
        if (_current == null) return;
        if (_isMidi) { Seek(Position + delta); return; }
        _handledEof = false;
        Mpv.CommandAsync(0, "seek", delta.ToString("0.###", CultureInfo.InvariantCulture), "relative+exact");
    }

    // ---------------------------------------------------------------------------------------------
    // Frame stepping, Premiere style. Every press moves a target frame: the time display shows it at once
    // and the picture follows as fast as decoding allows. Only one step or seek is in flight at a time,
    // and when it lands the next one goes straight to the target, so fast presses in both directions
    // always end on the exact frame (Left, Left, Right = one frame back).
    // Forward by a few frames: mpv's frame-step, which only decodes the next frame. Backwards, or far
    // ahead: one exact seek, which has to decode from the previous keyframe.
    // ---------------------------------------------------------------------------------------------
    private const int MaxFrameStepChain = 6;
    private long _stepTarget = -1;     // frame asked for; -1 when not stepping
    private bool _stepInFlight;
    private long _stepExpect = -1;     // frame a pending frame-step shows; -1 when a seek is pending
    private long _stepSeekedTo = -1;   // target of the last seek, so a frame that can't be reached isn't retried forever
    private long _stepSentAt;
    private DispatcherTimer? _stepWatchdog;

    /// <summary>Steps exactly <paramref name="frames"/> frames (negative = backwards) and pauses, like Premiere's arrow keys.</summary>
    public void StepFrames(int frames)
    {
        if (_current == null || frames == 0) return;
        if (_isMidi || !HasVideo)
        {
            // No frames in audio: step 1/30 s per "frame" so the keys still nudge the position.
            Pause();
            Seek(Position + frames / 30.0);
            return;
        }
        if (Fps <= 0)
        {
            for (int i = 0; i < Math.Abs(frames); i++) Mpv.CommandAsync(0, frames > 0 ? "frame-step" : "frame-back-step");
            return;
        }
        _handledEof = false;
        _jumpSettle?.Stop();
        if (!IsPaused) Mpv.SetFlag("pause", true);
        long from = _stepTarget >= 0 ? _stepTarget : CurrentFrame();
        _stepTarget = Math.Clamp(from + frames, 0, LastFrame);
        Position = _stepTarget / Fps; // the time display answers straight away
        if (!_stepInFlight) StepTowardTarget();
    }

    private long CurrentFrame() => TimeFormat.FrameIndex(Mpv.GetDouble("time-pos") ?? Position, Fps);

    private long LastFrame => Duration > 0 ? Math.Max(0, (long)Math.Ceiling(Duration * Fps - 0.5) - 1) : long.MaxValue;

    private void StepTowardTarget()
    {
        long current = CurrentFrame();
        long distance = _stepTarget - current;
        // There, or the last seek already went for this frame and landed elsewhere (variable frame rate).
        if (_stepTarget < 0 || distance == 0 || (_stepSeekedTo == _stepTarget && _stepExpect < 0 && _stepSentAt > 0))
        {
            FinishStepping();
            return;
        }
        _stepInFlight = true;
        _stepSentAt = Environment.TickCount64;
        if (distance is > 0 and <= MaxFrameStepChain)
        {
            _stepExpect = current + 1;
            _stepSeekedTo = -1;
            Mpv.CommandAsync(0, "frame-step");
        }
        else
        {
            _stepExpect = -1;
            _stepSeekedTo = _stepTarget;
            SeekToFrame(_stepTarget);
        }
        if (_stepWatchdog == null)
        {
            _stepWatchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _stepWatchdog.Tick += (_, _) => { _stepWatchdog!.Stop(); if (_stepInFlight) FinishStepping(); };
        }
        _stepWatchdog.Stop();
        _stepWatchdog.Start();
    }

    /// <summary>The pending frame-step showed its frame, or the pending seek landed: carry on toward the target.</summary>
    private void OnStepLanded()
    {
        if (!_stepInFlight) return;
        _stepInFlight = false;
        _stepWatchdog?.Stop();
        if (Log.TimingEnabled)
            Log.Info($"step {(_stepExpect >= 0 ? "frame-step" : "seek")} landed in {Environment.TickCount64 - _stepSentAt} ms");
        StepTowardTarget();
    }

    private void FinishStepping()
    {
        _stepInFlight = false;
        _stepTarget = -1;
        _stepExpect = -1;
        _stepSeekedTo = -1;
        _stepSentAt = 0;
        _stepWatchdog?.Stop();
        if (Mpv.GetDouble("time-pos") is double t) Position = t;
    }

    /// <summary>Any other seek, play or new file ends frame stepping.</summary>
    private void CancelStepping()
    {
        if (_stepTarget < 0 && !_stepInFlight) return;
        _stepInFlight = false;
        _stepTarget = -1;
        _stepExpect = -1;
        _stepSeekedTo = -1;
        _stepSentAt = 0;
        _stepWatchdog?.Stop();
    }

    /// <summary>Pauses on an exact frame. The target sits a quarter frame early so rounding can't land on the next one.</summary>
    private void SeekToFrame(long index)
    {
        if (Fps <= 0) return;
        index = Math.Max(0, index);
        Mpv.SetFlag("pause", true);
        double t = Math.Max(0, (index - 0.25) / Fps);
        Mpv.CommandAsync(0, "seek", t.ToString("0.######", CultureInfo.InvariantCulture), "absolute+exact");
    }

    private void OnPlaybackRestart()
    {
        if (_stepInFlight && _stepExpect < 0) OnStepLanded();
    }

    public void SetVolume(double v)
    {
        v = Math.Clamp(Math.Round(v), 0, 200);
        Volume = v;
        Settings.Volume = v;
        if (_isMidi) _midi?.SetVolume(Muted ? 0 : v / 100.0);
        Mpv.SetDouble("volume", v);
        if (Muted && v > 0) SetMuted(false);
    }

    public void SetMuted(bool muted)
    {
        Muted = muted;
        Settings.Muted = muted;
        if (_isMidi) _midi?.SetVolume(muted ? 0 : Volume / 100.0);
        Mpv.SetFlag("mute", muted);
    }

    public void SetSpeed(double s)
    {
        s = Math.Clamp(s, 0.1, 4.0);
        Speed = s;
        Mpv.SetDouble("speed", s);
        _midi?.SetSpeed(s);
    }

    // =============================================================================================
    // Tracks and delays
    // =============================================================================================
    public void SelectAudioTrack(long? id) => Mpv.SetString("aid", id?.ToString(CultureInfo.InvariantCulture) ?? "no");
    public void SelectSubtitleTrack(long? id) => Mpv.SetString("sid", id?.ToString(CultureInfo.InvariantCulture) ?? "no");
    public void SelectSecondarySubtitleTrack(long? id) => Mpv.SetString("secondary-sid", id?.ToString(CultureInfo.InvariantCulture) ?? "no");

    public void SetSubtitleDelay(double seconds) { Mpv.SetDouble("sub-delay", Math.Round(seconds, 3)); }
    public void SetAudioDelay(double seconds) { Mpv.SetDouble("audio-delay", Math.Round(seconds, 3)); }

    public void AdjustSubtitleDelay(double delta)
    {
        SetSubtitleDelay(SubtitleDelay + delta);
        Toast?.Invoke(Loc.F("toast.subDelay", TimeFormat.Delay(Math.Round(SubtitleDelay + delta, 3))));
    }

    public void AdjustAudioDelay(double delta)
    {
        SetAudioDelay(AudioDelay + delta);
        Toast?.Invoke(Loc.F("toast.audioDelay", TimeFormat.Delay(Math.Round(AudioDelay + delta, 3))));
    }

    public void AddSubtitleFile(string path)
    {
        if (_current == null || _isMidi) return;
        Mpv.Command("sub-add", path, "select");
        var m = History.GetOrAdd(_current.Path);
        m.ExternalSubtitles ??= new();
        if (!m.ExternalSubtitles.Contains(path, StringComparer.OrdinalIgnoreCase)) m.ExternalSubtitles.Add(path);
        Toast?.Invoke(Loc.F("toast.subtitleAdded", Path.GetFileName(path)));
    }

    public void AddAudioFile(string path)
    {
        if (_current == null || _isMidi) return;
        Mpv.Command("audio-add", path, "select");
        var m = History.GetOrAdd(_current.Path);
        m.ExternalAudio ??= new();
        if (!m.ExternalAudio.Contains(path, StringComparer.OrdinalIgnoreCase)) m.ExternalAudio.Add(path);
        Toast?.Invoke(Loc.F("toast.audioAdded", Path.GetFileName(path)));
    }

    // ---------------------------------------------------------------------------------------------
    // Subtitle position. While the controls cover the bottom of the picture (full screen and picture
    // in picture), the subtitles slide up above them and come back down when the controls hide.
    // ---------------------------------------------------------------------------------------------
    private double _subPosLimit = 100;
    private double _subPosShown = double.NaN;
    private bool _pipSubScale;
    private DispatcherTimer? _subTween;
    private double _tweenFrom, _tweenTo;
    private long _tweenStart;

    private double TargetSubPos => Math.Min(Math.Clamp(Settings.Subtitles.Position, 0, 100), _subPosLimit);

    /// <summary>Lowest the subtitles may sit (mpv sub-pos, 0 top … 100 bottom); 100 means no limit.</summary>
    public void SetSubtitleLimit(double limit, bool animate = true)
    {
        _subPosLimit = Math.Clamp(limit, 0, 100);
        double target = TargetSubPos;
        if (!animate || double.IsNaN(_subPosShown) || Math.Abs(target - _subPosShown) < 0.05)
        {
            _subTween?.Stop();
            SetSubPos(target);
            return;
        }
        _tweenFrom = _subPosShown;
        _tweenTo = target;
        _tweenStart = Environment.TickCount64;
        if (_subTween == null)
        {
            _subTween = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            _subTween.Tick += (_, _) =>
            {
                double t = Math.Min(1, (Environment.TickCount64 - _tweenStart) / 160.0);
                double eased = 1 - Math.Pow(1 - t, 3);
                SetSubPos(_tweenFrom + (_tweenTo - _tweenFrom) * eased);
                if (t >= 1) _subTween!.Stop();
            };
        }
        _subTween.Start();
    }

    /// <summary>Sets Settings › Subtitles › Position (0 top … 100 bottom) and applies it straight away.</summary>
    public void SetSubtitlePosition(double position)
    {
        Settings.Subtitles.Position = Math.Clamp(Math.Round(position), 0, 100);
        _subTween?.Stop();
        SetSubPos(TargetSubPos);
    }

    /// <summary>Picture in picture makes subtitles bigger (Settings › Subtitles › Size in picture in picture).</summary>
    public void SetPipSubtitleScale(bool pip)
    {
        _pipSubScale = pip;
        ApplySubScale();
    }

    private void ApplySubScale()
    {
        double scale = _pipSubScale ? Math.Clamp(Settings.Subtitles.PipScale, 0.5, 3) : 1;
        Mpv.SetString("sub-scale", scale.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private void SetSubPos(double pos)
    {
        _subPosShown = pos;
        // sub-pos takes decimals in current mpv; older builds only take whole numbers.
        if (!Mpv.SetString("sub-pos", pos.ToString("0.##", CultureInfo.InvariantCulture)))
            Mpv.SetString("sub-pos", Math.Round(pos).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Applies Settings › Subtitles to mpv. Option names changed in mpv 0.38, so both spellings are tried.</summary>
    public void ApplySubtitleStyle()
    {
        var st = Settings.Subtitles;
        string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        _subTween?.Stop();
        SetSubPos(TargetSubPos);
        ApplySubScale();
        Mpv.SetString("sub-font", st.Font);
        Mpv.SetString("sub-font-size", Num(st.FontSize));
        if (!Mpv.SetString("sub-outline-size", Num(st.OutlineSize))) Mpv.SetString("sub-border-size", Num(st.OutlineSize));
        Mpv.SetString("sub-color", ColorUtil.ToMpv(ColorUtil.Parse(st.TextColor, System.Windows.Media.Colors.White)));
        var outline = ColorUtil.ToMpv(ColorUtil.Parse(st.OutlineColor, System.Windows.Media.Colors.Black));
        if (!Mpv.SetString("sub-outline-color", outline)) Mpv.SetString("sub-border-color", outline);
        Mpv.SetString("sub-shadow-offset", st.Shadow ? Num(st.ShadowOffset) : "0");
        Mpv.SetString("sub-shadow-color", ColorUtil.ToMpv(ColorUtil.Parse(st.ShadowColor, System.Windows.Media.Color.FromArgb(0x80, 0, 0, 0))));
        Mpv.SetString("sub-back-color", ColorUtil.ToMpv(ColorUtil.Parse(st.BackgroundColor, System.Windows.Media.Color.FromArgb(0x99, 0, 0, 0))));
        if (st.Background)
        {
            if (!Mpv.SetString("sub-border-style", "background-box")) Mpv.SetString("sub-border-style", "opaque-box");
        }
        else Mpv.SetString("sub-border-style", "outline-and-shadow");
        Mpv.SetString("sub-codepage", string.IsNullOrWhiteSpace(st.Encoding) ? "auto" : st.Encoding);
        Mpv.SetString("sub-ass-override", st.OverrideAss ? "force" : "scale");
        // The second subtitle sits at the top of the picture, styled the same way.
        Mpv.SetString("secondary-sub-pos", "8");
    }

    // =============================================================================================
    // Screenshots
    // =============================================================================================
    public string ScreenshotFolder => string.IsNullOrWhiteSpace(Settings.ScreenshotFolder) ? AppPaths.DefaultScreenshotDir : Settings.ScreenshotFolder;

    public void TakeScreenshot(bool withSubtitles)
    {
        if (_current == null || !HasVideo || _isMidi) return;
        try
        {
            Directory.CreateDirectory(ScreenshotFolder);
            var stamp = TimeFormat.Format(Position, TimeDisplayMode.Timecode, Fps > 0 ? Fps : 30, true).Replace(':', '-');
            var baseName = SafeName(Path.GetFileNameWithoutExtension(_current.Path)) + " " + stamp;
            var path = Path.Combine(ScreenshotFolder, baseName + ".png");
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(ScreenshotFolder, $"{baseName} ({n}).png");
            _pendingScreenshot = path;
            Mpv.CommandAsync(ScreenshotReply, "screenshot-to-file", path, withSubtitles ? "subtitles" : "video");
        }
        catch (Exception ex)
        {
            Log.Error("Screenshot", ex);
            Toast?.Invoke(Loc.T("toast.screenshotFailed"));
        }
    }

    private static string SafeName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length > 120 ? s[..120] : s;
    }

    // =============================================================================================
    // MIDI
    // =============================================================================================
    /// <summary>Folder for extra soundfonts when FramePlay is installed somewhere read-only (like Program Files).</summary>
    public static string UserSoundfontDir => Path.Combine(AppPaths.ConfigDir, "soundfonts");

    /// <summary>Every .sf2 next to FramePlay.exe, plus any in the user soundfont folder, by file name.</summary>
    public static List<(string Name, string Path)> AvailableSoundfonts()
    {
        var list = new List<(string, string)>();
        foreach (var dir in new[] { AppPaths.AppDir, UserSoundfontDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.sf2"))
            {
                var name = Path.GetFileName(f);
                if (!list.Any(x => string.Equals(x.Item1, name, StringComparison.OrdinalIgnoreCase))) list.Add((name, f));
            }
        }
        return list.OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string? SoundfontPath()
    {
        var all = AvailableSoundfonts();
        var match = all.FirstOrDefault(x => string.Equals(x.Name, Settings.Soundfont, StringComparison.OrdinalIgnoreCase));
        if (match.Path != null) return match.Path;
        return all.Count > 0 ? all[0].Path : null;
    }

    private void StartMidi(QueueItem item)
    {
        Mpv.Command("stop");
        var sf = SoundfontPath();
        if (sf == null)
        {
            Toast?.Invoke(Loc.T("toast.noSoundfont"));
            IsIdle = true;
            MediaChanged?.Invoke();
            return;
        }
        try
        {
            _midi ??= new MidiPlayer(_ui);
            _midi.Ended -= OnMidiEnded;
            _midi.Ended += OnMidiEnded;
            var mem = History.Get(item.Path);
            double start = Settings.ResumePosition && mem != null && mem.Position > 5 ? mem.Position : 0;
            _midi.Open(item.Path, sf, Muted ? 0 : Volume / 100.0, Speed, start);
            IsMidi = true;
            IsIdle = false;
            IsPaused = false;
            HasVideo = false;
            Fps = 0;
            Duration = _midi.Duration;
            item.Duration = _midi.Duration;
            Position = start;
            if (_midi.Title != null) { Title = _midi.Title; item.Title = _midi.Title; }
            Tracks = new();
            TracksChanged?.Invoke();
            _fileReady = true;
            var m = History.GetOrAdd(item.Path);
            m.LastPlayed = DateTime.Now;
            m.Duration = _midi.Duration;
            _midiClock.Start();
            MediaChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("MIDI " + item.Path, ex);
            Toast?.Invoke(Loc.F("toast.cantPlay", item.FileName));
            IsMidi = false;
            if (HasNext()) Next(auto: true);
        }
    }

    private void OnMidiEnded()
    {
        EndReached = true;
        IsPaused = true;
        if (!_handledEof) { _handledEof = true; OnEndReached(); }
    }

    private void StopMidi()
    {
        _midiClock.Stop();
        _midi?.Close();
        IsMidi = false;
    }

    /// <summary>Reloads the current MIDI file with a newly chosen soundfont, keeping the position.</summary>
    public void ReloadSoundfont()
    {
        if (!_isMidi || _current == null || _midi == null) return;
        double pos = _midi.Position;
        bool paused = _midi.IsPaused;
        var sf = SoundfontPath();
        if (sf == null) return;
        _midi.Open(_current.Path, sf, Muted ? 0 : Volume / 100.0, Speed, pos);
        if (paused) _midi.Pause();
    }

    // =============================================================================================
    // Memory
    // =============================================================================================
    private void SaveMemory(bool flush)
    {
        if (_current != null && _fileReady && !EndReached)
        {
            var m = History.GetOrAdd(_current.Path);
            m.Position = Position;
            if (Duration > 0) m.Duration = Duration;
        }
        if (flush) History.Save();
    }

    public void Dispose()
    {
        SaveMemory(true);
        _saveTimer.Stop();
        _midiClock.Stop();
        _midi?.Dispose();
        Mpv.Dispose();
    }

    // =============================================================================================
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnChanged(name);
        return true;
    }

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
