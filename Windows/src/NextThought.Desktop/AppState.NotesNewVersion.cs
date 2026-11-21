using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-78/§3.4 notes item 3: Mac's 「AI 原稿已有新版本」 strip. A regenerated draft is offered while
/// the page still shows the user's edited version; 查看新版 opens the AI copy and 保留我的修订
/// acknowledges it without touching the edits.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private bool hasAiNewVersion;
    private string aiNewVersionMarkdown = "";
    private DateTimeOffset aiNewVersionGeneratedAt;

    public Action? RequestNotesEditorWindow { get; set; }

    public void RefreshAiNewVersion(SessionMeta meta, SessionUserContent content)
    {
        HasAiNewVersion = false;
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "summary.json");
            if (!File.Exists(path)) return;
            if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is not System.Text.Json.Nodes.JsonObject summary) return;
            var markdown = summary["markdown"]?.GetValue<string>() ?? "";
            if (markdown.Length == 0) return;
            if (summary["generatedAt"] is not { } node
                || !DateTimeOffset.TryParse(node.GetValue<string>(), out var generated)) return;
            var edited = content.EditedMarkdown is not null || content.EditedSummary is not null || content.EditedNotes is not null;
            if (!edited) return;
            if (content.BasedOnGeneratedAt is { } basedOn && generated <= basedOn) return;
            aiNewVersionMarkdown = markdown;
            aiNewVersionGeneratedAt = generated;
            HasAiNewVersion = true;
        }
        catch { HasAiNewVersion = false; }
    }

    [RelayCommand]
    private void ViewNewAiVersion()
    {
        if (CurrentSession is not { } meta) return;
        LoadNotesEditor();
        NotesEditorShowOriginal = true;
        RequestNotesEditorWindow?.Invoke();
        HasAiNewVersion = false;
    }

    [RelayCommand]
    private void KeepMyNotesRevision()
    {
        if (CurrentSession is { } meta)
        {
            try
            {
                var folder = Sessions.Folder(meta.Id);
                var content = SessionUserContentStore.Load(folder);
                SessionUserContentStore.Save(content with
                {
                    BasedOnGeneratedAt = content.BasedOnGeneratedAt ?? aiNewVersionGeneratedAt
                }, folder);
            }
            catch { }
        }
        HasAiNewVersion = false;
    }
}
