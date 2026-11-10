using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FramePlay.Localization;
using FramePlay.Playback;
using FramePlay.UI.Controls;

namespace FramePlay.UI;

/// <summary>What the control bar needs from the window it sits in (the main window or the fullscreen overlay).</summary>
public interface IPlayerHost
{
    PlayerController Player { get; }
    bool IsFullscreen { get; }
    bool IsQueueOpen { get; }
    void ToggleFullscreen();
    void TogglePip();
    void ToggleQueue();
    void ShowAudioMenu(FrameworkElement anchor);
    void ShowSubtitlesMenu(FrameworkElement anchor);
    void ShowSpeedMenu(FrameworkElement anchor);
    void ShowSeekPreview(SeekBar bar, double seconds, double x);
    void HideSeekPreview();
}

public partial class ControlBar : UserControl
{
    private IPlayerHost? _host;
    private PlayerController? _player;
    private TimeDisplayMode _mode;

    public ControlBar()
    {
        InitializeComponent();
        Loc.I.Changed += () => { UpdatePlayIcon(); UpdateLoop(); };
        // In a narrow window the volume slider gives its room to the seek bar; the mute button,
        // Up/Down and the mouse wheel over the video still set the volume.
        SizeChanged += (_, e) => VolumeControl.Visibility = e.NewSize.Width < NarrowWidth ? Visibility.Collapsed : Visibility.Visible;
    }

    private const double NarrowWidth = 900;

    public void Attach(IPlayerHost host)
    {
        _host = host;
        _player = host.Player;
        _player.PropertyChanged += OnPlayerChanged;
        Seek.SeekRequested += (t, exact) => _player.Seek(t, exact);
        Seek.HoverChanged += (t, x) => _host.ShowSeekPreview(Seek, t, x);
        Seek.HoverEnded += () => _host.HideSeekPreview();
        VolumeControl.ValueRequested += v => _player.SetVolume(v);
        _mode = TimeFormat.Parse(_player.Settings.TimeDisplay);
        RefreshAll();
    }

    /// <summary>Transparent background for the fullscreen overlay variant.</summary>
    public Brush BarBackground { set => BarRoot.Background = value; }

