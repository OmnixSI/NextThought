using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

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

/// <summary>
/// The update check itself: read the feed, pick a candidate, read the release's
/// <c>update.json</c> and compare with this build (port of Updater.check + UpdateCatalog).
/// Version/build come from the caller so the app assembly remains the single source.
/// </summary>
public static class UpdateChecker
{
    public static async Task<UpdateReport> RunAsync(
        string source, UpdateChannel channel, string currentVersion, int currentBuild,
        string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await UpdateFeedPolicy.ReadAsync(source, token, cancellationToken);
            var releases = GitHubRelease.DecodeList(json);
            var candidate = UpdateCatalog.Candidate(releases, channel);
            if (candidate is null) return Report(source, channel, currentVersion, currentBuild);

            // F14: the rolling release's tag never parses as a version, so the build number can
            // only come from the update.json shipped with the release.
            UpdateManifest? manifest = null;
            if (candidate.ManifestAsset?.BrowserDownloadUrl is { Length: > 0 } manifestUrl)
            {
                try { manifest = UpdateManifest.Decode(await UpdateFeedPolicy.ReadAsync(manifestUrl, token, cancellationToken)); }
                catch (Exception) { manifest = null; }
                // Updater.check: a manifest that does not describe this release is an invalid archive.
                if (manifest is not null && !Describes(manifest, candidate))
                    return Report(source, channel, currentVersion, currentBuild, candidate, "更新清单与发布不一致。");
            }

            var available = UpdateCatalog.IsNewer(manifest, candidate.TagName, currentVersion, currentBuild);
            var display = DisplayVersion(manifest, candidate);
            var setup = candidate.SetupAsset;
            var checksums = setup is null ? null : candidate.ChecksumsAsset(setup.Name);
            return new UpdateReport(DateTimeOffset.UtcNow, source, channel.Title(), currentVersion, currentBuild,
                candidate.TagName, display, available, candidate.WindowsAsset?.BrowserDownloadUrl, null,
                setup?.BrowserDownloadUrl, setup?.Name, checksums?.BrowserDownloadUrl);
        }
        catch (Exception ex)
        {
            return Report(source, channel, currentVersion, currentBuild, error: ex.Message);
        }
    }

    /// <summary>Updater.Available.displayVersion, without the app-bundle wording.</summary>
    private static string DisplayVersion(UpdateManifest? manifest, GitHubRelease release)
    {
        if (manifest is not null)
            return manifest.Tag == UpdateCatalog.RollingTag
                ? $"main 构建 #{manifest.Build}"
                : $"{manifest.Version}（#{manifest.Build}）";
        return UpdateCatalog.ParseVersion(release.TagName) is { } parsed
            ? string.Join('.', parsed.Numbers) + (parsed.Prerelease is { } pre ? "-" + pre : "")
            : release.TagName;
    }

    /// <summary>Updater.check: manifest.tag == release.tagName and manifest.zip == the payload name.</summary>
    private static bool Describes(UpdateManifest manifest, GitHubRelease release)
    {
        if (!string.Equals(manifest.Tag, release.TagName, StringComparison.Ordinal)) return false;
        if (release.WindowsAsset is not { } payload) return false;
        if (!payload.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(manifest.Zip, payload.Name, StringComparison.Ordinal);
    }

    private static UpdateReport Report(string source, UpdateChannel channel, string currentVersion, int currentBuild,
        GitHubRelease? candidate = null, string? error = null) =>
        new(DateTimeOffset.UtcNow, source, channel.Title(), currentVersion, currentBuild,
            candidate?.TagName, null, false, candidate?.WindowsAsset?.BrowserDownloadUrl, error);
}
