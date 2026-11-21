using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the SHA-256 side of in-app updates.</summary>
public sealed class UpdateChecksumTests
{
    private const string Hash = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    [Fact]
    public void FindsTheNamedFile()
    {
        var text = $"{new string('0', 64)}  other.zip\n{Hash}  NextThought-1.0-windows-x64-setup.exe\n";
        Assert.Equal(Hash, UpdateChecksums.Find(text, "NextThought-1.0-windows-x64-setup.exe"));
        Assert.Null(UpdateChecksums.Find(text, "missing.exe"));
    }

    [Fact]
    public void AcceptsTheStarPrefixAndExtraSpaces()
    {
        var text = $"  {Hash} *NextThought-1.0-windows-x64-setup.exe  \n";
        Assert.Equal(Hash, UpdateChecksums.Find(text, "NextThought-1.0-windows-x64-setup.exe"));
    }

    [Fact]
    public void FallsBackToASingleBareHash()
    {
        Assert.Equal(Hash, UpdateChecksums.Find(Hash + "\n", "anything.exe"));
        Assert.Null(UpdateChecksums.Find($"{Hash}\n{Hash}\n", "anything.exe"));
    }

    [Fact]
    public void IgnoresMalformedLines()
    {
        var text = "not-a-hash  file.exe\n" + Hash + "\n";
        Assert.Equal(Hash, UpdateChecksums.Find(text, "file.exe"));
        Assert.Null(UpdateChecksums.Find("garbage\nnonsense", "file.exe"));
    }

    [Fact]
    public void VerifiesARealFileAndRejectsTheWrongHash()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-checksum-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(path, "NextThought"u8.ToArray());
            using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
            Assert.True(UpdateChecksums.Verify(path, actual));
            Assert.True(UpdateChecksums.Verify(path, actual.ToLowerInvariant()));
            Assert.False(UpdateChecksums.Verify(path, new string('0', 64)));
            Assert.False(UpdateChecksums.Verify(path, "short"));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
