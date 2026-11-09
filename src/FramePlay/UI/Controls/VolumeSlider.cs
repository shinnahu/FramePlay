using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FramePlay.UI.Controls;

/// <summary>
/// A modern volume slider: a thin rounded track, an accent fill and a
/// round knob that grows on hover, with a percentage bubble while dragging. 0 to 200 %; the part above
/// 100 % (amplification) is drawn in the theme's warning colour, with a small mark at 100 %.
/// </summary>
public sealed class VolumeSlider : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((VolumeSlider)d).UpdateBubble()));
    public static readonly DependencyProperty MutedProperty = DependencyProperty.Register(nameof(Muted), typeof(bool), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BoostBrushProperty = DependencyProperty.Register(nameof(BoostBrush), typeof(Brush), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty KnobBrushProperty = DependencyProperty.Register(nameof(KnobBrush), typeof(Brush), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(VolumeSlider),
        new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool Muted { get => (bool)GetValue(MutedProperty); set => SetValue(MutedProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush BoostBrush { get => (Brush)GetValue(BoostBrushProperty); set => SetValue(BoostBrushProperty, value); }
    public Brush KnobBrush { get => (Brush)GetValue(KnobBrushProperty); set => SetValue(KnobBrushProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public event Action<double>? ValueRequested;

    private const double TrackHeight = 4, KnobSize = 12, KnobHover = 15, Pad = 8;
    private bool _dragging, _hover;
    private readonly Popup _bubble;
    private readonly TextBlock _bubbleText;

    public VolumeSlider()
    {
        Focusable = false;
        Cursor = Cursors.Hand;
        _bubbleText = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(7, 3, 7, 4) };
        _bubbleText.SetResourceReference(TextBlock.ForegroundProperty, "TextStrongBrush");
        var border = new Border { Child = _bubbleText, BorderThickness = new Thickness(1), Margin = new Thickness(4) };
        border.SetResourceReference(Border.BackgroundProperty, "Bg0Brush");
        border.SetResourceReference(Border.BorderBrushProperty, "MenuLineBrush");
        border.SetResourceReference(Border.CornerRadiusProperty, "CornerRadiusSmall");
        border.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Direction = 270, Opacity = 0.35 };
        _bubble = new Popup
        {
            Child = border, AllowsTransparency = true, Placement = PlacementMode.Relative, PlacementTarget = this,
            IsHitTestVisible = false, Focusable = false, StaysOpen = true, PopupAnimation = PopupAnimation.None,
        };
    }

    private double TrackLeft => Pad;
    private double TrackWidth => Math.Max(1, ActualWidth - Pad * 2);
    private double XFor(double v) => TrackLeft + TrackWidth * Math.Clamp(v / Maximum, 0, 1);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // whole area is clickable
        double cy = h / 2, r = TrackHeight / 2;
        var track = new Rect(TrackLeft, cy - r, TrackWidth, TrackHeight);
        dc.DrawRoundedRectangle(TrackBrush, null, track, r, r);

        double x = XFor(Value), x100 = XFor(100);
        double opacity = Muted ? 0.35 : 1;
        if (opacity < 1) dc.PushOpacity(opacity);
        dc.PushClip(new RectangleGeometry(track, r, r));
        dc.DrawRectangle(FillBrush, null, new Rect(TrackLeft, cy - r, Math.Max(0, Math.Min(x, x100) - TrackLeft), TrackHeight));
        if (x > x100) dc.DrawRectangle(BoostBrush, null, new Rect(x100, cy - r, x - x100, TrackHeight));
        dc.Pop();
        if (opacity < 1) dc.Pop();

        // Small mark at 100 % (normal loudness).
        dc.DrawEllipse(KnobBrush, null, new Point(x100, cy), 1.4, 1.4);

        double k = (_hover || _dragging ? KnobHover : KnobSize) / 2;
        var shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        dc.DrawEllipse(shadow, null, new Point(x, cy + 1), k + 0.5, k + 0.5);
        dc.DrawEllipse(KnobBrush, null, new Point(x, cy), k, k);
    }

    private void SetFrom(Point p)
    {
        double v = Math.Clamp((p.X - TrackLeft) / TrackWidth, 0, 1) * Maximum;
        if (Math.Abs(v - 100) < 3) v = 100; // snap to 100 %
        ValueRequested?.Invoke(Math.Round(v));
    }

    private void UpdateBubble()
    {
        if (!_bubble.IsOpen) return;
        _bubbleText.Text = $"{Value:0}%";
        _bubble.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _bubble.Child.DesiredSize;
        _bubble.HorizontalOffset = XFor(Value) - size.Width / 2;
        _bubble.VerticalOffset = -size.Height - 2;
    }

    private void ShowBubble(bool show)
    {
        _bubble.IsOpen = show;
        if (show) UpdateBubble();
    }

    protected override void OnMouseEnter(MouseEventArgs e) { _hover = true; InvalidateVisual(); ShowBubble(true); }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = false;
        InvalidateVisual();
        if (!_dragging) ShowBubble(false);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        CaptureMouse();
        _dragging = true;
        SetFrom(e.GetPosition(this));
        ShowBubble(true);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) SetFrom(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        if (!_hover) ShowBubble(false);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _dragging = false;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ValueRequested?.Invoke(Math.Clamp(Value + (e.Delta > 0 ? 5 : -5), 0, Maximum));
        ShowBubble(true);
        e.Handled = true;
    }
}
