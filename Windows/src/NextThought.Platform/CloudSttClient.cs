using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// One-shot cloud speech-to-text over a provider WebSocket (Soniox/OpenAI/Deepgram).
/// Used by the WAV import path: audio is read locally, resampled to the provider rate,
/// streamed and assembled with the Mac-compatible <see cref="CloudTranscriptAssembler"/>.
/// </summary>
public static class CloudSttClient
{
    public static async Task<IReadOnlyList<CloudTranscriptUpdate>> TranscribeWavAsync(
        string wavPath, CloudSTTProvider provider, string apiKey, string model, string language,
        IReadOnlyList<string> vocabulary, Action<string>? live = null, Uri? endpointOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new CloudTranscriptException("请填写云端语音识别的 API Key。");
        var rate = provider.SampleRate();
        var pcm = ReadPcm16(wavPath, rate);
        if (pcm.Length == 0) throw new CloudTranscriptException("录音里没有可识别的音频。");

        var endpoint = endpointOverride ?? new Uri(provider.Endpoint(model, language, vocabulary)!);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", provider.AuthorizationHeader(apiKey));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await socket.ConnectAsync(endpoint, timeout.Token);
        var assembler = new CloudTranscriptAssembler(provider);
        var finals = new List<CloudTranscriptUpdate>();
        var lastLive = "";
        string? config = provider.ConfigurationMessage(model, language, vocabulary);
        if (config is not null)
            await socket.SendAsync(Encoding.UTF8.GetBytes(config), WebSocketMessageType.Text, true, timeout.Token);

        const int chunk = 6400;
        for (var offset = 0; offset < pcm.Length; offset += chunk)
        {
            var length = Math.Min(chunk, pcm.Length - offset);
            if (provider.SendsBinaryAudio())
            {
                await socket.SendAsync(pcm.AsMemory(offset, length), WebSocketMessageType.Binary, true, timeout.Token);
            }
            else
            {
                var payload = Encoding.UTF8.GetBytes(CloudSTTProviders.OpenAiAppendMessage(pcm[offset..(offset + length)]));
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token);
            }
        }
        var close = provider.CloseMessage();
        if (close.Length > 0)
            await socket.SendAsync(Encoding.UTF8.GetBytes(close), WebSocketMessageType.Text, true, timeout.Token);
        else
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token);

        var buffer = new byte[16 * 1024];
        try
        {
            while (socket.State != WebSocketState.Closed && !timeout.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                foreach (var update in assembler.Consume(text))
                {
                    if (update.IsFinal) finals.Add(update);
                    else { lastLive = update.Text; live?.Invoke(update.Text); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }

        if (finals.Count == 0 && lastLive.Trim().Length > 0)
            finals.Add(new CloudTranscriptUpdate(lastLive.Trim(), true, 0, null));
        return finals;
    }

    /// <summary>Whole WAV → mono PCM16 at the provider rate.</summary>
    public static byte[] ReadPcm16(string wavPath, int sampleRate)
    {
        using var reader = new WaveFileReader(wavPath);
        ISampleProvider samples = reader.ToSampleProvider();
        if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples);
        if (samples.WaveFormat.SampleRate != sampleRate) samples = new WdlResamplingSampleProvider(samples, sampleRate);
        var floats = new float[4096];
        using var output = new MemoryStream();
        var bytes = new byte[2];
        int read;
        while ((read = samples.Read(floats, 0, floats.Length)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                var value = (short)Math.Clamp(floats[index] * short.MaxValue, short.MinValue, short.MaxValue);
                bytes[0] = (byte)(value & 0xFF);
                bytes[1] = (byte)((value >> 8) & 0xFF);
                output.Write(bytes, 0, 2);
            }
        }
        return output.ToArray();
    }
}
