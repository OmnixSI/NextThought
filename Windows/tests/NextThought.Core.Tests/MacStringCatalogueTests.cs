using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G10 §6.8: <c>UiTextMac.g.cs</c> is generated from the 3.4.0 Mac catalogue
/// (<c>Resources/Localization/Localizable.xcstrings</c>) and must carry exactly one row per catalogue
/// string, so the Windows table cannot silently drift when the Mac side adds or removes a sentence.
/// </summary>
public sealed class MacStringCatalogueTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Resources", "Localization", "Localizable.xcstrings")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static IReadOnlyDictionary<string, string> Catalogue()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Root(), "Resources", "Localization", "Localizable.xcstrings")));
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.GetProperty("strings").EnumerateObject())
            rows[entry.Name] = English(entry.Value);
        return rows;
    }

    /// <summary>The same lookup the generator uses: stringUnit, else the plural "other" form.</summary>
    private static string English(JsonElement entry)
    {
        if (!entry.TryGetProperty("localizations", out var localizations)
            || !localizations.TryGetProperty("en", out var english))
            return "";
        if (english.TryGetProperty("stringUnit", out var unit)
            && unit.TryGetProperty("value", out var value))
            return value.GetString() ?? "";
        if (english.TryGetProperty("variations", out var variations))
        {
            foreach (var category in new[] { "plural", "device" })
            {
                if (!variations.TryGetProperty(category, out var forms)) continue;
                foreach (var key in new[] { "other", "one" })
                    if (forms.TryGetProperty(key, out var form)
                        && form.TryGetProperty("stringUnit", out var formUnit)
                        && formUnit.TryGetProperty("value", out var formValue))
                        return formValue.GetString() ?? "";
            }
        }
        return "";
    }

    [Fact]
    public void TheCatalogueHasNoUntranslatedEntry()
    {
        var catalogue = Catalogue();
        Assert.NotEmpty(catalogue);
        var missing = catalogue.Where(pair => pair.Value.Length == 0).Select(pair => pair.Key).ToArray();
        Assert.True(missing.Length == 0, "catalogue entries without English: " + string.Join(", ", missing.Take(10)));
    }

    [Fact]
    public void TheGeneratedTableHasOneRowPerCatalogueString()
    {
        var catalogue = Catalogue();
        Assert.Equal(catalogue.Count, UiText.MacStringCount);
    }

    [Fact]
    public void EveryCatalogueKeyAndEnglishValueIsInTheGeneratedTable()
    {
        var catalogue = Catalogue();
        var missing = new List<string>();
        var wrong = new List<string>();
        foreach (var (key, value) in catalogue)
        {
            if (!UiText.IsMacString(key)) missing.Add(key);
            // A Windows-only replacement may deliberately give the same key different English wording;
            // those rows are recorded in WORDING.md and covered by WordingTableTests.
            else if (!UiText.WindowsStrings.ContainsKey(key)
                     && UiText.EnglishStrings.TryGetValue(key, out var generated) && generated != value)
                wrong.Add(key);
        }
        Assert.True(missing.Count == 0, "keys missing from UiTextMac.g.cs: " + string.Join(", ", missing.Take(10)));
        Assert.True(wrong.Count == 0, "English values differ from the Mac catalogue: " + string.Join(", ", wrong.Take(10)));
    }
}
