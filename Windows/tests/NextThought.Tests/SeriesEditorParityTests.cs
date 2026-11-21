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
        Assert.Contains("SelectedSeriesStatsLine", view);
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
    public void PrimarySectionsAreRecordsDocumentsKnowledgeWithTheInkUnderline()
    {
        var view = Read("Views", "SeriesEditorWindow.axaml");
        // WorkspaceEditorSection.primary = [records, documents, knowledge]
        Assert.Contains("x:Name=\"RecordsTab\"", view);
        Assert.Contains("x:Name=\"DocumentsTab\"", view);
        Assert.Contains("x:Name=\"KnowledgeTabButton\"", view);
        Assert.Contains("经验积累", view);
        Assert.Contains("SectionRecords_Click", view);
        Assert.Contains("SectionDocuments_Click", view);
        Assert.Contains("SectionKnowledge_Click", view);
        // Selected section is a 2px Ink underline over a 0.5px hairline track.
        Assert.Contains("Button.sectionTab.active", view);
        Assert.Contains("Value=\"0,0,0,2\"", view);
        Assert.Contains("BorderThickness=\"0,0,0,0.5\"", view);
        Assert.Contains("Value=\"{DynamicResource InkBrush}\"", view);
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
        Assert.Contains("SeriesLessons", view);
        Assert.Contains("SeriesBank", view);
        // Skills section (WorkspaceSkillsSection) lives behind 配置.
        Assert.Contains("SkillsSection", view);
        Assert.Contains("SeriesSkills", view);
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
