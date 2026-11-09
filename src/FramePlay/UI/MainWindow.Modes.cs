using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using FramePlay.Core;
using FramePlay.Interop;
using FramePlay.UI.Controls;

namespace FramePlay.UI;

public partial class MainWindow
{
    private bool _fullscreen;
    private bool _pip;
    private bool _manualTopmost;
    private bool _queueWasOpen;
    private WindowState _savedState;
    private Rect _savedBounds;
    private double _savedMinWidth, _savedMinHeight;
    private OverlayWindow? _overlay;
    private SeekPreview? _seekPreview;

    public bool IsFullscreen => _fullscreen;
    public bool IsPip => _pip;

    private void SaveNormalBounds()
    {
        _savedState = WindowState == WindowState.Minimized ? (_wasMaximized ? WindowState.Maximized : WindowState.Normal) : WindowState;
        _savedBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        _savedMinWidth = MinWidth;
        _savedMinHeight = MinHeight;
    }

    private void RestoreNormalBounds()
    {
        MinWidth = _savedMinWidth;
        MinHeight = _savedMinHeight;
        WindowState = WindowState.Normal;
        if (_savedBounds.Width > 0)
        {
            Left = _savedBounds.Left; Top = _savedBounds.Top;
            Width = _savedBounds.Width; Height = _savedBounds.Height;
        }
        if (_savedState == WindowState.Maximized) WindowState = WindowState.Maximized;
    }

    /// <summary>Hides the bars and rounded frame so the video fills the window (fullscreen and PiP).</summary>
    private void SetChromeless(bool chromeless)
    {
        var v = chromeless ? Visibility.Collapsed : Visibility.Visible;
        TopBar.Visibility = v;
        Bar.Visibility = v;
        TopRow.Height = chromeless ? new GridLength(0) : new GridLength(40);
        Stage.Margin = chromeless ? new Thickness(0) : new Thickness(10, 0, 10, 0);
        StageBackground.CornerRadius = new CornerRadius(chromeless ? 0 : 9);
        Video.CornerRadius = chromeless ? 0 : 9;
        Chrome.CaptionHeight = chromeless ? 0 : 40;
        if (chromeless)
        {
            _queueWasOpen = IsQueueOpen;
            QueuePanel.Visibility = Visibility.Collapsed;
            QueueColumn.Width = new GridLength(0);
        }
        else if (_queueWasOpen) SetQueueOpen(true);
    }

    // =============================================================================================
    // Fullscreen
    // =============================================================================================
    public void ToggleFullscreen()
    {
        if (_fullscreen) ExitFullscreen();
        else EnterFullscreen();
    }

    public void EnterFullscreen()
    {
        if (_fullscreen) return;
        if (_pip) ExitPip();
        Video.HoldPictureDuringResize();
        SaveNormalBounds();
        _fullscreen = true;
        SetChromeless(true);
        Chrome.ResizeBorderThickness = new Thickness(0);
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        var (mon, _) = Win32.MonitorRects(_hwnd);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOP, mon.Left, mon.Top, mon.Width, mon.Height, Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
        Bar.SetFullscreenState(true);
        ShowOverlay(OverlayMode.Fullscreen);
        UpdateTopmost();
    }

    public void ExitFullscreen()
    {
        if (!_fullscreen) return;
        Video.HoldPictureDuringResize();
        _fullscreen = false;
        Video.HideCursor = false;
        Chrome.ResizeBorderThickness = new Thickness(6);
        SetChromeless(false);
        RestoreNormalBounds();
        Bar.SetFullscreenState(false);
        ShowOverlay(OverlayMode.None);
        UpdateTopmost();
    }

    // =============================================================================================
    // Picture in picture
    // =============================================================================================
    public void TogglePip()
    {
        if (_pip) ExitPip();
        else EnterPip();
    }

    private double VideoAspect => Player.VideoWidth > 0 && Player.VideoHeight > 0 ? (double)Player.VideoWidth / Player.VideoHeight : 16.0 / 9.0;

    public void EnterPip()
    {
        if (_pip || !Player.HasVideo || Player.IsMidi) return;
        if (_fullscreen) ExitFullscreen();
        Video.HoldPictureDuringResize();
        SaveNormalBounds();
        _pip = true;
        SetChromeless(true);
        Chrome.ResizeBorderThickness = new Thickness(6);
        Video.EdgeResizeZone = (int)Math.Round(7 * Win32.GetDpiForWindow(_hwnd) / 96.0);
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        MinWidth = 240;
        MinHeight = 240 / VideoAspect;

        var work = SystemParameters.WorkArea;
        var saved = _settings.Pip;
        double w = saved?.Width is > 200 and < 2000 ? saved.Width : 426;
        double h = w / VideoAspect;
        double left = work.Right - w - 18, top = work.Bottom - h - 18;
        if (saved != null && saved.Width > 0)
        {
            var r = new Rect(saved.Left, saved.Top, w, h);
            var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (vs.Contains(r.TopLeft) && vs.Contains(r.BottomRight)) { left = saved.Left; top = saved.Top; }
        }
        Left = left; Top = top; Width = w; Height = h;
        UpdateTopmost();
        Player.SetPipSubtitleScale(true);
        Player.HoldLastFrameAtEnd = true;
        ShowOverlay(OverlayMode.Pip);
    }

