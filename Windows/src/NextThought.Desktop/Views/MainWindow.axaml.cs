using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly AppState state;
    private bool closing;

    public MainWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
        state.PickSaveFile = PickSaveFileAsync;
        state.PickOpenFile = PickOpenFileAsync;
        state.CopyText = async text =>
        {
            if (Clipboard is null) return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(text));
            await Clipboard.SetDataAsync(transfer);
        };
        state.Notify = ShowMessageAsync;
        state.Initialized += HandleInitialized;
        state.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.LiveMode)) ApplyLiveMode(state.LiveMode); };
        Opened += async (_, _) =>
        {
            await state.InitializeAsync();
            if (StartupOptions.Island) ToggleCueIsland();
            if (StartupOptions.CheckReport is { } report) await WriteStartupReportAsync(report);
        };
        Closing += OnClosing;
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MinimizeButton, WindowDecorationsElementRole.MinimizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MaximizeButton, WindowDecorationsElementRole.MaximizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(CloseButton, WindowDecorationsElementRole.CloseButton);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Off-screen startup self-check: writes a JSON report and exits, like the WPF baseline.</summary>
    private async Task WriteStartupReportAsync(string path)
    {
        await Task.Delay(400);
        var passed = state.Ready && state.StartupErrorText is null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var report = new
            {
                testedAt = DateTimeOffset.Now,
                passed,
                scope = "Avalonia 12 startup self-check; layout and interactive acceptance are measured separately",
                seconds = Math.Round(Program.Clock.Elapsed.TotalSeconds, 3),
                section = state.Section.ToString(),
                ready = state.Ready,
                error = state.StartupErrorText,
                historyCount = state.History.Count,
                transcriptCount = state.Transcript.Count,
                initTimings = state.InitTimings,
                version = typeof(AppState).Assembly.GetName().Version?.ToString(3),
                framework = Environment.Version.ToString(),
                workingSetMiB = Math.Round(Environment.WorkingSet / 1048576.0, 1),
                process = Environment.ProcessPath
            };
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            passed = false;
        }
        Console.WriteLine(passed ? "startup-check: passed" : "startup-check: failed");
        Close();
    }

    // The design-time constructor required by the XAML compiler for previews.
    public MainWindow() : this(new AppState()) { }

    private CueIslandWindow? island;
    private SubtitleBarWindow? subtitleBar;

    /// <summary>Show or hide the floating cue island (the Mac app's notch panel).</summary>
    public void ToggleCueIsland()
    {
        if (island is { IsVisible: true }) { island.Hide(); return; }
        island ??= new CueIslandWindow(state);
        island.Show();
        island.Activate();
    }

    /// <summary>The subtitle bar follows the three-state live window mode from Core.</summary>
    private void ApplyLiveMode(LiveWindowMode mode)
    {
        if (mode == LiveWindowMode.Notes)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        if (mode == LiveWindowMode.Hidden)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        subtitleBar ??= new SubtitleBarWindow(state);
        if (!subtitleBar.IsVisible) subtitleBar.Show();
    }

    private void HandleInitialized()
    {
        if (state.StartupErrorText is { } error) state.SetStatus(error, true);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = (Avalonia.Media.IBrush?)this.FindResource("Surface") ?? Avalonia.Media.Brushes.White,
            Content = new TextBlock
            {
                Text = message,
                Margin = new Avalonia.Thickness(24),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task<string?> PickSaveFileAsync(string filter, string suggestedName)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存",
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.'),
            FileTypeChoices = [BuildType(filter)]
        });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFileAsync(string filter)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开",
            AllowMultiple = false,
            FileTypeFilter = [BuildType(filter), new FilePickerFileType("全部文件") { Patterns = ["*"] }]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static FilePickerFileType BuildType(string filter)
    {
        var parts = filter.Split('|');
        var name = parts.Length > 1 ? parts[0] : "文件";
        var patterns = (parts.Length > 1 ? parts[1] : "*").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new FilePickerFileType(name) { Patterns = patterns };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private async void History_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is RecentMeetingRow row) await state.SelectHistoryAsync(row.Meta);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        closing = true;
        try
        {
            await state.ShutdownAsync();
        }
        catch
        {
            // Never block exit on shutdown errors; everything already written stays on disk.
        }
        Close();
    }
}
