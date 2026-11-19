using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class CompanionRelaySecurityTests
{
    private static CompanionRelayClient Client() =>
        new(() => new CompanionSnapshot(Status: "idle"), _ => Task.FromResult(true));

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
