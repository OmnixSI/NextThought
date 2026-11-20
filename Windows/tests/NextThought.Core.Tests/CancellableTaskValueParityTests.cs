using System.Diagnostics;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from CancellableTaskValueTests.swift: a cancelled waiter returns promptly without
/// cancelling the shared warm resource, and a completed shared resource stays reusable.
/// </summary>
public sealed class CancellableTaskValueParityTests
{
    [Fact]
    public async Task CancelledWaitDoesNotCancelWarmResource()
    {
        using var warmCancellation = new CancellationTokenSource();
        var warm = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), warmCancellation.Token);
            return 42;
        });
        try
        {
            using var waiterCancellation = new CancellationTokenSource();
            var waiter = CancellableTask.Value(warm, waiterCancellation.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(30));
            var started = Stopwatch.StartNew();
            waiterCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(0.5));
            Assert.False(warm.IsCanceled);
        }
        finally
        {
            warmCancellation.Cancel();
        }
    }

    [Fact]
    public async Task CompletedWarmResourceRemainsReusable()
    {
        var warm = Task.FromResult(7);
        Assert.Equal(7, await CancellableTask.Value(warm));
        Assert.Equal(7, await CancellableTask.Value(warm));
    }
}
