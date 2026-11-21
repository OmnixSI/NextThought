using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// Optional encrypted relay channel (port of CompanionRelay.swift). The relay only routes
/// ciphertext: it creates a room, the desktop joins as publisher, the phone as subscriber;
/// state frames go up, command frames come down, acknowledgements go up again.
/// </summary>
public sealed class CompanionRelayClient : IAsyncDisposable
{
    private sealed record RoomResponse(string Room, string Publisher, string Subscriber);

    private readonly Func<CompanionSnapshot> snapshot;
    private readonly Func<string, Task<bool>> perform;
    private readonly Action<string> status;
    private readonly CompanionCommandCache commands = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly object sync = new();
    private readonly string serverId = Guid.NewGuid().ToString("D");
    private CancellationTokenSource? lifetime;
    private Task? loop;
    private ClientWebSocket? socket;
    private Uri? baseUri;
    private string publisher = "";
    private long sequence;
    private CompanionSnapshot? pending;
    private int pumping;
    private bool connected;

    public CompanionRelayClient(Func<CompanionSnapshot> snapshot, Func<string, Task<bool>> perform, Action<string>? status = null)
    {
        this.snapshot = snapshot;
        this.perform = perform;
        this.status = status ?? (_ => { });
    }

    public CompanionPairing? Pairing { get; private set; }

    public bool Connected => connected;

    public string StatusText { get; private set; } = "中转未连接";

    /// <summary>
    /// Creates a room and starts the reconnect loop. HTTPS is required; the loopback exception
    /// exists for the offline relay check (the Mac app gates it the same way).
    /// </summary>
    public async Task<CompanionPairing> StartAsync(Uri baseUri, CompanionPairing local, bool allowLoopback, CancellationToken ct = default)
    {
        var loopback = allowLoopback && baseUri.Scheme == "http" && baseUri.Host == "127.0.0.1";
        if (!loopback && baseUri.Scheme != "https") throw new InvalidOperationException("中转入口必须为 HTTPS 地址。");
        if (!string.IsNullOrEmpty(baseUri.UserInfo)) throw new InvalidOperationException("中转入口不能带用户名或密码。");
        await StopAsync().ConfigureAwait(false);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await http.PostAsync(new Uri(baseUri, "rooms"), null, ct).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException("中转无法创建房间，请检查服务容量或地址。");
        var room = JsonSerializer.Deserialize<RoomResponse>(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false), CompanionCrypto.Json)
                   ?? throw new InvalidOperationException("中转返回的房间信息无法解析。");

