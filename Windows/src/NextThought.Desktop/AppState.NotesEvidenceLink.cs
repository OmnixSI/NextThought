using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-78/§3.4 notes item 6: selecting a 重点 in the personal notes links the notes evidence sidebar
/// to the evidence row that carries the same sentence (Mac's selection → evidence pane).
/// </summary>
public partial class AppState
{
    [ObservableProperty] private bool hasSelectedNotesEvidence;
    [ObservableProperty] private string selectedNotesEvidenceText = "";

    public void SelectNotesEvidenceForHighlight(string quote)
    {
        HasSelectedNotesEvidence = false;
        SelectedNotesEvidenceText = "";
        if (string.IsNullOrWhiteSpace(quote)) return;
        var match = NotesEvidence.FirstOrDefault(row =>
            row.Claim.Contains(quote, StringComparison.Ordinal)
            || quote.Contains(row.Claim, StringComparison.Ordinal));
        if (match is null) return;
        SelectedNotesEvidenceText = match.Claim;
        HasSelectedNotesEvidence = true;
    }
}
