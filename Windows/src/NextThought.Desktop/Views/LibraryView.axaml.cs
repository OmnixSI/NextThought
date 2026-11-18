using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NextThought.Desktop.Views;

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入资料",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("资料") { Patterns = ["*.pdf", "*.docx", "*.pptx", "*.xlsx", "*.md", "*.txt", "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tif", "*.tiff"] },
                new FilePickerFileType("全部文件") { Patterns = ["*"] }
            ]
        });
        var paths = files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Cast<string>().ToList();
        if (paths.Count > 0) await state.Library.ImportAsync(paths);
    }

    private void Reveal_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.Library.RevealSourceFile();
    }
}
