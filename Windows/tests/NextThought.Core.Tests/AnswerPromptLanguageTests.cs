using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G9: the live answer prompt follows Mac AppState.answer — AnswerPromptBuilder.makeParts over
/// structured data, with the answer/gloss language carried inside the JSON (AnswerLanguage.fields)
/// instead of Chinese sentences appended to the prompt. The instructions themselves stay verbatim
/// Mac Chinese like Mac (Mac has one prompt language; the answer language is a JSON field).
/// </summary>
public sealed class AnswerPromptLanguageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void AnswerAndGlossLanguageFollowTheSpokenQuestion()
    {
        Assert.Equal(("简体中文", (string?)null), AnswerLanguage.Fields("auto", false, "为什么用 Redis？"));
        Assert.Equal(("English", (string?)null), AnswerLanguage.Fields("auto", false, "Why use Redis?"));
        Assert.Equal(("English", "简体中文"), AnswerLanguage.Fields("auto", true, "Why use Redis?"));
        Assert.Equal(("简体中文", (string?)null), AnswerLanguage.Fields("auto", true, "为什么用 Redis？"));
        Assert.Equal(("简体中文", (string?)null), AnswerLanguage.Fields("zh", true, "Why use Redis?"));
        Assert.Equal(("English", "简体中文"), AnswerLanguage.Fields("en", true, "为什么用 Redis？"));
        Assert.Null(AnswerLanguage.Spoken("1234 !"));
    }

    [Fact]
    public void LanguageTravelsInTheJSONNotInTheSystemPrompt()
    {
        var (system, user) = AnswerPromptBuilder.MakeParts(
            "Why is the gain stable?", "", "", [], hasScreenshot: false,
            answerLanguage: "English", glossLanguage: "简体中文", today: Now);
        // The system prompt is exactly the Mac instructions; nothing about the answer language is
        // appended as a Chinese sentence (that was the Windows-invented behaviour G9 removes).
        Assert.Equal(AnswerPromptBuilder.Instructions(webSearch: false), system);
        Assert.DoesNotContain("回答语言：", system);
        var json = JsonNode.Parse(user)!.AsObject();
        Assert.Equal("English", json["answer_language"]!.GetValue<string>());
        Assert.Equal("简体中文", json["gloss_language"]!.GetValue<string>());
    }

    [Fact]
    public void InstructionsStillCarryTheMacAnswerLanguageRules()
    {
        var rules = AnswerPromptBuilder.Instructions();
        Assert.Contains("answer_language 存在时", rules);
        Assert.Contains("gloss_language 存在时", rules);
        Assert.Contains("> 译：", rules);
        Assert.DoesNotContain("__TOOLS__", rules);
    }

    [Fact]
    public void LiveAnswerPathUsesThePromptBuilderAndNoInlineChinesePrompt()
    {
        var root = RepoRoot();
        var live = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("AnswerPromptBuilder.MakeParts", live, StringComparison.Ordinal);
        Assert.DoesNotContain("你是会议中的参考助手", live, StringComparison.Ordinal);
        Assert.DoesNotContain("回答语言：中文。", live, StringComparison.Ordinal);
        var notes = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.DoesNotContain("回答语言：中文。", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("用户加载的技能（按适用部分执行）", notes, StringComparison.Ordinal);
    }

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
}
