using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the labeled question corpus and its offline evaluation.</summary>
public sealed class QuestionEvalTests
{
    [Fact]
    public void CorpusHasBothSetsAndBothLabels()
    {
        Assert.True(QuestionEvalCorpus.All.Count >= 100);
        Assert.Equal(QuestionEvalCorpus.Extended.Count + QuestionEvalCorpus.HeldOut.Count, QuestionEvalCorpus.All.Count);
        Assert.Contains(QuestionEvalCorpus.All, sample => sample.IsQuestion);
        Assert.Contains(QuestionEvalCorpus.All, sample => !sample.IsQuestion);
        Assert.All(QuestionEvalCorpus.All, sample => Assert.False(string.IsNullOrWhiteSpace(sample.Text)));
    }

    [Fact]
    public void EvaluationCountsEverySampleOnce()
    {
        var samples = new[]
        {
            new QuestionDetectionSample("Why did you choose Go?", true),
            new QuestionDetectionSample("I built a payments service last year.", false),
            new QuestionDetectionSample("Can you explain the cache design?", true),
            new QuestionDetectionSample("Mm.", false)
        };
        var report = QuestionDetectionEvaluator.Evaluate(samples);
        Assert.Equal(4, report.Questions + report.Statements);
        Assert.Equal(2, report.Questions);
        Assert.Equal(2, report.Statements);
        Assert.InRange(report.Detected, 0, report.Questions);
        Assert.InRange(report.FalseTriggers, 0, report.Statements);
        Assert.Equal(report.Questions - report.Detected, report.Missed.Count);
        Assert.Equal(report.FalseTriggers, report.FalsePositives.Count);
        Assert.Equal(4 * 6 / 3600.0, report.SimulatedHours, 8);
    }

    [Fact]
    public void ReportMetricsHandleEdgeCases()
    {
        var empty = QuestionDetectionEvaluator.Evaluate([]);
        Assert.Equal(1, empty.Recall);
        Assert.Equal(0, empty.FalseTriggersPerHour);
        Assert.Equal(0, empty.Questions);

        var missed = QuestionDetectionEvaluator.Evaluate([new QuestionDetectionSample("嗯。", true)]);
        Assert.InRange(missed.Recall, 0, 1);
        Assert.Equal(1, missed.Questions);
    }
}
