using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E19: the export sheet's data. The sheet picks 格式 / 转写版本 / 语言; this partial loads the same
/// archived projection the notes page shows and writes the selected format (Mac SessionExportSheet
/// + SessionFileExporter).
/// </summary>
public partial class AppState
{
    private SessionMeta? exportMeta;
    private IReadOnlyList<TranscriptLine> exportLines = [];
    private IReadOnlyList<SessionProjectedAnswer> exportAnswers = [];
    private IReadOnlyList<TranscriptCorrection> exportCorrections = [];
    private string exportSummaryMarkdown = "";
    private string exportModeTitle = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string sessionExportStatus = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool sessionExportBusy;

    public bool HasSessionExport => exportMeta is not null;
    public string SessionExportTitle => exportMeta?.Title ?? "";
    public string SessionExportSubtitle => exportMeta is null
        ? ""
        : exportMeta.LocalStartedAt + (exportMeta.SummaryState == "ready" ? " · " + UiText.T("有纪要") : "");
    public string SessionExportStatusText => SessionExportStatus;

    /// <summary>Opens the sheet for the selected meeting; false when nothing can be exported.</summary>
    public bool PrepareSessionExport()
    {
        if (CurrentSession is not { } meta) return false;
        try
        {
            var data = Sessions.Load(meta.Id);
            exportMeta = data.Meta;
            exportLines = data.Lines;
            exportSummaryMarkdown = data.Summary;
            exportAnswers = data.Answers.Select(answer => new SessionProjectedAnswer(
                answer.Id, answer.Date, answer.Question, answer.Answer, "", null)).ToList();
            try
            {
                var userContent = SessionUserContentStore.Load(Sessions.Folder(data.Meta.Id));
                exportCorrections = userContent.Corrections([], data.Lines);
            }
            catch { exportCorrections = []; }
            exportModeTitle = data.Meta.Mode;
            SessionExportStatus = "";
            OnPropertyChanged(nameof(HasSessionExport));
            OnPropertyChanged(nameof(SessionExportTitle));
            OnPropertyChanged(nameof(SessionExportSubtitle));
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(UiText.T("无法读取这场会议，暂时不能导出。"), true);
            SessionExportStatus = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// SessionExportPreviewContent.make: the preview and the file come from this single projection,
    /// so switching 转写版本 / 语言 re-renders both consistently.
    /// </summary>
    public string BuildSessionExportText(SessionExportFormat format, SessionTranscriptVariant transcriptVariant,
        SessionLanguageView languageView)
    {
        if (exportMeta is not { } meta) return "";
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, exportLines, exportAnswers,
            summary: new SessionProjectedSummary(meta.Title, exportSummaryMarkdown),
            options: new SessionProjectionOptions
            {
                TranscriptVariant = transcriptVariant,
                LanguageView = languageView
            },
            corrections: exportCorrections, meta: meta);
        var duration = (meta.EndedAt ?? meta.StartedAt) - meta.StartedAt;
        return SessionExportContent.Build(format, projection, meta, exportLines, exportModeTitle,
            duration.TotalSeconds, meta.AudioSource);
    }

    /// <summary>Mac SessionFileExporter.write: Markdown/字幕 are text, Word/PDF go through the renderers.</summary>
    public async Task<bool> SaveSessionExportAsync(SessionExportFormat format,
        SessionTranscriptVariant transcriptVariant, SessionLanguageView languageView, string path)
    {
        if (exportMeta is not { } meta) return false;
        SessionExportBusy = true;
        try
        {
            await Task.WhenAll(writes.ToArray());
            var text = BuildSessionExportText(format, transcriptVariant, languageView);
            if (text.Length == 0)
            {
                SessionExportStatus = UiText.T("没有可导出的内容。");
                return false;
            }
            await Task.Run(() =>
            {
                switch (format)
                {
                    case SessionExportFormat.Docx:
                        DocxWriter.Write(text, path);
                        break;
                    case SessionExportFormat.Pdf:
                        PdfWriter.Write(text, path, meta.Title);
                        break;
                    default:
                        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                        break;
                }
            });
            SessionExportStatus = UiText.Format("已导出：%@", Path.GetFileName(path));
            SetStatus(SessionExportStatus);
            return true;
        }
        catch (Exception ex)
        {
            SessionExportStatus = UiText.Format("导出失败：%@", ex.Message);
            SetStatus(SessionExportStatus, true);
            return false;
        }
        finally
        {
            SessionExportBusy = false;
        }
    }

    /// <summary>The sheet's suggested file name (Mac baseName + format extension).</summary>
    public string SuggestedSessionExportFileName(SessionExportFormat format) =>
        SessionExportContent.SuggestedFileName(exportMeta?.Title ?? "", format);

    /// <summary>Mac SessionExportSheet shows the subtitle timing/translation warnings before export.</summary>
    public (int Estimated, int StaleTranslations) SessionExportSubtitleWarnings(SessionTranscriptVariant variant,
        SessionLanguageView languageView)
    {
        if (exportMeta is not { } meta) return (0, 0);
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, exportLines, exportAnswers,
            options: new SessionProjectionOptions { TranscriptVariant = variant, LanguageView = languageView },
            corrections: exportCorrections, meta: meta);
        var export = SessionSubtitleExporter.ExportProjected(meta, projection.Transcript, SessionSubtitleFormat.Srt,
            SessionExportContent.SubtitleContent(languageView));
        return (export.EstimatedTimingCount, export.StaleTranslationCount);
    }

    /// <summary>Mac shows a read-issue banner and pauses export when the archive is damaged.</summary>
    public string SessionExportReadIssue
    {
        get
        {
            if (exportMeta is null) return "";
            try
            {
                var data = Sessions.Load(exportMeta.Id);
                return data.SkippedLines > 0
                    ? UiText.Format("归档有缺失或读取错误（跳过 %lld 行），导出已暂停。", data.SkippedLines)
                    : "";
            }
            catch (Exception ex) { return ex.Message; }
        }
    }

    /// <summary>Notes page button: open the sheet for the meeting currently shown.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenSessionExport() => RequestSessionExport?.Invoke();

    /// <summary>Set by the shell: opens the export sheet as a window.</summary>
    public Action? RequestSessionExport { get; set; }
}
