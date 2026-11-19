using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for UpdateCatalog.swift.</summary>
public sealed class UpdateCatalogTests
{
    private static GitHubRelease Release(string tag, int day, bool draft = false, string asset = "NextThought-0.0.0-windows-x64.zip") =>
        new(tag, tag, null, draft, false, new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero),
            [new GitHubReleaseAsset(1, asset, 10, "https://api", "https://dl/" + asset)]);

    [Fact]
    public void ParsesVersionsWithAndWithoutV()
    {
        Assert.Equal([1, 2, 3], UpdateCatalog.ParseVersion("v1.2.3")!.Value.Numbers);
        Assert.Equal("beta.4", UpdateCatalog.ParseVersion("V1.2.3-beta.4")!.Value.Prerelease);
        Assert.Null(UpdateCatalog.ParseVersion("main-latest"));
        Assert.Null(UpdateCatalog.ParseVersion("v1.x"));
    }

    [Fact]
    public void ComparesSemanticallyAndPrereleasesSortFirst()
    {
        Assert.Equal(VersionOrder.Ascending, UpdateCatalog.CompareVersions("v0.15.0", "v0.16.0"));
        Assert.Equal(VersionOrder.Descending, UpdateCatalog.CompareVersions("v1.0.0", "v0.9.9"));
        Assert.Equal(VersionOrder.Same, UpdateCatalog.CompareVersions("v1.2.0", "1.2.0"));
        Assert.Equal(VersionOrder.Ascending, UpdateCatalog.CompareVersions("v1.2.0-beta.1", "v1.2.0"));
        Assert.Equal(VersionOrder.Ascending, UpdateCatalog.CompareVersions("v1.2.0-beta.1", "v1.2.0-beta.2"));
        Assert.Equal(VersionOrder.Descending, UpdateCatalog.CompareVersions("v1.2.0-beta.10", "v1.2.0-beta.2"));
    }

    [Fact]
    public void StableChannelPicksTheNewestVersionedRelease()
    {
        var releases = new List<GitHubRelease>
        {
            Release("main-latest", 20),
            Release("v0.9.0", 10),
            Release("v0.10.1", 12),
            Release("v0.11.0", 14, draft: true),
            Release("v0.12.0", 15, asset: "NextThought-0.12.0-macOS-arm64.zip")
        };
        var candidate = UpdateCatalog.Candidate(releases, UpdateChannel.Stable);
        Assert.Equal("v0.10.1", candidate!.TagName);
    }

    [Fact]
    public void MainChannelTakesTheMostRecentRollingOrVersionedBuild()
    {
        var releases = new List<GitHubRelease>
        {
            Release("main-latest", 20),
            Release("v0.16.0", 25)
        };
        Assert.Equal("v0.16.0", UpdateCatalog.Candidate(releases, UpdateChannel.Main)!.TagName);
        releases.Add(Release("main-latest", 30));
        Assert.Equal("main-latest", UpdateCatalog.Candidate(releases, UpdateChannel.Main)!.TagName);
    }

    [Fact]
    public void IsNewerUsesTheManifestThenTheBuild()
    {
        Assert.True(UpdateCatalog.IsNewer(null, "v0.16.0", "0.15.0", 1));
        Assert.False(UpdateCatalog.IsNewer(null, "v0.15.0", "0.15.0", 1));
        Assert.False(UpdateCatalog.IsNewer(null, "main-latest", "0.15.0", 1));
        var manifest = new UpdateManifest("v0.15.0", "0.15.0", 7, "x.zip", new string('a', 64));
        Assert.True(UpdateCatalog.IsNewer(manifest, "v0.15.0", "0.15.0", 6));
        Assert.False(UpdateCatalog.IsNewer(manifest, "v0.15.0", "0.15.0", 7));
        Assert.True(UpdateCatalog.IsNewer(new UpdateManifest("v0.16.0", "0.16.0", 1, "x", ""), "v0.16.0", "0.15.0", 9));
    }

    [Fact]
    public void FindsHashesInShasumListings()
    {
        var hash = new string('b', 64);
        var sums = $"aaaa  other.zip\n{hash}  *NextThought-0.16.0-windows-x64.zip\n";
        Assert.Equal(hash, UpdateCatalog.Sha256For("NextThought-0.16.0-windows-x64.zip", sums));
        Assert.Null(UpdateCatalog.Sha256For("missing.zip", sums));
    }

    [Fact]
    public void DecodesGitHubPayloads()
    {
        var releases = GitHubRelease.DecodeList("""
            [{"tag_name":"v0.16.0","published_at":"2026-11-18T09:00:00Z","draft":false,
              "assets":[{"id":1,"name":"NextThought-0.16.0-windows-x64.zip","size":10,"url":"u"}]}]
            """);
        Assert.Single(releases);
        Assert.Equal("v0.16.0", releases[0].TagName);
        Assert.NotNull(releases[0].WindowsAsset);
        var manifest = UpdateManifest.Decode("""{"tag":"v0.16.0","version":"0.16.0","build":3,"zip":"a.zip","sha256":"abc"}""");
        Assert.Equal("0.16.0", manifest!.Version);
        Assert.Equal(3, manifest.Build);
    }
}
