using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E11/Mac LiveTranslationToggle + LiveTranslationHosts: the live-translation switch, its status,
/// the "heard foreign speech, turned on by itself" notice, and the two translation lanes shown
/// under the partial lines (system/remote and microphone).
/// </summary>
public partial class AppState
{
    private readonly LiveTranslationAutoOpen liveTranslationAutoOpen = new();

    [ObservableProperty] private bool liveTranslationEnabled;
    [ObservableProperty] private string liveTranslationStatus = "";
    [ObservableProperty] private string liveTranslationAutoNotice = "";

    public string LiveTranslationButtonLabel => LiveTranslationEnabled
        ? UiText.T("关闭同声传译")
        : UiText.T("开启同声传译");

    public string LiveTranslationTooltip => UiText.T("自动识别我和对方说的中文或英文，并翻译成另一种语言");

    public bool HasLiveTranslationStatus => LiveTranslationStatus.Length > 0;
    public bool HasLiveTranslationAutoNotice => LiveTranslationAutoNotice.Length > 0;
    public bool HasSystemPartialTranslation => SystemPartialTranslation.Length > 0;
    public bool HasMicrophonePartialTranslation => MicrophonePartialTranslation.Length > 0;

    public string SystemPartialTranslationLine => HasSystemPartialTranslation
        ? UiText.Format("译文：%@", SystemPartialTranslation)
        : "";
    public string MicrophonePartialTranslationLine => HasMicrophonePartialTranslation
        ? UiText.Format("译文：%@", MicrophonePartialTranslation)
        : "";

    partial void OnLiveTranslationEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(LiveTranslationButtonLabel));
        UpdateLiveTranslationStatus();
    }

    partial void OnSystemPartialTranslationChanged(string value)
    {
        OnPropertyChanged(nameof(HasSystemPartialTranslation));
        OnPropertyChanged(nameof(SystemPartialTranslationLine));
    }

    partial void OnMicrophonePartialTranslationChanged(string value)
    {
        OnPropertyChanged(nameof(HasMicrophonePartialTranslation));
        OnPropertyChanged(nameof(MicrophonePartialTranslationLine));
    }

    partial void OnLiveTranslationStatusChanged(string value) => OnPropertyChanged(nameof(HasLiveTranslationStatus));
    partial void OnLiveTranslationAutoNoticeChanged(string value) => OnPropertyChanged(nameof(HasLiveTranslationAutoNotice));

    private void UpdateLiveTranslationStatus()
    {
        LiveTranslationStatus = LiveTranslationEnabled ? UiText.T("同声传译") + " · " + UiText.T("译文") : "";
        OnPropertyChanged(nameof(HasLiveTranslationStatus));
    }

    /// <summary>Mac beginMeeting: the remembered choice holds for this meeting.</summary>
    private void BeginLiveMeetingTranslation()
    {
        liveTranslationAutoOpen.BeginMeeting(Settings.LiveTranslation);
        LiveTranslationEnabled = Settings.LiveTranslation ?? Settings.TranslateSpeech;
        LiveTranslationAutoNotice = "";
        UpdateLiveTranslationStatus();
    }

    /// <summary>Mac turnOnForForeignSpeech: the other side started speaking English.</summary>
    private void MaybeAutoOpenLiveTranslation(string systemText)
    {
        if (!Recording) return;
        // Mac asks the system for installed language packs; a configured translator is the Windows
        // equivalent of "the packs are ready" (no download sheet during a meeting).
        if (!liveTranslationAutoOpen.ShouldTurnOn(systemText, LiveTranslationEnabled,
                !UiText.EnglishUi, packsInstalled: translator is not null)) return;
        LiveTranslationEnabled = true;
        LiveTranslationAutoNotice = UiText.T("听到外语自动翻译") + " · " + UiText.T("同声传译");
        EnsureLiveTranslator();
    }

    private void EnsureLiveTranslator()
    {
        try
        {
            if (translator is null) translator = new(CurrentClient(), AnswerOptions());
        }
        catch { }
    }

    /// <summary>Mac LiveTranslationToggle: turn interpretation on or off; the choice is remembered.</summary>
    [RelayCommand]
    private void ToggleLiveTranslation()
    {
        LiveTranslationEnabled = !LiveTranslationEnabled;
        Settings.TranslateSpeech = LiveTranslationEnabled;
        Settings.LiveTranslation = LiveTranslationEnabled;
        liveTranslationAutoOpen.Remember(LiveTranslationEnabled);
        LiveTranslationAutoNotice = "";
        if (LiveTranslationEnabled) EnsureLiveTranslator();
        try { _ = Settings.SaveAsync(); } catch { }
    }

    /// <summary>Mac setEnabled(_:remember: false) during beginMeeting: nothing is written to settings.</summary>
    private void ApplyRememberedTranslatorForMeeting()
    {
        if (LiveTranslationEnabled && translator is null) EnsureLiveTranslator();
    }
}
