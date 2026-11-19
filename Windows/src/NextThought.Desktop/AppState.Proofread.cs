using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

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
                ProofreadStatus = "没有需要更正的地方。";
                SetStatus(NextThought.Core.UiText.EnglishUi ? "Proofreading finished: nothing to correct." : "校对完成：没有需要更正的地方。");
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
                SetStatus("转写已在界面更正，但用户更正记录写入失败：" + ex.Message, true);
            }
            ProofreadStatus = $"更正 {valid.Count} 处" + (truncated ? $"（会议较长，只校对了前 {maxCharacters} 字）" : "");
            SetStatus(NextThought.Core.UiText.EnglishUi
                ? $"Proofreading finished: {valid.Count} recognition errors corrected in the view; the raw transcript is unchanged and decisions live in user-content.json."
                : $"校对完成：{valid.Count} 处识别错误已在显示中更正；原始转写未改写，决定存于 user-content.json。");
        }
        catch (OperationCanceledException)
        {
            if (generation == requestEpoch) SetStatus("校对已取消。");
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
}
