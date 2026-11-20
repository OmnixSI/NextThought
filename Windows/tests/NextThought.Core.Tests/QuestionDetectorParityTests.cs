using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from QuestionDetectorTests.swift: the first-layer rule detector merges split
/// fragments, accepts Chinese requests and short follow-ups, and suppresses exact repeats
/// for 15 seconds.
/// </summary>
public sealed class QuestionDetectorParityTests
{
    [Fact]
    public void DetectsEnglishQuestionWithoutQuestionMark()
    {
        var detector = new QuestionDetector();
        Assert.NotNull(detector.Detect("How does a database index improve lookup speed"));
    }

    [Fact]
    public void DetectsChineseFollowUp()
    {
        var detector = new QuestionDetector();
        Assert.NotNull(detector.Detect("数据库索引有什么缺点"));
    }

    [Fact]
    public void IgnoresStatementsAndDeduplicatesRepeatedQuestions()
    {
        var detector = new QuestionDetector();
        Assert.Null(detector.Detect("The database index improves lookup speed"));
        Assert.NotNull(detector.Detect("What are the drawbacks of a database index?"));
        Assert.Null(detector.Detect("What are the drawbacks of a database index?"));
    }

    [Fact]
    public void JoinsQuestionFragmentsAcrossSegments()
    {
        var detector = new QuestionDetector();
        Assert.Null(detector.Detect("Can you"));
        Assert.Equal("Can you explain this design", detector.Detect("explain this design"));
    }

    [Fact]
    public void RecognizesChineseRequestAndMixedTerminology()
    {
        var detector = new QuestionDetector();
        Assert.Equal("解释一下这个设计", detector.Detect("解释一下这个设计"));
        Assert.NotNull(detector.Detect("解释一下这个 database index 设计"));
        Assert.Null(detector.Detect("这个 database index 会提升 lookup speed"));
    }

    [Fact]
    public void JoinsLongerQuestionThatWasSplitBetweenSegments()
    {
        var detector = new QuestionDetector();
        Assert.Null(detector.Detect("How does database"));
        Assert.Equal("How does database index improve lookup speed",
            detector.Detect("index improve lookup speed"));
    }

    [Fact]
    public void RecognizesShortFollowUpAndDeduplicatesChinese()
    {
        var detector = new QuestionDetector();
        Assert.NotNull(detector.Detect("为什么？"));
        Assert.Null(detector.Detect("为什么？"));
        Assert.NotNull(detector.Detect("它有什么缺点"));
        Assert.Null(detector.Detect("它有什么缺点"));
    }

    [Fact]
    public void RecognizesShortFollowUpWithoutQuestionMark()
    {
        var detector = new QuestionDetector();
        Assert.Equal("为什么", detector.Detect("为什么"));
        Assert.Null(detector.Detect("普通陈述"));
    }
}
