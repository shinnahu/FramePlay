using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Input;
using FramePlay.Interop;
using FramePlay.Localization;
using FramePlay.Playback;
using FramePlay.Themes;
using FramePlay.UI.Controls;
using Microsoft.Win32;

namespace FramePlay.UI;

public partial class MainWindow : Window, IPlayerHost
{
    private readonly AppSettings _settings;
    private readonly List<string> _startupFiles;
    private readonly HotkeyMap _hotkeys;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private IntPtr _hwnd;
    private bool _closing;
    private DateTime _lastExternalOpen = DateTime.MinValue;

    public PlayerController Player { get; }

    public MainWindow(AppSettings settings, History history, List<string> startupFiles)
    {
        _settings = settings;
        _startupFiles = startupFiles;
        InitializeComponent();

        Player = new PlayerController(Dispatcher, settings, history);
        _hotkeys = new HotkeyMap(settings.Hotkeys);

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastPanel.Visibility = Visibility.Collapsed; };
        _settingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _settingsSaveTimer.Tick += (_, _) => { _settingsSaveTimer.Stop(); _settings.Save(); };

        QueueList.ItemsSource = Player.Queue;
        Player.Queue.CollectionChanged += (_, _) => UpdateQueueInfo();
        UpdateQueueInfo();

        Bar.Attach(this);
        ControlBar.TimeModeChanged += mode => { Bar.SetTimeMode(mode); _overlay?.Bar.SetTimeMode(mode); SaveSettingsSoon(); };

        Player.PropertyChanged += OnPlayerChanged;
        Player.MediaChanged += UpdateView;
        Player.Toast += ShowToast;
        Player.VideoOutputReady += () => Video.DisableChildInput();
        Player.VideoSizeChanged += OnVideoSizeChanged;

