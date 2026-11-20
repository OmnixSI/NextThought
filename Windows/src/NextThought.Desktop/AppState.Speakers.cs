using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

public partial class AppState
{
    public System.Collections.ObjectModel.ObservableCollection<SpeakerRow> SpeakerRows { get; } = [];

    [ObservableProperty] private bool speakerEditorVisible;

    [RelayCommand]
    private void OpenSpeakerEditor()
    {
        SpeakerRows.Clear();
        foreach (var group in Transcript.Where(row => row.Voice is not null).GroupBy(row => row.Voice!.Value))
        {
            var label = group.Key == 0 && group.All(row => row.Speaker == "me") ? "我" : $"说话人{group.Key + 1}";
            SpeakerRows.Add(new SpeakerRow(group.Key, label));
        }
        if (SpeakerRows.Count == 0) SpeakerRows.Add(new SpeakerRow(null, "当前没有可命名的说话人"));
        SpeakerEditorVisible = true;
    }

    [RelayCommand]
    private void ApplySpeakerNames()
    {
        var names = SpeakerRows.Where(row => row.Voice is not null).ToDictionary(row => row.Voice!.Value, row => row);
        foreach (var row in Transcript)
        {
            if (row.Voice is not { } voice || !names.TryGetValue(voice, out var info)) continue;
            var suffix = info.IsMe ? " · 我" : string.IsNullOrWhiteSpace(info.Name) ? "" : " · " + info.Name.Trim();
            if (suffix.Length == 0) continue;
            var baseLabel = row.Label.Split(" · 名字:")[0];
            if (!baseLabel.Contains(suffix, StringComparison.Ordinal)) row.Label = baseLabel + suffix;
        }
        SpeakerEditorVisible = false;
        SetStatus("说话人命名已应用到当前转写。");
    }
}
