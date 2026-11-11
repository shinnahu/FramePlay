using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Input;
using FramePlay.Localization;
using FramePlay.Playback;
using FramePlay.Themes;
using FramePlay.UI.Controls;
using Microsoft.Win32;

namespace FramePlay.UI;

public partial class SettingsWindow : Window
{
    private static readonly (string Id, string Key, string Icon)[] Pages =
    [
        ("general", "nav.general", "Icon.GearFill"),
        ("playback", "nav.playback", "Icon.PlayFill"),
        ("subtitles", "nav.subtitles", "Icon.SubtitlesFill"),
        ("audio", "nav.audio", "Icon.NoteFill"),
        ("hotkeys", "nav.hotkeys", "Icon.Keyboard"),
        ("appearance", "nav.appearance", "Icon.Palette"),
        ("about", "nav.about", "Icon.Info"),
    ];

    private readonly MainWindow _main;
    private readonly AppSettings _s;
    private readonly DispatcherTimer _noticeTimer;
    private string _page = "general";
    private bool _building;
    private string? _recordingAction;
    private SubtitlePreview? _subPreview;

    public SettingsWindow(MainWindow main, AppSettings settings)
    {
        _main = main;
        _s = settings;
        InitializeComponent();
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _noticeTimer.Tick += (_, _) => { _noticeTimer.Stop(); Notice.Visibility = Visibility.Collapsed; };
        PreviewKeyDown += OnPreviewKeyDown;
        Loc.I.Changed += OnLanguageChanged;
        Closed += (_, _) => Loc.I.Changed -= OnLanguageChanged;
        BuildNav();
    }

    private static string T(string key) => Loc.T(key);
    private Brush Res(string key) => (Brush)FindResource(key);

    private void OnLanguageChanged()
    {
        BuildNav();
        Build();
    }

