using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using FramePlay.Localization;
using FramePlay.Themes;
using FramePlay.UI.Controls;

namespace FramePlay.UI;

/// <summary>
/// Edits one theme's colours with a live preview: every change is applied to the whole app straight away,
/// and Cancel puts the previous theme back. Same tokens as MarkNotes' theme editor.
/// </summary>
public sealed class ThemeEditorWindow : Window
{
    private readonly ThemeDefinition _theme;
    private readonly ThemeDefinition _revertTo;
    private bool _saved;

    public ThemeEditorWindow(Window owner, ThemeDefinition working, ThemeDefinition revertTo)
    {
        _theme = working;
        _revertTo = revertTo;
        Owner = owner;
        Title = Loc.T("editor.title");
        Width = 600;
        Height = 720;
        MinWidth = 480;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontFamily = (FontFamily)FindResource("UiFont");
        FontSize = 13;
        SetResourceReference(BackgroundProperty, "Bg2Brush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 44, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        Content = Build();
        Closed += (_, _) => { if (!_saved) ThemeManager.Apply(_revertTo); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox) Close(); };
        ThemeManager.Apply(_theme);
    }

    private Brush Res(string key) => (Brush)FindResource(key);

    private UIElement Build()
    {
        var root = new DockPanel();

        // Title bar
        var top = new Grid { Height = 44 };
        top.Children.Add(new TextBlock { Text = Loc.T("editor.title"), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Res("TextStrongBrush"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(22, 0, 0, 0) });
        var close = new Button { Style = (Style)FindResource("CloseWindowButton"), Content = FindResource("Icon.Close"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => Close();
        top.Children.Add(close);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        // Buttons
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 10, 22, 18) };
        var cancel = new Button { Content = Loc.T("editor.cancel"), Style = (Style)FindResource("GhostButton"), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        var save = new Button { Content = Loc.T("editor.save"), Style = (Style)FindResource("AccentButton") };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_theme.Name)) _theme.Name = Loc.F("editor.myVersion", _revertTo.Name);
            _saved = true;
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var stack = new StackPanel { Margin = new Thickness(22, 0, 22, 8) };

        // Name, author, base, corner radius
        var meta = new Grid();
        meta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        meta.ColumnDefinitions.Add(new ColumnDefinition());
        void MetaRow(string label, UIElement control)
        {
            int r = meta.RowDefinitions.Count;
            meta.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tb = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = Res("TextMutedBrush"), Margin = new Thickness(0, 6, 0, 6) };
            Grid.SetRow(tb, r);
            meta.Children.Add(tb);
            if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
            Grid.SetRow(control, r);
            Grid.SetColumn(control, 1);
            meta.Children.Add(control);
        }
        var name = new TextBox { Text = _theme.Name };
        name.TextChanged += (_, _) => _theme.Name = name.Text.Trim();
        MetaRow(Loc.T("editor.name"), name);
        var author = new TextBox { Text = _theme.Author };
        author.TextChanged += (_, _) => _theme.Author = author.Text.Trim();
        MetaRow(Loc.T("editor.author"), author);
        var baseCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Left, Width = 160 };
        baseCombo.Items.Add(new ComboBoxItem { Content = Loc.T("editor.dark"), Tag = "dark" });
        baseCombo.Items.Add(new ComboBoxItem { Content = Loc.T("editor.light"), Tag = "light" });
        baseCombo.SelectedIndex = _theme.Base == "light" ? 1 : 0;
        baseCombo.SelectionChanged += (_, _) => { _theme.Base = (string)((ComboBoxItem)baseCombo.SelectedItem).Tag; ThemeManager.Apply(_theme); };
        MetaRow(Loc.T("editor.base"), baseCombo);
        var radius = new Slider { Minimum = 0, Maximum = 16, Value = ThemeManager.RadiusOf(ThemeManager.Resolve(_theme)), IsSnapToTickEnabled = true, TickFrequency = 1, Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        radius.ValueChanged += (_, e) => { _theme.Vars["radius"] = ((int)e.NewValue).ToString(CultureInfo.InvariantCulture) + "px"; ThemeManager.Apply(_theme); };
        MetaRow(Loc.T("editor.radius"), radius);
        stack.Children.Add(meta);

        stack.Children.Add(new TextBlock { Text = Loc.T("editor.colors").ToUpper(CultureInfo.CurrentUICulture), FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextMutedBrush"), Margin = new Thickness(0, 16, 0, 8) });

        // One row per colour token
        var resolved = ThemeManager.Resolve(_theme);
        var colors = new StackPanel();
        foreach (var token in ThemeManager.Tokens.Where(t => t != "radius"))
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = Loc.T("token." + token), VerticalAlignment = VerticalAlignment.Center, Foreground = Res("TextStrongBrush") });
            var swatch = new ColorSwatchButton(ColorUtil.Parse(resolved.GetValueOrDefault(token), Colors.Magenta)) { AllowAlpha = token == "selection" };
            var tokenName = token;
            swatch.ColorPicked += c => { _theme.Vars[tokenName] = ColorUtil.ToHex(c); ThemeManager.Apply(_theme); };
            Grid.SetColumn(swatch, 1);
            row.Children.Add(swatch);
            colors.Children.Add(row);
        }
        var card = new Border { Child = colors, Padding = new Thickness(14, 8, 8, 8) };
        card.SetResourceReference(Border.BackgroundProperty, "Bg1Brush");
        card.SetResourceReference(Border.CornerRadiusProperty, "CornerRadiusLarge");
        stack.Children.Add(card);

        root.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return root;
    }
}
