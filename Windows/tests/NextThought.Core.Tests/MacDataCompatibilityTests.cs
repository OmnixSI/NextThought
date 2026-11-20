using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Mac-authored JSON shapes that the Windows port must read and write losslessly.</summary>
public sealed class MacDataCompatibilityTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-mac-compat-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    [Fact]
    public void BackupManifestReadsMacKindAndWritesItBackAsAString()
    {
        const string mac = """{"format":"nextthought-backup","version":1,"kind":"full","createdAt":"2026-11-18T10:00:00Z","appVersion":"3.3.0","appBuild":"1","includesSecrets":false,"workspaceCount":1,"sessionCount":2}""";
        var manifest = JsonSerializer.Deserialize<BackupManifest>(mac, BackupArchive.Options)!;
        Assert.Equal(BackupManifest.BackupKind.Full, manifest.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero), manifest.CreatedAt);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifest, BackupArchive.Options));
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("kind").ValueKind);
        Assert.Equal("full", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("2026-11-18T10:00:00Z", document.RootElement.GetProperty("createdAt").GetString());
    }

    [Fact]
    public void WorkspaceStoreWritesIsoUtcDatesForMac()
    {
        var store = new WorkspaceStore(root);
        var created = new DateTimeOffset(2026, 11, 18, 10, 0, 0, 123, TimeSpan.FromHours(8));
        var series = WorkspaceSeries.Create("Round-trip") with { CreatedAt = created };
        store.Save(new WorkspaceData([series]));

        var file = Directory.EnumerateFiles(Path.Combine(root, "Workspaces"), "*.json").Single();
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var value = document.RootElement.GetProperty("createdAt").GetString();
        Assert.Equal("2026-11-18T02:00:00Z", value);
    }

    [Fact]
    public void RestorePointUsesFoundationEpochAndReadsMacNumbers()
    {
        var store = new IncrementalBackupStore(root);
        var pointPath = store.Create(new Dictionary<string, byte[]> { ["a.txt"] = [1, 2, 3] });
        using (var written = JsonDocument.Parse(File.ReadAllText(pointPath)))
            Assert.Equal(JsonValueKind.Number, written.RootElement.GetProperty("createdAt").ValueKind);

        var emptyFingerprint = Convert.ToHexStringLower(SHA256.HashData([]));
        var points = Path.Combine(root, "RestorePoints");
        Directory.CreateDirectory(points);
        var macPoint = Path.Combine(points, "mac.ntrestore");
        File.WriteAllText(macPoint,
            "{\"version\":1,\"createdAt\":786000000.5,\"reason\":\"mac\",\"fingerprint\":\"" + emptyFingerprint + "\",\"files\":{}}");
        var error = Record.Exception(() => store.Restore(macPoint, Path.Combine(root, "restored")));
        Assert.Null(error);
    }
}
