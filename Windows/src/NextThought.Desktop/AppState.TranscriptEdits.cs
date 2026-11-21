using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-62/E16: right-click actions on the meeting page's 转写 tab — move one sentence to another
/// speaker and edit one sentence by hand. The data layer is core-parity's J6 port
/// (SessionSpeakerEdits / SessionTextEdits): recognised text stays in transcript.jsonl, a speaker
/// change is a voices.jsonl tag plus meta.json voiceNames/meVoices, and a text edit is a decision in
/// user-content.json's correctionDecisions.
/// </summary>
public partial class AppState
{
    /// <summary>Mac LineSpeakerMenu: 这句是谁说的 → 我 / 新说话人 / recognised people.</summary>
    public async Task AssignTranscriptLineSpeakerAsync(TranscriptRow? row, LineSpeaker target)
    {
        if (row is null || CurrentSession is not { } meta)
        {
            SetStatus(UiText.T("先开始记录，再改这一句的说话人。"));
            return;
        }
        try
        {
            var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, currentLines, row.Id, target);
            await Sessions.AppendVoiceTagAsync(meta.Id, row.Id, tag.Voice);
            var name = updated.VoiceNames is { } names
                       && names.TryGetValue(tag.Voice.ToString(System.Globalization.CultureInfo.InvariantCulture), out var value)
                ? value
                : null;
            var isMe = updated.MeVoices?.Contains(tag.Voice) == true;
            await Sessions.SetVoiceLabelAsync(meta.Id, tag.Voice, name, isMe);
            for (var index = 0; index < currentLines.Count; index++)
            {
                if (currentLines[index].Id != row.Id) continue;
                currentLines[index] = currentLines[index] with { Voice = tag.Voice };
                break;
            }
            row.MoveToVoice(tag.Voice);
            row.ApplySpeakerLabel(name, isMe);
            LoadSpeakerNames(meta.Id);
            OnPropertyChanged(nameof(Transcript));
        }
        catch (Exception error) { SetStatus(UiText.T("说话人修改失败：") + error.Message, true); }
    }

    /// <summary>Mac LineSpeakerMenu → 这句不是我说：put a voice's sentence back to me / take it away.</summary>
    public async Task MarkTranscriptVoiceAsMeAsync(int voice, bool isMe)
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            await Sessions.SetVoiceLabelAsync(meta.Id, voice, null, isMe);
            LoadSpeakerNames(meta.Id);
        }
        catch (Exception error) { SetStatus(UiText.T("保存说话人设置失败：") + error.Message, true); }
    }

    /// <summary>
    /// Mac editLine: rewrite one recognised sentence by hand. transcript.jsonl keeps the recognised
    /// text; user-content.json gets a whole-sentence correction decision and the page reloads the
    /// effective transcript (with the proofreading fixes and user decisions applied).
    /// </summary>
    public async Task SaveTranscriptLineEditAsync(TranscriptRow? row, string text)
    {
        if (row is null || CurrentSession is not { } meta) return;
        var corrected = text.Trim();
        if (corrected.Length == 0 || corrected == row.Text) return;
        try
        {
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            var raw = Sessions.Load(meta.Id).Lines;
            var updated = SessionTextEdits.EditLine(content, [], raw, row.Id, row.Text, corrected);
            SessionUserContentStore.Save(updated, folder);
            ReloadTranscriptAfterEdit(meta, updated);
        }
        catch (Exception error) { SetStatus(UiText.T("保存失败：") + error.Message, true); }
    }

    private void ReloadTranscriptAfterEdit(SessionMeta meta, SessionUserContent content)
    {
        var data = Sessions.Load(meta.Id);
        var lines = data.Lines;
        var corrections = content.Corrections([], lines);
        if (corrections.Count > 0) lines = NotesCorrections.Apply(corrections, lines);
        currentLines.Clear();
        currentLines.AddRange(lines);
        Transcript.Clear();
        foreach (var line in lines.TakeLast(400)) Transcript.Add(new TranscriptRow(line));
        LoadSpeakerNames(meta.Id);
        try { LoadMeetingNotes(content); } catch { }
        OnPropertyChanged(nameof(Transcript));
        OnPropertyChanged(nameof(HasTranscript));
    }
}
