using System.IO;
using System.Text.Json;

namespace FramePlay.Core;

/// <summary>What FramePlay remembers about one file: where you stopped and the tracks and delays you chose.</summary>
public sealed class FileMemory
{
    public double Position { get; set; }
    public double Duration { get; set; }
    public string? AudioTrack { get; set; }
    public string? SubtitleTrack { get; set; }
    public string? SecondarySubtitleTrack { get; set; }
    public List<string>? ExternalSubtitles { get; set; }
    public List<string>? ExternalAudio { get; set; }
    public double SubtitleDelay { get; set; }
    public double AudioDelay { get; set; }
    public DateTime LastPlayed { get; set; }
}

public sealed class History
{
    private const int MaxEntries = 1000;
    private Dictionary<string, FileMemory> _items = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;

    public static History Load()
    {
        var h = new History();
        try
        {
            if (File.Exists(AppPaths.HistoryFile))
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, FileMemory>>(File.ReadAllText(AppPaths.HistoryFile), AppSettings.Json);
                if (d != null) h._items = new Dictionary<string, FileMemory>(d, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) { Log.Error("History.Load", ex); }
        return h;
    }

    public FileMemory? Get(string path) => _items.TryGetValue(path, out var m) ? m : null;

    public FileMemory GetOrAdd(string path)
    {
        if (!_items.TryGetValue(path, out var m))
            _items[path] = m = new FileMemory();
        _dirty = true;
        return m;
    }

    public void Touch() => _dirty = true;

    public void Save()
    {
        if (!_dirty) return;
        try
        {
            if (_items.Count > MaxEntries)
            {
                foreach (var old in _items.OrderBy(kv => kv.Value.LastPlayed).Take(_items.Count - MaxEntries).Select(kv => kv.Key).ToList())
                    _items.Remove(old);
            }
            AppPaths.WriteAtomic(AppPaths.HistoryFile, JsonSerializer.Serialize(_items, AppSettings.Json));
            _dirty = false;
        }
        catch (Exception ex) { Log.Error("History.Save", ex); }
    }
}
