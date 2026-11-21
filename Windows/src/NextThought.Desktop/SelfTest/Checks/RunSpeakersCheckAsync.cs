using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E12: drives the speakers document strip/editor on an isolated archive — Mac-compatible
    /// meta.json labels (voiceNames/meVoices), renaming, marking 我, merging voices, and the my-voice
    /// settings surface. Real microphone recording is NOT exercised here (no microphone in the
    /// self-check); it is recorded as unverified in the compare notes.
    /// </summary>
    private async Task RunSpeakersCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var meta = new SessionMeta { Title = "E12 说话人自检会议" };
            await store.CreateAsync(meta);
            var lines = new List<TranscriptLine>();
            for (var index = 0; index < 6; index++)
                lines.Add(new TranscriptLine
                {
                    Date = meta.StartedAt.AddSeconds(index), Speaker = "room",
                    Text = "陈老师的第六条发言要检查这一句是否清晰 " + index
                });
            for (var index = 0; index < 5; index++)
                lines.Add(new TranscriptLine
                {
                    Date = meta.StartedAt.AddSeconds(10 + index), Speaker = "room",
                    Text = "我的发言被认成另一个声音 " + index
                });
            for (var index = 0; index < 6; index++)
                lines.Add(new TranscriptLine
                {
                    Date = meta.StartedAt.AddSeconds(20 + index), Speaker = "room",
                    Text = "另一位同事的第六条发言 " + index
                });
            foreach (var line in lines) await store.AppendTranscriptAsync(meta, line);
            for (var index = 0; index < 6; index++) await store.AppendVoiceTagAsync(meta.Id, lines[index].Id, 0);
            for (var index = 6; index < 11; index++) await store.AppendVoiceTagAsync(meta.Id, lines[index].Id, 1);
            for (var index = 11; index < 17; index++) await store.AppendVoiceTagAsync(meta.Id, lines[index].Id, 2);
            await store.SetVoiceLabelAsync(meta.Id, 0, "陈老师");
            await store.SetVoiceLabelAsync(meta.Id, 1, null, isMe: true);
            await store.EndAsync(meta);

            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            await state.SelectHistoryAsync(state.History.First(row => row.Meta.Id == meta.Id).Meta);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);

            // 1) SpeakerStrip：我（0 号）+ 陈老师（说话人 1），句数与 meta 一致。
            // The demo home stage keeps a meeting running; my-voice setup is a stand-by action
            // (Mac blocks it during recording), so end the demo meeting before the my-voice step.
            if (state.Recording)
            {
                state.StopRecordingCommand.Execute(null);
                for (var attempt = 0; attempt < 120 && state.Recording; attempt++) await Task.Delay(100);
            }

            state.OpenSpeakerEditorCommand.Execute(null);
            var people = state.SpeakerPeople.ToList();
            var chen = people.First(person => !person.IsMe && person.Label == "陈老师");
            Step("说话人条：我 + 陈老师 + 说话人", state.SpeakerStripVisible && people.Count == 3
                && people[0].IsMe && people[0].DisplayLabel == "我"
                && chen.Lines == 6,
                "人数 " + people.Count + "；" + string.Join(" / ", people.Select(person => person.DisplayLabel + "(" + person.Lines + "句)")));

            // 2) 命名：写入 meta.json voiceNames。
            state.SelectSpeakerPersonCommand.Execute(chen);
            state.SelectedSpeakerName = "李老师";
            state.SaveSpeakerEditCommand.Execute(null);
            await Task.Delay(400);
            var savedMeta = new SessionStore(state.Paths).Load(meta.Id).Meta;
            Step("命名写入 meta.json", savedMeta.VoiceNames?.GetValueOrDefault("0") == "李老师",
                "voiceNames=" + string.Join(",", savedMeta.VoiceNames ?? new Dictionary<string, string>()));

            // 3) 转写标签跟着改名（Mac SpeakerTag）。
            Step("转写行标签显示名字", state.Transcript.Any(row => row.Label.Contains("李老师")),
                state.Transcript.FirstOrDefault(row => row.Label.Contains("李老师"))?.Label ?? "");

            // 4) 合并：把我的声音并入李老师，声纹标记随行（meta meVoices + voices.jsonl）。
            state.OpenSpeakerEditorCommand.Execute(null);
            var me = state.SpeakerPeople.First(person => person.IsMe);
            var named = state.SpeakerPeople.First(person => !person.IsMe);
            state.SelectSpeakerPersonCommand.Execute(me);
            state.MergeSelectedSpeakerCommand.Execute(named);
            await Task.Delay(600);
            var merged = new SessionStore(state.Paths).Load(meta.Id);
            Step("合并两个声音（记录跟随、名字保留）", merged.Lines.All(line => line.Voice is 0 or 2)
                && merged.Meta.MeVoices is [0] && merged.Meta.VoiceNames?.GetValueOrDefault("0") == "李老师",
                "line voices=" + string.Join(",", merged.Lines.Select(line => line.Voice).Distinct()));

            // 5) 截图：说话人窗口（窗口数据已刷新）。
            state.OpenSpeakerEditorCommand.Execute(null);
            var speakerWindow = new SpeakerEditorWindow { DataContext = state };
            speakerWindow.Show();
            await Task.Delay(700);
            CaptureVisual(speakerWindow, Path.Combine(directory, "speaker-strip-light.png"));
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Task.Delay(350);
            CaptureVisual(speakerWindow, Path.Combine(directory, "speaker-strip-dark.png"));
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(250);
            speakerWindow.Close();

            // 6) 我的声音设置/录入面板（无麦克风，不实际录音）。
            var voiceWindow = new MyVoiceWindow { DataContext = state };
            voiceWindow.Show();
            await Task.Delay(600);
            CaptureVisual(voiceWindow, Path.Combine(directory, "my-voice-light.png"));
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Task.Delay(350);
            CaptureVisual(voiceWindow, Path.Combine(directory, "my-voice-dark.png"));
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(250);
            voiceWindow.Close();
            state.BeginMyVoiceSetupCommand.Execute(null);
            Step("我的声音：录入流程可打开（录音未执行）", state.MyVoiceSetupVisible
                && state.MyVoiceTakeHint.Length > 0 && state.MyVoiceRecordLabel.Length > 0
                && state.MyVoiceTotalTakes == 4,
                "visible=" + state.MyVoiceSetupVisible + " label=" + state.MyVoiceRecordLabel + " takes=" + state.MyVoiceTotalTakes + " hint=" + state.MyVoiceTakeHint);
            state.CancelMyVoiceSetupCommand.Execute(null);

            Step("真实麦克风录音未在自检中执行", true, "需要麦克风与声纹组件；见 compare.md 未解决项");
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "speakers-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "speakers-check: passed" : "speakers-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
