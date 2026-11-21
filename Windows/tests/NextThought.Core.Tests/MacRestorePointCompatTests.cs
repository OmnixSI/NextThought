using System.Security.Cryptography;
using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A16: Mac 3.4.0 restore points carry a legacy "Sessions/essions/…" path prefix when the walked
/// directory URL kept a trailing slash. Reading them on Windows must restore to the real directory.
/// </summary>
public sealed class MacRestorePointCompatTests
{
    [Theory]
    [InlineData("Sessions/essions/ABC/meta.json", "Sessions/ABC/meta.json")]
    [InlineData("Originals/riginals/abc.pdf", "Originals/abc.pdf")]
    [InlineData("Materials/aterials/1/document.json", "Materials/1/document.json")]
    [InlineData("Migration/igration/x.ntbackup", "Migration/x.ntbackup")]
    [InlineData("Sessions/ABC/meta.json", "Sessions/ABC/meta.json")]
    [InlineData("manifest.json", "manifest.json")]
    public void LegacyDirectoryPrefixIsNormalised(string input, string expected) =>
        Assert.Equal(expected, IncrementalBackupStore.NormaliseLegacyPath(input));

    [Fact]
    public void MacStyleRestorePointRestoresUnderTheRealDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-a16-restore-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "out");
        try
        {
            var body = Encoding.UTF8.GetBytes("{\"id\":\"ABC\"}");
            var hash = Convert.ToHexStringLower(SHA256.HashData(body));
            var objects = Path.Combine(root, "Backups", "Objects");
            var points = Path.Combine(root, "Backups", "RestorePoints");
            Directory.CreateDirectory(objects);
            Directory.CreateDirectory(points);
            File.WriteAllBytes(Path.Combine(objects, hash), body);

            // The Mac manifest uses Foundation's seconds-since-2001 date and the legacy session prefix.
            const string createdAt = "816920373.884471";
            var files = "{\"Sessions/essions/ABC/meta.json\":\"" + hash + "\"}";
            var fingerprintSource = "Sessions/essions/ABC/meta.json\u0000" + hash;
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)));
            var pointPath = Path.Combine(points, "fixture-A.ntrestore");
            File.WriteAllText(pointPath, "{\"version\":1,\"createdAt\":" + createdAt + ",\"reason\":\"fixture A\",\"fingerprint\":\""
                + fingerprint + "\",\"files\":" + files + "}");

            var store = new IncrementalBackupStore(Path.Combine(root, "Backups"));
            var listed = Assert.Single(store.List());
            Assert.Equal("fixture A", listed.Reason);
            Assert.True(listed.CreatedAt.Year > 2020);

            store.Restore(pointPath, destination);
            Assert.True(File.Exists(Path.Combine(destination, "Sessions", "ABC", "meta.json")));
            Assert.False(Directory.Exists(Path.Combine(destination, "Sessions", "essions")));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
