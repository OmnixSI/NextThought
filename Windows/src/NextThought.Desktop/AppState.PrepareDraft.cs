using System.Collections.ObjectModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E2: the preparation page's draft (port of Mac Sources/NextThought/AppState+Prepare.swift).
/// The page edits one draft; starting the meeting builds the ported MeetingPlan from it.
/// </summary>
public partial class AppState
{
    private MeetingDraft? meetingDraft;

    public MeetingDraft? Draft => meetingDraft;

    /// <summary>Opens a draft for the preparation page (Mac beginNewMeeting).</summary>
    public void EnsureDraft(MeetingMode? scene = null)
    {
        if (meetingDraft is not null) return;
        var mode = scene ?? SelectedMode;
        var series = SelectedSeries is { } row ? workspace.Find(row.Id) : null;
        meetingDraft = new MeetingDraft
        {
            Mode = mode,
            Title = MeetingTitle,
            SeriesId = series?.Id,
            BaselineDocumentIds = series?.DocumentIds.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal),
            PreviousSessionId = PreviousTranscriptContext()?.Id,
            Carry = CarryPlan
        };
        if (scene is { } picked)
        {
            SelectedMode = picked;
            meetingDraft.TypeChosen = true;
        }
        RaisePrepareDraft();
    }

    public void DiscardDraft()
    {
        meetingDraft = null;
        RaisePrepareDraft();
    }

    // ---- title, type, guess ------------------------------------------------------------

    public string DraftTitle
    {
        get => meetingDraft?.Title ?? "";
        set
        {
            EnsureDraft();
            var draft = meetingDraft!;
            draft.Title = value;
            MeetingTitle = value;
            if (!draft.TypeChosen)
            {
                var guessed = MeetingTypeGuesser.Guess(value).ToMeetingMode();
                if (guessed != MeetingMode.General && guessed != draft.Mode)
                {
                    draft.Mode = guessed;
                    SelectedMode = guessed;
                }
            }
            RaisePrepareDraft();
        }
    }

    /// <summary>Mac shows "根据标题选好了，可以改" while the type was guessed, not picked.</summary>
    public bool DraftGuessNoteVisible =>
        meetingDraft is { TypeChosen: false } draft && MeetingTypeGuesser.Guess(draft.Title) != SessionMode.General;

    public bool DraftIsCustom => meetingDraft?.Mode == MeetingMode.Custom;

    public string DraftCustomTypeName
    {
        get => meetingDraft?.CustomTypeName ?? "";
        set
        {
            EnsureDraft();
            meetingDraft!.CustomTypeName = value;
            RaisePrepareDraft();
        }
    }

    private IReadOnlyList<SceneChoice>? draftBaseScenes;

    public IReadOnlyList<SceneChoice> DraftBaseScenes =>
        draftBaseScenes ??= Scenes.Where(choice => choice.Mode != MeetingMode.Custom).ToList();

    public string DraftCustomBaseTitle =>
        Scenes.FirstOrDefault(choice => choice.Mode == (meetingDraft?.CustomBaseMode ?? MeetingMode.General))?.Title
        ?? Scenes.FirstOrDefault()?.Title ?? "";

    public string DraftSummary => NextThought.Desktop.SceneSummary.Line(meetingDraft ?? new MeetingDraft { Mode = SelectedMode });

    public string DraftPromise => SelectedScene?.Promise ?? "";

    public IReadOnlyList<string> DraftMoments => SelectedScene?.Moments ?? [];

    public void SetDraftCustomName(string name)
    {
        EnsureDraft();
        meetingDraft!.CustomTypeName = name;
        RaisePrepareDraft();
    }

    public void SetDraftCustomBase(MeetingMode mode)
    {
        EnsureDraft();
        var draft = meetingDraft!;
        if (draft.Mode == MeetingMode.Custom && draft.CustomBaseMode == mode && draft.TypeChosen) return;
        draft.CustomBaseMode = mode;
        draft.Mode = MeetingMode.Custom;
        draft.TypeChosen = true;
        RaisePrepareDraft();
    }

    /// <summary>Mac setDraftType: the person picked a scene, so the title no longer guesses it.</summary>
    public void SetDraftType(MeetingMode mode)
    {
        EnsureDraft();
        var draft = meetingDraft!;
        draft.Mode = mode;
        draft.TypeChosen = true;
        SelectedMode = mode;
        RaisePrepareDraft();
    }

    /// <summary>Mac setDraftCustomType: switch to 自定义 and keep the chosen name/base.</summary>
    public void SetDraftCustomType(string? name = null, MeetingMode? baseMode = null)
    {
        EnsureDraft();
        var draft = meetingDraft!;
        if (name is not null) draft.CustomTypeName = name;
        if (baseMode is not null) draft.CustomBaseMode = baseMode.Value;
        draft.Mode = MeetingMode.Custom;
        draft.TypeChosen = true;
        SelectedMode = MeetingMode.Custom;
        RaisePrepareDraft();
    }

    // ---- venue ------------------------------------------------------------------------

    public bool DraftOnline
    {
        get => Settings.CaptureSystem;
        set => _ = SetVenueAsync(offline: !value);
    }

    public bool DraftOffline
    {
        get => !Settings.CaptureSystem;
        set => _ = SetVenueAsync(offline: value);
    }

    private async Task SetVenueAsync(bool offline)
    {
        if (Settings.CaptureSystem == offline)
        {
            // 线上 = system sound plus the microphone; 线下 = microphone only (Mac MeetingVenue).
            Settings.CaptureSystem = !offline;
            if (offline) Settings.CaptureMicrophone = true;
        }
        else
        {
            Settings.CaptureSystem = !offline;
            if (offline) Settings.CaptureMicrophone = true;
        }
        await Settings.SaveAsync();
        SetStatus(UiText.T(offline
            ? "线下会议只用麦克风，转写会标为“现场”。"
            : "线上会议听电脑里播放的声音。"));
        RaisePrepareDraft();
    }

    // ---- microphone / answer / phone checks -------------------------------------------

    public string PrepareMicLine => DraftOffline
        ? UiText.T("麦克风 · 现场")
        : Settings.CaptureMicrophone ? UiText.T("麦克风 · 我") : UiText.T("麦克风未开启");

    public bool PrepareShowUseMicrophone => !Settings.CaptureMicrophone || DraftOffline is false && !Settings.CaptureMicrophone;

    public bool PrepareShowRecordOnly => Settings.CaptureSystem;

    public string PrepareAudioProblem => PrepareAudioReady
        ? ""
        : UiText.T("还没有可用的语音识别；可以先只记录声音，或到设置准备语音组件。");

    public string PrepareAnswerLine => Settings.HasApiKey
        ? (string.IsNullOrWhiteSpace(Settings.Model) ? UiText.T("API · 未指定模型") : UiText.Format("API · %@", Settings.Model))
        : UiText.T("未配置 · 不影响记录");

    public string PrepareAnswerAction => Settings.HasApiKey ? UiText.T("更换") : UiText.T("去连接");

    public string PrepareEngineVerification => EngineTestPassed
        ? UiText.T("连接检查通过")
        : EngineTestFailed ? UiText.T("连接检查未通过 · 记录仍可继续")
        : EngineTestRunning ? UiText.T("正在检查 AI 服务…")
        : UiText.T("已配置 · 尚未检查连接");

    public string PreparePhoneLine => PreparePhoneReady
        ? (string.IsNullOrWhiteSpace(Settings.CompanionRelayUrl) ? UiText.T("已连接") : Settings.CompanionRelayUrl)
        : UiText.T("未连接");

    // ---- materials (Mac MaterialsBlock / FileRow) -------------------------------------

    public IReadOnlyList<PrepareMaterialRow> PrepareMaterialsRows
    {
        get
        {
            var series = SelectedSeries is { } row ? workspace.Find(row.Id) : null;
            var ids = series?.DocumentIds.ToHashSet(StringComparer.Ordinal);
            IEnumerable<MaterialDocument> documents = Library.Materials;
            if (ids is { Count: > 0 }) documents = documents.Where(document => ids.Contains(document.Id));
            var rows = new List<PrepareMaterialRow>();
            foreach (var document in documents)
            {
                var segments = document.Blocks.Count;
                var failed = segments == 0;
                var warning = document.Warnings.Count > 0 ? UiText.T(" · ") + UiText.Format("有 %lld 处提示", document.Warnings.Count) : "";
                var status = failed
                    ? UiText.T("还没有可用的文字")
                    : UiText.Format("%lld 段", segments) + warning;
                rows.Add(new PrepareMaterialRow(document.Name, status, !failed, failed));
            }
            return rows;
        }
    }

    public string PrepareMaterialsHeader => PreparedMaterialCount > 0
        ? UiText.Format("这场会用的资料 · %lld 份", PreparedMaterialCount)
        : UiText.T("把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「导入资料」。");

    /// <summary>Mac importDraftDocuments: drop or pick files on the preparation page.</summary>
    public async Task ImportDraftDocumentsAsync(IReadOnlyList<string> files)
    {
        var paths = files.Where(File.Exists).ToList();
        if (paths.Count == 0) return;
        await Library.ImportAsync(paths);
        RaisePrepareDraft();
        OnPropertyChanged(nameof(PrepareMaterialsRows));
        OnPropertyChanged(nameof(PrepareMaterialsHeader));
    }

    public void RefreshPrepareDraft()
    {
        if (meetingDraft is { } draft)
        {
            draft.SeriesId = SelectedSeries?.Id;
            if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series && draft.BaselineDocumentIds.Count == 0)
                draft.BaselineDocumentIds = series.DocumentIds.ToHashSet(StringComparer.Ordinal);
        }
        RaisePrepareDraft();
        OnPropertyChanged(nameof(PrepareMaterialsRows));
        OnPropertyChanged(nameof(PrepareMaterialsHeader));
    }

    private void RaisePrepareDraft()
    {
        OnPropertyChanged(nameof(Draft));
        OnPropertyChanged(nameof(DraftTitle));
        OnPropertyChanged(nameof(DraftGuessNoteVisible));
        OnPropertyChanged(nameof(DraftIsCustom));
        OnPropertyChanged(nameof(DraftCustomTypeName));
        OnPropertyChanged(nameof(DraftCustomBaseTitle));
        OnPropertyChanged(nameof(DraftSummary));
        OnPropertyChanged(nameof(DraftPromise));
        OnPropertyChanged(nameof(DraftMoments));
        OnPropertyChanged(nameof(DraftOnline));
        OnPropertyChanged(nameof(DraftOffline));
        OnPropertyChanged(nameof(DraftBaseScenes));
        OnPropertyChanged(nameof(PrepareMicLine));
        OnPropertyChanged(nameof(PrepareShowUseMicrophone));
        OnPropertyChanged(nameof(PrepareShowRecordOnly));
        OnPropertyChanged(nameof(PrepareAudioProblem));
        OnPropertyChanged(nameof(PrepareAnswerLine));
        OnPropertyChanged(nameof(PrepareAnswerAction));
        OnPropertyChanged(nameof(PrepareEngineVerification));
        OnPropertyChanged(nameof(PreparePhoneLine));
        OnPropertyChanged(nameof(PrepareMaterialsRows));
        OnPropertyChanged(nameof(PrepareMaterialsHeader));
        OnPropertyChanged(nameof(PrepareMaterialsLine));
        OnPropertyChanged(nameof(PrepareCarrySummary));
        OnPropertyChanged(nameof(PreparedMaterialCount));
        OnPropertyChanged(nameof(PrepareAudioReady));
        OnPropertyChanged(nameof(PrepareAnswerReady));
        OnPropertyChanged(nameof(PreparePhoneReady));
        // J2/§6.7: the reworked page's blocks (chips, carry card, feature picker, start bar) re-read.
        RaisePrepareLayout();
    }

    // ---- commands ---------------------------------------------------------------------

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SetDraftType(string mode)
    {
        if (Enum.TryParse<MeetingMode>(mode, ignoreCase: true, out var parsed)) SetDraftType(parsed);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SetDraftCustomType() => SetDraftCustomType(name: null, baseMode: null);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SetDraftCustomBase(string mode)
    {
        if (Enum.TryParse<MeetingMode>(mode, ignoreCase: true, out var parsed)) SetDraftCustomBase(parsed);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SetDraftVenue(string venue) => _ = SetVenueAsync(offline: venue == "offline");

    /// <summary>Mac continueWithoutMicrophone: record only the other side.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task ContinueWithoutMicrophoneAsync()
    {
        Settings.CaptureMicrophone = false;
        await Settings.SaveAsync();
        SetStatus(UiText.T("这场只记录对方的声音。想连自己的话一起记，可以重新打开麦克风。"));
        RaisePrepareDraft();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task UseMicrophoneAgainAsync()
    {
        Settings.CaptureMicrophone = true;
        await Settings.SaveAsync();
        SetStatus(UiText.T("麦克风已重新打开。"));
        RaisePrepareDraft();
    }

    /// <summary>Mac startPreparedMeeting: the draft becomes the plan, then the usual start runs.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task StartPreparedMeetingAsync()
    {
        EnsureDraft();
        var draft = meetingDraft!;
        MeetingTitle = draft.Title;
        SelectedMode = draft.Mode;
        if (draft.SeriesId is { } seriesId)
            SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == seriesId);
        if (!StartRecordingCommand.CanExecute(null))
        {
            SetStatus(UiText.T("还不能开始记录：请先在设置里准备语音组件。"), true);
            return;
        }
        StartRecordingCommand.Execute(null);
        await Task.CompletedTask;
    }

    public bool CanStartPreparedMeeting => PrepareAudioReady && IsIdle;

    /// <summary>The draft as the ported MeetingPlan (Mac draftMeetingPlan).</summary>
    public MeetingPlan? DraftMeetingPlan => meetingDraft is { } draft
        ? new MeetingPlan
        {
            Title = draft.Title.Trim().Length > 0 ? draft.Title.Trim() : null,
            Mode = draft.Mode,
            CustomTypeName = draft.Mode == MeetingMode.Custom ? draft.CustomTypeName : null,
            CustomBaseMode = draft.CustomBaseMode,
            Carry = draft.Carry,
            BaselineDocumentIds = draft.BaselineDocumentIds,
            PreviousSessionId = draft.SeriesId is null
                ? null
                : History.Where(row => workspace.Find(draft.SeriesId)?.SessionIds.Contains(row.Meta.Id.ToString()) == true)
                    .OrderByDescending(row => row.Meta.StartedAt)
                    .Select(row => row.Meta.Id.ToString())
                    .FirstOrDefault()
        }
        : null;
}
