using System.Diagnostics;
using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the blocking line reader, process waiter and cancellable wait.</summary>
public sealed class ProcessSupportTests
{
    private static async Task<List<string>> Lines(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var lines = new List<string>();
        await foreach (var line in ProcessLineReader.Lines(stream)) lines.Add(line);
        return lines;
    }

    [Fact]
    public async Task LinesSplitOnNewlinesAndStripCarriageReturns()
    {
        Assert.Equal(["a", "b", "c"], await Lines("a\nb\r\nc"));
        Assert.Equal(["a", "b"], await Lines("a\r\nb\r\n"));
        Assert.Equal(["a", "", "b"], await Lines("a\n\nb"));
        Assert.Equal(["中文", "第二行"], await Lines("中文\n第二行"));
        Assert.Empty(await Lines(""));
    }

    [Fact]
    public async Task WaiterSeesTheExitStatus()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 3",
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        using var waiter = new ProcessExitWaiter(process);
        Assert.Equal(3, await waiter.WaitAsync());
        Assert.Equal(3, waiter.WaitSynchronously(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaiterHonoursCancellationWithoutKillingTheProcess()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 10 127.0.0.1 > nul",
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        using var waiter = new ProcessExitWaiter(process);
        using var cancellation = new CancellationTokenSource();
        var wait = waiter.WaitAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.False(process.HasExited);
        process.Kill();
    }

    [Fact]
    public async Task CancellableTaskReturnsTheSharedResult()
    {
        Assert.Equal(7, await CancellableTask.Value(Task.FromResult(7)));
        Assert.Equal("done", await CancellableTask.Value(Task.Run(() => "done")));
    }

    [Fact]
    public async Task CancellableTaskRefusesACancelledCallerButLeavesTheSharedTaskAlone()
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var wait = CancellableTask.Value(source.Task, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.False(source.Task.IsCompleted);
        source.SetResult(11);
        Assert.Equal(11, await source.Task);

        using var already = new CancellationTokenSource();
        already.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CancellableTask.Value(Task.FromResult(1), already.Token));
    }
}
