using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Exercises the relay protocol against a loopback relay: join, state, command, ack.</summary>
public sealed class CompanionRelayIntegrationTests
{
    [Fact]
    public async Task LoopbackRelayCarriesStateAndCommand()
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var pairing = new CompanionPairing();
        var performed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.HttpMethod == "POST" && context.Request.Url!.AbsolutePath == "/rooms")
                {
                    var body = Encoding.UTF8.GetBytes("{\"room\":\"room\",\"publisher\":\"pub\",\"subscriber\":\"sub\"}");
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body);
                    context.Response.Close();
                    continue;
                }
                if (context.Request.Url!.AbsolutePath != "/socket" || !context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var accepted = await context.AcceptWebSocketAsync(null);
                var socket = accepted.WebSocket;
                var joined = await Receive(socket);
                Assert.Contains("\"publisher\"", joined);
                await Send(socket, "{\"joined\":true}");
                var state = await Receive(socket);
                Assert.Contains("\"kind\":\"state\"", state);
                stateSeen.TrySetResult(true);
                var command = CompanionCrypto.Seal(new CompanionCommand
                {
                    Id = Guid.NewGuid().ToString("D"), Action = "previous",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }, "command", pairing with { Room = "room", Subscriber = "sub" });
                await Send(socket, JsonSerializer.Serialize(command, CompanionCrypto.Json));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                return;
            }
        });

        await using var client = new CompanionRelayClient(
            () => new CompanionSnapshot(Status: "relay", Question: "q", Answer: "a"),
            action => { performed.TrySetResult(action); return Task.FromResult(true); });
        var result = await client.StartAsync(new Uri($"http://127.0.0.1:{port}/"), pairing, allowLoopback: true);
        Assert.Equal("room", result.Room);
        Assert.Equal("sub", result.Subscriber);
        Assert.Equal("previous", await performed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(await stateSeen.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        await server.WaitAsync(TimeSpan.FromSeconds(10));
        await client.StopAsync();
        listener.Stop();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task Send(WebSocket socket, string json) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<string> Receive(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.ToArray());
    }
}
