using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string coachSummaryText = "";

    /// <summary>E10: fold the meeting transcript into the coach summary and keep it beside the notes.</summary>
    public void BuildCoachSummary()
    {
        if (CurrentSession is not { } meta) return;
        var accumulator = new CoachSummaryAccumulator();
        var rows = Transcript.ToList();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var start = Math.Max(0, (row.Date - meta.StartedAt).TotalSeconds);
            var end = index + 1 < rows.Count
                ? Math.Max(start, (rows[index + 1].Date - meta.StartedAt).TotalSeconds)
                : start + Math.Max(2, row.Text.Length / 4.0);
            accumulator.Ingest(new SpokenSegment(row.Speaker == "me", start, end, row.Text));
        }
        var summary = accumulator.Summary();
        if (summary.IsEmpty) { CoachSummaryText = ""; return; }
        CoachSummaryText = Format(summary);
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "coach-summary.json");
            File.WriteAllText(path, JsonSerializer.Serialize(summary, JsonDefaults.Options));
        }
        catch { }
    }


    private void LoadCoachSummary(Guid sessionId)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(sessionId), "coach-summary.json");
            if (!File.Exists(path)) { CoachSummaryText = ""; return; }
            var summary = JsonSerializer.Deserialize<CoachSummary>(File.ReadAllText(path), JsonDefaults.Options);
            CoachSummaryText = summary is null || summary.IsEmpty ? "" : Format(summary);
        }
        catch { CoachSummaryText = ""; }
    }

    private static string Format(CoachSummary summary)
    {
        var parts = new List<string>();
        if (summary.Pace is { } pace) parts.Add(UiText.Format("语速 %.0f 单位/分钟", pace));
        if (summary.TalkShare is { } share) parts.Add(UiText.Format("发言占比 %.0f%%", share * 100));
        if (summary.LongestMonologue >= CoachSummaryAccumulator.MinimumMonologue) parts.Add(UiText.Format("最长独白 %.0f 秒", summary.LongestMonologue));
        if (summary.EyeContact is { } contact) parts.Add(UiText.Format("看镜头 %.0f%%", contact * 100));
        return string.Join(" · ", parts);
    }
}
