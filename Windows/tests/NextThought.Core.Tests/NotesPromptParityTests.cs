using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B8: the template notes prompt matches NotesTemplates.swift:582-623.</summary>
public sealed class NotesPromptParityTests
{
    private static readonly DateTimeOffset MeetingDate = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void SystemPromptCarriesTheSwiftRulesShapesAndMeetingDate()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.General, false);
        var prompt = NotesPrompt.OrganizeSystem(template, english: false, inPerson: false, meetingDate: MeetingDate);
        Assert.Contains("你是 NextThought 的会后纪要助手。", prompt);
        Assert.Contains("会议日期：" + NotesDates.Format(MeetingDate.DateTime, false), prompt);
        Assert.Contains("1. 写结果，不写过程。每条一句话、一件事，不超过 50 个字。", prompt);
        Assert.Contains("8. 所有文字用简体中文（人名、术语保持原样）。", prompt);
        Assert.Contains("只输出一个 JSON 对象，不要代码块以外的说明：", prompt);
        Assert.Contains("{\"title\": \"不超过 24 个字的标题\"", prompt);
        foreach (var section in template.Sections)
            Assert.Contains("- id \"" + section.Id + "\"、「" + section.Title + "」：", prompt);
    }

    [Fact]
    public void UserPromptAppendsTheReferenceAnswersOnlyWhenPresent()
    {
        Assert.Equal("头\n\n## 完整转写\n转写", NotesPrompt.OrganizeUser("头", "转写"));
        var withAnswers = NotesPrompt.OrganizeUser("头", "转写", "回答");
        Assert.Contains("## 会中生成的参考回答（不代表“我”实际说的话）\n回答", withAnswers);
        Assert.Contains("这是完整转写的第 2/3 部分。只整理这一部分，按同样的 JSON 格式输出",
            NotesPrompt.PartialUser("头", "块", 1, 3));
    }
}
