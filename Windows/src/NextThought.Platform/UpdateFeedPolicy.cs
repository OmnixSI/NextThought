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

    /// <summary>E27: Mac <c>updateRepository</c> — the GitHub repository the update feed reads.</summary>
    public static string DefaultFor(string repository)
    {
        var trimmed = (repository ?? "").Trim().Trim('/');
        if (trimmed.Length == 0 || trimmed.Contains(' ') || trimmed.Count(ch => ch == '/') != 1)
            return Default;
        return "https://api.github.com/repos/" + trimmed + "/releases";
    }

    public static string ResolveSource(Func<string, string?> read) => ResolveSourceFor(null, read);

    /// <summary>E27: the QA switch still wins; otherwise the saved repository builds the feed URL.</summary>
    public static string ResolveSourceFor(string? repository, Func<string, string?> read)
    {
        var value = read("NEXTTHOUGHT_UPDATE_FEED");
        if (string.IsNullOrWhiteSpace(value)) return DefaultFor(repository ?? "");
        if (read(QaVariable) != "1") return DefaultFor(repository ?? "");
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return DefaultFor(repository ?? "");
        return value;
    }

    public static async Task<string> ReadAsync(string source, CancellationToken cancellationToken = default)
        => await ReadAsync(source, null, cancellationToken).ConfigureAwait(false);

    /// <summary>E27: authenticated read for a private repository (Mac keeps the token in the Keychain).</summary>
    public static async Task<string> ReadAsync(string source, string? token, CancellationToken cancellationToken = default)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NextThought-Windows/0.5");
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim());
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
            }
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        return await File.ReadAllTextAsync(source, cancellationToken).ConfigureAwait(false);
    }
}
