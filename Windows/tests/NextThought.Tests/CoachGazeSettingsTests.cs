using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E10: the 留意眼神 settings are Mac preferences (coachWatchesGaze / coachGazeSensitivity) with the
/// Mac defaults (off / standard), the calibrated pose is a Windows sidecar rather than a Mac key,
/// and the settings file keeps Mac key names like every other preference.
/// </summary>
public sealed class CoachGazeSettingsTests
{
    private static AppPaths TempPaths() =>
        new(Path.Combine(Path.GetTempPath(), "nt-coach-gaze-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void CoachGazeDefaultsAreOffAndStandard()
    {
        var store = new SettingsStore(TempPaths());
        var settings = store.Load();
        Assert.False(settings.CoachWatchesGaze);
        Assert.Equal("standard", settings.CoachGazeSensitivity);
        Assert.Equal(false, store.LoadMacValue("coachWatchesGaze"));
        Assert.Equal("standard", store.LoadMacValue("coachGazeSensitivity"));
    }

    [Fact]
    public async Task CoachGazeSettingsPersistUnderTheMacKeys()
    {
        var paths = TempPaths();
        var store = new SettingsStore(paths);
        await store.SaveMacPreferencesAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["coachWatchesGaze"] = true,
            ["coachGazeSensitivity"] = "strict"
        });

        var settings = store.Load();
        Assert.True(settings.CoachWatchesGaze);
        Assert.Equal("strict", settings.CoachGazeSensitivity);
        Assert.Equal(true, store.LoadMacValue("coachWatchesGaze"));
        Assert.Equal("strict", store.LoadMacValue("coachGazeSensitivity"));

        var json = File.ReadAllText(paths.Settings);
        Assert.Contains("\"coachWatchesGaze\"", json, StringComparison.Ordinal);
        Assert.Contains("\"coachGazeSensitivity\"", json, StringComparison.Ordinal);
        // The document uses the Mac key names, never the C# property spellings.
        Assert.DoesNotContain("\"CoachWatchesGaze\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CoachGazeSensitivity\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GazeBaselineSidecarRoundTrips()
    {
        var paths = TempPaths();
        var store = new GazeBaselineStore(paths);
        Assert.Null(store.Load());

        store.Save(new GazeSignature(0.1, -0.2, 0.3, 0.4));
        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(0.1, loaded!.X, 9);
        Assert.Equal(-0.2, loaded.Y, 9);
        Assert.Equal(0.3, loaded.Yaw, 9);
        Assert.Equal(0.4, loaded.Pitch, 9);
        Assert.True(File.Exists(Path.Combine(paths.Root, "WindowsState", "coach-gaze-baseline.json")));
        // The pose is Windows state, not a Mac settings key, so settings.json stays untouched.
        Assert.False(File.Exists(paths.Settings));
    }
}
