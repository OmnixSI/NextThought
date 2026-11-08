using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Windows;

public sealed class MaterialWindow : Window
{
    private readonly MaterialStore store;
    private readonly ListBox documents = new(), blocks = new() { DisplayMemberPath = "Location" };
    private readonly TextBox text = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) };
    private readonly Image preview = new() { Stretch = System.Windows.Media.Stretch.Uniform, MaxHeight = 650 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private readonly Button import = new() { Content = "导入资料" }, cancel = new() { Content = "取消导入", IsEnabled = false };
    private readonly CheckBox enhancedOcr = new() { Content = "增强 OCR（CPU 慢速，每页数分钟）", ToolTip = "仅对图片和扫描 PDF 生效，需要独立 Paddle 环境；可取消，最多 10 页/文件，结果需对照原页。", VerticalAlignment = VerticalAlignment.Center };
    private CancellationTokenSource? importing, rendering;
    public MaterialWindow(AppPaths paths, SourceCitation? source = null)
    {
        store = new(paths); Title = "资料库与来源定位"; Width = 1120; Height = 790; MinWidth = 880; MinHeight = 620;
        var root = new DockPanel { Margin = new Thickness(16) }; Content = root;
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; controls.Children.Add(import); controls.Children.Add(cancel); controls.Children.Add(enhancedOcr);
        var open = new Button { Content = "打开原文件" }; controls.Children.Add(open); DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(230) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(170) }); grid.ColumnDefinitions.Add(new()); root.Children.Add(grid);
        grid.Children.Add(documents); Grid.SetColumn(blocks, 1); grid.Children.Add(blocks);
        var content = new Grid(); content.RowDefinitions.Add(new() { Height = new GridLength(2, GridUnitType.Star) }); content.RowDefinitions.Add(new() { Height = new GridLength(3, GridUnitType.Star) });
        content.Children.Add(text); var scroll = new ScrollViewer { Content = preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); content.Children.Add(scroll); Grid.SetColumn(content, 2); grid.Children.Add(content);
        documents.SelectionChanged += (_, _) => { if (documents.SelectedItem is MaterialDocument d) { blocks.ItemsSource = d.Blocks; blocks.SelectedIndex = 0; status.Text = string.Join("\n", d.Warnings); } };
        blocks.SelectionChanged += async (_, _) => await ShowBlockAsync();
        import.Click += async (_, _) => await ImportAsync(); cancel.Click += (_, _) => importing?.Cancel();
        open.Click += (_, _) => { if (documents.SelectedItem is MaterialDocument d) try { Process.Start(new ProcessStartInfo(store.SourcePath(d)) { UseShellExecute = true }); } catch (Exception ex) { status.Text = ex.Message; } };
        Closing += (_, e) => { if (importing is not null) { importing.Cancel(); e.Cancel = true; status.Text = "正在取消导入，请稍后关闭。"; } else rendering?.Cancel(); };
        documents.ItemsSource = store.List();
        if (source is not null)
        {
            documents.SelectedItem = ((IReadOnlyList<MaterialDocument>)documents.ItemsSource).FirstOrDefault(x => x.Id == source.DocumentId && x.Sha256 == source.Sha256);
            if (documents.SelectedItem is MaterialDocument d) blocks.SelectedItem = d.Blocks.FirstOrDefault(b => b.Location == source.Location);
            else status.Text = "引用原文件未找到，引用摘录仍保存在回答记录中。";
        }
        else documents.SelectedIndex = 0;
    }
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "支持的资料|*.pdf;*.docx;*.pptx;*.xlsx;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.txt;*.md" };
        if (dialog.ShowDialog(this) != true) return;
        import.IsEnabled = enhancedOcr.IsEnabled = false; cancel.IsEnabled = true; importing = new(); var enhanced = enhancedOcr.IsChecked == true;
        try
        {
            foreach (var path in dialog.FileNames)
            {
                status.Text = "正在读取 " + Path.GetFileName(path) + "…";
                await Task.Run(() => store.ImportAsync(path, importing.Token, enhanced));
            }
            status.Text = "已导入并保存原文件。PDF/图片可预览原页，Office 按段落、幻灯片或单元格定位。";
        }
        catch (OperationCanceledException) { status.Text = "已取消；本批次已经完成的资料保留。"; }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { importing.Dispose(); importing = null; import.IsEnabled = enhancedOcr.IsEnabled = true; cancel.IsEnabled = false; documents.ItemsSource = store.List(); documents.SelectedIndex = 0; }
    }
    private async Task ShowBlockAsync()
    {
        rendering?.Cancel(); var request = rendering = new(); preview.Source = null;
        if (documents.SelectedItem is not MaterialDocument document || blocks.SelectedItem is not DocumentBlock block) return;
        text.Text = $"{document.Name} · {block.Location}\nSHA-256: {document.Sha256}\n\n{block.Text}";
        try
        {
            var bytes = await Task.Run(() => DocumentPreview.RenderAsync(store.SourcePath(document), block.Page ?? 1, request.Token));
            if (bytes is null || request.IsCancellationRequested) return;
            using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); preview.Source = image;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!request.IsCancellationRequested) status.Text = "预览失败：" + ex.Message; }
        finally { if (ReferenceEquals(rendering, request)) rendering = null; request.Dispose(); }
    }
}
