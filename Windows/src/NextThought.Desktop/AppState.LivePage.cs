using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>Mac LiveNotesEngine.isEnabled: whether this meeting is keeping live notes.</summary>
    [ObservableProperty] private bool liveNotesEnabled;

    /// <summary>The tab is only offered as the meeting's own tab while it is on (Mac onAppear).</summary>
    public string LiveNotesStatusText => !LiveNotesEnabled
        ? UiText.T("实时纪要没有开")
        : !AnswerEngineReady
            ? UiText.T("还没有连接回答服务，写不了实时纪要")
            : UiText.T("有了内容就开始整理");

    /// <summary>Mac LiveNotesColumn: the empty state, in its on and off wording.</summary>
    public string LiveNotesHintText => LiveNotesEnabled
        ? UiText.T("说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。")
        : UiText.T("这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。");

    /// <summary>Mac: 现在更新 while live notes are on, 开启实时纪要 while they are off.</summary>
    public string LiveNotesButtonText => LiveNotesEnabled ? UiText.T("现在更新") : UiText.T("开启实时纪要");

    /// <summary>现在更新 needs the answer engine; enabling is always possible.</summary>
    public bool LiveNotesCanUpdate => !LiveNotesEnabled || AnswerEngineReady;

    public void RaiseLiveNotesTabUi()
    {
        // LiveNotesEnabled itself is raised by its generated setter; raising it here as well would
        // loop through LiveView's property-changed handler.
        OnPropertyChanged(nameof(LiveNotesStatusText));
        OnPropertyChanged(nameof(LiveNotesHintText));
        OnPropertyChanged(nameof(LiveNotesButtonText));
        OnPropertyChanged(nameof(LiveNotesCanUpdate));
    }

    /// <summary>Mac 开启实时纪要 button: turns the engine on for this meeting.</summary>
    public void EnableLiveNotes()
    {
        if (LiveNotesEnabled) return;
        LiveNotesEnabled = true;
        RaiseLiveNotesTabUi();
        SetStatus(UiText.T("已开启实时纪要：大约每 45 秒整理一次。"));
    }

    /// <summary>Mac 现在更新: asks for a rewrite now (the engine is J1; the state is reported).</summary>
    public void UpdateLiveNotesNow()
    {
        if (!LiveNotesEnabled) return;
        if (!AnswerEngineReady)
        {
            SetStatus(UiText.T("还没有连接回答服务，写不了实时纪要"), true);
            return;
        }
        SetStatus(UiText.T("正在更新实时纪要…"));
    }

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
}
