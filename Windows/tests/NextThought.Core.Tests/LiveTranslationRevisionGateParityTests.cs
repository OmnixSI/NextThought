using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of LiveTranslationRevisionGateTests.swift (5 Swift Testing tests): continuous recognition
/// keeps dispatching partial translations, and an older in-flight translation can never replace a
/// newer visible one.
/// </summary>
public sealed class LiveTranslationRevisionGateParityTests
{
    [Fact]
    public void ContinuousRecognitionStillDispatchesPartialTranslations()
    {
        var throttle = new LiveTranslationPartialThrottle(0.5);

        var first = throttle.ShouldDispatch(0.0);
        var beforeInterval = throttle.ShouldDispatch(0.2);
        var stillBeforeInterval = throttle.ShouldDispatch(0.4);
        var atInterval = throttle.ShouldDispatch(0.5);
        var afterResetInterval = throttle.ShouldDispatch(0.7);
        var nextInterval = throttle.ShouldDispatch(1.0);

        Assert.True(first);
        Assert.False(beforeInterval);
        Assert.False(stillBeforeInterval);
        Assert.True(atInterval);
        Assert.False(afterResetInterval);
        Assert.True(nextInterval);
    }

    [Fact]
    public void FirstPartialCanDispatchImmediatelyAfterAnUtteranceReset()
    {
        var throttle = new LiveTranslationPartialThrottle(0.5);
        var first = throttle.ShouldDispatch(0.0);
        var throttled = throttle.ShouldDispatch(0.1);
        throttle.Reset();
        var afterReset = throttle.ShouldDispatch(0.2);

        Assert.True(first);
        Assert.False(throttled);
        Assert.True(afterReset);
    }

    [Fact]
    public void PartialTranslationCanAppearWhileRecognitionKeepsUpdating()
    {
        var gate = new LiveTranslationRevisionGate();
        var first = gate.Revise();
        var second = gate.Revise();
        var acceptedFirst = gate.Accept(first);
        var acceptedSecond = gate.Accept(second);

        Assert.False(gate.IsLatest(first));
        Assert.True(gate.IsLatest(second));
        Assert.True(acceptedFirst);
        Assert.True(acceptedSecond);
    }

    [Fact]
    public void OlderTranslationCannotReplaceANewerResult()
    {
        var gate = new LiveTranslationRevisionGate();
        var first = gate.Revise();
        var second = gate.Revise();
        var acceptedSecond = gate.Accept(second);
        var acceptedFirst = gate.Accept(first);

        Assert.True(acceptedSecond);
        Assert.False(acceptedFirst);
    }

    [Fact]
    public void ClearingAnUtteranceRejectsItsLateTranslation()
    {
        var gate = new LiveTranslationRevisionGate();
        var old = gate.Revise();
        gate.Invalidate();
        var current = gate.Revise();
        var acceptedOld = gate.Accept(old);
        var acceptedCurrent = gate.Accept(current);

        Assert.False(acceptedOld);
        Assert.True(acceptedCurrent);
    }
}
