using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// 任务书 §6.5 / E4: the live page surfaces that belong to the page rather than the answer pipeline —
/// the three-tab right column (实时纪要 / 问答 / 我的笔记) with the Mac 实时纪要 empty state, the
/// 「是我说的」 echo restore, and the transcript status line's microphone/engine pieces.
/// The live-notes engine itself is J1; until it lands the tab shows the same states Mac shows before
/// its first update (off / no answer service), with the Mac copy verbatim.
/// </summary>
public partial class AppState
{
    // ---- 右栏: 实时纪要 tab (Mac LiveNotesColumn) --------------------------------------
    // The engine itself is AppState.LiveNotes.cs (J1): status, content rows and the demo notes.

    // ---- 左栏: 是我说的 (Mac markNotEcho) ---------------------------------------------

    /// <summary>
    /// Mac markNotEcho: the line was ignored as speaker echo; if it was mine, put it back into the
    /// conversation and take one off the echo count.
    /// </summary>
    public void MarkNotEcho(Guid lineId)
    {
        var row = Transcript.FirstOrDefault(item => item.Id == lineId);
        if (row is null || !row.IsEcho) return;
        row.RestoreEcho();
        for (var index = 0; index < currentLines.Count; index++)
        {
            if (currentLines[index].Id != lineId) continue;
            currentLines[index] = currentLines[index] with { SuspectedEcho = null };
            break;
        }
        EchoDroppedCount = Math.Max(0, EchoDroppedCount - 1);
        SetStatus(UiText.T("已把这一句算作我说的。"));
    }

    // ---- 左栏: 状态行 (Mac TranscriptStatus) -------------------------------------------

    /// <summary>Mac: 「· 麦克风」 only while the microphone is part of this meeting's audio.</summary>
    public bool LiveUsesMicrophone => Settings.CaptureMicrophone;

    /// <summary>The 「· 麦克风」 fragment of the status line (Mac TranscriptStatus).</summary>
    public string LiveMicrophoneSuffix => UiText.T("· 麦克风");

    // ---- 说话人 (J6/Mac SpeakerStrip + LineSpeakerMenu) --------------------------------

    /// <summary>Mac SpeakerStrip.isShown: people appear once the voices have been told apart.</summary>
    public bool LiveSpeakerStripVisible => SpeakerPeople.Count > 0;

    /// <summary>Mac layout.asksForMe: no voice is marked as the user yet.</summary>
    public bool LiveAsksForMe => SpeakerPeople.Count > 0 && SpeakerPeople.All(person => !person.IsMe);

    /// <summary>The hint beside the strip (Mac SpeakerStrip's trailing caption).</summary>
    public string LiveSpeakerStripHint => UiText.T("点名字改名、合并或标成我");

    /// <summary>J6/Mac voiceNumbers: refresh the strip and the speaker names on the transcript rows.</summary>
    public void ReloadLiveSpeakerStrip()
    {
        ReloadSpeakerPeople();
        RefreshLiveSpeakerNames();
        OnPropertyChanged(nameof(LiveSpeakerStripVisible));
        OnPropertyChanged(nameof(LiveAsksForMe));
    }

    /// <summary>J6: put each live row's recognised voice name (or 我) into the speaker column.</summary>
    public void RefreshLiveSpeakerNames()
    {
        var meta = CurrentSession;
        var meVoices = meta?.MeVoices?.ToHashSet() ?? [];
        var lines = currentLines;
        var meLines = SpeakerLabels.MeLines(lines, meVoices);
        var numbered = SpeakerLabels.Names(meta?.VoiceNames, lines, meVoices);
        var numbers = SpeakerLabels.Numbers(lines, meVoices);
        foreach (var row in Transcript)
        {
            var isMe = meLines.Contains(row.Id);
            var name = row.Voice is { } voice && numbers.TryGetValue(row.Id, out var number)
                ? named(number, numbered, voice)
                : null;
            row.ApplyLiveIdentity(name, isMe);
        }

        string? named(int number, IReadOnlyDictionary<int, string> names, int voice) =>
            names.TryGetValue(number, out var value) && value.Length > 0
                ? value
                : meta?.VoiceNames is { } direct
                  && direct.TryGetValue(voice.ToString(System.Globalization.CultureInfo.InvariantCulture), out var own)
                    ? own
                    : null;
    }

    /// <summary>
    /// J6/Mac assignLine: one sentence moves to the user, a chosen voice, or a new person. Only
    /// voices.jsonl gets a tag and meta.json gets voiceNames/meVoices — transcript.jsonl stays byte
    /// for byte the recognised text (Mac keeps the raw transcript untouched).
    /// </summary>
    public async Task AssignLiveLineSpeakerAsync(Guid lineId, LineSpeaker target)
    {
        if (CurrentSession is not { } meta)
        {
            SetStatus(UiText.T("先开始记录，再改这一句的说话人。"));
            return;
        }
        try
        {
            var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, currentLines, lineId, target);
            await EnqueueVoiceTag(meta.Id, lineId, tag.Voice);
            var name = updated.VoiceNames is { } names
                       && names.TryGetValue(tag.Voice.ToString(System.Globalization.CultureInfo.InvariantCulture), out var value)
                ? value
                : null;
            var isMe = updated.MeVoices?.Contains(tag.Voice) == true;
            await Sessions.SetVoiceLabelAsync(meta.Id, tag.Voice, name, isMe);
            for (var index = 0; index < currentLines.Count; index++)
            {
                if (currentLines[index].Id == lineId)
                {
                    currentLines[index] = currentLines[index] with { Voice = tag.Voice };
                    break;
                }
            }
            var row = Transcript.FirstOrDefault(item => item.Id == lineId);
            row?.MoveToVoice(tag.Voice);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ReloadLiveSpeakerStrip();
            SetStatus(UiText.Format("这一句改成了%@。", isMe
                ? UiText.T("我")
                : name is { Length: > 0 } ? name : UiText.Format("说话人 %lld", tag.Voice + 1)));
        }
        catch (Exception ex)
        {
            SetStatus(UiText.T("说话人修改失败：") + ex.Message, true);
        }
    }

    /// <summary>J6/Mac markVoice: the chosen voice is (or is no longer) the user's.</summary>
    public async Task MarkLiveVoiceAsMeAsync(int voice, bool isMe)
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            await Sessions.SetVoiceLabelAsync(meta.Id, voice, null, isMe);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ReloadLiveSpeakerStrip();
        }
        catch (Exception ex) { SetStatus(UiText.T("保存说话人设置失败：") + ex.Message, true); }
    }
}
