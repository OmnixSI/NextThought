using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the document job model.</summary>
public sealed class DocumentJobsTests
{
    [Fact]
    public void LimitsMatchTheImporter()
    {
        Assert.Equal(512L * 1024 * 1024, DocumentLimits.MaximumFileBytes);
        Assert.Equal(2000, DocumentLimits.MaximumPages);
        Assert.Equal(2L * 1024 * 1024 * 1024, DocumentLimits.MaximumExpandedBytes);
    }

    [Theory]
    [InlineData(DocumentJob.StageValue.Receiving, "正在接收")]
    [InlineData(DocumentJob.StageValue.Ocr, "OCR / 文件代读")]
    [InlineData(DocumentJob.StageValue.Ready, "资料已就绪")]
    [InlineData(DocumentJob.StageValue.Paused, "会议结束后继续")]
    public void StageTitlesAreUserFacing(DocumentJob.StageValue stage, string title)
    {
        Assert.Equal(title, stage.Title());
    }

    [Fact]
    public void OnlyFinishedStagesAreTerminal()
    {
        Assert.True(DocumentJob.StageValue.Ready.Terminal());
        Assert.True(DocumentJob.StageValue.Warning.Terminal());
        Assert.True(DocumentJob.StageValue.Failed.Terminal());
        Assert.True(DocumentJob.StageValue.Cancelled.Terminal());
        Assert.False(DocumentJob.StageValue.Receiving.Terminal());
        Assert.False(DocumentJob.StageValue.Waiting.Terminal());
        Assert.False(DocumentJob.StageValue.Paused.Terminal());
    }

    [Fact]
    public void CreateFillsDefaultsAndTimestamps()
    {
        var job = DocumentJob.Create("ws", "slides.pptx");
        Assert.Equal("ws", job.WorkspaceId);
        Assert.Equal("slides.pptx", job.FileName);
        Assert.Equal("system", job.Provider);
        Assert.Equal("gpt-6-luna", job.Model);
        Assert.Equal("medium", job.Effort);
        Assert.True(job.Fallback);
        Assert.Equal(600, job.Timeout);
        Assert.Equal(DocumentJob.StageValue.Receiving, job.Stage);
        Assert.Empty(job.Categories);
        Assert.False(job.IsTerminal);
        Assert.Equal(job.CreatedAt, job.UpdatedAt);
        Assert.True(job.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void ProgressTextShowsTotalsWhenKnown()
    {
        var job = DocumentJob.Create("ws", "a.pdf") with { Stage = DocumentJob.StageValue.Embedding, Completed = 3, Total = 10 };
        Assert.Equal("建立语义索引 3/10", job.ProgressText);
        Assert.Equal("建立语义索引", (job with { Total = null }).ProgressText);
    }

    [Fact]
    public void DependencyErrorKeepsItsReasonAndMessage()
    {
        var error = new DocumentDependencyError(DocumentJob.WaitingReason.OcrModels, "需要 OCR 组件");
        Assert.Equal(DocumentJob.WaitingReason.OcrModels, error.Reason);
        Assert.Equal("需要 OCR 组件", error.Message);
    }

    [Fact]
    public void EventsAndArtifactsCarryTheirPayloads()
    {
        var progress = new DocumentJobEvent(DocumentJob.StageValue.Lexical, 2, 5, "索引中");
        Assert.Equal(2, progress.Completed);
        Assert.Equal(5, progress.Total);
        var artifact = new DocumentArtifact(new StructuredDocument { Pipeline = "pdf" }, "hash", "pdfpig");
        Assert.Equal(1, artifact.Version);
        Assert.Equal("pdf", artifact.Content.Pipeline);
    }
}
