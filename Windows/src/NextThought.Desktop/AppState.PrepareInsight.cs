using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public enum PrepareInsightState
{
    Idle,
    Running,
    Ready,
    Failed
}

/// <summary>
/// E2 task-33: the preparation page's remaining Mac pieces — MaterialsInsight ("讨论准备 · AI 可选"
/// reading the series materials through the answer service), the CheckCard audio levels and the
/// answer-service test state machine (Mac PreparationPreview/readDraftMaterials/testAnswerEngine).
/// </summary>
public partial class AppState
{
    private CancellationTokenSource? insightCancellation;

    // ---- MaterialsInsight (Mac PreparationPreview) ------------------------------------

    [ObservableProperty] private PrepareInsightState prepareInsightState = PrepareInsightState.Idle;
    [ObservableProperty] private string prepareInsightText = "";
    [ObservableProperty] private string prepareInsightError = "";
    [ObservableProperty] private string prepareInsightStatus = "";

    public bool PrepareInsightRunning => PrepareInsightState == PrepareInsightState.Running;
    public bool PrepareInsightHasText => PrepareInsightText.Length > 0;
    public bool PrepareInsightShowsBody => PrepareInsightState != PrepareInsightState.Idle;
    public bool PrepareInsightHasMaterials => PreparedMaterialCount > 0;

    public string PrepareInsightHeading => UiText.T("讨论准备 · AI 可选");
    public string PrepareInsightHint => UiText.T("用本场选择的资料整理讨论重点、证据和值得追问的问题。无需 AI 也能开始记录。");
    public string PrepareInsightAction => PrepareAnswerReady ? UiText.T("整理讨论重点") : UiText.T("设置 AI 服务（可选）");
    public string PrepareInsightActionHint => PrepareAnswerReady
        ? UiText.T("读完资料后，AI 写出它读到的 3 条重点和 5 道可能会问的题，可以先在心里过一遍。")
        : UiText.T("还没有配置回答服务；整理讨论重点需要它，记录和转写不需要。");

    partial void OnPrepareInsightStateChanged(PrepareInsightState value)
    {
        OnPropertyChanged(nameof(PrepareInsightRunning));
        OnPropertyChanged(nameof(PrepareInsightHasText));
        OnPropertyChanged(nameof(PrepareInsightShowsBody));
        OnPropertyChanged(nameof(PrepareInsightAction));
        OnPropertyChanged(nameof(PrepareInsightActionHint));
    }

    partial void OnPrepareInsightTextChanged(string value) => OnPropertyChanged(nameof(PrepareInsightHasText));

    /// <summary>Mac MaterialsInsight button: 整理讨论重点, or the settings entry when AI is optional.</summary>
    [RelayCommand]
    private void PrepareInsightActionCommand()
    {
        if (PrepareAnswerReady) _ = ReadMaterialsInsightAsync();
        else NavigateCommand.Execute("Settings");
    }

