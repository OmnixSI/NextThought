using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Swift parity: Tests/NextThoughtCoreTests/DocumentNoticeTests.swift.</summary>
public sealed class DocumentNoticeParityTests
{
    private static string Figure(int page) =>
        $"p.{page}：已提取原生文字；页内图像中的文字、图表或公式尚未读取，请核对原件。";

    [Fact]
    public void UnreadPicturesAreOneQuietLine()
    {
        var lines = new[] { Figure(7), Figure(15), Figure(17), Figure(21) };
        Assert.Empty(DocumentNotice.Warnings(lines));
        Assert.Equal("p.7、15、17、21 的图片没有读取，正文已完整提取。", DocumentNotice.Note(lines));
    }

    [Fact]
    public void AFailedPageStillNeedsALook()
    {
        var lines = new[] { Figure(7), "p.9：识别失败，其他页面已保留；timeout" };
        Assert.Equal(["p.9：识别失败，其他页面已保留；timeout"], DocumentNotice.Warnings(lines));
        Assert.Equal("p.7 的图片没有读取，正文已完整提取。", DocumentNotice.Note(lines));
    }

    [Fact]
    public void TheSameLineIsSaidOnce()
    {
        var line = "幻灯片 3：请核对空白页或不可提取图形。";
        Assert.Equal(2, DocumentNotice.Unique([line, line, "p.1：未识别到文字，请核对原件。"]).Count);
        Assert.Null(DocumentNotice.Note([line]));
    }

    [Fact]
    public void TheFoldedLineIsItselfANote()
    {
        var folded = DocumentNotice.Note([Figure(2)])!;
        Assert.True(DocumentNotice.IsNote(folded));
        Assert.Empty(DocumentNotice.Warnings([folded]));
    }

    [Fact]
    public void BlocksOfOneSlideShareItsMark()
    {
        var blocks = new List<DocumentBlock>
        {
            new("幻灯片 1", "Title", 1),
            new("幻灯片 1", "First point", 1),
            new("幻灯片 2", "Results", 2)
        };
        var deck = new MaterialDocument { Name = "deck.pptx", Blocks = blocks };
        var text = AnswerPromptBuilder.WholeMaterialOf([deck]);
        Assert.Equal("【deck.pptx · 幻灯片 1】\nTitle\nFirst point\n\n【deck.pptx · 幻灯片 2】\nResults", text);
    }

    [Fact]
    public void PagesOfAReportKeepTheirNumbers()
    {
        var blocks = new List<DocumentBlock>
        {
            new("p.1", "Intro", 1),
            new("p.7", "Method", 7),
            new("p.7", "More method", 7)
        };
        var report = new MaterialDocument { Name = "report.pdf", Blocks = blocks };
        var text = AnswerPromptBuilder.WholeMaterialOf([report]) ?? "";
        Assert.Contains("【report.pdf · p.7】\nMethod\nMore method", text);
        Assert.DoesNotContain("第 2", text);
    }

    [Fact]
    public void PlainNotesAndTheLimit()
    {
        var note = new ContextDocument { Name = "notes.md", Pages = ["Hello"] };
        Assert.Equal("\u3010notes.md\u3011\nHello", AnswerPromptBuilder.WholeMaterialOf([note]));
        Assert.Null(AnswerPromptBuilder.WholeMaterialOf([note], 3));
        var off = note with { Enabled = false };
        Assert.Null(AnswerPromptBuilder.WholeMaterialOf([off]));
    }

    [Fact]
    public void AnEnglishDoubtIsAnsweredInEnglish()
    {
        var doubt = "I am not fully convinced by the term physics informed. It seems to me that many model-based deep learning methods already use the array response.";
        Assert.Equal("English", AnswerLanguage.Spoken(doubt));
        var fields = AnswerLanguage.Fields("auto", true, doubt);
        Assert.Equal("English", fields.Answer);
        Assert.Equal("简体中文", fields.Gloss);
    }

    [Fact]
    public void RecognisedEnglishWithAChineseWordIsStillEnglish()
    {
        Assert.Equal("English", AnswerLanguage.Spoken("Could you explain the 消融 study on the adaptation slide?"));
    }

    [Fact]
    public void AChineseQuestionNamingTermsIsChinese()
    {
        var question = "XL-MIMO 的 near field 效应你是怎么考虑的？";
        Assert.Equal("简体中文", AnswerLanguage.Spoken(question));
        Assert.Null(AnswerLanguage.Fields("auto", true, question).Gloss);
    }

    [Fact]
    public void AFixedChoiceWinsAndShortRemarksDecideNothing()
    {
        Assert.Equal("English", AnswerLanguage.Fields("en", true, "你怎么看这个问题？").Answer);
        Assert.Null(AnswerLanguage.Fields("zh", true, "What is new here?").Gloss);
        Assert.Null(AnswerLanguage.Spoken("OK."));
        Assert.Null(AnswerLanguage.Fields("auto", false, "What is new in your method?").Gloss);
    }
}
