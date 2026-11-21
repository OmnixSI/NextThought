using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E12 说话人：Mac SpeakerLayout/SpeakerStrip/SpeakerEditor/MeEditor. The people above the transcript,
/// the popover that names one person, says "这是我", or merges two voices; the choices live in the
/// meeting's own meta.json (voiceNames / meVoices) so a renamed archive reads the same on the Mac.
/// </summary>
public partial class AppState
{
    /// <summary>The legacy flat editor list (kept for the batch 应用 path and older tests).</summary>
    public System.Collections.ObjectModel.ObservableCollection<SpeakerRow> SpeakerRows { get; } = [];

    /// <summary>E12/Mac SpeakerStrip: one capsule per person recognised in this meeting.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SpeakerPersonRow> SpeakerPeople { get; } = [];

    /// <summary>E12/Mac SpeakerEditor: the other people a voice can be merged into.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SpeakerPersonRow> SpeakerMergeChoices { get; } = [];

    [ObservableProperty] private bool speakerEditorVisible;
    [ObservableProperty] private SpeakerPersonRow? selectedSpeaker;
    [ObservableProperty] private string selectedSpeakerName = "";
    [ObservableProperty] private bool selectedSpeakerIsMe;
    [ObservableProperty] private string speakerEditorStatus = "";
    [ObservableProperty] private bool speakerStripVisible;

    /// <summary>E12: the empty state when no voices were told apart yet.</summary>
    public bool HasSpeakerPeople => SpeakerPeople.Count > 0;
    public bool HasSelectedSpeaker => SelectedSpeaker is not null;
    public string SelectedSpeakerLabel => SelectedSpeaker?.DisplayLabel ?? "";
    public string SelectedSpeakerLines => SelectedSpeaker?.LinesText ?? "";
    public bool SelectedSpeakerIsCurrentMe => SelectedSpeaker?.IsMe == true;

    partial void OnSelectedSpeakerChanged(SpeakerPersonRow? value)
    {
        SelectedSpeakerName = value is null || value.IsMe ? "" : value.Label;
        SelectedSpeakerIsMe = value?.IsMe == true;
        SpeakerMergeChoices.Clear();
        if (value is not null)
        {
            foreach (var other in SpeakerPeople.Where(person => person.Voice != value.Voice && !person.IsMe))
                SpeakerMergeChoices.Add(other);
        }
        OnPropertyChanged(nameof(HasSelectedSpeaker));
        OnPropertyChanged(nameof(SelectedSpeakerLabel));
        OnPropertyChanged(nameof(SelectedSpeakerLines));
        OnPropertyChanged(nameof(SelectedSpeakerIsCurrentMe));
    }

    /// <summary>Opens the people strip/editor for the current meeting (Mac: tapping a capsule).</summary>
    [RelayCommand]
    private void OpenSpeakerEditor()
    {
        ReloadSpeakerPeople();
        var first = SpeakerPeople.FirstOrDefault(person => person.IsMe) ?? SpeakerPeople.FirstOrDefault();
        SelectedSpeaker = first;
        SpeakerEditorStatus = SpeakerPeople.Count == 0
            ? UiText.T("当前没有可命名的说话人")
            : "";
        SpeakerEditorVisible = true;
    }

