using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E22: 术语和人名（GlossaryView.swift + AppState+Glossary.swift）。全局表与当前系列表分层生效；
/// 识别热词、回答/纪要提示与转写查找都用「全局 + 系列 + 资料术语」的合并结果，原始转写永不改写。
/// </summary>
public partial class AppState
{
    private GlossaryStore? glossaryStore;
    private Glossary glossary = Glossary.Empty;

    [ObservableProperty] private string glossaryText = "";
    [ObservableProperty] private int glossaryCount;
    [ObservableProperty] private bool glossarySeriesScope;
    [ObservableProperty] private string glossaryFilter = "";
    [ObservableProperty] private string glossaryStatusText = "";
    [ObservableProperty] private string glossaryEditorCanonical = "";
    [ObservableProperty] private string glossaryEditorVariants = "";
    [ObservableProperty] private string glossaryEditorNote = "";
    [ObservableProperty] private bool glossaryEditorIsPerson;
    [ObservableProperty] private string glossarySelectedId = "";
    [ObservableProperty] private bool glossaryPasteMode;
    [ObservableProperty] private string glossaryPasteText = "";
    [ObservableProperty] private bool glossaryPastePerson;
    public bool HasGlossaryPasteText => GlossaryPasteText.Trim().Length > 0;
    partial void OnGlossaryPasteTextChanged(string value) => OnPropertyChanged(nameof(HasGlossaryPasteText));
    [ObservableProperty] private string glossaryQuery = "";
    [ObservableProperty] private string newGlossaryTerm = "";
    [ObservableProperty] private string newGlossaryVariants = "";

    // 加入术语表草稿（AddToGlossarySheet）
    [ObservableProperty] private string glossaryDraftLine = "";
    [ObservableProperty] private string glossaryDraftCanonical = "";
    [ObservableProperty] private string glossaryDraftHeard = "";
    [ObservableProperty] private bool glossaryDraftPerson;
    [ObservableProperty] private bool glossaryDraftSeriesOnly;

    public ObservableCollection<GlossaryEntryRow> GlossaryRows { get; } = [];
    public ObservableCollection<GlossaryEntryRow> SeriesGlossaryRows { get; } = [];
    public ObservableCollection<GlossaryTranscriptHitRow> GlossaryTranscriptHits { get; } = [];

    public string GlossaryTitleText => UiText.T("术语和人名");
    public string GlossaryHint => UiText.T("名字和术语在转写、回答、纪要里写法统一；记下听错的写法，搜索时一并找出来。原始转写不会被改写。");
    public string GlossaryScopeGlobalLabel => UiText.T("全局");
    public string GlossaryScopeSeriesLabel => UiText.T("当前系列");
    public string GlossaryFindPlaceholder => UiText.T("在转写里查找（听错的写法也会找出来）");
    public string GlossaryListHeading => GlossarySeriesScope ? UiText.T("当前系列的术语") : UiText.T("全部术语");
    public string GlossaryEmptyText => GlossaryRows.Count == 0
        ? UiText.T(GlossarySeriesScope
            ? "这个系列还没有自己的条目。"
            : "还没有条目。点「新增」，或用「批量粘贴」一次加入多条。")
        : "";
    public string GlossaryFilteredEmptyText => GlossaryRows.Count == 0 && GlossaryFilter.Trim().Length > 0
        ? UiText.T("没有匹配的条目。")
        : "";
    public string GlossaryCountText => UiText.Format("%lld 条", GlossaryRows.Count);
    public string GlossaryMatchCountText => GlossaryQuery.Trim().Length == 0
        ? ""
        : UiText.Format("%lld 处", GlossaryTranscriptHits.Count);
    public bool HasGlossaryTranscriptHits => GlossaryTranscriptHits.Count > 0;
    public bool HasGlossarySelection => GlossarySelectedId.Length > 0;

    /// <summary>Mac duplicatesAnother: the spelling being edited already exists in this scope.</summary>
    public bool HasGlossaryDuplicate
    {
        get
        {
            var key = Glossary.LooseKey(GlossaryEditorCanonical);
            if (key.Length == 0) return false;
            return GlossaryEntriesInScope().Any(entry => entry.Id != GlossarySelectedId
                && Glossary.LooseKey(entry.Canonical) == key);
        }
    }

