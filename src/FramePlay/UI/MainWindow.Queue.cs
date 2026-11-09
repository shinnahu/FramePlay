using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FramePlay.Core;
using FramePlay.Localization;
using FramePlay.Playback;
using Microsoft.Win32;

namespace FramePlay.UI;

public partial class MainWindow
{
    private const string QueueDragFormat = "FramePlay.QueueItems";
    private Point _queueDragStart;
    private QueueItem? _queueDragItem;

    public IntPtr Hwnd => _hwnd;

    private void Random_Click(object sender, RoutedEventArgs e) => Player.Shuffle = QueueRandom.IsChecked == true;

    private void QueueSort_Click(object sender, RoutedEventArgs e)
    {
        var sorted = Player.Queue.OrderBy(q => q.FileName, NaturalComparer.Instance).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int old = Player.Queue.IndexOf(sorted[i]);
            if (old != i) Player.Queue.Move(old, i);
        }
    }

    private void QueueClear_Click(object sender, RoutedEventArgs e) => Player.ClearQueue();

    private void QueueAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = MediaFormats.OpenDialogFilter(Loc.T) };
        if (dlg.ShowDialog(this) == true) Player.Enqueue(dlg.FileNames, playNow: false);
    }

    private void QueueList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnderMouse(e) is { } item) Player.Play(item);
    }

    private void QueueList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            foreach (var item in QueueList.SelectedItems.Cast<QueueItem>().ToList()) Player.RemoveFromQueue(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && QueueList.SelectedItem is QueueItem item)
        {
            Player.Play(item);
            e.Handled = true;
        }
    }

    private void QueueMenuPlay_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is QueueItem item) Player.Play(item);
    }

    private void QueueMenuShow_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is QueueItem item)
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
    }

    private void QueueMenuRemove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in QueueList.SelectedItems.Cast<QueueItem>().ToList()) Player.RemoveFromQueue(item);
    }

    private QueueItem? ItemUnderMouse(MouseEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        while (hit != null && hit is not ListBoxItem) hit = VisualTreeHelper.GetParent(hit);
        return (hit as ListBoxItem)?.DataContext as QueueItem;
    }

    // ---- drag to reorder, drop files to add ----
    private void QueueList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _queueDragStart = e.GetPosition(QueueList);
        _queueDragItem = ItemUnderMouse(e);
    }

    private void QueueList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _queueDragItem == null) return;
        var p = e.GetPosition(QueueList);
        if (Math.Abs(p.X - _queueDragStart.X) < 5 && Math.Abs(p.Y - _queueDragStart.Y) < 5) return;
        var item = _queueDragItem;
        _queueDragItem = null;
        DragDrop.DoDragDrop(QueueList, new DataObject(QueueDragFormat, item), DragDropEffects.Move);
    }

    private void QueueList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(QueueDragFormat) ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private int DropIndex(DragEventArgs e)
    {
        var p = e.GetPosition(QueueList);
        for (int i = 0; i < Player.Queue.Count; i++)
        {
            if (QueueList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem c) continue;
            var top = c.TranslatePoint(new Point(0, 0), QueueList).Y;
            if (p.Y < top + c.ActualHeight / 2) return i;
        }
        return Player.Queue.Count;
    }

    private void QueueList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        int index = DropIndex(e);
        if (e.Data.GetData(QueueDragFormat) is QueueItem moved)
        {
            int old = Player.Queue.IndexOf(moved);
            if (index > old) index--;
            Player.MoveInQueue(moved, index);
            QueueList.SelectedItem = moved;
            return;
        }
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            int before = Player.Queue.Count;
            Player.Enqueue(files, playNow: false);
            // Move the new items to where they were dropped.
            for (int i = before; i < Player.Queue.Count; i++)
                Player.MoveInQueue(Player.Queue[i], Math.Min(index + (i - before), Player.Queue.Count - 1));
        }
    }

    // ---- settings ----
    private SettingsWindow? _settingsWindow;

    public void ShowSettings() => ShowSettings(null);

    public void ShowSettings(string? page)
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            if (page != null) _settingsWindow.ShowPage(page);
            return;
        }
        _settingsWindow = new SettingsWindow(this, _settings) { Owner = this };
        if (page != null) _settingsWindow.ShowPage(page);
        _settingsWindow.Show();
    }

    /// <summary>Called by the settings window after a change that affects the player.</summary>
    public void ApplySettings()
    {
        _hotkeys.Rebuild(_settings.Hotkeys);
        Player.ApplySubtitleStyle();
        Bar.SetTimeMode(TimeFormat.Parse(_settings.TimeDisplay));
        _overlay?.Bar.SetTimeMode(TimeFormat.Parse(_settings.TimeDisplay));
        UpdateTopmost();
        UpdateQueueInfo();
        UpdateView();
        SaveSettingsSoon();
    }
}
