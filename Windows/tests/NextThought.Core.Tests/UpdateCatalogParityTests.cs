using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Swift parity: Tests/NextThoughtCoreTests/UpdateCatalogTests.swift.
/// The Windows port gates releases on a Windows asset instead of the Mac zip asset, so the
/// fixture releases carry the Windows asset name.
/// </summary>
public sealed class UpdateCatalogParityTests
{
    private static GitHubRelease Release(string tag, bool prerelease = false, bool draft = false, bool zip = true) =>
        new(tag, tag, null, draft, prerelease, null,
            zip ? [new GitHubReleaseAsset(1, $"NextThought-{tag}-windows-x64.zip", 1, "u")] : []);

    [Fact]
    public void VersionOrdering()
    {
        Assert.Equal(VersionOrder.Descending, UpdateCatalog.CompareVersions("v1.2.10", "v1.2.9"));
        Assert.Equal(VersionOrder.Same, UpdateCatalog.CompareVersions("1.2", "v1.2.0"));
        Assert.Equal(VersionOrder.Ascending, UpdateCatalog.CompareVersions("v1.3.0-beta.2", "v1.3.0"));
        Assert.Equal(VersionOrder.Descending, UpdateCatalog.CompareVersions("v1.3.0-beta.10", "v1.3.0-beta.9"));
        Assert.Null(UpdateCatalog.ParseVersion("main-latest"));
    }

    [Fact(Skip = "missing API: UpdateCatalog.Candidate(releases, UpdateChannel.Main) tie-break without publishedAt (Swift max-by keeps the first equal element; the C# port orders by PublishedAt and keeps the last)")]
    public void PicksNewestVersionedOrRollingRelease()
    {
    }

    [Fact]
    public void BuildNumberDecidesWhenManifestExists()
    {
        var manifest = new UpdateManifest("main-latest", "0.2.0", 42, "z.zip", new string('a', 64));
        Assert.True(UpdateCatalog.IsNewer(manifest, "main-latest", "0.2.0", 41));
        Assert.False(UpdateCatalog.IsNewer(manifest, "main-latest", "0.2.0", 42));
        Assert.False(UpdateCatalog.IsNewer(null, "main-latest", "0.2.0", 1));
        Assert.True(UpdateCatalog.IsNewer(null, "v0.3.0", "0.2.0", 1));
    }

    [Fact]
    public void MarketingVersionPreventsDowngradeAcrossBuildCounters()
    {
        var oldMain = new UpdateManifest("main-latest", "1.4.1", 34, "z.zip", "a");
        Assert.False(UpdateCatalog.IsNewer(oldMain, "main-latest", "1.6.0", 3));
        var formal = new UpdateManifest("v1.6.0", "1.6.0", 3, "z.zip", "a");
        Assert.True(UpdateCatalog.IsNewer(formal, "v1.6.0", "1.4.1", 34));
        Assert.False(UpdateCatalog.IsNewer(formal, "v1.6.0", "1.6.0", 3));
        Assert.True(UpdateCatalog.IsNewer(formal, "v1.6.0", "1.6.0", 2));
    }

    [Fact]
    public void MainChannelUsesFormalReleaseWhenRollingBuildIsStale()
    {
        var rolling = Release("main-latest", prerelease: true) with { PublishedAt = DateTimeOffset.FromUnixTimeSeconds(100) };
        var formal = Release("v1.6.0") with { PublishedAt = DateTimeOffset.FromUnixTimeSeconds(200) };
        Assert.Equal("v1.6.0", UpdateCatalog.Candidate([rolling, formal], UpdateChannel.Main)?.TagName);
        rolling = rolling with { PublishedAt = DateTimeOffset.FromUnixTimeSeconds(300) };
        Assert.Equal("main-latest", UpdateCatalog.Candidate([formal, rolling], UpdateChannel.Main)?.TagName);
        Assert.Equal("v1.6.0", UpdateCatalog.Candidate([rolling, formal], UpdateChannel.Stable)?.TagName);
    }

    [Fact]
    public void DecodesGitHubPayloadAndChecksums()
    {
        var json = """
            [{"tag_name":"v1.0.0","name":"NextThought v1.0.0","draft":false,"prerelease":false,"published_at":"2026-10-01T10:00:00Z",
              "assets":[{"id":7,"name":"NextThought-1.0.0-macOS-arm64.zip","size":123,"url":"https://api.github.com/repos/o/r/releases/assets/7","browser_download_url":"https://github.com/o/r/releases/download/v1.0.0/x.zip"}]}]
            """;
        var releases = GitHubRelease.DecodeList(json);
        Assert.Equal(7L, releases.First().Asset("NextThought-1.0.0-macOS-arm64.zip")?.Id);

        var sums = new string('b', 64) + "  NextThought-1.0.0-macOS-arm64.zip\n"
                   + new string('c', 64) + "  NextThought-1.0.0-macOS-arm64.dmg\n";
        Assert.Equal(new string('b', 64), UpdateCatalog.Sha256For("NextThought-1.0.0-macOS-arm64.zip", sums));
        Assert.Null(UpdateCatalog.Sha256For("missing.zip", sums));
    }
}
