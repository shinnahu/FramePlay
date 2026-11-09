using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace FramePlay.UI;

/// <summary>
/// Tooltips for the icon buttons. WPF's own tooltips (since .NET 8) stay open while the mouse is anywhere
/// between a button and its tooltip, and that area reaches into the neighbouring button, so sliding along
/// a row kept showing the previous button's text. These always belong to the button under the mouse.
/// A button opts in by turning WPF's tooltip off (ToolTipService.IsEnabled = False in its style); the text
/// still comes from its ToolTip property.
/// </summary>
public static class HoverTip
{
    private static readonly Popup Popup = new()
    {
        AllowsTransparency = true, Placement = PlacementMode.Custom, IsHitTestVisible = false, StaysOpen = true, Focusable = false,
    };
    private static readonly TextBlock Text = new() { FontSize = 12 };
    private static readonly DispatcherTimer ShowTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private static readonly DispatcherTimer Watch = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private static ButtonBase? _pending, _shown;
    private static long _hiddenAt;

    public static void Install()
    {
        var border = new Border { Padding = new Thickness(8, 5, 8, 5), BorderThickness = new Thickness(1), Child = Text };
        border.SetResourceReference(Border.BackgroundProperty, "Bg0Brush");
        border.SetResourceReference(Border.BorderBrushProperty, "MenuLineBrush");
        border.SetResourceReference(Border.CornerRadiusProperty, "CornerRadiusSmall");
        Text.SetResourceReference(TextBlock.ForegroundProperty, "TextStrongBrush");
        Text.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
        Popup.Child = border;
        Popup.CustomPopupPlacementCallback = Place;
        ShowTimer.Tick += (_, _) => { ShowTimer.Stop(); if (_pending != null) Show(_pending); };
        // Safety net: if the button disappears or the window moves away without a MouseLeave, hide.
        Watch.Tick += (_, _) => { if (_shown == null || !_shown.IsVisible || !_shown.IsMouseOver) Hide(); };

        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseEnterEvent, new MouseEventHandler((s, _) => OnEnter((ButtonBase)s)));
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent, new MouseEventHandler((s, _) => OnLeave((ButtonBase)s)));
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => Hide()), true);
    }

    private static bool OptedIn(ButtonBase b) => !ToolTipService.GetIsEnabled(b) && b.ToolTip is string { Length: > 0 };

    private static void OnEnter(ButtonBase b)
    {
        if (!OptedIn(b)) return;
        _pending = b;
        ShowTimer.Stop();
        // Moving along a row of buttons: the next tooltip shows straight away.
        if (Popup.IsOpen || Environment.TickCount64 - _hiddenAt < 400) Show(b);
        else ShowTimer.Start();
    }

    private static void OnLeave(ButtonBase b)
    {
        if (ReferenceEquals(_pending, b)) { _pending = null; ShowTimer.Stop(); }
        if (ReferenceEquals(_shown, b)) Hide();
    }

    private static void Show(ButtonBase b)
    {
        _pending = null;
        if (!b.IsMouseOver || !b.IsVisible || b.ToolTip is not string text) return;
        _shown = b;
        Text.Text = text;
        Popup.IsOpen = false;
        Popup.PlacementTarget = b;
        Popup.IsOpen = true;
        Watch.Start();
    }

    private static void Hide()
    {
        ShowTimer.Stop();
        Watch.Stop();
        _pending = null;
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
            _hiddenAt = Environment.TickCount64;
        }
        _shown = null;
    }

    /// <summary>Centred above the button, or below it for buttons at the top of their window.</summary>
    private static CustomPopupPlacement[] Place(Size popup, Size target, Point offset)
    {
        double x = (target.Width - popup.Width) / 2;
        var above = new CustomPopupPlacement(new Point(x, -popup.Height - 6), PopupPrimaryAxis.Horizontal);
        var below = new CustomPopupPlacement(new Point(x, target.Height + 6), PopupPrimaryAxis.Horizontal);
        return _shown != null && NearTop(_shown) ? [below, above] : [above, below];
    }

    private static bool NearTop(FrameworkElement el)
    {
        var w = Window.GetWindow(el);
        if (w == null) return false;
        try { return el.TranslatePoint(new Point(0, 0), w).Y < 48; }
        catch (InvalidOperationException) { return false; }
    }
}
