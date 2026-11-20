using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Loopback protocol test for the cloud STT WAV client (no real provider key).</summary>
[Collection("CloudSTT")]
public sealed class CloudSttLoopbackTests
{
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task SonioxWavRoundTripSendsConfigAudioAndAssemblesFinal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cloud-stt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var wav = Path.Combine(directory, "tone.wav");
        try
        {
            using (var writer = new WaveFileWriter(wav, new WaveFormat(16000, 16, 1)))
            {
                var samples = Enumerable.Range(0, 3200).Select(index => (short)(Math.Sin(index / 20.0) * 8000)).ToArray();
                writer.WriteSamples(samples, 0, samples.Length);
            }
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var configSeen = false;
            var audioBytes = 0;
            var server = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (text.Contains("model", StringComparison.Ordinal)) configSeen = true;
                    }
                    else audioBytes += result.Count;
                }
                var final = """{"tokens":[{"text":"hello","is_final":true,"start_ms":0,"end_ms":500},{"text":"<end>","is_final":true}],"finished":true}""";
                await socket.SendAsync(Encoding.UTF8.GetBytes(final), WebSocketMessageType.Text, true, CancellationToken.None);
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
            });
            var updates = await CloudSttClient.TranscribeWavAsync(
                wav, CloudSTTProvider.Soniox, "test-key", "", "auto", [],
                endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
            await server;
            Assert.True(configSeen);
            Assert.True(audioBytes > 0);
            Assert.Single(updates);
            Assert.Equal("hello", updates[0].Text);
            Assert.True(updates[0].IsFinal);
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static string WriteWav(string directory)
    {
        var wav = Path.Combine(directory, "tone.wav");
        using var writer = new WaveFileWriter(wav, new WaveFormat(16000, 16, 1));
        var samples = Enumerable.Range(0, 3200).Select(index => (short)(Math.Sin(index / 20.0) * 8000)).ToArray();
        writer.WriteSamples(samples, 0, samples.Length);
        return wav;
    }

    [Fact]
    public async Task OpenAiSendsJsonAudioAndReadsCompletedTranscript()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cloud-stt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var wav = WriteWav(directory);
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var server = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (text.Contains("input_audio_buffer.commit", StringComparison.Ordinal))
                    {
                        var final = """{"type":"conversation.item.input_audio_transcription.completed","item_id":"item1","transcript":"openai hello"}""";
                        await socket.SendAsync(Encoding.UTF8.GetBytes(final), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;
                    }
                }
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
            });
            var updates = await CloudSttClient.TranscribeWavAsync(
                wav, CloudSTTProvider.OpenAI, "test-key", "", "auto", [],
                endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
            await server;
            Assert.Single(updates);
            Assert.Equal("openai hello", updates[0].Text);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    [Fact]
    public async Task DeepgramSendsBinaryAuthAndReadsSpeechFinal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cloud-stt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var wav = WriteWav(directory);
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var auth = "";
            var server = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                auth = context.Request.Headers["Authorization"] ?? "";
                using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType == WebSocketMessageType.Text && Encoding.UTF8.GetString(buffer, 0, result.Count).Contains("CloseStream", StringComparison.Ordinal))
                    {
                        var final = """{"type":"Results","channel":{"alternatives":[{"transcript":"deepgram hello"}]},"is_final":true,"speech_final":true,"start":0,"duration":0.5}""";
                        await socket.SendAsync(Encoding.UTF8.GetBytes(final), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;
                    }
                }
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
            });
            var updates = await CloudSttClient.TranscribeWavAsync(
                wav, CloudSTTProvider.Deepgram, "test-key", "", "en", [],
                endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
            await server;
            Assert.Equal("Token test-key", auth);
            Assert.Single(updates);
            Assert.Equal("deepgram hello", updates[0].Text);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }
}
