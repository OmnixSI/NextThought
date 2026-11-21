using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E12: Mac SpeakerStrip + SpeakerEditor + MeEditor. Capsules pick a person; the editor names them,
/// marks them as 我, or merges them into another person. Everything is written into meta.json.
/// </summary>
public partial class SpeakerEditorWindow : Window
{
    public SpeakerEditorWindow() => InitializeComponent();

    private void SelectPerson_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SpeakerPersonRow person } || DataContext is not AppState state) return;
        state.SelectSpeakerPersonCommand.Execute(person);
    }

    private void MergeInto_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SpeakerPersonRow target } || DataContext is not AppState state) return;
        state.MergeSelectedSpeakerCommand.Execute(target);
    }

    /// <summary>Quick action from the old flat editor: merge the selected person into the one before.</summary>
    private void MergePrevious_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState { SelectedSpeaker: { } person } state) return;
        var index = state.SpeakerPeople.IndexOf(person);
        if (index <= 0 || state.SpeakerPeople[index - 1] is not { } into) return;
        state.MergeSelectedSpeakerCommand.Execute(into);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.OpenSpeakerEditorCommand.Execute(null);
        Close();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