    public void RefreshAll()
    {
        if (_player == null) return;
        Seek.Maximum = _player.Duration;
        Seek.Value = _player.Position;
        RandomButton.IsChecked = _player.Shuffle;
        UpdateLoop();
        QueueButton.IsChecked = _host?.IsQueueOpen ?? false;
        SetFullscreenState(_host?.IsFullscreen ?? false);
        UpdatePlayIcon();
        UpdateVolume();
        UpdateTimes();
        UpdateButtons();
    }

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_player == null) return;
        switch (e.PropertyName)
        {
            case nameof(PlayerController.Position):
                if (!Seek.IsDragging) Seek.Value = _player.Position;
                UpdateTimes();
                break;
            case nameof(PlayerController.Duration):
                Seek.Maximum = _player.Duration;
                UpdateTimes();
                break;
            case nameof(PlayerController.Fps):
                UpdateTimes();
                break;
            case nameof(PlayerController.IsPaused):
            case nameof(PlayerController.IsIdle):
                UpdatePlayIcon();
                UpdateButtons();
                break;
            case nameof(PlayerController.Volume):
            case nameof(PlayerController.Muted):
                UpdateVolume();
                break;
            case nameof(PlayerController.Shuffle):
                RandomButton.IsChecked = _player.Shuffle;
                break;
            case nameof(PlayerController.Loop):
                UpdateLoop();
                break;
            case nameof(PlayerController.HasVideo):
            case nameof(PlayerController.IsMidi):
            case nameof(PlayerController.Current):
                UpdateButtons();
                break;
        }
    }

    private void UpdatePlayIcon()
    {
        if (_player == null) return;
        bool playing = _player.IsPlaying;
        PlayButton.Content = FindResource(playing ? "Icon.PauseFill" : "Icon.PlayFill");
        PlayButton.ToolTip = Loc.T(playing ? "tip.pause" : "tip.play");
    }

    /// <summary>Lit for loop all and loop one; loop one shows a 1 inside the arrows.</summary>
    private void UpdateLoop()
    {
        if (_player == null) return;
        var loop = _player.Loop;
        LoopButton.IsChecked = loop != LoopMode.Off;
        LoopButton.Content = FindResource(loop == LoopMode.One ? "Icon.RepeatOneFill" : "Icon.RepeatFill");
        LoopButton.ToolTip = Loc.T(LoopModes.Key(loop));
    }

    private void UpdateVolume()
    {
        if (_player == null) return;
        VolumeControl.Value = _player.Volume;
        VolumeControl.Muted = _player.Muted;
        MuteButton.Content = FindResource(_player.Muted || _player.Volume <= 0 ? "Icon.VolumeMuteFill" : "Icon.VolumeFill");
        MuteButton.ToolTip = $"{Loc.T(_player.Muted ? "tip.unmute" : "tip.mute")} ({_player.Volume:0}%)";
    }

    private void UpdateButtons()
    {
        if (_player == null) return;
        bool hasMedia = _player.Current != null;
        bool video = _player.HasVideo && !_player.IsMidi;
        AudioButton.IsEnabled = hasMedia && !_player.IsMidi;
        SubtitlesButton.IsEnabled = video;
        PipButton.IsEnabled = video;
        StopButton.IsEnabled = hasMedia;
    }

    public void SetQueueOpen(bool open) => QueueButton.IsChecked = open;

    public void SetFullscreenState(bool fullscreen)
    {
        FullscreenButton.Content = FindResource(fullscreen ? "Icon.ExitFullscreenFill" : "Icon.FullscreenFill");
        FullscreenButton.ToolTip = Loc.T(fullscreen ? "tip.exitFullscreen" : "tip.fullscreen");
    }

    public void SetTimeMode(TimeDisplayMode mode)
    {
        _mode = mode;
        UpdateTimes();
    }

    private void UpdateTimes()
    {
        if (_player == null) return;
        double fps = _player.Fps;
        var mode = (_mode != TimeDisplayMode.Time && fps <= 0) ? TimeDisplayMode.Time : _mode;
        bool longForm = _player.Duration >= 3600;
        double pos = _player.Position, dur = _player.Duration;
        TimeText.Text = _player.Settings.ShowRemainingTime && dur > 0
            ? "-" + TimeFormat.Format(Math.Max(0, dur - pos), mode, fps, longForm)
            : TimeFormat.Format(pos, mode, fps, longForm);
        DurationText.Text = TimeFormat.Format(dur, mode, fps, longForm);
    }

    // ---- clicks ----
    private void Prev_Click(object sender, RoutedEventArgs e) => _player?.Previous();
    private void Play_Click(object sender, RoutedEventArgs e) => _player?.PlayPause();
    private void Next_Click(object sender, RoutedEventArgs e) => _player?.Next();
    private void Stop_Click(object sender, RoutedEventArgs e) => _player?.Stop();
    private void Mute_Click(object sender, RoutedEventArgs e) { if (_player != null) _player.SetMuted(!_player.Muted); }
    private void Random_Click(object sender, RoutedEventArgs e) { if (_player != null) _player.Shuffle = RandomButton.IsChecked == true; }
    // The toggle flips IsChecked on its own; CycleLoop's change sets it back to match the mode.
    private void Loop_Click(object sender, RoutedEventArgs e) { _player?.CycleLoop(); UpdateLoop(); }
    private void Audio_Click(object sender, RoutedEventArgs e) => _host?.ShowAudioMenu(AudioButton);
    private void Subtitles_Click(object sender, RoutedEventArgs e) => _host?.ShowSubtitlesMenu(SubtitlesButton);
    private void Speed_Click(object sender, RoutedEventArgs e) => _host?.ShowSpeedMenu(SpeedButton);
    private void Queue_Click(object sender, RoutedEventArgs e) => _host?.ToggleQueue();
    private void Pip_Click(object sender, RoutedEventArgs e) => _host?.TogglePip();
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => _host?.ToggleFullscreen();

    private void Time_LeftClick(object sender, MouseButtonEventArgs e)
    {
        if (_player == null) return;
        _player.Settings.ShowRemainingTime = !_player.Settings.ShowRemainingTime;
        UpdateTimes();
    }

    /// <summary>Right-click the time: switch between seconds, timecode with frames, and frame numbers.</summary>
    private void Time_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_player == null) return;
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Top };
        void Add(TimeDisplayMode mode, string key)
        {
            var item = new MenuItem { Header = Loc.T(key), IsCheckable = true, IsChecked = _mode == mode };
            item.Click += (_, _) =>
            {
                _player.Settings.TimeDisplay = TimeFormat.ToSetting(mode);
                TimeModeChanged?.Invoke(mode);
            };
            menu.Items.Add(item);
        }
        Add(TimeDisplayMode.Time, "time.time");
        Add(TimeDisplayMode.Timecode, "time.timecode");
        Add(TimeDisplayMode.Frames, "time.frames");
        menu.Items.Add(new Separator());
        var remaining = new MenuItem { Header = Loc.T("time.remaining"), IsCheckable = true, IsChecked = _player.Settings.ShowRemainingTime };
        remaining.Click += (_, _) => { _player.Settings.ShowRemainingTime = remaining.IsChecked; UpdateTimes(); };
        menu.Items.Add(remaining);
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Raised when the user picks a time format, so every bar (main and fullscreen) switches together.</summary>
    public static event Action<TimeDisplayMode>? TimeModeChanged;
}
