using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FramePlay.Core;

namespace FramePlay.Playback;

public sealed class QueueItem : INotifyPropertyChanged
{
    private string _title;
    private double _duration;
    private bool _isCurrent;

    public QueueItem(string path)
    {
        Path = path;
        Kind = MediaFormats.KindOf(path);
        _title = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public string Path { get; }
    public MediaKind Kind { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";

    public string Title { get => _title; set { if (_title != value) { _title = value; OnChanged(); } } }

    public double Duration
    {
        get => _duration;
        set { if (Math.Abs(_duration - value) > 0.01) { _duration = value; OnChanged(); OnChanged(nameof(DurationText)); } }
    }

    public string DurationText => _duration > 0 ? TimeFormat.Clock(_duration, false) : "";

    public bool IsCurrent { get => _isCurrent; set { if (_isCurrent != value) { _isCurrent = value; OnChanged(); } } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
