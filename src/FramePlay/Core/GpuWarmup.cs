using System.Runtime.InteropServices;

namespace FramePlay.Core;

/// <summary>
/// Loads the graphics driver for Direct3D 11 on a background thread as FramePlay starts. mpv's first
/// Direct3D 11 device otherwise spends a long moment (~140 ms on a GeForce) loading the driver right when
/// the video should appear; started here, that happens while WPF builds the window. The warm-up device is
/// kept until mpv has made its own, so the driver stays loaded.
/// </summary>
internal static class GpuWarmup
{
    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    private const int DriverTypeHardware = 1;
    private const uint CreateDeviceBgraSupport = 0x20;
    private const uint SdkVersion = 7;

    private static IntPtr _device, _context;
    private static int _released;

    public static void Start()
    {
        var thread = new Thread(() =>
        {
            try
            {
                int hr = D3D11CreateDevice(IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, CreateDeviceBgraSupport,
                    IntPtr.Zero, 0, SdkVersion, out var device, out _, out var context);
                if (hr < 0) return;
                _device = device;
                _context = context;
                Log.Timing("graphics driver loaded");
                if (Volatile.Read(ref _released) != 0) Release(); // mpv was quicker; nothing to keep
            }
            catch (Exception ex)
            {
                Log.Warn("Graphics driver warm-up: " + ex.Message);
            }
        })
        { IsBackground = true, Name = "GPU warm-up" };
        thread.Start();
    }

    /// <summary>mpv has its own device now (or there's no video): let the warm-up device go.</summary>
    public static void Release()
    {
        Volatile.Write(ref _released, 1);
        var context = Interlocked.Exchange(ref _context, IntPtr.Zero);
        var device = Interlocked.Exchange(ref _device, IntPtr.Zero);
        if (context != IntPtr.Zero) Marshal.Release(context);
        if (device != IntPtr.Zero) Marshal.Release(device);
    }
}
