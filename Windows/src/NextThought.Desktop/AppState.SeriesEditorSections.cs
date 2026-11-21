using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §3.5 series-editor 3–7: knowledge edit/search, skills badges and the basic-tab custom type,
/// background notes and collapsed extra fields.
/// </summary>
public partial class AppState
{
    // ---- 经验积累: 编辑条目模式 + 搜索 (KnowledgeTab) --------------------------------
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool seriesEditorKnowledgeEditing;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesEditorLessonQuery = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesEditorBankQuery = "";

    public System.Collections.ObjectModel.ObservableCollection<WorkspaceLesson> SeriesEditorLessonsShown { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<QuestionBankRow> SeriesEditorBankShown { get; } = [];

    public string SeriesEditorKnowledgeModeLabel => UiText.T(SeriesEditorKnowledgeEditing ? "完成" : "编辑条目");

    partial void OnSeriesEditorKnowledgeEditingChanged(bool value) => OnPropertyChanged(nameof(SeriesEditorKnowledgeModeLabel));
    partial void OnSeriesEditorLessonQueryChanged(string value) => RefreshSeriesEditorKnowledge();
    partial void OnSeriesEditorBankQueryChanged(string value) => RefreshSeriesEditorKnowledge();

    public void ToggleSeriesEditorKnowledgeEditing() => SeriesEditorKnowledgeEditing = !SeriesEditorKnowledgeEditing;

    public void RefreshSeriesEditorKnowledge()
    {
        var lessonTerm = SeriesEditorLessonQuery.Trim();
        SeriesEditorLessonsShown.Clear();
        foreach (var lesson in SeriesLessons)
            if (lessonTerm.Length == 0 || lesson.Text.Contains(lessonTerm, StringComparison.OrdinalIgnoreCase))
                SeriesEditorLessonsShown.Add(lesson);
        var bankTerm = SeriesEditorBankQuery.Trim();
        SeriesEditorBankShown.Clear();
        foreach (var row in SeriesBank)
            if (bankTerm.Length == 0
                || row.Question.Contains(bankTerm, StringComparison.OrdinalIgnoreCase)
                || row.Answer.Contains(bankTerm, StringComparison.OrdinalIgnoreCase))
                SeriesEditorBankShown.Add(row);
        OnPropertyChanged(nameof(SeriesEditorLessonsShown));
        OnPropertyChanged(nameof(SeriesEditorBankShown));
    }

    // ---- 系列技能: 导入并用于此系列 / 管理技能 (WorkspaceSkillsSection) ----------------
    /// <summary>Attach the given skill ids to the selected series (import-and-use).</summary>
    public void AttachSkillsToSelectedSeries(IEnumerable<string> skillIds)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var ids = (series.SkillIds ?? []).ToList();
        foreach (var id in skillIds)
            if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
        workspace = workspace.WithSeries(series with { SkillIds = ids });
        SaveWorkspace();
        RefreshSeriesSkills();
        RefreshSeriesSkillsForCheck();
    }

    // ---- 基本信息: 自定义类型名 / 背景笔记 / 折叠额外字段 ---------------------------
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesEditorCustomTypeName = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesEditorNotes = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool seriesEditorShowAllFields;

    /// <summary>WorkspaceEditor.showUnsavedPrompt: asked before switching sections or closing.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool showSeriesEditorUnsavedPrompt;

    public bool IsSeriesEditorCustomMode => CurrentEditorModeForUi == MeetingMode.Custom;

    private MeetingMode CurrentEditorModeForUi =>
        Enum.TryParse<MeetingMode>(SeriesEditorMode, true, out var parsed) ? parsed : MeetingMode.General;

    public System.Collections.ObjectModel.ObservableCollection<SeriesEditorFieldRow> SeriesEditorCoreFields { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<SeriesEditorFieldRow> SeriesEditorExtraFields { get; } = [];
    public bool HasSeriesEditorExtraFields => SeriesEditorExtraFields.Count > 0;

    public void RefreshSeriesEditorFieldGroups()
    {
        SeriesEditorCoreFields.Clear();
        SeriesEditorExtraFields.Clear();
        var index = 0;
        foreach (var row in SeriesEditorFieldRows)
        {
            if (index++ < 2) SeriesEditorCoreFields.Add(row);
            else SeriesEditorExtraFields.Add(row);
        }
        OnPropertyChanged(nameof(HasSeriesEditorExtraFields));
        OnPropertyChanged(nameof(SeriesEditorCoreFields));
        OnPropertyChanged(nameof(SeriesEditorExtraFields));
    }

    public string SeriesEditorAllFieldsLabel => UiText.T(SeriesEditorShowAllFields ? "隐藏" : "更多");

    public void ToggleSeriesEditorAllFields()
    {
        SeriesEditorShowAllFields = !SeriesEditorShowAllFields;
        OnPropertyChanged(nameof(SeriesEditorShowAllFields));
    }
}
