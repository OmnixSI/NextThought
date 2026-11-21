using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-78/§3.4 notes item 2 (first part): Mac's 来源过期提示. Older summaries that do not record a
/// transcriptSourceRevision cannot be checked against the current transcript, so the page shows the
/// Mac sentence above the tabs instead of silently rendering them.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private bool hasNotesStaleHint;

    public string NotesStaleHintText =>
        UiText.T("部分旧内容未记录来源版本；无法确认是否与当前原话和纪要一致。");

    public void RefreshNotesStatus(SessionMeta meta)
    {
        HasNotesStaleHint = false;
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "summary.json");
            if (!File.Exists(path)) return;
            if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is not System.Text.Json.Nodes.JsonObject summary) return;
            var revision = summary["transcriptSourceRevision"]?.GetValue<string>() ?? "";
            HasNotesStaleHint = revision.Length == 0;
        }
        catch { HasNotesStaleHint = false; }
    }
}
