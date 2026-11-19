using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Loopback test for the live cloud session (audio frames in, final update out).</summary>
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
}
