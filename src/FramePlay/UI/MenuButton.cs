using System.Windows;

namespace FramePlay.UI;

/// <summary>Marks a button whose menu is open, so it shows its "on" colour like the queue button does.</summary>
public static class MenuButton
{
    public static readonly DependencyProperty IsMenuOpenProperty =
        DependencyProperty.RegisterAttached("IsMenuOpen", typeof(bool), typeof(MenuButton), new PropertyMetadata(false));

    public static bool GetIsMenuOpen(DependencyObject o) => (bool)o.GetValue(IsMenuOpenProperty);
    public static void SetIsMenuOpen(DependencyObject o, bool value) => o.SetValue(IsMenuOpenProperty, value);
}
