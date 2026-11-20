using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string notesEditorText = "";
    [ObservableProperty] private string personalNotesText = "";

    /// <summary>E16: prefill the editor from user-content.json without touching generated notes.</summary>
    public void LoadNotesEditor()
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            var content = SessionUserContentStore.Load(Sessions.Folder(meta.Id));
            NotesEditorText = content.EditedMarkdown ?? SummaryText;
            PersonalNotesText = content.PersonalNotes;
        }
        catch { NotesEditorText = SummaryText; PersonalNotesText = ""; }
    }

    [RelayCommand]
    private void SaveNotesEditor()
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            SessionUserContentStore.Save(content with
            {
                EditedMarkdown = NotesEditorText,
                PersonalNotes = PersonalNotesText
            }, folder);
            SetStatus("纪要编辑已保存；生成结果未被覆盖。");
        }
        catch (Exception ex) { SetStatus("保存失败：" + ex.Message, true); }
    }
}
