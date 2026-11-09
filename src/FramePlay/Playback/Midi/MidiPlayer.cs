using System.IO;
using System.Windows.Threading;
using FramePlay.Core;
using MeltySynth;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FramePlay.Playback.Midi;

/// <summary>
/// Plays MIDI files through a SoundFont with MeltySynth, sample-accurately, out of the default
/// audio device via WASAPI. Seeking resets the synth and re-applies instruments and controllers.
/// </summary>
public sealed class MidiPlayer : IDisposable
{
    private static SoundFont? _cachedFont;
    private static string? _cachedFontPath;

    private readonly Dispatcher _ui;
    private WasapiOut? _out;
    private Renderer? _renderer;

    public event Action? Ended;

    public MidiPlayer(Dispatcher ui) => _ui = ui;

    public double Duration => _renderer?.Duration ?? 0;
    public double Position => _renderer?.Position ?? 0;
    public bool IsPaused => _renderer?.Paused ?? true;
    public bool IsOpen => _renderer != null;
    public string? Title { get; private set; }

    public static SoundFont LoadSoundFont(string path)
    {
        lock (typeof(MidiPlayer))
        {
            if (_cachedFont != null && string.Equals(_cachedFontPath, path, StringComparison.OrdinalIgnoreCase)) return _cachedFont;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var font = new SoundFont(path);
            Log.Info($"Soundfont loaded in {sw.ElapsedMilliseconds} ms: {path}");
            _cachedFont = font;
            _cachedFontPath = path;
            return font;
        }
    }

    /// <summary>Drops the cached soundfont (about 100 MB for the default one) when MIDI is no longer playing.</summary>
    public static void ReleaseSoundFont()
    {
        lock (typeof(MidiPlayer))
        {
            _cachedFont = null;
            _cachedFontPath = null;
        }
    }

    public void Open(string midiPath, string soundfontPath, double volume, double speed, double startAt)
    {
        Close();
        var song = SmfFile.Load(midiPath);
        Title = song.Title;
        var font = LoadSoundFont(soundfontPath);

        int sampleRate = 48000;
        try
        {
            using var en = new MMDeviceEnumerator();
            using var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            sampleRate = dev.AudioClient.MixFormat.SampleRate;
        }
        catch (Exception ex) { Log.Error("MIDI mix format", ex); }

        var settings = new SynthesizerSettings(sampleRate) { EnableReverbAndChorus = true, MaximumPolyphony = 128 };
        var synth = new Synthesizer(font, settings);
        _renderer = new Renderer(synth, song, sampleRate)
        {
            Volume = (float)volume,
            Speed = speed,
        };
        _renderer.Finished += () => _ui.BeginInvoke(() => Ended?.Invoke());
        if (startAt > 0) _renderer.Seek(startAt);

        _out = new WasapiOut(AudioClientShareMode.Shared, true, 60);
        _out.Init(_renderer);
        _out.Play();
    }

    public void Play() { if (_renderer != null) _renderer.Paused = false; }
    public void Pause() { if (_renderer != null) _renderer.Paused = true; }
    public void Seek(double seconds) => _renderer?.Seek(seconds);
    public void SetVolume(double v) { if (_renderer != null) _renderer.Volume = (float)v; }
    public void SetSpeed(double s) { if (_renderer != null) _renderer.Speed = s; }

    public void Close()
    {
        try { _out?.Stop(); } catch { }
        _out?.Dispose();
        _out = null;
        _renderer = null;
    }

    public void Dispose() => Close();

    /// <summary>The audio callback: renders the synth between MIDI events so every event lands on its exact sample.</summary>
    private sealed class Renderer : ISampleProvider
    {
        private readonly object _lock = new();
        private readonly Synthesizer _synth;
        private readonly SmfFile _song;
        private readonly int _rate;
        private float[] _left = new float[4096];
        private float[] _right = new float[4096];
        private int _next;
        private double _time;
        private bool _finished;
        private float _peak;
        private long _peakLogged;

        /// <summary>FRAMEPLAY_DEBUG_AUDIO=1 logs the output level once a second (for testing without speakers).</summary>
        private static readonly bool DebugLevels = Environment.GetEnvironmentVariable("FRAMEPLAY_DEBUG_AUDIO") == "1";

        public event Action? Finished;

        public Renderer(Synthesizer synth, SmfFile song, int rate)
        {
            _synth = synth;
            _song = song;
            _rate = rate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
        }

        public WaveFormat WaveFormat { get; }
        public double Duration => _song.Duration;
        public double Position { get { lock (_lock) return Math.Min(_time, _song.Duration); } }
        public bool Paused { get; set; }
        public float Volume { get; set; } = 1f;
        public double Speed { get; set; } = 1.0;

        public void Seek(double seconds)
        {
            lock (_lock)
            {
                seconds = Math.Clamp(seconds, 0, _song.Duration);
                _synth.Reset();
                // Chase: re-apply the last instrument, controllers and pitch bend before the new position.
                int i = 0;
                var events = _song.Events;
                for (; i < events.Count && events[i].Time < seconds; i++)
                {
                    var e = events[i];
                    if (e.Command is 0xB0 or 0xC0 or 0xE0)
                        _synth.ProcessMidiMessage(e.Channel, e.Command, e.Data1, e.Data2);
                }
                _next = i;
                _time = seconds;
                _finished = false;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            if (_left.Length < frames) { _left = new float[frames]; _right = new float[frames]; }
            bool raiseFinished = false;
            lock (_lock)
            {
                if (Paused || _finished)
                {
                    // While paused keep rendering silence-free release tails only when not paused.
                    Array.Clear(buffer, offset, count);
                    return count;
                }
                var events = _song.Events;
                double speed = Math.Clamp(Speed, 0.1, 4.0);
                int done = 0;
                while (done < frames)
                {
                    while (_next < events.Count && events[_next].Time <= _time)
                    {
                        var e = events[_next++];
                        _synth.ProcessMidiMessage(e.Channel, e.Command, e.Data1, e.Data2);
                    }
                    int chunk = frames - done;
                    if (_next < events.Count)
                    {
                        double until = (events[_next].Time - _time) * _rate / speed;
                        chunk = (int)Math.Clamp(Math.Ceiling(until), 1, chunk);
                    }
                    _synth.Render(_left.AsSpan(done, chunk), _right.AsSpan(done, chunk));
                    _time += chunk * speed / _rate;
                    done += chunk;
                }
                float vol = Volume;
                float peak = 0;
                for (int i = 0, o = offset; i < frames; i++)
                {
                    float l = _left[i] * vol, r = _right[i] * vol;
                    buffer[o++] = l;
                    buffer[o++] = r;
                    if (DebugLevels) peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
                }
                if (DebugLevels)
                {
                    _peak = Math.Max(_peak, peak);
                    if (Environment.TickCount64 - _peakLogged > 1000)
                    {
                        Log.Info($"MIDI output peak {_peak:0.000} at {_time:0.0}s");
                        _peak = 0;
                        _peakLogged = Environment.TickCount64;
                    }
                }
                // Let the last notes ring for a moment, then report the end.
                if (_next >= events.Count && _time >= _song.Duration + 1.0)
                {
                    _finished = true;
                    raiseFinished = true;
                }
            }
            if (raiseFinished) Finished?.Invoke();
            return count;
        }
    }
}
