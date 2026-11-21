using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A transcript row as the interface shows it (label, text, translation).</summary>
public partial class TranscriptRow : ObservableObject
{
    private readonly string clock;
    private readonly string speakerKey;
    private readonly string directionKey;

    public TranscriptRow(TranscriptLine line)
    {
        Id = line.Id;
        clock = line.Date.ToLocalTime().ToString("HH:mm:ss");
        speakerKey = line.Speaker == "remote" ? "系统声音" : line.Speaker == "room" ? "现场" : "麦克风";
        directionKey = line.Translation?.Direction == "zh-en" ? "英译：" : "中译：";
        Text = line.Text;
        TranslationRaw = line.Translation is { } t ? t.Text : (line.TranslatedText ?? "");
        Label = BuildLabel();
        Translation = BuildTranslation();
        Speaker = line.Speaker;
        Date = line.Date;
        IsEcho = line.SuspectedEcho == true;
        Voice = line.Voice;
    }

    public Guid Id { get; }
    public string Speaker { get; }
    public DateTimeOffset Date { get; }
    public bool IsEcho { get; }
    public int? Voice { get; }
    [ObservableProperty] private string label;
    [ObservableProperty] private string text;
    [ObservableProperty] private string translation;
    [ObservableProperty] private bool isHighlighted;
    public string TranslationRaw { get; }
    public bool HasTranslation => Translation.Length > 0;
    public bool IsSystem => Speaker == "remote";

    /// <summary>E4/QuestionMark: the detector sees a question in this line (remote speakers only).</summary>
    public bool IsQuestion => Speaker != "me" && !IsEcho && QuestionDetector.LooksLikeQuestion(Text);

    /// <summary>G8: the speaker label follows the interface language without rebuilding the collection.</summary>
    public void RefreshLanguage()
    {
        Label = BuildLabel();
        Translation = BuildTranslation();
    }

    private string BuildLabel() =>
        clock + " · " + UiText.T(speakerKey) +
        (Voice is { } voice ? UiText.Format(" · 说话人 %lld", voice + 1) : "") +
        (IsEcho ? UiText.T(" · 可能回声") : "");

    /// <summary>
    /// E12/Mac SpeakerTag: once names or the "this is me" mark are set, the voice part shows the
    /// name (or 我) instead of 说话人 N. Clearing the edits restores the plain label.
    /// </summary>
    public void ApplySpeakerLabel(string? name, bool isMe)
    {
        var voicePart = Voice is { } voice
            ? " · " + (isMe ? UiText.T("我") : string.IsNullOrWhiteSpace(name) ? UiText.Format("说话人 %lld", voice + 1) : name.Trim())
            : "";
        Label = clock + " · " + UiText.T(speakerKey) + voicePart + (IsEcho ? UiText.T(" · 可能回声") : "");
    }

    private string BuildTranslation() =>
        TranslationRaw.Length == 0 ? "" : UiText.T(directionKey) + TranslationRaw;
}

public sealed record CloudSttChoice(string Value, string Label)
{
    public override string ToString() => UiText.T(Label);
}

public sealed record CloudLanguageChoice(string Value, string Label)
{
    public override string ToString() => UiText.T(Label);
}

public sealed record AppearanceOption(string Value, string Label)
{
    public override string ToString() => UiText.T(Label);
}

public sealed record ProviderPreset(string Name, string BaseUrl, string Model)
{
    public override string ToString() => UiText.T(Name);
}

public sealed record DeviceChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record ProcessChoice(int Id, long StartTicks, string Title)
{
    public override string ToString() => UiText.T(Title);
}

public sealed record ModelChoice(string Id, string Title)
{
    public override string ToString() => UiText.T(Title);
}

public sealed record EffortChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}

/// <summary>E26 one scene card; the constructor arguments are Mac catalogue keys, the getters localize.</summary>
public sealed record SceneChoice(NextThought.Core.MeetingMode Mode, string TitleKey, string SubtitleKey)
{
    public NextThought.Core.SceneTheme Theme { get; } = NextThought.Core.SceneTheme.For(Mode);
    public IBrush Tint { get; } = new SolidColorBrush(Color.Parse(NextThought.Core.SceneTheme.For(Mode).TintLight));
    public IBrush TintDark { get; } = new SolidColorBrush(Color.Parse(NextThought.Core.SceneTheme.For(Mode).TintDark));
    public IBrush DeepTint { get; } = new SolidColorBrush(Color.Parse(NextThought.Core.SceneTheme.For(Mode).DeepLight));
    public string Symbol => Theme.Symbol;
    public string Title => UiText.T(TitleKey);
    public string Subtitle => UiText.T(SubtitleKey);
    public string Promise => UiText.T(Theme.Promise);
    public IReadOnlyList<string> Moments => Theme.Moments.Select(UiText.T).ToArray();
    /// <summary>E2: Mac SessionMode.defaultsSummary for the chip tooltip and the type line.</summary>
    public string DefaultsSummary => SceneSummary.Defaults(Mode);

    public override string ToString() => Title;
}

public sealed record CueWidthOption(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}

public sealed record AutoEndChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}
