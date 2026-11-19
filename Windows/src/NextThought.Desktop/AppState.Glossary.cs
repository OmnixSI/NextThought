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

    private void LoadGlossary()
    {
        glossaryStore = new GlossaryStore(Paths.Root);
        glossary = glossaryStore.Load();
        GlossaryText = GlossaryStore.ToText(glossary);
        GlossaryCount = glossary.Entries.Count;
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
