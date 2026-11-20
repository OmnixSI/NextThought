using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>E26b: cueSurface / presenterSlideNotes keep the Mac keys, defaults and allowed values.</summary>
public sealed class CuePresentationSettingsTests
{
    [Fact]
    public void TheMacDefaultsAreTheSame()
    {
        var settings = new AppSettings();
        Assert.Equal("island", settings.CueSurface);
        Assert.True(settings.PresenterSlideNotes);
        Assert.True(MacPreferenceMap.IsMapped("cueSurface"));
        Assert.True(MacPreferenceMap.IsMapped("presenterSlideNotes"));
    }

    [Fact]
    public void TheKeysRoundTripAndRejectUnknownValues()
    {
        var settings = new AppSettings();
        MacPreferenceMap.Apply(settings, "cueSurface", "window");
        MacPreferenceMap.Apply(settings, "presenterSlideNotes", false);
        Assert.Equal("window", settings.CueSurface);
        Assert.False(settings.PresenterSlideNotes);
        Assert.Equal("window", MacPreferenceMap.Read(settings, "cueSurface"));
        Assert.Equal(false, MacPreferenceMap.Read(settings, "presenterSlideNotes"));
        MacPreferenceMap.Apply(settings, "cueSurface", "popover");
        // Mac normalisation turns an unknown value into the registered default.
        Assert.Equal("island", settings.CueSurface);
    }
}
