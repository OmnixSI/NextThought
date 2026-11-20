using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B11: translated from MeetingTypeAndNotesTests.swift reviewFeedsTheQuestionBank.</summary>
public sealed class NotesModelTests
{
    [Fact]
    public void ReviewFeedsTheQuestionBank()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        const string reply = """
            {"sections": [{"id": "questions", "items": [
              {"text": "如果删除缓存失败了怎么办？", "time": "18:22", "answer": "重试队列 + binlog", "verdict": "good"},
              {"text": "热点 key 过期怎么办？", "time": "27:05", "answer": "加锁", "verdict": "improve", "better": "互斥锁重建 + 逻辑过期。"}
            ]}, {"id": "improve", "items": [{"text": "先说结论", "time": "27:05"}]}]}
            """;
        var notes = NotesJson.Parse(reply, template, "zh")!;
        Assert.Equal(2, notes.QuestionCount());
        Assert.Equal(1, notes.ImproveCount());
        var entry = Assert.Single(notes.BankEntries());
        Assert.Equal("热点 key 过期怎么办？", entry.Question);
        Assert.Equal("互斥锁重建 + 逻辑过期。", entry.Answer);
        Assert.Equal(["先说结论"], notes.Lessons(template));
    }

    [Fact]
    public void CopiesAndNamesCarryThroughTheNotes()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.General);
        var copy = template.CustomCopy("我的模板");
        Assert.NotEqual(template.Id, copy.Id);
        Assert.Equal("我的模板", copy.Name);
        var doc = new StructuredNotesDoc("general", "会议", "zh", "说话人 1 的会",
            [new NotesSectionResult("actions", "行动", NotesSectionKind.Actions, false,
                [new NotesItem { Text = "说话人 1 跟进", Owner = "说话人 1" }])]);
        var named = doc.Named(new Dictionary<int, string> { [1] = "小王" });
        Assert.Equal("小王 的会", named.Title);
        Assert.Equal("小王 跟进", named.Sections[0].Items[0].Text);
        Assert.Equal("小王", named.Sections[0].Items[0].Owner);
    }
}
