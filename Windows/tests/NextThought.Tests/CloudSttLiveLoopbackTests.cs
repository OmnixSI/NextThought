using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Loopback test for the live cloud session (audio frames in, final update out).</summary>
[Collection("CloudSTT")]
public sealed class CloudSttLiveLoopbackTests
{
    [Fact]
    public async Task LiveFramesProduceFinalUpdate()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var buffer = new byte[16 * 1024];
            var audio = 0;
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    audio += result.Count;
                    if (audio > 0)
                    {
                        var final = """{"tokens":[{"text":"live","is_final":true,"start_ms":0,"end_ms":200},{"text":"<end>","is_final":true}],"finished":true}""";
                        await socket.SendAsync(Encoding.UTF8.GetBytes(final), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;
                    }
                }
            }
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
        });

        await using var session = await CloudSttSession.StartAsync(
            CloudSTTProvider.Soniox, "test-key", "", "auto", [],
            endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
        var finals = new List<string>();
        session.Updated += update => { if (update.IsFinal) finals.Add(update.Text); };
        await session.SendAsync(new short[1600]);
        for (var attempt = 0; attempt < 20 && finals.Count == 0; attempt++) await Task.Delay(50);
        await session.StopAsync();
        await server;
        Assert.Contains("live", finals);
    }

    [Fact]
    public async Task OpenAiLiveSessionCommitsAudioWhileRecording()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var commits = 0;
        var committed = new TaskCompletionSource();
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
                    commits++;
                    if (!committed.Task.IsCompleted) committed.SetResult();
                }
            }
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
        });

        await using var session = await CloudSttSession.StartAsync(
            CloudSTTProvider.OpenAI, "test-key", "", "auto", [],
            endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
        for (var index = 0; index < 21; index++) await session.SendAsync(new short[1600]);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await session.StopAsync();
        await server;
        Assert.True(commits >= 1);
    }

    [Fact]
    public async Task FortyKilobyteMessageSplitIntoThreeFragmentsIsAssembled()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var payload = new string('a', 41_000);
        var json = "{\"tokens\":[{\"text\":\"" + payload + "\",\"is_final\":true,\"start_ms\":0,\"end_ms\":500}," +
                   "{\"text\":\"<end>\",\"is_final\":true}],\"finished\":true}";
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.True(bytes.Length >= 40 * 1024, "the test message must be at least 40 KiB");
        var fragments = 0;
        var serverError = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            try
            {
                var context = await listener.GetContextAsync();
                using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                var buffer = new byte[16 * 1024];
                // Wait for the client configuration message so the receive loop is running.
                await socket.ReceiveAsync(buffer, CancellationToken.None);
                // Wait for the first audio frame as well: StartAsync returns right after it starts the
                // receive loop, so sending the fragments before the test attached Updated would lose
                // the update (the CI failure this test used to hit).
                await socket.ReceiveAsync(buffer, CancellationToken.None);
                var cuts = new[] { 0, 15_000, 30_000, bytes.Length };
                for (var index = 0; index < 3; index++)
                {
                    await socket.SendAsync(bytes.AsMemory(cuts[index], cuts[index + 1] - cuts[index]),
                        WebSocketMessageType.Text, index == 2, CancellationToken.None);
                    fragments++;
                }
                // Complete the closing handshake the way a provider does: wait for the client's
                // close frame, then answer it. Disposing right after CloseOutputAsync aborts the
                // connection and the client cannot read the message it was just sent.
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
                serverError.TrySetResult(null);
            }
            catch (Exception ex) { serverError.TrySetResult(ex); }
        });

        await using var session = await CloudSttSession.StartAsync(
            CloudSTTProvider.Soniox, "test-key", "", "auto", [],
            endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
        var received = new TaskCompletionSource<CloudTranscriptUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update => { if (update.IsFinal) received.TrySetResult(update); };
        session.Failed += message => received.TrySetException(new InvalidOperationException("session failed: " + message));
        // Let the server know the handlers are attached before it sends the large message.
        await session.SendAsync(new short[1600]);
        var final = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await session.StopAsync();
        var serverException = await serverError.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await server;
        Assert.Null(serverException);
        Assert.Equal(3, fragments);
        Assert.Equal(payload, final.Text);
        Assert.True(final.IsFinal);
    }

    [Fact]
    public async Task MessageOverTheLimitStopsWithExplicitError()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var payload = new string('b', 525_000);
        var bytes = Encoding.UTF8.GetBytes("{\"tokens\":[{\"text\":\"" + payload + "\",\"is_final\":true}]}");
        Assert.True(bytes.Length > 512 * 1024, "the test message must exceed 512 KiB");
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var buffer = new byte[16 * 1024];
            await socket.ReceiveAsync(buffer, CancellationToken.None);
            // Feed the oversized message in 64 KiB fragments: the cap applies to the reassembled message.
            for (var offset = 0; offset < bytes.Length; offset += 64 * 1024)
            {
                var count = Math.Min(64 * 1024, bytes.Length - offset);
                await socket.SendAsync(bytes.AsMemory(offset, count), WebSocketMessageType.Text,
                    offset + count == bytes.Length, CancellationToken.None);
            }
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
        });

        await using var session = await CloudSttSession.StartAsync(
            CloudSTTProvider.Soniox, "test-key", "", "auto", [],
            endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
        var failure = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Failed += message => failure.TrySetResult(message);
        var message = await failure.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("云端识别消息超过 512 KiB，已停止。", message);
        await session.StopAsync();
        await server.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SlowNetworkBoundsInFlightSendsInsteadOfBufferingAudio()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        // The server stops reading audio after the configuration message, like a connection whose
        // upload cannot keep up. The client must stop recognition instead of queueing frames forever.
        var drain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var buffer = new byte[16 * 1024];
            await socket.ReceiveAsync(buffer, CancellationToken.None);
            await drain.Task;
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
        });

        await using var session = await CloudSttSession.StartAsync(
            CloudSTTProvider.Soniox, "test-key", "", "auto", [],
            endpointOverride: new Uri($"ws://127.0.0.1:{port}/stt"));
        var failure = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Failed += message => failure.TrySetResult(message);
        var frame = new short[1600]; // 100 ms of 16 kHz audio, one capture tick
        var sends = new List<Task>();
        for (var index = 0; index < 400 && !failure.Task.IsCompleted; index++)
        {
            sends.Add(session.SendAsync(frame));
            await Task.Delay(5);
        }
        var message = await failure.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("网络上传速度跟不上实时音频，已停止云端识别。", message);
        drain.TrySetResult();
        await Task.WhenAny(Task.WhenAll(sends), Task.Delay(TimeSpan.FromSeconds(5)));
        await session.StopAsync();
        await server.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
