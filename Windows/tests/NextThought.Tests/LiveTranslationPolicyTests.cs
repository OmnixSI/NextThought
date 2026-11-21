using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E11: the live-translation direction detector and the "hear English, turn interpretation on"
/// policy (Mac turnOnForForeignSpeech).
/// </summary>
public sealed class LiveTranslationPolicyTests
{
    [Fact]
    public void DirectionFollowsTheScriptOfTheUtterance()
    {
        Assert.Equal(LiveTranslationDirection.ChineseToEnglish, LiveTranslationDirectionDetector.Detect("我们先确认一下需求"));
        Assert.Equal(LiveTranslationDirection.EnglishToChinese, LiveTranslationDirectionDetector.Detect("Let us confirm the requirement first"));
        Assert.Null(LiveTranslationDirectionDetector.Detect("   "));
        Assert.Null(LiveTranslationDirectionDetector.Detect("12345"));
    }

    [Fact]
    public void MixedSpeechIsDecidedByTheDominantScript()
    {
        Assert.Equal(LiveTranslationDirection.ChineseToEnglish,
            LiveTranslationDirectionDetector.Detect("这个 API 的 latency 还可以，但我们需要观察一周"));
        Assert.Equal(LiveTranslationDirection.EnglishToChinese,
            LiveTranslationDirectionDetector.Detect("The deployment pipeline is slow because the staging cluster has too few nodes"));
    }

    [Fact]
    public void EnglishWordGateMatchesMacFourWordRule()
    {
        Assert.False(LiveTranslationDirectionDetector.HasEnoughEnglishWords("yes okay"));
        Assert.True(LiveTranslationDirectionDetector.HasEnoughEnglishWords("we should check this tomorrow"));
    }

    [Fact]
    public void EnglishFromTheOtherSideTurnsItOnOnce()
    {
        var policy = new LiveTranslationAutoOpen();
        policy.BeginMeeting(null);

        Assert.True(policy.ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: false, uiIsChinese: true, packsInstalled: true));
        // once per meeting, and never when the user already chose a state
        Assert.False(policy.ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: false, uiIsChinese: true, packsInstalled: true));

        var chosen = new LiveTranslationAutoOpen();
        chosen.BeginMeeting(false);
        Assert.False(chosen.ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: false, uiIsChinese: true, packsInstalled: true));
    }

    [Fact]
    public void ItWaitsForChineseUiAndInstalledPacks()
    {
        Assert.False(new LiveTranslationAutoOpen().ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: false, uiIsChinese: false, packsInstalled: true));
        Assert.False(new LiveTranslationAutoOpen().ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: false, uiIsChinese: true, packsInstalled: false));
        Assert.False(new LiveTranslationAutoOpen().ShouldTurnOn("我们先确认需求再决定",
            enabled: false, uiIsChinese: true, packsInstalled: true));
    }

    [Fact]
    public void TheRememberedChoiceSurvivesTheNextMeeting()
    {
        var policy = new LiveTranslationAutoOpen();
        policy.Remember(true);
        policy.BeginMeeting(policy.Choice);
        Assert.True(policy.Choice);
        Assert.False(policy.ShouldTurnOn("Could you explain the deployment pipeline again",
            enabled: true, uiIsChinese: true, packsInstalled: true));

        policy.Remember(false);
        policy.BeginMeeting(policy.Choice);
        Assert.False(policy.Choice);
    }
}
