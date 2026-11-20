using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B10: NotesShape.tidy, tolerant decoding and Mac Markdown line markers.</summary>
public sealed class NotesShapeParityTests
{
    [Fact]
    public void TidyCapsOnlyPointSectionsAndGroupsTopics()
    {
        var many = Enumerable.Range(0, 20).Select(index => new NotesItem { Text = "item " + index }).ToList();
        Assert.Equal(14, NotesJson.Tidy(many, NotesSectionKind.Items, false).Count);
        Assert.Equal(6, NotesJson.Tidy(many, NotesSectionKind.Items, true).Count);
        Assert.Equal(20, NotesJson.Tidy(many, NotesSectionKind.OneLine, false).Count);
        Assert.Equal(20, NotesJson.Tidy(many, NotesSectionKind.Actions, false).Count);

        var grouped = new List<NotesItem>
        {
            new() { Text = "a", Topic = "上线顺序" },
            new() { Text = "b", Topic = "资金" },
            new() { Text = "c", Topic = "上线顺序" }
        };
        var tidy = NotesJson.Tidy(grouped, NotesSectionKind.Items, false);
        Assert.Equal(["a", "c", "b"], tidy.Select(item => item.Text));
        Assert.All(tidy, item => Assert.NotNull(item.Topic));

        var ungrouped = new List<NotesItem> { new() { Text = "a", Topic = "only" }, new() { Text = "b", Topic = "only" } };
        Assert.All(NotesJson.Tidy(ungrouped, NotesSectionKind.Items, false), item => Assert.Null(item.Topic));
    }

    [Fact]
    public void DecoderAcceptsStringBooleansNumericTimesAndNumbers()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        var doc = NotesJson.Parse("""
            {"title":" 一面 ","sections":[{"id":"questions","items":[
              {"text":"为什么选这个岗位","time":190,"uncertain":"true","answer":42,"verdict":"improve","better":"先说岗位要求"}]}]}
            """, template)!;
        var item = Assert.Single(doc.Sections.First(section => section.Id == "questions").Items);
        Assert.Equal("一面", doc.Title);
        Assert.Equal("190", item.Time);
        Assert.True(item.Uncertain);
        Assert.Equal("42", item.Answer);
        var markdown = NotesMarkdown.Render(doc);
        Assert.Contains("## 逐题回放", markdown);
        Assert.Contains("（待改进）（待确认） [190]", markdown);
        Assert.Contains("（待确认）", markdown);
        Assert.Contains("更好的答法：先说岗位要求", markdown);
    }

    [Fact]
    public void FactsLineCarriesDurationPlaceAndCorrections()
    {
        var doc = new StructuredNotesDoc("general", "会议", "zh", "标题", [])
        {
            Corrections = [new TranscriptCorrection { Original = "甲", Corrected = "乙" }]
        };
        var facts = NotesMarkdown.Facts(doc, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 38 * 60, false, false);
        Assert.Contains("38 分钟", facts);
        Assert.Contains("线上", facts);
        Assert.Contains("已校对 1 处识别错误", facts);
        Assert.Contains(" · ", facts);
    }

}
