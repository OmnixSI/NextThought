using System.Net.Http.Json;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>Result of one web lookup; errors are data so the answer can still be produced.</summary>
public sealed record WebSearchResponse(IReadOnlyList<WebResult> Results, string Provider, string? Error = null);

public interface IWebSearchProvider
{
    Task<WebSearchResponse> SearchAsync(string query, int count, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tavily search provider (the provider the answer prompt expects). The key comes from
/// TAVILY_API_KEY; without it the app reports that web search is not configured instead of failing.
/// </summary>
public sealed class TavilySearchProvider : IWebSearchProvider
{
    private readonly HttpClient client;
    private readonly string apiKey;

    public TavilySearchProvider(HttpClient client, string apiKey)
    {
        this.client = client;
        this.apiKey = apiKey;
    }

    public string Provider => "tavily";

    public async Task<WebSearchResponse> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("https://api.tavily.com/search", new
            {
                api_key = apiKey,
                query,
                max_results = Math.Clamp(count, 1, 10),
                search_depth = "basic",
                include_answer = false
            }, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new WebSearchResponse([], Provider, $"联网搜索失败（HTTP {(int)response.StatusCode}）。");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var results = new List<WebResult>();
            if (document.RootElement.TryGetProperty("results", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                    var url = item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
                    var snippet = item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
                    if (url.Length == 0 || snippet.Length == 0) continue;
                    var published = item.TryGetProperty("published_date", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString()
                        : null;
                    results.Add(new WebResult(title, url, snippet, published, DateTimeOffset.UtcNow.ToString("O")));
                }
            }
            return new WebSearchResponse(results, Provider);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new WebSearchResponse([], Provider, "联网搜索失败：" + exception.Message);
        }
    }
}

public static class WebSearchConfiguration
{
    public const string KeyVariable = "TAVILY_API_KEY";
    public const string EnableVariable = "NEXTTHOUGHT_WEB_SEARCH";

    /// <summary>Reads the environment; explicitly disabling wins over a present key.</summary>
    public static (string? ApiKey, bool Enabled) FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var disabled = string.Equals(read(EnableVariable), "0", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(read(EnableVariable), "off", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(read(EnableVariable), "false", StringComparison.OrdinalIgnoreCase);
        var key = read(KeyVariable)?.Trim();
        return (string.IsNullOrEmpty(key) ? null : key, !disabled && !string.IsNullOrEmpty(key));
    }

    public static IWebSearchProvider? CreateProvider(HttpClient? client = null, Func<string, string?>? read = null)
    {
        var (key, enabled) = FromEnvironment(read);
        if (!enabled || key is null) return null;
        client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NextThought-Windows/1.3");
        return new TavilySearchProvider(client, key);
    }
}
