using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FramePlay.Interop;
using FramePlay.Localization;
using FramePlay.Playback;

namespace FramePlay.UI;

public enum OverlayMode { None, Fullscreen, Pip }

/// <summary>
/// Transparent window that floats the controls over the video in fullscreen and picture in picture.
/// It never takes keyboard focus, and its see-through parts pass clicks to the video below.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly MainWindow _main;
    private readonly DispatcherTimer _hideTimer;
    private bool _shown;
    private IntPtr _hwnd;

    public OverlayMode Mode { get; private set; } = OverlayMode.None;
    public event Action? Hidden;
    public event Action? Shown;

    public OverlayWindow(MainWindow main)
    {
        _main = main;
        InitializeComponent();
        Bar.Attach(main);
        Bar.BarBackground = Brushes.Transparent;
        PipSeek.SeekRequested += (t, exact) => main.Player.Seek(t, exact);
        PipSeek.HoverChanged += (t, x) => main.ShowSeekPreview(PipSeek, t, x);
        PipSeek.HoverEnded += () => main.HideSeekPreview();
        main.Player.PropertyChanged += OnPlayerChanged;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _hideTimer.Tick += (_, _) => TryHide();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            int ex = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(_hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
        };
        MouseMove += (_, _) => Poke();
        // Create the native window now (hidden) so it can be positioned before it is first shown.
        new WindowInteropHelper(this).EnsureHandle();
    }

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlayerController.Position):
            case nameof(PlayerController.Duration):
                UpdatePipTime();
                break;
            case nameof(PlayerController.IsPaused):
            case nameof(PlayerController.IsIdle):
                UpdatePipPlay();
                break;
            case nameof(PlayerController.Title):
                FsTitle.Text = _main.Player.Title;
                break;
        }
    }

    public void SetMode(OverlayMode mode)
    {
        Mode = mode;
        FullscreenLayout.Visibility = mode == OverlayMode.Fullscreen ? Visibility.Visible : Visibility.Collapsed;
        PipLayout.Visibility = mode == OverlayMode.Pip ? Visibility.Visible : Visibility.Collapsed;
        if (mode == OverlayMode.None)
        {
            _hideTimer.Stop();
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 0;
            Root.IsHitTestVisible = false;
            _shown = false;
            Hide();
            return;
        }
        RefreshMode();
    }

    public void RefreshMode()
    {
        FsTitle.Text = _main.Player.Title;
        Bar.RefreshAll();
        UpdatePipTime();
        UpdatePipPlay();
    }

    private void UpdatePipTime()
    {
        if (Mode != OverlayMode.Pip) return;
        var p = _main.Player;
        PipSeek.Maximum = p.Duration;
        if (!PipSeek.IsDragging) PipSeek.Value = p.Position;
        bool longForm = p.Duration >= 3600;
        PipTime.Text = $"{TimeFormat.Clock(p.Position, longForm)} / {TimeFormat.Clock(p.Duration, longForm)}";
    }

    private void UpdatePipPlay()
    {
        bool playing = _main.Player.IsPlaying;
        PipPlay.Content = FindResource(playing ? "Icon.PauseFill" : "Icon.PlayFill");
        PipPlay.ToolTip = Loc.T(playing ? "tip.pause" : "tip.play");
    }

    /// <summary>Places the overlay over the given screen rectangle (device pixels).</summary>
    public void PlaceOver(Win32.RECT r)
    {
        _placement = r;
        if (_hwnd == IntPtr.Zero) return;
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height, Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
    }

    private Win32.RECT _placement;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // The first Show() creates the window: put it where it belongs straight away.
        if (_placement.Width > 0) Dispatcher.BeginInvoke(() => PlaceOver(_placement));
    }

    /// <summary>Shows the controls and restarts the auto-hide timer.</summary>
    public void Poke()
    {
        if (Mode == OverlayMode.None) return;
        if (!_shown) FadeTo(1);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>True when the mouse is over the given element, checked against the real cursor position.</summary>
    private static bool CursorOver(FrameworkElement el)
    {
        if (!el.IsVisible || PresentationSource.FromVisual(el) == null) return false;
        Win32.GetCursorPos(out var p);
        var topLeft = el.PointToScreen(new Point(0, 0));
        var bottomRight = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
        return p.X >= topLeft.X && p.X < bottomRight.X && p.Y >= topLeft.Y && p.Y < bottomRight.Y;
    }

    private void TryHide()
    {
        bool menuOpen = Mouse.Captured is System.Windows.Controls.ContextMenu or System.Windows.Controls.MenuItem;
        bool busy = menuOpen
                    || (Mode == OverlayMode.Fullscreen && (CursorOver(FsPanel) || Bar.Seek.IsDragging))
                    || (Mode == OverlayMode.Pip && (CursorOver(PipLayout) || PipSeek.IsDragging));
        if (busy) return; // keep the timer running and check again
        _hideTimer.Stop();
        FadeTo(0);
    }

    /// <summary>Strip height (device-independent pixels) the fullscreen controls need at the bottom of the screen.</summary>
    public const double FullscreenStripHeight = 140;

    /// <summary>True while the controls are showing (or fading in).</summary>
    public bool IsShowingControls => _shown;

    /// <summary>How much of the bottom of the video (DIP) the controls cover, so subtitles can stay above them.</summary>
    public double CoveredBottom => Mode switch
    {
        OverlayMode.Fullscreen => FsPanel.ActualHeight > 0 ? FsPanel.ActualHeight + FsPanel.Margin.Bottom : FullscreenStripHeight,
        OverlayMode.Pip => PipBottom.ActualHeight > 0 ? PipBottom.ActualHeight + PipBottom.Margin.Bottom + PipLayout.Margin.Bottom : 40,
        _ => 0,
    };

    private void FadeTo(double target)
    {
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(target > 0 ? 120 : 260));
        if (target > 0)
        {
            // Hidden windows cost nothing; show the window again only when the controls appear.
            if (!IsVisible) Show();
            Root.IsHitTestVisible = true;
            _shown = true;
            Shown?.Invoke();
        }
        else
        {
            _shown = false;
            anim.Completed += (_, _) =>
            {
                if (_shown) return;
                Root.IsHitTestVisible = false;
                Hide();
            };
            Hidden?.Invoke();
            _main.HideSeekPreview();
        }
        Root.BeginAnimation(OpacityProperty, anim);
    }

    // ---- picture in picture actions ----
    private void PipScrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _main.ExitPip();
            return;
        }
        // Drag the picture-in-picture window from anywhere on the video.
        Win32.ReleaseCapture();
        Win32.SendMessage(_main.Hwnd, Win32.WM_NCLBUTTONDOWN, (IntPtr)Win32.HTCAPTION, IntPtr.Zero);
    }

    private void PipBack_Click(object sender, RoutedEventArgs e) => _main.ExitPip();

    private void PipClose_Click(object sender, RoutedEventArgs e)
    {
        // Like YouTube: closing the small window pauses the video and brings the player back.
        _main.Player.Pause();
        _main.ExitPip();
    }

    private void PipPrev_Click(object sender, RoutedEventArgs e) => _main.Player.Previous();
    private void PipNext_Click(object sender, RoutedEventArgs e) => _main.Player.Next();
    private void PipPlay_Click(object sender, RoutedEventArgs e) => _main.Player.PlayPause();
}
