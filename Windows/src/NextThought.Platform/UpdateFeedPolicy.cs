using System.Net.Http;

namespace NextThought.Platform;

/// <summary>
/// Update-source policy shared with tests. Normal runs use the HTTPS release feed; overrides
/// require NEXTTHOUGHT_QA=1 and plain HTTP is always rejected.
/// </summary>
public static class UpdateFeedPolicy
{
    public const string Default = "https://api.github.com/repos/OmnixSI/NextThought/releases";
    public const string QaVariable = "NEXTTHOUGHT_QA";

    public static string ResolveSource(Func<string, string?> read)
    {
        var value = read("NEXTTHOUGHT_UPDATE_FEED");
        if (string.IsNullOrWhiteSpace(value)) return Default;
        if (read(QaVariable) != "1") return Default;
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return Default;
        return value;
    }

    public static async Task<string> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NextThought-Windows/0.5");
            return await client.GetStringAsync(source, cancellationToken);
        }
        return await File.ReadAllTextAsync(source, cancellationToken);
    }
}
