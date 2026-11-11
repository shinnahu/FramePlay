using System.IO;
using System.Windows;
using System.Windows.Threading;
using FramePlay.Core;
using FramePlay.Localization;
using FramePlay.Themes;
using FramePlay.UI;

namespace FramePlay;

public partial class App : Application
{
    private SingleInstance? _instance;

    /// <summary>
    /// Held while FramePlay runs so Setup and the uninstaller (Inno Setup's AppMutex) can ask to close it
    /// first instead of leaving files that are in use behind.
    /// </summary>
    private static System.Threading.Mutex? _runningMutex;

    public static AppSettings Settings { get; private set; } = new();

    private readonly DateTime _constructedAt = DateTime.Now;

    protected override void OnStartup(StartupEventArgs e)
    {
        var enteredAt = DateTime.Now;
        base.OnStartup(e);
        _runningMutex = new System.Threading.Mutex(false, "FramePlay.Running");

        string? dataDir = null;
        var files = new List<string>();
        foreach (var a in e.Args)
        {
            if (a.StartsWith("--data-dir=", StringComparison.OrdinalIgnoreCase)) dataDir = a[11..].Trim('"');
            else if (a.StartsWith("--")) continue;
            else if (a.Length > 0) files.Add(Path.GetFullPath(a));
        }
        AppPaths.Init(dataDir);
        Log.Info("Start " + string.Join(' ', e.Args));

        // One window per user when asked: Settings › General decides when a second launch hands over.
        var (single, fromFileManager) = AppSettings.PeekInstanceOptions();
        _instance = new SingleInstance();
        _instance.Claim();
        bool handOver = !_instance.IsPrimary && (files.Count > 0 ? single || fromFileManager : single);
        if (handOver && _instance.TrySend(files, files.Count == 0))
        {
            Shutdown();
            return;
        }
        // Load the graphics driver while the window is being built, so the first video frame doesn't wait for it.
        GpuWarmup.Start();
        new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Background, (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            GpuWarmup.Release(); // nothing played video by now: don't keep the warm-up device around
        }, Dispatcher).Start();

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("UI thread", ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error("Unhandled", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Error("Task", ex.Exception); ex.SetObserved(); };

        Log.Timing($"OnStartup (App constructed at {(_constructedAt - Log.ProcessStart).TotalMilliseconds:0} ms, " +
                   $"OnStartup entered at {(enteredAt - Log.ProcessStart).TotalMilliseconds:0} ms)");
        HoverTip.Install();
        Log.Timing("hover tips");
        IconFactory.Register(Resources);
        Log.Timing("icons");
        Settings = AppSettings.Load();
        if (Settings.ApplyInstallerLanguage()) Settings.Save();
        var history = History.Load();
        Log.Timing("settings and history");
        Loc.I.SetLanguage(Settings.Language);
        ThemeManager.AccentOverride = Settings.AccentColor;
        ThemeManager.Apply(ThemeManager.Find(Settings, Settings.Theme));
        Log.Timing("resources and settings ready");

        var window = new MainWindow(Settings, history, files);
        Log.Timing("main window created");
        MainWindow = window;
        window.Show();
        Log.Timing("main window shown");

        if (_instance.IsPrimary)
            _instance.StartServer(list => Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => window.OnOtherInstance(list)));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
