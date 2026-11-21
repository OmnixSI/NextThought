using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E25 / decisions §3.1 zoom 1 and §3.6 settings-structure: <c>textZoom</c> is part of the 114-key
/// Mac preference registry and <c>recordSessions</c> is a typed setting with the Mac default and
/// behaviour consumer; both round-trip through settings.json under the Mac key names.
/// </summary>
public sealed class RecordSessionsAndTextZoomPreferenceTests
{
    private static string TemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-e25-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void RecordSessionsHasTheMacKeyDefaultAndMapping()
    {
        var preference = MacPreferences.All.Single(item => item.Key == "recordSessions");
        Assert.Equal(MacPreferenceKind.Bool, preference.Kind);
        Assert.Equal(true, preference.Default);
        Assert.True(MacPreferenceMap.IsMapped("recordSessions"));
        Assert.Equal(true, MacPreferenceMap.Read(new AppSettings(), "recordSessions"));
        Assert.Contains("recordSessions", MacPreferenceMap.MappedSettingsJsonNames);
    }

    [Fact]
    public void TextZoomIsMappedInThePreferenceRegistry()
    {
        var preference = MacPreferences.All.Single(item => item.Key == "textZoom");
        Assert.Equal(MacPreferenceKind.Real, preference.Kind);
        Assert.Equal(1.0, preference.Default);
        Assert.True(MacPreferenceMap.IsMapped("textZoom"));
        Assert.Equal(1.0, MacPreferenceMap.Read(new AppSettings(), "textZoom"));
        var settings = new AppSettings { TextZoom = 1.2 };
        var json = MacPreferenceMap.ToJsonObject(settings);
        Assert.Equal(1.2, json["textZoom"]!.GetValue<double>());
    }

}
