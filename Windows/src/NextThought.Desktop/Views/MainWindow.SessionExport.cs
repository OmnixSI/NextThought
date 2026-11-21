using Avalonia.Controls;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>E19: the 导出… button opens the export sheet for the meeting currently shown.</summary>
    private void OpenSessionExportSheet()
    {
        if (state.CurrentSession is null) return;
        _ = ShowSessionExportSheetAsync();
    }

    private async Task ShowSessionExportSheetAsync()
    {
        try
        {
            var sheet = new SessionExportSheet { DataContext = state };
            await sheet.ShowDialog(this);
        }
        catch (Exception ex) { state.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }
}
