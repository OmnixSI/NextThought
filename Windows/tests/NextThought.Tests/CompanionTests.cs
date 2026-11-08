using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class CompanionTests
{
    [Theory]
    [InlineData("100.64.0.0", true)]
    [InlineData("100.72.252.201", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData("100.63.255.255", false)]
    [InlineData("100.128.0.0", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("192.168.1.10", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("10.1.2.3", true)]
    public void AddressSelectionIncludesSharedCampusSpaceAndExcludesProxyTestAddress(string address, bool accepted)
        => Assert.Equal(accepted, CompanionServer.IsLanAddress(IPAddress.Parse(address)));
    [Fact]
    public void PhoneAddressListNeverOffersLoopback()
        => Assert.DoesNotContain(CompanionServer.Addresses(), a => IPAddress.IsLoopback(a.Address));
    private static string Token(CompanionServer server)
    {
        var encoded = server.PairingUrl.Split("#p=")[1].Replace('-', '+').Replace('_', '/'); encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(encoded)); return doc.RootElement.GetProperty("token").GetString()!;
    }
    [Fact]
    public async Task AuthCommandDedupSseReconnectRotationAndShutdownUseRealTcp()
    {
        int commands = 0; var server = new CompanionServer(IPAddress.Loopback, action => { Interlocked.Increment(ref commands); return Task.FromResult(true); });
        try
        {
            using var http = new HttpClient { BaseAddress = server.BaseUri, Timeout = TimeSpan.FromSeconds(5) }; var token = Token(server); var path = "/snapshot?t=" + token;
            Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/snapshot?t=wrong")).StatusCode);
            server.Publish(new("fixture", "公开问题", "公开回答", false)); var first = await http.GetFromJsonAsync<CompanionEnvelope>(path, NextThought.Core.JsonDefaults.Options);
            Assert.Equal("公开回答", first!.Snapshot.Answer); var id = Guid.NewGuid(); var command = $"/action?t={token}&a=answer&id={id}";
            Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync(command, null)).StatusCode); Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync(command, null)).StatusCode); Assert.Equal(1, commands);
            using var foreign = new HttpRequestMessage(HttpMethod.Post, $"/action?t={token}&a=answer&id={Guid.NewGuid()}"); foreign.Headers.Add("Origin", "https://example.invalid");
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(foreign)).StatusCode); Assert.Equal(1, commands);
            using (var response = await http.GetAsync("/events?t=" + token, HttpCompletionOption.ResponseHeadersRead))
            using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
            { var line = await reader.ReadLineAsync(); Assert.StartsWith("data: ", line); Assert.Contains("serverID", line); }
            server.Publish(new("fixture-2", "新问题", "新回答", false)); var reconnected = await http.GetFromJsonAsync<CompanionEnvelope>(path, NextThought.Core.JsonDefaults.Options);
            Assert.True(reconnected!.Sequence > first.Sequence); Assert.Equal("新问题", reconnected.Snapshot.Question);
            using var badHost = new HttpRequestMessage(HttpMethod.Get, path); badHost.Headers.Host = "attacker.invalid"; Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(badHost)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync($"/action?t={token}&a=start&id={Guid.NewGuid()}", null)).StatusCode);
            Assert.Contains("NextThought", await http.GetStringAsync("/")); Assert.Contains("Windows", await http.GetStringAsync("/phone.js"));
            await using var next = new CompanionServer(IPAddress.Loopback, _ => Task.FromResult(true)); using var nextHttp = new HttpClient { BaseAddress = next.BaseUri };
            Assert.NotEqual(token, Token(next)); Assert.Equal(HttpStatusCode.Forbidden, (await nextHttp.GetAsync(path)).StatusCode);
        }
        finally { await server.DisposeAsync(); }
        Assert.Equal(0, server.ConnectionCount); using var socket = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(async () => await socket.ConnectAsync(server.BaseUri.Host, server.BaseUri.Port));
    }
}
