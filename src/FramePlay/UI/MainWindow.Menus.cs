using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FramePlay.Input;
using FramePlay.Interop;
using FramePlay.Localization;
using FramePlay.Playback;

namespace FramePlay.UI;

public partial class MainWindow
{
    private string Keys(string actionId)
    {
        var first = Gesture.ParseList(HotkeyMap.GesturesFor(actionId, _settings.Hotkeys)).FirstOrDefault();
        return first == default ? "" : first.Display;
    }

    private static MenuItem Item(string header, Action onClick, string gesture = "", bool isChecked = false, bool checkable = false, bool enabled = true)
    {
        var mi = new MenuItem { Header = header, InputGestureText = gesture, IsCheckable = checkable, IsChecked = isChecked, IsEnabled = enabled };
        mi.Click += (_, _) => onClick();
        return mi;
    }

    // =============================================================================================
    // App menu (logo / "...")
    // =============================================================================================
    private void AppMenu_Click(object sender, RoutedEventArgs e)
    {
        var anchor = (FrameworkElement)sender;
        if (ClickClosedItsMenu(anchor)) return;
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom, VerticalOffset = 2 };
        menu.Items.Add(Item(Loc.T("menu.open"), OpenFiles, Keys("open_file")));
        menu.Items.Add(Item(Loc.T("menu.openFolder"), OpenFolder, Keys("open_folder")));
        menu.Items.Add(new Separator());
        bool video = Player.HasVideo && !Player.IsMidi;
        menu.Items.Add(Item(Loc.T("menu.addSubtitle"), AddSubtitleFile, enabled: video));
        menu.Items.Add(Item(Loc.T("menu.addAudio"), AddAudioFile, enabled: Player.Current != null && !Player.IsMidi));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("menu.settings"), ShowSettings, Keys("settings")));
        menu.Items.Add(Item(Loc.T("menu.about"), () => ShowSettings("about")));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("menu.exit"), Close, "Alt+F4"));
        OpenMenu(menu, anchor);
    }

    // ---------------------------------------------------------------------------------------------
    // Menu buttons work as toggles: clicking the button again closes its menu. The click that closes a
    // menu also reaches the button underneath, which would open the menu again, so that click is
    // remembered and skipped.
    // ---------------------------------------------------------------------------------------------
    private ContextMenu? _anchoredMenu;            // the open (or closing) button menu
    private FrameworkElement? _anchoredMenuButton;
    private FrameworkElement? _menuClosedOnButton; // the button the mouse was on when its menu closed
    private long _menuClosedAt;

    private void OpenMenu(ContextMenu menu, FrameworkElement anchor)
    {
        _anchoredMenu = menu;
        _anchoredMenuButton = anchor;
        MenuButton.SetIsMenuOpen(anchor, true);
        menu.Closed += (_, _) =>
        {
            MenuButton.SetIsMenuOpen(anchor, false);
            if (ReferenceEquals(_anchoredMenu, menu)) { _anchoredMenu = null; _anchoredMenuButton = null; }
            _menuClosedOnButton = CursorOver(anchor) ? anchor : null;
            _menuClosedAt = Environment.TickCount64;
        };
        menu.IsOpen = true;
    }

    /// <summary>True when this click on the button is the one that closed (or is closing) its menu.</summary>
    private bool ClickClosedItsMenu(FrameworkElement anchor)
    {
        // The click arrived before the menu finished closing.
        if (ReferenceEquals(_anchoredMenuButton, anchor))
        {
            if (_anchoredMenu is { IsOpen: true } open) open.IsOpen = false;
            _anchoredMenu = null;
            _anchoredMenuButton = null;
            return true;
        }
        // The menu closed first, because of this click on its button.
        bool closedIt = ReferenceEquals(_menuClosedOnButton, anchor) && Environment.TickCount64 - _menuClosedAt < 600;
        _menuClosedOnButton = null;
        return closedIt;
    }

    private static bool CursorOver(FrameworkElement el)
    {
        if (!el.IsVisible || PresentationSource.FromVisual(el) == null) return false;
        Win32.GetCursorPos(out var p);
        var topLeft = el.PointToScreen(new Point(0, 0));
        var bottomRight = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
        return p.X >= topLeft.X && p.X < bottomRight.X && p.Y >= topLeft.Y && p.Y < bottomRight.Y;
    }

    // =============================================================================================
    // Right-click on the video
    // =============================================================================================
    private void ShowVideoMenu()
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        bool video = Player.HasVideo && !Player.IsMidi && Player.Current != null;
        menu.Items.Add(Item(Loc.T("ctx.playPause"), Player.PlayPause, Keys("play_pause"), enabled: Player.Current != null));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("ctx.screenshot"), () => Player.TakeScreenshot(false), Keys("screenshot"), enabled: video));
        menu.Items.Add(Item(Loc.T("ctx.screenshotSubs"), () => Player.TakeScreenshot(true), enabled: video));
        menu.Items.Add(Item(Loc.T("ctx.openScreenshots"), OpenScreenshotFolder));
        menu.Items.Add(new Separator());

        var audio = new MenuItem { Header = Loc.T("ctx.audio"), IsEnabled = Player.Current != null && !Player.IsMidi };
        FillAudioItems(audio.Items);
        menu.Items.Add(audio);
        var subs = new MenuItem { Header = Loc.T("ctx.subtitles"), IsEnabled = video };
        FillSubtitleItems(subs.Items);
        menu.Items.Add(subs);
        var speed = new MenuItem { Header = Loc.T("ctx.speed"), IsEnabled = Player.Current != null };
        FillSpeedItems(speed.Items);
        menu.Items.Add(speed);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("ctx.pip"), TogglePip, Keys("pip"), isChecked: _pip, checkable: true, enabled: video));
        menu.Items.Add(Item(Loc.T("ctx.fullscreen"), ToggleFullscreen, Keys("fullscreen"), isChecked: _fullscreen, checkable: true));
        menu.Items.Add(Item(Loc.T("ctx.onTop"), ToggleManualTopmost, Keys("on_top"), isChecked: _manualTopmost, checkable: true));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("menu.open"), OpenFiles, Keys("open_file")));
        if (Player.Current != null)
            menu.Items.Add(Item(Loc.T("ctx.info"), () => Clipboard.SetText(Player.Current!.Path)));
        menu.Items.Add(Item(Loc.T("menu.settings"), ShowSettings, Keys("settings")));
        menu.IsOpen = true;
    }

    private void OpenScreenshotFolder()
    {
        try
        {
            Directory.CreateDirectory(Player.ScreenshotFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Player.ScreenshotFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Core.Log.Error("Open screenshots", ex); }
    }

    // =============================================================================================
    // Track pickers (control bar buttons)
    // =============================================================================================
    /// <summary>Opens a menu above a control-bar button, right-aligned with it so it stays inside the window.</summary>
    private static ContextMenu MenuAbove(FrameworkElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Custom };
        // The menu template keeps a 10 px transparent margin on the right and bottom for its shadow.
        menu.CustomPopupPlacementCallback = (popup, target, _) =>
        [
            new CustomPopupPlacement(new Point(target.Width - popup.Width + 10, -popup.Height + 4), PopupPrimaryAxis.Horizontal),
            new CustomPopupPlacement(new Point(0, -popup.Height + 4), PopupPrimaryAxis.Horizontal),
        ];
        return menu;
    }

    public void ShowAudioMenu(FrameworkElement anchor)
    {
        if (ClickClosedItsMenu(anchor)) return;
        var menu = MenuAbove(anchor);
        menu.Items.Add(Header(Loc.T("tracks.audio")));
        FillAudioItems(menu.Items);
        OpenMenu(menu, anchor);
    }

    public void ShowSubtitlesMenu(FrameworkElement anchor)
    {
        if (ClickClosedItsMenu(anchor)) return;
        var menu = MenuAbove(anchor);
        menu.Items.Add(Header(Loc.T("tracks.subtitles")));
        FillSubtitleItems(menu.Items);
        OpenMenu(menu, anchor);
    }

    public void ShowSpeedMenu(FrameworkElement anchor)
    {
        if (ClickClosedItsMenu(anchor)) return;
        var menu = MenuAbove(anchor);
        menu.Items.Add(Header(Loc.T("ctx.speed")));
        FillSpeedItems(menu.Items);
        OpenMenu(menu, anchor);
    }

    private static MenuItem Header(string text) => new()
    {
        Header = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(-22, 0, 0, 0) },
        IsEnabled = false,
        Style = (Style)Application.Current.FindResource("PlainMenuItem"),
    };

    private string TrackLabel(TrackInfo t)
    {
        var label = t.Label;
        var flags = new List<string>();
        if (t.IsDefault) flags.Add(Loc.T("tracks.default"));
        if (t.IsForced) flags.Add(Loc.T("tracks.forced"));
        if (t.IsExternal) flags.Add(Loc.T("tracks.external"));
        if (t.Type == "audio" && t.Channels > 0) flags.Add(t.Channels switch { 1 => "mono", 2 => "stereo", 6 => "5.1", 8 => "7.1", _ => t.Channels + " ch" });
        if (!string.IsNullOrWhiteSpace(t.Codec)) flags.Add(t.Codec!);
        return flags.Count > 0 ? $"{label}  ·  {string.Join(", ", flags)}" : label;
    }

    private void FillAudioItems(ItemCollection items)
    {
        var tracks = Player.Tracks.Where(t => t.Type == "audio").ToList();
        if (tracks.Count == 0) items.Add(new MenuItem { Header = Loc.T("tracks.none"), IsEnabled = false });
        foreach (var t in tracks)
            items.Add(Item(TrackLabel(t), () => Player.SelectAudioTrack(t.Id), isChecked: t.Selected, checkable: true));
        items.Add(new Separator());
        items.Add(DelayRow(Loc.T("tracks.delay"), () => Player.AudioDelay, Player.SetAudioDelay));
        items.Add(Item(Loc.T("tracks.addFile"), AddAudioFile, enabled: Player.Current != null && !Player.IsMidi));
    }

    private void FillSubtitleItems(ItemCollection items)
    {
        var tracks = Player.Tracks.Where(t => t.Type == "sub").ToList();
        items.Add(Item(Loc.T("tracks.off"), () => Player.SelectSubtitleTrack(null), isChecked: Player.SubtitleTrackId is "no" or "", checkable: true));
        foreach (var t in tracks.Where(t => t.MainSelection != 1 || !t.Selected))
            items.Add(Item(TrackLabel(t), () => Player.SelectSubtitleTrack(t.Id), isChecked: t.Selected && t.MainSelection == 0, checkable: true));
        // Second subtitle, shown at the top of the picture (two languages at once).
        var second = new MenuItem { Header = Loc.T("tracks.secondary"), IsEnabled = tracks.Count > 0 };
        second.Items.Add(Item(Loc.T("tracks.off"), () => Player.SelectSecondarySubtitleTrack(null),
            isChecked: Player.SecondarySubtitleTrackId is "no" or "", checkable: true));
        foreach (var t in tracks)
            second.Items.Add(Item(TrackLabel(t), () => Player.SelectSecondarySubtitleTrack(t.Id), isChecked: t.Selected && t.MainSelection == 1, checkable: true));
        items.Add(second);
        items.Add(new Separator());
        items.Add(DelayRow(Loc.T("tracks.delay"), () => Player.SubtitleDelay, Player.SetSubtitleDelay));
        items.Add(AdjustRow(Loc.T("tracks.height"), "↓", "↑", SubtitleHeightText,
            down: () => MoveSubtitles(+1, toast: false),
            up: () => MoveSubtitles(-1, toast: false),
            reset: () => { Player.SetSubtitlePosition(new Core.SubtitleStyle().Position); SaveSettingsSoon(); }));
        items.Add(Item(Loc.T("tracks.addFile"), AddSubtitleFile));
    }

    private void FillSpeedItems(ItemCollection items)
    {
        foreach (var s in Speeds)
        {
            var label = Math.Abs(s - 1) < 0.001 ? Loc.T("speed.normal") : s.ToString("0.##", CultureInfo.CurrentCulture) + "×";
            items.Add(Item(label, () => Player.SetSpeed(s), isChecked: Math.Abs(Player.Speed - s) < 0.001, checkable: true));
        }
    }

    /// <summary>"Delay  [−]  +0.050 s  [+]  Reset" row that stays open while you adjust it (50 ms steps).</summary>
    private MenuItem DelayRow(string label, Func<double> get, Action<double> set)
    {
        // mpv reports the new value asynchronously, so the row keeps its own copy while it's open.
        double current = get();
        void Apply(double v)
        {
            current = Math.Round(v, 3);
            set(current);
        }
        return AdjustRow(label, "−", "+", () => TimeFormat.Delay(current),
            down: () => Apply(current - 0.05), up: () => Apply(current + 0.05), reset: () => Apply(0));
    }

    /// <summary>"Label  [down]  value  [up]  Reset" menu row that stays open while you adjust it.</summary>
    private MenuItem AdjustRow(string label, string downText, string upText, Func<string> text, Action down, Action up, Action reset)
    {
        var value = new TextBlock { MinWidth = 76, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextStrongBrush"), Text = text() };
        value.SetValue(System.Windows.Documents.Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
        Button Small(string content, Action a)
        {
            var b = new Button { Content = content, Style = (Style)FindResource("TextButton"), Padding = new Thickness(9, 2, 9, 3), MinWidth = 30, Focusable = false };
            b.Click += (_, e) => { a(); value.Text = text(); e.Handled = true; };
            return b;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), MinWidth = 56 });
        row.Children.Add(Small(downText, down));
        row.Children.Add(value);
        row.Children.Add(Small(upText, up));
        var resetButton = Small(Loc.T("tracks.reset"), reset);
        resetButton.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(resetButton);
        return new MenuItem { Header = row, StaysOpenOnClick = true, Style = (Style)FindResource("PlainMenuItem") };
    }
}
