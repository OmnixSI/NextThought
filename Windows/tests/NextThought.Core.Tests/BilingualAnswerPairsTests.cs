using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>E6: the "> 问：" question gloss and "> 译：" meaning lines pair with their sentences.</summary>
public sealed class BilingualAnswerPairsTests
{
    private const string Answer =
        "> 问：对方问的是缓存一致性怎么保证。\n\n" +
        "The prior is not guaranteed to be exactly correct without ground-truth labels.\n" +
        "> 译：没有真实标签，这个先验不能保证完全正确。\n\n" +
        "I assess it through held-out service pilots and compare it with simpler baselines.\n" +
        "> 译：我用留出的服务导频评估，并和更简单的基线比较。\n";

    [Fact]
    public void TheQuestionComesFromTheFirstMarkedLine()
    {
        Assert.True(BilingualAnswer.IsBilingual(Answer));
        Assert.Equal("对方问的是缓存一致性怎么保证。", BilingualAnswer.Question(Answer));
        Assert.Null(BilingualAnswer.Question("A plain English answer."));
    }

    [Fact]
    public void MeaningsAreRecognisedWithAndWithoutTheYiMarker()
    {
        Assert.IsType<BilingualAnswer.Gloss.Line>(BilingualAnswer.GlossOf("> 译：一句话。"));
        // Models often drop the marker: in a bilingual answer every quoted line is a meaning.
        Assert.IsType<BilingualAnswer.Gloss.Line>(BilingualAnswer.GlossOf("> 一句话。"));
        Assert.Null(BilingualAnswer.GlossOf("> 一句话。", bilingual: false));
        Assert.IsType<BilingualAnswer.Gloss.Question>(BilingualAnswer.GlossOf("> 问：问的是什么？"));
    }

    [Fact]
    public void SpokenKeepsOnlyTheSentencesToSay()
    {
        var spoken = BilingualAnswer.Spoken(Answer).Replace("\r\n", "\n");
        Assert.DoesNotContain(">", spoken);
        Assert.Contains("The prior is not guaranteed", spoken);
        Assert.DoesNotContain("没有真实标签", spoken);
        // A plain answer is returned unchanged.
        Assert.Equal("A plain answer.", BilingualAnswer.Spoken("A plain answer."));
    }

    [Fact]
    public void SentencesPairWithTheMeaningThatFollowsThem()
    {
        var groups = new List<(string Sentence, string Gloss)>();
        foreach (var raw in Answer.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            switch (BilingualAnswer.GlossOf(raw))
            {
                case BilingualAnswer.Gloss.Line gloss:
                    if (groups.Count > 0) groups[^1] = (groups[^1].Sentence, gloss.Text);
                    break;
                case BilingualAnswer.Gloss.Question:
                    break;
                default:
                    groups.Add((raw.Trim(), ""));
                    break;
            }
        }
        Assert.Equal(2, groups.Count);
        Assert.StartsWith("The prior is not guaranteed", groups[0].Sentence);
        Assert.Equal("没有真实标签，这个先验不能保证完全正确。", groups[0].Gloss);
        Assert.StartsWith("I assess it through held-out", groups[1].Sentence);
        Assert.Equal("我用留出的服务导频评估，并和更简单的基线比较。", groups[1].Gloss);
    }

    [Fact]
    public void TheCueLinesCarryTheirMeaningToo()
    {
        var lines = CueScript.Lines(Answer, CueStyle.KeyPoints);
        Assert.Contains(lines, line => line.Gloss.Contains("没有真实标签"));
        Assert.Contains(lines, line => line.Gloss.Contains("留出的服务导频"));
        Assert.DoesNotContain(lines, line => line.Gloss.Contains(BilingualAnswer.Question(Answer)!));
    }

    [Fact]
    public void AnswerLanguageChoicesDriveThePromptFields()
    {
        Assert.Equal(("English", "简体中文"), AnswerLanguage.Fields("auto", true, "Why use Redis?"));
        Assert.Equal(("简体中文", (string?)null), AnswerLanguage.Fields("auto", true, "为什么用 Redis？"));
        Assert.Equal(("English", "简体中文"), AnswerLanguage.Fields("en", true, "这个问题怎么答？"));
        Assert.Equal(("简体中文", (string?)null), AnswerLanguage.Fields("zh", true, "Why use Redis?"));
        Assert.Equal(("English", (string?)null), AnswerLanguage.Fields("auto", false, "Why use Redis?"));
    }
}