    partial void OnGlossaryEditorCanonicalChanged(string value) => OnPropertyChanged(nameof(HasGlossaryDuplicate));
    public string GlossaryEditorHeading => GlossarySelectedId.Length > 0 ? UiText.T("编辑条目") : UiText.T("新增条目");
    public string GlossaryVariantsPlaceholder => UiText.T("识别出来的错误写法，逗号分隔");
    public string GlossaryNotePlaceholder => UiText.T("可选，例如“我们的数据库项目”");
    public string GlossaryPasteHint => UiText.T("每行一条：规范写法 | 变体1, 变体2 | 类型 | 备注。后两项可以省略，类型写“人名”或“术语”。");
    public string GlossaryPasteHeading => UiText.T("批量粘贴");
    public string GlossaryKindTermLabel => UiText.T("术语");
    public string GlossaryKindPersonLabel => UiText.T("人名");
    public string GlossarySeriesHint => UiText.T("系列术语只叠加在这个系列的回答、纪要与查找里；全局术语仍然生效。");
    public string GlossarySeriesEmptyText => UiText.T("这个系列还没有自己的术语；全局条目仍会叠加生效。");
    public bool HasSeriesGlossaryEntries => SeriesGlossaryRows.Count > 0;
    public bool HasGlossaryStatus => GlossaryStatusText.Length > 0;
    public bool HasGlossary => GlossaryCount > 0;
    public string GlossaryStatus => GlossaryCount == 0
        ? UiText.T("还没有术语。")
        : UiText.Format("%lld 条术语 · 识别时作为热词，转写里出现的错写会被改回正确写法", GlossaryCount);

