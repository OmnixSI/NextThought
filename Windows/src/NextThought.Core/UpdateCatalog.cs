using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>One file attached to a release.</summary>
public sealed record GitHubReleaseAsset(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl = null);

/// <summary>One GitHub release, as the API returns it.</summary>
public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("body")] string? Body = null,
    [property: JsonPropertyName("draft")] bool Draft = false,
    [property: JsonPropertyName("prerelease")] bool Prerelease = false,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt = null,
    IReadOnlyList<GitHubReleaseAsset>? Assets = null)
{
    [JsonPropertyName("assets")]
    public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = Assets ?? [];

    public GitHubReleaseAsset? Asset(string name) => Assets.FirstOrDefault(asset => asset.Name == name);

    /// <summary>The Windows payload of a release (the Mac build uses -macOS-arm64.zip).</summary>
    public GitHubReleaseAsset? WindowsAsset => Assets.FirstOrDefault(asset =>
        asset.Name.EndsWith("-windows-x64.zip", StringComparison.OrdinalIgnoreCase) || asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>F14: the update.json manifest published with the release.</summary>
    public GitHubReleaseAsset? ManifestAsset => Assets.FirstOrDefault(asset =>
        string.Equals(asset.Name, "update.json", StringComparison.OrdinalIgnoreCase));

    /// <summary>The Windows installer, when this release ships one (in-app replacement).</summary>
    public GitHubReleaseAsset? SetupAsset => Assets.FirstOrDefault(asset =>
        asset.Name.EndsWith("-windows-x64-setup.exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>Checksums for the installer: <c>&lt;setup&gt;.sha256</c> first, then SHA256SUMS.txt.</summary>
    public GitHubReleaseAsset? ChecksumsAsset(string setupName) =>
        Assets.FirstOrDefault(asset => string.Equals(asset.Name, setupName + ".sha256", StringComparison.OrdinalIgnoreCase))
        ?? Assets.FirstOrDefault(asset => string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));

    public static List<GitHubRelease> DecodeList(string json) =>
        JsonSerializer.Deserialize<List<GitHubRelease>>(json, UpdateJson.Options) ?? [];
}

/// <summary>
/// <c>update.json</c>, published next to the payload by the release workflow. The Mac writes the
/// archive name in <c>zip</c>; Windows (R4) writes the installer name in <c>asset</c>. Both decode.
/// </summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("build")] int Build,
    [property: JsonPropertyName("zip")] string? Zip,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("asset")] string? Asset = null,
    [property: JsonPropertyName("commit")] string? Commit = null,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt = null)
{
    /// <summary>The published payload file: the installer on Windows, the zip on the Mac.</summary>
    public string FileName => Asset ?? Zip ?? "";

    public static UpdateManifest? Decode(string json)
    {
        try { return JsonSerializer.Deserialize<UpdateManifest>(json, UpdateJson.Options); }
        catch (JsonException) { return null; }
    }
}

public static class UpdateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    /// <summary>Versioned releases (vX.Y.Z, including betas).</summary>
    Stable,
    /// <summary>The rolling <c>main-latest</c> build of the main branch.</summary>
    Main
}

public static class UpdateChannels
{
    public static string Raw(this UpdateChannel channel) => channel == UpdateChannel.Main ? "main" : "stable";

    public static string Title(this UpdateChannel channel) => channel == UpdateChannel.Main ? "main 最新构建" : "正式版本";
}

/// <summary>What a version is worth in an ordering.</summary>
public enum VersionOrder
{
    Ascending,
    Same,
    Descending
}

/// <summary>
/// GitHub Releases payloads and the rules for picking an update. Network and installation live in
/// the app; this part is pure. Ported from UpdateCatalog.swift.
/// </summary>
public static class UpdateCatalog
{
    public const string RollingTag = "main-latest";
    /// <summary>R4: Windows releases are tagged windows-v&lt;version&gt;; the Mac uses v&lt;version&gt;.</summary>
    public const string WindowsTagPrefix = "windows-v";

    /// <summary>The release to offer on a channel, or null.</summary>
    /// <summary>Swift max(by:): equal publish times keep the first release, not the last.</summary>
    private static GitHubRelease? Newest(IEnumerable<GitHubRelease> releases)
    {
        GitHubRelease? best = null;
        var bestAt = DateTimeOffset.MinValue;
        foreach (var release in releases)
        {
            var at = release.PublishedAt ?? DateTimeOffset.MinValue;
            if (best is null || at > bestAt) { best = release; bestAt = at; }
        }
        return best;
    }

    /// <summary>The release to offer on a channel, or null (Mac UpdateCatalog.swift rules).</summary>
    public static GitHubRelease? Candidate(IReadOnlyList<GitHubRelease> releases, UpdateChannel channel)
    {
        var usable = releases.Where(release => !release.Draft && release.WindowsAsset is not null).ToList();
        return channel switch
        {
            // A formal release may be newer than the last rolling build.
            UpdateChannel.Main => Newest(usable
                .Where(release => release.TagName == RollingTag || ParseVersion(release.TagName) is not null)),
            _ => usable
                .Where(release => release.TagName != RollingTag && ParseVersion(release.TagName) is not null)
                .OrderBy(release => release.TagName, Comparer<string>.Create((a, b) => CompareVersions(a, b) switch
                {
                    VersionOrder.Ascending => -1,
                    VersionOrder.Descending => 1,
                    _ => 0
                }))
                .LastOrDefault()
        };
    }

