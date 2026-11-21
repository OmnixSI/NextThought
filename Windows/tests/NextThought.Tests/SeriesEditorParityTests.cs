using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §6.7 E25/E30: the series editor matches WorkspacesView.swift's WorkspaceEditor. Mac shows a
/// header (SceneGlyph + series name + SceneTag + SeriesStatsLine) with 开下一场/配置 actions, then
/// only three primary sections (记录/资料/经验积累) with a 2px ink underline; 基本信息/回答要求/
/// 系列技能 live behind the 配置 menu with a save/cancel draft bar.
/// </summary>
public sealed class SeriesEditorParityTests
{
    private static string Windows()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Windows(), "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void EditorHeaderMatchesWorkspaceEditorSwift()
    {
        var view = Read("Views", "SeriesEditorWindow.axaml");
        // WorkspaceEditor.workspaceName: SceneGlyph(36) + name display + SceneTag + SeriesStatsLine.
        Assert.Contains("SceneGlyph", view);
        Assert.Contains("SceneTag", view);
        Assert.True(view.Contains("SelectedSeriesStatsLine") || view.Contains("SeriesEditorStatsLine"),
            "header stats line binding missing");
        Assert.Contains("Classes=\"display\"", view);
        // editorActions: 开下一场 + 配置 menu with the three configuration sections.
        Assert.Contains("开下一场", view);
        Assert.Contains("OpenNextMeetingInSeriesCommand", view);
        Assert.Contains("配置", view);
        Assert.Contains("基本信息", view);
        Assert.Contains("回答要求", view);
        Assert.Contains("系列技能", view);
        Assert.Contains("SectionBasic_Click", view);
        Assert.Contains("SectionRequirements_Click", view);
        Assert.Contains("SectionSkills_Click", view);
    }

    [Fact]
    public void EditorUsesDesignSystemComponentsForSwitchesAndDots()
    {
        var view = Read("Views", "SeriesEditorWindow.axaml");
        // decisions §3.1 components 6: status dots and switches come from the design system.
        Assert.Contains("dsc:StatusDot", view);
        Assert.Contains("dsc:InkSegmented", view);
        Assert.Contains("dsc:InkSwitch", view);
        Assert.Contains("dsc:WideToggleRow", view);
        Assert.DoesNotContain("ToggleSwitch", view);
        Assert.Contains("DotSize=\"7\"", view);
    }

    [Fact]
    public void PrimarySectionsAreRecordsDocumentsKnowledgeWithTheInkUnderline()
    {
        var view = Read("Views", "SeriesEditorWindow.axaml");
        // decisions §3.1 components 6: the section strip is the design-system InkSegmented
        // (sliding thumb), not three hand-made sectionTab buttons.
        Assert.Contains("x:Name=\"SectionTabs\"", view);
        Assert.Contains("InkSegmented", view);
        Assert.DoesNotContain("Classes=\"sectionTab", view);
        Assert.Contains("SectionTabs_SelectionChanged", view);
        // The three primary titles are the component's items, set next to the section state.
        var code = Read("Views", "SeriesEditorWindow.axaml.cs");
        foreach (var title in new[] { "记录", "资料", "经验积累" })
            Assert.Contains(title, code);
        // The strip's hairline track stays; the thumb/selection styling now comes from the component.
        Assert.Contains("BorderThickness=\"0,0,0,0.5\"", view);
        Assert.Contains("dsc:InkSegmented", view);
        Assert.Contains("dsc:StatusDot", view);
        Assert.DoesNotContain("ToggleSwitch", view);
        // Draft save/cancel bar only for basic/requirements (WorkspaceEditor.editActionsBar).
        Assert.Contains("EditActionsBar", view);
        Assert.Contains("SaveDraft_Click", view);
        Assert.Contains("CancelDraft_Click", view);
    }

