using NAudio.Wave;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>F8: files longer than the old 30-second window and more than two channels.</summary>
public sealed class CloudSttFileTests
{
    [Fact]
    public void ThreeChannelWavIsDownmixedToMono()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-f8-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            var format = new WaveFormat(8000, 16, 3);
            using (var writer = new WaveFileWriter(path, format))
            {
                var buffer = new byte[format.AverageBytesPerSecond];
                for (var index = 0; index < 8000; index++)
                {
                    // Three different values so a naive first-channel copy would be visible.
                    writer.WriteByte(0);
                    writer.WriteByte(0);
                    writer.WriteByte(0x40);
                    writer.WriteByte(0x1F);
                    writer.WriteByte(0);
                    writer.WriteByte(0x80);
                }
            }
            var pcm = CloudSttClient.ReadPcm16(path, 16000);
            Assert.NotEmpty(pcm);
            Assert.True(pcm.Length > 16000 * 2 * 0.5, "one second at 16 kHz should not be truncated");
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
