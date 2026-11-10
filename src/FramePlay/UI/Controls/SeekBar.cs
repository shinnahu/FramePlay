using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FramePlay.UI.Controls;

/// <summary>
/// The seek bar: a thin rounded line with the played part in the accent colour, as thin as the volume track.
/// Under the mouse it thickens, a round handle (in <see cref="HoverBrush"/>) appears and <see cref="FillBrush"/>
/// lights the stretch up to the pointer. Click seeks exactly; dragging scrubs (fast keyframe seeks), releasing lands exactly.
/// </summary>
public sealed class SeekBar : FrameworkElement
{
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SeekBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(SeekBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(SeekBar),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(SeekBar),
        new FrameworkPropertyMetadata(Brushes.DarkOrange, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ThumbBrushProperty = DependencyProperty.Register(nameof(ThumbBrush), typeof(Brush), typeof(SeekBar),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HoverBrushProperty = DependencyProperty.Register(nameof(HoverBrush), typeof(Brush), typeof(SeekBar),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ThinProperty = DependencyProperty.Register(nameof(Thin), typeof(bool), typeof(SeekBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush ThumbBrush { get => (Brush)GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }
    public Brush HoverBrush { get => (Brush)GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    /// <summary>Thin variant for the picture-in-picture window.</summary>
    public bool Thin { get => (bool)GetValue(ThinProperty); set => SetValue(ThinProperty, value); }

    /// <summary>(seconds, exact). exact=false while dragging.</summary>
    public event Action<double, bool>? SeekRequested;
    /// <summary>(seconds, x position in this control) while the mouse hovers or drags.</summary>
    public event Action<double, double>? HoverChanged;
    public event Action? HoverEnded;
    public event Action<bool>? DraggingChanged;

    private bool _dragging;
    private double _hoverX = -1;
    private DateTime _lastDragSeek;

    public bool IsDragging => _dragging;

    public SeekBar()
    {
        Focusable = false;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    private double TimeAt(double x)
    {
        double w = Math.Max(1, ActualWidth);
        return Math.Clamp(x / w, 0, 1) * Math.Max(0, Maximum);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double frac = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        // Transparent hit area so the whole height is clickable.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        if (Thin)
        {
            double thin = _hoverX >= 0 || _dragging ? 6 : 4;
            var r = new Rect(0, h - thin, w, thin);
            dc.DrawRectangle(TrackBrush, null, r);
            dc.DrawRectangle(ThumbBrush, null, new Rect(0, h - thin, w * frac, thin));
            return;
        }

        // A thin rounded track across the middle; the whole height of the control still takes clicks.
        // Under the mouse the track thickens, the handle grows, and the stretch up to the pointer lights up.
        bool active = _hoverX >= 0 || _dragging;
        double th = active ? TrackActive : TrackIdle, tr = th / 2, cy = h / 2;
        var track = new Rect(0, cy - tr, w, th);
        dc.DrawRoundedRectangle(TrackBrush, null, track, tr, tr);
        double fx = w * frac;
        dc.PushClip(new RectangleGeometry(track, tr, tr));
        if (_hoverX > fx && !_dragging) dc.DrawRectangle(FillBrush, null, new Rect(fx, cy - tr, Math.Min(_hoverX, w) - fx, th));
        if (fx > 0) dc.DrawRectangle(ThumbBrush, null, new Rect(0, cy - tr, fx, th));
        dc.Pop();
        if (!active) return; // at rest just the line, like the volume track; the handle shows under the mouse
        const double k = Knob / 2;
        dc.DrawEllipse(KnobShadow, null, new Point(fx, cy + 1), k + 0.5, k + 0.5);
        dc.DrawEllipse(HoverBrush, null, new Point(fx, cy), k, k);
    }

    private const double TrackIdle = 4, TrackActive = 6, Knob = 14;
    private static readonly Brush KnobShadow = Frozen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)));
    private static Brush Frozen(Brush b) { b.Freeze(); return b; }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Maximum <= 0) return;
        CaptureMouse();
        _dragging = true;
        DraggingChanged?.Invoke(true);
        var x = e.GetPosition(this).X;
        Value = TimeAt(x);
        SeekRequested?.Invoke(Value, false);
        _lastDragSeek = DateTime.UtcNow;
        HoverChanged?.Invoke(Value, x);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var x = e.GetPosition(this).X;
        _hoverX = x;
        double t = TimeAt(x);
        if (_dragging)
        {
            Value = t;
            if ((DateTime.UtcNow - _lastDragSeek).TotalMilliseconds > 60)
            {
                SeekRequested?.Invoke(t, false);
                _lastDragSeek = DateTime.UtcNow;
            }
        }
        else InvalidateVisual();
        if (Maximum > 0) HoverChanged?.Invoke(t, x);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        DraggingChanged?.Invoke(false);
        double t = TimeAt(e.GetPosition(this).X);
        Value = t;
        SeekRequested?.Invoke(t, true);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            DraggingChanged?.Invoke(false);
            SeekRequested?.Invoke(Value, true);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hoverX = -1;
        InvalidateVisual();
        if (!_dragging) HoverEnded?.Invoke();
    }
}
