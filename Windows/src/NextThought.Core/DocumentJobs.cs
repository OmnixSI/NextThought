namespace NextThought.Core;

public static class DocumentLimits
{
    public const long MaximumFileBytes = 512L * 1024 * 1024;
    public const int MaximumPages = 2000;
    public const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
}

/// <summary>
/// One document conversion job (port of the DocumentJob model; the Mac SQLite queue stays
/// app-side because Windows imports synchronously with an atomic staging directory).
/// </summary>
public sealed record DocumentJob
{
    public enum WaitingReason
    {
        Runtime,
        OcrModels,
        CodexConnection,
        LegacyOffice,
        Semantic
    }

    public enum StageValue
    {
        Receiving,
        Queued,
        Parsing,
        Ocr,
        Validating,
        Lexical,
        Embedding,
        Ready,
        Warning,
        Waiting,
        Paused,
        Failed,
        Cancelled
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    [System.Text.Json.Serialization.JsonPropertyName("workspaceID")]
    public string WorkspaceId { get; init; } = "";
    public string FileName { get; init; } = "";
    public string? OriginalPath { get; init; }
    public string? SourceHash { get; init; }
    public byte[]? Bookmark { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = [];
    public string Provider { get; init; } = "system";
    public string Model { get; init; } = "gpt-6-luna";
    [System.Text.Json.Serialization.JsonPropertyName("localModelID")]
    public string? LocalModelId { get; init; }
    public string Effort { get; init; } = "medium";
    public bool Fallback { get; init; } = true;
    public double Timeout { get; init; } = 600;
    public StageValue Stage { get; init; } = StageValue.Receiving;
    public int Completed { get; init; }
    public int? Total { get; init; }
    public string Message { get; init; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("waitingReason")]
    public WaitingReason? WaitingFor { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("documentID")]
    public string? DocumentId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public static DocumentJob Create(string workspaceId, string fileName, string? category = null,
        IReadOnlyList<string>? categories = null, string provider = "system", string model = "gpt-6-luna",
        string effort = "medium", bool fallback = true, double timeout = 600)
    {
        var now = DateTimeOffset.UtcNow;
        return new DocumentJob
        {
            WorkspaceId = workspaceId,
            FileName = fileName,
            Category = category,
            Categories = categories ?? [],
            Provider = provider,
            Model = model,
            Effort = effort,
            Fallback = fallback,
            Timeout = timeout,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public string ProgressText => Stage.Title() + (Total is { } total ? $" {Completed}/{total}" : "");

    public bool IsTerminal => Stage.Terminal();
}

public static class DocumentJobStages
{
    public static string Title(this DocumentJob.StageValue stage) => stage switch
    {
        DocumentJob.StageValue.Receiving => "正在接收",
        DocumentJob.StageValue.Queued => "排队中",
        DocumentJob.StageValue.Parsing => "格式解析",
        DocumentJob.StageValue.Ocr => "OCR / 文件代读",
        DocumentJob.StageValue.Validating => "内容检查",
        DocumentJob.StageValue.Lexical => "建立文字索引",
        DocumentJob.StageValue.Embedding => "建立语义索引",
        DocumentJob.StageValue.Ready => "资料已就绪",
        DocumentJob.StageValue.Warning => "已完成，需核对",
        DocumentJob.StageValue.Waiting => "等待组件",
        DocumentJob.StageValue.Paused => "会议结束后继续",
        DocumentJob.StageValue.Failed => "处理失败",
        _ => "已取消"
    };

    public static bool Terminal(this DocumentJob.StageValue stage) =>
        stage is DocumentJob.StageValue.Ready or DocumentJob.StageValue.Warning
            or DocumentJob.StageValue.Failed or DocumentJob.StageValue.Cancelled;
}

/// <summary>Conversion discovers dependencies lazily, so a text-only PDF never needs an OCR runtime.</summary>
public sealed record DocumentDependencyError(DocumentJob.WaitingReason Reason, string Message);

public sealed record DocumentJobEvent(DocumentJob.StageValue Stage, int Completed = 0, int? Total = null, string Message = "");

public sealed record DocumentArtifact(StructuredDocument Content, string SourceHash, string Converter)
{
    public int Version { get; init; } = 1;
}
