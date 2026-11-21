using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

public enum LiveNotesStatus
{
    Off,
    Waiting,
    Updating,
    Failed,
    NoEngine
}

/// <summary>J1/Mac LiveNotesColumn: one talking point of a topic, with "what the last update added".</summary>
public sealed record LiveNotesPointRow(string Text, bool Fresh);

/// <summary>J1: one topic of the live notes.</summary>
public sealed record LiveNotesTopicRow(string Title, IReadOnlyList<LiveNotesPointRow> Points);

/// <summary>J1: one decision of the live notes.</summary>
public sealed record LiveNotesDecisionRow(string Text, bool Fresh);

/// <summary>J1: one to-do of the live notes, with its owner (Mac: 「…（我）」).</summary>
public sealed record LiveNotesTodoRow(string Text, string OwnerText, bool Fresh)
{
    /// <summary>Mac LiveNotesColumn: the owner is written after the text in full-width brackets.</summary>
    public string DisplayText => OwnerText.Length > 0 ? Text + "（" + OwnerText + "）" : Text;
}

/// <summary>
/// J1: the notes kept while the meeting runs (LiveNotesEngine.swift). Every couple of seconds the
/// engine asks <see cref="LiveNotesSchedule"/> whether an update is due; when it is, one request
/// carries the notes so far and the lines said since, and the whole updated notes come back as JSON
/// (<see cref="LiveNotesPrompt"/>). Earlier topics are settled and never sent back for rewriting, a
/// reply that cannot be read leaves the notes on show untouched, and what the last update added is
/// marked with the scene's colour. The full notes are still written from the whole transcript when
/// the meeting ends (that pass is the meeting-end summary, not this engine).
/// </summary>
public partial class AppState
{
    private LiveNotes notes = new();
    private readonly HashSet<string> liveNotesFresh = new(StringComparer.Ordinal);
    private LiveNotesStatus liveNotesEngineStatus = LiveNotesStatus.Off;
    private DateTimeOffset? liveNotesUpdatedAt;
    private DateTimeOffset liveNotesLastAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset liveNotesLastCheck = DateTimeOffset.MinValue;
    private Guid liveNotesMeeting = Guid.NewGuid();
    private bool liveNotesInFlight;
    private bool liveNotesIsEnabled;
    private DispatcherTimer? liveNotesTimer;
    private bool liveNotesDemoInstalled;

    /// <summary>Mac LiveNotesEngine.status.</summary>
    public LiveNotesStatus LiveNotesEngineStatus => liveNotesEngineStatus;

    /// <summary>Mac isEnabled; also the more-menu 实时纪要 checkbox (J2 keeps it per series).</summary>
    public bool LiveNotesEnabled
    {
        get => liveNotesIsEnabled;
        set => SetLiveNotesEnabled(value);
    }

    public bool LiveNotesHasContent => !notes.IsEmpty;

    public string LiveNotesSummaryText => notes.Summary;

    public IReadOnlyList<LiveNotesTopicRow> LiveNotesTopicRows => notes.Topics
        .Select(topic => new LiveNotesTopicRow(topic.Title,
            topic.Points.Select(point => new LiveNotesPointRow(point, liveNotesFresh.Contains(point))).ToArray()))
        .ToArray();

    public IReadOnlyList<LiveNotesDecisionRow> LiveNotesDecisionRows => notes.Decisions
        .Select(decision => new LiveNotesDecisionRow(decision, liveNotesFresh.Contains(decision)))
        .ToArray();

    public IReadOnlyList<LiveNotesTodoRow> LiveNotesTodoRows => notes.Todos
        .Select(todo => new LiveNotesTodoRow(todo.Text, todo.Owner ?? "",
            liveNotesFresh.Contains(todo.Text)))
        .ToArray();

