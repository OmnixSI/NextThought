using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// components-6: PillTabs select their item by index; these bridges keep the design-system index in
/// step with the existing domain state (notes format / corrected transcript version).
/// </summary>
public partial class AppState
{
    private static readonly NotesFormat[] NotesFormatOrder =
        [NotesFormat.Standard, NotesFormat.Outline, NotesFormat.Fishbone, NotesFormat.Viewpoints];

    [ObservableProperty] private int notesFormatIndex;
    [ObservableProperty] private int correctedTranscriptVersionIndex;

    partial void OnNotesFormatIndexChanged(int value)
    {
        if (value < 0 || value >= NotesFormatOrder.Length) return;
        if (SelectedNotesFormat != NotesFormatOrder[value]) SelectedNotesFormat = NotesFormatOrder[value];
    }

    partial void OnSelectedNotesFormatChanged(NotesFormat value)
    {
        var index = System.Array.IndexOf(NotesFormatOrder, value);
        if (index >= 0 && NotesFormatIndex != index) NotesFormatIndex = index;
    }

    partial void OnCorrectedTranscriptVersionIndexChanged(int value)
    {
        var original = value == 1;
        if (CorrectedTranscriptShowOriginal != original) CorrectedTranscriptShowOriginal = original;
    }
}
