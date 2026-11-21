using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// F16: before the title bar offers "重启以更新", the installer is downloaded in the background
/// and must pass SHA-256 (Updater.downloadUpdate + prepare). A bad checksum is rejected and the
/// downloaded file deleted, like UpdateError.checksumMismatch on Mac.
/// </summary>
public sealed class UpdateInstallerTests
{
    [Fact]
    public async Task InstallerIsDownloadedAndVerifiedBeforeTheButtonAppears()
    {
        var release = new FakeReleaseDirectory();
        try
        {
            var report = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Main, "1.0.0", 40);
            Assert.True(report.CanInstall);
            var staging = Path.Combine(release.Root, "download");
            var prepared = await UpdateInstaller.PrepareAsync(
                report.SetupUrl!, report.SetupName!, report.ChecksumsUrl!, staging);
            Assert.True(File.Exists(prepared.Path));
            Assert.Equal(release.Setup, await File.ReadAllBytesAsync(prepared.Path));
            Assert.Equal(release.ExpectedSha, prepared.Sha256);

            // The cached file can be verified again for the install step without downloading.
            Assert.Equal(release.ExpectedSha, await UpdateInstaller.VerifyAsync(prepared.Path, report.ChecksumsUrl!));
        }
        finally { release.Delete(); }
    }

    [Fact]
    public async Task InstallerWithAWrongChecksumIsRejectedAndDeleted()
    {
        var release = new FakeReleaseDirectory();
        try
        {
            var report = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Main, "1.0.0", 40);
            // Replace the published checksum with the hash of different content.
            var sums = Path.Combine(release.Root, release.SetupName + ".sha256");
            await File.WriteAllTextAsync(sums, new string('b', 64) + "  " + release.SetupName + "\n");
            var staging = Path.Combine(release.Root, "download-bad");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                UpdateInstaller.PrepareAsync(report.SetupUrl!, report.SetupName!, report.ChecksumsUrl!, staging));
            Assert.Equal("安装包校验失败，已删除下载文件。", error.Message);
            Assert.False(File.Exists(Path.Combine(staging, report.SetupName!)));
        }
        finally { release.Delete(); }
    }
}