    /// <summary>Mac readDraftMaterials: one request over the series materials, streamed into the page.</summary>
    [RelayCommand]
    private async Task ReadMaterialsInsightAsync()
    {
        if (PrepareInsightRunning) return;
        if (!PrepareAnswerReady)
        {
            PrepareInsightState = PrepareInsightState.Failed;
            PrepareInsightError = UiText.T("还没有配置回答服务：到 设置 → 回答 填好 API Key 后可整理讨论重点。");
            return;
        }
        var material = DraftMaterialText();
        if (material.Length == 0)
        {
            PrepareInsightState = PrepareInsightState.Failed;
            PrepareInsightError = UiText.T("还没有可用的资料：先导入资料，再整理讨论重点。");
            return;
        }
        PrepareInsightState = PrepareInsightState.Running;
        PrepareInsightText = "";
        PrepareInsightError = "";
        PrepareInsightStatus = UiText.T("正在读资料…");
        insightCancellation = new CancellationTokenSource();
        try
        {
            var mode = Draft?.EffectiveMode ?? SelectedMode;
            var system = PreparationPrompt.System(mode);
            var user = PreparationPrompt.Output(mode) + "\n\n资料：\n" + material;
            var result = await CurrentClient().AnswerAsync(AnswerOptions(), system, user,
                text => Dispatcher.UIThread.Post(() =>
                {
                    if (PrepareInsightState == PrepareInsightState.Running) PrepareInsightText = text;
                }), insightCancellation.Token);
            if (PrepareInsightState != PrepareInsightState.Running) return;
            PrepareInsightText = result;
            PrepareInsightState = PrepareInsightState.Ready;
            PrepareInsightStatus = UiText.T("讨论重点已整理。");
        }
        catch (OperationCanceledException)
        {
            PrepareInsightState = PrepareInsightState.Idle;
            PrepareInsightStatus = UiText.T("已取消生成。");
        }
        catch (Exception ex)
        {
            PrepareInsightState = PrepareInsightState.Failed;
            PrepareInsightError = ex.Message;
            PrepareInsightStatus = "";
        }
        finally
        {
            insightCancellation?.Dispose();
            insightCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelMaterialsInsight()
    {
        insightCancellation?.Cancel();
        PrepareInsightState = PrepareInsightState.Idle;
        PrepareInsightStatus = UiText.T("已取消生成。");
    }

    /// <summary>The series' material text (Mac draftSeries.materialText), falling back to the library.</summary>
    private string DraftMaterialText()
    {
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series && series.MaterialText() is { Length: > 0 } text)
            return Clip(text, 24_000);
        var pages = Library.DocumentsForRetrieval()
            .SelectMany(document => document.Blocks.Select(block => $"{document.Name}：{block.Text}"))
            .ToList();
        return Clip(string.Join("\n\n", pages), 24_000);
    }

    private static string Clip(string text, int limit) => text.Length <= limit ? text : text[..limit];

    // ---- CheckCard audio meters (Mac LevelMeters wording) ------------------------------

    [ObservableProperty] private double systemAudioLevel;
    [ObservableProperty] private double microphoneAudioLevel;

    public double SystemAudioLevelPercent => Math.Clamp(SystemAudioLevel, 0, 1);
    public double MicrophoneAudioLevelPercent => Math.Clamp(MicrophoneAudioLevel, 0, 1);
    public string AudioLevelsText => UiText.Format("对方 · 系统声音 %.0f · 我 · 麦克风 %.0f", SystemAudioLevel * 100, MicrophoneAudioLevel * 100);

    partial void OnSystemAudioLevelChanged(double value) => OnPropertyChanged(nameof(SystemAudioLevelPercent));

    partial void OnMicrophoneAudioLevelChanged(double value) => OnPropertyChanged(nameof(MicrophoneAudioLevelPercent));

    public void UpdateAudioLevels(double system, double microphone)
    {
        SystemAudioLevel = system;
        MicrophoneAudioLevel = microphone;
        OnPropertyChanged(nameof(AudioLevelsText));
    }

    // ---- VenuePicker (Mac MeetingVenue) ------------------------------------------------

    public string DraftVenueHint => DraftOffline
        ? UiText.T("线下：只用麦克风，声音标为“现场”。")
        : UiText.T("线上：听电脑里会议软件播放的声音。");

    public string DraftVenueStateLabel => DraftOffline ? UiText.T("线下") : UiText.T("线上");

    // ---- CheckCard: answer-service test state machine (Mac testAnswerEngine) ------------

    public string PrepareEngineActionLabel => EngineTestState switch
    {
        EngineTestState.Running => UiText.T("正在检查…"),
        EngineTestState.Idle => UiText.T("检查回答服务"),
        _ => UiText.T("重新检查")
    };

    public bool PrepareEngineActionEnabled => !EngineTestRunning;
    public string PrepareEngineStateLabel => EngineTestState switch
    {
        EngineTestState.Running => UiText.T("正在检查 AI 服务…"),
        EngineTestState.Passed => UiText.Format("连接检查通过 · 用时 %.1f 秒", EngineTestSeconds),
        EngineTestState.Failed => EngineTestMessage.Length > 0 ? EngineTestMessage : UiText.T("连接检查未通过 · 记录仍可继续"),
        _ => PrepareAnswerReady ? UiText.T("已配置 · 尚未检查连接") : UiText.T("未配置 · 不影响记录")
    };

    private void RaisePrepareEngineState()
    {
        OnPropertyChanged(nameof(PrepareEngineActionLabel));
        OnPropertyChanged(nameof(PrepareEngineActionEnabled));
        OnPropertyChanged(nameof(PrepareEngineStateLabel));
    }
}
