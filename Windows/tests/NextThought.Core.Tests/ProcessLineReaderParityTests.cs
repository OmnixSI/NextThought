using System.Diagnostics;
using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from ProcessLineReaderTests.swift: an idle pipe never blocks another process's
/// reader, and lines survive a split UTF-8 sequence, CRLF and a final unterminated line.
/// </summary>
public sealed class ProcessLineReaderParityTests
{
    [Fact]
    public async Task IdlePipeDoesNotBlockAnotherProcess()
    {
        // A child process that stays silent is the idle pipe of the Swift test.
        using var idle = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 10 127.0.0.1 >nul",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        })!;
        using var idleCancellation = new CancellationTokenSource();
        var idleReader = ConsumeAsync(idle.StandardOutput.BaseStream, idleCancellation.Token);

        using var active = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c echo second process ready",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        })!;

        try
        {
            await Task.Delay(50);

            var lines = ProcessLineReader.Lines(active.StandardOutput.BaseStream);
            var result = await FirstLineAsync(lines).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("second process ready", result);
        }
        finally
        {
            idleCancellation.Cancel();
            if (!idle.HasExited) idle.Kill(entireProcessTree: true);
            await idle.WaitForExitAsync();
            await idleReader;
        }
    }

    [Fact]
    public async Task PreservesSplitUtf8CrlfAndFinalUnterminatedLine()
    {
        var data = Encoding.UTF8.GetBytes("联网🙂\r\nlast");
        // The Swift test writes one byte at a time into a Pipe; this stream hands out one byte per
        // Read so the reader still sees the UTF-8 sequence and the text split across reads.
        var lines = ProcessLineReader.Lines(new OneBytePerReadStream(data));
        var result = new List<string>();
        await foreach (var line in lines) result.Add(line);
        Assert.Equal(new[] { "联网🙂", "last" }, result);
    }

    private static async Task ConsumeAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in ProcessLineReader.Lines(stream, cancellationToken)) { }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The idle reader is cancelled when the test kills its process.
        }
    }

    private static async Task<string?> FirstLineAsync(IAsyncEnumerable<string> lines)
    {
        await foreach (var line in lines) return line;
        return null;
    }

    private sealed class OneBytePerReadStream(byte[] data) : Stream
    {
        private int position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0 || position >= data.Length) return 0;
            buffer[offset] = data[position++];
            return 1;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