    /// <summary>Mac LiveNotesColumn.statusText.</summary>
    public string LiveNotesStatusText => liveNotesEngineStatus switch
    {
        LiveNotesStatus.Off => UiText.T("实时纪要没有开"),
        LiveNotesStatus.Updating => UiText.T("正在更新…"),
        LiveNotesStatus.Failed => UiText.T("这次没更新成功，稍后会再试"),
        LiveNotesStatus.NoEngine => UiText.T("还没有连接回答服务，写不了实时纪要"),
        _ => liveNotesUpdatedAt is { } at
            ? UiText.Format("更新于 %@ · 约每 45 秒一次", at.ToLocalTime().ToString("h:mm"))
            : UiText.T("有了内容就开始整理")
    };

    public bool LiveNotesStatusIsWarning =>
        liveNotesEngineStatus is LiveNotesStatus.Failed or LiveNotesStatus.NoEngine;

    /// <summary>Mac: 现在更新 while live notes are on, 开启实时纪要 while they are off.</summary>
    public string LiveNotesButtonText => liveNotesIsEnabled ? UiText.T("现在更新") : UiText.T("开启实时纪要");

    public bool LiveNotesCanUpdate => !liveNotesIsEnabled ||
        (liveNotesEngineStatus != LiveNotesStatus.Updating && AnswerEngineReady);

    public bool LiveNotesIsUpdating => liveNotesEngineStatus == LiveNotesStatus.Updating;

    /// <summary>Mac's empty state, in its on and off wording.</summary>
    public string LiveNotesHintText => liveNotesIsEnabled
        ? UiText.T("说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。")
        : UiText.T("这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。");

    /// <summary>Mac 定下来的 / 要做的 overlines; empty lists hide their sections.</summary>
    public bool HasLiveNotesDecisions => notes.Decisions.Count > 0;
    public bool HasLiveNotesTodos => notes.Todos.Count > 0;

    // ---- lifecycle (Mac begin / setEnabled / end) --------------------------------------

    /// <summary>Mac begin(enabled:): a meeting starts; the timer looks for due updates.</summary>
    public void BeginLiveNotes(bool enabled)
    {
        liveNotesMeeting = Guid.NewGuid();
        notes = new LiveNotes();
        liveNotesFresh.Clear();
        liveNotesUpdatedAt = null;
        liveNotesLastAttempt = DateTimeOffset.UtcNow;
        liveNotesLastCheck = DateTimeOffset.MinValue;
        liveNotesInFlight = false;
        liveNotesIsEnabled = enabled;
        liveNotesEngineStatus = enabled ? LiveNotesStatus.Waiting : LiveNotesStatus.Off;
        liveNotesDemoInstalled = false;
        StartLiveNotesTimer();
        RaiseLiveNotesUi();
    }

    /// <summary>Mac setEnabled(_:): turned on or off during the meeting.</summary>
    public void SetLiveNotesEnabled(bool enabled)
    {
        if (enabled == liveNotesIsEnabled)
        {
            RaiseLiveNotesUi();
            return;
        }
        liveNotesIsEnabled = enabled;
        liveNotesEngineStatus = enabled ? LiveNotesStatus.Waiting : LiveNotesStatus.Off;
        if (enabled)
        {
            liveNotesLastAttempt = DateTimeOffset.MinValue;
            StartLiveNotesTimer();
        }
        RaiseLiveNotesUi();
        SetStatus(enabled
            ? UiText.T("已开启实时纪要：大约每 45 秒整理一次。")
            : UiText.T("已关闭实时纪要。"));
    }

    /// <summary>Mac end(): the meeting is over; the notes stay readable.</summary>
    public void EndLiveNotes()
    {
        liveNotesMeeting = Guid.NewGuid();
        liveNotesIsEnabled = false;
        if (liveNotesEngineStatus != LiveNotesStatus.Off)
            liveNotesEngineStatus = notes.IsEmpty ? LiveNotesStatus.Off : LiveNotesStatus.Waiting;
        liveNotesInFlight = false;
        liveNotesTimer?.Stop();
        RaiseLiveNotesUi();
    }

    private void StartLiveNotesTimer()
    {
        if (liveNotesTimer is null)
        {
            liveNotesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            liveNotesTimer.Tick += async (_, _) => await LiveNotesTickAsync();
        }
        liveNotesTimer.Start();
    }

