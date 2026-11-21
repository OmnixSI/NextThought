using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E21 / §6.7: the export sheet (port of SessionExportUI.swift SessionExportSheet). Three pickers —
/// 格式 / 转写 / 语言 — drive one projection; the preview shows the same text the file gets; damaged
/// archives pause the export; the export button is the default action.
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
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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

        // A damaged archive pauses the export with the Mac caution block.
        var readIssue = state.SessionExportReadIssue;
        ReadIssuePanel.IsVisible = readIssue.Length > 0;
        ReadIssueText.Text = readIssue;
        ExportButton.IsEnabled = readIssue.Length == 0 && text.Length > 0;

        // Derived notes/hand notes may be stale, or came from an old archive without a source revision
        // (Mac projection.derivedContentStatus); the window shows the Mac caution/secondary notice.
        var derivedStatus = state.SessionExportDerivedContentStatus;
        var showDerivedNotice = SessionExportContent.UsesPageLayout(format)
                                && state.SessionExportHasDerivedNotes
                                && derivedStatus != SessionDerivedContentStatus.Current;
        StaleNotice.IsVisible = showDerivedNotice;
        if (showDerivedNotice)
        {
            var isStale = derivedStatus == SessionDerivedContentStatus.Stale;
            StaleIcon.Text = ResourceString(isStale ? "IconExclamationmarkTriangleFill" : "IconInfoCircle");
            StaleIcon.Foreground = ResourceBrush(isStale ? "Caution" : "Secondary");
            StaleText.Text = state.SessionExportDerivedContentNotice;
            StaleText.Foreground = ResourceBrush(isStale ? "Caution" : "Secondary");
        }

        // Subtitle formats only: estimated endings and possibly stale translations.
        var (estimated, stale) = state.SessionExportSubtitleWarnings(SelectedVariant, SelectedLanguage);
        var subtitleFormat = !SessionExportContent.UsesPageLayout(format);
        EstimatedWarning.IsVisible = subtitleFormat && estimated > 0;
        if (estimated > 0) EstimatedText.Text = UiText.Format("%lld 段结束时间为估算值", estimated);
        StaleTranslationWarning.IsVisible = subtitleFormat && stale > 0;
        if (stale > 0) StaleTranslationText.Text = UiText.Format("%lld 条译文可能已过期", stale);
        SubtitleWarnings.IsVisible = EstimatedWarning.IsVisible || StaleTranslationWarning.IsVisible;

        StatusText.Text = state.SessionExportStatusText;
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (state is null || !ExportButton.IsEnabled) return;
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
            StatusText.Text = state.SessionExportStatusText;
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>Mac: the export button has the default action shortcut (⏎).</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || !ExportButton.IsEnabled) return;
        e.Handled = true;
        Export_Click(ExportButton, new RoutedEventArgs());
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Theme resource lookup for the stale notice (Application.TryFindResource).</summary>
    private static string ResourceString(string key) =>
        Avalonia.Application.Current?.TryFindResource(key, out var value) == true && value is string text ? text : "";

    private static IBrush ResourceBrush(string key) =>
        Avalonia.Application.Current?.TryFindResource(key, out var value) == true && value is IBrush brush
            ? brush : Brushes.Gray;

    /// <summary>E21 evidence hook: selects a format/variant/language and refreshes the preview.</summary>
    public void SelectForCheck(int formatIndex, int variantIndex = 0, int languageIndex = 0)
    {
        FormatBox.SelectedIndex = formatIndex;
        VariantBox.SelectedIndex = variantIndex;
        LanguageBox.SelectedIndex = languageIndex;
        UpdatePreview();
    }
}
