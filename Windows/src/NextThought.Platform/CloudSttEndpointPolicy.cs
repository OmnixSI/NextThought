using NextThought.Core;

namespace NextThought.Platform;

/// <summary>F7: cloud STT settings may only point at the providers Mac encodes.</summary>
public static class CloudSttEndpointPolicy
{
    public static readonly IReadOnlySet<string> AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stt-rt.soniox.com",
        "api.openai.com",
        "api.deepgram.com"
    };

    public static Uri? FromSetting(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            throw new CloudTranscriptException("云端识别地址不是有效 URL。");
        if (!string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
            throw new CloudTranscriptException("云端识别地址必须是 wss://。");
        if (!AllowedHosts.Contains(uri.Host))
            throw new CloudTranscriptException("云端识别地址的主机不在允许范围内。");
        return uri;
    }
}
