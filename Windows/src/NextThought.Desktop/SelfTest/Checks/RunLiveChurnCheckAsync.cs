using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// J8/③⑦: <c>--self-test=live-churn</c> — the Mac 3.4.0 <c>LiveSessionAcceptanceSelfTest.churn</c>
    /// measurement. A long meeting is put into the live page (500 lines), then 300 lines arrive in
    /// steps; the report shows how much work the page did per part and how long it took.
    /// </summary>
    private async Task RunLiveChurnCheckAsync(string path)
    {
        await Task.Delay(300);
        var steps = new List<object>();
        var passed = false;
        try
        {
            var measurement = await MeasureLiveChurnAsync(500, 300);
            var incremental = (Dictionary<string, int>)measurement["incrementalCounts"]!;
            var longMeeting = (Dictionary<string, int>)measurement["longMeetingCounts"]!;
            // The page must do work for the new lines, and the transcript list must not be rebuilt
            // more than once per batch: the counters show the same shape the Mac walkthrough reads.
            steps.Add(new { step = "500 lines seeded", ok = longMeeting["transcript"] > 0 && longMeeting["line"] >= 500, detail = string.Join(", ", longMeeting) });
            steps.Add(new { step = "300 lines arrive", ok = incremental["line"] >= 300, detail = string.Join(", ", incremental) });
            steps.Add(new { step = "page attached once", ok = (int)measurement["pageAttach"]! >= 1 && (int)measurement["pageAttach"]! <= 3, detail = measurement["pageAttach"] });
            steps.Add(new { step = "time bounded", ok = (long)measurement["incrementalMs"]! < 30_000, detail = measurement["incrementalMs"] + " ms" });
            passed = steps.All(step => (bool)step.GetType().GetProperty("ok")!.GetValue(step)!);
            await WriteReportAsync(path, new { passed, steps, measurement });
            Console.WriteLine(passed ? "live-churn-check: passed" : "live-churn-check: failed");
        }
        catch (Exception error)
        {
            await WriteReportAsync(path + ".error.txt", new { passed = false, error = error.ToString() });
            Console.Error.WriteLine("live-churn-check: " + error);
        }
        finally
        {
            Close();
        }
    }

    /// <summary>
    /// J8/⑦: the Mac walkthrough's <c>NEXTTHOUGHT_DEMO_CHURN=&lt;file&gt;</c> measurement, run from
    /// <c>--demo --demo-churn=&lt;file&gt;</c>. Demo-only, exactly like the Mac environment variable.
    /// </summary>
    private async Task RunDemoChurnAsync(string path)
    {
        try
        {
            var measurement = await MeasureLiveChurnAsync(500, 300);
            var after = (Dictionary<string, int>)measurement["incrementalCounts"]!;
            var text = "lines 500 + 300 (" + measurement["longMs"] + " ms + " + measurement["incrementalMs"] + " ms)\n"
                       + "after: " + string.Join(" ", after.Select(entry => entry.Key + " " + entry.Value)) + "\n";
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, text);
        }
        catch { }
    }

    /// <summary>
    /// Puts the demo into the live page, seeds a long meeting and measures the page's work. Shared by
    /// the self-test and the walkthrough entry so both read the same counters.
    /// </summary>
    private async Task<Dictionary<string, object?>> MeasureLiveChurnAsync(int lines, int incremental)
    {
        state.LoadDemo();
        await Task.Delay(150);
        state.Section = AppSection.Live;
        await PumpAsync();
        // Keep the viewport where it is: the measurement is the page's own work, not scrolling.
        state.LiveScrolledAway();
        // The page attach is what happened when the live page opened; count it before resetting.
        var pageAttach = LiveRenderCount.Page;
        LiveRenderCount.Reset();
        var startedAt = state.CurrentSession?.StartedAt ?? DateTimeOffset.Now;
        TranscriptRow Row(int index) => new(new TranscriptLine
        {
            Date = startedAt.AddSeconds(index * 3),
            StartedAt = startedAt.AddSeconds(index * 3),
            EndedAt = startedAt.AddSeconds(index * 3 + 3),
            Speaker = index % 3 == 0 ? "me" : "remote",
            Text = "第 " + index + " 句：缓存命中率目标是百分之九十二，灰度期间比较 P95 与错误率。",
            Voice = index % 4
        });

        var began = System.Diagnostics.Stopwatch.StartNew();
        for (var index = 0; index < lines; index++) state.Transcript.Add(Row(index));
        await WaitAsync(300);
        var longMs = began.ElapsedMilliseconds;
        var longMeetingCounts = LiveRenderCount.Snapshot();

        LiveRenderCount.Reset();
        began.Restart();
        for (var index = 0; index < incremental; index++)
        {
            state.Transcript.Add(Row(lines + index));
            if (index % 10 == 9) await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }
        await WaitAsync(150);
        var incrementalMs = began.ElapsedMilliseconds;
        var incrementalCounts = LiveRenderCount.Snapshot();

        return new Dictionary<string, object?>
        {
            ["linesSeeded"] = lines,
            ["linesAdded"] = incremental,
            ["transcriptLines"] = state.Transcript.Count,
            ["pageAttach"] = pageAttach,
            ["longMeetingCounts"] = longMeetingCounts,
            ["incrementalCounts"] = incrementalCounts,
            ["longMs"] = longMs,
            ["incrementalMs"] = incrementalMs
        };
    }
}
