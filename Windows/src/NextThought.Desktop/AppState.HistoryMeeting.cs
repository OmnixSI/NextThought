using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §3.4 history: the Mac right pane embeds MeetingPage, so a content-search hit opens the meeting
/// page at that hit (HistoryView.openFromOverview + initialTarget). Windows reuses NotesView and
/// AppState.ShowNotesTranscriptAt for the transcript-target case.
/// </summary>
public partial class AppState
{
    /// <summary>HistoryView.searchHitNavigator: jump inside the embedded meeting page when a hit moves.</summary>
    public void OpenHistoryHitInMeeting(SessionSearchHit? hit)
    {
        if (hit is null) return;
        switch (hit.Kind)
        {
            case SessionSearchFieldKind.Transcript:
            case SessionSearchFieldKind.CorrectedTranscript:
            case SessionSearchFieldKind.Translation:
                if (ParseHitSeconds(hit.Location) is { } seconds) ShowNotesTranscriptAt(seconds);
                else NotesTabIndex = 1;
                break;
            case SessionSearchFieldKind.Question:
            case SessionSearchFieldKind.Answer:
                NotesTabIndex = 2;
                break;
            default:
                NotesTabIndex = 0;
                break;
        }
    }

    private static int? ParseHitSeconds(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var parts = location.Trim().Split(':');
        if (parts.Length is < 2 or > 3) return null;
        var total = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part.Trim(), out var value) || value < 0) return null;
            total = total * 60 + value;
        }
        return total;
    }

    /// <summary>HistoryView「重试读取」: reload the archive list without losing the current scope.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void RetryHistoryRead() => RefreshHistory();
}
