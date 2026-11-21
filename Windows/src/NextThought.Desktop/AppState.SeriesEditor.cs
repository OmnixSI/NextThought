using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E20: the series editor (WorkspaceEditor + WorkspaceBasicTab / WorkspaceRequirementsTab /
/// WorkspaceDocumentsTab / ReviewTab). The window keeps one draft per section on AppState and
/// commits it through 保存; documents import and remove immediately like the Mac editor.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private string seriesEditorName = "";
    [ObservableProperty] private string seriesEditorMode = "";
    [ObservableProperty] private string seriesEditorStyle = "";
    [ObservableProperty] private string seriesEditorPreferences = "";
    [ObservableProperty] private bool seriesEditorUseKnowledge = true;
    [ObservableProperty] private string seriesEditorStageReview = "";
    [ObservableProperty] private string seriesEditorImportStatus = "";
    [ObservableProperty] private bool seriesEditorImporting;
    [ObservableProperty] private string seriesEditorPreparationSummary = "";

    public ObservableCollection<SeriesEditorFieldRow> SeriesEditorFieldRows { get; } = [];
    public ObservableCollection<SeriesDocumentRow> SeriesEditorDocuments { get; } = [];
    public bool SeriesEditorDocumentsEmpty => SeriesEditorDocuments.Count == 0;
    public ObservableCollection<SeriesPreparationRow> SeriesEditorPreparationRows { get; } = [];
    public ObservableCollection<string> SeriesEditorModeChoices { get; } =
        [.. Enum.GetValues<MeetingMode>().Select(mode => ModeValue(mode))];

    /// <summary>WorkspaceBasicTab shows the mode's display name above the fields.</summary>
    public string SeriesEditorModeTitle => ModeProfile.For(CurrentEditorMode).Mode switch
    {
        MeetingMode.General => UiText.T("通用"),
        MeetingMode.Meeting => UiText.T("会议"),
        MeetingMode.Client => UiText.T("客户"),
        MeetingMode.Academic => UiText.T("汇报 / 答辩"),
        MeetingMode.LabMeeting => UiText.T("组会"),
        MeetingMode.Lesson => UiText.T("听课"),
        MeetingMode.Discussion => UiText.T("讨论"),
        MeetingMode.Interview => UiText.T("面试"),
        MeetingMode.GroupProject => UiText.T("小组项目"),
        _ => UiText.T("自定义")
    };

    /// <summary>WorkspaceBasicTab: role and review focus come from the mode profile.</summary>
    public string SeriesEditorRole => UiText.Format("角色：%@", ModeProfile.For(CurrentEditorMode).Role);
    public string SeriesEditorReviewFocus => ModeProfile.For(CurrentEditorMode).ReviewFocus;

    private MeetingMode CurrentEditorMode =>
        Enum.TryParse<MeetingMode>(SeriesEditorMode, true, out var parsed) ? parsed : MeetingMode.General;

    private static string ModeValue(MeetingMode mode)
    {
        var name = mode.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public void LoadSeriesEditor()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        SeriesEditorName = series.Name;
        SeriesEditorMode = ModeValue(series.EffectiveMode);
        SeriesEditorStyle = series.Style ?? "";
        SeriesEditorPreferences = series.Preferences ?? "";
        SeriesEditorUseKnowledge = series.UseKnowledge;
        SeriesEditorStageReview = series.StageReview ?? "";
        SeriesEditorNotes = series.Notes ?? "";
        SeriesEditorCustomTypeName = series.CustomTypeName ?? "";
        SeriesEditorImportStatus = "";
        RebuildSeriesEditorFields(series);
        SeriesEditorDocuments.Clear();
        foreach (var document in series.Documents) SeriesEditorDocuments.Add(new SeriesDocumentRow(document));
        OnPropertyChanged(nameof(SeriesEditorDocumentsEmpty));
        RefreshSeriesPreparation(series);
        RefreshSeriesEditorFieldGroups();
        OnPropertyChanged(nameof(SeriesEditorModeTitle));
        OnPropertyChanged(nameof(SeriesEditorRole));
        OnPropertyChanged(nameof(SeriesEditorReviewFocus));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
        MarkSeriesEditorClean();
    }

    // ---- WorkspaceEditor.hasChanges / showUnsavedPrompt (decisions §3.5 series-editor 6) ----

    private sealed record SeriesEditorDraftSnapshot(
        string Name, string Mode, string Style, string Preferences, bool UseKnowledge,
        string StageReview, string Notes, string CustomTypeName, IReadOnlyList<(string Id, string Value)> Fields);

    private SeriesEditorDraftSnapshot? seriesEditorBaseline;

    /// <summary>WorkspaceEditor.hasChanges: the basic/requirements draft differs from load/save.</summary>
    public bool SeriesEditorHasChanges =>
        seriesEditorBaseline is { } baseline && !SameDraft(baseline, DraftSnapshot());

    private SeriesEditorDraftSnapshot DraftSnapshot() => new(
        SeriesEditorName, SeriesEditorMode, SeriesEditorStyle, SeriesEditorPreferences, SeriesEditorUseKnowledge,
        SeriesEditorStageReview, SeriesEditorNotes, SeriesEditorCustomTypeName,
        SeriesEditorFieldRows.Select(row => (row.Id, row.Value)).ToList());

    private static bool SameDraft(SeriesEditorDraftSnapshot left, SeriesEditorDraftSnapshot right) =>
        left.Name == right.Name && left.Mode == right.Mode && left.Style == right.Style
        && left.Preferences == right.Preferences && left.UseKnowledge == right.UseKnowledge
        && left.StageReview == right.StageReview && left.Notes == right.Notes
        && left.CustomTypeName == right.CustomTypeName
        && left.Fields.Count == right.Fields.Count
        && left.Fields.Zip(right.Fields).All(pair => pair.First == pair.Second);

    /// <summary>After 保存 / 取消: the draft becomes the new baseline and the prompt closes.</summary>
    public void MarkSeriesEditorClean()
    {
        seriesEditorBaseline = DraftSnapshot();
        ShowSeriesEditorUnsavedPrompt = false;
        OnPropertyChanged(nameof(SeriesEditorHasChanges));
    }

    /// <summary>WorkspaceEditor.showUnsavedPrompt setter side effects; the window asks for it.</summary>
    public void ShowSeriesEditorUnsaved() => ShowSeriesEditorUnsavedPrompt = true;

    public void HideSeriesEditorUnsaved() => ShowSeriesEditorUnsavedPrompt = false;

    public string SeriesEditorUnsavedTitle => UiText.T("有未保存的修改");
    public string SeriesEditorUnsavedSubtitle => UiText.T("保存后继续刚才的操作；放弃更改会丢弃本次草稿。");

    partial void OnSeriesEditorModeChanged(string value)
    {
        RefreshSeriesEditorFieldGroups();
        OnPropertyChanged(nameof(SeriesEditorModeTitle));
        OnPropertyChanged(nameof(SeriesEditorRole));
        OnPropertyChanged(nameof(SeriesEditorReviewFocus));
        if (SelectedSeries is { } selected && workspace.Find(selected.Id) is { } series)
            RebuildSeriesEditorFields(series);
    }

    /// <summary>
    /// WorkspaceBasicTab's field editor: the mode's own fields (课程/岗位/论文 …) plus any unknown
    /// key kept from an older file, so saving never drops a Mac custom field.
    /// </summary>
    private void RebuildSeriesEditorFields(WorkspaceSeries series)
    {
        var values = series.Fields ?? new Dictionary<string, string>();
        var known = ModeProfile.For(CurrentEditorMode).Fields.Select(field => field.Id)
            .ToHashSet(StringComparer.Ordinal);
        SeriesEditorFieldRows.Clear();
        foreach (var field in ModeProfile.For(CurrentEditorMode).Fields)
            SeriesEditorFieldRows.Add(new SeriesEditorFieldRow(field, values.GetValueOrDefault(field.Id) ?? ""));
        foreach (var pair in values.Where(pair => !known.Contains(pair.Key)).OrderBy(pair => pair.Key))
            SeriesEditorFieldRows.Add(new SeriesEditorFieldRow(
                new ModeField(pair.Key, pair.Key, ""), pair.Value, custom: true));
    }

    /// <summary>ReviewTab's four counters: 会话 / 问题 / 经验 / 题库.</summary>
    public string SeriesEditorStatistics
    {
        get
        {
            if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return "";
            var sessions = series.SessionIds
                .Concat(History.Where(row => row.Meta.WorkspaceId == series.Id).Select(row => row.Meta.Id.ToString("D")))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var questions = sessions.Sum(id => Guid.TryParse(id, out var guid)
                ? History.FirstOrDefault(row => row.Meta.Id == guid)?.Meta.QuestionCount ?? 0
                : 0);
            return UiText.Format("会话 %lld · 问题 %lld · 经验 %lld · 题库 %lld",
                sessions.Count, questions, series.Lessons.Count, series.QuestionBank.Count);
        }
    }

    /// <summary>WorkspaceRequirementsTab: preferences plus the mode's review focus.</summary>
    [RelayCommand]
    private void SaveSeriesEditor()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var name = SeriesEditorName.Trim();
        if (name.Length == 0) { SetStatus(UiText.T("系列名称不能为空。"), true); return; }
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in SeriesEditorFieldRows)
            if (!string.IsNullOrWhiteSpace(row.Value)) fields[row.Id] = row.Value.Trim();
        workspace = workspace.WithSeries(series with
        {
            Name = name,
            Mode = SeriesEditorMode.Trim(),
            Style = SeriesEditorStyle.Trim(),
            Preferences = SeriesEditorPreferences.Trim(),
            UseKnowledge = SeriesEditorUseKnowledge,
            Fields = fields,
            Notes = SeriesEditorNotes.Trim().Length == 0 ? null : SeriesEditorNotes.Trim(),
            CustomTypeName = SeriesEditorCustomTypeName.Trim().Length == 0 ? null : SeriesEditorCustomTypeName.Trim(),
            StageReview = SeriesEditorStageReview.Trim().Length == 0 ? null : SeriesEditorStageReview.Trim(),
            StageReviewAt = SeriesEditorStageReview.Trim().Length == 0 ? null : DateTimeOffset.UtcNow
        });
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("系列信息已保存。"));
        MarkSeriesEditorClean();
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    // MARK: 资料 (WorkspaceDocumentsTab)

    [RelayCommand]
    private void RemoveSeriesDocument(SeriesDocumentRow? row)
    {
        if (row is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        lastRemovedSeriesDocument = (series.Id, row.Document);
        workspace = workspace.WithSeries(SeriesMaterials.Detach(series, row.Document.Id));
        SaveWorkspace();
        RefreshSeries();
        SeriesEditorDocuments.Remove(row);
        OnPropertyChanged(nameof(SeriesEditorDocumentsEmpty));
        SetStatus(UiText.Format("已从系列移除 %@", row.Document.Name));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    private (string SeriesId, ContextDocument Document)? lastRemovedSeriesDocument;

    /// <summary>WorkspaceDocumentsTab's 撤销 after a removal.</summary>
    [RelayCommand]
    private void UndoRemoveSeriesDocument()
    {
        if (lastRemovedSeriesDocument is not { } removed) return;
        if (workspace.Find(removed.SeriesId) is not { } series) return;
        if (series.Documents.Any(document => document.Id == removed.Document.Id)) return;
        workspace = workspace.WithSeries(series with
        {
            Documents = [.. series.Documents, removed.Document],
            DocumentIds = [.. series.DocumentIds, removed.Document.Id]
        });
        SaveWorkspace();
        RefreshSeries();
        SeriesEditorDocuments.Add(new SeriesDocumentRow(removed.Document));
        OnPropertyChanged(nameof(SeriesEditorDocumentsEmpty));
        lastRemovedSeriesDocument = null;
        SetStatus(UiText.Format("已恢复 %@", removed.Document.Name));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    [RelayCommand]
    private void SetSeriesDocumentEnabled(SeriesDocumentRow? row)
    {
        if (row is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var documents = series.Documents
            .Select(document => document.Id == row.Document.Id ? document with { Enabled = row.Enabled } : document)
            .ToList();
        workspace = workspace.WithSeries(series with { Documents = documents });
        SaveWorkspace();
        RefreshSeries();
    }

    [RelayCommand]
    private void MoveSeriesDocumentToCategory(SeriesDocumentRow? row) => ReorderOrCategorize(row);

    /// <summary>WorkspaceDocumentsTab's 移动 menu: files the material under the next profile slot.</summary>
    private void ReorderOrCategorize(SeriesDocumentRow? row)
    {
        if (row is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var categories = ModeProfile.For(series.EffectiveMode).DocumentCategories.ToList();
        if (categories.Count == 0) return;
        var index = categories.IndexOf(SeriesMaterials.CategoryOf(row.Document));
        var next = categories[(index + 1) % categories.Count];
        var documents = series.Documents
            .Select(document => document.Id == row.Document.Id ? document with { Category = next } : document)
            .ToList();
        workspace = workspace.WithSeries(series with { Documents = documents });
        SaveWorkspace();
        RefreshSeries();
        row.Category = next;
        SetStatus(UiText.Format("已把 %@ 移到「%@」", row.Document.Name, next));
    }

    /// <summary>
    /// WorkspaceDocumentsTab import: reads the files on a worker (OCR included), then attaches the
    /// library documents to the series. The same file imported twice is attached only once.
    /// <paramref name="category"/> comes from 导入到 …; null runs the Mac 自动归类 content classifier.
    /// </summary>
    public IReadOnlyList<string> SeriesEditorImportCategories
    {
        get
        {
            var categories = new List<string>(ModeProfile.For(CurrentEditorMode).DocumentCategories);
            foreach (var row in SeriesEditorDocuments)
                if (!categories.Contains(row.Category, StringComparer.Ordinal)) categories.Add(row.Category);
            if (categories.Count == 0) categories.Add(SeriesMaterials.OtherCategory);
            return categories;
        }
    }

    public async Task ImportSeriesDocumentsAsync(IReadOnlyList<string> paths, string? category = null)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        if (paths.Count == 0) return;
        SeriesEditorImporting = true;
        try
        {
            var store = new MaterialStore(Paths);
            var imported = new List<MaterialDocument>();
            var categories = SeriesEditorImportCategories;
            var autoCategories = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                try
                {
                    var material = await Task.Run(() => store.ImportAsync(path));
                    if (category is null)
                    {
                        // Mac 自动归类: the same Core classifier the document pipeline uses.
                        var text = string.Join("\n", material.Blocks.Select(block => block.Text));
                        autoCategories[material.Id] = DocumentClassifier.Classify(material.Name, text, categories);
                    }
                    imported.Add(material);
                    SeriesEditorImportStatus = UiText.Format("正在导入 %@…", Path.GetFileName(path));
                }
                catch (Exception ex)
                {
                    SetStatus(UiText.Format("导入 %@ 失败：%@", Path.GetFileName(path), ex.Message), true);
                    SeriesEditorImportStatus = UiText.Format("导入 %@ 失败：%@", Path.GetFileName(path), ex.Message);
                }
            }
            if (imported.Count == 0) return;
            series = workspace.Find(selected.Id) ?? series;
            var attached = imported.Count;
            foreach (var material in imported)
                series = SeriesMaterials.Attach(series, material,
                    category ?? autoCategories.GetValueOrDefault(material.Id));
            workspace = workspace.WithSeries(series);
            SaveWorkspace();
            RefreshSeries();
            RefreshSeriesEditorDocuments(series);
            TryRefreshLibrary();
            SeriesEditorImportStatus = UiText.Format("已把 %lld 份资料加入系列。", attached);
            SetStatus(SeriesEditorImportStatus);
            OnPropertyChanged(nameof(SeriesEditorStatistics));
        }
        finally
        {
            SeriesEditorImporting = false;
        }
    }

    private void RefreshSeriesEditorDocuments(WorkspaceSeries series)
    {
        SeriesEditorDocuments.Clear();
        foreach (var document in series.Documents) SeriesEditorDocuments.Add(new SeriesDocumentRow(document));
        OnPropertyChanged(nameof(SeriesEditorDocumentsEmpty));
    }

    private void TryRefreshLibrary()
    {
        try { Library.Refresh(); } catch { }
    }

    // MARK: 准备队列摘要 (PreparationQueueSummary)

    private string SeriesPreparationQueueDirectory => Path.Combine(Paths.Root, "DocumentJobs");

    /// <summary>
    /// WorkspaceDocumentsTab's PreparationQueueSummary: failed and partial imports stay visible even
    /// when no job is running, with 重试 / 取消 for the ones that need attention.
    /// </summary>
    public void RefreshSeriesPreparation(WorkspaceSeries? series = null)
    {
        series ??= SelectedSeries is { } selected ? workspace.Find(selected.Id) : null;
        SeriesEditorPreparationRows.Clear();
        SeriesEditorPreparationSummary = "";
        if (series is null) return;
        IReadOnlyList<DocumentJob> jobs;
        try
        {
            jobs = new DocumentJobQueue(SeriesPreparationQueueDirectory).All
                .Where(job => job.WorkspaceId == series.Id).ToList();
        }
        catch { jobs = []; }
        foreach (var job in jobs.OrderBy(job => job.CreatedAt).TakeLast(100))
            SeriesEditorPreparationRows.Add(new SeriesPreparationRow(job));
        var ready = jobs.Count(job => job.Stage == DocumentJob.StageValue.Ready);
        var pending = jobs.Count(job => !job.IsTerminal);
        var attention = jobs.Count(job => job.Stage is DocumentJob.StageValue.Failed or DocumentJob.StageValue.Warning);
        var cancelled = jobs.Count(job => job.Stage == DocumentJob.StageValue.Cancelled);
        var parts = new List<string> { UiText.T("会前准备"), UiText.Format("%lld 份已就绪", ready) };
        if (pending > 0) parts.Add(UiText.Format("%lld 份待完成", pending));
        if (attention > 0) parts.Add(UiText.Format("%lld 份需处理", attention));
        if (cancelled > 0) parts.Add(UiText.Format("%lld 份已取消", cancelled));
        SeriesEditorPreparationSummary = string.Join(" · ", parts);
        if (jobs.Count == 0)
            SeriesEditorPreparationSummary = UiText.Format("会前准备：这个系列已有 %lld 份资料，均在本机解析。",
                series.Documents.Count);
        OnPropertyChanged(nameof(SeriesEditorPreparationSummary));
        OnPropertyChanged(nameof(SeriesEditorNeedsAttention));
        OnPropertyChanged(nameof(SeriesEditorAttentionText));
    }

    public bool SeriesEditorNeedsAttention => SeriesEditorPreparationRows.Any(row => row.NeedsAttention);
    public string SeriesEditorAttentionText =>
        UiText.Format("%lld 份资料需要处理或核对，展开下方列表查看。",
            SeriesEditorPreparationRows.Count(row => row.NeedsAttention));

    [RelayCommand]
    private void RetrySeriesPreparation(SeriesPreparationRow? row)
    {
        if (row is null) return;
        try
        {
            var queue = new DocumentJobQueue(SeriesPreparationQueueDirectory);
            var job = queue.All.FirstOrDefault(item => item.Id == row.Job.Id);
            if (job is null) return;
            queue.Update(job with
            {
                Stage = DocumentJob.StageValue.Queued,
                Completed = 0,
                Total = null,
                Message = "",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            RefreshSeriesPreparation();
            SetStatus(UiText.T("已重新排队。"));
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand]
    private void CancelSeriesPreparation(SeriesPreparationRow? row)
    {
        if (row is null) return;
        try
        {
            var queue = new DocumentJobQueue(SeriesPreparationQueueDirectory);
            var job = queue.All.FirstOrDefault(item => item.Id == row.Job.Id);
            if (job is null) return;
            queue.Update(job with { Stage = DocumentJob.StageValue.Cancelled, UpdatedAt = DateTimeOffset.UtcNow });
            RefreshSeriesPreparation();
            SetStatus(UiText.T("已取消。"));
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    // MARK: 编辑经验与题库 (ExperienceLessonRow / BankRow)

    /// <summary>ExperienceLessonRow's edit field: creates a revision instead of overwriting history.</summary>
    public void UpdateSeriesLesson(WorkspaceLesson lesson, string text)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        if (WorkspaceLessonHistory.EditText(lesson, text, DateTimeOffset.UtcNow) is not { } updated) return;
        workspace = workspace.WithSeries(series with
        {
            Lessons = series.Lessons.Select(item => item.Id == lesson.Id ? updated : item).ToList()
        });
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("经验已更新。"));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    [RelayCommand]
    private void ToggleSeriesLessonPinned(WorkspaceLesson? lesson)
    {
        if (lesson is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        workspace = workspace.WithSeries(series with
        {
            Lessons = series.Lessons
                .Select(item => item.Id == lesson.Id ? item with { Pinned = !item.Pinned } : item).ToList()
        });
        SaveWorkspace();
        RefreshSeries();
    }

    /// <summary>ExperienceLessonRow's delete: the accepted lesson leaves the series.</summary>
    [RelayCommand]
    private void DeleteSeriesLesson(WorkspaceLesson? lesson)
    {
        if (lesson is null || SelectedSeries is not { } selected) return;
        workspace = workspace.RemoveLesson(selected.Id, lesson.Id);
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("经验已删除。"));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    /// <summary>BankRow's editable question and answer; edits keep the answer revision history.</summary>
    public void UpdateSeriesQuestionBankItem(QuestionBankItem item, string question, string answer)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var updated = KnowledgeMerger.EditQuestionBankItem(item, question, answer, DateTimeOffset.UtcNow);
        workspace = workspace.WithSeries(series with
        {
            QuestionBank = series.QuestionBank.Select(existing => existing.Id == item.Id ? updated : existing).ToList()
        });
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("题库已更新。"));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    [RelayCommand]
    private void DeleteSeriesBankItem(QuestionBankItem? item)
    {
        if (item is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        workspace = workspace.WithSeries(series with
        {
            QuestionBank = series.QuestionBank.Where(existing => existing.Id != item.Id).ToList()
        });
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("题目已删除。"));
        OnPropertyChanged(nameof(SeriesEditorStatistics));
    }

    // MARK: 归档 / 删除 (WorkspaceActions)

    [RelayCommand]
    private void ArchiveSeries()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        workspace = workspace.WithSeries(series with { Archived = true });
        SaveWorkspace();
        RefreshSeries();
        SetStatus(UiText.T("系列已归档。"));
    }

    /// <summary>WorkspaceActions.deleteWorkspace: the series and its meeting records go together.</summary>
    public async Task DeleteSeriesFromEditorAsync()
    {
        if (SelectedSeries is not { } selected) return;
        await DeleteSeriesWithMeetingsAsync(selected.Id);
    }
}

/// <summary>E20: one material of the series in the 资料 tab (WorkspaceDocumentsTab row).</summary>
public sealed class SeriesDocumentRow : System.ComponentModel.INotifyPropertyChanged
{
    public SeriesDocumentRow(ContextDocument document)
    {
        Document = document;
        enabled = document.Enabled;
        category = SeriesMaterials.CategoryOf(document);
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public ContextDocument Document { get; }
    public string Name => Document.Name;
    public string MetaText => UiText.Format("%lld 块 · %lld 字符",
        Document.Pages.Count, Document.CharacterCount);
    public string FormatLabel => Document.SourceFormat ?? "";

    private bool enabled;
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            enabled = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Enabled)));
        }
    }

    private string category;
    public string Category
    {
        get => category;
        set
        {
            if (category == value) return;
            category = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Category)));
        }
    }
}
