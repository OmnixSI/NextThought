using System.Net.WebSockets;
using System.Text;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// Live cloud speech-to-text session: 16 kHz mono frames are forwarded to the provider
/// WebSocket, and assembled updates are raised for the caller to merge into the transcript.
/// </summary>
public sealed class CloudSttSession : IAsyncDisposable
{
    private readonly CloudSTTProvider provider;
    private readonly ClientWebSocket socket = new();
    private readonly CloudTranscriptAssembler assembler;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private Task? receive;
    private int openAiSamples;

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
        if (samples.Length == 0 || socket.State != WebSocketState.Open) return;
        var pcm = provider == CloudSTTProvider.OpenAI ? PCMResampler.Upsample16kTo24k(samples) : samples;
        var bytes = PCMResampler.LittleEndianData(pcm);
        await sendLock.WaitAsync(ct);
        try
        {
            if (socket.State != WebSocketState.Open) return;
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
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { }
        finally { sendLock.Release(); }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!lifetime.IsCancellationRequested && socket.State != WebSocketState.Closed)
            {
                var result = await socket.ReceiveAsync(buffer, lifetime.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                foreach (var update in assembler.Consume(text)) Updated?.Invoke(update);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!lifetime.IsCancellationRequested) Failed?.Invoke(ex is CloudTranscriptException ? ex.Message : "云端识别断开：" + ex.Message);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                var close = provider.CloseMessage();
                if (close.Length > 0)
                    await socket.SendAsync(Encoding.UTF8.GetBytes(close), WebSocketMessageType.Text, true, lifetime.Token);
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", lifetime.Token);
            }
            if (receive is not null) await receive.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await StopAsync();
        socket.Dispose();
        sendLock.Dispose();
        lifetime.Dispose();
    }
}
