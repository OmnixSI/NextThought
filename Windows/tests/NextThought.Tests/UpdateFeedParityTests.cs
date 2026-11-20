using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// F14: the update check is driven from a local fake release directory, like Updater.check()
/// reads the GitHub feed. A rolling build's tag never parses as a version, so only the
/// update.json build number can decide that it is newer.
/// </summary>
public sealed class UpdateFeedParityTests
{
    [Fact]
    public async Task RollingReleaseIsFoundThroughItsManifestBuildNumber()
    {
        var release = new FakeReleaseDirectory();
        try
        {
            var report = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Main, "1.0.0", 40);
            Assert.Null(report.Error);
            Assert.Equal("main-latest", report.CandidateTag);
            Assert.True(report.Available, "a higher manifest build number must be offered");
            Assert.Equal("main 构建 #41", report.LatestVersion);
            Assert.True(report.CanInstall, "the release ships a setup and a checksum file");
            Assert.Equal(release.SetupName, report.SetupName);

            // Same build as the running app: nothing to install.
            var upToDate = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Main, "1.0.0", 41);
            Assert.False(upToDate.Available);
        }
        finally { release.Delete(); }
    }

    [Fact]
    public async Task ManifestMustDescribeTheRelease()
    {
        var release = new FakeReleaseDirectory(manifestTag: "v9.9.9");
        try
        {
            var report = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Main, "1.0.0", 40);
            Assert.False(report.Available);
            Assert.Equal("更新清单与发布不一致。", report.Error);
        }
        finally { release.Delete(); }
    }
}
