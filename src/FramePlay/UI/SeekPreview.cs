using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FramePlay.Playback;
using FramePlay.UI.Controls;

namespace FramePlay.UI;

/// <summary>The hover preview above the seek bar: a video frame (for videos) and the time under the mouse.</summary>
public sealed class SeekPreview
{
    private const double ThumbWidth = 208;
    private readonly PlayerController _player;
    private readonly Popup _popup;
    private readonly Border _frame;
    private readonly Image _image;
    private readonly Border _imageBox;
    private readonly TextBlock _time;
    private ThumbnailService? _thumbs;
    private double _wanted = -1;

    public SeekPreview(PlayerController player)
    {
        _player = player;
        _image = new Image { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _imageBox = new Border
        {
            Width = ThumbWidth, Height = ThumbWidth * 9 / 16, Background = Brushes.Black, ClipToBounds = true,
            CornerRadius = new CornerRadius(6), Child = _image,
        };
        _time = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8, 6, 8, 2), FontWeight = FontWeights.SemiBold };
        _time.SetResourceReference(TextBlock.ForegroundProperty, "TextStrongBrush");
        _time.SetValue(System.Windows.Documents.Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
        var stack = new StackPanel();
        stack.Children.Add(_imageBox);
        stack.Children.Add(_time);
        _frame = new Border { Padding = new Thickness(4), BorderThickness = new Thickness(1), Child = stack, Margin = new Thickness(0, 0, 0, 0) };
        _frame.SetResourceReference(Border.BackgroundProperty, "Bg0Brush");
        _frame.SetResourceReference(Border.BorderBrushProperty, "MenuLineBrush");
        _frame.SetResourceReference(Border.CornerRadiusProperty, "CornerRadius");
        _popup = new Popup
        {
            Child = _frame, AllowsTransparency = true, Placement = PlacementMode.Relative, IsHitTestVisible = false,
            StaysOpen = true, Focusable = false, PopupAnimation = PopupAnimation.None,
        };
    }

    public void Show(SeekBar bar, double seconds, double x)
    {
        bool video = _player.HasVideo && !_player.IsMidi && _player.Current != null;
        _imageBox.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        var mode = TimeFormat.Parse(_player.Settings.TimeDisplay);
        if (mode != TimeDisplayMode.Time && _player.Fps <= 0) mode = TimeDisplayMode.Time;
        _time.Text = TimeFormat.Format(seconds, mode, _player.Fps, _player.Duration >= 3600);

        if (video)
        {
            double aspect = _player.VideoWidth > 0 && _player.VideoHeight > 0 ? (double)_player.VideoWidth / _player.VideoHeight : 16.0 / 9;
            _imageBox.Height = Math.Round(ThumbWidth / Math.Clamp(aspect, 0.5, 3.0));
            _wanted = seconds;
            double scale = VisualTreeHelper.GetDpi(bar).DpiScaleX;
            int pw = (int)Math.Round(ThumbWidth * scale);
            int ph = (int)Math.Round(_imageBox.Height * scale);
            _thumbs ??= new ThumbnailService(bar.Dispatcher);
            _thumbs.Get(_player.Current!.Path, seconds, pw, ph, (t, bmp) =>
            {
                // Keep showing the last frame until the one for the current spot arrives.
                if (bmp != null && Math.Abs(t - _wanted) < 30) _image.Source = bmp;
            });
        }

        _frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _frame.DesiredSize;
        _popup.PlacementTarget = bar;
        _popup.HorizontalOffset = Math.Round(x - size.Width / 2);
        _popup.VerticalOffset = -size.Height - 10;
        _popup.IsOpen = true;
    }

    public void Hide()
    {
        _popup.IsOpen = false;
        _wanted = -1;
    }

    /// <summary>Called when the file changes so a stale frame is never shown for a new video.</summary>
    public void Reset() => _image.Source = null;

    public void Dispose() => _thumbs?.Dispose();
}