        var pairing = local with
        {
            Relay = baseUri.AbsoluteUri.TrimEnd('/'),
            Room = room.Room,
            Subscriber = room.Subscriber
        };
        Pairing = pairing;
        publisher = room.Publisher;
        this.baseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        loop = Task.Run(() => RunAsync(pairing, token), CancellationToken.None);
        return pairing;
    }

    /// <summary>Latest snapshot for the relay (coalesced; a slow link only sends the newest).</summary>
    public void Publish(CompanionSnapshot value)
    {
        lock (sync) pending = value;
        if (socket is { State: WebSocketState.Open } && Interlocked.CompareExchange(ref pumping, 1, 0) == 0)
            _ = PumpAsync();
    }

    private async Task RunAsync(CompanionPairing pairing, CancellationToken ct)
    {
        var attempts = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(pairing, ct).ConfigureAwait(false);
                attempts = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetStatus("中转正在恢复连接：" + ex.Message);
            }
            if (ct.IsCancellationRequested) return;
            attempts = Math.Min(attempts + 1, 4);
            var delay = Math.Min(8, 0.5 * Math.Pow(2, attempts)) * (0.8 + Random.Shared.NextDouble() * 0.4);
            try { await Task.Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ConnectAsync(CompanionPairing pairing, CancellationToken ct)
    {
        if (baseUri is null) return;
        var builder = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme == "https" ? "wss" : "ws",
            Path = baseUri.AbsolutePath.TrimEnd('/') + "/socket",
            Query = ""
        };
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
        socket = ws;
        await ws.ConnectAsync(builder.Uri, ct).ConfigureAwait(false);
        var join = JsonSerializer.SerializeToUtf8Bytes(new { room = pairing.Room, role = "publisher", token = publisher }, CompanionCrypto.Json);
        await ws.SendAsync(join, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return;
                message.Write(buffer, 0, result.Count);
                if (message.Length > 400_000) throw new InvalidOperationException("中转消息过大。");
            } while (!result.EndOfMessage);

            using var document = JsonDocument.Parse(message.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) continue;
            if (root.TryGetProperty("joined", out var joined) && joined.ValueKind == JsonValueKind.True)
            {
                connected = true;
                SetStatus("中转已连接 · 端到端加密");
                Publish(snapshot());
                continue;
            }
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "command") continue;
            var frame = JsonSerializer.Deserialize<CompanionCipherFrame>(root.GetRawText(), CompanionCrypto.Json);
            if (frame is null) continue;
            CompanionCommand? command;
            try { command = CompanionCrypto.Open<CompanionCommand>(frame, pairing); }
            catch (Exception) { continue; }
            if (!command.IsFresh() || !commands.Accept(command.Id)) continue;
            if (await perform(command.Action).ConfigureAwait(false))
            {
                var ack = CompanionCrypto.Seal(new CompanionAcknowledgement { Id = command.Id }, "ack", pairing);
                await SendAsync(JsonSerializer.SerializeToUtf8Bytes(ack, CompanionCrypto.Json), ct).ConfigureAwait(false);
            }
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                CompanionSnapshot? value;
                lock (sync) { value = pending; pending = null; }
                if (value is null) break;
                var pairing = Pairing;
                if (pairing is null || socket is not { State: WebSocketState.Open }) break;
                var envelope = new CompanionEnvelope(1, serverId, Interlocked.Increment(ref sequence),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), value);
                var frame = CompanionCrypto.Seal(envelope, "state", pairing);
                await SendAsync(JsonSerializer.SerializeToUtf8Bytes(frame, CompanionCrypto.Json), CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            SetStatus("中转发送失败：" + ex.Message);
            socket?.Abort();
        }
        finally
        {
            Interlocked.Exchange(ref pumping, 0);
            bool more;
            lock (sync) more = pending is not null;
            if (more && socket is { State: WebSocketState.Open } && Interlocked.CompareExchange(ref pumping, 1, 0) == 0) _ = PumpAsync();
        }
    }

    private async Task SendAsync(byte[] payload, CancellationToken ct)
    {
        if (socket is not { State: WebSocketState.Open } ws) throw new InvalidOperationException("中转未连接。");
        await sendGate.WaitAsync(ct).ConfigureAwait(false);
        try { await ws.SendAsync(payload, WebSocketMessageType.Text, true, ct).ConfigureAwait(false); }
        finally { sendGate.Release(); }
    }

    private void SetStatus(string message)
    {
        StatusText = message;
        connected = message.StartsWith("中转已连接", StringComparison.Ordinal);
        status(message);
    }

    /// <summary>Leaves the room (best effort) and stops reconnecting.</summary>
    public async Task StopAsync()
    {
        var cancellation = Interlocked.Exchange(ref lifetime, null);
        cancellation?.Cancel();
        var ws = socket;
        socket = null;
        if (ws is not null)
        {
            try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { }
            ws.Dispose();
        }
        if (baseUri is not null && Pairing is { } pairing)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, $"rooms/{pairing.Room}/close"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", publisher);
                using var response = await http.SendAsync(request).ConfigureAwait(false);
            }
            catch { }
        }
        cancellation?.Dispose();
        if (loop is { } task)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch { }
        }
        loop = null;
        Pairing = null;
        baseUri = null;
        publisher = "";
        lock (sync) pending = null;
        SetStatus("中转已断开");
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
