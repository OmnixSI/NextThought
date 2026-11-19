using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the port of LiveTranslationRevisionGate.swift.</summary>
public sealed class LiveTranslationTests
{
    [Fact]
    public void ReviseAdvancesAndLatestTracksIt()
    {
        var gate = new LiveTranslationRevisionGate();
        Assert.Equal(new LiveTranslationRevision(0, 0), gate.Current);
        var first = gate.Revise();
        var second = gate.Revise();
        Assert.Equal(new LiveTranslationRevision(0, 2), second);
        Assert.True(gate.IsLatest(second));
        Assert.False(gate.IsLatest(first));
    }

    [Fact]
    public void InvalidateStartsANewGeneration()
    {
        var gate = new LiveTranslationRevisionGate();
        var stale = gate.Revise();
        gate.Invalidate();
        Assert.Equal(1, gate.Generation);
        Assert.Equal(0, gate.Value);
        Assert.False(gate.IsLatest(stale));
        Assert.False(gate.Accept(stale));
    }

    [Fact]
    public void AcceptRejectsDuplicatesAndOldRevisions()
    {
        var gate = new LiveTranslationRevisionGate();
        var first = gate.Revise();
        var second = gate.Revise();
        Assert.True(gate.Accept(second));
        Assert.False(gate.Accept(second));
        Assert.False(gate.Accept(first));
    }

    [Fact]
    public void AcceptAllowsAnIntermediateWhileANewerOneIsInFlight()
    {
        var gate = new LiveTranslationRevisionGate();
        var first = gate.Revise();
        var second = gate.Revise();
        // The translation of the first snapshot arrives while the second is still running.
        Assert.True(gate.Accept(first));
        Assert.True(gate.Accept(second));
        Assert.False(gate.Accept(first));
    }

    [Fact]
    public void ThrottleSendsFirstRequestThenWaitsForTheInterval()
    {
        var throttle = new LiveTranslationPartialThrottle(1.0);
        Assert.True(throttle.ShouldDispatch(10.0));
        Assert.False(throttle.ShouldDispatch(10.1));
        Assert.False(throttle.ShouldDispatch(10.99));
        Assert.True(throttle.ShouldDispatch(11.0));
        Assert.False(throttle.ShouldDispatch(11.5));
    }

    [Fact]
    public void ThrottleResetAllowsImmediateDispatch()
    {
        var throttle = new LiveTranslationPartialThrottle(5);
        Assert.True(throttle.ShouldDispatch(0));
        Assert.False(throttle.ShouldDispatch(1));
        throttle.Reset();
        Assert.True(throttle.ShouldDispatch(1.5));
    }

    [Fact]
    public void CoordinatorSendsTheFirstSnapshotAndThrottlesTheNext()
    {
        var coordinator = new LiveTranslationCoordinator(0.7);
        var first = coordinator.Observe("remote", "我们先讨论", 0.0);
        Assert.NotNull(first);
        Assert.Null(coordinator.Observe("remote", "我们先讨论缓存", 0.2));
        var second = coordinator.Observe("remote", "我们先讨论缓存一致性", 0.8);
        Assert.NotNull(second);
        // Every new snapshot is a revision, even the one the throttle held back.
        Assert.Equal(3, second!.Revision.Value);
        Assert.Equal(1, first!.Revision.Value);
    }

    [Fact]
    public void CoordinatorKeepsOnlyTheBestVisibleTranslation()
    {
        var coordinator = new LiveTranslationCoordinator(0.7);
        var first = coordinator.Observe("remote", "我们先讨论", 0.0)!;
        var second = coordinator.Observe("remote", "我们先讨论缓存一致性", 0.8)!;

        Assert.True(coordinator.Complete(second.Revision, "Let us discuss cache consistency"));
        Assert.Equal("Let us discuss cache consistency", coordinator.Translation);
        // The older request finishes late and must not overwrite the newer sentence.
        Assert.False(coordinator.Complete(first.Revision, "Let us discuss"));
        Assert.Equal("Let us discuss cache consistency", coordinator.Translation);
    }

    [Fact]
    public void CoordinatorShowsAnIntermediateResultWhileANewerOneRuns()
    {
        var coordinator = new LiveTranslationCoordinator(0.7);
        var first = coordinator.Observe("remote", "我们先讨论", 0.0)!;
        var second = coordinator.Observe("remote", "我们先讨论缓存", 0.8)!;
        Assert.True(coordinator.Complete(first.Revision, "Let us discuss"));
        Assert.Equal("Let us discuss", coordinator.Translation);
        Assert.True(coordinator.Complete(second.Revision, "Let us discuss the cache"));
        Assert.Equal("Let us discuss the cache", coordinator.Translation);
    }

    [Fact]
    public void InvalidateClearsTranslationAndDropsInflightResults()
    {
        var coordinator = new LiveTranslationCoordinator(0.7);
        var request = coordinator.Observe("remote", "我们先讨论", 0.0)!;
        coordinator.Invalidate();
        Assert.Equal("", coordinator.Translation);
        Assert.False(coordinator.Complete(request.Revision, "Let us discuss"));
        Assert.Equal("", coordinator.Translation);
    }

    [Fact]
    public void SwitchingSourceStartsANewUtterance()
    {
        var coordinator = new LiveTranslationCoordinator(0);
        var remote = coordinator.Observe("remote", "对方在说话", 0.0)!;
        coordinator.Invalidate();
        var me = coordinator.Observe("me", "I am speaking now", 1.0)!;
        Assert.NotEqual(remote.Revision.Generation, me.Revision.Generation);
        Assert.Equal("me", coordinator.Source);
        Assert.False(coordinator.Complete(remote.Revision, "对方在说话"));
        Assert.True(coordinator.Complete(me.Revision, "我正在说话"));
    }

    [Fact]
    public void EmptySnapshotsAreIgnored()
    {
        var coordinator = new LiveTranslationCoordinator(0.7);
        Assert.Null(coordinator.Observe("remote", "   ", 0.0));
        Assert.Equal("", coordinator.Translation);
    }
}