    partial void OnGlossaryCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasGlossary));
        OnPropertyChanged(nameof(GlossaryStatus));
    }

    partial void OnGlossaryQueryChanged(string value) => RefreshGlossaryTranscriptHits();
    partial void OnGlossaryFilterChanged(string value) => RefreshGlossaryRows();
    partial void OnGlossaryStatusTextChanged(string value) => OnPropertyChanged(nameof(HasGlossaryStatus));
    partial void OnGlossarySeriesScopeChanged(bool value)
    {
        GlossarySelectedId = "";
        ClearGlossaryEditor();
        RefreshGlossaryRows();
        RefreshGlossaryTranscriptHits();
        OnPropertyChanged(nameof(GlossaryListHeading));
        OnPropertyChanged(nameof(GlossaryEmptyText));
        RefreshEditorFlags();
    }

    /// <summary>Mac effectiveGlossary: global + the meeting's series (+ material terms).</summary>
    public Glossary EffectiveGlossary(SessionMeta? meta = null, bool includeMaterialTerms = true)
    {
        var series = meta is not null
            ? workspace.SeriesOfSession(meta.Id.ToString())
            : SelectedSeries is { } selected ? workspace.Find(selected.Id) : null;
        var layered = Glossary.Layered(glossary, series?.Glossary);
        if (!includeMaterialTerms || series is null) return layered;
        var pages = series.Documents.Where(document => document.Enabled)
            .SelectMany(document => document.Pages).ToList();
        return pages.Count == 0 ? layered : GlossaryMaterialTerms.WithTerms(layered, MaterialTerms.Extract(pages, 24));
    }

    /// <summary>Live answer prompt block: layered glossary capped at 1,500 characters.</summary>
    private string GlossaryPromptSection(SessionMeta? meta) => EffectiveGlossary(meta).PromptSection(1500);

    public void LoadGlossary()
    {
        glossaryStore = new GlossaryStore(Paths.Root);
        glossary = glossaryStore.Load();
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
        RefreshGlossaryRows();
        RefreshSeriesGlossaryRows();
    }

    private IReadOnlyList<GlossaryEntry> GlossaryEntriesInScope() => GlossarySeriesScope
        ? SelectedSeries is { } selected && workspace.Find(selected.Id)?.Glossary is { } series ? series.Entries : []
        : glossary.Entries;

    private void RefreshGlossaryRows()
    {
        GlossaryRows.Clear();
        var filter = GlossaryFilter.Trim();
        foreach (var entry in GlossaryEntriesInScope())
        {
            if (filter.Length > 0 && !entry.Spellings.Concat([entry.Note ?? ""])
                    .Any(spelling => spelling.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                continue;
            GlossaryRows.Add(new GlossaryEntryRow(entry, RemoveGlossaryEntry));
        }
        OnPropertyChanged(nameof(GlossaryEmptyText));
        OnPropertyChanged(nameof(GlossaryFilteredEmptyText));
        OnPropertyChanged(nameof(GlossaryCountText));
    }

    [RelayCommand]
    private void UseGlobalGlossaryScope() => GlossarySeriesScope = false;

    [RelayCommand]
    private void UseGlossaryTermKind() => GlossaryEditorIsPerson = false;

    [RelayCommand]
    private void UseGlossaryPersonKind() => GlossaryEditorIsPerson = true;

    [RelayCommand]
    private void UsePasteTermKind() => GlossaryPastePerson = false;

    [RelayCommand]
    private void UsePastePersonKind() => GlossaryPastePerson = true;

    [RelayCommand]
    private void UseSeriesGlossaryScope() => GlossarySeriesScope = true;

    // MARK: 编辑

    public void BeginGlossaryEntry(GlossaryEntryRow row)
    {
        GlossarySelectedId = row.Entry.Id;
        GlossaryEditorCanonical = row.Entry.Canonical;
        GlossaryEditorVariants = string.Join(", ", row.Entry.Variants);
        GlossaryEditorNote = row.Entry.Note ?? "";
        GlossaryEditorIsPerson = row.Entry.Kind == GlossaryKind.Person;
        GlossaryPasteMode = false;
        RefreshEditorFlags();
    }

    [RelayCommand]
    private void StartNewGlossaryEntry()
    {
        GlossarySelectedId = "";
        ClearGlossaryEditor();
        GlossaryPasteMode = false;
        RefreshEditorFlags();
    }

    private void ClearGlossaryEditor()
    {
        GlossaryEditorCanonical = "";
        GlossaryEditorVariants = "";
        GlossaryEditorNote = "";
        GlossaryEditorIsPerson = false;
    }

    private void RefreshEditorFlags()
    {
        OnPropertyChanged(nameof(HasGlossarySelection));
        OnPropertyChanged(nameof(GlossaryEditorHeading));
        OnPropertyChanged(nameof(HasGlossaryDuplicate));
    }

    private static string[] SplitVariants(string text) => text
        .Split([',', '，', '、', ';', '；', '|', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [RelayCommand]
    private void SaveGlossaryEntry()
    {
        var canonical = GlossaryEditorCanonical.Trim();
        if (canonical.Length == 0) { SetStatus(UiText.T("规范写法不能为空。"), true); return; }
        var entry = new GlossaryEntry(canonical,
            GlossaryEditorIsPerson ? GlossaryKind.Person : GlossaryKind.Term,
            SplitVariants(GlossaryEditorVariants),
            GlossaryEditorNote.Trim().Length == 0 ? null : GlossaryEditorNote.Trim(),
            GlossarySelectedId.Length > 0 ? GlossarySelectedId : null);
        if (!SaveEntryToScope(entry)) { SetStatus(UiText.T("请先选择一个系列，或在全局作用域编辑。"), true); return; }
        GlossarySelectedId = entry.Id;
        RefreshEditorFlags();
        GlossaryStatusText = UiText.T("已保存");
        SetStatus(UiText.T("术语已保存。"));
    }

    private bool SaveEntryToScope(GlossaryEntry entry)
    {
        if (!GlossarySeriesScope)
        {
            var entries = glossary.Entries.ToList();
            var index = entries.FindIndex(item => item.Id == entry.Id);
            if (index >= 0) entries[index] = entry; else entries.Add(entry);
            ReplaceGlobalGlossary(new Glossary(entries));
            return true;
        }
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return false;
        var current = series.Glossary?.Entries.ToList() ?? [];
        var existing = current.FindIndex(item => item.Id == entry.Id);
        if (existing >= 0) current[existing] = entry; else current.Add(entry);
        SaveSeriesGlossary(series, new Glossary(current));
        return true;
    }

    private void ReplaceGlobalGlossary(Glossary updated)
    {
        glossary = updated.Normalized();
        try { glossaryStore?.Save(glossary); } catch { }
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
        RefreshGlossaryRows();
        RefreshGlossaryTranscriptHits();
    }

    private void SaveSeriesGlossary(WorkspaceSeries series, Glossary updated)
    {
        var normalized = updated.Normalized();
        workspace = workspace.WithSeries(series with { Glossary = normalized.IsEmpty ? null : normalized });
        SaveWorkspace();
        RefreshSeries();
        RefreshSeriesGlossaryRows();
        RefreshGlossaryRows();
        RefreshGlossaryTranscriptHits();
    }

    private void RemoveGlossaryEntry(GlossaryEntry entry)
    {
        if (!GlossarySeriesScope) { ReplaceGlobalGlossary(new Glossary(glossary.Entries.Where(item => item.Id != entry.Id).ToList())); return; }
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id)?.Glossary is not { } seriesGlossary) return;
        SaveSeriesGlossary(workspace.Find(selected.Id)!, new Glossary(seriesGlossary.Entries.Where(item => item.Id != entry.Id).ToList()));
        if (GlossarySelectedId == entry.Id) { GlossarySelectedId = ""; ClearGlossaryEditor(); RefreshEditorFlags(); }
    }

    public void RefreshSeriesGlossaryRows()
    {
        SeriesGlossaryRows.Clear();
        if (SelectedSeries is { } selected && workspace.Find(selected.Id)?.Glossary is { } seriesGlossary)
            foreach (var entry in seriesGlossary.Entries)
                SeriesGlossaryRows.Add(new GlossaryEntryRow(entry, RemoveSeriesGlossaryEntry));
        OnPropertyChanged(nameof(HasSeriesGlossaryEntries));
        OnPropertyChanged(nameof(GlossarySeriesEmptyText));
    }

    private void RemoveSeriesGlossaryEntry(GlossaryEntry entry)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id)?.Glossary is not { } seriesGlossary) return;
        SaveSeriesGlossary(workspace.Find(selected.Id)!, new Glossary(seriesGlossary.Entries.Where(item => item.Id != entry.Id).ToList()));
    }

    // MARK: 批量粘贴 / 导入导出 / 整理

    public void BeginGlossaryPaste()
    {
        GlossaryPasteMode = true;
        GlossaryPasteText = "";
        GlossarySelectedId = "";
        ClearGlossaryEditor();
        RefreshEditorFlags();
    }

    [RelayCommand]
    private void ImportGlossaryPasted()
    {
        var added = ImportGlossaryText(GlossaryPasteText, GlossaryPastePerson ? GlossaryKind.Person : GlossaryKind.Term);
        GlossaryStatusText = added > 0 ? UiText.Format("已加入 %lld 项", added) : UiText.T("没有新的内容");
        GlossaryPasteText = "";
        GlossaryPasteMode = false;
        RefreshEditorFlags();
    }

    /// <summary>Pasted lines (规范写法 | 变体 | 类型 | 备注) merged into the current scope.</summary>
    public int ImportGlossaryText(string text, GlossaryKind defaultKind)
    {
        var incoming = Glossary.ParseText(text, defaultKind).Normalized();
        if (incoming.IsEmpty) return 0;
        if (!GlossarySeriesScope)
        {
            var before = glossary;
            var merged = before.Merging(incoming);
            ReplaceGlobalGlossary(merged);
            return GlossaryMerge.Count(before, glossary);
        }
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return 0;
        var current = series.Glossary ?? Glossary.Empty;
        var combined = current.Merging(incoming);
        SaveSeriesGlossary(series, combined);
        return GlossaryMerge.Count(current, combined);
    }

    [RelayCommand]
    private void TidyGlossaries()
    {
        var before = GlossaryMerge.Size(glossary);
        var cleaned = glossary.Normalized();
        if (cleaned != glossary) ReplaceGlobalGlossary(cleaned);
        if (SelectedSeries is { } selected && workspace.Find(selected.Id) is { } series && series.Glossary is { } seriesGlossary)
        {
            var normalized = seriesGlossary.Normalized();
            if (normalized != seriesGlossary) SaveSeriesGlossary(series, normalized);
        }
        RefreshGlossaryRows();
        RefreshSeriesGlossaryRows();
        GlossaryStatusText = UiText.T("已整理");
        SetStatus(UiText.Format("已整理重复项（%lld 条 → %lld 条）。", before, GlossaryMerge.Size(glossary)));
    }

    public string ExportGlossaryText() => GlossaryStore.ToText(GlossarySeriesScope
        ? new Glossary(GlossaryEntriesInScope())
        : glossary);

    // MARK: 转写查找（TranscriptFindField + MisheardMark）

    /// <summary>
    /// A query typed as the canonical spelling also finds lines that only contain the recorded
    /// misheard spelling; the hit row then shows MisheardMark with that spelling.
    /// </summary>
    private void RefreshGlossaryTranscriptHits()
    {
        GlossaryTranscriptHits.Clear();
        var query = GlossaryQuery.Trim();
        if (query.Length > 0)
        {
            var search = EffectiveGlossary(CurrentSession).Search(query);
            foreach (var row in Transcript)
            {
                if (search.FirstMatch(row.Text) is not { } hit) continue;
                GlossaryTranscriptHits.Add(new GlossaryTranscriptHitRow(row, hit.Form, hit.IsMisheard));
                if (GlossaryTranscriptHits.Count >= 20) break;
            }
        }
        OnPropertyChanged(nameof(GlossaryMatchCountText));
        OnPropertyChanged(nameof(HasGlossaryTranscriptHits));
    }

    /// <summary>Joins one transcript line to the glossary (Mac AddToGlossarySheet).</summary>
    public void BeginAddToGlossary(string line)
    {
        GlossaryDraftLine = line;
        GlossaryDraftCanonical = "";
        GlossaryDraftHeard = "";
        GlossaryDraftPerson = false;
        GlossaryDraftSeriesOnly = SelectedSeries is not null && GlossarySeriesScope;
    }

    public bool CommitAddToGlossary()
    {
        var canonical = GlossaryDraftCanonical.Trim();
        if (canonical.Length == 0) return false;
        var entry = new GlossaryEntry(canonical,
            GlossaryDraftPerson ? GlossaryKind.Person : GlossaryKind.Term,
            SplitVariants(GlossaryDraftHeard));
        if (!GlossaryDraftSeriesOnly)
        {
            ReplaceGlobalGlossary(glossary.Merging(new Glossary([entry])));
            return true;
        }
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return false;
        SaveSeriesGlossary(series, (series.Glossary ?? Glossary.Empty).Merging(new Glossary([entry])));
        return true;
    }

    // MARK: 会议页转写查找（TranscriptFindField + MisheardMark）

    [ObservableProperty] private string transcriptFindQuery = "";
    public ObservableCollection<GlossaryTranscriptHitRow> TranscriptFindHits { get; } = [];
    public bool HasTranscriptFindHits => TranscriptFindHits.Count > 0;
    public bool HasTranscriptFindQuery => TranscriptFindQuery.Trim().Length > 0;
    public string TranscriptFindCountText => HasTranscriptFindQuery
        ? UiText.Format("%lld 处", TranscriptFindHits.Count)
        : "";
    public string TranscriptFindPlaceholder => UiText.T("在转写里查找（听错的写法也会找出来）");

    partial void OnTranscriptFindQueryChanged(string value) => RefreshTranscriptFindHits();

    [RelayCommand]
    private void ClearTranscriptFind()
    {
        TranscriptFindQuery = "";
        RefreshTranscriptFindHits();
    }

    private void RefreshTranscriptFindHits()
    {
        TranscriptFindHits.Clear();
        var query = TranscriptFindQuery.Trim();
        if (query.Length > 0)
        {
            var search = EffectiveGlossary(CurrentSession).Search(query);
            foreach (var row in Transcript)
            {
                if (search.FirstMatch(row.Text) is not { } hit) continue;
                TranscriptFindHits.Add(new GlossaryTranscriptHitRow(row, hit.Form, hit.IsMisheard));
                if (TranscriptFindHits.Count >= 100) break;
            }
        }
        OnPropertyChanged(nameof(HasTranscriptFindHits));
        OnPropertyChanged(nameof(HasTranscriptFindQuery));
        OnPropertyChanged(nameof(TranscriptFindCountText));
    }
    // MARK: 兼容旧入口

    [RelayCommand]
    private void AddGlossaryEntry()
    {
        var term = NewGlossaryTerm.Trim();
        if (term.Length == 0) return;
        ReplaceGlobalGlossary(glossary.Merging(new Glossary([new GlossaryEntry(term, variants: SplitVariants(NewGlossaryVariants))])));
        NewGlossaryTerm = "";
        NewGlossaryVariants = "";
    }

    [RelayCommand]
    private void AddGlossaryToSeries()
    {
        var term = NewGlossaryTerm.Trim();
        if (term.Length == 0 || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        SaveSeriesGlossary(series, (series.Glossary ?? Glossary.Empty)
            .Merging(new Glossary([new GlossaryEntry(term, variants: SplitVariants(NewGlossaryVariants))])));
        NewGlossaryTerm = "";
        NewGlossaryVariants = "";
        SetStatus(UiText.T("已加入系列术语。"));
    }

    /// <summary>Canonical spellings for the recognizer (settings.Vocabulary stays supported).</summary>
    public IReadOnlyList<string> GlossaryHotwords() => EffectiveGlossary(CurrentSession).Hotwords();

    /// <summary>Corrects the recognised text with the glossary's canonical spellings.</summary>
    public string Canonicalize(string text)
    {
        var effective = EffectiveGlossary(CurrentSession);
        return effective.IsEmpty ? text : effective.Canonicalize(text);
    }

    [RelayCommand]
    private void SaveGlossary()
    {
        var parsed = Glossary.ParseText(GlossaryText);
        try
        {
            glossaryStore?.Save(parsed);
            glossary = parsed;
            GlossaryText = GlossaryStore.ToText(parsed);
            GlossaryCount = parsed.Entries.Count;
            RefreshGlossaryRows();
            SetStatus(parsed.Entries.Count == 0
                ? UiText.T("术语表已清空。")
                : UiText.Format("术语表已保存 %lld 条，下一场会议生效。", parsed.Entries.Count));
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }
}
