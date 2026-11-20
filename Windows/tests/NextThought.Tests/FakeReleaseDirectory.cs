using System.Security.Cryptography;
using System.Text;

namespace NextThought.Tests;

/// <summary>
/// A local directory shaped like one published GitHub release: releases.json plus update.json,
/// the Windows zip, a setup .exe and its .sha256. The update flow reads it through the same
/// UpdateFeedPolicy paths the QA switch uses, so no network is involved.
/// </summary>
internal sealed class FakeReleaseDirectory
{
    public string Root { get; }
    public byte[] Setup { get; }
    public string SetupName { get; } = "NextThought-1.0.0-windows-x64-setup.exe";
    public string ExpectedSha { get; }
    public string ZipName { get; } = "NextThought-1.0.0-windows-x64.zip";
    public string Feed { get; }

    public FakeReleaseDirectory(string? manifestTag = null, int build = 41, string tag = "main-latest")
    {
        Root = Path.Combine(Path.GetTempPath(), "nt-fake-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Setup = Encoding.UTF8.GetBytes("fake installer payload " + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(Path.Combine(Root, SetupName), Setup);
        ExpectedSha = Convert.ToHexString(SHA256.HashData(Setup)).ToLowerInvariant();
        File.WriteAllText(Path.Combine(Root, SetupName + ".sha256"), ExpectedSha + "  " + SetupName + "\n");
        File.WriteAllBytes(Path.Combine(Root, ZipName), [1, 2, 3]);

        File.WriteAllText(Path.Combine(Root, "update.json"),
            $"{{\"tag\":\"{manifestTag ?? tag}\",\"version\":\"1.0.0\",\"build\":{build}," +
            $"\"zip\":\"{ZipName}\",\"sha256\":\"{new string('a', 64)}\",\"commit\":\"deadbeef\"}}");

        string Asset(long id, string name) =>
            $"{{\"id\":{id},\"name\":\"{name}\",\"size\":1,\"url\":\"u{id}\"," +
            $"\"browser_download_url\":{System.Text.Json.JsonSerializer.Serialize(Path.Combine(Root, name))}}}";
        Feed = Path.Combine(Root, "releases.json");
        File.WriteAllText(Feed,
            "[{\"tag_name\":\"" + tag + "\",\"name\":\"rolling\",\"draft\":false,\"prerelease\":true," +
            "\"published_at\":\"2026-11-20T10:00:00Z\",\"assets\":[" +
            string.Join(',', Asset(1, "update.json"), Asset(2, ZipName), Asset(3, SetupName), Asset(4, SetupName + ".sha256")) +
            "]}]");
    }

    public void Delete()
    {
        try { Directory.Delete(Root, true); } catch { }
    }
}
