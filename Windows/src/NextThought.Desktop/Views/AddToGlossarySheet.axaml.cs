using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E22: AddToGlossarySheet.swift — the transcript line it came from, the canonical spelling and the
/// misheard one. The caller pre-fills the misheard spelling (the find hit knows the recorded form);
/// picking text from the transcript and copying it stays a manual step.
/// </summary>
public partial class AddToGlossarySheet : Window
{
    public AddToGlossarySheet()
    {
        InitializeComponent();
        // AddToGlossarySheet.onAppear: prefill the misheard spelling from the clipboard when the
        // copied text is a short fragment of the source line (Mac prefillFromClipboard).
        Opened += async (_, _) => await PrefillFromClipboardAsync();
    }

    public async Task PrefillFromClipboardAsync()
    {
        try
        {
            if (DataContext is not AppState state) return;
            if (state.GlossaryDraftHeard.Trim().Length > 0) return;
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            var text = (await clipboard.TryGetTextAsync() ?? "").Trim();
            if (text.Length is < 2 or > 40 || text.Contains('\n')) return;
            if (!state.GlossaryDraftLine.Contains(text, StringComparison.Ordinal)) return;
            state.GlossaryDraftHeard = text;
        }
        catch { }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        if (!state.CommitAddToGlossary()) { state.SetStatus(UiText.T("规范写法不能为空。"), true); return; }
        Close();
    }
}
