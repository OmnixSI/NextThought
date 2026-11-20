using System.Net.WebSockets;
using System.Text;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// Live cloud speech-to-text session: 16 kHz mono frames are forwarded to the provider
/// WebSocket, and assembled updates are raised for the caller to merge into the transcript.
/// Port of CloudStreamingTranscriber / CloudAudioSink: sends are bounded (80 in flight) so a
/// network that cannot keep up stops recognition with an error instead of buffering audio forever.
/// </summary>
public sealed class CloudSttSession : IAsyncDisposable
{
    /// <summary>CloudAudioSink closes the sink once this many sends are outstanding.</summary>
    private const int MaxInFlightSends = 80;

    private readonly CloudSTTProvider provider;
    private readonly ClientWebSocket socket = new();
    private readonly CloudTranscriptAssembler assembler;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private Task? receive;
    private int openAiSamples;
    private int inFlight;
    private int closed;
    private int failed;
    private volatile bool stopping;

    private CloudSttSession(CloudSTTProvider provider)
    {
        this.provider = provider;
        assembler = new CloudTranscriptAssembler(provider);
    }

    public event Action<CloudTranscriptUpdate>? Updated;
    public event Action<string>? Failed;

    public static async Task<CloudSttSession> StartAsync(
        CloudSTTProvider provider, string apiKey, string model, string language,
        IReadOnlyList<string> vocabulary, Uri? endpointOverride = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new CloudTranscriptException("请填写云端语音识别的 API Key。");
        var session = new CloudSttSession(provider);
        session.socket.Options.SetRequestHeader("Authorization", provider.AuthorizationHeader(apiKey));
        var endpoint = endpointOverride ?? new Uri(provider.Endpoint(model, language, vocabulary)!);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await session.socket.ConnectAsync(endpoint, timeout.Token);
        var config = provider.ConfigurationMessage(model, language, vocabulary);
        if (config is not null)
            await session.socket.SendAsync(Encoding.UTF8.GetBytes(config), WebSocketMessageType.Text, true, timeout.Token);
        session.receive = Task.Run(session.ReceiveLoop);
        return session;
    }

