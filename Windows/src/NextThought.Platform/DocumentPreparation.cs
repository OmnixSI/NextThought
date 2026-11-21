using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record DocumentPreparationOptions(string Provider = "system", bool Fallback = true, CodexDocumentOptions? Codex = null);

/// <summary>
/// E23b: the Windows port of DocumentPreparation.swift's queue side. One import becomes one durable
/// DocumentJob (Receiving → Parsing → OCR/文件代读 → Validating → Ready/Warning/Failed); the Codex
/// provider really runs the answer-service document reader inside that job instead of only queueing.
/// </summary>
public sealed class DocumentPreparation(AppPaths paths, IAnswerClient? codexClient = null)
{
    public DocumentJobQueue Queue { get; } = new(Path.Combine(paths.Root, "document-jobs"));

    private MaterialStore? store;
    private MaterialStore Store => store ??= new MaterialStore(paths, paths);

    public async Task<MaterialDocument> ImportAsync(string file, DocumentPreparationOptions options, CancellationToken ct = default)
    {
        var codex = options.Codex ?? new CodexDocumentOptions();
        var job = DocumentJob.Create("", Path.GetFileName(file), provider: options.Provider, model: codex.Model,
            effort: codex.Effort, fallback: options.Fallback, timeout: codex.TimeoutSeconds)
            with { Stage = DocumentJob.StageValue.Receiving, UpdatedAt = DateTimeOffset.UtcNow };
        Queue.Enqueue(job);
        void Stage(DocumentJob.StageValue stage, int completed = 0, int? total = null, string message = "")
        {
            job = job with { Stage = stage, Completed = completed, Total = total, Message = message, UpdatedAt = DateTimeOffset.UtcNow };
            Queue.Update(job);
        }
        Stage(DocumentJob.StageValue.Parsing, message: "格式解析");
        try
        {
            IReadOnlyList<DocumentBlock>? blocks = null;
            IReadOnlyList<string>? warnings = null;
            string? reader = null;
            var enhanced = false;
            if (options.Provider == "codex")
            {
                Stage(DocumentJob.StageValue.Ocr, message: "Codex 文件代读");
                try
                {
                    var progress = new Progress<(int Completed, int Total, string Message)>(value =>
                        Stage(DocumentJob.StageValue.Ocr, value.Completed, value.Total, value.Message));
                    (blocks, warnings) = await CodexDocumentReader.ReadAsync(file, codex, codexClient, progress, ct).ConfigureAwait(false);
                    reader = "codex-" + codex.Model;
                }
                catch (Exception exception) when (options.Fallback && exception is not OperationCanceledException)
                {
                    warnings = ["Codex 文件代读失败，已改用系统识别：" + exception.Message];
                    blocks = null;
                    reader = null;
                }
            }
            else
            {
                enhanced = DocumentAssets.UseEnhancedOcr(paths, options.Provider, options.Fallback);
            }
            Stage(DocumentJob.StageValue.Validating, message: "内容检查");
            var document = await Store.ImportAsync(file, ct, enhanced, blocks, warnings, reader).ConfigureAwait(false);
            var stage = document.Warnings.Count > 0 ? DocumentJob.StageValue.Warning : DocumentJob.StageValue.Ready;
            job = job with { DocumentId = document.Id, Stage = stage, Message = DocumentJobStages.Title(stage), UpdatedAt = DateTimeOffset.UtcNow };
            Queue.Update(job);
            return document;
        }
        catch (OperationCanceledException)
        {
            Stage(DocumentJob.StageValue.Cancelled, message: "已取消");
            throw;
        }
        catch (Exception exception)
        {
            Stage(DocumentJob.StageValue.Failed, message: exception.Message);
            throw;
        }
    }
}
