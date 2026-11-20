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

        // E28: skills — new, edit+save, install the three Mac demo skills, delete (with the
        // same store calls the settings card's buttons use).
        var skillsBefore = state.EditableSkills.Count;
        state.AddSkill();
        var addedSkill = state.EditableSkills[^1];
        Step("skill-new", state.EditableSkills.Count == skillsBefore + 1
                           && addedSkill.Name == NextThought.Core.Skill.StarterName
                           && addedSkill.Summary == NextThought.Core.Skill.StarterSummary,
            $"{skillsBefore} → {state.EditableSkills.Count}，{addedSkill.Name}");
        Step("skill-starter", addedSkill.Content == NextThought.Core.Skill.StarterContent
                             && !addedSkill.Global && addedSkill.Enabled, "starter template / 默认仅本系列");
        state.SaveSkillEdit(addedSkill with { Name = "QA 技能", Summary = "测试摘要", Content = "测试正文" });
        var editedSkill = state.EditableSkills.FirstOrDefault(skill => skill.Id == addedSkill.Id);
        Step("skill-save", editedSkill is { Name: "QA 技能", Summary: "测试摘要", Content: "测试正文" }, editedSkill?.Name ?? "");
        state.InstallDemoSkill();
        var demo = NextThought.Core.Skill.DemoSkills;
        var demoRows = demo.Select(item => state.EditableSkills.FirstOrDefault(skill => skill.Name == item.Name)).ToList();
        Step("skill-install-demo", demoRows.All(row => row is not null)
                                 && demoRows[0]!.Global && !demoRows[1]!.Global && !demoRows[2]!.Global,
            string.Join(" / ", demoRows.Select(row => row?.Name ?? "missing")));
        state.DeleteSkill(addedSkill.Id);
        Step("skill-delete", state.EditableSkills.All(skill => skill.Id != addedSkill.Id), $"删除后 {state.EditableSkills.Count} 个技能");

        // E26b: capture the scene page and the quick settings panel in light/dark/English from
        // inside the self-test (the full screenshot batch can be interrupted by other QA runs).
        var evidenceDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetTempPath(), "evidence");
        var previousTheme = Avalonia.Application.Current?.RequestedThemeVariant;
        foreach (var (dark, english, suffix) in new[] { (false, false, "-light"), (true, false, "-dark"), (false, true, "-en-light"), (true, true, "-en-dark") })
        {
            try
            {
                UiTextSource.Instance.SetEnglish(english);
                if (Avalonia.Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                var settingsView = new SettingsView { DataContext = state };
                var window = new Avalonia.Controls.Window
                {
                    Content = settingsView,
                    Width = 1120,
                    Height = 820,
                    ShowInTaskbar = false
                };
                window.Show();
                settingsView.SelectPage("Scene");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(settingsView.UpdateLayout, Avalonia.Threading.DispatcherPriority.Background);
                await Task.Delay(250);
                CaptureControl(settingsView, Path.Combine(evidenceDirectory, "settings-scene" + suffix + ".png"));
                // E28: the skill card is the first card of 回答与资料.
                settingsView.SelectPage("Answer");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(settingsView.UpdateLayout, Avalonia.Threading.DispatcherPriority.Background);
                await Task.Delay(200);
                CaptureControl(settingsView, Path.Combine(evidenceDirectory, "settings-skills" + suffix + ".png"));
                window.Close();

                var quick = new QuickSettingsPanel { DataContext = state };
                var quickWindow = new Avalonia.Controls.Window
                {
                    Content = quick,
                    SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
                    ShowInTaskbar = false
                };
                quickWindow.Show();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(quick.UpdateLayout, Avalonia.Threading.DispatcherPriority.Background);
                await Task.Delay(250);
                CaptureControl(quick, Path.Combine(evidenceDirectory, "quick-settings" + suffix + ".png"));
                quickWindow.Close();
            }
            catch (Exception exception)
            {
                steps.Add(new { action = "evidence-" + suffix.Trim('-'), ok = false, detail = exception.Message });
            }
        }
        UiTextSource.Instance.SetEnglish(false);
        if (Avalonia.Application.Current is { } restore && previousTheme is { } theme) restore.RequestedThemeVariant = theme;

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

    private static void CaptureControl(Avalonia.Controls.Control control, string file)
    {
        var width = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
            new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96));
        bitmap.Render(control);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        bitmap.Save(file);
    }
}