        WireVideoInput();
        RestoreWindowBounds();
        if (_settings.QueueOpen) SetQueueOpen(true);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => PositionOverlay();
        SizeChanged += (_, _) =>
        {
            PositionOverlay();
            if (_overlay?.IsShowingControls == true) UpdateSubtitleLift(animate: false);
        };
        Activated += (_, _) => PositionOverlay();
        PreviewKeyDown += OnPreviewKeyDown;
        Drop += OnDrop;
        DragOver += OnDragOver;
        ThemeManager.Changed += ApplyWindowChrome;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Log.Timing("main window handle created");
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        ApplyWindowChrome();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log.Timing("main window loaded");
        StartPlayback();
    }

    private bool _playbackStarted;

    private void StartPlayback()
    {
        if (_playbackStarted) return;
        if (Video.Surface == IntPtr.Zero) UpdateLayout();
        if (Video.Surface == IntPtr.Zero) return; // not built yet: Loaded tries again
        _playbackStarted = true;
        try
        {
            Player.Initialize(Video.Surface);
            Log.Timing("mpv initialized");
        }
        catch (Exception ex)
        {
            Log.Error("mpv init", ex);
            MessageBox.Show(this, "FramePlay could not start its playback engine (libmpv-2.dll).\n\n" + ex.Message, "FramePlay",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        EnableFileDropOnVideo();
        UpdateView();
        if (_startupFiles.Count > 0) Player.Enqueue(_startupFiles, playNow: true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closing = true;
        if (_pip) ExitPip();
        if (_fullscreen) ExitFullscreen();
        SaveWindowBounds();
        _settings.Save();
        _overlay?.Close();
        _seekPreview?.Dispose();
        Player.Dispose();
    }

    /// <summary>Another FramePlay launch (e.g. a double-click in File Explorer) sent us its files.</summary>
    public void OnOtherInstance(List<string> files)
    {
        if (WindowState == WindowState.Minimized) WindowState = _wasMaximized ? WindowState.Maximized : WindowState.Normal;
        Activate();
        Win32.SetForegroundWindow(_hwnd);
        if (files.Count == 0) return;
        // Explorer can start one process per selected file; files arriving together join the queue.
        bool batch = (DateTime.UtcNow - _lastExternalOpen).TotalMilliseconds < 700;
        _lastExternalOpen = DateTime.UtcNow;
        Player.Enqueue(files, playNow: !batch);
    }

    // =============================================================================================
    // Views: video, audio-only or idle
    // =============================================================================================
    private void UpdateView()
    {
        bool idle = Player.Current == null || Player.IsIdle && !Player.IsMidi;
        bool video = !idle && Player.HasVideo && !Player.IsMidi;
        Video.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        AudioView.Visibility = !idle && !video ? Visibility.Visible : Visibility.Collapsed;
        IdleView.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        StageBackground.Background = video ? Brushes.Black : (Brush)FindResource("Bg2Brush");

        if (!idle && !video) UpdateAudioView();
        _seekPreview?.Reset();
        UpdateTitle();
        if (!video && (_pip || _fullscreen) && _pip) ExitPip();
        UpdateTopmost();
        _overlay?.RefreshMode();
    }

    private void UpdateTitle()
    {
        var t = Player.Current == null ? "FramePlay" : Player.Title;
        TitleText.Text = t;
        Title = Player.Current == null ? "FramePlay" : $"{t} - FramePlay";
    }

    private void UpdateAudioView()
    {
        var item = Player.Current;
        if (item == null) return;
        AudioTitle.Text = Player.IsMidi ? item.Title : (Player.Title ?? item.Title);
        if (Player.IsMidi)
        {
            AudioSubtitle.Text = Loc.F("audio.midi", Path.GetFileNameWithoutExtension(Player.SoundfontPath() ?? "?"));
            CoverImage.Source = null;
        }
        else
        {
            AudioSubtitle.Text = string.Join("  ·  ", new[] { Player.Artist, Player.Album }.Where(s => !string.IsNullOrWhiteSpace(s)));
            CoverImage.Source = ShellThumbnail.Get(item.Path, 512);
        }
        CoverPlaceholder.Visibility = CoverImage.Source == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlayerController.Title):
            case nameof(PlayerController.Artist):
            case nameof(PlayerController.Album):
                UpdateTitle();
                if (AudioView.Visibility == Visibility.Visible) UpdateAudioView();
                break;
            case nameof(PlayerController.HasVideo):
            case nameof(PlayerController.IsIdle):
            case nameof(PlayerController.IsMidi):
                UpdateView();
                break;
            case nameof(PlayerController.IsPaused):
                UpdateTopmost();
                break;
        }
    }

    /// <summary>Settings › "Show above all windows while a video plays".</summary>
    private void UpdateTopmost()
    {
        bool want = _pip || _manualTopmost || (_settings.TopmostWhilePlayingVideo && Player.IsPlaying && Player.HasVideo);
        if (Topmost != want) Topmost = want;
        SyncOverlayLayer();
    }

    /// <summary>
    /// The controls float over the video in their own window, which must share the main window's
    /// always-on-top layer. Otherwise an overlay created after the window went on top (entering picture
    /// in picture straight away, for example) sits behind the video and can't be seen or clicked.
    /// </summary>
    private void SyncOverlayLayer()
    {
        if (_overlay != null && _overlay.Topmost != Topmost) _overlay.Topmost = Topmost;
    }

    // =============================================================================================
    // Toasts
    // =============================================================================================
    private void ShowToast(string text)
    {
        if (Video.Visibility == Visibility.Visible && !string.IsNullOrEmpty(text))
        {
            // Over video, mpv draws the message itself (a WPF element can't sit on top of the video window).
            Player.Mpv.Command("show-text", text, "1500");
            return;
        }
        ToastText.Text = text;
        ToastPanel.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public void SaveSettingsSoon()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    // =============================================================================================
    // Window chrome
    // =============================================================================================
    private bool _wasMaximized;

    private void ApplyWindowChrome()
    {
        if (_hwnd == IntPtr.Zero) return;
        int dark = ThemeManager.IsDark ? 1 : 0;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        var bg = ThemeManager.Bg0;
        int colorRef = bg.R | (bg.G << 8) | (bg.B << 16);
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) _wasMaximized = WindowState == WindowState.Maximized;
        MaxButton.ToolTip = Loc.T(WindowState == WindowState.Maximized ? "tip.restore" : "tip.maximize");
        if (WindowState == WindowState.Minimized && _settings.AutoPipOnMinimize && Player.IsPlaying && Player.HasVideo && !_pip)
        {
            Dispatcher.BeginInvoke(() => { WindowState = WindowState.Normal; EnterPip(); });
        }
        PositionOverlay();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RestoreWindowBounds()
    {
        var b = _settings.Window;
        if (b == null || b.Width < MinWidth || b.Height < MinHeight) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        // Only restore if the saved spot is still on a monitor.
        var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!vs.IntersectsWith(new Rect(b.Left, b.Top, b.Width, b.Height))) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = b.Left; Top = b.Top; Width = b.Width; Height = b.Height;
        if (b.Maximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowBounds()
    {
        if (_pip || _fullscreen) return;
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (r.Width <= 0) return;
        _settings.Window = new WindowBounds { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height, Maximized = WindowState == WindowState.Maximized };
    }

    // =============================================================================================
    // Open files
    // =============================================================================================
    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFiles();

    public void OpenFiles()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = MediaFormats.OpenDialogFilter(Loc.T) };
        if (dlg.ShowDialog(this) == true) Player.Enqueue(dlg.FileNames, playNow: true);
    }

    public void OpenFolder()
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("dialog.openFolder") };
        if (dlg.ShowDialog(this) == true) Player.Enqueue(new[] { dlg.FolderName }, playNow: true);
    }

    public void AddSubtitleFile()
    {
        var dlg = new OpenFileDialog { Filter = MediaFormats.SubtitleDialogFilter(Loc.T) };
        if (Player.Current != null) dlg.InitialDirectory = Player.Current.Folder;
        if (dlg.ShowDialog(this) == true) Player.AddSubtitleFile(dlg.FileName);
    }

    public void AddAudioFile()
    {
        var dlg = new OpenFileDialog { Filter = MediaFormats.OpenDialogFilter(Loc.T) };
        if (Player.Current != null) dlg.InitialDirectory = Player.Current.Folder;
        if (dlg.ShowDialog(this) == true) Player.AddAudioFile(dlg.FileName);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            // Shift-drop only adds to the queue.
            bool addOnly = (e.KeyStates & DragDropKeyStates.ShiftKey) != 0;
            Player.Enqueue(files, playNow: !addOnly);
            Activate();
        }
    }

    /// <summary>The video area is a native window, so it accepts Explorer drops itself.</summary>
    private void EnableFileDropOnVideo()
    {
        if (Video.Surface == IntPtr.Zero) return;
        Video.FilesDropped += files => { Player.Enqueue(files, playNow: true); Activate(); };
        Video.AcceptFiles();
    }

    // =============================================================================================
    // IPlayerHost
    // =============================================================================================
    public bool IsQueueOpen => QueuePanel.Visibility == Visibility.Visible;

    public void ToggleQueue() => SetQueueOpen(!IsQueueOpen);

    public void SetQueueOpen(bool open)
    {
        QueuePanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        QueueColumn.Width = open ? new GridLength(340) : new GridLength(0);
        Bar.SetQueueOpen(open);
        _overlay?.Bar.SetQueueOpen(open);
        _settings.QueueOpen = open;
        SaveSettingsSoon();
    }

    private void UpdateQueueInfo()
    {
        int n = Player.Queue.Count;
        QueueCount.Text = n == 0 ? "" : n == 1 ? Loc.T("queue.one") : Loc.F("queue.count", n);
        QueueEmpty.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnVideoSizeChanged()
    {
        if (_pip) FitPipToAspect();
    }
}
