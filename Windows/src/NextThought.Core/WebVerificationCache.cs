namespace NextThought.Core;

/// <summary>
/// E14: one stored web-verification result. The cache exists so the automatic background lookup
/// does not repeat a Tavily query for the same question, while manual "对已显示回答查证" stays fresh.
/// </summary>
public sealed record WebVerificationEntry(
    string Question,
    string Provider,
    string Query,
    IReadOnlyList<WebResult> Results,
    DateTimeOffset FetchedAt)
{
    public string AgeLabel(DateTimeOffset now)
    {
        var age = now - FetchedAt;
        if (age < TimeSpan.FromMinutes(1)) return "刚刚";
        if (age < TimeSpan.FromHours(1)) return (int)age.TotalMinutes + " 分钟前";
        return (int)age.TotalHours + " 小时前";
    }
}

/// <summary>
/// Web verification cache (E14). Semantics:
/// · keyed by normalised question + provider, so a different provider or a rewritten question misses;
/// · entries expire after <see cref="Lifetime"/> because "latest prices/versions/news" go stale;
/// · saving a new Tavily key invalidates everything (the old key may belong to another account);
/// · empty result sets are never cached, so a transient failure is retried next time.
/// </summary>
public sealed class WebVerificationCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    public const int Capacity = 40;

    private readonly Dictionary<string, WebVerificationEntry> entries = new(StringComparer.Ordinal);

    public int Count => entries.Count;

    public static string KeyFor(string question, string provider) =>
        provider.Trim().ToLowerInvariant() + "\u0000" + Normalise(question);

    public static string Normalise(string question)
    {
        var text = string.Join(' ', question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        return text.Length > 400 ? text[..400] : text;
    }

    /// <summary>A fresh hit only; expired entries are dropped on the way out.</summary>
    public bool TryGet(string question, string provider, DateTimeOffset now, out WebVerificationEntry entry)
    {
        var key = KeyFor(question, provider);
        if (entries.TryGetValue(key, out var stored))
        {
            if (now - stored.FetchedAt <= Lifetime)
            {
                entry = stored;
                return true;
            }
            entries.Remove(key);
        }
        entry = null!;
        return false;
    }

    public void Store(WebVerificationEntry entry)
    {
        if (entry.Results.Count == 0) return;
        entries[KeyFor(entry.Question, entry.Provider)] = entry;
        if (entries.Count <= Capacity) return;
        foreach (var key in entries.Values.OrderBy(value => value.FetchedAt).Take(entries.Count - Capacity)
                     .Select(value => KeyFor(value.Question, value.Provider)).ToList())
            entries.Remove(key);
    }

    /// <summary>Saving a new key clears the cache: earlier answers may belong to another account.</summary>
    public void Invalidate() => entries.Clear();

    public DateTimeOffset? FetchedAt(string question, string provider) =>
        entries.TryGetValue(KeyFor(question, provider), out var entry) ? entry.FetchedAt : null;
}
