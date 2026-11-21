using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E23: the document-reading settings keep the Mac keys, defaults and allowed values
/// (DocumentModelSettings.swift / DocumentRuntimeInstaller.swift).
/// </summary>
public sealed class DocumentSettingsTests
{
    [Fact]
    public void TheMacDefaultsAreTheSame()
    {
        var settings = new AppSettings();
        Assert.Equal("system", settings.DocumentOcrProvider);
        Assert.False(settings.DocumentProviderWasSelected);
        Assert.Equal("paddleocr-vl-1-6-mlx-8bit", settings.LocalOcrModelId);
        Assert.True(settings.OcrFallbackToVision);
        Assert.Equal("gpt-6-luna", settings.CodexOcrModel);
        Assert.Equal("medium", settings.CodexOcrEffort);
        Assert.Equal(600, settings.CodexOcrTimeout);
        foreach (var key in new[]
                 {
                     "documentOCRProvider", "documentProviderWasSelected", "localOCRModelID",
                     "ocrFallbackToVision", "codexOCRModel", "codexOCREffort", "codexOCRTimeout"
                 })
            Assert.True(MacPreferenceMap.IsMapped(key), "未接入的 Mac 键：" + key);
    }

    [Fact]
    public void TheMappingRoundTripsTheMacKeys()
    {
        var settings = new AppSettings();
        foreach (var (key, value) in new (string Key, object? Value)[]
                 {
                     ("documentOCRProvider", "paddle"),
                     ("documentProviderWasSelected", true),
                     ("localOCRModelID", "paddleocr-vl-1-6-native"),
                     ("ocrFallbackToVision", false),
                     ("codexOCRModel", "gpt-6-luna"),
                     ("codexOCREffort", "xhigh"),
                     ("codexOCRTimeout", 900.0)
                 })
            MacPreferenceMap.Apply(settings, key, value);
        Assert.Equal("paddle", settings.DocumentOcrProvider);
        Assert.True(settings.DocumentProviderWasSelected);
        Assert.False(settings.OcrFallbackToVision);
        Assert.Equal("xhigh", settings.CodexOcrEffort);
        Assert.Equal(900, settings.CodexOcrTimeout);
        Assert.Equal("paddle", MacPreferenceMap.Read(settings, "documentOCRProvider"));
        Assert.Equal(900.0, MacPreferenceMap.Read(settings, "codexOCRTimeout"));
    }

    [Fact]
    public void InvalidValuesKeepTheMacAllowedValues()
    {
        var settings = new AppSettings();
        MacPreferenceMap.Apply(settings, "documentOCRProvider", "some-unknown-reader");
        Assert.Equal("system", settings.DocumentOcrProvider);
        MacPreferenceMap.Apply(settings, "codexOCREffort", "extreme");
        Assert.Equal("medium", settings.CodexOcrEffort);
        MacPreferenceMap.Apply(settings, "codexOCRTimeout", 5.0);
        Assert.Equal(30, settings.CodexOcrTimeout);
        MacPreferenceMap.Apply(settings, "codexOCRTimeout", 99999.0);
        Assert.Equal(3600, settings.CodexOcrTimeout);
        // An unparsable value falls back to the registered Mac default.
        MacPreferenceMap.Apply(settings, "codexOCRTimeout", "not-a-number");
        Assert.Equal(600, settings.CodexOcrTimeout);
    }

    [Fact]
    public void TheMappedJsonNamesCoverTheDocumentKeys()
    {
        foreach (var name in new[]
                 {
                     "documentOcrProvider", "documentProviderWasSelected", "localOcrModelId",
                     "ocrFallbackToVision", "codexOcrModel", "codexOcrEffort", "codexOcrTimeout"
                 })
            Assert.Contains(name, MacPreferenceMap.MappedSettingsJsonNames);
    }
}
