using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FramePlay.UI.Controls;

/// <summary>
/// The thick seek bar from the redesign: a dark track, the played part filled with a dim accent and an
/// accent handle. Click seeks exactly; dragging scrubs (fast keyframe seeks), releasing lands exactly.
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
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(SeekBar),
        new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ThinProperty = DependencyProperty.Register(nameof(Thin), typeof(bool), typeof(SeekBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush ThumbBrush { get => (Brush)GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }
    public Brush HoverBrush { get => (Brush)GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
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

        // The track sits a few pixels inside the control so the handle can stand out above and below it.
        const double inset = 4;
        double th = h - inset * 2;
        double cr = Math.Min(CornerRadius, th / 2);
        var track = new Rect(0, inset, w, th);
        dc.DrawRoundedRectangle(TrackBrush, null, track, cr, cr);
        double fx = w * frac;
        if (fx > 0)
        {
            dc.PushClip(new RectangleGeometry(track, cr, cr));
            dc.DrawRectangle(FillBrush, null, new Rect(0, inset, fx, th));
            dc.Pop();
        }
        if (_hoverX >= 0 && !_dragging)
        {
            var hb = HoverBrush.Clone();
            hb.Opacity = 0.35;
            dc.DrawRectangle(hb, null, new Rect(Math.Clamp(_hoverX, 0, w - 1), inset + 3, 1, th - 6));
        }
        // Handle: a pill taller than the track (it covers the full height and pokes out a little).
        const double tw = 7;
        double tx = Math.Clamp(fx - tw / 2, 0, w - tw);
        dc.DrawRoundedRectangle(ThumbBrush, null, new Rect(tx, 0, tw, h), tw / 2, tw / 2);
    }

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
