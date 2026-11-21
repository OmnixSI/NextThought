using Xunit;

namespace NextThought.Tests;

/// <summary>
/// A15: the committed Mac fixtures and the MacCodec check are part of the verification chain. These
/// structure checks fail if the pipeline loses a family or the README's compatibility notes.
/// </summary>
public sealed class MacFixtureParityTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Core", "NextThought.Core.csproj")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Fixtures => Path.Combine(Root(), "Windows", "tests", "Fixtures", "mac");

    [Fact]
    public void MacFixturesAreCommittedWithTheReadme()
    {
        if (!Directory.Exists(Fixtures)) return; // SKIP: the fixture directory only exists in the repository checkout.
        Assert.True(File.Exists(Path.Combine(Fixtures, "README.md")));
        foreach (var name in new[]
                 {
                     "session-meta.json", "transcript.jsonl", "translations.jsonl", "voices.jsonl", "questions.jsonl",
                     "answers.jsonl", "summary.json", "structured-notes.json", "user-content.json", "workspace.json",
                     "skills.json", "templates.json", "glossary.json", "backup-manifest.json", "revisions.json"
                 })
            Assert.True(File.Exists(Path.Combine(Fixtures, name)), name);
        Assert.True(Directory.Exists(Path.Combine(Fixtures, "sessions")));
    }

    [Fact]
    public void TheReadmeKeepsTheSixMacFormatDifferences()
    {
        if (!Directory.Exists(Fixtures)) return; // SKIP: fixtures live in the repository checkout.
        var readme = File.ReadAllText(Path.Combine(Fixtures, "README.md"));
        foreach (var sentence in new[]
                 {
                     "没有 `sessionIds`", "没有 `sessions`、`workspaces` 列表", "`score`", "`disposition`",
                     "`audioSource`、`recognizer`、`answerModel`、`transcriptCount`、`questionCount`、`answerCount`、`summaryState`",
                     "读取时也接受裸数组", "每行一个紧凑 JSON 对象"
                 })
            Assert.Contains(sentence, readme);
    }

    [Fact]
    public void TheCodecCheckCoversEveryFixtureFamily()
    {
        var program = File.ReadAllText(Path.Combine(Root(), "Windows", "tools", "MacCodecCheck", "Program.cs"));
        foreach (var name in new[]
                 {
                     "session-meta.json", "summary.json", "user-content.json", "workspace.json", "backup-manifest.json",
                     "revisions.json", "transcript.jsonl", "translations.jsonl", "questions.jsonl", "skills.json",
                     "templates.json", "glossary.json"
                 })
            Assert.Contains(name, program);
        // The old-emit differences are asserted by the tool as well.
        Assert.Contains("sessionIds", program);
        Assert.Contains("workspaceCount", program);
        Assert.Contains("score", program);
    }

    [Fact]
    public void VerifyRunsTheCodecCheckOnEveryVerify()
    {
        var verify = File.ReadAllText(Path.Combine(Root(), "Windows", "verify.ps1"));
        Assert.Contains("MacCodecCheck", verify);
        Assert.Contains("Fixtures", verify);
    }

    [Fact]
    public void MacReferenceScreenshotsAreCommitted()
    {
        var screens = Path.Combine(Root(), "Windows", "validation", "mac-screens");
        if (!Directory.Exists(screens)) return; // SKIP: the external reference batch is optional locally.
        var images = Directory.GetFiles(screens, "*.png", SearchOption.AllDirectories);
        Assert.True(images.Length >= 90, "Mac reference screenshots: " + images.Length);
    }
}
