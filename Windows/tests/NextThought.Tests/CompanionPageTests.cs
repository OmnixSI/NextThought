using CompanionSnapshot = NextThought.Platform.CompanionSnapshot;
using CompanionEnvelope = NextThought.Platform.CompanionEnvelope;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Renders the real phone page in Chromium and reads its text over CDP.</summary>
public sealed class CompanionPageTests
{
    [Fact]
    public async Task PhonePageRendersTheSnapshotInChromium()
    {
        var browserPath = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
        }.FirstOrDefault(File.Exists);
        if (browserPath is null) return;

        var port = FreePort();
        var profile = Path.Combine(Path.GetTempPath(), "nextthought-page-" + Guid.NewGuid().ToString("N"));
        var performed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new CompanionServer(IPAddress.Loopback, action =>
        {
            performed.TrySetResult(action);
            return Task.FromResult(true);
        });
        server.Publish(new CompanionSnapshot(Status: "页面自检", Question: "手机问题", Answer: "手机回答"));
        using var browser = Process.Start(new ProcessStartInfo(browserPath,
            $"--headless=new --disable-gpu --no-first-run --remote-debugging-port={port} --remote-allow-origins=* "
            + $"--user-data-dir=\"{profile}\" \"{server.PairingUrl}\"")
        {
            UseShellExecute = false, CreateNoWindow = true
        })!;
        try
        {
            var socketUrl = await WaitForTarget(port);
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(socketUrl), CancellationToken.None);
            await Task.Delay(2500);
            var text = await Evaluate(socket, "document.body.innerText");
            var diagnostics = await Evaluate(socket, "JSON.stringify({href:location.href,ready:document.readyState,html:document.documentElement.outerHTML.slice(0,1200),text:document.body.innerText.slice(0,500)})");
            Assert.True(text.Contains("手机问题") && text.Contains("手机回答"), diagnostics);
            server.Publish(new CompanionSnapshot(Status: "页面自检", Question: "更新问题", Answer: "更新回答"));
            var updated = await WaitForText(socket, "更新问题", TimeSpan.FromSeconds(8));
            Assert.Contains("更新回答", updated);
            await Evaluate(socket, "document.querySelector('[data-action=\"previous\"]').click()");
            Assert.Equal("previous", await performed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            try { browser.Kill(entireProcessTree: true); } catch { }
            try { Directory.Delete(profile, true); } catch { }
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> WaitForTarget(int port)
    {
        using var client = new HttpClient();
        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                var json = await client.GetStringAsync($"http://127.0.0.1:{port}/json/list");
                using var document = JsonDocument.Parse(json);
                foreach (var target in document.RootElement.EnumerateArray())
                {
                    if (!target.TryGetProperty("webSocketDebuggerUrl", out var url) || url.GetString() is not { Length: > 0 } value)
                        continue;
                    if (target.TryGetProperty("url", out var pageUrl)
                        && pageUrl.GetString()?.StartsWith("http://127.0.0.1", StringComparison.Ordinal) == true)
                        return value;
                }
            }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Chromium DevTools endpoint did not appear.");
    }

    private static async Task<string> WaitForText(ClientWebSocket socket, string expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var text = await Evaluate(socket, "document.body.innerText");
            if (text.Contains(expected, StringComparison.Ordinal)) return text;
            await Task.Delay(200);
        }
        throw new TimeoutException("Phone page did not show: " + expected);
    }

    private static async Task<string> Evaluate(ClientWebSocket socket, string expression)
    {
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new { expression, returnByValue = true }
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            using var document = JsonDocument.Parse(message.ToArray());
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var id) && id.GetInt32() == 1)
            {
                if (root.TryGetProperty("result", out var response)
                    && response.TryGetProperty("result", out var value)
                    && value.TryGetProperty("value", out var text))
                    return text.GetString() ?? "";
                return "";
            }
        }
    }
}
