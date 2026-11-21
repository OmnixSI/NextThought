using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E13: temporary screenshot files are recognisable and only stale ones are removed at start.
/// </summary>
public sealed class CaptureFilesTests
{
    [Fact]
    public void OnlyCapturePrefixesAreRecognised()
    {
        Assert.True(CaptureFiles.IsCaptureFile("/tmp/nextthought-shot-20260101-101010.png"));
        Assert.True(CaptureFiles.IsCaptureFile("/tmp/nextthought-window-20260101-101010.png"));
        Assert.False(CaptureFiles.IsCaptureFile("/tmp/nextthought-backup.zip"));
        Assert.False(CaptureFiles.IsCaptureFile("/tmp/screenshot.png"));
    }

    [Fact]
    public void StaleListsOnlyOldCaptureFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var stale = Path.Combine(directory, "nextthought-shot-20260101-101010.png");
            var fresh = Path.Combine(directory, "nextthought-window-20260101-101011.png");
            var other = Path.Combine(directory, "notes.txt");
            File.WriteAllText(stale, "old");
            File.WriteAllText(fresh, "new");
            File.WriteAllText(other, "keep");
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-9));
            File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddHours(-9));

            var cutoff = DateTimeOffset.UtcNow.AddHours(-6);
            Assert.Equal([stale], CaptureFiles.Stale(directory, cutoff));
            Assert.Equal(1, CaptureFiles.DeleteStale(directory, cutoff));
            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(fresh));
            Assert.True(File.Exists(other));
        }
        finally { Directory.Delete(directory, true); }
    }
}