    /// <summary>Mac tick(): every 2 s, update when the schedule says so.</summary>
    private async Task LiveNotesTickAsync()
    {
        if (!liveNotesIsEnabled || !Recording || ListeningPaused || liveNotesInFlight) return;
        var now = DateTimeOffset.UtcNow;
        if ((now - liveNotesLastCheck).TotalSeconds < 2) return;
        liveNotesLastCheck = now;
        if (!AnswerEngineReady)
        {
            liveNotesEngineStatus = LiveNotesStatus.NoEngine;
            RaiseLiveNotesUi();
            return;
        }
        if (liveNotesEngineStatus == LiveNotesStatus.NoEngine) liveNotesEngineStatus = LiveNotesStatus.Waiting;
        var lines = currentLines.Count(line => line.SuspectedEcho != true);
        if (!LiveNotesSchedule.IsDue(notes, lines, (now - liveNotesLastAttempt).TotalSeconds))
        {
            RaiseLiveNotesUi();
            return;
        }
        await UpdateLiveNotesAsync();
    }

    /// <summary>Mac 现在更新: not waiting for the interval.</summary>
    public async Task UpdateLiveNotesNowAsync()
    {
        if (!liveNotesIsEnabled || liveNotesInFlight || !AnswerEngineReady) return;
        await UpdateLiveNotesAsync();
    }

    /// <summary>Mac update(_:): one request returns the whole updated notes; a bad reply keeps them.</summary>
    private async Task UpdateLiveNotesAsync()
    {
        var entries = currentLines.Where(line => line.SuspectedEcho != true).ToList();
        if (entries.Count <= notes.Covered) return;
        var began = recordingStartedAt != default ? recordingStartedAt : entries[0].Date;
        var promptLines = entries.Select(line => new LiveNotesPrompt.Line(
            Math.Max(0, (int)(line.Date - began).TotalSeconds),
            LiveNotesSpeakerLabel(line),
            line.Text)).ToList();
        var prompt = LiveNotesPrompt.User(notes, promptLines, LiveNotesLanguage);
        if (Settings.Model.Length == 0)
        {
            liveNotesEngineStatus = LiveNotesStatus.NoEngine;
            RaiseLiveNotesUi();
            return;
        }
        var meeting = liveNotesMeeting;
        var previous = notes;
        var covered = promptLines.Count;
        liveNotesInFlight = true;
        liveNotesLastAttempt = DateTimeOffset.UtcNow;
        liveNotesEngineStatus = LiveNotesStatus.Updating;
        RaiseLiveNotesUi();
        try
        {
            var options = AnswerOptions(notes: true);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var reply = await CurrentClient().AnswerAsync(options, LiveNotesPrompt.System, prompt,
                _ => { }, cancellation.Token);
            if (meeting != liveNotesMeeting) return;
            var next = LiveNotesPrompt.Parse(reply, previous, covered);
            if (next is null)
            {
                liveNotesEngineStatus = LiveNotesStatus.Failed;
                RaiseLiveNotesUi();
                return;
            }
            liveNotesFresh.Clear();
            if (!previous.IsEmpty)
            {
                foreach (var statement in next.Statements)
                    if (!previous.Statements.Contains(statement)) liveNotesFresh.Add(statement);
            }
            notes = next;
            liveNotesUpdatedAt = DateTimeOffset.Now;
            liveNotesEngineStatus = LiveNotesStatus.Waiting;
            RaiseLiveNotesUi();
        }
        catch (OperationCanceledException)
        {
            if (meeting == liveNotesMeeting) { liveNotesEngineStatus = LiveNotesStatus.Failed; RaiseLiveNotesUi(); }
        }
        catch (Exception)
        {
            if (meeting == liveNotesMeeting) { liveNotesEngineStatus = LiveNotesStatus.Failed; RaiseLiveNotesUi(); }
        }
        finally
        {
            liveNotesInFlight = false;
            RaiseLiveNotesUi();
        }
    }