    /// <summary>E12/Mac voiceNumbers: who is on show, their numbers, names and sentence counts.</summary>
    private void ReloadSpeakerPeople(params SpeakerPersonRow[] select)
    {
        SpeakerPeople.Clear();
        var meta = CurrentSession;
        var meVoices = meta?.MeVoices?.ToHashSet() ?? new HashSet<int>();
        var names = meta?.VoiceNames;
        var lines = Transcript.Select(row => new TranscriptLine
        {
            Id = row.Id, Speaker = row.Speaker, Date = row.Date, Text = row.Text, Voice = row.Voice
        }).ToList();
        var numbers = SpeakerLabels.VoiceNumbers(lines, meVoices);
        var counts = Transcript
            .Where(row => row.Speaker != "me" && row.Voice is not null)
            .GroupBy(row => row.Voice!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var named = SpeakerLabels.Names(names, lines, meVoices);
        foreach (var voice in (meta?.MeVoices ?? []).Where(counts.ContainsKey))
            SpeakerPeople.Add(new SpeakerPersonRow(voice, 0, UiText.T("我"), counts[voice], true));
        foreach (var (voice, number) in numbers.OrderBy(pair => pair.Value))
            SpeakerPeople.Add(new SpeakerPersonRow(voice, number,
                named.TryGetValue(number, out var name) && name.Length > 0
                    ? name
                    : UiText.Format("说话人 %lld", number),
                counts.TryGetValue(voice, out var count) ? count : 0, false));
        SpeakerStripVisible = SpeakerPeople.Count > 0;
        OnPropertyChanged(nameof(HasSpeakerPeople));
    }

    /// <summary>E12/Mac SpeakerEditor: choose the person whose name/merge choices are edited.</summary>
    [RelayCommand]
    private void SelectSpeakerPerson(SpeakerPersonRow? person)
    {
        if (person is null) return;
        SelectedSpeaker = person;
    }

    /// <summary>E12/Mac SpeakerEditor 完成: save the name and the "this is me" mark into meta.json.</summary>
    [RelayCommand]
    private async Task SaveSpeakerEditAsync()
    {
        if (SelectedSpeaker is not { } person || CurrentSession is not { } meta) return;
        if (person.IsMe) { SpeakerEditorVisible = false; return; }
        var name = string.IsNullOrWhiteSpace(SelectedSpeakerName) ? null : SelectedSpeakerName.Trim();
        try
        {
            await Sessions.SetVoiceLabelAsync(meta.Id, person.Voice, name, SelectedSpeakerIsMe);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ApplySpeakerLabelsToTranscript();
            ReloadSpeakerPeople();
            SpeakerEditorVisible = false;
            SetStatus(name is null
                ? UiText.Format("已清除说话人 %lld 的名字。", person.Number)
                : UiText.Format("说话人 %lld 已命名为 %@。", person.Number, name));
        }
        catch (Exception ex) { SpeakerEditorStatus = UiText.T("保存说话人设置失败：") + ex.Message; }
    }

    /// <summary>E12/Mac "不是我": takes the me mark off the selected voice.</summary>
    [RelayCommand]
    private async Task ClearSelectedSpeakerMeAsync()
    {
        if (SelectedSpeaker is not { IsMe: true } person || CurrentSession is not { } meta) return;
        try
        {
            await Sessions.SetVoiceLabelAsync(meta.Id, person.Voice, null, false);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ApplySpeakerLabelsToTranscript();
            ReloadSpeakerPeople();
            SelectedSpeaker = SpeakerPeople.FirstOrDefault();
            SpeakerEditorVisible = false;
            SetStatus(UiText.T("已取消「这是我」，这场会议里不会再识别为该声音。"));
        }
        catch (Exception ex) { SpeakerEditorStatus = UiText.T("保存说话人设置失败：") + ex.Message; }
    }

    /// <summary>E12/Mac mergeVoices: two voices are one person; names and the me mark come along.</summary>
    [RelayCommand]
    private async Task MergeSelectedSpeakerAsync(SpeakerPersonRow? target)
    {
        if (SelectedSpeaker is not { } person || target is null || person.Voice == target.Voice) return;
        if (CurrentSession is not { } meta) return;
        try
        {
            await Sessions.MergeVoicesAsync(meta.Id, person.Voice, target.Voice);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ApplySpeakerLabelsToTranscript();
            ReloadSpeakerPeople();
            SelectedSpeaker = SpeakerPeople.FirstOrDefault(row => row.Voice == target.Voice) ?? SpeakerPeople.FirstOrDefault();
            SpeakerEditorVisible = false;
            SetStatus(UiText.Format("已把 %@ 并入 %@。", person.DisplayLabel, target.DisplayLabel));
        }
        catch (Exception ex) { SpeakerEditorStatus = UiText.T("合并说话人失败：") + ex.Message; }
    }

    /// <summary>Batch 应用 (legacy flat list): persists every name/me flag and applies the labels.</summary>
    [RelayCommand]
    private async Task ApplySpeakerNamesAsync()
    {
        var names = SpeakerRows.Where(row => row.Voice is not null)
            .ToDictionary(row => row.Voice!.Value, row => (row.Name, row.IsMe));
        try
        {
            if (CurrentSession is { } meta)
            {
                foreach (var (voice, (name, isMe)) in names)
                    await Sessions.SetVoiceLabelAsync(meta.Id, voice,
                        string.IsNullOrWhiteSpace(name) ? null : name.Trim(), isMe);
                CurrentSession = Sessions.Load(meta.Id).Meta;
                ApplySpeakerLabelsToTranscript();
                ReloadSpeakerPeople();
                WriteLegacySpeakerFile(meta.Id, names);
            }
            ApplySpeakerNames(); // legacy label rewrite kept for callers that have no session file
        }
        catch (Exception ex) { SetStatus(UiText.T("保存说话人设置失败：") + ex.Message, true); }
        SpeakerEditorVisible = false;
    }

    private void WriteLegacySpeakerFile(Guid sessionId, IReadOnlyDictionary<int, (string Name, bool IsMe)> names)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(sessionId), "speakers.json");
            var map = names.ToDictionary(pair => pair.Key.ToString(),
                pair => new { name = pair.Value.Name, me = pair.Value.IsMe });
            File.WriteAllText(path, JsonSerializer.Serialize(map, JsonDefaults.Options));
        }
        catch { }
    }

    /// <summary>The old label-only batch apply; still used by the flat editor list.</summary>
    private void ApplySpeakerNames()
    {
        foreach (var row in Transcript)
        {
            if (row.Voice is not { } voice) continue;
            var info = SpeakerRows.FirstOrDefault(candidate => candidate.Voice == voice);
            if (info is null) continue;
            row.ApplySpeakerLabel(info.Name, info.IsMe);
        }
        SetStatus("说话人命名已应用到当前转写。");
    }

    /// <summary>E12: rows show 说话人 N, the given name, or 我 (Mac SpeakerTag).</summary>
    private void ApplySpeakerLabelsToTranscript()
    {
        var meta = CurrentSession;
        var meVoices = meta?.MeVoices?.ToHashSet() ?? new HashSet<int>();
        var names = meta?.VoiceNames;
        var lines = Transcript.Select(row => new TranscriptLine
        {
            Id = row.Id, Speaker = row.Speaker, Date = row.Date, Text = row.Text, Voice = row.Voice
        }).ToList();
        var numbered = SpeakerLabels.Names(names, lines, meVoices);
        var meLines = SpeakerLabels.MeLines(lines, meVoices);
        foreach (var row in Transcript)
        {
            if (row.Voice is not { } voice) continue;
            var isMe = meLines.Contains(row.Id);
            var number = SpeakerLabels.Numbers(lines, meVoices).TryGetValue(row.Id, out var value) ? value : 0;
            var name = number > 0 && numbered.TryGetValue(number, out var named) ? named : null;
            row.ApplySpeakerLabel(name, isMe);
        }
    }

    /// <summary>
    /// E12: on opening a saved meeting, prefer the Mac-compatible meta.json (voiceNames/meVoices),
    /// falling back to the old Windows speakers.json for archives made before E12.
    /// </summary>
    private void LoadSpeakerNames(Guid sessionId)
    {
        try
        {
            var data = Sessions.Load(sessionId);
            CurrentSession = data.Meta;
            ApplySpeakerLabelsToTranscript();
            ReloadSpeakerPeople();
            if (data.Meta.VoiceNames is not null || data.Meta.MeVoices is not null) return;
            var path = Path.Combine(Sessions.Folder(sessionId), "speakers.json");
            if (!File.Exists(path)) return;
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path), JsonDefaults.Options);
            if (map is null) return;
            foreach (var row in Transcript)
            {
                if (row.Voice is not { } voice || !map.TryGetValue(voice.ToString(), out var info)) continue;
                var name = info.TryGetProperty("name", out var value) ? value.GetString() ?? "" : "";
                var isMe = info.TryGetProperty("me", out var me) && me.GetBoolean();
                row.ApplySpeakerLabel(name, isMe);
            }
        }
        catch { }
    }

    /// <summary>E12: merge one voice into another; transcript rows of the merged voice get the target name.</summary>
    public void MergeSpeakerVoices(int from, int into)
    {
        _ = MergeSpeakerVoicesAsync(from, into);
    }

    private async Task MergeSpeakerVoicesAsync(int from, int into)
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            await Sessions.MergeVoicesAsync(meta.Id, from, into);
            CurrentSession = Sessions.Load(meta.Id).Meta;
            ApplySpeakerLabelsToTranscript();
            ReloadSpeakerPeople();
            SetStatus(UiText.Format("说话人 %lld 已合并到 %lld。", from + 1, into + 1));
        }
        catch (Exception ex) { SetStatus(UiText.T("合并说话人失败：") + ex.Message, true); }
    }
}
