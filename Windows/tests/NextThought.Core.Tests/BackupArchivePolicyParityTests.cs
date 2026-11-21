using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from BackupArchivePolicyTests.swift (portableArchiveAndResourceGuard): the
/// portable-archive preflight accepts a well-formed listing and rejects a free-space squeeze,
/// a traversal path and a symlink.
/// </summary>
public sealed class BackupArchivePolicyParityTests
{
    [Fact]
    public void PortableArchiveAndResourceGuard()
    {
        const string names = "Backup/\nBackup/manifest.json\n";
        const string listing =
            "drwxr-xr-x folder\n-rw-r--r-- manifest\n2 files, 100 bytes uncompressed, 50 bytes compressed";

        BackupArchivePolicy.Validate(names, listing, 1024L * 1024 * 1024);

        Assert.ThrowsAny<Exception>(() => BackupArchivePolicy.Validate(names, listing, 100));
        Assert.ThrowsAny<Exception>(() => BackupArchivePolicy.Validate(
            "Backup/../escape\n", "1 file, 1 bytes uncompressed", 1024L * 1024 * 1024));
        Assert.ThrowsAny<Exception>(() => BackupArchivePolicy.Validate(
            "Backup/link\n", "lrwxrwxrwx link\n1 file, 1 bytes uncompressed", 1024L * 1024 * 1024));
    }
}
