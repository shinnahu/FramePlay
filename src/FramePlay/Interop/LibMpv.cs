using System.Runtime.InteropServices;
using System.Text;

namespace FramePlay.Interop;

/// <summary>P/Invoke bindings for libmpv-2.dll (client API 2.x and the render API).</summary>
public static class LibMpv
{
    private const string Dll = "libmpv-2.dll";

    public enum Format
    {
        None = 0, String = 1, OsdString = 2, Flag = 3, Int64 = 4, Double = 5, Node = 6, NodeArray = 7, NodeMap = 8, ByteArray = 9,
    }

    public enum EventId
    {
        None = 0, Shutdown = 1, LogMessage = 2, GetPropertyReply = 3, SetPropertyReply = 4, CommandReply = 5,
        StartFile = 6, EndFile = 7, FileLoaded = 8, Idle = 11, Tick = 14, ClientMessage = 16, VideoReconfig = 17,
        AudioReconfig = 18, Seek = 20, PlaybackRestart = 21, PropertyChange = 22, QueueOverflow = 24, Hook = 25,
    }

    public enum EndFileReason { Eof = 0, Stop = 2, Quit = 3, Error = 4, Redirect = 5 }

    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        public EventId EventId;
        public int Error;
        public ulong ReplyUserdata;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventProperty
    {
        public IntPtr Name;
        public Format Format;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventEndFile
    {
        public EndFileReason Reason;
        public int Error;
        public long PlaylistEntryId;
        public long PlaylistInsertId;
        public int PlaylistInsertNumEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventLogMessage
    {
        public IntPtr Prefix;
        public IntPtr Level;
        public IntPtr Text;
        public int LogLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventClientMessage
    {
        public int NumArgs;
        public IntPtr Args;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern ulong mpv_client_api_version();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr mpv_create();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_initialize(IntPtr ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void mpv_terminate_destroy(IntPtr ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_set_option_string(IntPtr ctx, byte[] name, byte[] data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_set_property_string(IntPtr ctx, byte[] name, byte[] data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr mpv_get_property_string(IntPtr ctx, byte[] name);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_set_property(IntPtr ctx, byte[] name, Format format, ref double data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_set_property(IntPtr ctx, byte[] name, Format format, ref long data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_set_property(IntPtr ctx, byte[] name, Format format, ref int data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_get_property(IntPtr ctx, byte[] name, Format format, out double data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_get_property(IntPtr ctx, byte[] name, Format format, out long data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_get_property(IntPtr ctx, byte[] name, Format format, out int data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_command(IntPtr ctx, IntPtr[] args);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_command_async(IntPtr ctx, ulong replyUserdata, IntPtr[] args);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_observe_property(IntPtr ctx, ulong replyUserdata, byte[] name, Format format);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void mpv_wakeup(IntPtr ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void mpv_free(IntPtr data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr mpv_error_string(int error);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_request_log_messages(IntPtr ctx, byte[] minLevel);

    // ---- render API (software renderer, used for seek-bar thumbnails) ----
    public enum RenderParamType
    {
        Invalid = 0, ApiType = 1, SwSize = 17, SwFormat = 18, SwStride = 19, SwPointer = 20,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderParam
    {
        public RenderParamType Type;
        public IntPtr Data;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RenderUpdateFn(IntPtr ctx);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, RenderParam[] parameters);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int mpv_render_context_render(IntPtr ctx, RenderParam[] parameters);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void mpv_render_context_set_update_callback(IntPtr ctx, RenderUpdateFn? callback, IntPtr callbackCtx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern ulong mpv_render_context_update(IntPtr ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void mpv_render_context_free(IntPtr ctx);

    // ---- helpers ----
    public static byte[] Utf8(string s)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
        return bytes;
    }

    public static string? FromUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    public static string ErrorString(int code) => FromUtf8(mpv_error_string(code)) ?? code.ToString();

    /// <summary>Builds a NULL-terminated array of UTF-8 strings for mpv_command; free with <see cref="FreeArgs"/>.</summary>
    public static IntPtr[] AllocArgs(IReadOnlyList<string> args)
    {
        var arr = new IntPtr[args.Count + 1];
        for (int i = 0; i < args.Count; i++)
            arr[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
        arr[args.Count] = IntPtr.Zero;
        return arr;
    }

    public static void FreeArgs(IntPtr[] arr)
    {
        foreach (var p in arr)
            if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
    }
}
