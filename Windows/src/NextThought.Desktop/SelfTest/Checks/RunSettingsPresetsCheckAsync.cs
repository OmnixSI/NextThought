using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E27/E29 QA: drives the settings page model the way the controls do — add / rename / delete
    /// an API preset, the delete-last guard, the engine-test failure path without a model, the
    /// permission rows and the GitHub token status. Writes a step-by-step report.
    /// </summary>
    private async Task RunSettingsPresetsCheckAsync(string path)
    {
        await Task.Delay(200);
        var settings = state.Settings;
        var steps = new List<object>();
        var passed = true;
        void Step(string action, bool ok, string detail)
        {
            steps.Add(new { action, ok, detail });
            passed &= ok;
        }

        var before = settings.ApiPresetChoices.Count;
        Step("preset-count", before >= 1, $"{before} 个预设");

        // 新建预设（页面上是 预设输入框 + 新建预设按钮）
        settings.ApiPresetName = "QA 预设";
        settings.AddApiPresetFromDraftCommand.Execute(null);
        var afterAdd = settings.ApiPresetChoices.Count;
        var addedName = settings.SelectedApiPreset?.Name ?? "";
        Step("add", afterAdd == before + 1 && addedName.StartsWith("QA 预设", StringComparison.Ordinal),
            $"{before} → {afterAdd}，当前 {addedName}");

        // 重命名（名称输入框 + 重命名按钮）
        settings.ApiPresetName = "QA 预设 改名";
        settings.RenameActiveApiPresetFromDraftCommand.Execute(null);
        Step("rename", settings.ApiPresetChoices.Any(choice => choice.Name == "QA 预设 改名"),
            settings.SelectedApiPreset?.Name ?? "");

        // 删除（确认对话框之后）
        settings.DeleteActiveApiPresetConfirmed();
        Step("delete", settings.ApiPresetChoices.Count == before, $"删除后 {settings.ApiPresetChoices.Count} 个预设");

        // 最后一个预设不能删（删除按钮 IsEnabled=CanDeleteApiPreset）
        Step("keep-one", settings.CanDeleteApiPreset == settings.ApiPresetChoices.Count > 1,
            $"CanDeleteApiPreset={settings.CanDeleteApiPreset}");

        // 推理强度选择
        settings.SelectedApiEffort = settings.ApiEffortChoices.First(choice => choice.Value == "high");
        var effort = settings.Build().ReasoningEffort;
        Step("effort", effort == "high", $"reasoning_effort={effort}");

        // 引擎测试：没有模型时直接失败并给 Mac 原句（不联网）
        settings.SelectedApiEffort = settings.ApiEffortChoices.First(choice => choice.Value == "default");
        var savedModel = settings.Model;
        var savedCodex = settings.UseCodex;
        settings.UseCodex = false;
        settings.Model = "";
        await state.TestAnswerEngineCommand.ExecuteAsync(null);
        Step("engine-test-guard", state.EngineTestState == EngineTestState.Failed
                                  && state.EngineTestLabel == UiText.T("请先选择或填写模型"),
            state.EngineTestLabel);
        settings.Model = savedModel;
        settings.UseCodex = savedCodex;

        // E29 权限行 + 打开系统设置的命令存在
        state.RefreshPermissions();
        Step("permissions", state.MicrophonePermissionDetail.Length > 0 && state.CameraPermissionDetail.Length > 0,
            $"麦克风：{state.MicrophonePermissionDetail}；摄像头：{state.CameraPermissionDetail}");
        Step("permission-commands", state.OpenMicrophonePermissionSettingsCommand is not null
                                    && state.OpenCameraPermissionSettingsCommand is not null, "");

        // E27 GitHub 访问：状态行与命令（不写入真实凭据）
        Step("github-status", settings.GithubTokenStatus.Length > 0, settings.GithubTokenStatus);
        Step("github-commands", settings.SaveGithubTokenCommand is not null && settings.ClearGithubTokenCommand is not null
                                && settings.FetchApiModelsCommand is not null, "");

        var report = new
        {
            passed,
            presetCount = settings.ApiPresetChoices.Count,
            engineTest = state.EngineTestState.ToString(),
            steps
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "settings-presets-check: passed" : "settings-presets-check: failed");
        Close();
    }
}
