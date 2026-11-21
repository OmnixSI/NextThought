using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record CompanionSnapshot(string Status = "待机", string Question = "", string Answer = "", bool Answering = false,
    string Candidate = "", string Position = "", string? SessionID = null, string? Sources = null,
    string? AnswerID = null, string? Phase = null, string? Slide = null, bool? UpdateAvailable = null);
public sealed record CompanionEnvelope(int Version, string ServerID, long Sequence, double UpdatedAt, CompanionSnapshot Snapshot);
public sealed record CompanionAddress(IPAddress Address, string Adapter) { public override string ToString() => $"{Adapter} · {Address}"; }

/// <summary>Opt-in trusted-LAN server, compatible with upstream browser/native LAN v1.
/// No URL ACL, firewall rule, relay or background service is installed.</summary>
public sealed class CompanionServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentDictionary<int, (TcpClient Client, Task Task)> clients = new();
    private readonly Func<string, Task<bool>> action;
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private readonly Dictionary<Guid, DateTimeOffset> commands = [];
    private readonly object sync = new();
    private CompanionEnvelope envelope = new(1, Guid.NewGuid().ToString(), 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), new());
    private readonly string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private readonly byte[] tokenBytes;
    private Task acceptTask = Task.CompletedTask;
    private int id, streams;
    private static readonly string[] Allowed = ["answer", "cancel", "regenerate", "previous", "next", "shorter", "example", "english", "chinese"];
    public Uri BaseUri { get; }
    public string PairingUrl { get; }
    public bool Running => !lifetime.IsCancellationRequested;
    public int ConnectionCount => clients.Count;

    /// <summary>The last state published to the phone (also the first frame the relay sends).</summary>
    public CompanionSnapshot Current { get { lock (sync) return envelope.Snapshot; } }
    public static bool IsLanAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        var b = address.GetAddressBytes();
        // F12: match Mac's LAN scope exactly (RFC 1918 + loopback only).
        return b.Length == 4 && (b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] is >= 16 and <= 31);
    }
    public static IReadOnlyList<CompanionAddress> Addresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 2)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address) && IsLanAddress(a.Address)).Select(a => new CompanionAddress(a.Address, n.Name)))
        .ToArray();
    public CompanionServer(IPAddress address, Func<string, Task<bool>> action, int port = 0)
    {
        if (!IsLanAddress(address)) throw new ArgumentException("请选择本机局域网 IPv4 地址。");
        this.action = action; tokenBytes = Encoding.ASCII.GetBytes(token); listener = new(address, port); listener.Start(16);
        BaseUri = new($"http://{address}:{((IPEndPoint)listener.LocalEndpoint).Port}");
        // Fragment stays out of the initial page request and Referrer headers. Each service lifetime rotates the token.
        var pairing = JsonSerializer.SerializeToUtf8Bytes(new { v = 1, lan = BaseUri.GetLeftPart(UriPartial.Authority), token }, JsonDefaults.Options);
        PairingUrl = BaseUri + "#p=" + Convert.ToBase64String(pairing).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        acceptTask = AcceptAsync();
    }
    public void Publish(CompanionSnapshot snapshot)
    {
        // Companion deliberately excludes transcripts, materials, screenshots and API credentials.
        snapshot = snapshot with { Status = Limit(snapshot.Status, 300), Question = Limit(snapshot.Question, 12000), Answer = Limit(snapshot.Answer, 100000), Candidate = Limit(snapshot.Candidate, 12000), Sources = Limit(snapshot.Sources ?? "", 2000) };
        lock (sync) if (snapshot != envelope.Snapshot) envelope = envelope with { Sequence = envelope.Sequence + 1, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Snapshot = snapshot };
    }
    private static string Limit(string value, int length) => value.Length <= length ? value : value[..(char.IsHighSurrogate(value[length - 1]) ? length - 1 : length)];
    private byte[] State() { lock (sync) return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonDefaults.Options); }
    private async Task AcceptAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(lifetime.Token).ConfigureAwait(false);
                if (clients.Count >= 24 || !IsLanAddress(((IPEndPoint)client.Client.RemoteEndPoint!).Address)) { client.Dispose(); continue; }
                var key = Interlocked.Increment(ref id); var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var task = Task.Run(async () => { await start.Task.ConfigureAwait(false); try { await ServeAsync(client).ConfigureAwait(false); } finally { client.Dispose(); clients.TryRemove(key, out _); } });
                clients[key] = (client, task); start.SetResult();
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { }
        finally { listener.Stop(); foreach (var item in clients.Values) item.Client.Dispose(); }
    }
    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); deadline.CancelAfter(TimeSpan.FromSeconds(8));
            var stream = client.GetStream(); var buffer = new byte[8192]; var count = 0; var end = -1;
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(count, 1), deadline.Token).ConfigureAwait(false); if (read == 0) return; count += read;
                if (count >= 4 && buffer.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8)) { end = count; break; }
            }
            if (end < 0) { await ReplyAsync(stream, 431, "text/plain", [], deadline.Token); return; }
            var header = Encoding.ASCII.GetString(buffer, 0, count).Split("\r\n"); var first = header[0].Split(' ');
            if (first.Length != 3 || first[2] != "HTTP/1.1" || !first[1].StartsWith('/') || first[1].StartsWith("//")) { await ReplyAsync(stream, 400, "text/plain", [], deadline.Token); return; }
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in header.Skip(1).Where(l => l.Length > 0))
            {
                var colon = line.IndexOf(':'); if (colon <= 0 || !headers.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim())) { await ReplyAsync(stream, 400, "text/plain", [], deadline.Token); return; }
            }
            if (!headers.TryGetValue("Host", out var host) || !string.Equals(host, BaseUri.Authority, StringComparison.OrdinalIgnoreCase)
                || headers.ContainsKey("Transfer-Encoding") || headers.TryGetValue("Content-Length", out var size) && size != "0"
                || headers.TryGetValue("Origin", out var origin) && origin != BaseUri.GetLeftPart(UriPartial.Authority))
            { await ReplyAsync(stream, 403, "text/plain", [], deadline.Token); return; }
            var uri = new Uri(BaseUri, first[1]); var path = uri.AbsolutePath;
            if (first[0] == "GET" && path is "/" or "/phone.js")
            {
                var name = path == "/" ? "index.html" : "phone.js";
                using var asset = Assembly.GetExecutingAssembly().GetManifestResourceStream("NextThought.Platform.Resources.Companion." + name)!;
                using var bytes = new MemoryStream(); await asset.CopyToAsync(bytes, deadline.Token);
                await ReplyAsync(stream, 200, path == "/" ? "text/html; charset=utf-8" : "text/javascript; charset=utf-8", bytes.ToArray(), deadline.Token); return;
            }
            var query = new Dictionary<string, string>();
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var values = pair.Split('=', 2); if (!query.TryAdd(Uri.UnescapeDataString(values[0]), values.Length == 2 ? Uri.UnescapeDataString(values[1]) : "")) { await ReplyAsync(stream, 400, "text/plain", [], deadline.Token); return; }
            }
            var provided = Encoding.ASCII.GetBytes(query.GetValueOrDefault("t", ""));
            if (!CryptographicOperations.FixedTimeEquals(provided, tokenBytes)) { await ReplyAsync(stream, 403, "text/plain", [], deadline.Token); return; }
            if (first[0] == "GET" && path == "/snapshot") { await ReplyAsync(stream, 200, "application/json", State(), deadline.Token); return; }
            if (first[0] == "POST" && path == "/ack") { await ReplyAsync(stream, 204, "text/plain", [], deadline.Token); return; }
            if (first[0] == "POST" && path == "/action")
            {
                if (!Guid.TryParse(query.GetValueOrDefault("id"), out var command) || !Allowed.Contains(query.GetValueOrDefault("a"))) { await ReplyAsync(stream, 400, "text/plain", [], deadline.Token); return; }
                await commandGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                bool accepted;
                try
                {
                    foreach (var old in commands.Where(p => DateTimeOffset.UtcNow - p.Value >= TimeSpan.FromMinutes(5)).Select(p => p.Key).ToArray()) commands.Remove(old);
                    accepted = commands.ContainsKey(command);
                    if (!accepted && commands.Count < 1024)
                    {
                        // F10: record the command and answer at once; the phone has an 8s request
                        // deadline and a retry of the same id must not run the action twice.
                        commands[command] = DateTimeOffset.UtcNow;
                        accepted = true;
                        var requestedAction = query["a"];
                        _ = Task.Run(async () =>
                        {
                            try { await action(requestedAction).ConfigureAwait(false); }
                            catch (Exception) { /* the action failure is reported through state */ }
                        }, lifetime.Token);
                    }
                }
                finally { commandGate.Release(); }
                await ReplyAsync(stream, accepted ? 204 : 409, "text/plain", [], deadline.Token); return;
            }
            if (first[0] != "GET" || path != "/events") { await ReplyAsync(stream, 404, "text/plain", [], deadline.Token); return; }
            if (Interlocked.Increment(ref streams) > 8) { Interlocked.Decrement(ref streams); await ReplyAsync(stream, 429, "text/plain", [], deadline.Token); return; }
            try
            {
                deadline.CancelAfter(Timeout.InfiniteTimeSpan); await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: close\r\nX-Content-Type-Options: nosniff\r\n\r\n"), deadline.Token);
                while (!deadline.IsCancellationRequested)
                {
                    using var write = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); write.CancelAfter(TimeSpan.FromSeconds(3));
                    await stream.WriteAsync("data: "u8.ToArray(), write.Token); await stream.WriteAsync(State(), write.Token); await stream.WriteAsync("\n\n"u8.ToArray(), write.Token);
                    await Task.Delay(1000, deadline.Token).ConfigureAwait(false);
                }
            }
            finally { Interlocked.Decrement(ref streams); }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or UriFormatException) { }
    }
    private static async Task ReplyAsync(NetworkStream stream, int status, string type, byte[] body, CancellationToken ct)
    {
        var head = $"HTTP/1.1 {status} Response\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'self'; script-src 'self'; style-src 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct); if (body.Length > 0) await stream.WriteAsync(body, ct);
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel(); listener.Stop(); foreach (var client in clients.Values) client.Client.Dispose();
        await acceptTask.ConfigureAwait(false); await Task.WhenAll(clients.Values.Select(c => c.Task)).ConfigureAwait(false);
    }
}
