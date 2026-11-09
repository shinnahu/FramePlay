using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FramePlay.Core;

/// <summary>
/// One FramePlay window per user profile when requested: a second launch hands its files to the running
/// window over a named pipe and exits. Same approach as MarkNotes.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    private readonly string _key;
    private Mutex? _mutex;
    private Thread? _server;

    public bool IsPrimary { get; private set; }

    public SingleInstance()
    {
        var basis = (AppPaths.ConfigDir + "|" + Environment.UserName).ToLowerInvariant();
        uint h = 2166136261;
        foreach (var c in basis) { h ^= c; h *= 16777619; }
        _key = h.ToString("x8");
    }

    private string PipeName => "FramePlay.Pipe." + _key;

    /// <summary>Claims the primary role if no other FramePlay window owns it.</summary>
    public void Claim()
    {
        _mutex = new Mutex(true, "Local\\FramePlay." + _key, out bool created);
        IsPrimary = created;
    }

    /// <summary>Sends the request to the running window. Returns false if it could not be reached.</summary>
    public bool TrySend(IReadOnlyList<string> files, bool activateOnly)
    {
        try
        {
            AllowSetForegroundWindow(ASFW_ANY);
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            var payload = JsonSerializer.Serialize(new Message { Files = files.ToList(), ActivateOnly = activateOnly });
            var bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SingleInstance.TrySend", ex);
            return false;
        }
    }

    public void StartServer(Action<List<string>> onFiles)
    {
        _server = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                    server.WaitForConnection();
                    using var ms = new MemoryStream();
                    server.CopyTo(ms);
                    var msg = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(ms.ToArray()));
                    onFiles(msg?.Files ?? new List<string>());
                }
                catch (Exception ex)
                {
                    Log.Error("SingleInstance.Server", ex);
                    Thread.Sleep(500);
                }
            }
        })
        { IsBackground = true, Name = "FramePlay.Pipe" };
        _server.Start();
    }

    public void Dispose()
    {
        try { if (IsPrimary) _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
    }

    private sealed class Message
    {
        public List<string> Files { get; set; } = new();
        public bool ActivateOnly { get; set; }
    }
}
