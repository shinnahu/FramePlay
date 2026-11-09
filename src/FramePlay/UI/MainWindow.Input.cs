using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FramePlay.Interop;
using FramePlay.Localization;

namespace FramePlay.UI;

public partial class MainWindow
{
    private static readonly double[] Speeds = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 3.0, 4.0];
    private DateTime _lastVideoClickToggle = DateTime.MinValue;
    private string _lastSubtitleId = "auto";

    private void WireVideoInput()
    {
        Video.LeftButtonDown += OnVideoLeftDown;
        Video.LeftButtonUp += OnVideoLeftUp;
        Video.LeftDoubleClick += OnVideoDoubleClick;
        Video.RightButtonUp += _ => ShowVideoMenu();
        Video.Wheel += delta => ChangeVolume(delta > 0 ? 5 : -5);
        Video.MouseMoved += _ => OnVideoMouseActivity();
    }

    private bool _pipDragMoved;

    private void OnVideoLeftDown(Point p)
    {
        _clickActivated = (DateTime.UtcNow - _activatedAt).TotalMilliseconds < 250;
        if (!_pip) return;
        // Picture in picture: drag anywhere on the video to move the window.
        Win32.GetCursorPos(out var before);
        Win32.ReleaseCapture();
        Win32.SendMessage(_hwnd, Win32.WM_NCLBUTTONDOWN, (IntPtr)Win32.HTCAPTION, IntPtr.Zero);
        Win32.GetCursorPos(out var after);
        _pipDragMoved = Math.Abs(after.X - before.X) > 3 || Math.Abs(after.Y - before.Y) > 3;
        _overlay?.Poke();
    }

    private DateTime _activatedAt = DateTime.MinValue;
    private bool _clickActivated;

    private void OnVideoLeftUp(Point p)
    {
        if (_pip) return;
        // A click that only brought FramePlay to the front shouldn't pause the video.
        if (_clickActivated) { _clickActivated = false; return; }
        // YouTube style: a click pauses or plays.
        Player.PlayPause();
        _lastVideoClickToggle = DateTime.UtcNow;
    }

    private void OnVideoDoubleClick(Point p)
    {
        if (_pip)
        {
            if (!_pipDragMoved) ExitPip();
            return;
        }
        ToggleFullscreen();
        // The first click of the double-click already toggled playback; undo that.
        if ((DateTime.UtcNow - _lastVideoClickToggle).TotalMilliseconds < Win32.GetDoubleClickTime() + 100) Player.PlayPause();
    }

    private void OnVideoMouseActivity() => _overlay?.Poke();

    private void ChangeVolume(double delta)
    {
        Player.SetVolume(Player.Volume + delta);
        ShowToast(Loc.F("toast.volume", Player.Volume.ToString("0")));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || Keyboard.FocusedElement is TextBox) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed) key = e.ImeProcessedKey;
        var id = _hotkeys.Lookup(key, Keyboard.Modifiers);
        if (id != null && RunAction(id)) e.Handled = true;
    }

    /// <summary>Runs a hotkey action. Returns false when it doesn't apply right now (so the key works normally).</summary>
    public bool RunAction(string id)
    {
        switch (id)
        {
            case "play_pause": Player.PlayPause(); break;
            case "stop": Player.Stop(); break;
            case "next": Player.Next(); break;
            case "previous": Player.Previous(); break;
            case "frame_forward": Player.StepFrames(1); break;
            case "frame_back": Player.StepFrames(-1); break;
            case "frames_forward_5": Player.StepFrames(5); break;
            case "frames_back_5": Player.StepFrames(-5); break;
            case "seek_forward_5": Player.SeekRelative(5); break;
            case "seek_back_5": Player.SeekRelative(-5); break;
            case "seek_forward_10": Player.SeekRelative(10); break;
            case "seek_back_10": Player.SeekRelative(-10); break;
            case "go_start": Player.Seek(0); break;
            case "go_end": Player.SeekToEnd(); break;
            case "volume_up": ChangeVolume(5); break;
            case "volume_down": ChangeVolume(-5); break;
            case "mute":
                Player.SetMuted(!Player.Muted);
                ShowToast(Player.Muted ? Loc.T("toast.muted") : Loc.F("toast.volume", Player.Volume.ToString("0")));
                break;
            case "speed_up": StepSpeed(+1); break;
            case "speed_down": StepSpeed(-1); break;
            case "speed_reset": Player.SetSpeed(1); ShowToast(Loc.F("toast.speed", "1")); break;
            case "fullscreen": ToggleFullscreen(); break;
            case "exit_fullscreen":
                if (_fullscreen) ExitFullscreen();
                else if (_pip) ExitPip();
                else return false;
                break;
            case "pip": TogglePip(); break;
            case "on_top": ToggleManualTopmost(); break;
            case "queue": ToggleQueue(); break;
            case "random": Player.Shuffle = !Player.Shuffle; break;
            case "subtitles_toggle": ToggleSubtitles(); break;
            case "sub_delay_minus": Player.AdjustSubtitleDelay(-0.05); break;
            case "sub_delay_plus": Player.AdjustSubtitleDelay(0.05); break;
            // No overlay poke for these: showing the controls would lift the subtitles being placed.
            case "sub_pos_up": MoveSubtitles(-1, toast: true); return true;
            case "sub_pos_down": MoveSubtitles(+1, toast: true); return true;
            case "audio_delay_minus": Player.AdjustAudioDelay(-0.05); break;
            case "audio_delay_plus": Player.AdjustAudioDelay(0.05); break;
            case "screenshot": Player.TakeScreenshot(_settings.ScreenshotWithSubtitles); break;
            case "open_file": OpenFiles(); break;
            case "open_folder": OpenFolder(); break;
            case "settings": ShowSettings(); break;
            default:
                if (id.StartsWith("percent_", StringComparison.Ordinal) && int.TryParse(id[8..], out var pct) && Player.Duration > 0)
                {
                    Player.Seek(Player.Duration * pct / 10.0);
                    break;
                }
                return false;
        }
        _overlay?.Poke();
        return true;
    }

    /// <summary>Moves the subtitles up (negative) or down (positive) and keeps it as the Settings › Subtitles height.</summary>
    private void MoveSubtitles(double delta, bool toast)
    {
        Player.SetSubtitlePosition(_settings.Subtitles.Position + delta);
        if (toast) ShowToast(Loc.F("toast.subHeight", SubtitleHeightText()));
        SaveSettingsSoon();
    }

    /// <summary>The subtitle position as people think of it: height above the bottom of the picture, in %.</summary>
    private string SubtitleHeightText() => (100 - _settings.Subtitles.Position).ToString("0", CultureInfo.CurrentCulture) + "%";

    private void StepSpeed(int dir)
    {
        double cur = Player.Speed;
        double next = dir > 0
            ? Speeds.FirstOrDefault(s => s > cur + 0.001, Speeds[^1])
            : Speeds.LastOrDefault(s => s < cur - 0.001, Speeds[0]);
        Player.SetSpeed(next);
        ShowToast(Loc.F("toast.speed", next.ToString("0.##")));
    }

    private void ToggleSubtitles()
    {
        if (Player.SubtitleTrackId is not ("no" or ""))
        {
            _lastSubtitleId = Player.SubtitleTrackId;
            Player.Mpv.SetString("sid", "no");
        }
        else
        {
            var first = Player.Tracks.FirstOrDefault(t => t.Type == "sub");
            Player.Mpv.SetString("sid", _lastSubtitleId is "auto" or "no" ? first?.Id.ToString() ?? "auto" : _lastSubtitleId);
        }
    }

    private void ToggleManualTopmost()
    {
        _manualTopmost = !_manualTopmost;
        UpdateTopmost();
        ShowToast(Loc.T(_manualTopmost ? "toast.onTopOn" : "toast.onTopOff"));
    }
}
