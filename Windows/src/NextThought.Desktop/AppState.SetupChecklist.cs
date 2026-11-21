using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E3: the settings checklist (Mac Onboarding.swift SetupChecklist / setupItems). Windows drops the
/// macOS-only rows (screen recording, signature) and keeps the three that apply here.
/// </summary>
public partial class AppState
{
    public IReadOnlyList<SetupChecklistRow> SetupChecklistRows =>
    [
        new("asr", UiText.T("语音识别"),
            ModelReady ? UiText.T("本机离线识别已就绪") : ModelStatusText,
            ModelReady, false,
            ModelReady ? "" : UiText.T("下载")),
        new("engine", UiText.T("回答引擎"),
            Settings.HasApiKey ? PrepareAnswerLine : UiText.T("必需：选择服务商，粘贴 API Key"),
            Settings.HasApiKey, EngineTestRunning,
            Settings.HasApiKey ? "" : UiText.T("设置")),
        new("workspace", UiText.T("当前场景"),
            $"{SelectedScene?.Title ?? UiText.T("通用")} · {Library.MaterialCountText}",
            Library.Materials.Count > 0, false,
            UiText.T("编辑"))
    ];

    [RelayCommand]
    private void SetupChecklistAction(string id)
    {
        switch (id)
        {
            case "asr":
                if (!ModelReady) PrepareModelsCommand.Execute(null);
                else NavigateCommand.Execute("Settings");
                break;
            case "engine":
                NavigateCommand.Execute("Settings");
                break;
            case "workspace":
                NavigateCommand.Execute("Series");
                break;
        }
    }

    /// <summary>Mac SetupChecklist is shown in 设置 → 通用 (excluding the macOS-only rows).</summary>
    public bool ShowSetupChecklist => true;
}

/// <summary>One quiet checklist row: status dot, title, one gray line and an optional action.</summary>
public sealed record SetupChecklistRow(string Id, string Title, string Detail, bool Ready, bool Working, string ActionLabel)
{
    public bool HasAction => ActionLabel.Length > 0;
}
