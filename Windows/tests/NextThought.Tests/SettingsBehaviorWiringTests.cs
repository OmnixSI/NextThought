using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// G5b: the Mac keys whose Windows consumer already existed are wired to real behaviour:
/// <c>reducedEffects</c> drives the motion tokens and <c>teleprompterOpacity</c> /
/// <c>teleprompterFontSize</c> are the cue window's opacity/font size with Mac defaults and ranges.
/// </summary>
public sealed class SettingsBehaviorWiringTests
{
    [Fact]
    public void MotionReductionCombinesTheSystemAndAppSettings()
    {
        // System "show animations" off reduces; the app's 减少视觉特效 setting reduces too.
        Assert.True(MotionPlan.ShouldReduce(true, false));
        Assert.True(MotionPlan.ShouldReduce(false, true));
        Assert.True(MotionPlan.ShouldReduce(true, true));
        Assert.False(MotionPlan.ShouldReduce(false, false));
        // Reduced motion means zero duration for every token (nothing animates).
        foreach (var kind in Enum.GetValues<MotionKind>())
        {
            Assert.Equal(TimeSpan.Zero, MotionPlan.Duration(kind, MotionPlan.ShouldReduce(false, true)));
            Assert.Equal(1.0, MotionPlan.Progress(kind, TimeSpan.Zero, MotionPlan.ShouldReduce(false, true)));
        }
    }

    [Fact]
    public void ReducedEffectsIsTheMacDefaultOffAndRoundTrips()
    {
        var defaults = new AppSettings();
        Assert.False(defaults.ReducedEffects);
        Assert.Equal(false, MacPreferences.Default("reducedEffects"));
        Assert.Equal(false, MacPreferenceMap.ReadAll(defaults)["reducedEffects"]);
    }

    [Fact]
    public async Task TeleprompterOpacityAndFontSizeUseTheMacDefaultsRangesAndSettingsStore()
    {
        var defaults = new AppSettings();
        Assert.Equal(1.0, defaults.TeleprompterOpacity);
        Assert.Equal(22.0, defaults.TeleprompterFontSize);
        Assert.Equal(1.0, (double)MacPreferences.Default("teleprompterOpacity")!);
        Assert.Equal(22.0, (double)MacPreferences.Default("teleprompterFontSize")!);
        // Mac slider ranges: opacity 0.35–1, font size 14–36.
        Assert.Equal(1.0, MacPreferences.Normalize("teleprompterOpacity", 4.0));
        Assert.Equal(0.35, MacPreferences.Normalize("teleprompterOpacity", 0.0));
        Assert.Equal(36.0, MacPreferences.Normalize("teleprompterFontSize", 99.0));
        Assert.Equal(14.0, MacPreferences.Normalize("teleprompterFontSize", 1.0));

        var root = Path.Combine(Path.GetTempPath(), "nt-g5b-teleprompter-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(new AppPaths(root));
            await store.SaveMacPreferencesAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["teleprompterOpacity"] = 0.5,
                ["teleprompterFontSize"] = 28.0,
                ["reducedEffects"] = true
            });
            var settings = store.Load();
            Assert.Equal(0.5, settings.TeleprompterOpacity);
            Assert.Equal(28.0, settings.TeleprompterFontSize);
            Assert.True(settings.ReducedEffects);
            var values = store.LoadMacPreferences();
            Assert.Equal(0.5, (double)values["teleprompterOpacity"]!);
            Assert.Equal(28.0, (double)values["teleprompterFontSize"]!);
            Assert.Equal(true, values["reducedEffects"]);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void SettingsJsonUsesTheMacNamesForTheNewlyWiredKeys()
    {
        var node = MacPreferenceMap.ToJsonObject(new AppSettings
        {
            TeleprompterOpacity = 0.8,
            TeleprompterFontSize = 30,
            ReducedEffects = true
        });
        Assert.True(node.ContainsKey("teleprompterOpacity"));
        Assert.True(node.ContainsKey("teleprompterFontSize"));
        Assert.True(node.ContainsKey("reducedEffects"));
        Assert.False(node.ContainsKey("teleprompterOpacityMirror"));
        Assert.Equal(0.8, node["teleprompterOpacity"]!.GetValue<double>());
        Assert.Equal(30.0, node["teleprompterFontSize"]!.GetValue<double>());
        Assert.True(node["reducedEffects"]!.GetValue<bool>());
    }
}
