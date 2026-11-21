using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

// Port of CompanionCrypto.swift: the relay, the LAN page and the phone share one
// directional HKDF + AES-256-GCM envelope. The relay only ever sees ciphertext.

/// <summary>
/// Pairing material: LAN link/token, relay room/subscriber and the 32-byte content key.
/// Mirrors <c>CompanionPairing</c> (Swift) and is also the JSON in the QR fragment.
/// </summary>
public sealed record CompanionPairing
{
    public int V { get; init; } = 1;

    public string? Lan { get; init; }

    public string? Token { get; init; }

    public string? Relay { get; init; }

    public string Room { get; init; } = Guid.NewGuid().ToString("D");

    public string? Subscriber { get; init; }

    /// <summary>Base64 of 32 random bytes.</summary>
    public string Key { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string? Ble { get; init; }

    /// <summary>The same JSON fragment the Mac app puts in the pairing QR (base64url, no padding).</summary>
    public Uri Url(Uri baseUri)
    {
        var json = JsonSerializer.Serialize(this, CompanionCrypto.Json);
        var fragment = "p=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var builder = new UriBuilder(baseUri) { Query = "", Fragment = fragment };
        return builder.Uri;
    }
}

/// <summary>The encrypted envelope: <c>body</c> is ciphertext followed by the 16-byte tag.</summary>
public sealed record CompanionCipherFrame
{
    public int V { get; init; } = 1;

    public string Kind { get; init; } = "";

    /// <summary>Base64 of the 12-byte nonce.</summary>
    public string N { get; init; } = "";

    /// <summary>Base64 of ciphertext + GCM tag.</summary>
    public string Body { get; init; } = "";
}

/// <summary>One phone command; retries share the id so they are deduplicated.</summary>
public sealed record CompanionCommand
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");

    public string Action { get; init; } = "";

    /// <summary>Unix milliseconds, from the phone.</summary>
    public double CreatedAt { get; init; }

    public bool IsFresh(DateTimeOffset? now = null) =>
        Math.Abs((now ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds() - CreatedAt) < 30_000;
}

public sealed record CompanionAcknowledgement
{
    public string Id { get; init; } = "";
}

/// <summary>Accepts each command id once within five minutes (CompanionCommandCache).</summary>
public sealed class CompanionCommandCache
{
    private readonly Dictionary<string, DateTimeOffset> commands = [];

    public bool Accept(string id, DateTimeOffset? now = null)
    {
        if (!Guid.TryParse(id, out _)) return false;
        var current = now ?? DateTimeOffset.UtcNow;
        foreach (var old in commands.Where(pair => current - pair.Value >= TimeSpan.FromMinutes(5)).Select(pair => pair.Key).ToArray())
            commands.Remove(old);
        if (commands.ContainsKey(id)) return false;
        if (commands.Count >= 1024 && commands.MinBy(pair => pair.Value) is var oldest && oldest.Key is { } first)
            commands.Remove(first);
        commands[id] = current;
        return true;
    }
}

public static class CompanionCrypto
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly HashSet<string> Kinds = ["state", "command", "ack"];

    /// <summary>HKDF-SHA256(salt = room, info = "ih-companion-v1/&lt;kind&gt;", 32 bytes).</summary>
    public static byte[] Derived(CompanionPairing pairing, string kind)
    {
        if (!Kinds.Contains(kind)) throw new ArgumentException("kind", nameof(kind));
        var input = Convert.FromBase64String(pairing.Key);
        if (input.Length != 32) throw new InvalidOperationException("invalidKey");
        return Hkdf(input, Encoding.UTF8.GetBytes(pairing.Room), Encoding.UTF8.GetBytes($"ih-companion-v1/{kind}"), 32);
    }

    public static CompanionCipherFrame Seal<T>(T value, string kind, CompanionPairing pairing, byte[]? nonce = null)
    {
        if (!Kinds.Contains(kind)) throw new InvalidOperationException("invalidFrame");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (plaintext.Length > 256 * 1024) throw new InvalidOperationException("invalidFrame");
        var iv = nonce ?? RandomNumberGenerator.GetBytes(12);
        if (iv.Length != 12) throw new ArgumentException("nonce", nameof(nonce));
        var key = Derived(pairing, kind);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(key, 16))
            gcm.Encrypt(iv, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes($"{pairing.Room}/{kind}"));
        var body = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, body, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, body, ciphertext.Length, tag.Length);
        return new CompanionCipherFrame { Kind = kind, N = Convert.ToBase64String(iv), Body = Convert.ToBase64String(body) };
    }

    public static T Open<T>(CompanionCipherFrame frame, CompanionPairing pairing)
    {
        if (frame.V != 1 || !Kinds.Contains(frame.Kind) || frame.Body.Length > 350_000) throw new InvalidOperationException("invalidFrame");
        var iv = Convert.FromBase64String(frame.N);
        var body = Convert.FromBase64String(frame.Body);
        if (iv.Length != 12 || body.Length < 16) throw new InvalidOperationException("invalidFrame");
        var ciphertext = body.AsSpan(0, body.Length - 16).ToArray();
        var tag = body.AsSpan(body.Length - 16).ToArray();
        var plaintext = new byte[ciphertext.Length];
        using (var gcm = new AesGcm(Derived(pairing, frame.Kind), 16))
            gcm.Decrypt(iv, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes($"{pairing.Room}/{frame.Kind}"));
        return JsonSerializer.Deserialize<T>(plaintext, Json) ?? throw new InvalidOperationException("invalidFrame");
    }

    /// <summary>RFC 5869 HKDF-SHA256 (extract then expand).</summary>
    public static byte[] Hkdf(byte[] inputKey, byte[] salt, byte[] info, int length)
    {
        var prk = HMACSHA256.HashData(salt, inputKey);
        var okm = new byte[length];
        var block = Array.Empty<byte>();
        var offset = 0;
        for (byte counter = 1; offset < length; counter++)
        {
            var input = new byte[block.Length + info.Length + 1];
            Buffer.BlockCopy(block, 0, input, 0, block.Length);
            Buffer.BlockCopy(info, 0, input, block.Length, info.Length);
            input[^1] = counter;
            block = HMACSHA256.HashData(prk, input);
            var take = Math.Min(block.Length, length - offset);
            Buffer.BlockCopy(block, 0, okm, offset, take);
            offset += take;
        }
        return okm;
    }
}
