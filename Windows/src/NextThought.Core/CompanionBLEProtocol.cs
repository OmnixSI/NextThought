using System.Text;

namespace NextThought.Core;

/// <summary>
/// BLE wire format for the phone companion (port of CompanionBLEProtocol.swift): 8-byte
/// big-endian header (message id, part, count) before every fragment.
/// </summary>
public static class CompanionBLEProtocol
{
    public const string Service = "9A8F0001-1C28-4E55-BE7D-C4E9A13277B0";
    public const string State = "9A8F0002-1C28-4E55-BE7D-C4E9A13277B0";
    public const string Command = "9A8F0003-1C28-4E55-BE7D-C4E9A13277B0";
    public const int MaximumBytes = 384 * 1024;

    public static IReadOnlyList<byte[]> Fragments(byte[] data, uint id, int mtu)
    {
        if (data.Length == 0 || data.Length > MaximumBytes || mtu < 20) return [];
        var payload = Math.Min(512, mtu) - 8;
        var count = (data.Length + payload - 1) / payload;
        if (count > 32768) return [];
        var fragments = new List<byte[]>(count);
        for (var index = 0; index < count; index++)
        {
            var header = new byte[8];
            header[0] = (byte)(id >> 24 & 255);
            header[1] = (byte)(id >> 16 & 255);
            header[2] = (byte)(id >> 8 & 255);
            header[3] = (byte)(id & 255);
            header[4] = (byte)(index >> 8 & 255);
            header[5] = (byte)(index & 255);
            header[6] = (byte)(count >> 8 & 255);
            header[7] = (byte)(count & 255);
            var end = Math.Min(data.Length, (index + 1) * payload);
            var packet = new byte[8 + end - index * payload];
            header.CopyTo(packet, 0);
            Array.Copy(data, index * payload, packet, 8, end - index * payload);
            fragments.Add(packet);
        }
        return fragments;
    }

    public sealed class Reassembler
    {
        private uint? id;
        private int count;
        private readonly Dictionary<int, byte[]> chunks = [];
        private int bytes;
        private DateTimeOffset started = DateTimeOffset.MinValue;

        /// <summary>Returns the whole message once every part arrived; duplicate parts are ignored.</summary>
        public (uint Id, byte[] Data)? Receive(byte[] packet, DateTimeOffset? now = null)
        {
            var current = now ?? DateTimeOffset.UtcNow;
            if (packet.Length <= 8 || packet.Length > 512) return null;
            var message = (uint)(packet[0] << 24 | packet[1] << 16 | packet[2] << 8 | packet[3]);
            var part = packet[4] << 8 | packet[5];
            var total = packet[6] << 8 | packet[7];
            if (total <= 0 || total > 32768 || part >= total) return null;
            if (id != message || (current - started).TotalSeconds > 15)
            {
                id = message;
                count = total;
                chunks.Clear();
                bytes = 0;
                started = current;
            }
            if (count != total) return null;
            if (!chunks.ContainsKey(part))
            {
                chunks[part] = packet[8..];
                bytes += packet.Length - 8;
                started = current;
            }
            if (bytes > MaximumBytes)
            {
                id = null;
                chunks.Clear();
                bytes = 0;
                return null;
            }
            if (chunks.Count != count) return null;
            var data = new List<byte>(bytes);
            for (var index = 0; index < count; index++)
            {
                if (!chunks.TryGetValue(index, out var chunk)) return null;
                data.AddRange(chunk);
            }
            id = null;
            chunks.Clear();
            bytes = 0;
            return (message, data.ToArray());
        }
    }
}

/// <summary>
/// Reliable notification outbox: current transfer plus one replaceable latest state
/// (port of CompanionBLEOutbox). A missing ACK replays exactly the same message id.
/// </summary>
public sealed class CompanionBLEOutbox
{
    private byte[]? state;
    private readonly List<byte[]> acknowledgements = [];
    private List<byte[]> packets = [];
    private int cursor;
    private DateTimeOffset? awaiting;
    private int retries;
    private uint counter;

    public uint? ActiveMessageId { get; private set; }
    public int FailureCount { get; private set; }

    public CompanionBLEOutbox(uint seed)
    {
        counter = seed;
    }

    public void EnqueueState(byte[] data)
    {
        if (data.Length <= CompanionBLEProtocol.MaximumBytes) state = data;
    }

    public void EnqueueAcknowledgement(byte[] data)
    {
        if (data.Length > CompanionBLEProtocol.MaximumBytes) return;
        if (acknowledgements.Count >= 8) acknowledgements.RemoveAt(0);
        acknowledgements.Add(data);
    }

    public void Acknowledge(uint id)
    {
        if (ActiveMessageId != id) return;
        packets = [];
        cursor = 0;
        awaiting = null;
        retries = 0;
        ActiveMessageId = null;
    }

    public byte[]? Peek(int mtu, DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        if (awaiting is { } sent && (current - sent).TotalSeconds > 2)
        {
            awaiting = null;
            if (retries < 3) { retries++; cursor = 0; }
            else { packets = []; cursor = 0; retries = 0; ActiveMessageId = null; FailureCount++; }
        }
        if (packets.Count == 0)
        {
            byte[] data;
            if (acknowledgements.Count > 0)
            {
                data = acknowledgements[0];
                acknowledgements.RemoveAt(0);
            }
            else if (state is not null)
            {
                data = state;
                state = null;
            }
            else
            {
                return null;
            }
            unchecked { counter++; }
            ActiveMessageId = counter;
            packets = CompanionBLEProtocol.Fragments(data, counter, mtu).ToList();
            cursor = 0;
            retries = 0;
        }
        if (awaiting is not null || cursor >= packets.Count) return null;
        return packets[cursor];
    }

    public void DidSend(DateTimeOffset? now = null)
    {
        if (awaiting is not null || cursor >= packets.Count) return;
        cursor++;
        if (cursor == packets.Count) awaiting = now ?? DateTimeOffset.UtcNow;
    }
}
