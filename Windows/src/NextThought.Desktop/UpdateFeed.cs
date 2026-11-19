using System.Text.Json;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A checked-for update: what the feed said and what it means for this build.</summary>
public sealed record UpdateReport(
    DateTimeOffset CheckedAt,
    string Feed,
    string Channel,
    string CurrentVersion,
    int CurrentBuild,
    string? CandidateTag,
    string? LatestVersion,
    bool Available,
    string? DownloadUrl,
    string? Error,
    string? SetupUrl = null,
    string? SetupName = null,
    string? ChecksumsUrl = null)
{
    /// <summary>True when the app can download, verify and run the installer itself.</summary>
    public bool CanInstall => SetupUrl is { Length: > 0 } && ChecksumsUrl is { Length: > 0 };

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Describe() => Error is not null
        ? "检查更新失败：" + Error
        : Available
            ? $"有新版本 {LatestVersion}（{CandidateTag}），" + (CanInstall ? "可以下载并安装。" : "可以打开下载页。")
            : $"已是最新：当前 {CurrentVersion}，频道「{Channel}」。";
}

/// <summary>Reads a release feed from an HTTP(S) URL or a local file (the QA path).</summary>
public static class UpdateFeed
{
    public const string Default = NextThought.Platform.UpdateFeedPolicy.Default;

    /// <summary>
    /// The feed is fixed to HTTPS in normal runs; overrides only work with
    /// NEXTTHOUGHT_QA=1 and never over plain HTTP.
    /// </summary>
    public static string Source => NextThought.Platform.UpdateFeedPolicy.ResolveSource(Environment.GetEnvironmentVariable);

    public static Task<string> ReadAsync(string source, CancellationToken cancellationToken = default) =>
        NextThought.Platform.UpdateFeedPolicy.ReadAsync(source, cancellationToken);
}

/// <summary>The update check itself: read the feed, pick a candidate, compare with this build.</summary>
public static class UpdateCheck
{
    public static string CurrentVersion
    {
        get
        {
            var version = typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static int CurrentBuild => (typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0)).Revision;

    public static async Task<UpdateReport> RunAsync(string source, UpdateChannel channel, CancellationToken cancellationToken = default)
    {
        var version = CurrentVersion;
        var build = CurrentBuild;
        try
        {
            var json = await UpdateFeed.ReadAsync(source, cancellationToken);
            var releases = GitHubRelease.DecodeList(json);
            var candidate = UpdateCatalog.Candidate(releases, channel);
            if (candidate is null)
                return new UpdateReport(DateTimeOffset.UtcNow, source, channel.Title(), version, build, null, null, false, null, null);
            var latest = UpdateCatalog.ParseVersion(candidate.TagName) is { } parsed
                ? string.Join('.', parsed.Numbers) + (parsed.Prerelease is { } pre ? "-" + pre : "")
                : candidate.TagName;
            var available = UpdateCatalog.IsNewer(null, candidate.TagName, version, build);
            var url = candidate.WindowsAsset?.BrowserDownloadUrl;
            var setup = candidate.SetupAsset;
            var checksums = setup is null ? null : candidate.ChecksumsAsset(setup.Name);
            return new UpdateReport(DateTimeOffset.UtcNow, source, channel.Title(), version, build,
                candidate.TagName, latest, available, url, null,
                setup?.BrowserDownloadUrl, setup?.Name, checksums?.BrowserDownloadUrl);
        }
        catch (Exception ex)
        {
            return new UpdateReport(DateTimeOffset.UtcNow, source, channel.Title(), version, build, null, null, false, null, ex.Message);
        }
    }
}
