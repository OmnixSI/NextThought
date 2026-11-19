using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of OnboardingFlowTests.swift.</summary>
public sealed class OnboardingFlowTests
{
    [Fact]
    public void StepsRunInTheDesignedOrder()
    {
        Assert.Equal([OnboardingStep.Scene, OnboardingStep.Sound, OnboardingStep.Answers, OnboardingStep.Start],
            Enum.GetValues<OnboardingStep>());
        Assert.Equal(4, OnboardingSteps.Count);
        Assert.Equal(0, (int)OnboardingStep.Scene);
        Assert.Equal(2, (int)OnboardingStep.Answers);
        Assert.Equal(3, (int)OnboardingStep.Start);
    }

    [Fact]
    public void NextAndPreviousStopAtTheEnds()
    {
        Assert.Null(OnboardingStep.Scene.Previous());
        Assert.Equal(OnboardingStep.Sound, OnboardingStep.Scene.Next());
        Assert.Null(OnboardingStep.Start.Next());
        Assert.Equal(OnboardingStep.Answers, OnboardingStep.Start.Previous());
        Assert.True(OnboardingStep.Start.IsLast());
        Assert.False(OnboardingStep.Answers.IsLast());
        Assert.Equal(2, OnboardingStep.Sound.Position());
    }

    [Fact]
    public void StoredStepIsClampedIntoRange()
    {
        Assert.Equal(OnboardingStep.Scene, OnboardingSteps.Clamped(-4));
        Assert.Equal(OnboardingStep.Answers, OnboardingSteps.Clamped(2));
        Assert.Equal(OnboardingStep.Start, OnboardingSteps.Clamped(3));
        Assert.Equal(OnboardingStep.Start, OnboardingSteps.Clamped(99));
    }

    [Fact]
    public void OnlyTheAnswerServiceCanBeSkipped()
    {
        Assert.Equal([OnboardingStep.Answers], Enum.GetValues<OnboardingStep>().Where(step => step.IsSkippable()));
    }

    [Fact]
    public void EveryStepHasATimeEstimate()
    {
        Assert.All(Enum.GetValues<OnboardingStep>(), step => Assert.True(step.EstimatedSeconds() > 0));
    }

    [Fact]
    public void ResumeNamesOnlyWhatIsStillOpen()
    {
        Assert.Equal([OnboardingPending.Sound, OnboardingPending.Answers], OnboardingPendings.Open(false, false));
        Assert.Equal([OnboardingPending.Answers], OnboardingPendings.Open(true, false));
        Assert.Equal([OnboardingPending.Sound], OnboardingPendings.Open(false, true));
        Assert.Empty(OnboardingPendings.Open(true, true));
    }
}