    public async Task SendAsync(short[] samples, CancellationToken ct = default)
    {
        if (samples.Length == 0 || Volatile.Read(ref closed) != 0 || socket.State != WebSocketState.Open) return;
        // F6: bound the number of frames waiting on the network. Without this every capture tick
        // starts a fire-and-forget send and a slow connection grows the pending work without limit.
        if (Interlocked.Increment(ref inFlight) > MaxInFlightSends)
        {
            Interlocked.Decrement(ref inFlight);
            ReportFailure("网络上传速度跟不上实时音频，已停止云端识别。");
            return;
        }
        try
        {
            var pcm = provider == CloudSTTProvider.OpenAI ? PCMResampler.Upsample16kTo24k(samples) : samples;
            var bytes = PCMResampler.LittleEndianData(pcm);
            await sendLock.WaitAsync(ct);
            try
            {
                if (socket.State != WebSocketState.Open || Volatile.Read(ref closed) != 0) return;
                if (provider.SendsBinaryAudio())
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Binary, true, ct);
                }
                else
                {
                    await socket.SendAsync(Encoding.UTF8.GetBytes(CloudSTTProviders.OpenAiAppendMessage(bytes)), WebSocketMessageType.Text, true, ct);
                    // With turn_detection disabled, commit about every two seconds so partial items
                    // are transcribed during the meeting instead of only at stop.
                    openAiSamples += samples.Length;
                    if (openAiSamples >= 32000)
                    {
                        openAiSamples = 0;
                        await socket.SendAsync(Encoding.UTF8.GetBytes(provider.CloseMessage()), WebSocketMessageType.Text, true, ct);
                    }
                }
            }
            finally { sendLock.Release(); }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // CloudAudioSink reports a failed send instead of silently dropping audio.
            if (!ct.IsCancellationRequested && !lifetime.IsCancellationRequested)
                ReportFailure("云端识别发送失败：" + ex.Message);
        }
        finally { Interlocked.Decrement(ref inFlight); }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                // F6: a close frame already arrived. Do not call ReceiveAsync again: the socket is
                // in the 'CloseReceived' state and the retry surfaces as a bogus "closed without
                // completing the close handshake" failure.
                if (socket.State is WebSocketState.Closed or WebSocketState.CloseReceived or WebSocketState.Aborted)
                {
                    await CompleteCloseHandshakeAsync();
                    break;
                }
                var result = await socket.ReceiveAsync(buffer, lifetime.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CompleteCloseHandshakeAsync();
                    break;
                }
                using var message = new MemoryStream();
                message.Write(buffer, 0, result.Count);
                var closedByPeer = false;
                while (!result.EndOfMessage)
                {
                    result = await socket.ReceiveAsync(buffer, lifetime.Token);
                    if (result.MessageType == WebSocketMessageType.Close) { closedByPeer = true; break; }
                    message.Write(buffer, 0, result.Count);
                }
                if (closedByPeer)
                {
                    await CompleteCloseHandshakeAsync();
                    break;
                }
                if (message.Length > 512 * 1024) throw new CloudTranscriptException("云端识别消息超过 512 KiB，已停止。");
                var text = Encoding.UTF8.GetString(message.ToArray());
                foreach (var update in assembler.Consume(text)) Updated?.Invoke(update);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // A close/abort while the caller is stopping is the normal end of a finish(), not a
            // recognition failure (Swift: a pending `finished` continuation resumes without onError).
            if (!lifetime.IsCancellationRequested && !stopping)
                ReportFailure(ex is CloudTranscriptException ? ex.Message : "云端识别断开：" + ex.Message);
        }
    }

    /// <summary>Answer a peer-initiated close so the handshake completes instead of aborting.</summary>
    private async Task CompleteCloseHandshakeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", lifetime.Token);
        }
        catch { }
    }

    private void ReportFailure(string message)
    {
        if (stopping || Interlocked.Exchange(ref failed, 1) != 0) return;
        Volatile.Write(ref closed, 1);
        Failed?.Invoke(message);
    }

    public async Task StopAsync()
    {
        stopping = true;
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                // Finish semantics from CloudStreamingTranscriber.finish(): send the provider's
                // end-of-audio message, wait briefly for the final results, then close.
                var close = provider.CloseMessage();
                if (close.Length > 0 && await sendLock.WaitAsync(TimeSpan.FromSeconds(2)))
                {
                    try
                    {
                        if (socket.State == WebSocketState.Open)
                            await socket.SendAsync(Encoding.UTF8.GetBytes(close), WebSocketMessageType.Text, true, lifetime.Token);
                    }
                    finally { sendLock.Release(); }
                }
                if (receive is not null) await WaitQuietlyAsync(receive, TimeSpan.FromSeconds(2));
                // A provider that keeps the socket open after the end message (Soniox waits for a
                // close frame) must still receive one; timing out above must not skip this step.
                if (socket.State == WebSocketState.Open && await sendLock.WaitAsync(TimeSpan.FromSeconds(2)))
                {
                    try
                    {
                        if (socket.State == WebSocketState.Open)
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", lifetime.Token);
                    }
                    finally { sendLock.Release(); }
                }
            }
            if (receive is not null) await WaitQuietlyAsync(receive, TimeSpan.FromSeconds(2));
        }
        catch { }
    }

    private static async Task WaitQuietlyAsync(Task task, TimeSpan timeout)
    {
        try { await task.WaitAsync(timeout); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        // Send the end-of-audio message and wait for the final result before cancelling:
        // cancelling first made StopAsync send with a cancelled token, dropping the last sentence.
        await StopAsync();
        lifetime.Cancel();
        socket.Dispose();
        sendLock.Dispose();
        lifetime.Dispose();
    }
}
