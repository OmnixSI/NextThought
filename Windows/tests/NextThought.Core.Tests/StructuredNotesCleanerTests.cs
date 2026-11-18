using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for NotesVerifier and StructuredNotesCleaner.</summary>
public sealed class StructuredNotesCleanerTests
{
    private static readonly int[] Offsets = [0, 30, 65, 130, 200, 260];

    private static StructuredNotesPayload Fishbone(params (string Name, (string Text, int? At)[] Causes)[] categories)
    {
        var parsed = StructuredNotesParser.Parse(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                head = "残差偏大",
                categories = categories.Select(category => new
                {
                    name = category.Name,
                    causes = category.Causes.Select(cause => new { text = cause.Text, time = cause.At })
                })
            }),
            NotesFormat.Fishbone)!;
        return parsed;
    }

    [Fact]
    public void SupportedTimesMustBeNearATranscriptLine()
    {
        Assert.True(NotesVerifier.IsSupported(130, Offsets, duration: 300));
        Assert.True(NotesVerifier.IsSupported(160, Offsets, duration: 300)); // within 45 s of 130
        Assert.False(NotesVerifier.IsSupported(600, Offsets, duration: 300));
        Assert.False(NotesVerifier.IsSupported(400, Offsets, duration: 300)); // beyond duration + 90
        // Without line times only the meeting length is checked.
        Assert.True(NotesVerifier.IsSupported(280, [], duration: 300));
    }

    [Fact]
    public void CleanerDropsNodesWithoutEvidence()
    {
        var payload = Fishbone(("数据", [("支座测点不足", 30), ("没有依据的原因", 900)]),
            ("模型", [("刚度未标定", 130)]));
        var cleaned = StructuredNotesCleaner.Clean(payload, Offsets, duration: 300)!;
        Assert.NotNull(cleaned);
        Assert.Equal(1, cleaned.DroppedCount);
        Assert.Equal(2, cleaned.Fishbone!.Categories.Sum(category => category.Causes.Count));
        Assert.DoesNotContain(cleaned.Fishbone.Categories.SelectMany(category => category.Causes),
            cause => cause.Text == "没有依据的原因");
    }

    [Fact]
    public void CleanerDropsNodesWithoutAnyTime()
    {
        // Only one supported cause is left, which is below the "drawable" threshold of two.
        Assert.Null(StructuredNotesCleaner.Clean(
            Fishbone(("数据", [("支座测点不足", null)]), ("模型", [("刚度未标定", 130)])), Offsets, duration: 300));

        var cleaned = StructuredNotesCleaner.Clean(
            Fishbone(("数据", [("支座测点不足", null), ("风速未接入", 65)]), ("模型", [("刚度未标定", 130)])),
            Offsets, duration: 300)!;
        Assert.Equal(2, cleaned.Fishbone!.Categories.Sum(category => category.Causes.Count));
    }

    [Fact]
    public void CleanerNeedsAtLeastTwoNodes()
    {
        var payload = Fishbone(("数据", [("支座测点不足", 30)]));
        Assert.Null(StructuredNotesCleaner.Clean(payload, Offsets, duration: 300));
    }

    [Fact]
    public void CleanerMergesSameCategoryAndRemovesRepeats()
    {
        var payload = Fishbone(
            ("数据", [("支座测点不足", 30), ("支座测点不足", 65)]),
            ("数据", [("风速数据未接入", 130)]));
        var cleaned = StructuredNotesCleaner.Clean(payload, Offsets, duration: 300)!;
        Assert.Single(cleaned.Fishbone!.Categories);
        Assert.Equal(2, cleaned.Fishbone.Categories[0].Causes.Count);
    }

    [Fact]
    public void CleanerCapsAndTidiesText()
    {
        var payload = Fishbone(("数据", [("同一句话，" + new string('长', 80), 30), ("第二条", 65)]));
        var cleaned = StructuredNotesCleaner.Clean(payload, Offsets, duration: 300)!;
        var cause = cleaned.Fishbone!.Categories[0].Causes[0];
        Assert.EndsWith("…", cause.Text);
        Assert.True(cause.Text.Length <= StructuredNotesLimits.CauseCharacters);
    }

    [Fact]
    public void OutlineCleanerDropsUnsupportedPointsAndConcepts()
    {
        var reply = """
            {"sections": [
              {"title": "温度补偿", "time": "00:30", "points": [
                {"text": "小波分解", "time": "01:10"},
                {"text": "无依据的要点", "time": "40:00"}
              ]}
            ], "concepts": [{"term": "残差", "meaning": "差值", "time": "02:10"}], "review": []}
            """;
        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Outline)!;
        var cleaned = StructuredNotesCleaner.Clean(payload, Offsets, duration: 300)!;
        Assert.Equal("小波分解", cleaned.Outline!.Sections[0].Points[0].Text);
        Assert.Single(cleaned.Outline.Sections[0].Points);
        Assert.Equal("残差", cleaned.Outline.Concepts[0].Term);
        Assert.Equal(1, cleaned.DroppedCount);
    }

    [Fact]
    public void ViewpointCleanerKeepsOnlyKnownPartiesAndSupportedRows()
    {
        var reply = """
            {"topic": "方案", "parties": ["我", "导师"],
             "issues": [{"title": "刚度", "positions": [
               {"party": "我", "text": "先做敏感性分析", "time": "02:10"},
               {"party": "临时插入的人", "text": "无关", "time": "02:10"}]}],
             "consensus": [{"text": "本周先跑 20 组", "time": "04:20"}]}
            """;
        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Viewpoints)!;
        var cleaned = StructuredNotesCleaner.Clean(payload, Offsets, duration: 300)!;
        var matrix = cleaned.Viewpoints!;
        // A side first seen inside a position is still a party (Swift does the same), so both stay.
        Assert.Equal(["我", "临时插入的人"], matrix.Parties);
        Assert.Equal(2, matrix.Issues[0].Positions.Count);
        Assert.Equal("先做敏感性分析", matrix.Issues[0].Positions[0].Text);
        Assert.Single(matrix.Consensus);
    }
}
