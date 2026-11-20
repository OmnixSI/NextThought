using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of OnboardingVersionTests.swift: feature releases rearm the guide, patches do not.</summary>
public sealed class OnboardingVersionParityTests
{
    [Fact]
    public void FirstInstallAndFeatureUpgradeShowGuide()
    {
        Assert.True(OnboardingVersion.NeedsGuide("1.7.0", null));
        Assert.True(OnboardingVersion.NeedsGuide("1.8.0", "1.7"));
        Assert.True(OnboardingVersion.NeedsGuide("2.0.0", "1.9"));
        // The redesigned guide reaches existing users with the next feature release.
        Assert.True(OnboardingVersion.NeedsGuide("2.2.0", "2.1"));
    }

    [Fact]
    public void PatchUpdateDoesNotRepeatCompletedGuide()
    {
        Assert.False(OnboardingVersion.NeedsGuide("1.7.2", "1.7"));
        Assert.False(OnboardingVersion.NeedsGuide("2.1.4", "2.1"));
    }

    [Fact]
    public void FeatureReleaseOrNewLayoutRearmsTheGuideOnce()
    {
        Assert.True(OnboardingVersion.IsFeatureUpgrade("3.0.0", null, null, 2));
        Assert.True(OnboardingVersion.IsFeatureUpgrade("3.1.0", "3.0", 2, 2));
        Assert.True(OnboardingVersion.IsFeatureUpgrade("3.0.0", "3.0", 1, 2));
        Assert.False(OnboardingVersion.IsFeatureUpgrade("3.0.2", "3.0", 2, 2));
    }

    [Fact]
    public void BackupKeepsExpertPreferenceButNeverSkipsNewInstallGuide()
    {
        var snapshot = SettingsSnapshot.Filter(new Dictionary<string, object?>
        {
            ["advancedSettingsEnabled"] = true,
            ["onboardingGuideCompletedVersion"] = "1.7",
            ["onboardingCompleted"] = true
        });
        Assert.Equal(true, snapshot["advancedSettingsEnabled"]);
        Assert.False(snapshot.ContainsKey("onboardingGuideCompletedVersion"));
        Assert.False(snapshot.ContainsKey("onboardingCompleted"));
    }
}
