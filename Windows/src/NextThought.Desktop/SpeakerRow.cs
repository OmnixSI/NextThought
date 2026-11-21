using CommunityToolkit.Mvvm.ComponentModel;

namespace NextThought.Desktop;

/// <summary>E12: one detected speaker in the live meeting (legacy flat editor row).</summary>
public partial class SpeakerRow : ObservableObject
{
    public SpeakerRow(int? voice, string label) { Voice = voice; Label = label; }
    public int? Voice { get; }
    public string Label { get; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private bool isMe;
}

/// <summary>
/// E12/Mac SpeakerStrip: one person above the transcript. The user is not numbered and shows 我
/// (Mac: number 0); other people get 1-based numbers and a small shape instead of a colour.
/// </summary>
public sealed class SpeakerPersonRow
{
    public SpeakerPersonRow(int voice, int number, string label, int lines, bool isMe)
    {
        Voice = voice;
        Number = number;
        Label = label;
        Lines = lines;
        IsMe = isMe;
    }

    public int Voice { get; }
    /// <summary>0 for the user (Mac isMe), else the 1-based speaker number.</summary>
    public int Number { get; }
    public string Label { get; }
    public int Lines { get; }
    public bool IsMe { get; }

    /// <summary>Mac SpeakerMark: person for the user, circle/square/triangle/diamond otherwise. These
    /// are the bundled Fluent System Icons glyphs (IconPersonFill / IconCircle / IconSquare /
    /// IconTriangle / IconDiamond in Styles/Icons.axaml), not geometry characters.</summary>
    public string MarkGlyph => Number switch
    {
        1 => "\uF2BC", // circle_24_regular
        2 => "\uF292", // square_24_regular
        3 => "\uF03A5", // triangle_24_regular
        0 => "\uF5BE", // person_24_regular
        _ => "\uE4BC" // diamond_24_regular
    };

    /// <summary>Legacy geometry mark, kept for callers that only compare the shape.</summary>
    public string Mark => Number switch { 1 => "●", 2 => "■", 3 => "▲", 0 => "○", _ => "◆" };

    public string DisplayLabel => IsMe ? UiText.T("我") : Label;
    public string LinesText => UiText.Format("%lld 句", Lines);
    public string Tooltip => IsMe ? UiText.T("这是你自己的声音") : UiText.T("改名字，或和另一个人合并");
}
