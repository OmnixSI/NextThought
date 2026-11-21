namespace NextThought.Desktop;

/// <summary>
/// task-62/①: the structured notes pane follows the Mac layout rule — below 520 points the fishbone
/// chart becomes the collapsible FishboneTree list (Views/Notes/FishboneTree.cs).
/// </summary>
public partial class AppState
{
    public const double FishboneTreeWidth = 520;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private double notesStructuredWidth;

    public bool NotesUseFishboneTree => NotesStructuredWidth > 0 && NotesStructuredWidth < FishboneTreeWidth;
    public bool NotesShowWideFishbone => HasFishboneDrawing && !NotesUseFishboneTree;
    public bool NotesShowFishboneTree => HasFishboneDrawing && NotesUseFishboneTree;

    public void SetNotesStructuredWidth(double width)
    {
        if (Math.Abs(NotesStructuredWidth - width) < 1) return;
        NotesStructuredWidth = width;
    }

    partial void OnNotesStructuredWidthChanged(double value) => RaiseFishboneLayout();
    public void RaiseFishboneLayout()
    {
        OnPropertyChanged(nameof(NotesUseFishboneTree));
        OnPropertyChanged(nameof(NotesShowWideFishbone));
        OnPropertyChanged(nameof(NotesShowFishboneTree));
    }
}
