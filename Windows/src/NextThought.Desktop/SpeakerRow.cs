using CommunityToolkit.Mvvm.ComponentModel;

namespace NextThought.Desktop;

/// <summary>E12: one detected speaker in the live meeting.</summary>
public partial class SpeakerRow : ObservableObject
{
    public SpeakerRow(int? voice, string label) { Voice = voice; Label = label; }
    public int? Voice { get; }
    public string Label { get; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private bool isMe;
}
