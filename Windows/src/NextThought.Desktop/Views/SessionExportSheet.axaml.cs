using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E19: the export sheet (port of SessionExportUI.swift's SessionExportSheet). Three selectors —
/// 格式 / 转写 / 语言 — drive one projection; the preview shows the same text the file gets.
/// </summary>
public partial class SessionExportSheet : Window
{
    private static readonly SessionExportFormat[] Formats =
        [SessionExportFormat.Markdown, SessionExportFormat.Docx, SessionExportFormat.Pdf,
            SessionExportFormat.Srt, SessionExportFormat.Vtt];

    private static readonly SessionTranscriptVariant[] Variants =
        [SessionTranscriptVariant.Corrected, SessionTranscriptVariant.Original];

    private static readonly SessionLanguageView[] Languages =
        [SessionLanguageView.Bilingual, SessionLanguageView.Source, SessionLanguageView.Translated];

    private AppState? state;

    public SessionExportSheet()
    {
        InitializeComponent();
        Opened += (_, _) => Load();
    }

    private void Load()
    {
        if (DataContext is not AppState app) return;
        state = app;
        if (!app.PrepareSessionExport())
        {
            Close();
            return;
        }
        FormatBox.ItemsSource = Formats.Select(format => format.Title()).ToList();
        VariantBox.ItemsSource = new List<string> { UiText.T("当前校正版本"), UiText.T("原始记录") };
        LanguageBox.ItemsSource = new List<string> { UiText.T("原文与译文"), UiText.T("仅原文"), UiText.T("译文优先") };
        FormatBox.SelectedIndex = 0;
        VariantBox.SelectedIndex = 0;
        LanguageBox.SelectedIndex = 0;
        UpdatePreview();
    }

    private SessionExportFormat SelectedFormat =>
        Formats[Math.Clamp(FormatBox.SelectedIndex, 0, Formats.Length - 1)];

    private SessionTranscriptVariant SelectedVariant =>
        Variants[Math.Clamp(VariantBox.SelectedIndex, 0, Variants.Length - 1)];

    private SessionLanguageView SelectedLanguage =>
        Languages[Math.Clamp(LanguageBox.SelectedIndex, 0, Languages.Length - 1)];

    private void Option_Changed(object? sender, SelectionChangedEventArgs e) => UpdatePreview();

    /// <summary>SessionExportUI.swift: the same projection feeds the preview and the saved file.</summary>
    private void UpdatePreview()
    {
        if (state is null) return;
        var format = SelectedFormat;
        var text = state.BuildSessionExportText(format, SelectedVariant, SelectedLanguage);
        PreviewBox.Text = text.Length > 0 ? text : UiText.T("没有可导出的内容。");
        HintText.Text = SessionExportContent.UsesPageLayout(format)
            ? UiText.T("页面预览与导出使用同一版校正、译文和纪要内容。")
            : UiText.T("预览使用同一版转写投影；字幕时间来自语句起止时间，缺少结束时间时会标明估算。");
        var (estimated, stale) = state.SessionExportSubtitleWarnings(SelectedVariant, SelectedLanguage);
        var warnings = new List<string>();
        if (!SessionExportContent.UsesPageLayout(format))
        {
            if (estimated > 0) warnings.Add(UiText.Format("%lld 段结束时间为估算值", estimated));
            if (stale > 0) warnings.Add(UiText.Format("%lld 条译文可能已过期", stale));
        }
        var readIssue = state.SessionExportReadIssue;
        if (readIssue.Length > 0) warnings.Insert(0, readIssue);
        WarningText.Text = string.Join("  ·  ", warnings);
        WarningText.IsVisible = warnings.Count > 0;
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (state is null) return;
            var format = SelectedFormat;
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = UiText.T("导出会议"),
                SuggestedFileName = state.SuggestedSessionExportFileName(format),
                DefaultExtension = format.FileExtension(),
                ShowOverwritePrompt = true
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            await state.SaveSessionExportAsync(format, SelectedVariant, SelectedLanguage, path);
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
