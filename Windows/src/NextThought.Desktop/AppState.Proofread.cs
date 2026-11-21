using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Desktop.Views;

namespace NextThought.Desktop;

/// <summary>校对转写：模型只改明确判定的识别错误，改动可审计（写入 corrections.json）。</summary>
public partial class AppState
{
    [ObservableProperty] private bool proofreading;

    [ObservableProperty] private string proofreadStatus = "";

    public bool HasProofreadStatus => ProofreadStatus.Length > 0;

    partial void OnProofreadStatusChanged(string value) => OnPropertyChanged(nameof(HasProofreadStatus));

    [RelayCommand(CanExecute = nameof(CanProofread))]
    private async Task ProofreadTranscriptAsync()
    {
        if (CurrentSession is not { } meta || currentLines.Count == 0) return;
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        Answering = Proofreading = true;
        ProgressIndeterminate = true;
        RaiseBusy();
        try
        {
            await Task.WhenAll(writes.ToArray());
            var original = currentLines.ToList();
            var transcript = ProofreadPrompt.Transcript(original, meta.StartedAt, numbered: true);
            const int maxCharacters = 12000;
            var truncated = transcript.Length > maxCharacters;
            if (truncated) transcript = transcript[..maxCharacters];
            SetStatus("正在校对转写…");
            var reply = await CurrentClient().AnswerAsync(AnswerOptions(false), ProofreadPrompt.System(),
                ProofreadPrompt.User(transcript, GlossaryHotwords()), null, cancellation.Token, 180);
            if (generation != requestEpoch) return;
            var raw = NotesCorrectionsJson.Parse(reply);
            var valid = NotesCorrections.Valid(raw, original);
            if (valid.Count == 0)
            {
                ProofreadStatus = UiText.T("没有需要更正的地方。");
                SetStatus(UiText.T("校对完成：没有需要更正的地方。"));
                return;
            }
            var corrected = NotesCorrections.Apply(valid, original);
            currentLines.Clear();
            currentLines.AddRange(corrected);
            Transcript.Clear();
            foreach (var line in corrected) Transcript.Add(new TranscriptRow(line));
            // Non-destructive like Mac: transcript.jsonl stays raw, decisions live in user-content.json.
            var folder = Sessions.Folder(meta.Id);
            var decisions = valid.Select(correction =>
            {
                var line = correction.LineId is { } lineId
                    ? original.FirstOrDefault(item => item.Id == lineId)
                    : (correction.Line >= 0 && correction.Line < original.Count ? original[correction.Line] : null);
                return new SessionCorrectionDecision(null, line?.Id ?? Guid.NewGuid(), correction.Original, correction.Corrected, false);
            }).ToList();
            try
            {
                var content = SessionUserContentStore.Load(folder);
                SessionUserContentStore.Save(content with
                {
                    CorrectionDecisions = [.. content.CorrectionDecisions, .. decisions]
                }, folder);
                await File.WriteAllTextAsync(Path.Combine(folder, "corrections.json"),
                    NotesCorrectionsJson.ToJson(valid), cancellation.Token);
            }
            catch (Exception ex)
            {
                SetStatus(UiText.T("转写已在界面更正，但用户更正记录写入失败：") + ex.Message, true);
            }
            ProofreadStatus = UiText.Format("更正 %lld 处", valid.Count)
                + (truncated ? UiText.Format("（会议较长，只校对了前 %lld 字）", maxCharacters) : "");
            SetStatus(NextThought.Core.UiText.EnglishUi
                ? $"Proofreading finished: {valid.Count} recognition errors corrected in the view; the raw transcript is unchanged and decisions live in user-content.json."
                : $"校对完成：{valid.Count} 处识别错误已在显示中更正；原始转写未改写，决定存于 user-content.json。");
        }
        catch (OperationCanceledException)
        {
            if (generation == requestEpoch) SetStatus(UiText.T("校对已取消。"));
        }
        catch (Exception ex)
        {
            if (generation == requestEpoch) SetStatus(ex.Message, true);
        }
        finally
        {
            cancellation.Dispose();
            if (generation == requestEpoch)
            {
                requestCancellation = null;
                Answering = Proofreading = false;
                ProgressIndeterminate = false;
                RaiseBusy();
            }
        }
    }

    private bool CanProofread() => IsIdle && !Answering && CurrentSession is not null && currentLines.Count > 0;

    // MARK: - E17 校正原话（Mac NotesPageView.swift CorrectedTranscript / HistoryView.swift「校正原话」）

