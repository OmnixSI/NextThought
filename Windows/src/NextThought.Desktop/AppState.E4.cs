using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E4 leftover live-page surfaces from Mac LiveSessionView/WebSearch: the three-state web-search
/// mode, the background-status dot (answer service not ready is the yellow one), the transparency
/// counters in the transcript status line, and the scroll-follow banner.
/// </summary>
public partial class AppState
{
    // ---- 三态联网模式 (Mac WebSearchSettings 联网: 关 / 自动判断 / 总是) -------------------

    private WebSearchMode LiveWebSearchMode => WebSearchModes.Parse(Settings.WebSearchMode);

    public string LiveWebSearchModeLabel => UiText.T(LiveWebSearchMode.Title());

    public bool LiveWebSearchIsOff => LiveWebSearchMode == WebSearchMode.Off;
    public bool LiveWebSearchIsAuto => LiveWebSearchMode == WebSearchMode.Auto;
    public bool LiveWebSearchIsAlways => LiveWebSearchMode == WebSearchMode.Always;

    /// <summary>Mac webSearchMode: off / auto / always; saved immediately.</summary>
    [RelayCommand]
    private void SetLiveWebSearchMode(string? mode)
    {
        var parsed = WebSearchModes.Parse(mode);
        var value = parsed.Value();
        if (Settings.WebSearchMode == value) return;
        Settings.WebSearchMode = value;
        RaiseWebSearchModeUi();
        _ = Settings.SaveAsync();
        SetStatus(UiText.T("联网") + " · " + UiText.T(parsed.Title()));
    }

    public void RaiseWebSearchModeUi()
    {
        OnPropertyChanged(nameof(LiveWebSearchModeLabel));
        OnPropertyChanged(nameof(LiveWebSearchIsOff));
        OnPropertyChanged(nameof(LiveWebSearchIsAuto));
        OnPropertyChanged(nameof(LiveWebSearchIsAlways));
    }

    /// <summary>E4: mode first, then the question's local rules; a configured provider is required.</summary>
    private bool ShouldSearchWeb(string question) => LiveWebSearchMode.ShouldSearch(
        allowed: Settings.WebSearch && WebSearch() is not null,
        rulesNeedSearch: WebSearchNeed.Assess(question).NeedsSearch);

    // ---- 背景状态点 (Mac companion/screen/translation dot + answerEngineReady) -----------

    /// <summary>Approximation of Mac answerEngineReady: an API key or the local Codex account.</summary>
    public bool AnswerEngineReady => Settings.UseCodex || Settings.HasApiKey;

    public bool LiveBackgroundDotVisible => LiveCompanionOn || LiveScreenWatchOn || LiveTranslationEnabled || !AnswerEngineReady;

    public string LiveBackgroundDotHelp => AnswerEngineReady
        ? UiText.T("状态")
        : UiText.T("还没有连接回答服务，听到问题也不会自动回答");

    // ---- 转写状态行的透明计数 (Mac TranscriptStatus) ------------------------------------

    [ObservableProperty] private int echoDroppedCount;
    [ObservableProperty] private int droppedAudioSegments;

    public bool HasEchoDropped => EchoDroppedCount > 0;
    public bool HasDroppedAudioSegments => DroppedAudioSegments > 0;
    public string EchoDroppedText => HasEchoDropped ? UiText.Format("· %lld 句回声已忽略", EchoDroppedCount) : "";
    public string DroppedSegmentsText => HasDroppedAudioSegments ? UiText.Format("· 遗漏 %lld 段", DroppedAudioSegments) : "";
    public string LiveMicOffText => !Settings.CaptureMicrophone && Recording
        ? UiText.T("· 麦克风未开启，不会记下你说的话")
        : "";

    public bool HasLiveMicOff => LiveMicOffText.Length > 0;

    partial void OnEchoDroppedCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasEchoDropped));
        OnPropertyChanged(nameof(EchoDroppedText));
    }

    partial void OnDroppedAudioSegmentsChanged(int value)
    {
        OnPropertyChanged(nameof(HasDroppedAudioSegments));
        OnPropertyChanged(nameof(DroppedSegmentsText));
    }

    private void NoteEchoDropped() => EchoDroppedCount++;

    private void NoteDroppedAudioSegment(int count = 1) => DroppedAudioSegments += Math.Max(1, count);

    private void ResetLiveStatusCounters()
    {
        EchoDroppedCount = 0;
        DroppedAudioSegments = 0;
        OnPropertyChanged(nameof(LiveMicOffText));
        OnPropertyChanged(nameof(HasLiveMicOff));
    }

    public void RaiseLiveStatusCountersUi()
    {
        OnPropertyChanged(nameof(LiveMicOffText));
        OnPropertyChanged(nameof(HasLiveMicOff));
    }

    // ---- 滚动跟随 / 「新增 N 句 · 回到现场」 (Mac ScrollFollowState) --------------------

    private readonly ScrollFollowState liveFollow = new();

    [ObservableProperty] private bool liveFollowPaused;
    [ObservableProperty] private string liveFollowBannerText = "";

    public bool HasLiveFollowBanner => LiveFollowPaused;

    public event Action? LiveFollowScrollRequested;

    private void RefreshLiveFollowUi()
    {
        LiveFollowPaused = !liveFollow.IsFollowing;
        LiveFollowBannerText = !LiveFollowPaused
            ? ""
            : liveFollow.PendingItemCount > 0
                ? UiText.T("已暂停跟随（点按回到现场）") + " · " + liveFollow.PendingItemCount
                : UiText.T("已暂停跟随（点按回到现场）");
        OnPropertyChanged(nameof(HasLiveFollowBanner));
    }

    /// <summary>Called by the live page when the transcript grew while the user is reading history.</summary>
    public void LiveTranscriptGrew(int count = 1)
    {
        liveFollow.ReceivedNewContent(count);
        RefreshLiveFollowUi();
    }

    public void LiveScrolledAway()
    {
        liveFollow.UserScrolledAway();
        RefreshLiveFollowUi();
    }

    public void LiveReachedLatest()
    {
        liveFollow.ReachedLatest();
        RefreshLiveFollowUi();
    }

    public void LiveSelectedText()
    {
        liveFollow.UserSelectedText();
        RefreshLiveFollowUi();
    }

    /// <summary>True when the page should keep its viewport on the newest line.</summary>
    public bool LiveIsFollowingTranscript => liveFollow.IsFollowing;

    /// <summary>「回到现场」: restore the live edge, clear the unread count, and scroll.</summary>
    [RelayCommand]
    private void ReturnLiveToFollow()
    {
        liveFollow.ReturnToLatest();
        RefreshLiveFollowUi();
        LiveFollowScrollRequested?.Invoke();
    }

    private void BeginLiveFollow()
    {
        liveFollow.ReturnToLatest();
        RefreshLiveFollowUi();
    }
}
