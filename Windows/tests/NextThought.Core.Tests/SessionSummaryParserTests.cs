using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for parsing the generated summary Markdown.</summary>
public sealed class SessionSummaryParserTests
{
    private const string Sample = """
        # 标题
        *数据库扩容复盘*

        # 问题
        1. 为什么选择分库？—— 单表写入到了瓶颈
        2. 回滚方案是什么？我的回答要点：保留旧双写开关
        3. 延期风险 — 
        - 没有列表标记的忽略？

        # 经验沉淀
        - 先在预发演练扩容
        · 监控阈值提前配置

        # 题库更新
        问：怎么做容量评估？
        答：按峰值 QPS 乘 1.5 预留
        连续行也算答案
        问：演练频率？
        答：每季度一次
        """;

    [Fact]
    public void TitleIsCleanedAndTruncated()
    {
        var parsed = SessionSummaryParser.Parse(Sample);
        Assert.Equal("数据库扩容复盘", parsed.Title);
        var longTitle = SessionSummaryParser.ParseAll("# 标题\n" + new string('长', 80)).Title;
        Assert.Equal(60, longTitle!.Length);
    }

    [Fact]
    public void QuestionsStripMarkersSuffixesAndGists()
    {
        var parsed = SessionSummaryParser.Parse(Sample);
        Assert.Equal(4, parsed.Questions.Count);
        Assert.Equal("为什么选择分库？—— 单表写入到了瓶颈", parsed.Questions[0].Text);
        Assert.Null(parsed.Questions[0].AnswerGist);
        Assert.Equal("回滚方案是什么？", parsed.Questions[1].Text);
        Assert.Equal("保留旧双写开关", parsed.Questions[1].AnswerGist);
        Assert.Equal("延期风险", parsed.Questions[2].Text);
        Assert.Null(parsed.Questions[2].AnswerGist);
    }

    [Fact]
    public void LinesWithoutListMarkersAreIgnoredInQuestions()
    {
        var parsed = SessionSummaryParser.ParseAll("# 问题\n没有编号的问题\n- 有标记的问题");
        Assert.Single(parsed.Questions);
        Assert.Equal("有标记的问题", parsed.Questions[0].Text);
    }

    [Fact]
    public void LessonsAcceptBothDashAndDotBullets()
    {
        var parsed = SessionSummaryParser.ParseAll(Sample);
        Assert.Equal(["先在预发演练扩容", "监控阈值提前配置"], parsed.Lessons);
    }

    [Fact]
    public void BankEntriesJoinContinuationLines()
    {
        var parsed = SessionSummaryParser.ParseAll(Sample);
        Assert.Equal(2, parsed.Bank.Count);
        Assert.Equal("怎么做容量评估？", parsed.Bank[0].Question);
        Assert.Equal("按峰值 QPS 乘 1.5 预留\n连续行也算答案", parsed.Bank[0].Answer);
        Assert.Equal("演练频率？", parsed.Bank[1].Question);
        Assert.Equal("每季度一次", parsed.Bank[1].Answer);
    }

    [Fact]
    public void BankPrefixVariantsAreRecognised()
    {
        var parsed = SessionSummaryParser.ParseAll("# 题库\nQ: q1\nA: a1\n**问**：q2\n**答**：a2");
        Assert.Equal(2, parsed.Bank.Count);
        Assert.Equal("a1", parsed.Bank[0].Answer);
        Assert.Equal("a2", parsed.Bank[1].Answer);
    }

    [Fact]
    public void HeadingsEndThePreviousBankEntry()
    {
        var parsed = SessionSummaryParser.ParseAll("# 题库\n问：q\n答：a\n# 经验\n- 经验一");
        Assert.Single(parsed.Bank);
        Assert.Single(parsed.Lessons);
    }

    [Fact]
    public void EmptyMarkdownYieldsNothing()
    {
        var parsed = SessionSummaryParser.ParseAll("");
        Assert.Null(parsed.Title);
        Assert.Empty(parsed.Questions);
        Assert.Empty(parsed.Lessons);
        Assert.Empty(parsed.Bank);
        Assert.Null(SessionSummaryParser.Parse("").Title);
    }
}