    /// <summary>Mac liveNotesLanguage: notes language setting, else the app's own.</summary>
    private string LiveNotesLanguage => UiText.EnglishUi ? "English" : "简体中文";

    /// <summary>Mac LiveNotesPrompt line speaker: 我 / 现场 / 对方, or the recognised name.</summary>
    private string LiveNotesSpeakerLabel(TranscriptLine line)
    {
        var row = Transcript.FirstOrDefault(item => item.Id == line.Id);
        if (row is null) return line.Speaker == "me" ? UiText.T("我") : UiText.T("对方");
        if (row.SpeakerIsMe) return UiText.T("我");
        if (line.Speaker == "room") return UiText.T("现场");
        return row.SpeakerName.Length > 0 ? row.SpeakerName : UiText.T("对方");
    }

    /// <summary>Mac installDemo: notes for screenshots and walkthroughs.</summary>
    public void InstallDemoLiveNotes()
    {
        var demo = new LiveNotes
        {
            Summary = "面试进行到系统设计：先聊了订单缓存层的项目，现在在追问数据库和缓存的一致性。",
            Topics =
            [
                new LiveNotes.Topic("项目经历", ["负责订单服务的缓存层", "核心查询从数据库迁到 Redis"]),
                new LiveNotes.Topic("缓存一致性", ["先更新数据库，再删除缓存", "删除失败进重试队列", "面试官追问：删除缓存失败怎么办"])
            ],
            Decisions = ["下一步讲热点 key 的处理"],
            Todos = [new LiveNotes.Todo("会后补一张读写时序图", "我")],
            Covered = 6
        };
        notes = demo;
        liveNotesFresh.Clear();
        foreach (var statement in new[] { "面试官追问：删除缓存失败怎么办", "会后补一张读写时序图" })
            liveNotesFresh.Add(statement);
        liveNotesIsEnabled = true;
        liveNotesEngineStatus = LiveNotesStatus.Waiting;
        liveNotesUpdatedAt = DateTimeOffset.Now;
        liveNotesDemoInstalled = true;
        RaiseLiveNotesUi();
    }

    /// <summary>Demo mode shows the Mac demo notes so the tab can be compared with live-notes-*.png.</summary>
    public void EnsureDemoLiveNotes()
    {
        if (liveNotesDemoInstalled || !StartupOptions.Demo) return;
        InstallDemoLiveNotes();
    }

    /// <summary>
    /// The status/empty-state surfaces only. Called from the LiveView property-changed handler for
    /// LiveNotesEnabled and by the page's tab logic; it must never raise LiveNotesEngineStatus or the
    /// full UI list, or the handler would loop back into itself (stack overflow).
    /// </summary>
    public void RaiseLiveNotesTabUi()
    {
        OnPropertyChanged(nameof(LiveNotesStatusText));
        OnPropertyChanged(nameof(LiveNotesStatusIsWarning));
        OnPropertyChanged(nameof(LiveNotesHintText));
        OnPropertyChanged(nameof(LiveNotesButtonText));
        OnPropertyChanged(nameof(LiveNotesCanUpdate));
        OnPropertyChanged(nameof(LiveNotesIsUpdating));
    }

    /// <summary>Full live-notes UI refresh (content + status). Excludes LiveNotesEngineStatus: it is
    /// read through LiveNotesStatusText, and raising it would re-enter the page's handler.</summary>
    public void RaiseLiveNotesUi()
    {
        foreach (var name in new[]
                 {
                     nameof(LiveNotesEnabled), nameof(LiveNotesHasContent),
                     nameof(LiveNotesSummaryText), nameof(LiveNotesTopicRows), nameof(LiveNotesDecisionRows),
                     nameof(LiveNotesTodoRows), nameof(LiveNotesStatusText), nameof(LiveNotesStatusIsWarning),
                     nameof(LiveNotesButtonText), nameof(LiveNotesCanUpdate), nameof(LiveNotesIsUpdating),
                     nameof(LiveNotesHintText), nameof(HasLiveNotesDecisions), nameof(HasLiveNotesTodos)
                 })
            OnPropertyChanged(name);
    }
}
