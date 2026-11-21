using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// R4 / §10.4: Windows releases are tagged <c>windows-v&lt;version&gt;</c> and must ship both the
/// installer and update.json; Mac releases (v3.4.0), the rolling main-latest build, the old
/// windows-1.3.7-evidence tag and Windows releases without a manifest are ignored, so a Windows
/// update never offers a Mac artifact and never reads the wrong manifest.
/// </summary>
public sealed class WindowsUpdateCatalogTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "AppState.Update.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static GitHubReleaseAsset Asset(string name) =>
        new(1, name, 1, "u", "https://example/" + name);

    private static GitHubRelease Release(string tag, params string[] assets) =>
        new(tag, tag, null, false, false, DateTimeOffset.Parse("2026-11-20T10:00:00Z"),
            assets.Select(Asset).ToList());

    private static readonly IReadOnlyList<GitHubRelease> Feed =
    [
        // Mac release: no Windows installer.
        Release("v3.4.0", "NextThought-3.4.0-macOS-arm64.zip"),
        Release("main-latest", "NextThought-main-windows-x64.zip", "update.json"),
        Release("windows-1.3.7-evidence", "NextThought-1.3.7-windows-x64-setup.exe", "update.json"),
        Release("windows-v0.9.0", "NextThought-0.9.0-windows-x64-setup.exe", "update.json", "SHA256SUMS.txt"),
        Release("windows-v0.9.1", "NextThought-0.9.1-windows-x64-setup.exe", "update.json", "SHA256SUMS.txt"),
        // Installer without update.json: cannot be verified.
        Release("windows-v0.9.2", "NextThought-0.9.2-windows-x64-setup.exe", "SHA256SUMS.txt")
    ];

    [Fact]
    public void OnlyWindowsTaggedReleasesWithInstallerAndManifestAreCandidates()
    {
        var candidate = UpdateCatalog.WindowsCandidate(Feed);
        Assert.NotNull(candidate);
        Assert.Equal("windows-v0.9.1", candidate.TagName);
        // The translated Mac contract is untouched: the legacy channel selector keeps its
        // Mac rules (Mac tags/rolling build) and never drives the Windows app.
        Assert.Equal("main-latest", UpdateCatalog.Candidate(Feed, UpdateChannel.Main)?.TagName);
        Assert.Null(UpdateCatalog.Candidate(Feed, UpdateChannel.Stable));
    }

    [Fact]
    public void WindowsVersionComesOnlyFromTheWindowsTagPrefix()
    {
        Assert.Equal((new[] { 0, 9, 1 }, (string?)null), UpdateCatalog.WindowsVersion("windows-v0.9.1"));
        Assert.Null(UpdateCatalog.WindowsVersion("v3.4.0"));
        Assert.Null(UpdateCatalog.WindowsVersion("main-latest"));
        Assert.Null(UpdateCatalog.WindowsVersion("windows-1.3.7-evidence"));
    }

    [Fact]
    public void TheManifestReadsTheInstallerAssetFieldFromTheWindowsRelease()
    {
        var manifest = UpdateManifest.Decode(
            "{\"tag\":\"windows-v0.9.1\",\"version\":\"0.9.1\",\"build\":42,\"asset\":\"NextThought-0.9.1-windows-x64-setup.exe\"," +
            "\"sha256\":\"" + new string('a', 64) + "\",\"commit\":\"deadbeef\",\"publishedAt\":\"2026-11-20T10:00:00Z\"}");
        Assert.NotNull(manifest);
        Assert.Equal("windows-v0.9.1", manifest.Tag);
        Assert.Equal("NextThought-0.9.1-windows-x64-setup.exe", manifest.FileName);
        // Mac-style update.json (zip field) still decodes, so old fixtures keep working.
        var macStyle = UpdateManifest.Decode(
            "{\"tag\":\"main-latest\",\"version\":\"1.0.0\",\"build\":41,\"zip\":\"NextThought-main-windows-x64.zip\"," +
            "\"sha256\":\"" + new string('b', 64) + "\"}");
        Assert.NotNull(macStyle);
        Assert.Equal("NextThought-main-windows-x64.zip", macStyle.FileName);
    }

    [Fact]
    public void TheAppExposesOnlyTheFormalWindowsChannel()
    {
        // R4 / §10.4 item 3: the rolling main channel is not offered on Windows this release.
        var app = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Update.cs"));
        Assert.Contains("new(UpdateChannel.Stable", app);
        Assert.DoesNotContain("new(UpdateChannel.Main", app);
        Assert.Contains("R4", app);
    }

    [Fact]
    public async Task TheWindowsFeedOffersTheLatestWindowsReleaseOnly()
    {
        var release = new FakeReleaseDirectory(tag: "windows-v1.0.0", build: 41, useAssetField: true);
        try
        {
            var report = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Stable, "0.9.0", 40);
            Assert.Null(report.Error);
            Assert.Equal("windows-v1.0.0", report.CandidateTag);
            Assert.True(report.Available);
            Assert.Equal("1.0.0（#41）", report.LatestVersion);
            Assert.True(report.CanInstall, "installer and checksums must be offered");

            var upToDate = await UpdateChecker.RunAsync(release.Feed, UpdateChannel.Stable, "1.0.0", 41);
            Assert.False(upToDate.Available);

            // Mac-style release lists never become a Windows candidate.
            var macOnly = new FakeReleaseDirectory(tag: "v3.4.0");
            try
            {
                var macReport = await UpdateChecker.RunAsync(macOnly.Feed, UpdateChannel.Stable, "0.9.0", 40);
                Assert.False(macReport.Available);
                Assert.Null(macReport.CandidateTag);
            }
            finally { macOnly.Delete(); }
        }
        finally { release.Delete(); }
    }
}
