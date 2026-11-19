using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A transcript row as the interface shows it (label, text, translation).</summary>
public partial class TranscriptRow : ObservableObject
{
    public TranscriptRow(TranscriptLine line)
    {
        Id = line.Id;
        Label = BuildLabel(line);
        Text = line.Text;
        TranslationRaw = line.Translation is { } t ? t.Text : "";
        Translation = TranslationRaw.Length == 0 ? "" : (line.Translation!.Direction == "zh-en" ? "英译：" : "中译：") + TranslationRaw;
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
    public string TranslationRaw { get; }
    public bool HasTranslation => Translation.Length > 0;
    public bool IsSystem => Speaker == "remote";

    private static string BuildLabel(TranscriptLine line) =>
        $"{line.Date.ToLocalTime():HH:mm:ss} · {(line.Speaker == "remote" ? "系统声音" : line.Speaker == "room" ? "现场" : "麦克风")}" +
        (line.Voice is { } voice ? $" · 说话人{voice + 1}" : "") +
        (line.SuspectedEcho == true ? " · 可能回声" : "");
}

public sealed record ProviderPreset(string Name, string BaseUrl, string Model)
{
    public override string ToString() => Name;
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

public sealed record SceneChoice(NextThought.Core.MeetingMode Mode, string Title, string Subtitle)
{
    public override string ToString() => UiText.T(Title);
}

public sealed record AutoEndChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}
