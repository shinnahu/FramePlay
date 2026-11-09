using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FramePlay.Core;
using FramePlay.Themes;

namespace FramePlay.UI.Controls;

/// <summary>Live preview of the subtitle style (font, size, outline, shadow, background, position) over a sample picture.</summary>
public sealed class SubtitlePreview : FrameworkElement
{
    private SubtitleStyle _style = new();
    private string _sample = "Subtitles";

    public void Update(SubtitleStyle style, string sample)
    {
        _style = style;
        _sample = sample;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var scene = new LinearGradientBrush(Color.FromRgb(0x3a, 0x4a, 0x5c), Color.FromRgb(0x12, 0x16, 0x1c), 90);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), 10, 10));
        dc.DrawRectangle(scene, null, new Rect(0, 0, w, h));
        // A soft "hill" so light and dark text are both judged against something realistic.
        var hill = new StreamGeometry();
        using (var c = hill.Open())
        {
            c.BeginFigure(new Point(0, h * 0.72), true, true);
            c.BezierTo(new Point(w * 0.3, h * 0.55), new Point(w * 0.6, h * 0.9), new Point(w, h * 0.66), true, false);
            c.LineTo(new Point(w, h), true, false);
            c.LineTo(new Point(0, h), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x5d, 0x6b, 0x4a)), null, hill);

        // mpv sizes subtitles relative to a 720-line picture; the preview acts as a 720-line picture scaled down.
        double scale = h / 720.0 * 1.6;
        double fontPx = Math.Max(6, _style.FontSize * scale);
        var typeface = new Typeface(new FontFamily(_style.Font), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText(_sample, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontPx, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip) { TextAlignment = TextAlignment.Center, MaxTextWidth = w * 0.9 };
        double margin = h * 0.06;
        double bottom = margin + (h - 2 * margin) * Math.Clamp(_style.Position, 0, 100) / 100.0;
        double top = Math.Clamp(bottom - ft.Height, 0, h - ft.Height);
        var origin = new Point((w - ft.MaxTextWidth) / 2, top);
        var geo = ft.BuildGeometry(origin);

        var text = ColorUtil.Parse(_style.TextColor, Colors.White);
        var outline = ColorUtil.Parse(_style.OutlineColor, Colors.Black);
        if (_style.Background)
        {
            var bg = ColorUtil.Parse(_style.BackgroundColor, Color.FromArgb(0x99, 0, 0, 0));
            var b = geo.Bounds;
            b.Inflate(fontPx * 0.25, fontPx * 0.12);
            dc.DrawRectangle(new SolidColorBrush(bg), null, b);
        }
        if (_style.Shadow && _style.ShadowOffset > 0)
        {
            var shadow = ColorUtil.Parse(_style.ShadowColor, Color.FromArgb(0x80, 0, 0, 0));
            double off = _style.ShadowOffset * scale;
            dc.PushTransform(new TranslateTransform(off, off));
            dc.DrawGeometry(new SolidColorBrush(shadow), _style.OutlineSize > 0 ? new Pen(new SolidColorBrush(shadow), _style.OutlineSize * scale * 2) : null, geo);
            dc.Pop();
        }
        if (_style.OutlineSize > 0 && !_style.Background)
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(outline), _style.OutlineSize * scale * 2) { LineJoin = PenLineJoin.Round }, geo);
        dc.DrawGeometry(new SolidColorBrush(text), null, geo);
        dc.Pop();
    }
}
