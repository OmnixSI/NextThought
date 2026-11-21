using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D7: every icon in the shell comes from Styles/Icons.axaml (Fluent System Icons, bundled and
/// MIT). The tests keep views from going back to literal glyph codepoints and keep the icon table,
/// the views and ICON_MAP.md in sync.
/// </summary>
public sealed class IconResourceTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Styles", "Icons.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop() => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");

    private static string IconsAxaml() => File.ReadAllText(Path.Combine(Desktop(), "Styles", "Icons.axaml"));

    private static string IconMap() => File.ReadAllText(Path.Combine(Root(), "Windows", "ICON_MAP.md"));

    private static IEnumerable<string> ViewSources(string extension) =>
        Directory.GetFiles(Path.Combine(Desktop(), "Views"), "*" + extension, SearchOption.AllDirectories);

    private static Dictionary<string, string> IconKeys()
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(IconsAxaml(), "<x:String x:Key=\"(Icon[A-Za-z0-9]+)\">&#x([0-9A-Fa-f]+);</x:String>"))
            entries[match.Groups[1].Value] = match.Groups[2].Value.ToUpperInvariant();
        return entries;
    }

    [Fact]
    public void IconFontIsTheBundledFluentSystemIconsFont()
    {
        var icons = IconsAxaml();
        Assert.Contains("avares://NextThought/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular", icons);
        Assert.Contains("Segoe Fluent Icons", icons); // documented fallback for unpacked builds
        var fontPath = Path.Combine(Desktop(), "Assets", "Fonts", "FluentSystemIcons-Regular.ttf");
        Assert.True(File.Exists(fontPath), "the bundled Fluent icon font is missing");
        Assert.True(new FileInfo(fontPath).Length > 100_000);
        Assert.True(File.Exists(Path.Combine(Desktop(), "Assets", "Fonts", "LICENSE-FluentSystemIcons.txt")));
        var notices = File.ReadAllText(Path.Combine(Root(), "Windows", "THIRD_PARTY_NOTICES.txt"));
        Assert.Contains("Fluent System Icons", notices);
    }

    [Fact]
    public void ViewsUseIconResourcesInsteadOfGlyphLiterals()
    {
        var usages = 0;
        foreach (var file in ViewSources(".axaml"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("&#xE", text);
            Assert.DoesNotContain("Segoe Fluent Icons", text);
            Assert.DoesNotContain("Segoe MDL2", text);
            usages += Regex.Matches(text, @"\{StaticResource Icon[A-Za-z0-9]+\}").Count;
        }
        foreach (var file in ViewSources(".cs"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"\\uE[0-9A-Fa-f]{3}", text);
            Assert.DoesNotContain("Segoe Fluent Icons", text);
            Assert.DoesNotContain("Segoe MDL2", text);
        }
        Assert.True(usages >= 30, "views should reference at least 30 icon resources, found " + usages);
    }

    [Fact]
    public void EveryIconResourceIsUsedOrDocumentedAsUnused()
    {
        var keys = IconKeys();
        Assert.True(keys.Count >= 57, "the icon table should cover the 57 Swift symbols plus extras");
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ViewSources(".axaml").Concat(ViewSources(".cs")))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"StaticResource (Icon[A-Za-z0-9]+)|""(Icon[A-Za-z0-9]+)"""))
                referenced.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        referenced.Remove("IconFont");
        var unusedSection = IconMap()[IconMap().IndexOf("## 暂未使用", StringComparison.Ordinal)..];
        foreach (var key in keys.Keys)
        {
            if (referenced.Contains(key)) continue;
            Assert.Contains("`" + key + "`", unusedSection);
        }
        Assert.All(referenced, key => Assert.True(keys.ContainsKey(key), "unknown icon resource referenced: " + key));
    }

    [Fact]
    public void IconMapListsTheSameFluentNamesAndCodepointsAsTheResourceTable()
    {
        var keys = IconKeys();
        var map = IconMap();
        var documented = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(map, @"\|\s*`?U\+([0-9A-F]{4,})`?\s*\|\s*`(Icon[A-Za-z0-9]+)`\s*\|"))
            documented[match.Groups[2].Value] = match.Groups[1].Value.ToUpperInvariant();
        foreach (var (key, codepoint) in keys)
        {
            Assert.True(documented.TryGetValue(key, out var mapped), "ICON_MAP.md is missing " + key);
            Assert.Equal(codepoint, mapped);
        }
        // Every Image(systemName:) symbol in the Mac sources has a row in the map (old test also
        // checks the symbol names; this one checks the Fluent name column is filled).
        var swiftSymbols = Directory.GetFiles(Path.Combine(Root(), "Sources", "NextThought"), "*.swift", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "systemName: \"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
            .Distinct().ToList();
        Assert.True(swiftSymbols.Count >= 50);
        Assert.All(swiftSymbols, symbol => Assert.Contains("`" + symbol + "`", map));
        Assert.Equal(swiftSymbols.Count, Regex.Matches(map, @"^\| `[^`]+` \| `ic_fluent_[a-z0-9_]+` \|", RegexOptions.Multiline).Count);
    }

    [Fact]
    public void SceneGlyphUsesTheSharedIconResources()
    {
        var glyph = File.ReadAllText(Path.Combine(Desktop(), "Views", "Controls", "SceneGlyph.cs"));
        foreach (var key in new[] { "IconPersonCropRectangle", "IconFlask", "IconGraduationcap", "IconPerson3",
                     "IconPerson2", "IconBriefcase", "IconBook", "IconBubbleLeftAndBubbleRight", "IconSliderHorizontal3" })
            Assert.Contains(key, glyph);
        Assert.Contains("Lookup(\"IconFont\")", glyph);
        Assert.DoesNotContain("Segoe", glyph);
    }
}
