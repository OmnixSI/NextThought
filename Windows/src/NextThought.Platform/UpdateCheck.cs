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
            // R4: the Windows app only offers the formal channel, so its feed uses the windows-v
            // candidate. The legacy Main channel keeps the Mac rolling-build selection the
            // translated update tests exercise (it is hidden in the app this release).
            var candidate = channel == UpdateChannel.Main
                ? UpdateCatalog.Candidate(releases, channel)
                : UpdateCatalog.WindowsCandidate(releases);
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

    /// <summary>
    /// Updater.check + R4: manifest.tag must equal the release tag; on Mac the manifest.zip names the
    /// payload zip, on Windows the manifest.asset names the installer.
    /// </summary>
    private static bool Describes(UpdateManifest manifest, GitHubRelease release)
    {
        if (!string.Equals(manifest.Tag, release.TagName, StringComparison.Ordinal)) return false;
        // Windows update.json names the installer in "asset".
        if (manifest.Asset is { Length: > 0 } asset)
            return release.SetupAsset is { } setup && string.Equals(asset, setup.Name, StringComparison.Ordinal);
        // Mac-style update.json names the payload zip in "zip".
        if (manifest.Zip is { Length: > 0 } zip)
        {
            var payload = release.Assets.FirstOrDefault(candidate =>
                candidate.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            return payload is null || string.Equals(zip, payload.Name, StringComparison.Ordinal);
        }
        return false;
    }

    private static UpdateReport Report(string source, UpdateChannel channel, string currentVersion, int currentBuild,
        GitHubRelease? candidate = null, string? error = null) =>
        new(DateTimeOffset.UtcNow, source, channel.Title(), currentVersion, currentBuild,
            candidate?.TagName, null, false, candidate?.WindowsAsset?.BrowserDownloadUrl, error);
}
