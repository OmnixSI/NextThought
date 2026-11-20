using System.Text.Json;
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
        try
        {
            if (CurrentSession is { } meta)
            {
                var path = Path.Combine(Sessions.Folder(meta.Id), "speakers.json");
                var map = names.ToDictionary(pair => pair.Key.ToString(), pair => new { name = pair.Value.Name, me = pair.Value.IsMe });
                File.WriteAllText(path, JsonSerializer.Serialize(map, JsonDefaults.Options));
            }
        }
        catch { }
        SetStatus("说话人命名已应用到当前转写。");
    }

    private void LoadSpeakerNames(Guid sessionId)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(sessionId), "speakers.json");
            if (!File.Exists(path)) return;
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path), JsonDefaults.Options);
            if (map is null) return;
            foreach (var row in Transcript)
            {
                if (row.Voice is not { } voice || !map.TryGetValue(voice.ToString(), out var info)) continue;
                var name = info.TryGetProperty("name", out var value) ? value.GetString() ?? "" : "";
                var isMe = info.TryGetProperty("me", out var me) && me.GetBoolean();
                var suffix = isMe ? " · 我" : string.IsNullOrWhiteSpace(name) ? "" : " · " + name;
                if (suffix.Length > 0 && !row.Label.Contains(suffix, StringComparison.Ordinal)) row.Label += suffix;
            }
        }
        catch { }
    }
}
