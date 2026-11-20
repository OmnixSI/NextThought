using System.Text.RegularExpressions;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G8: interface text is fetched explicitly. Every Chinese literal in a .axaml file must sit inside a
/// <c>{loc:Tr …}</c> markup extension (the Mac original sentence is the key), and the tree-walk
/// translation mechanism must be gone.
/// </summary>
public sealed class LocalizationSourceScanTests
{
    private static readonly Regex Cjk = new("[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff00-\uffef]", RegexOptions.Compiled);
    private static readonly Regex XmlComment = new("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TrValue = new(@"\{loc:Tr\s+(?:'[^']*'|[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex TrAttribute = new(@"=" + '"' + @"\{loc:Tr\s+(?:'(?<quoted>[^']*)'|(?<plain>[^}]*))\}" + '"', RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found from " + AppContext.BaseDirectory);
    }

    private static string Desktop => Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop");

    [Fact]
    public void EveryChineseLiteralInXamlIsAMarkupExtensionKey()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Desktop, "*.axaml", SearchOption.AllDirectories))
        {
            var raw = File.ReadAllText(file);
            var stripped = TrValue.Replace(XmlComment.Replace(raw, string.Empty), string.Empty);
            var match = Cjk.Match(stripped);
            if (!match.Success) continue;
            var line = stripped[..match.Index].Count(character => character == '\n') + 1;
            var index = match.Index;
            offenders.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{line}: {stripped[index..Math.Min(stripped.Length, index + 60)].Trim()}");
        }
        Assert.True(offenders.Count == 0,
            "Chinese literals must be written as {loc:Tr 中文原句}:\n" + string.Join("\n", offenders.Take(20)));
    }

    [Fact]
    public void MarkupExtensionKeysAreTheChineseSourceSentences()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Desktop, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match match in TrAttribute.Matches(File.ReadAllText(file)))
                keys.Add(match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value);
        }
        Assert.True(keys.Count > 400, $"expected the migrated shell to carry 400+ keys, found {keys.Count}");
        Assert.All(keys, key =>
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.True(Cjk.IsMatch(key), "the key must be the Mac Chinese original sentence: " + key);
            // Chinese mode returns the key unchanged; the English table must not return an empty string.
            Assert.Equal(key, UiText.Lookup(key, false));
            Assert.False(string.IsNullOrWhiteSpace(UiText.Lookup(key, true)));
        });
    }

    [Fact]
    public void TreeWalkTranslationMechanismIsGone()
    {
        var root = RepoRoot();
        Assert.False(File.Exists(Path.Combine(Desktop, "UiTranslation.cs")), "UiTranslation.cs must be deleted");
        var offenders = Directory.EnumerateFiles(Desktop, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("UiTranslation", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file))
            .ToArray();
        Assert.True(offenders.Length == 0, "tree-walk translation leftovers: " + string.Join(", ", offenders));
    }
}