    // =============================================================================================
    // Navigation
    // =============================================================================================
    private void BuildNav()
    {
        _building = true;
        Nav.Items.Clear();
        foreach (var (id, key, icon) in Pages)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var path = new System.Windows.Shapes.Path { Data = (Geometry)FindResource(icon) };
            path.SetBinding(System.Windows.Shapes.Shape.FillProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1) });
            row.Children.Add(new Viewbox { Width = 18, Height = 18, Child = new Canvas { Width = 24, Height = 24, Children = { path } } });
            row.Children.Add(new TextBlock { Text = T(key), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var item = new ListBoxItem { Content = row, Tag = id, Padding = new Thickness(12, 9, 12, 9) };
            Nav.Items.Add(item);
            if (id == _page) Nav.SelectedItem = item;
        }
        _building = false;
        Build();
    }

    public void ShowPage(string page)
    {
        _page = page;
        foreach (ListBoxItem item in Nav.Items) if ((string)item.Tag == page) Nav.SelectedItem = item;
        Build();
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_building || Nav.SelectedItem is not ListBoxItem item) return;
        _page = (string)item.Tag;
        _recordingAction = null;
        Build();
        Scroll.ScrollToTop();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Build()
    {
        Page.Children.Clear();
        _subPreview = null;
        switch (_page)
        {
            case "general": BuildGeneral(); break;
            case "playback": BuildPlayback(); break;
            case "subtitles": BuildSubtitles(); break;
            case "audio": BuildAudio(); break;
            case "hotkeys": BuildHotkeys(); break;
            case "appearance": BuildAppearance(); break;
            case "about": BuildAbout(); break;
        }
    }

    // =============================================================================================
    // Building blocks
    // =============================================================================================
    private void PageTitle(string text, string? desc = null)
    {
        Page.Children.Add(new TextBlock { Text = text, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Res("TextStrongBrush"), Margin = new Thickness(0, 2, 0, desc == null ? 14 : 6) });
        if (desc != null)
            Page.Children.Add(new TextBlock { Text = desc, Foreground = Res("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
    }

    private void Section(string text) => Page.Children.Add(new TextBlock
    {
        Text = text.ToUpper(CultureInfo.CurrentUICulture), FontSize = 11, FontWeight = FontWeights.SemiBold,
        Foreground = Res("TextMutedBrush"), Margin = new Thickness(4, 18, 0, 8),
    });

    private Border Card(params UIElement[] rows)
    {
        var stack = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) stack.Children.Add(new Border { Height = 1, Background = Res("LineBrush"), Opacity = 0.55, Margin = new Thickness(16, 0, 16, 0) });
            stack.Children.Add(rows[i]);
        }
        var card = new Border { Child = stack, Padding = new Thickness(0, 2, 0, 2) };
        card.SetResourceReference(Border.BackgroundProperty, "Bg1Brush");
        card.SetResourceReference(Border.CornerRadiusProperty, "CornerRadiusLarge");
        Page.Children.Add(card);
        return card;
    }

    private Grid Row(string label, string? desc, UIElement control)
    {
        var g = new Grid { Margin = new Thickness(16, 12, 16, 12), Background = Brushes.Transparent };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
        text.Children.Add(new TextBlock { Text = label, Foreground = Res("TextStrongBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 13.5 });
        if (!string.IsNullOrEmpty(desc))
            text.Children.Add(new TextBlock { Text = desc, Foreground = Res("TextMutedBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        g.Children.Add(text);
        if (control is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    private UIElement Toggle(string label, string? desc, bool value, Action<bool> set)
    {
        var cb = new CheckBox { IsChecked = value, Style = (Style)FindResource("Switch") };
        cb.Checked += (_, _) => { set(true); Changed(); };
        cb.Unchecked += (_, _) => { set(false); Changed(); };
        var row = Row(label, desc, cb);
        row.Cursor = Cursors.Hand;
        row.MouseLeftButtonUp += (_, _) => { if (!cb.IsMouseOver) cb.IsChecked = cb.IsChecked != true; };
        return row;
    }

    private UIElement Combo(string label, string? desc, IEnumerable<(string Value, string Text)> items, string current, Action<string> set, double width = 240)
    {
        var combo = new ComboBox { Width = width };
        foreach (var (v, t) in items) combo.Items.Add(new ComboBoxItem { Content = t, Tag = v });
        var all = combo.Items.Cast<ComboBoxItem>().ToList();
        combo.SelectedItem = all.FirstOrDefault(i => string.Equals((string)i.Tag, current, StringComparison.OrdinalIgnoreCase)) ?? all.FirstOrDefault();
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem it) { set((string)it.Tag); Changed(); }
        };
        return Row(label, desc, combo);
    }

    private UIElement SliderRow(string label, string? desc, double min, double max, double step, double value, Action<double> set, string format)
    {
        var valueText = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right, Foreground = Res("TextStrongBrush"), VerticalAlignment = VerticalAlignment.Center,
            Text = value.ToString(format, CultureInfo.CurrentCulture) };
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 200, SmallChange = step, LargeChange = step * 5,
            IsSnapToTickEnabled = true, TickFrequency = step, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = e.NewValue.ToString(format, CultureInfo.CurrentCulture);
            set(e.NewValue);
            Changed();
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return Row(label, desc, panel);
    }

    private UIElement ColorRow(string label, string? desc, string value, bool alpha, Action<string> set)
    {
        var sw = new ColorSwatchButton(ColorUtil.Parse(value, Colors.White)) { AllowAlpha = alpha };
        sw.ColorPicked += c => { set(ColorUtil.ToHex(c)); Changed(); };
        return Row(label, desc, sw);
    }

    private UIElement TextRow(string label, string? desc, string value, Action<string> set, double width = 240)
    {
        var tb = new TextBox { Text = value, Width = width };
        void Commit() { set(tb.Text.Trim()); Changed(); }
        tb.LostKeyboardFocus += (_, _) => Commit();
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        return Row(label, desc, tb);
    }

    private Button Btn(string text, Action onClick, string style = "TextButton")
    {
        var b = new Button { Content = text, Style = (Style)FindResource(style), Margin = new Thickness(8, 0, 0, 0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    private StackPanel Buttons(params Button[] buttons)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons) p.Children.Add(b);
        return p;
    }

    private void Changed()
    {
        _main.ApplySettings();
        _subPreview?.Update(_s.Subtitles, T("subs.sample"));
    }

    private void ShowNotice(string text)
    {
        NoticeText.Text = text;
        Notice.Visibility = Visibility.Visible;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("Open folder", ex); }
    }

    // =============================================================================================
    // Pages
    // =============================================================================================
    private void BuildGeneral()
    {
        PageTitle(T("nav.general"));
        Card(Combo(T("gen.language"), T("gen.language.desc"),
            [("auto", T("gen.language.auto")), ("en", "English"), ("es", "Español")], _s.Language, v =>
            {
                _s.Language = v;
                Dispatcher.BeginInvoke(() => Loc.I.SetLanguage(v));
            }));

        Section(T("gen.windows"));
        Card(
            Toggle(T("gen.topmost"), T("gen.topmost.desc"), _s.TopmostWhilePlayingVideo, v => _s.TopmostWhilePlayingVideo = v),
            Toggle(T("gen.single"), T("gen.single.desc"), _s.SingleInstance, v => _s.SingleInstance = v),
            Toggle(T("gen.singleFm"), T("gen.singleFm.desc"), _s.SingleInstanceFromFileManager, v => _s.SingleInstanceFromFileManager = v),
            Toggle(T("gen.autoPip"), T("gen.autoPip.desc"), _s.AutoPipOnMinimize, v => _s.AutoPipOnMinimize = v));

        Section(T("gen.playback"));
        Card(
            Toggle(T("gen.pauseLast"), T("gen.pauseLast.desc"), _s.PauseOnLastFrame, v => _s.PauseOnLastFrame = v),
            Toggle(T("gen.resume"), T("gen.resume.desc"), _s.ResumePosition, v => _s.ResumePosition = v));

        Section(T("gen.default"));
        Card(Row(T("gen.default"), T("gen.default.desc"), Btn(T("gen.default.button"), () =>
        {
            try
            {
                FileAssociations.Register();
                FileAssociations.OpenDefaultAppsSettings();
                ShowNotice(T("gen.default.done"));
            }
            catch (Exception ex) { Log.Error("File associations", ex); ShowNotice(ex.Message); }
        })));
    }

    private void BuildPlayback()
    {
        PageTitle(T("nav.playback"));
        Card(
            Combo(T("play.time"), T("play.time.desc"),
                [("time", T("time.time")), ("timecode", T("time.timecode")), ("frames", T("time.frames"))], _s.TimeDisplay, v => _s.TimeDisplay = v),
            Toggle(T("play.remaining"), null, _s.ShowRemainingTime, v => _s.ShowRemainingTime = v));

        Section(T("play.screenshots"));
        var folder = new TextBox { Text = _main.Player.ScreenshotFolder, Width = 260, IsReadOnly = true };
        var folderRow = new StackPanel { Orientation = Orientation.Horizontal };
        folderRow.Children.Add(folder);
        folderRow.Children.Add(Btn(T("play.browse"), () =>
        {
            var dlg = new OpenFolderDialog { InitialDirectory = _main.Player.ScreenshotFolder };
            if (dlg.ShowDialog(this) == true)
            {
                _s.ScreenshotFolder = dlg.FolderName;
                folder.Text = dlg.FolderName;
                Changed();
            }
        }));
        folderRow.Children.Add(Btn(T("play.open"), () => OpenFolder(_main.Player.ScreenshotFolder)));
        Card(
            Row(T("play.folder"), null, folderRow),
            Toggle(T("play.withSubs"), T("play.withSubs.desc"), _s.ScreenshotWithSubtitles, v => _s.ScreenshotWithSubtitles = v));

        Section(T("play.languages"));
        Card(
            TextRow(T("play.alang"), T("play.alang.desc"), _s.PreferredAudioLanguages, v => _s.PreferredAudioLanguages = v),
            TextRow(T("play.slang"), null, _s.PreferredSubtitleLanguages, v => _s.PreferredSubtitleLanguages = v));
    }

    private static readonly (string, string)[] Encodings =
    [
        ("utf-8", "UTF-8"), ("cp1252", "Western (Windows-1252)"), ("iso-8859-1", "Western (ISO-8859-1)"),
        ("cp1250", "Central European (Windows-1250)"), ("cp1251", "Cyrillic (Windows-1251)"), ("koi8-r", "Cyrillic (KOI8-R)"),
        ("cp1253", "Greek (Windows-1253)"), ("cp1254", "Turkish (Windows-1254)"), ("cp1255", "Hebrew (Windows-1255)"),
        ("cp1256", "Arabic (Windows-1256)"), ("cp1257", "Baltic (Windows-1257)"), ("cp1258", "Vietnamese (Windows-1258)"),
        ("cp874", "Thai (Windows-874)"), ("shift_jis", "Japanese (Shift-JIS)"), ("gb18030", "Chinese Simplified (GB18030)"),
        ("big5", "Chinese Traditional (Big5)"), ("cp949", "Korean (CP949)"),
    ];

    private void BuildSubtitles()
    {
        var st = _s.Subtitles;
        PageTitle(T("nav.subtitles"));
        _subPreview = new SubtitlePreview { Height = 210, Margin = new Thickness(0, 0, 0, 4) };
        _subPreview.Update(st, T("subs.sample"));
        Page.Children.Add(_subPreview);

        Section(T("subs.style"));
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (!fonts.Contains(st.Font)) fonts.Insert(0, st.Font);
        var fontRow = Combo(T("subs.font"), null, fonts.Select(f => (f, f)), st.Font, v => st.Font = v);
        if (fontRow is Grid g && g.Children.OfType<ComboBox>().FirstOrDefault() is { } fontCombo)
            fontCombo.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        Card(
            // Stored as mpv's sub-pos (0 top … 100 bottom); shown as the height above the bottom.
            SliderRow(T("subs.position"), T("subs.position.desc"), 0, 100, 1, 100 - st.Position, v => st.Position = 100 - v, "0'%'"),
            fontRow,
            SliderRow(T("subs.size"), null, 16, 120, 1, st.FontSize, v => st.FontSize = v, "0"),
            SliderRow(T("subs.pipScale"), T("subs.pipScale.desc"), 100, 200, 5, Math.Round(st.PipScale * 100), v => st.PipScale = v / 100, "0'%'"),
            SliderRow(T("subs.outline"), null, 0, 10, 0.5, st.OutlineSize, v => st.OutlineSize = v, "0.0"),
            ColorRow(T("subs.color"), null, st.TextColor, false, v => st.TextColor = v),
            ColorRow(T("subs.outlineColor"), null, st.OutlineColor, false, v => st.OutlineColor = v));
        Section(T("subs.shadow") + " · " + T("subs.background"));
        Card(
            Toggle(T("subs.shadow"), null, st.Shadow, v => st.Shadow = v),
            ColorRow(T("subs.shadowColor"), null, st.ShadowColor, true, v => st.ShadowColor = v),
            Toggle(T("subs.background"), null, st.Background, v => st.Background = v),
            ColorRow(T("subs.backgroundColor"), null, st.BackgroundColor, true, v => st.BackgroundColor = v));
        Section(T("subs.encoding"));
        Card(
            Combo(T("subs.encoding"), T("subs.encoding.desc"), new[] { ("auto", T("subs.encoding.auto")) }.Concat(Encodings), st.Encoding, v => st.Encoding = v, 280),
            Toggle(T("subs.ass"), null, st.OverrideAss, v => st.OverrideAss = v));
        var reset = Btn(T("subs.reset"), () => { _s.Subtitles = new SubtitleStyle(); Changed(); Build(); }, "GhostButton");
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Margin = new Thickness(0, 14, 0, 0);
        Page.Children.Add(reset);
    }

    private void BuildAudio()
    {
        PageTitle(T("nav.audio"));
        var fonts = PlayerController.AvailableSoundfonts();
        var items = fonts.Count > 0 ? fonts.Select(f => (f.Name, Path.GetFileNameWithoutExtension(f.Name))) : [("", T("audio.none"))];
        string current = fonts.Any(f => string.Equals(f.Name, _s.Soundfont, StringComparison.OrdinalIgnoreCase)) ? _s.Soundfont : fonts.FirstOrDefault().Name ?? "";
        Card(
            Combo(T("audio.soundfont"), T("audio.soundfont.desc"), items, current, v =>
            {
                if (string.IsNullOrEmpty(v)) return;
                _s.Soundfont = v;
                _main.Player.ReloadSoundfont();
            }, 260),
            Row("", null, Buttons(Btn(T("audio.openFolder"), () => OpenFolder(AppPaths.AppDir)), Btn(T("audio.refresh"), Build))));
        Section(T("play.languages"));
        Card(TextRow(T("play.alang"), T("play.alang.desc"), _s.PreferredAudioLanguages, v => _s.PreferredAudioLanguages = v));
    }

    // ---- hotkeys ----
    private void BuildHotkeys()
    {
        PageTitle(T("nav.hotkeys"), T("keys.desc"));
        foreach (var group in new[] { "playback", "frames", "seek", "audio", "subtitles", "view", "tools" })
        {
            Section(T("keys.group." + group));
            var rows = HotkeyCatalog.Actions.Where(a => a.Group == group).Select(HotkeyRow).ToArray();
            Card(rows);
        }
        var resetAll = Btn(T("keys.resetAll"), () => { _s.Hotkeys.Clear(); _recordingAction = null; Changed(); Build(); }, "GhostButton");
        resetAll.HorizontalAlignment = HorizontalAlignment.Left;
        resetAll.Margin = new Thickness(0, 14, 0, 0);
        Page.Children.Add(resetAll);
    }

    private UIElement HotkeyRow(HotkeyAction action)
    {
        var gestures = Gesture.ParseList(HotkeyMap.GesturesFor(action.Id, _s.Hotkeys));
        var chips = new StackPanel { Orientation = Orientation.Horizontal };
        if (_recordingAction == action.Id)
            chips.Children.Add(new TextBlock { Text = T("keys.press"), Foreground = Res("AccentBrush"), FontWeight = FontWeights.SemiBold });
        else if (gestures.Count == 0)
            chips.Children.Add(new TextBlock { Text = T("keys.none"), Foreground = Res("TextMutedBrush") });
        else
            foreach (var g in gestures)
            {
                var cap = new Border { Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(4, 0, 0, 0), BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = KeyLabel(g), FontSize = 12, Foreground = Res("TextStrongBrush") } };
                cap.SetResourceReference(Border.BackgroundProperty, "BgInputBrush");
                cap.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
                cap.SetResourceReference(Border.CornerRadiusProperty, "CornerRadiusSmall");
                chips.Children.Add(cap);
            }
        var edit = new Button { Content = chips, Style = (Style)FindResource("GhostButton"), Padding = new Thickness(6, 4, 6, 4) };
        edit.Click += (_, _) => { _recordingAction = action.Id; Build(); };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(edit);
        if (_s.Hotkeys.ContainsKey(action.Id))
            panel.Children.Add(Btn(T("keys.reset"), () => { _s.Hotkeys.Remove(action.Id); Changed(); Build(); }, "GhostButton"));
        var row = Row(T("action." + action.Id), null, panel);
        row.Margin = new Thickness(16, 6, 12, 6);
        return row;
    }

    private static string KeyLabel(Gesture g) => g.Key switch
    {
        Key.MediaPlayPause => "Media Play/Pause",
        Key.MediaNextTrack => "Media Next",
        Key.MediaPreviousTrack => "Media Previous",
        Key.MediaStop => "Media Stop",
        _ => g.Display,
    };

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingAction == null)
        {
            if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox) { Close(); e.Handled = true; }
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
        e.Handled = true;
        var id = _recordingAction;
        _recordingAction = null;
        if (key == Key.Escape) { Build(); return; }
        if (key is Key.Back or Key.Delete)
        {
            _s.Hotkeys[id] = "";
            Changed();
            Build();
            return;
        }
        var gesture = new Gesture(Keyboard.Modifiers, key);
        // A key can only do one thing: take it away from any other action that used it.
        foreach (var other in HotkeyCatalog.Actions.Where(a => a.Id != id))
        {
            var list = Gesture.ParseList(HotkeyMap.GesturesFor(other.Id, _s.Hotkeys));
            if (list.Remove(gesture))
            {
                _s.Hotkeys[other.Id] = string.Join(", ", list.Select(x => x.ToString()));
                ShowNotice(Loc.F("keys.moved", KeyLabel(gesture), T("action." + other.Id)));
            }
        }
        _s.Hotkeys[id] = gesture.ToString();
        Changed();
        Build();
    }

    // ---- appearance ----
    private void BuildAppearance()
    {
        PageTitle(T("nav.appearance"), T("app.themes.desc"));
        var wrap = new WrapPanel();
        foreach (var t in ThemeManager.AllThemes(_s)) wrap.Children.Add(ThemeCard(t));
        Page.Children.Add(wrap);
        var buttons = new WrapPanel { Margin = new Thickness(-8, 6, 0, 0) };
        buttons.Children.Add(Btn(T("app.create"), CreateTheme));
        buttons.Children.Add(Btn(T("app.import"), ImportThemes));
        buttons.Children.Add(Btn(T("app.export"), ExportTheme));
        Page.Children.Add(buttons);
        AccentSection();
    }

    // ---- accent colour ----
    /// <summary>
    /// Circles that replace the theme's accent: the theme's own colour first, the ready-made ones, then one
    /// that opens the colour picker for any colour.
    /// </summary>
    private void AccentSection()
    {
        var current = ThemeManager.AccentOverride;
        var themeAccent = ColorUtil.Parse(ThemeManager.Resolve(ThemeManager.Current).GetValueOrDefault("accent"), Colors.Gray);
        var circles = new WrapPanel { Margin = new Thickness(12, 0, 12, 10) };
        circles.Children.Add(AccentCircle(new SolidColorBrush(themeAccent), null, Loc.F("app.accent.theme", ColorUtil.ToHex(themeAccent)),
            current == null, () => SetAccent(null, rebuild: true)));
        bool preset = false;
        foreach (var hex in ThemeManager.AccentPresets)
        {
            bool on = string.Equals(current, hex, StringComparison.OrdinalIgnoreCase);
            preset |= on;
            circles.Children.Add(AccentCircle(new SolidColorBrush(ColorUtil.Parse(hex, Colors.Gray)), null, hex, on, () => SetAccent(hex, rebuild: true)));
        }

        // Any colour: a rainbow circle, with the chosen colour in the middle once it's in use.
        bool custom = current != null && !preset;
        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var (stop, hex) in new[] { (0.0, "#ff4b4b"), (0.2, "#ffd84b"), (0.4, "#9bf200"), (0.6, "#00f2ea"), (0.8, "#4b9dff"), (1.0, "#f200e8") })
            rainbow.GradientStops.Add(new GradientStop(ColorUtil.Parse(hex, Colors.Gray), stop));
        var customColor = custom ? ColorUtil.Parse(current, themeAccent) : themeAccent;
        FrameworkElement? picker = null;
        picker = AccentCircle(rainbow, custom ? new SolidColorBrush(customColor) : null,
            custom ? $"{T("app.accent.custom")} ({current})" : T("app.accent.custom"), custom, () =>
            {
                // Live while dragging; the page is rebuilt (to move the ring) once the picker closes.
                var popup = ColorPicker.Show(picker!, customColor, false, c => SetAccent(ColorUtil.ToHex(Color.FromRgb(c.R, c.G, c.B)), rebuild: false));
                popup.Closed += (_, _) => Build();
            });
        circles.Children.Add(picker);

        var text = new StackPanel { Margin = new Thickness(16, 12, 16, 10) };
        text.Children.Add(new TextBlock { Text = T("app.accent"), Foreground = Res("TextStrongBrush"), FontSize = 13.5 });
        text.Children.Add(new TextBlock { Text = T("app.accent.desc"), Foreground = Res("TextMutedBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(circles);
        Card(stack).Margin = new Thickness(0, 18, 0, 0);
    }

    /// <summary>A round colour choice; <paramref name="inner"/> draws a smaller circle in the middle.</summary>
    private FrameworkElement AccentCircle(Brush fill, Brush? inner, string tip, bool selected, Action onClick)
    {
        var dot = new Grid { Width = 24, Height = 24 };
        dot.Children.Add(new System.Windows.Shapes.Ellipse { Fill = fill });
        if (inner != null) dot.Children.Add(new System.Windows.Shapes.Ellipse { Fill = inner, Width = 12, Height = 12 });
        var ring = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(2), Padding = new Thickness(3),
            Margin = new Thickness(0, 0, 4, 4), Child = dot, Cursor = Cursors.Hand, ToolTip = tip, Background = Brushes.Transparent,
            BorderBrush = selected ? Res("TextStrongBrush") : Brushes.Transparent,
        };
        ring.MouseEnter += (_, _) => { if (!selected) ring.BorderBrush = Res("LineBrush"); };
        ring.MouseLeave += (_, _) => { if (!selected) ring.BorderBrush = Brushes.Transparent; };
        ring.MouseLeftButtonUp += (_, _) => onClick();
        return ring;
    }

    private void SetAccent(string? hex, bool rebuild)
    {
        _s.AccentColor = hex;
        ThemeManager.AccentOverride = hex;
        ThemeManager.Apply(ThemeManager.Current);
        Changed();
        if (rebuild) Build();
    }

    private FrameworkElement ThemeCard(ThemeDefinition t)
    {
        // As it will look, with the accent picked below.
        var v = ThemeManager.ResolveShown(t);
        Brush B(string k) => new SolidColorBrush(ColorUtil.Parse(v.GetValueOrDefault(k), Colors.Magenta));
        bool selected = ThemeManager.Current.Id == t.Id;

        // A tiny FramePlay: top bar, video, control bar with the play pill and seek fill, and the queue.
        var player = new Grid { Background = B("bg-1") };
        player.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9) });
        player.RowDefinitions.Add(new RowDefinition());
        player.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        var video = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(3), Margin = new Thickness(5, 0, 5, 0) };
        Grid.SetRow(video, 1);
        player.Children.Add(video);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        bar.Children.Add(new Border { Width = 13, Height = 7, CornerRadius = new CornerRadius(2), Background = B("accent") });
        var track = new Grid { Width = 50, Height = 5, Margin = new Thickness(6, 0, 0, 0) };
        track.Children.Add(new Border { Background = B("bg-input"), CornerRadius = new CornerRadius(2) });
        track.Children.Add(new Border { Background = B("seek"), Opacity = 0.55, CornerRadius = new CornerRadius(2), Width = 22, HorizontalAlignment = HorizontalAlignment.Left });
        bar.Children.Add(track);
        bar.Children.Add(new Border { Width = 12, Height = 3, Margin = new Thickness(6, 0, 0, 0), Background = B("volume"), CornerRadius = new CornerRadius(1.5) });
        bar.Children.Add(new Border { Width = 5, Height = 5, Margin = new Thickness(5, 0, 0, 0), Background = B("toggle"), CornerRadius = new CornerRadius(2.5) });
        Grid.SetRow(bar, 2);
        player.Children.Add(bar);
        var queue = new StackPanel { Background = B("bg-0"), Width = 46 };
        queue.Children.Add(new Border { Height = 4, Width = 30, Margin = new Thickness(0, 12, 0, 0), Background = B("accent"), CornerRadius = new CornerRadius(2) });
        queue.Children.Add(new Border { Height = 4, Width = 30, Margin = new Thickness(0, 6, 0, 0), Background = B("text-muted"), CornerRadius = new CornerRadius(2), Opacity = 0.8 });
        queue.Children.Add(new Border { Height = 4, Width = 24, Margin = new Thickness(-6, 6, 0, 0), Background = B("text-muted"), CornerRadius = new CornerRadius(2), Opacity = 0.8 });
        var preview = new Grid { Height = 96 };
        preview.ColumnDefinitions.Add(new ColumnDefinition());
        preview.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        preview.Children.Add(player);
        Grid.SetColumn(queue, 1);
        preview.Children.Add(queue);
        var clip = new Border { Child = preview, CornerRadius = new CornerRadius(7, 7, 0, 0), ClipToBounds = true };

        var name = new TextBlock { Text = t.Name, Foreground = Res("TextStrongBrush"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var footer = new DockPanel { Margin = new Thickness(10, 7, 6, 8), LastChildFill = true };
        if (t.Custom)
        {
            var del = new Button { Style = (Style)FindResource("IconButton"), Content = FindResource("Icon.MinusFill"), Width = 24, Height = 22, Padding = new Thickness(4), ToolTip = T("app.delete") };
            del.Click += (_, e) => { e.Handled = true; DeleteTheme(t); };
            var edit = new Button { Style = (Style)FindResource("IconButton"), Content = FindResource("Icon.EditFill"), Width = 24, Height = 22, Padding = new Thickness(4), ToolTip = T("app.edit") };
            edit.Click += (_, e) => { e.Handled = true; EditTheme(t); };
            DockPanel.SetDock(del, Dock.Right);
            DockPanel.SetDock(edit, Dock.Right);
            footer.Children.Add(del);
            footer.Children.Add(edit);
        }
        if (selected)
        {
            var check = new System.Windows.Shapes.Path { Data = Geometry.Parse("M1.5,6.5 L5,10 L12,2.5"), Stroke = Res("AccentBrush"), StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            DockPanel.SetDock(check, Dock.Right);
            footer.Children.Add(check);
        }
        footer.Children.Add(name);

        var stack = new StackPanel();
        stack.Children.Add(clip);
        stack.Children.Add(footer);
        var card = new Border
        {
            Width = 178, Margin = new Thickness(0, 0, 12, 12), CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(2),
            Child = stack, Cursor = Cursors.Hand, Background = Res("Bg1Brush"),
            BorderBrush = selected ? Res("AccentBrush") : Brushes.Transparent,
        };
        card.MouseEnter += (_, _) => { if (!selected) card.BorderBrush = Res("LineBrush"); };
        card.MouseLeave += (_, _) => { if (!selected) card.BorderBrush = Brushes.Transparent; };
        card.MouseLeftButtonUp += (_, _) => SelectTheme(t);
        return card;
    }

    private void SelectTheme(ThemeDefinition t)
    {
        _s.Theme = t.Id;
        ThemeManager.Apply(t);
        Changed();
        Build();
    }

    private void CreateTheme()
    {
        var current = ThemeManager.Current;
        var copy = new ThemeDefinition
        {
            Id = ThemeFiles.NewId(), Name = Loc.F("editor.myVersion", current.Name), Author = Environment.UserName,
            // From what's on screen, so a picked accent carries over into the new theme.
            Base = current.Base, Vars = ThemeManager.ResolveShown(current), Custom = true,
        };
        var editor = new ThemeEditorWindow(this, copy, current);
        if (editor.ShowDialog() == true)
        {
            _s.CustomThemes.Add(copy);
            SelectTheme(copy);
        }
    }

    private void EditTheme(ThemeDefinition t)
    {
        var working = t.Clone();
        var revert = ThemeManager.Current;
        var editor = new ThemeEditorWindow(this, working, revert);
        if (editor.ShowDialog() == true)
        {
            int i = _s.CustomThemes.FindIndex(x => x.Id == t.Id);
            if (i >= 0) _s.CustomThemes[i] = working;
            SelectTheme(working);
        }
    }

    private void DeleteTheme(ThemeDefinition t)
    {
        if (MessageBox.Show(this, Loc.F("app.deleteConfirm", t.Name), "FramePlay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _s.CustomThemes.RemoveAll(x => x.Id == t.Id);
        if (_s.Theme == t.Id) SelectTheme(BuiltInThemes.All[0]);
        else { Changed(); Build(); }
    }

    private void ImportThemes()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "Themes (*.mntheme;*.json)|*.mntheme;*.json" };
        if (dlg.ShowDialog(this) != true) return;
        ThemeDefinition? last = null;
        int count = 0;
        foreach (var f in dlg.FileNames)
        {
            var t = ThemeFiles.Import(f);
            if (t == null) { ShowNotice(Loc.F("app.importFailed", Path.GetFileName(f))); continue; }
            _s.CustomThemes.Add(t);
            last = t;
            count++;
        }
        if (last != null)
        {
            SelectTheme(last);
            ShowNotice(Loc.F("app.imported", count));
        }
    }

    private void ExportTheme()
    {
        var t = ThemeManager.Current;
        var dlg = new SaveFileDialog { Filter = "Theme (*.mntheme)|*.mntheme", FileName = string.Concat(t.Name.Split(Path.GetInvalidFileNameChars())) + ".mntheme" };
        if (dlg.ShowDialog(this) == true)
        {
            try { ThemeFiles.Export(t, dlg.FileName); }
            catch (Exception ex) { Log.Error("Export theme", ex); ShowNotice(ex.Message); }
        }
    }

    // ---- about ----
    private void BuildAbout()
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
        head.Children.Add(new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/FramePlay-256.png")), Width = 64, Height = 64 });
        var titles = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "FramePlay", FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Res("TextStrongBrush") });
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        titles.Children.Add(new TextBlock { Text = Loc.F("about.version", version) + "  ·  " + T("about.desc"), Foreground = Res("TextMutedBrush"), Margin = new Thickness(0, 4, 0, 0) });
        head.Children.Add(titles);
        Page.Children.Add(head);

        Section(T("about.credits"));
        var credits = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        foreach (var key in new[] { "about.mpv", "about.design", "about.themes", "about.midi", "about.soundfont" })
            credits.Children.Add(new TextBlock { Text = T(key), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3), Foreground = Res("TextBrush") });
        Card(credits);

        Section(T("about.folders"));
        Card(Row("", null, Buttons(Btn(T("about.settingsFolder"), () => OpenFolder(AppPaths.ConfigDir)), Btn(T("about.logsFolder"), () => OpenFolder(Path.GetDirectoryName(AppPaths.LogFile)!)))));
    }
}
