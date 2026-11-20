using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from CompanionCryptoTests.swift: the cross-platform HKDF/AES-GCM vector and its
/// authentication, and the pairing secret living only in the URL fragment, bounded in size.
/// </summary>
public sealed class CompanionCryptoParityTests
{
    [Fact]
    public void CrossPlatformVectorAndAuthentication()
    {
        var pairing = new CompanionPairing
        {
            Room = "room-one",
            Key = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray())
        };
        const string value = "secret 可靠 -3.7 dBm";
        var frame = CompanionCrypto.Seal(value, "state", pairing, Enumerable.Repeat((byte)4, 12).ToArray());

        Assert.Equal("BAQEBAQEBAQEBAQE", frame.N);
        Assert.Equal("4p2rT4wvtodvAZxobxrsTNxx8zyhDXC7TyxE1esTaHBCIhkoLHmlIw==", frame.Body);
        Assert.Equal(value, CompanionCrypto.Open<string>(frame, pairing));

        var wrongKind = frame with { Kind = "command" };
        Assert.ThrowsAny<Exception>(() => CompanionCrypto.Open<string>(wrongKind, pairing));

        var changed = pairing with { Room = "wrong" };
        Assert.ThrowsAny<Exception>(() => CompanionCrypto.Open<string>(frame, changed));
    }

    [Fact]
    public void PairingSecretOnlyInFragmentAndPayloadBound()
    {
        var pairing = new CompanionPairing
        {
            Lan = "http://192.168.1.2:8765",
            Token = "token",
            Key = Convert.ToBase64String(Enumerable.Repeat((byte)5, 32).ToArray())
        };
        var url = pairing.Url(new Uri("https://relay.example/?old=secret"));

        Assert.Empty(url.Query);
        Assert.StartsWith("p=", url.Fragment.TrimStart('#'));
        Assert.Throws<InvalidOperationException>(() =>
            CompanionCrypto.Seal(new string('x', 300_000), "state", pairing));
    }
}
