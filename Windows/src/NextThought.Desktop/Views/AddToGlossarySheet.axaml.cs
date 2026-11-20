using Avalonia.Controls;
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
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        if (!state.CommitAddToGlossary()) { state.SetStatus(UiText.T("规范写法不能为空。"), true); return; }
        Close();
    }
}