    /// <summary>
    /// R4: the Windows updater's candidate. A Windows release is tagged <c>windows-v&lt;version&gt;</c>
    /// and ships both the installer and update.json; Mac releases, main-latest, the old
    /// windows-1.3.7-evidence tag and Windows releases without a manifest are ignored. Kept separate
    /// from <see cref="Candidate"/> so the translated Mac UpdateCatalog tests keep their contract.
    /// </summary>
    public static GitHubRelease? WindowsCandidate(IReadOnlyList<GitHubRelease> releases) =>
        releases
            .Where(IsWindowsRelease)
            .OrderBy(release => WindowsVersion(release.TagName) is { } version ? version.Numbers.Length : 0)
            .ThenBy(release => release.TagName, Comparer<string>.Create((a, b) => CompareVersions(a, b) switch
            {
                VersionOrder.Ascending => -1,
                VersionOrder.Descending => 1,
                _ => 0
            }))
            .LastOrDefault();

    /// <summary>The version after the windows-v prefix, or null when this is not a Windows tag.</summary>
    public static (int[] Numbers, string? Prerelease)? WindowsVersion(string tag)
    {
        if (!tag.StartsWith(WindowsTagPrefix, StringComparison.Ordinal)) return null;
        return ParseVersion(tag[WindowsTagPrefix.Length..]);
    }

    /// <summary>R4: a release the Windows updater may consider at all.</summary>
    public static bool IsWindowsRelease(GitHubRelease release) =>
        !release.Draft
        && WindowsVersion(release.TagName) is not null
        && release.SetupAsset is not null
        && release.ManifestAsset is not null;

    /// <summary>Whether <paramref name="manifest"/> (or, without one, the release tag) is newer than the running app.</summary>
    public static bool IsNewer(UpdateManifest? manifest, string releaseTag, string currentVersion, int currentBuild)
    {
        if (manifest is not null)
        {
            var order = CompareVersions(manifest.Version, currentVersion);
            if (order != VersionOrder.Same) return order == VersionOrder.Descending;
            return manifest.Build > currentBuild;
        }
        if (ParseVersion(releaseTag) is null) return false;
        return CompareVersions(releaseTag, currentVersion) == VersionOrder.Descending;
    }

    /// <summary>"v1.2.3-beta.4" → ([1, 2, 3], "beta.4").</summary>
    public static (int[] Numbers, string? Prerelease)? ParseVersion(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        var parts = text.Split('-', 2);
        var core = parts[0];
        var numbers = new List<int>();
        foreach (var piece in core.Split('.'))
        {
            if (!int.TryParse(piece, out var number)) return null;
            numbers.Add(number);
        }
        return numbers.Count == 0 ? null : (numbers.ToArray(), parts.Length > 1 ? parts[1] : null);
    }

    /// <summary>Semantic-version ordering; a prerelease sorts before its release.</summary>
    public static VersionOrder CompareVersions(string a, string b)
    {
        var left = ParseVersion(a);
        var right = ParseVersion(b);
        if (left is null || right is null) return string.CompareOrdinal(a, b) switch
        {
            < 0 => VersionOrder.Ascending,
            > 0 => VersionOrder.Descending,
            _ => VersionOrder.Same
        };
        var (leftNumbers, leftPre) = left.Value;
        var (rightNumbers, rightPre) = right.Value;
        for (var index = 0; index < Math.Max(leftNumbers.Length, rightNumbers.Length); index++)
        {
            var l = index < leftNumbers.Length ? leftNumbers[index] : 0;
            var r = index < rightNumbers.Length ? rightNumbers[index] : 0;
            if (l != r) return l < r ? VersionOrder.Ascending : VersionOrder.Descending;
        }
        if (leftPre is null && rightPre is null) return VersionOrder.Same;
        if (leftPre is null) return VersionOrder.Descending;
        if (rightPre is null) return VersionOrder.Ascending;
        // Compare dot-separated identifiers numerically when both are numbers.
        var lp = leftPre.Split('.');
        var rp = rightPre.Split('.');
        for (var index = 0; index < Math.Max(lp.Length, rp.Length); index++)
        {
            if (index >= lp.Length) return VersionOrder.Ascending;
            if (index >= rp.Length) return VersionOrder.Descending;
            if (int.TryParse(lp[index], out var li) && int.TryParse(rp[index], out var ri))
            {
                if (li != ri) return li < ri ? VersionOrder.Ascending : VersionOrder.Descending;
            }
            else if (!string.Equals(lp[index], rp[index], StringComparison.Ordinal))
            {
                return string.CompareOrdinal(lp[index], rp[index]) < 0 ? VersionOrder.Ascending : VersionOrder.Descending;
            }
        }
        return VersionOrder.Same;
    }

    /// <summary>Finds the hash for <paramref name="file"/> in a <c>shasum -a 256</c> listing.</summary>
    public static string? Sha256For(string file, string sums)
    {
        foreach (var line in sums.Split('\n'))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var name = parts[^1].Trim('*', '\r');
            if (name == file && parts[0].Length == 64) return parts[0].ToLowerInvariant();
        }
        return null;
    }
}
