using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §5.6: every icon codepoint the UI uses must exist in the bundled icon font. The check reads the
/// real FluentSystemIcons cmap (formats 4 and 12) and compares it with Icons.axaml and with every
/// literal glyph codepoint in the XAML/code, plus it makes sure every {StaticResource IconXxx} is a
/// defined icon resource. The page-turn arrows had been rendered as missing-glyph boxes, so this
/// regression check is part of the verification chain.
/// </summary>
public sealed class IconGlyphCoverageTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");

    private static string Font => Path.Combine(Desktop, "Assets", "Fonts", "FluentSystemIcons-Regular.ttf");

    private static HashSet<int> IconCodepoints()
    {
        var xaml = File.ReadAllText(Path.Combine(Desktop, "Styles", "Icons.axaml"));
        var result = new HashSet<int>();
        foreach (Match match in Regex.Matches(xaml, "&#x([0-9A-Fa-f]{4,6});"))
            result.Add(Convert.ToInt32(match.Groups[1].Value, 16));
        return result;
    }

    [Fact]
    public void EveryIconCodepointExistsInTheBundledIconFont()
    {
        var path = Font;
        Assert.True(File.Exists(path), "missing icon font: " + path);
        var cmap = ReadCmap(path);
        Assert.True(cmap.Count > 1000, "cmap parser read too few glyphs: " + cmap.Count);

        var icons = File.ReadAllText(Path.Combine(Desktop, "Styles", "Icons.axaml"));
        var failures = new List<string>();
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(icons, "x:Key=\"(Icon[A-Za-z0-9]+)\">&#x([0-9A-Fa-f]{4,6});"))
        {
            var name = match.Groups[1].Value;
            var code = Convert.ToInt32(match.Groups[2].Value, 16);
            defined.Add(name);
            if (!cmap.Contains(code)) failures.Add($"{name}: U+{code:X4} is not in the icon font");
        }
        Assert.True(defined.Count > 20, "too few icon resources parsed: " + defined.Count);

        // Every literal glyph used anywhere in the desktop sources must exist in the font too.
        foreach (var file in DesktopSources())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, "&#x([0-9A-Fa-f]{4,6});"))
            {
                var code = Convert.ToInt32(match.Groups[1].Value, 16);
                if (!cmap.Contains(code))
                    failures.Add($"{Path.GetFileName(file)}: U+{code:X4} is not in the icon font");
            }
        }

        // Every referenced icon resource must exist in Icons.axaml (IconFont is the family itself).
        foreach (var file in DesktopSources().Where(path => path.EndsWith(".axaml", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, "StaticResource (Icon[A-Za-z0-9]+)"))
            {
                var name = match.Groups[1].Value;
                if (name == "IconFont") continue;
                if (!defined.Contains(name))
                    failures.Add($"{Path.GetFileName(file)}: {name} is not defined in Icons.axaml");
            }
        }

        Assert.True(failures.Count == 0, "图标码点缺字（§5.6）：\n" + string.Join("\n", failures));
    }

    private static IEnumerable<string> DesktopSources()
    {
        foreach (var path in Directory.GetFiles(Desktop, "*.*", SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".axaml", StringComparison.Ordinal) && !path.EndsWith(".cs", StringComparison.Ordinal))
                continue;
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            yield return path;
        }
    }

    /// <summary>Reads the unicode codepoints covered by a TrueType 'cmap' table (formats 4 and 12).</summary>
    private static HashSet<int> ReadCmap(string path)
    {
        var data = File.ReadAllBytes(path);
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
        var cmapOffset = -1;
        for (var index = 0; index < tableCount; index++)
        {
            var record = 12 + index * 16;
            var tag = Encoding.ASCII.GetString(data, record, 4);
            if (tag != "cmap") continue;
            cmapOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8, 4));
            break;
        }
        Assert.True(cmapOffset >= 0, "font has no cmap table");
        var subtableCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmapOffset + 2, 2));
        var codepoints = new HashSet<int>();
        for (var index = 0; index < subtableCount; index++)
        {
            var encodingRecord = cmapOffset + 4 + index * 8;
            var subtable = cmapOffset + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(encodingRecord + 4, 4));
            var format = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(subtable, 2));
            if (format == 4) ReadFormat4(data, subtable, codepoints);
            else if (format == 12) ReadFormat12(data, subtable, codepoints);
        }
        return codepoints;
    }

    private static void ReadFormat4(byte[] data, int start, HashSet<int> codepoints)
    {
        var segmentCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start + 6, 2)) / 2;
        var endCodes = start + 14;
        var startCodes = endCodes + segmentCount * 2 + 2;
        var deltas = startCodes + segmentCount * 2;
        var rangeOffsets = deltas + segmentCount * 2;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var end = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(endCodes + segment * 2, 2));
            var first = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(startCodes + segment * 2, 2));
            var delta = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(deltas + segment * 2, 2));
            var rangeOffset = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(rangeOffsets + segment * 2, 2));
            if (first == 0xFFFF) continue;
            for (var code = first; code <= end && code < 0xFFFE; code++)
            {
                int glyph;
                if (rangeOffset == 0) glyph = (code + delta) & 0xFFFF;
                else
                {
                    var glyphAddress = rangeOffsets + segment * 2 + rangeOffset + (code - first) * 2;
                    if (glyphAddress + 2 > data.Length) continue;
                    glyph = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(glyphAddress, 2));
                    if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                }
                if (glyph != 0) codepoints.Add(code);
            }
        }
    }

    private static void ReadFormat12(byte[] data, int start, HashSet<int> codepoints)
    {
        var groupCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 12, 4));
        for (var group = 0; group < groupCount; group++)
        {
            var record = start + 16 + group * 12;
            var first = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record, 4));
            var last = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 4, 4));
            for (var code = first; code <= last && code - first < 70000; code++) codepoints.Add(code);
        }
    }
}
