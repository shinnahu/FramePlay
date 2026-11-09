using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FramePlay.Interop;
using static FramePlay.Interop.Win32;

namespace FramePlay.UI;

/// <summary>
/// A native child window that mpv renders into (mpv's "wid"). Mouse input over the video arrives here:
/// mpv's own window inside it is disabled, so clicks fall through to this window.
/// </summary>
public sealed class VideoHost : HwndHost
{
    private const string ClassName = "FramePlayVideoSurface";
    private static ushort _classAtom;
    private static readonly IntPtr DefWindowProcPtr = GetProcAddress(GetModuleHandle("user32.dll"), "DefWindowProcW");

    private double _cornerRadius = 9;
    private bool _hideCursor;
    private bool _trackingLeave;
    private int _lastMoveX = int.MinValue, _lastMoveY = int.MinValue;

    public IntPtr Surface { get; private set; }

    /// <summary>Raised with positions in device-independent pixels relative to the video area.</summary>
    public event Action<Point>? MouseMoved;
    public event Action<Point>? LeftButtonDown;
    public event Action<Point>? LeftButtonUp;
    public event Action<Point>? LeftDoubleClick;
    public event Action<Point>? RightButtonUp;
    public event Action? MiddleButtonUp;
    public event Action<int>? Wheel;
    public event Action? MouseLeft;
    public event Action<string[]>? FilesDropped;

    /// <summary>
    /// When &gt; 0, mouse positions this close (device pixels) to the edge of the top-level window fall through
    /// to it, so the borderless picture-in-picture window can still be resized from its edges.
    /// </summary>
    public int EdgeResizeZone { get; set; }

    [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(IntPtr hDrop, uint index, System.Text.StringBuilder? file, uint size);
    [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr hDrop);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    private const int WM_DROPFILES = 0x0233;

    /// <summary>Lets files dragged from File Explorer be dropped straight onto the video.</summary>
    public void AcceptFiles()
    {
        if (Surface != IntPtr.Zero) DragAcceptFiles(Surface, true);
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; UpdateRegion(); }
    }

    public bool HideCursor
    {
        get => _hideCursor;
        set
        {
            if (_hideCursor == value) return;
            _hideCursor = value;
            if (Surface != IntPtr.Zero && GetCursorPos(out var p))
            {
                // Refresh the cursor right away instead of waiting for the next mouse move.
                ScreenToClient(Surface, ref p);
                GetClientRect(Surface, out var r);
                if (p.X >= 0 && p.Y >= 0 && p.X < r.Width && p.Y < r.Height)
                    SetCursor(value ? IntPtr.Zero : LoadCursor(IntPtr.Zero, IDC_ARROW));
            }
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (_classAtom == 0)
        {
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                style = CS_DBLCLKS,
                lpfnWndProc = DefWindowProcPtr,
                hInstance = GetModuleHandle(null),
                hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
                hbrBackground = GetStockObject(4), // BLACK_BRUSH
                lpszClassName = ClassName,
            };
            _classAtom = RegisterClassEx(ref wc);
        }
        Surface = CreateWindowEx(0, ClassName, "FramePlay video", WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 16, 16, hwndParent.Handle, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        return new HandleRef(this, Surface);
    }

    protected override void DestroyWindowCore(HandleRef hwnd) => DestroyWindow(hwnd.Handle);

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        UpdateRegion();
    }

    // ---------------------------------------------------------------------------------------------
    // Holding the picture while the window changes size. When the window goes full screen (or into or out
    // of picture in picture), Windows shows mpv's last frame stretched to the new size until mpv has
    // resized its own window and drawn again. Meanwhile the video area is hidden (the black stage behind
    // it shows), and it comes back once mpv's window has the new size and has had a few frames to draw.
    // ---------------------------------------------------------------------------------------------
    private System.Windows.Threading.DispatcherTimer? _holdTimer;
    private bool _holding;
    private long _holdSince;
    private int _holdSettled;
    private RECT _holdStartSize;

    public void HoldPictureDuringResize()
    {
        if (Surface == IntPtr.Zero) return;
        if (_holdTimer == null)
        {
            _holdTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            _holdTimer.Tick += (_, _) => CheckHold();
        }
        GetClientRect(Surface, out _holdStartSize);
        _holding = true;
        _holdSince = Environment.TickCount64;
        _holdSettled = 0;
        UpdateRegion();
        _holdTimer.Stop();
        _holdTimer.Start();
    }

    private void CheckHold()
    {
        long elapsed = Environment.TickCount64 - _holdSince;
        GetClientRect(Surface, out var host);
        bool hostResized = host.Width != _holdStartSize.Width || host.Height != _holdStartSize.Height;
        bool childMatches = true;
        EnumChildWindows(Surface, (h, _) =>
        {
            GetClientRect(h, out var c);
            if (c.Width != host.Width || c.Height != host.Height) childMatches = false;
            return true;
        }, IntPtr.Zero);
        // Once the window has its new size and mpv's window matches it, give mpv ~3 frames to draw.
        if ((hostResized || elapsed > 150) && childMatches) _holdSettled++;
        else _holdSettled = 0;
        if (_holdSettled >= 3 || elapsed > 500)
        {
            _holding = false;
            _holdTimer!.Stop();
            UpdateRegion();
        }
    }