    [Fact]
    public void SectionContentMatchesTheSwiftTabs()
    {
        var view = Read("Views", "SeriesEditorWindow.axaml");
        // Records tab uses the SessionRow-style dot/time/details.
        Assert.Contains("SeriesRecords", view);
        Assert.Contains("ModeTintBrush", view);
        // Documents tab: preparation summary + preparation queue + documents list.
        Assert.Contains("SeriesEditorPreparationSummary", view);
        Assert.Contains("SeriesEditorPreparationRows", view);
        Assert.Contains("SeriesEditorDocuments", view);
        // Knowledge tab: KnowledgeTab + ReviewTab (stage review, lessons, question bank).
        Assert.Contains("SeriesEditorStageReview", view);
        Assert.Contains("SeriesEditorKnowledgeEditing", view);
        Assert.Contains("SeriesEditorLessonQuery", view);
        Assert.Contains("SeriesEditorBankQuery", view);
        Assert.Contains("SeriesEditorCustomTypeName", view);
        Assert.Contains("SeriesEditorNotes", view);
        Assert.Contains("SeriesEditorCoreFields", view);
        Assert.Contains("SeriesEditorExtraFields", view);
        Assert.Contains("SeriesLessons", view);
        Assert.Contains("SeriesBank", view);
        // Skills section (WorkspaceSkillsSection) lives behind 配置.
        Assert.Contains("SkillsSection", view);
        Assert.Contains("SeriesSkills", view);
        // §3.5 per-item parity: heading 技能 N, import-for-this-series + manage buttons, and the
        // Mac row (toggle disabled for 所有系列 skills, 已停用 badge, one-line summary).
        foreach (var member in new[]
                 {
                     "{Binding SeriesSkillsHeading}", "{loc:Tr 导入并用于此系列…}", "{loc:Tr 管理技能}",
                     "ImportSeriesSkills_Click", "ManageSkills_Click",
                     "IsChecked=\"{Binding IsAttached, Mode=TwoWay}\"", "IsEnabled=\"{Binding CanToggle}\"",
                     "{Binding IsGlobal}", "{Binding IsDisabled}", "{Binding HasSummary}",
                     "{loc:Tr 所有系列}", "{loc:Tr 已停用}", "{Binding Summary}"
                 })
            Assert.Contains(member, view);
        var state = Read("AppState.Series.cs");
        Assert.Contains("SeriesSkillsHeading", state);
        Assert.Contains("skill.Global || attachedHere", state);
        Assert.Contains("AttachSkillToSelectedSeries", state);
        Assert.Contains("if (row.IsGlobal) return;", state);
        var code = Read("Views", "SeriesEditorWindow.axaml.cs");
        Assert.Contains("AttachSkillToSelectedSeries", code);
        Assert.Contains("OpenSettingsPage(\"Answer\", \"技能\")", code);
        var header = Read("AppState.SeriesEditorHeader.cs");
        Assert.Contains("SettingsRequestedPage", header);
        Assert.Contains("SettingsRequestedAnchor", header);
        var settings = Read("Views", "SettingsView.axaml.cs");
        Assert.Contains("SettingsRequestedPage", settings);
        Assert.Contains("ScrollToSection(anchor)", settings);
        var catalogue = File.ReadAllText(Path.Combine(Windows(), "src", "NextThought.Core", "UiTextMac.g.cs"));
        foreach (var key in new[] { "导入并用于此系列…", "管理技能", "所有系列", "已停用", "技能" })
            Assert.Contains("[\"" + key + "\"]", catalogue);
    }

    [Fact]
    public void HeaderStateComesFromTheActiveWorkspace()
    {
        var state = Read("AppState.SeriesEditorHeader.cs");
        Assert.Contains("SelectedSeriesMode", state);
        Assert.Contains("SelectedSeriesName", state);
        Assert.Contains("SelectedSeriesTypeName", state);
        Assert.Contains("EffectiveMode", state);
        Assert.Contains("CustomTypeName", state);
        Assert.Contains("HasSeriesSkills", state);
    }
}
