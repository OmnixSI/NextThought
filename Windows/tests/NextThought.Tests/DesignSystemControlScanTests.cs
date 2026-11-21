using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §5.6: every control on screen must come from the design system. The pages are XAML, so the
/// check walks the committed XAML instead of a live control tree (the test project has no desktop
/// reference): a control type is acceptable when Styles/Controls.axaml gives it a default style, or
/// when the element carries a class that has a style selector. Expander has no design-system
/// component at all, so any occurrence fails as well (the meeting page used it for the two
/// dropdown-looking sections 历史回答/截图).
/// </summary>
public sealed class DesignSystemControlScanTests
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

    /// <summary>The control types the task book names, plus Expander (no design-system component).</summary>
    private static readonly string[] Scanned =
        ["ComboBox", "Button", "TextBox", "CheckBox", "ToggleSwitch", "Expander"];

    private static (HashSet<string> Types, Dictionary<string, HashSet<string>> Classes) DesignSystem()
    {
        var types = new HashSet<string>(StringComparer.Ordinal);
        var classes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Desktop, "Styles"), "*.axaml"))
        {
            var xaml = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(xaml, "Selector=\"([^\"]+)\""))
            {
                var selector = match.Groups[1].Value.Trim();
                var first = selector.Split(' ', '/', ':', ',')[0];
                if (first.Length == 0) continue;
                var dot = first.IndexOf('.');
                if (dot < 0)
                {
                    types.Add(first);
                    continue;
                }
                var type = first[..dot];
                var name = first[(dot + 1)..];
                if (!classes.TryGetValue(type, out var set)) classes[type] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(name);
            }
        }
        return (types, classes);
    }

    [Fact]
    public void EveryScreenControlUsesTheDesignSystem()
    {
        var (types, classes) = DesignSystem();
        // Guard: the scan itself must see the design-system defaults, otherwise it would pass by blindness.
        Assert.Contains("Button", types);
        Assert.Contains("ComboBox", types);
        Assert.Contains("TextBox", types);
        Assert.Contains("CheckBox", types);

        var failures = new List<string>();
        var files = Directory.GetFiles(Desktop, "*.axaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Styles" + Path.DirectorySeparatorChar))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .OrderBy(path => path, StringComparer.Ordinal);
        foreach (var file in files)
        {
            XDocument document;
            try { document = XDocument.Load(file, LoadOptions.SetLineInfo); }
            catch (Exception) { continue; }
            foreach (var element in document.Descendants())
            {
                var name = element.Name.LocalName;
                if (!Scanned.Contains(name)) continue;
                var line = ((System.Xml.IXmlLineInfo)element).HasLineInfo()
                    ? ((System.Xml.IXmlLineInfo)element).LineNumber
                    : 0;
                var shortName = Path.GetFileName(file);
                if (name == "Expander")
                {
                    failures.Add($"{shortName}:{line}: Expander has no design-system component (§5.6)");
                    continue;
                }
                if (types.Contains(name)) continue;
                // No default style for this type: it must carry a class the design system styles.
                var elementClasses = (element.Attribute("Classes")?.Value ?? "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(value => !value.Contains('{') && !value.Contains('}'))
                    .ToList();
                if (classes.TryGetValue(name, out var styled) && elementClasses.Any(styled.Contains)) continue;
                failures.Add($"{shortName}:{line}: {name} is not styled by the design system (no default style and no known class)");
            }
        }
        Assert.True(failures.Count == 0,
            "未套用设计系统样式的控件（§5.6）：\n" + string.Join("\n", failures));
    }
}