    /// <summary>Rounds the corners of the video area (the region also clips mpv's window inside).</summary>
    private void UpdateRegion()
    {
        if (Surface == IntPtr.Zero) return;
        if (_holding)
        {
            SetWindowRgn(Surface, CreateRoundRectRgn(0, 0, 0, 0, 0, 0), true); // nothing shows
            return;
        }
        GetClientRect(Surface, out var r);
        double scale = GetDpiForWindow(Surface) / 96.0;
        int rad = (int)Math.Round(_cornerRadius * scale);
        if (rad <= 0) { SetWindowRgn(Surface, IntPtr.Zero, true); return; }
        var rgn = CreateRoundRectRgn(0, 0, r.Width + 1, r.Height + 1, rad * 2, rad * 2);
        SetWindowRgn(Surface, rgn, true); // the system owns the region from here on
    }

    /// <summary>Disables mpv's own window(s) so they never take focus or mouse input.</summary>
    public void DisableChildInput()
    {
        if (Surface == IntPtr.Zero) return;
        EnumChildWindows(Surface, (h, _) =>
        {
            if (IsWindowEnabled(h)) EnableWindow(h, false);
            return true;
        }, IntPtr.Zero);
    }

    private Point ToDip(IntPtr lParam)
    {
        var p = new Point(LoWord(lParam), HiWord(lParam));
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.Transform(p) : p;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TRACKMOUSEEVENT { public int cbSize; public int dwFlags; public IntPtr hwndTrack; public int dwHoverTime; }
    [DllImport("user32.dll")] private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST when EdgeResizeZone > 0:
            {
                var top = GetAncestor(hwnd, 2 /*GA_ROOT*/);
                GetWindowRect(top, out var wr);
                int x = LoWord(lParam), y = HiWord(lParam), z = EdgeResizeZone;
                if (x - wr.Left < z || wr.Right - x <= z || y - wr.Top < z || wr.Bottom - y <= z)
                {
                    handled = true;
                    return (IntPtr)HTTRANSPARENT;
                }
                break;
            }
            case WM_DROPFILES:
            {
                var files = new List<string>();
                uint n = DragQueryFile(wParam, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < n; i++)
                {
                    uint len = DragQueryFile(wParam, i, null, 0);
                    var sb = new System.Text.StringBuilder((int)len + 1);
                    DragQueryFile(wParam, i, sb, len + 1);
                    files.Add(sb.ToString());
                }
                DragFinish(wParam);
                if (files.Count > 0) FilesDropped?.Invoke(files.ToArray());
                handled = true;
                return IntPtr.Zero;
            }
            case WM_SETCURSOR:
                if (_hideCursor && LoWord(lParam) == HTCLIENT)
                {
                    SetCursor(IntPtr.Zero);
                    handled = true;
                    return (IntPtr)1;
                }
                break;
            case WM_MOUSEMOVE:
                if (!_trackingLeave)
                {
                    var tme = new TRACKMOUSEEVENT { cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(), dwFlags = 0x2 /*TME_LEAVE*/, hwndTrack = hwnd };
                    _trackingLeave = TrackMouseEvent(ref tme);
                }
                // Windows also sends WM_MOUSEMOVE when a window appears or disappears under a cursor that
                // didn't move (the controls hiding, for example). Only real movement counts as activity,
                // otherwise hiding the controls would bring them straight back.
                if (GetCursorPos(out var sp) && (sp.X != _lastMoveX || sp.Y != _lastMoveY))
                {
                    _lastMoveX = sp.X;
                    _lastMoveY = sp.Y;
                    MouseMoved?.Invoke(ToDip(lParam));
                }
                break;
            case WM_MOUSELEAVE:
                _trackingLeave = false;
                MouseLeft?.Invoke();
                break;
            case WM_LBUTTONDOWN:
                LeftButtonDown?.Invoke(ToDip(lParam));
                handled = true;
                return IntPtr.Zero;
            case WM_LBUTTONUP:
                LeftButtonUp?.Invoke(ToDip(lParam));
                handled = true;
                return IntPtr.Zero;
            case WM_LBUTTONDBLCLK:
                LeftDoubleClick?.Invoke(ToDip(lParam));
                handled = true;
                return IntPtr.Zero;
            case WM_RBUTTONUP:
                RightButtonUp?.Invoke(ToDip(lParam));
                handled = true;
                return IntPtr.Zero;
            case WM_MBUTTONUP:
                MiddleButtonUp?.Invoke();
                handled = true;
                return IntPtr.Zero;
            case WM_MOUSEWHEEL:
                Wheel?.Invoke(HiWord(wParam));
                handled = true;
                return IntPtr.Zero;
        }
        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }
}
