using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §3.3 live 9 (Mac archiveSaveStatus): archive writes during a meeting go through the bounded
/// Core queue and are flushed one line at a time every 500 ms. The queue's snapshot drives the
/// live top-bar status: 保存中 / 保存受阻 · N 项未保存 · 重试 / 确认缺失。
/// </summary>
public partial class AppState
{
    public enum SaveStatusKind { Idle, Saving, Blocked }

    private readonly SessionArchiveWriteQueue archiveWriteQueue = new();
    private readonly Dictionary<int, (Guid Session, TaskCompletionSource Completion, string? CountKind)> archiveWriteWaiters = [];
    private readonly object archiveWriteLock = new();
    private DispatcherTimer? archiveWriteTimer;
    private bool archiveFlushing;

    public SaveStatusKind SaveStatus => archiveWriteQueue.Snapshot switch
    {
        { LastFailure: { Length: > 0 } } => SaveStatusKind.Blocked,
        { PendingEvents: > 0 } or { UnqueuedWriteCount: > 0 } => SaveStatusKind.Saving,
        _ => SaveStatusKind.Idle
    };

    public string SaveStatusText => SaveStatus switch
    {
        SaveStatusKind.Blocked => UiText.Format("保存受阻 · 有 %lld 项未保存 · 重试", (long)Math.Max(1, SaveUnqueuedCount)),
        SaveStatusKind.Saving => UiText.T("保存中"),
        _ => ""
    };

    public int SaveUnqueuedCount => archiveWriteQueue.Snapshot switch
    {
        var snapshot when snapshot.UnqueuedWriteCount > 0 => snapshot.UnqueuedWriteCount,
        var snapshot => snapshot.PendingEvents
    };

    public bool HasSaveStatus => SaveStatus != SaveStatusKind.Idle;
    public bool SaveIsBlocked => SaveStatus == SaveStatusKind.Blocked;
    public bool CanAcknowledgeSaveLoss => archiveWriteQueue.CanAcknowledgeDataLoss;

    public void RaiseSaveStatusUi()
    {
        foreach (var name in new[]
                 {
                     nameof(SaveStatus), nameof(SaveStatusText), nameof(SaveUnqueuedCount),
                     nameof(HasSaveStatus), nameof(SaveIsBlocked), nameof(CanAcknowledgeSaveLoss)
                 })
            OnPropertyChanged(name);
    }

    private void EnsureArchiveWriteTimer()
    {
        if (archiveWriteTimer is not null) return;
        archiveWriteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        archiveWriteTimer.Tick += (_, _) => _ = FlushArchiveQueueAsync();
        archiveWriteTimer.Start();
    }

    /// <summary>Live path: queue one JSONL line; the returned task completes when it is persisted.</summary>
    private Task EnqueueArchiveWrite(Guid session, string fileName, object item, string? countKind)
    {
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item, JsonDefaults.ArchiveOptions) + "\n");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = archiveWriteQueue.Enqueue(fileName, data);
        lock (archiveWriteLock)
        {
            if (result.Queued) archiveWriteWaiters[result.Revision] = (session, completion, countKind);
            else completion.SetException(new IOException("待保存队列已满，会议内容未能入队。"));
        }
        if (!result.Queued) archiveWriteQueue.Fail("待保存队列已满；部分新写入没有入队。");
        EnsureArchiveWriteTimer();
        RaiseSaveStatusUi();
        if (result.Queued) _ = FlushArchiveQueueAsync();
        return completion.Task;
    }

    private Task EnqueueTranscriptLine(SessionMeta meta, TranscriptLine line) =>
        EnqueueArchiveWrite(meta.Id, "transcript.jsonl", line, "transcript");

    private Task EnqueueQuestion(SessionMeta meta, SessionQuestionEvent question) =>
        EnqueueArchiveWrite(meta.Id, "questions.jsonl", question, "questions");

    private Task EnqueueAnswer(SessionMeta meta, AnswerRecord answer) =>
        EnqueueArchiveWrite(meta.Id, "answers.jsonl", answer, "answers");

    private Task EnqueueVoiceTag(Guid session, Guid line, int voice) =>
        EnqueueArchiveWrite(session, "voices.jsonl", new SessionVoiceTag(line, voice), null);

    private Task EnqueueTranslation(Guid session, SessionTranslation translation) =>
        EnqueueArchiveWrite(session, "translations.jsonl",
            translation with { InputRevision = translation.InputRevision ?? translation.SourceHash }, null);

    /// <summary>The 500 ms tick plus the Stop/End/export flush points.</summary>
    public async Task FlushArchiveQueueAsync()
    {
        if (archiveFlushing) return;
        archiveFlushing = true;
        try
        {
            while (true)
            {
                SessionArchiveWriteQueue.Entry? entry;
                (Guid Session, TaskCompletionSource Completion, string? CountKind) waiter;
                lock (archiveWriteLock)
                {
                    entry = archiveWriteQueue.Entries.FirstOrDefault();
                    if (entry is null) break;
                    if (!archiveWriteWaiters.Remove(entry.Revision, out waiter))
                        waiter = (Guid.Empty, new TaskCompletionSource(), null);
                }
                if (waiter.Session == Guid.Empty)
                {
                    archiveWriteQueue.Acknowledge(entry.Revision);
                    continue;
                }
                try
                {
                    await Sessions.AppendRawAsync(waiter.Session, entry.FileName, entry.Data).ConfigureAwait(false);
                    if (waiter.CountKind is { Length: > 0 } kind)
                        await Sessions.IncrementMetaCountAsync(waiter.Session, kind).ConfigureAwait(false);
                    archiveWriteQueue.Acknowledge(entry.Revision);
                    waiter.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    archiveWriteQueue.Fail(ex.Message);
                    waiter.Completion.TrySetException(ex);
                    break;
                }
            }
        }
        finally
        {
            archiveFlushing = false;
            await Dispatcher.UIThread.InvokeAsync(RaiseSaveStatusUi);
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task RetryPersistence() => await FlushArchiveQueueAsync();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void AcknowledgeSaveLoss()
    {
        if (archiveWriteQueue.AcknowledgeDataLossAndResume()) RaiseSaveStatusUi();
    }
}