    /// <summary>
    /// E17: one row per transcript line in the <see cref="CorrectedTranscript"/> window. Built from
    /// the raw transcript plus the user's correction decisions, so reverting a correction is a
    /// projection change and never rewrites transcript.jsonl (Mac does the same).
    /// </summary>
    public ObservableCollection<CorrectedTranscriptRow> CorrectedTranscriptRows { get; } = [];

    [ObservableProperty] private bool correctedTranscriptShowOriginal;
    /// <summary>task-78 notes 5: the corrected transcript renders inside the 转写 tab.</summary>
    [ObservableProperty] private bool correctedTranscriptInline;
    [ObservableProperty] private string correctedTranscriptFind = "";
    [ObservableProperty] private string correctedTranscriptCountText = "";
    [ObservableProperty] private string correctedTranscriptStatus = "";

    public bool HasCorrectedTranscriptRows => CorrectedTranscriptRows.Count > 0;

    public bool CorrectedTranscriptShowCorrected => !CorrectedTranscriptShowOriginal;

    public bool HasCorrectedTranscriptCount => CorrectedTranscriptCountText.Length > 0;

    public bool HasCorrectedTranscriptStatus => CorrectedTranscriptStatus.Length > 0;

    partial void OnCorrectedTranscriptInlineChanged(bool value)
    {
        if (value) RefreshCorrectedTranscript();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleInlineCorrectedTranscript() => CorrectedTranscriptInline = !CorrectedTranscriptInline;

    partial void OnCorrectedTranscriptShowOriginalChanged(bool value)
    {
        var index = value ? 1 : 0;
        if (CorrectedTranscriptVersionIndex != index) CorrectedTranscriptVersionIndex = index;
        OnPropertyChanged(nameof(CorrectedTranscriptShowCorrected));
        RefreshCorrectedTranscript();
    }

    partial void OnCorrectedTranscriptFindChanged(string value) => RefreshCorrectedTranscript();

    partial void OnCorrectedTranscriptCountTextChanged(string value) => OnPropertyChanged(nameof(HasCorrectedTranscriptCount));

    partial void OnCorrectedTranscriptStatusChanged(string value) => OnPropertyChanged(nameof(HasCorrectedTranscriptStatus));

    /// <summary>Mac: 转写版本 segment (校正转写 / 原始转写).</summary>
    [RelayCommand]
    private void SetCorrectedTranscriptVersion(string? version) =>
        CorrectedTranscriptShowOriginal = version == "original";

    /// <summary>Opens the 校正原话 window (the Notes page's 转写 tab has the matching button).</summary>
    [RelayCommand]
    private void OpenCorrectedTranscript() => Views.CorrectedTranscript.ShowFor(this);

    /// <summary>
    /// Rebuilds the rows like Mac's SessionTextProjection: raw transcript → user decisions +
    /// model corrections → corrected text; the original text stays one toggle away.
    /// </summary>
    public void RefreshCorrectedTranscript()
    {
        CorrectedTranscriptRows.Clear();
        if (CurrentSession is null)
        {
            UpdateCorrectedTranscriptSummary(0, 0);
            OnPropertyChanged(nameof(HasCorrectedTranscriptRows));
            return;
        }
        IReadOnlyList<TranscriptLine> rawLines;
        DateTimeOffset startedAt;
        IReadOnlyList<TranscriptCorrection> corrections;
        if (StartupOptions.Demo)
        {
            // The demo session lives only in memory (nothing is written to the data directory),
            // so the window shows the demo meeting's transcript with sample decisions for the
            // E17 screenshot. See DemoCorrectedTranscriptCorrections below.
            rawLines = demoData?.Review.Lines ?? currentLines.ToList();
            startedAt = demoData?.Review.Meta.StartedAt ?? CurrentSession.StartedAt;
            corrections = DemoCorrectedTranscriptCorrections(rawLines);
        }
        else
        {
            SessionData data;
            try { data = Sessions.Load(CurrentSession.Id); }
            catch
            {
                UpdateCorrectedTranscriptSummary(0, 0);
                OnPropertyChanged(nameof(HasCorrectedTranscriptRows));
                return;
            }
            rawLines = data.Lines;
            startedAt = data.Meta.StartedAt;
            try
            {
                var content = SessionUserContentStore.Load(Sessions.Folder(data.Meta.Id));
                corrections = content.Corrections([], rawLines);
            }
            catch
            {
                corrections = [];
            }
        }
        var correctedLines = NotesCorrections.Apply(corrections, rawLines);
        var correctedById = correctedLines.ToDictionary(line => line.Id);
        var activeFixes = corrections.Count(correction => !correction.Reverted);
        var find = CorrectedTranscriptFind.Trim();
        var matches = 0;
        foreach (var line in rawLines)
        {
            var lineCorrections = corrections.Where(correction => correction.LineId == line.Id).ToList();
            var correctedText = correctedById.TryGetValue(line.Id, out var correctedLine)
                ? correctedLine.Text
                : line.Text;
            if (find.Length > 0
                && !line.Text.Contains(find, StringComparison.OrdinalIgnoreCase)
                && !correctedText.Contains(find, StringComparison.OrdinalIgnoreCase)) continue;
            matches++;
            var shownCorrections = CorrectedTranscriptShowOriginal
                ? []
                : lineCorrections.Select(correction => new CorrectedTranscriptFix(
                    correction.Id,
                    correction.Reverted
                        ? UiText.Format("恢复校对：%@ → %@", correction.Original, correction.Corrected)
                        : UiText.Format("撤销校对：%@ → %@", correction.Corrected, correction.Original),
                    correction.Reverted)).ToList();
            CorrectedTranscriptRows.Add(new CorrectedTranscriptRow(
                line.Id,
                NotesTime.Text(Math.Max(0, (int)((line.StartedAt ?? line.Date) - startedAt).TotalSeconds)),
                SpeakerDisplayLabel(line),
                CorrectedTranscriptShowOriginal ? line.Text : correctedText,
                shownCorrections));
        }
        UpdateCorrectedTranscriptSummary(find.Length > 0 ? matches : 0, activeFixes);
        OnPropertyChanged(nameof(HasCorrectedTranscriptRows));
    }

    /// <summary>Mac: undo / redo one correction from the corrected transcript; transcript.jsonl is untouched.</summary>
    [RelayCommand]
    private void ToggleCorrectedTranscriptFix(CorrectedTranscriptFix? fix)
    {
        if (fix is null || StartupOptions.Demo || CurrentSession is not { } meta) return;
        var folder = Sessions.Folder(meta.Id);
        SessionUserContent content;
        try { content = SessionUserContentStore.Load(folder); }
        catch { return; }
        var decisions = content.CorrectionDecisions.ToList();
        var index = decisions.FindIndex(decision => decision.Id == fix.Id);
        if (index < 0) return;
        decisions[index] = decisions[index] with { Reverted = !decisions[index].Reverted };
        SessionUserContentStore.Save(content with { CorrectionDecisions = decisions }, folder);
        // Mac's projection re-renders the meeting too: keep the open transcript in step.
        try
        {
            var data = Sessions.Load(meta.Id);
            var corrections = (content with { CorrectionDecisions = decisions }).Corrections([], data.Lines);
            var lines = NotesCorrections.Apply(corrections, data.Lines);
            currentLines.Clear();
            currentLines.AddRange(lines);
            Transcript.Clear();
            foreach (var line in lines.TakeLast(400)) Transcript.Add(new TranscriptRow(line));
        }
        catch
        {
            // A failed reload still leaves the decision saved; the window refreshes below.
        }
        RefreshCorrectedTranscript();
    }

    private void UpdateCorrectedTranscriptSummary(int matches, int activeFixes)
    {
        CorrectedTranscriptCountText = matches > 0 ? UiText.Format("%lld 句", matches) : "";
        CorrectedTranscriptStatus = activeFixes > 0 ? UiText.Format("已校对 %lld 处识别错误", activeFixes) : "";
    }

    private static string SpeakerDisplayLabel(TranscriptLine line) => line.Speaker switch
    {
        "me" => UiText.T("我"),
        "room" => UiText.T("现场"),
        _ => UiText.T("对方")
    };

    /// <summary>
    /// E17 screenshot fixture: the Mac demo transcript has no saved proofreading decisions, so the
    /// demo-only window shows two sample decisions on demo lines. Never written to disk.
    /// </summary>
    private static IReadOnlyList<TranscriptCorrection> DemoCorrectedTranscriptCorrections(IReadOnlyList<TranscriptLine> lines)
    {
        var picks = new (string Original, string Corrected)[]
        {
            ("我主要负责订单服务", "我主要负责订单业务"),
            ("先更新数据库、再删除缓存", "先写数据库、再删缓存")
        };
        var corrections = new List<TranscriptCorrection>();
        foreach (var (original, corrected) in picks)
        {
            var index = lines.ToList().FindIndex(line => line.Text.Contains(original, StringComparison.Ordinal));
            if (index < 0) continue;
            var line = lines[index];
            corrections.Add(new TranscriptCorrection
            {
                Line = index,
                LineId = line.Id,
                Original = original,
                Corrected = corrected
            });
        }
        return corrections;
    }
}
