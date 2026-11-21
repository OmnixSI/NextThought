using CompanionSnapshot = NextThought.Platform.CompanionSnapshot;
using CompanionEnvelope = NextThought.Platform.CompanionEnvelope;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class CompanionRelaySecurityTests
{
    private static CompanionRelayClient Client() =>
        new(() => new CompanionSnapshot(Status: "idle"), _ => Task.FromResult(true));

    [Fact]
    public void SnapshotUsesTheMacJSONKeys()
    {
        var snapshot = new CompanionSnapshot(Status: "idle", Question: "q", Answer: "a",
            SessionID: "session", AnswerID: "answer", Phase: "done", Slide: "3", UpdateAvailable: true);
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"sessionID\"", json);
        Assert.Contains("\"answerID\"", json);
        Assert.Contains("\"updateAvailable\"", json);
        var envelope = new CompanionEnvelope(1, "server", 3, 0, snapshot);
        Assert.Contains("\"serverID\"", System.Text.Json.JsonSerializer.Serialize(envelope, NextThought.Core.JsonDefaults.Options));
    }

    [Fact]
    public async Task PlainHttpRelayIsRejected()
    {
        await using var client = Client();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartAsync(new Uri("http://relay.example.com/"), new CompanionPairing(), allowLoopback: false));
    }

    [Fact]
    public async Task LoopbackIsOnlyAllowedWhenTheCheckOptsIn()
    {
        await using var client = Client();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartAsync(new Uri("http://127.0.0.1:9/"), new CompanionPairing(), allowLoopback: false));
    }

    [Fact]
    public async Task RelayUrlCannotCarryCredentials()
    {
        await using var client = Client();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartAsync(new Uri("https://user:secret@relay.example.com/"), new CompanionPairing(), allowLoopback: false));
    }
}
