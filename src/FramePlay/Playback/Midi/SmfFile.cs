using System.IO;
using System.Text;

namespace FramePlay.Playback.Midi;

/// <summary>
/// Minimal Standard MIDI File reader (format 0 and 1, .mid/.midi/.kar, and RIFF .rmi).
/// Produces channel events with absolute times in seconds, using the file's tempo map.
/// </summary>
public sealed class SmfFile
{
    public readonly struct Event(double time, byte status, byte data1, byte data2)
    {
        public readonly double Time = time;
        public readonly byte Status = status;
        public readonly byte Data1 = data1;
        public readonly byte Data2 = data2;
        public int Channel => Status & 0x0F;
        public int Command => Status & 0xF0;
    }

    public List<Event> Events { get; } = new();
    public double Duration { get; private set; }
    public string? Title { get; private set; }

    private readonly struct RawEvent(long tick, int order, byte status, byte d1, byte d2, int tempo)
    {
        public readonly long Tick = tick;
        public readonly int Order = order;
        public readonly byte Status = status;
        public readonly byte D1 = d1;
        public readonly byte D2 = d2;
        public readonly int Tempo = tempo; // > 0 for tempo meta events
    }

    public static SmfFile Load(string path)
    {
        var data = File.ReadAllBytes(path);
        int start = 0;
        if (data.Length > 12 && Ascii(data, 0) == "RIFF" && Ascii(data, 8) == "RMID")
        {
            // RIFF MIDI: the SMF lives in the "data" chunk.
            int p = 12;
            while (p + 8 <= data.Length)
            {
                string id = Ascii(data, p);
                int len = BitConverter.ToInt32(data, p + 4);
                if (id == "data") { start = p + 8; break; }
                p += 8 + len + (len & 1);
            }
        }
        return Parse(data, start);
    }

    private static string Ascii(byte[] d, int i) => Encoding.ASCII.GetString(d, i, 4);

    private static SmfFile Parse(byte[] d, int pos)
    {
        if (pos + 14 > d.Length || Ascii(d, pos) != "MThd") throw new InvalidDataException("Not a MIDI file");
        int headerLen = BE32(d, pos + 4);
        int ntracks = BE16(d, pos + 10);
        int division = BE16(d, pos + 12);
        pos += 8 + headerLen;

        var raw = new List<RawEvent>(4096);
        int order = 0;
        long lastTick = 0;
        string? title = null;

        for (int t = 0; t < ntracks && pos + 8 <= d.Length; t++)
        {
            if (Ascii(d, pos) != "MTrk")
            {
                // Skip unknown chunks.
                pos += 8 + BE32(d, pos + 4);
                t--;
                continue;
            }
            int len = BE32(d, pos + 4);
            int p = pos + 8, end = Math.Min(d.Length, p + len);
            pos = end;
            long tick = 0;
            byte running = 0;
            while (p < end)
            {
                tick += ReadVlq(d, ref p);
                if (p >= end) break;
                byte b = d[p];
                if (b == 0xFF)
                {
                    byte type = d[p + 1];
                    p += 2;
                    int mlen = (int)ReadVlq(d, ref p);
                    if (type == 0x51 && mlen >= 3)
                        raw.Add(new RawEvent(tick, order++, 0, 0, 0, (d[p] << 16) | (d[p + 1] << 8) | d[p + 2]));
                    else if (type == 0x03 && t <= 1 && title == null && mlen > 0)
                        title = Encoding.Latin1.GetString(d, p, Math.Min(mlen, end - p)).Trim('\0', ' ');
                    p += mlen;
                    if (type == 0x2F) break;
                    continue;
                }
                if (b == 0xF0 || b == 0xF7)
                {
                    p++;
                    int slen = (int)ReadVlq(d, ref p);
                    p += slen;
                    continue;
                }
                byte status;
                if ((b & 0x80) != 0) { status = b; p++; running = status; }
                else status = running; // running status
                if (status == 0) { p++; continue; }
                int cmd = status & 0xF0;
                byte d1 = p < end ? d[p++] : (byte)0;
                byte d2 = 0;
                if (cmd != 0xC0 && cmd != 0xD0) d2 = p < end ? d[p++] : (byte)0;
                raw.Add(new RawEvent(tick, order++, status, d1, d2, 0));
            }
            lastTick = Math.Max(lastTick, tick);
        }

        raw.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));

        var file = new SmfFile { Title = string.IsNullOrWhiteSpace(title) ? null : title };
        double seconds = 0;
        long prevTick = 0;
        double tempo = 500000; // microseconds per quarter note (120 bpm)
        bool smpte = (division & 0x8000) != 0;
        double secondsPerTickSmpte = 0;
        if (smpte)
        {
            int fps = -(sbyte)(division >> 8);
            int ticksPerFrame = division & 0xFF;
            secondsPerTickSmpte = 1.0 / (Math.Max(1, fps) * Math.Max(1, ticksPerFrame));
        }
        int tpq = Math.Max(1, division & 0x7FFF);

        foreach (var e in raw)
        {
            long dt = e.Tick - prevTick;
            seconds += smpte ? dt * secondsPerTickSmpte : dt * tempo / 1_000_000.0 / tpq;
            prevTick = e.Tick;
            if (e.Tempo > 0) { tempo = e.Tempo; continue; }
            file.Events.Add(new Event(seconds, e.Status, e.D1, e.D2));
        }
        long tailTicks = lastTick - prevTick;
        seconds += smpte ? tailTicks * secondsPerTickSmpte : tailTicks * tempo / 1_000_000.0 / tpq;
        file.Duration = seconds;
        return file;
    }

    private static int BE16(byte[] d, int i) => (d[i] << 8) | d[i + 1];
    private static int BE32(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

    private static long ReadVlq(byte[] d, ref int p)
    {
        long v = 0;
        for (int i = 0; i < 4 && p < d.Length; i++)
        {
            byte b = d[p++];
            v = (v << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return v;
    }
}
