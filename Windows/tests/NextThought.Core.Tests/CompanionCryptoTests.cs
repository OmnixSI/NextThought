using System.Text;
using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for CompanionCrypto.swift (HKDF, AES-GCM envelope, commands, pairing URL).</summary>
public sealed class CompanionCryptoTests
{
    private static CompanionPairing Pairing() => new()
    {
        Room = "room-42",
        Key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
    };

    private sealed record Payload(string Status, int Count);

    [Fact]
    public void HkdfMatchesRfc5869TestCaseOne()
    {
        var ikm = Enumerable.Repeat((byte)0x0b, 22).ToArray();
        var salt = Convert.FromHexString("000102030405060708090a0b0c");
        var info = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9");
        var okm = CompanionCrypto.Hkdf(ikm, salt, info, 42);
        Assert.Equal("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865",
            Convert.ToHexString(okm).ToLowerInvariant());
    }

    [Fact]
    public void SealAndOpenRoundTrip()
    {
        var pairing = Pairing();
        var frame = CompanionCrypto.Seal(new Payload("整理中", 3), "state", pairing,
            Convert.FromHexString("000102030405060708090a0b"));
        Assert.Equal(1, frame.V);
        Assert.Equal("state", frame.Kind);
        Assert.Equal(12, Convert.FromBase64String(frame.N).Length);
        Assert.True(Convert.FromBase64String(frame.Body).Length > 16);

        var opened = CompanionCrypto.Open<Payload>(frame, pairing);
        Assert.Equal(new Payload("整理中", 3), opened);
    }

    [Fact]
    public void CrossPlatformVectorMatchesTheMacTest()
    {
        var pairing = new CompanionPairing
        {
            Room = "room-one",
            Key = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray())
        };
        var value = "secret 可靠 -3.7 dBm";
        var frame = CompanionCrypto.Seal(value, "state", pairing, Enumerable.Repeat((byte)4, 12).ToArray());
        Assert.Equal("BAQEBAQEBAQEBAQE", frame.N);
        Assert.Equal("4p2rT4wvtodvAZxobxrsTNxx8zyhDXC7TyxE1esTaHBCIhkoLHmlIw==", frame.Body);
        Assert.Equal(value, CompanionCrypto.Open<string>(frame, pairing));
        var wrong = pairing with { Room = "wrong" };
        Assert.ThrowsAny<Exception>(() => CompanionCrypto.Open<string>(frame, wrong));
    }

    [Fact]
    public void OpeningWithAnotherKindFails()
    {
        var pairing = Pairing();
        var frame = CompanionCrypto.Seal(new Payload("a", 1), "state", pairing);
        var relabelled = frame with { Kind = "ack" };
        Assert.ThrowsAny<Exception>(() => CompanionCrypto.Open<Payload>(relabelled, pairing));
    }

    [Fact]
    public void TamperedBodyFails()
    {
        var pairing = Pairing();
        var frame = CompanionCrypto.Seal(new Payload("a", 1), "state", pairing);
        var body = Convert.FromBase64String(frame.Body);
        body[0] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() => CompanionCrypto.Open<Payload>(frame with { Body = Convert.ToBase64String(body) }, pairing));
    }

    [Fact]
    public void DifferentRoomsDeriveDifferentKeys()
    {
        var a = Pairing();
        var b = a with { Room = "room-43" };
        Assert.NotEqual(CompanionCrypto.Derived(a, "state"), CompanionCrypto.Derived(b, "state"));
        Assert.NotEqual(CompanionCrypto.Derived(a, "state"), CompanionCrypto.Derived(a, "command"));
    }

    [Fact]
    public void PairingUrlCarriesTheJsonFragment()
    {
        var pairing = Pairing();
        var url = pairing.Url(new Uri("https://relay.example.com/"));
        Assert.Equal("https", url.Scheme);
        var fragment = url.Fragment.TrimStart('#');
        Assert.StartsWith("p=", fragment);
        var padded = fragment[2..].Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        var json = JsonSerializer.Deserialize<CompanionPairing>(Encoding.UTF8.GetString(Convert.FromBase64String(padded)), CompanionCrypto.Json);
        Assert.Equal(pairing.Room, json!.Room);
        Assert.Equal(pairing.Key, json.Key);
    }

    [Fact]
    public void CommandFreshnessIsThirtySeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var fresh = new CompanionCommand { Action = "next", CreatedAt = now.ToUnixTimeMilliseconds() };
        Assert.True(fresh.IsFresh(now));
        Assert.False((fresh with { CreatedAt = now.AddSeconds(-31).ToUnixTimeMilliseconds() }).IsFresh(now));
        Assert.False((fresh with { CreatedAt = now.AddSeconds(31).ToUnixTimeMilliseconds() }).IsFresh(now));
    }

    [Fact]
    public void CommandCacheAcceptsEachIdOnceAndRejectsNonGuids()
    {
        var cache = new CompanionCommandCache();
        var id = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        Assert.True(cache.Accept(id, now));
        Assert.False(cache.Accept(id, now.AddSeconds(1)));
        Assert.False(cache.Accept("not-a-guid", now));
    }

    [Fact]
    public void CommandCacheForgetsIdsAfterFiveMinutes()
    {
        var cache = new CompanionCommandCache();
        var id = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        Assert.True(cache.Accept(id, now));
        Assert.True(cache.Accept(id, now.AddMinutes(5)));
    }
}
