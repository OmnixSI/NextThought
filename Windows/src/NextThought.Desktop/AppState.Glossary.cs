using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>术语表（人名与专有名词）：喂给识别器的热词，也用于纠正转写与提示词。</summary>
public partial class AppState
{
    private GlossaryStore? glossaryStore;
    private Glossary glossary = Glossary.Empty;

    [ObservableProperty] private string glossaryText = "";
    [ObservableProperty] private int glossaryCount;

    public bool HasGlossary => GlossaryCount > 0;
    public string GlossaryStatus => GlossaryCount == 0
        ? "还没有术语。每行一个：正确写法 | 识别错的写法 | 另一种错法"
        : $"{GlossaryCount} 条术语 · 识别时作为热词，转写里出现的错写会被改回正确写法";

    partial void OnGlossaryCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasGlossary));
        OnPropertyChanged(nameof(GlossaryStatus));
    }



    public System.Collections.ObjectModel.ObservableCollection<TranscriptRow> GlossaryTranscriptHits { get; } = [];

    partial void OnGlossaryQueryChanged(string value) => RefreshGlossaryTranscriptHits();

    private void RefreshGlossaryTranscriptHits()
    {
        GlossaryTranscriptHits.Clear();
        var query = GlossaryQuery.Trim();
        if (query.Length < 1) return;
        foreach (var row in Transcript.Where(row => row.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(20))
            GlossaryTranscriptHits.Add(row);
    }


    public System.Collections.ObjectModel.ObservableCollection<GlossaryEntryRow> SeriesGlossaryRows { get; } = [];

    public void RefreshSeriesGlossaryRows()
    {
        SeriesGlossaryRows.Clear();
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id)?.Glossary is not { } seriesGlossary) return;
        foreach (var entry in seriesGlossary.Entries)
            SeriesGlossaryRows.Add(new GlossaryEntryRow(entry, RemoveSeriesGlossaryEntry));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void AddGlossaryToSeries()
    {
        var term = NewGlossaryTerm.Trim();
        if (term.Length == 0 || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var variants = NewGlossaryVariants.Split(['|', ',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var layered = Glossary.Layered(series.Glossary ?? Glossary.Empty, new Glossary([new GlossaryEntry(term, variants: variants)]));
        workspace = workspace.WithSeries(series with { Glossary = layered });
        SaveWorkspace();
        RefreshSeries();
        RefreshSeriesGlossaryRows();
        NewGlossaryTerm = ""; NewGlossaryVariants = "";
        SetStatus("已加入系列术语：" + term);
    }

    private void RemoveSeriesGlossaryEntry(GlossaryEntry entry)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series || series.Glossary is not { } current) return;
        workspace = workspace.WithSeries(series with { Glossary = new Glossary(current.Entries.Where(item => item.Id != entry.Id).ToList()) });
        SaveWorkspace();
        RefreshSeries();
        RefreshSeriesGlossaryRows();
    }

    public System.Collections.ObjectModel.ObservableCollection<GlossaryEntryRow> GlossaryRows { get; } = [];
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string glossaryQuery = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string newGlossaryTerm = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string newGlossaryVariants = "";
    public string GlossaryEmptyText => GlossaryRows.Count == 0 ? "还没有术语；添加一个正确写法和常见误听。" : "";

    private void RefreshGlossaryRows()
    {
        GlossaryRows.Clear();
        foreach (var entry in glossary.Entries)
            GlossaryRows.Add(new GlossaryEntryRow(entry, RemoveGlossaryEntry));
        OnPropertyChanged(nameof(GlossaryEmptyText));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void AddGlossaryEntry()
    {
        var term = NewGlossaryTerm.Trim();
        if (term.Length == 0) return;
        var variants = NewGlossaryVariants.Split(['|', ',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = glossary.Entries.ToList();
        entries.Add(new GlossaryEntry(term, variants: variants));
        glossary = new Glossary(entries).Normalized();
        try { glossaryStore?.Save(glossary); } catch { }
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
        NewGlossaryTerm = ""; NewGlossaryVariants = "";
        RefreshGlossaryRows();
    }

    private void RemoveGlossaryEntry(GlossaryEntry entry)
    {
        glossary = new Glossary(glossary.Entries.Where(item => item.Id != entry.Id).ToList());
        try { glossaryStore?.Save(glossary); } catch { }
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
        RefreshGlossaryRows();
    }

    private void LoadGlossary()
    {
        glossaryStore = new GlossaryStore(Paths.Root);
        glossary = glossaryStore.Load();
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
        RefreshGlossaryRows();
    }

    /// <summary>Canonical spellings for the recognizer (settings.Vocabulary stays supported).</summary>
    public IReadOnlyList<string> GlossaryHotwords() => glossary.Hotwords();

    /// <summary>Corrects the recognised text with the glossary's canonical spellings.</summary>
    public string Canonicalize(string text) => glossary.IsEmpty ? text : glossary.Canonicalize(text);

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
            SetStatus(parsed.Entries.Count == 0
                ? NextThought.Core.UiText.EnglishUi ? "Glossary cleared." : "术语表已清空。"
                : NextThought.Core.UiText.EnglishUi
                    ? $"Glossary saved ({parsed.Entries.Count} entries); applies to the next meeting."
                    : $"术语表已保存 {parsed.Entries.Count} 条，下一场会议生效。");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }
}
