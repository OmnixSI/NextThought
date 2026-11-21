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
        // F8: a 10-minute recording must not be cut at 30 seconds.
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
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
                using var message = new MemoryStream();
                message.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await socket.ReceiveAsync(buffer, timeout.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    message.Write(buffer, 0, result.Count);
                }
                var text = Encoding.UTF8.GetString(message.ToArray());
                foreach (var update in assembler.Consume(text))
                {
                    if (update.IsFinal) finals.Add(update);
                    else { lastLive = update.Text; live?.Invoke(update.Text); }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // F8: report a timeout instead of silently truncating the recording.
            if (finals.Count == 0) throw new CloudTranscriptException("云端转写超时，请重试或改用本机识别。");
        }
        catch (WebSocketException)
        {
            // A provider may drop the socket right after the final message; only a failure with
            // no transcript at all is surfaced to the user.
            if (finals.Count == 0) throw new CloudTranscriptException("云端转写连接中断，请重试或改用本机识别。");
        }

        if (finals.Count == 0 && lastLive.Trim().Length > 0)
            finals.Add(new CloudTranscriptUpdate(lastLive.Trim(), true, 0, null));
        return finals;
    }

    /// <summary>Whole WAV → mono PCM16 at the provider rate.</summary>
    public static byte[] ReadPcm16(string wavPath, int sampleRate)
    {
        using var reader = new WaveFileReader(wavPath);
        ISampleProvider samples = reader.ToSampleProvider();
        // F8: more than two channels are downmixed instead of being rejected/truncated.
        if (samples.WaveFormat.Channels > 1) samples = new DownmixSampleProvider(samples, samples.WaveFormat.Channels);
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

    /// <summary>Average every interleaved channel into one mono frame.</summary>
    private sealed class DownmixSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly float[] buffer;
        public DownmixSampleProvider(ISampleProvider source, int channels)
        {
            this.source = source;
            this.channels = channels;
            buffer = new float[4096 * channels];
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }
        public WaveFormat WaveFormat { get; }
        public int Read(float[] destination, int offset, int count)
        {
            var frames = Math.Min(count, buffer.Length / channels);
            var read = source.Read(buffer, 0, frames * channels);
            var completed = read / channels;
            for (var frame = 0; frame < completed; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++) sum += buffer[frame * channels + channel];
                destination[offset + frame] = sum / channels;
            }
            return completed;
        }
    }
}