    public void ExitPip()
    {
        if (!_pip) return;
        Video.HoldPictureDuringResize();
        _settings.Pip = new WindowBounds { Left = Left, Top = Top, Width = Width, Height = Height };
        _pip = false;
        Player.SetPipSubtitleScale(false);
        Player.HoldLastFrameAtEnd = false;
        Video.EdgeResizeZone = 0;
        SetChromeless(false);
        RestoreNormalBounds();
        ShowOverlay(OverlayMode.None);
        UpdateTopmost();
        if (!_closing) { Activate(); SaveSettingsSoon(); }
    }

    private void FitPipToAspect()
    {
        if (!_pip) return;
        Height = Width / VideoAspect;
        MinHeight = MinWidth / VideoAspect;
    }

    // =============================================================================================
    // Overlay (controls over the video in fullscreen and PiP)
    // =============================================================================================
    private void ShowOverlay(OverlayMode mode)
    {
        if (mode == OverlayMode.None)
        {
            _overlay?.SetMode(OverlayMode.None);
            Player.SetSubtitleLimit(100, animate: false);
            return;
        }
        if (_overlay == null)
        {
            _overlay = new OverlayWindow(this) { Owner = this };
            _overlay.Hidden += () => { if (_fullscreen) Video.HideCursor = true; UpdateSubtitleLift(); };
            _overlay.Shown += () => { Video.HideCursor = false; UpdateSubtitleLift(); };
        }
        SyncOverlayLayer();
        _overlay.SetMode(mode);
        PositionOverlay();
        _overlay.Poke();
    }

    /// <summary>
    /// Keeps subtitles above the controls while they show over the video (full screen and picture in
    /// picture). Subtitles that already sit higher stay where they are.
    /// </summary>
    private void UpdateSubtitleLift(bool animate = true)
    {
        if (_overlay == null || _overlay.Mode == OverlayMode.None || !_overlay.IsShowingControls || ActualHeight <= 0)
        {
            Player.SetSubtitleLimit(100, animate);
            return;
        }
        // sub-pos is a percentage of the window height; leave a small gap above the controls.
        double covered = _overlay.CoveredBottom + 8;
        Player.SetSubtitleLimit(Math.Clamp(100 * (1 - covered / ActualHeight), 40, 100), animate);
    }

    /// <summary>Keeps the overlay over the video: the bottom strip in fullscreen, the whole window in PiP.</summary>
    private void PositionOverlay()
    {
        if (_overlay == null || _overlay.Mode == OverlayMode.None || _hwnd == IntPtr.Zero) return;
        Win32.GetWindowRect(_hwnd, out var r);
        if (_overlay.Mode == OverlayMode.Fullscreen)
        {
            int strip = (int)Math.Round(OverlayWindow.FullscreenStripHeight * Win32.GetDpiForWindow(_hwnd) / 96.0);
            r.Top = Math.Max(r.Top, r.Bottom - strip);
        }
        _overlay.PlaceOver(r);
    }

    // =============================================================================================
    // Seek preview (time + thumbnail above the seek bar)
    // =============================================================================================
    public void ShowSeekPreview(SeekBar bar, double seconds, double x)
    {
        _seekPreview ??= new SeekPreview(Player);
        _seekPreview.Show(bar, seconds, x);
        _overlay?.Poke();
    }

    public void HideSeekPreview() => _seekPreview?.Hide();

    // =============================================================================================
    // Window messages
    // =============================================================================================
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Win32.WM_GETMINMAXINFO when !_fullscreen:
            {
                // Maximize to the monitor's work area (WindowChrome would otherwise spill over the edges).
                var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                var (mon, work) = Win32.MonitorRects(hwnd);
                mmi.ptMaxPosition.X = work.Left - mon.Left;
                mmi.ptMaxPosition.Y = work.Top - mon.Top;
                mmi.ptMaxSize.X = work.Width;
                mmi.ptMaxSize.Y = work.Height;
                Marshal.StructureToPtr(mmi, lParam, true);
                break;
            }
            case Win32.WM_SIZING when _pip:
            {
                // Keep the picture-in-picture window at the video's aspect ratio while resizing.
                var r = Marshal.PtrToStructure<Win32.RECT>(lParam);
                double aspect = VideoAspect;
                int edge = wParam.ToInt32();
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                // The side that doesn't follow the mouse grows away from the nearest screen edge,
                // so a window parked in a corner never slides off the screen.
                var (_, work) = Win32.MonitorRects(hwnd);
                bool lowerHalf = (r.Top + r.Bottom) / 2 > (work.Top + work.Bottom) / 2;
                bool rightHalf = (r.Left + r.Right) / 2 > (work.Left + work.Right) / 2;
                if (edge is Win32.WMSZ_TOP or Win32.WMSZ_BOTTOM)
                {
                    int nw = (int)Math.Round(h * aspect);
                    if (rightHalf) r.Left = r.Right - nw; else r.Right = r.Left + nw;
                }
                else if (edge is Win32.WMSZ_LEFT or Win32.WMSZ_RIGHT)
                {
                    int nh = (int)Math.Round(w / aspect);
                    if (lowerHalf) r.Top = r.Bottom - nh; else r.Bottom = r.Top + nh;
                }
                else
                {
                    int nh = (int)Math.Round(w / aspect);
                    if (edge is Win32.WMSZ_TOPLEFT or Win32.WMSZ_TOPRIGHT) r.Top = r.Bottom - nh;
                    else r.Bottom = r.Top + nh;
                }
                Marshal.StructureToPtr(r, lParam, true);
                handled = true;
                return (IntPtr)1;
            }
            case Win32.WM_DPICHANGED:
                Dispatcher.BeginInvoke(PositionOverlay);
                break;
        }
        return IntPtr.Zero;
    }
}
