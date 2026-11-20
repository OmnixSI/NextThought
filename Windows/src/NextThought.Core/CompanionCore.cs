using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>B22: the small state mirrored to the paired phone (Companion.swift).</summary>
public sealed record CompanionSnapshot(
    string Status = "待机", string Question = "", string Answer = "", bool Answering = false,
    string Candidate = "", string Position = "", string? SessionId = null, string? AnswerId = null,
    string? Phase = null, string? Sources = null, string? Slide = null, bool? UpdateAvailable = false);

/// <summary>B22: a full replacement frame with a server-scoped sequence number.</summary>
public sealed record CompanionEnvelope(string ServerId, ulong Sequence, CompanionSnapshot Snapshot, double UpdatedAt)
{
    public static CompanionEnvelope Create(string serverId, ulong sequence, CompanionSnapshot snapshot, DateTimeOffset now) =>
        new(serverId, sequence, snapshot, now.ToUnixTimeMilliseconds());

    public byte[] Sse(string? @event = null)
    {
        var json = JsonSerializer.Serialize(this, JsonDefaults.Options);
        var body = "id: " + ServerId + ":" + Sequence.ToString(CultureInfo.InvariantCulture) + "\n"
                   + (@event is { Length: > 0 } ? "event: " + @event + "\n" : "")
                   + "data: " + json + "\n\n";
        return Encoding.UTF8.GetBytes(body);
    }
}

/// <summary>B22: HTTP request line parsing, token comparison and SSE frames.</summary>
public static class CompanionHTTP
{
    public sealed record Request(string Method, string Path, IReadOnlyDictionary<string, string> Query);

    public static IReadOnlySet<string> AllowedActions { get; } = new HashSet<string>
    {
        "answer", "cancel", "regenerate", "previous", "next", "shorter", "detailed", "example",
        "english", "chinese", "star", "clarify", "followUps", "applyWeb"
    };

    public static Request? Parse(string head)
    {
        var line = head.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').FirstOrDefault() ?? "";
        if (line.Length == 0) return null;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal)
            || !parts[1].StartsWith('/') || parts[1].Length >= 2_048) return null;
        var target = parts[1].Split('?', 2);
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (target.Length == 2)
        {
            foreach (var pair in target[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                query[key] = kv.Length == 2 ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : "";
            }
        }
        return new Request(parts[0], target[0], query);
    }

    public static bool TokenMatches(string? provided, string expected)
    {
        if (provided is null || expected.Length == 0) return false;
        var left = Encoding.UTF8.GetBytes(provided);
        var right = Encoding.UTF8.GetBytes(expected);
        if (left.Length != right.Length) return false;
        var difference = 0;
        for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
        return difference == 0;
    }

    public static byte[] SseEvent(CompanionSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonDefaults.Options);
        return Encoding.UTF8.GetBytes("data: " + json + "\n\n");
    }

    public static byte[] Response(string status, string contentType, byte[] body, IReadOnlyList<string>? extraHeaders = null)
    {
        var head = "HTTP/1.1 " + status + "\r\nContent-Type: " + contentType + "\r\nContent-Length: " + body.Length
                   + "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n";
        foreach (var header in extraHeaders ?? []) head += header + "\r\n";
        return [.. Encoding.UTF8.GetBytes(head + "\r\n"), .. body];
    }
}
