using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G10: Windows/WORDING.md is the Mac → Windows wording replacement table. Every sentence whose
/// Windows wording differs from the Mac original must be listed there (Chinese and English), and the
/// English screenshot scan recorded in it must show no residual Chinese outside user data.
/// </summary>
public sealed class WordingTableTests
{
    private static string RepoRoot()
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

    private static string Wording => Path.Combine(RepoRoot(), "Windows", "WORDING.md");

    [Fact]
    public void EveryWindowsOnlySentenceIsListedWithItsEnglishWording()
    {
        Assert.True(File.Exists(Wording), Wording + " is missing");
        var document = File.ReadAllText(Wording);
        var missing = UiText.WindowsStrings.Keys
            .Where(key => !document.Contains(Cell(key), StringComparison.Ordinal))
            .ToArray();
        Assert.True(missing.Length == 0,
            "句子没有出现在 WORDING.md 的替换表里（重跑 localization_scan.py --write）：\n" +
            string.Join("\n", missing.Take(20)));
    }

    [Fact]
    public void WordingTableDocumentsTheSystemNounReplacements()
    {
        var document = File.ReadAllText(Wording);
        foreach (var (mac, windows) in new[]
                 {
                     ("钥匙串", "凭据管理器"),
                     ("访达", "文件资源管理器"),
                 })
        {
            Assert.Contains(mac, document, StringComparison.Ordinal);
            Assert.Contains(windows, document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EnglishScreenshotScanHasNoResidualChinese()
    {
        var document = File.ReadAllText(Wording);
        Assert.Contains("英文模式逐页扫描", document, StringComparison.Ordinal);
        Assert.Contains("**残留（需要修）：0**", document, StringComparison.Ordinal);
    }

    [Fact]
    public void EnglishScreenshotScanAlsoChecksTruncationAndListsEveryPage()
    {
        var document = File.ReadAllText(Wording);
        Assert.Contains("**英文文字截断/溢出（需要修）：0**", document, StringComparison.Ordinal);
        Assert.Contains("逐页结论", document, StringComparison.Ordinal);
        var evidencePath = Path.Combine(RepoRoot(), "Windows", "validation", "localization", "english-scan.md");
        Assert.True(File.Exists(evidencePath), evidencePath + " is missing");
        var evidence = File.ReadAllText(evidencePath);
        Assert.Contains("逐页结论", evidence, StringComparison.Ordinal);
        Assert.Contains("文字截断/溢出：0", evidence, StringComparison.Ordinal);
        // Every English page row ends in a conclusion; the scan must cover the whole page set.
        var rows = evidence.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal)).ToArray();
        Assert.True(rows.Length >= 40, "per-page table only has " + rows.Length + " rows");
        Assert.DoesNotContain("不通过", evidence, StringComparison.Ordinal);
    }

    /// <summary>Same escaping the generator uses for a Markdown table cell.</summary>
    private static string Cell(string key) =>
        key.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", "⏎", StringComparison.Ordinal);
}
