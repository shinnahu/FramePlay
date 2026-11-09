using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FramePlay.Themes;

namespace FramePlay.UI.Controls;

/// <summary>A compact HSV colour picker (with optional alpha) shown in a popup next to a swatch button.</summary>
public sealed class ColorPicker : Border
{
    private const double SvWidth = 220, SvHeight = 150, BarHeight = 14;
    private readonly bool _alpha;
    private double _h, _s, _v, _a = 1;
    private readonly Border _svHue;
    private readonly Ellipse _svMarker;
    private readonly Canvas _svCanvas;
    private readonly Rectangle _hueMarker, _alphaMarker;
    private readonly Canvas _hueCanvas, _alphaCanvas;
    private readonly LinearGradientBrush _alphaBrush;
    private readonly TextBox _hex;
    private bool _updating;

    public event Action<Color>? ColorChanged;

    public ColorPicker(Color initial, bool allowAlpha)
    {
        _alpha = allowAlpha;
        Padding = new Thickness(12);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "Bg0Brush");
        SetResourceReference(BorderBrushProperty, "MenuLineBrush");
        SetResourceReference(CornerRadiusProperty, "CornerRadius");
        Margin = new Thickness(0, 0, 10, 10);
        Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.4 };

        var stack = new StackPanel();

        // Saturation / value square.
        _svHue = new Border { Width = SvWidth, Height = SvHeight };
        var white = new Border { Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
        var black = new Border { Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
        _svMarker = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        _svMarker.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8 };
        _svCanvas = new Canvas { Width = SvWidth, Height = SvHeight, Background = Brushes.Transparent, ClipToBounds = true };
        _svCanvas.Children.Add(_svMarker);
        var sv = new Grid { Width = SvWidth, Height = SvHeight, Cursor = Cursors.Cross };
        sv.Children.Add(_svHue);
        sv.Children.Add(white);
        sv.Children.Add(black);
        sv.Children.Add(_svCanvas);
        Drag(sv, p => { _s = Math.Clamp(p.X / SvWidth, 0, 1); _v = 1 - Math.Clamp(p.Y / SvHeight, 0, 1); Changed(); });
        stack.Children.Add(new Border { Child = sv, CornerRadius = new CornerRadius(6), ClipToBounds = true });

        // Hue bar.
        var hueBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++) hueBrush.GradientStops.Add(new GradientStop(Hsv(i * 60 % 360, 1, 1), i / 6.0));
        _hueMarker = Marker();
        _hueCanvas = new Canvas { Width = SvWidth, Height = BarHeight, Background = hueBrush, Cursor = Cursors.Hand };
        _hueCanvas.Children.Add(_hueMarker);
        Drag(_hueCanvas, p => { _h = Math.Clamp(p.X / SvWidth, 0, 1) * 360; Changed(); });
        stack.Children.Add(new Border { Child = _hueCanvas, CornerRadius = new CornerRadius(4), ClipToBounds = true, Margin = new Thickness(0, 10, 0, 0) });

        // Alpha bar (over a checkerboard).
        _alphaBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        _alphaMarker = Marker();
        _alphaCanvas = new Canvas { Width = SvWidth, Height = BarHeight, Background = _alphaBrush, Cursor = Cursors.Hand };
        _alphaCanvas.Children.Add(_alphaMarker);
        Drag(_alphaCanvas, p => { _a = Math.Clamp(p.X / SvWidth, 0, 1); Changed(); });
        if (allowAlpha)
        {
            var checker = new Grid { Width = SvWidth, Height = BarHeight, Background = Checkerboard() };
            checker.Children.Add(_alphaCanvas);
            stack.Children.Add(new Border { Child = checker, CornerRadius = new CornerRadius(4), ClipToBounds = true, Margin = new Thickness(0, 8, 0, 0) });
        }

        // Hex value and quick swatches.
        _hex = new TextBox { Margin = new Thickness(0, 10, 0, 0), FontFamily = new FontFamily("Cascadia Mono, Consolas") };
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyHex(); };
        _hex.LostKeyboardFocus += (_, _) => ApplyHex();
        stack.Children.Add(_hex);

        var swatches = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), Width = SvWidth };
        foreach (var hex in new[] { "#ffffff", "#000000", "#ffd400", "#ffeb3b", "#00e5ff", "#5865f2", "#3ba55c", "#faa61a", "#ed4245", "#eb459e", "#9b59b6", "#808080" })
        {
            var c = ColorUtil.Parse(hex, Colors.White);
            var b = new Border { Width = 16, Height = 16, Margin = new Thickness(0, 0, 2.2, 2.2), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(c),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            b.MouseLeftButtonDown += (_, _) => { SetColor(Color.FromArgb((byte)Math.Round(_a * 255), c.R, c.G, c.B)); Changed(); };
            swatches.Children.Add(b);
        }
        stack.Children.Add(swatches);
        Child = stack;
        SetColor(initial);
    }

    private static Rectangle Marker() => new()
    {
        Width = 4, Height = BarHeight, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 0.6, RadiusX = 2, RadiusY = 2, IsHitTestVisible = false,
    };

    private static Brush Checkerboard()
    {
        var g = new GeometryGroup();
        g.Children.Add(new RectangleGeometry(new Rect(0, 0, 5, 5)));
        g.Children.Add(new RectangleGeometry(new Rect(5, 5, 5, 5)));
        return new DrawingBrush(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(200, 200, 200)), null, g))
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 10, 10), ViewportUnits = BrushMappingMode.Absolute,
        };
    }

    private static void Drag(FrameworkElement el, Action<Point> onPoint)
    {
        el.MouseLeftButtonDown += (_, e) => { el.CaptureMouse(); onPoint(e.GetPosition(el)); e.Handled = true; };
        el.MouseMove += (_, e) => { if (el.IsMouseCaptured) onPoint(e.GetPosition(el)); };
        el.MouseLeftButtonUp += (_, _) => el.ReleaseMouseCapture();
    }

    public Color Current => WithAlpha(Hsv(_h, _s, _v));

    private Color WithAlpha(Color c) => Color.FromArgb(_alpha ? (byte)Math.Round(_a * 255) : (byte)255, c.R, c.G, c.B);

    public void SetColor(Color c)
    {
        ToHsv(c, out _h, out _s, out _v);
        _a = c.A / 255.0;
        UpdateVisuals();
    }

    private void Changed()
    {
        UpdateVisuals();
        ColorChanged?.Invoke(Current);
    }

    private void UpdateVisuals()
    {
        _updating = true;
        _svHue.Background = new SolidColorBrush(Hsv(_h, 1, 1));
        Canvas.SetLeft(_svMarker, _s * SvWidth - 6);
        Canvas.SetTop(_svMarker, (1 - _v) * SvHeight - 6);
        Canvas.SetLeft(_hueMarker, _h / 360 * SvWidth - 2);
        var rgb = Hsv(_h, _s, _v);
        _alphaBrush.GradientStops.Clear();
        _alphaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, rgb.R, rgb.G, rgb.B), 0));
        _alphaBrush.GradientStops.Add(new GradientStop(rgb, 1));
        Canvas.SetLeft(_alphaMarker, _a * SvWidth - 2);
        _hex.Text = ColorUtil.ToHex(Current);
        _updating = false;
    }

    private void ApplyHex()
    {
        if (_updating) return;
        if (ColorUtil.TryParse(_hex.Text, out var c))
        {
            if (!_alpha) c.A = 255;
            SetColor(c);
            ColorChanged?.Invoke(Current);
        }
        else _hex.Text = ColorUtil.ToHex(Current);
    }

    public static Color Hsv(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static void ToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        s = max == 0 ? 0 : d / max;
        v = max;
    }

    /// <summary>Opens a picker under <paramref name="anchor"/>; <paramref name="onChange"/> runs live as the colour changes.</summary>
    public static Popup Show(FrameworkElement anchor, Color initial, bool allowAlpha, Action<Color> onChange)
    {
        var picker = new ColorPicker(initial, allowAlpha);
        picker.ColorChanged += onChange;
        var popup = new Popup
        {
            Child = picker, PlacementTarget = anchor, Placement = PlacementMode.Bottom, VerticalOffset = 4,
            AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.None,
        };
        popup.IsOpen = true;
        return popup;
    }
}

/// <summary>A small rounded colour swatch that opens <see cref="ColorPicker"/> when clicked.</summary>
public sealed class ColorSwatchButton : Button
{
    private readonly Border _swatch;
    private readonly TextBlock _label;
    private Color _color;

    public bool AllowAlpha { get; set; }
    public event Action<Color>? ColorPicked;

    public ColorSwatchButton(Color color)
    {
        Focusable = false;
        Cursor = Cursors.Hand;
        _swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
        _swatch.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0), FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12 };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_swatch);
        row.Children.Add(_label);
        Content = row;
        SetResourceReference(StyleProperty, "GhostButton");
        Padding = new Thickness(6, 4, 8, 4);
        SetColor(color);
        Click += (_, _) => ColorPicker.Show(this, _color, AllowAlpha, c => { SetColor(c); ColorPicked?.Invoke(c); });
    }

    public void SetColor(Color c)
    {
        _color = c;
        _swatch.Background = new SolidColorBrush(c);
        _label.Text = ColorUtil.ToHex(c);
    }
}
