using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

public partial class AppState
{

    public AppSettings SettingsSnapshot => Settings.Build();

    /// <summary>
    /// G8: applies an interface language at runtime. The markup extensions update by themselves; this
    /// also refreshes the code-built labels, lists and rows (used by the settings picker, the
    /// screenshots batch and the ui-language self-test).
    /// </summary>
    public void ApplyUiLanguage(bool english)
    {
        UiTextSource.Instance.SetEnglish(english);
        Status = UiText.Translate(Status, UiText.EnglishUi);
        PageSubtitle = UiText.Translate(PageSubtitle, UiText.EnglishUi);
        LanguageChanged?.Invoke(english);
    }

    public void SetStatus(string text, bool error = false)
    {
        Status = UiText.Translate(text, UiTextSource.Instance.IsEnglish);
        StatusIsError = error;
    }

    public void RefreshProcesses() => Settings.RefreshProcesses();

    private void LoadShortcutOverrides()
    {
        try { shortcutOverrides = new Dictionary<string, string>(SettingsStore.Load().Shortcuts ?? []); }
        catch { shortcutOverrides = []; }
        ShortcutText = BuildShortcutText();
    }

    private bool CanSaveSettings() => IsIdle;

    private void ApplyAppearance()
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = Settings.Build().Appearance switch
        {
            "dark" => Avalonia.Styling.ThemeVariant.Dark,
            "light" => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }

    // MARK: - E27 引擎测试（Mac EngineTestRow / testAnswerEngine）

    /// <summary>Mac <c>engineTest</c>: idle / running / passed / failed for the answer-service check.</summary>
    [ObservableProperty] private EngineTestState engineTestState = EngineTestState.Idle;
    [ObservableProperty] private string engineTestMessage = "";
    [ObservableProperty] private double engineTestSeconds;

    public bool EngineTestRunning => EngineTestState == EngineTestState.Running;
    public bool EngineTestPassed => EngineTestState == EngineTestState.Passed;
    public bool EngineTestFailed => EngineTestState == EngineTestState.Failed;
    public string EngineTestLabel => EngineTestState switch
    {
        EngineTestState.Running => UiText.T("正在检查…"),
        EngineTestState.Passed => UiText.Format("已连接 · 用时 %.1f 秒", EngineTestSeconds),
        EngineTestState.Failed => EngineTestMessage,
        _ => ""
    };

    partial void OnEngineTestStateChanged(EngineTestState value)
    {
        OnPropertyChanged(nameof(EngineTestRunning));
        OnPropertyChanged(nameof(EngineTestPassed));
        OnPropertyChanged(nameof(EngineTestFailed));
        OnPropertyChanged(nameof(EngineTestLabel));
    }

    partial void OnEngineTestSecondsChanged(double value) => OnPropertyChanged(nameof(EngineTestLabel));

    /// <summary>
    /// Mac <c>testAnswerEngine</c>: sends the shortest real request through the current answer
    /// service and reports seconds / failure in plain words.
    /// </summary>
    [RelayCommand]
    private async Task TestAnswerEngineAsync()
    {
        if (EngineTestState == EngineTestState.Running) return;
        var settings = Settings.Build();
        var options = AnswerOptions();
        var model = settings.UseCodex ? settings.CodexModel : settings.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            EngineTestState = EngineTestState.Failed;
            EngineTestMessage = UiText.T("请先选择或填写模型");
            OnPropertyChanged(nameof(EngineTestLabel));
            return;
        }
        EngineTestState = EngineTestState.Running;
        EngineTestMessage = "";
        EngineTestSeconds = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            const string system = "这是连通性测试。只回复两个字：收到。";
            var reply = await CurrentClient()
                .AnswerAsync(options, system, "测试", null, CancellationToken.None, 45)
                .ConfigureAwait(true);
            EngineTestSeconds = started.Elapsed.TotalSeconds;
            EngineTestState = EngineTestState.Passed;
            OnPropertyChanged(nameof(EngineTestLabel));
            if (reply.Trim().Length > 0) _ = reply;
        }
        catch (Exception exception)
        {
            EngineTestState = EngineTestState.Failed;
            EngineTestMessage = exception is HttpRequestException { StatusCode: { } status }
                ? UiText.T(APIFailureExplainer.Message(APIFailureExplainer.Classify((int)status)))
                : exception.Message;
            OnPropertyChanged(nameof(EngineTestLabel));
        }
    }

    // MARK: - E29 权限分组（Mac PermissionSettingsSection；Windows 没有录屏授权，只显示麦克风/摄像头）

    public WindowsPermissions.PermissionState MicrophonePermission => WindowsPermissions.Microphone();
    public WindowsPermissions.PermissionState CameraPermission => WindowsPermissions.Camera();
    public bool MicrophonePermissionGranted => MicrophonePermission == WindowsPermissions.PermissionState.Allowed;
    public bool CameraPermissionGranted => CameraPermission == WindowsPermissions.PermissionState.Allowed;

    public string MicrophonePermissionDetail => MicrophonePermission switch
    {
        WindowsPermissions.PermissionState.Allowed => UiText.T("已允许 · 你说的话标记为“我”"),
        WindowsPermissions.PermissionState.Denied => UiText.T("未允许 · 只记录对方的声音"),
        _ => UiText.T("第一次使用时才会询问")
    };

    public string CameraPermissionDetail => CameraPermission switch
    {
        WindowsPermissions.PermissionState.Allowed => UiText.T("已允许 · 临场教练用它留意眼神，画面不保存、不上传"),
        WindowsPermissions.PermissionState.Denied => UiText.T("未允许 · 只影响临场教练的眼神提示"),
        _ => UiText.T("打开“留意眼神”时才会询问")
    };

    /// <summary>E29: Mac opens System Settings; Windows opens the matching Settings deep link.</summary>
    [RelayCommand]
    private void OpenMicrophonePermissionSettings() => WindowsPermissions.OpenMicrophoneSettings();

    [RelayCommand]
    private void OpenCameraPermissionSettings() => WindowsPermissions.OpenCameraSettings();

    /// <summary>Called when the page is shown: permissions changed in Windows Settings meanwhile.</summary>
    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(MicrophonePermission));
        OnPropertyChanged(nameof(CameraPermission));
        OnPropertyChanged(nameof(MicrophonePermissionGranted));
        OnPropertyChanged(nameof(CameraPermissionGranted));
        OnPropertyChanged(nameof(MicrophonePermissionDetail));
        OnPropertyChanged(nameof(CameraPermissionDetail));
    }
}
