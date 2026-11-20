using System.Text;
using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/SessionArchivePersistenceTests.swift.</summary>
public sealed class SessionArchivePersistenceTestsParityTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("nt-archive-parity-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { /* best effort */ }
    }

    private string Temp(string name) => Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N"));

    private sealed class MissingReadException : Exception;
    private sealed class DeniedReadException : Exception;
    private sealed class InjectedWriteException : Exception;

    private static bool IsMissing(Exception exception) => exception is MissingReadException;

    private sealed record Metadata(int Version);

    // The Swift snapshot's computed members over the same counters the C# snapshot exposes.
    private static bool IsBlocked(SessionPersistenceSnapshot snapshot) => snapshot.LastFailure is not null;

    private static bool IsSaved(SessionPersistenceSnapshot snapshot) =>
        snapshot.LastFailure is null && snapshot.PendingEvents == 0
        && snapshot.SubmittedRevision == snapshot.PersistedRevision;

    private static bool HasUnacknowledgedDataLoss(SessionPersistenceSnapshot snapshot) =>
        snapshot.UnqueuedWriteCount > snapshot.AcknowledgedUnqueuedWriteCount;

    private static bool DataLossAcknowledged(SessionPersistenceSnapshot snapshot) =>
        snapshot.UnqueuedWriteCount > 0
        && snapshot.AcknowledgedUnqueuedWriteCount >= snapshot.UnqueuedWriteCount;

    [Fact]
    public void ArchiveRootReadSeparatesMissingEmptyAndUnreadable()
    {
        var root = Temp("archive");
        var empty = SessionArchiveDirectoryReader.Read(root, false, _ => [], _ => false);
        Assert.Empty(Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(empty).Items);

        var populated = new[] { Path.Combine(root, Guid.NewGuid().ToString()) };
        var listed = SessionArchiveDirectoryReader.Read(root, false, _ => populated, _ => false);
        Assert.Equal(populated, Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(listed).Items);

        var absent = SessionArchiveDirectoryReader.Read(root, false,
            _ => throw new MissingReadException(), IsMissing);
        Assert.Empty(Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(absent).Items);

        var denied = SessionArchiveDirectoryReader.Read(root, false,
            _ => throw new DeniedReadException(), IsMissing);
        var unavailable = Assert.IsType<SessionArchiveDirectoryReadResult.Unavailable>(denied);
        Assert.Equal(root, unavailable.Path);
        Assert.NotEmpty(unavailable.Reason);
    }

    [Fact]
    public void MissingArchiveRootIsEmptyOnlyOnFirstUse()
    {
        var root = Temp("known-archive");
        var firstUse = SessionArchiveDirectoryReader.Read(root, false,
            _ => throw new MissingReadException(), IsMissing);
        Assert.Empty(Assert.IsType<SessionArchiveDirectoryReadResult.Contents>(firstUse).Items);

        var knownSessions = SessionArchiveDirectoryReader.Read(root, true,
            _ => throw new MissingReadException(), IsMissing);
        var unavailable = Assert.IsType<SessionArchiveDirectoryReadResult.Unavailable>(knownSessions);
        Assert.Equal(root, unavailable.Path);
        Assert.Contains("缺失", unavailable.Reason);
    }

    [Fact]
    public void ArchiveRootLossRemainsKnownAfterRelaunch()
    {
        var parent = Temp("archive-history");
        var root = Path.Combine(parent, "Sessions");
        var marker = Path.Combine(parent, "sessions-archive-present.marker");
        Directory.CreateDirectory(Path.Combine(root, "known-session"));
        SessionArchiveContentMarker.Record(marker);

        // The local state is intentionally reconstructed as it would be after
        // process relaunch; only the marker survives outside the missing root.
        var knownAfterRelaunch = SessionArchiveContentMarker.WasPreviouslyPopulated(marker);
        Directory.Delete(root, true);
        var afterRelaunch = SessionArchiveDirectoryReader.Read(root, knownAfterRelaunch,
            _ => throw new MissingReadException(), IsMissing);
        var unavailable = Assert.IsType<SessionArchiveDirectoryReadResult.Unavailable>(afterRelaunch);
        Assert.Equal(root, unavailable.Path);
        Assert.Contains("缺失", unavailable.Reason);
    }

    [Fact]
    public void ArchiveMetadataReadSeparatesMissingUnreadableAndMalformed()
    {
        var url = Path.Combine(directory, "meta.json");
        var decode = (byte[] bytes) => JsonSerializer.Deserialize<Metadata>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var valid = SessionArchiveMetadataReader.Read(url, _ => "{\"version\":2}"u8.ToArray(), decode, IsMissing);
        Assert.Equal(2, Assert.IsType<SessionArchiveMetadataReadResult<Metadata>.Success>(valid).Value.Version);

        var missing = SessionArchiveMetadataReader.Read(url, _ => throw new MissingReadException(), decode, IsMissing);
        var missingIssue = Assert.IsType<SessionArchiveMetadataReadIssue.Missing>(
            Assert.IsType<SessionArchiveMetadataReadResult<Metadata>.Failure>(missing).Issue);
        Assert.Equal(url, missingIssue.Path);

        var denied = SessionArchiveMetadataReader.Read(url, _ => throw new DeniedReadException(), decode, IsMissing);
        var unreadable = Assert.IsType<SessionArchiveMetadataReadIssue.Unreadable>(
            Assert.IsType<SessionArchiveMetadataReadResult<Metadata>.Failure>(denied).Issue);
        Assert.Equal(url, unreadable.Path);
        Assert.NotEmpty(unreadable.Reason);

        var malformed = SessionArchiveMetadataReader.Read(url, _ => "not json"u8.ToArray(), decode, _ => false);
        var malformedIssue = Assert.IsType<SessionArchiveMetadataReadIssue.Malformed>(
            Assert.IsType<SessionArchiveMetadataReadResult<Metadata>.Failure>(malformed).Issue);
        Assert.Equal(url, malformedIssue.Path);
        Assert.NotEmpty(malformedIssue.Reason);
    }

    [Fact]
    public void LegacyAnswerOnlyRecordsBecomeLabeledEvents()
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(123);
        var events = LegacySessionAnswerMigration.Events(["first answer", "second answer"], date);
        Assert.Equal(2, events.Count);
        Assert.Equal(["first answer", "second answer"], events.Select(item => item.Answer));
        Assert.Equal(["旧版回答 1（问题未保存）", "旧版回答 2（问题未保存）"], events.Select(item => item.Question));
        Assert.All(events, item =>
        {
            Assert.Equal(date, item.Date);
            Assert.Equal("旧版记录", item.Model);
        });
    }

    [Fact]
    public void MigrationStagesAllFoldersBeforePublishingAndRollsBackPublishFailure()
    {
        var parent = Temp("archive-migration");
        var root = Path.Combine(parent, "Sessions");
        Directory.CreateDirectory(parent);

        var moveCount = 0;
        Assert.ThrowsAny<Exception>(() => SessionArchiveMigrationPublisher.Publish(["first", "second"], root,
            stage: (id, folder) =>
            {
                if (id == "second") throw new DeniedReadException();
                File.WriteAllBytes(Path.Combine(folder, "data"), Encoding.UTF8.GetBytes(id));
            },
            validate: (id, folder) =>
            {
                if (!File.ReadAllBytes(Path.Combine(folder, "data")).SequenceEqual(Encoding.UTF8.GetBytes(id))
                    || Directory.Exists(Path.Combine(root, id)))
                    throw new InvalidDataException("staged data is wrong");
            },
            move: (source, destination) =>
            {
                moveCount++;
                Directory.Move(source, destination);
            }));
        Assert.False(Directory.Exists(Path.Combine(root, "first")));
        Assert.Equal(0, moveCount);

        moveCount = 0;
        Assert.ThrowsAny<Exception>(() => SessionArchiveMigrationPublisher.Publish(["first", "second"], root,
            stage: (id, folder) => File.WriteAllBytes(Path.Combine(folder, "data"), Encoding.UTF8.GetBytes(id)),
            validate: (id, folder) =>
            {
                if (!File.ReadAllBytes(Path.Combine(folder, "data")).SequenceEqual(Encoding.UTF8.GetBytes(id)))
                    throw new InvalidDataException("staged data is wrong");
            },
            move: (source, destination) =>
            {
                moveCount++;
                if (moveCount == 2) throw new DeniedReadException();
                Directory.Move(source, destination);
            }));
        Assert.False(Directory.Exists(Path.Combine(root, "first")));
        Assert.False(Directory.Exists(Path.Combine(root, "second")));
    }

    [Fact]
    public void MigrationCommitFailureRestoresReplacedDestination()
    {
        var parent = Temp("archive-migration");
        var root = Path.Combine(parent, "Sessions");
        var destination = Path.Combine(root, "session");
        Directory.CreateDirectory(destination);
        var oldFile = Path.Combine(destination, "meta.json");
        File.WriteAllText(oldFile, "old");

        Assert.ThrowsAny<Exception>(() => SessionArchiveMigrationPublisher.Publish(["session"], root,
            stage: (_, folder) => File.WriteAllText(Path.Combine(folder, "meta.json"), "new"),
            validate: (_, folder) =>
            {
                if (File.ReadAllText(Path.Combine(folder, "meta.json")) != "new")
                    throw new InvalidDataException("staged meta is wrong");
            },
            canReplaceExisting: (_, _) => true,
            commit: () => throw new DeniedReadException()));
        Assert.Equal("old", File.ReadAllText(oldFile));
    }

    [Fact]
    public void MigrationRestoreFailurePreservesBackupAndRequiresRecovery()
    {
        var parent = Temp("archive-migration-restore");
        var root = Path.Combine(parent, "Sessions");
        var destination = Path.Combine(root, "session");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "meta.json"), "old");
        var backupsMarker = Path.DirectorySeparatorChar + "backups" + Path.DirectorySeparatorChar;

        SessionArchiveMigrationPublisher.RecoveryRequired? recovery = null;
        try
        {
            SessionArchiveMigrationPublisher.Publish(["session"], root,
                stage: (_, folder) => File.WriteAllText(Path.Combine(folder, "meta.json"), "new"),
                validate: (_, folder) =>
                {
                    if (File.ReadAllText(Path.Combine(folder, "meta.json")) != "new")
                        throw new InvalidDataException("staged meta is wrong");
                },
                canReplaceExisting: (_, _) => true,
                move: (source, target) =>
                {
                    if (source.Contains(backupsMarker, StringComparison.Ordinal)) throw new DeniedReadException();
                    Directory.Move(source, target);
                },
                commit: () => throw new DeniedReadException());
            Assert.Fail("the injected restore failure should require recovery");
        }
        catch (SessionArchiveMigrationPublisher.RecoveryRequired error)
        {
            recovery = error;
        }

        Assert.NotNull(recovery);
        var preservedBackup = Path.Combine(recovery!.StagingDirectory, "backups", "session", "meta.json");
        Assert.Equal("old", File.ReadAllText(preservedBackup));
        Assert.False(Directory.Exists(destination));
        var pendingRecovery = SessionArchiveMigrationPublisher.PendingRecoveryDirectory(root);
        Assert.Equal(Path.GetFullPath(recovery.StagingDirectory), Path.GetFullPath(pendingRecovery!));
    }

    [Fact]
    public void JsonLinesReportsMalformedTailAndKeepsValidPrefix()
    {
        var first = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(10), Speaker = "remote", Text = "\u7B2C\u4E00\u53E5" };
        var valid = JSONLines.Line(first);
        var torn = System.Text.Encoding.UTF8.GetBytes("{\"id\":\"broken");
        var report = JSONLines.DecodeReport<TranscriptLine>([.. valid, .. torn]);
        Assert.Equal([first], report.Records);
        Assert.Equal([2], report.MalformedLineNumbers);
        Assert.True(report.TruncatedFinalLine);

        var empty = JSONLines.DecodeReport<TranscriptLine>([]);
        Assert.Empty(empty.Records);
        Assert.Empty(empty.MalformedLineNumbers);
        Assert.False(empty.TruncatedFinalLine);
    }

    [Fact]
    public void JsonLinesRecoversRecordsAfterMalformedMiddleLineAndLegacyFinalLine()
    {
        var first = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(10), Speaker = "remote", Text = "\u7B2C\u4E00\u53E5" };
        var last = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(20), Speaker = "me", Text = "\u6700\u540E\u4E00\u53E5" };
        var firstLine = JSONLines.Line(first);
        var lastLine = JSONLines.Line(last).Take(..^1).ToArray();
        var bytes = firstLine.Concat(System.Text.Encoding.UTF8.GetBytes("{\"torn\n")).Concat(lastLine).ToArray();
        var report = JSONLines.DecodeReport<TranscriptLine>(bytes);
        Assert.Equal([first, last], report.Records);
        Assert.Equal([2], report.MalformedLineNumbers);
        Assert.False(report.TruncatedFinalLine);
    }

    [Fact]
    public void FailedWriteKeepsQueuedBytesForRetryAndReceipt()
    {
        var url = Path.Combine(Temp("queue"), "transcript.jsonl");
        var queue = new SessionArchiveWriteQueue(maximumPendingBytes: 100);
        var result = queue.Enqueue("transcript.jsonl", "line\n"u8.ToArray());
        Assert.True(result.Queued);
        var revision = result.Revision;

        var failing = new SessionArchiveFileAppender((_, _, _) => throw new InjectedWriteException());
        try
        {
            failing.Append(queue.Entries[0].Data, url);
            Assert.Fail("expected injected write failure");
        }
        catch (InjectedWriteException)
        {
            queue.Fail("disk full");
        }
        Assert.True(IsBlocked(queue.Snapshot));
        Assert.Equal(1, queue.Snapshot.PendingEvents);
        Assert.Equal(0, queue.Snapshot.PersistedRevision);

        SessionArchiveFileAppender.Live().Append(queue.Entries[0].Data, url);
        queue.Acknowledge(revision, DateTimeOffset.FromUnixTimeSeconds(100));
        Assert.Equal("line\n"u8.ToArray(), File.ReadAllBytes(url));
        Assert.True(IsSaved(queue.Snapshot));
        Assert.Equal(0, queue.Snapshot.PendingBytes);
        Assert.Equal(revision, queue.Snapshot.PersistedRevision);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(100), queue.Snapshot.LastPersistedAt);
    }

    [Fact]
    public void MidWriteFailureRollsBackToAcknowledgedPrefixAndRetrySavesOnce()
    {
        var url = Path.Combine(Temp("midwrite"), "transcript.jsonl");
        var earlier = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(10), Speaker = "remote", Text = "\u5DF2\u4FDD\u5B58\u8BB0\u5F55" };
        var pending = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(20), Speaker = "me", Text = "\u5F85\u91CD\u8BD5\u8BB0\u5F55" };
        var earlierLine = JSONLines.Line(earlier);
        var pendingLine = JSONLines.Line(pending);
        var queue = new SessionArchiveWriteQueue();
        var earlierResult = queue.Enqueue("transcript.jsonl", earlierLine);
        Assert.True(earlierResult.Queued);
        SessionArchiveFileAppender.Live().Append(queue.Entries[0].Data, url);
        queue.Acknowledge(earlierResult.Revision);

        var pendingResult = queue.Enqueue("transcript.jsonl", pendingLine);
        Assert.True(pendingResult.Queued);
        var partialWriter = SessionArchiveFileAppender.UsingFileHandleAppend((data, stream) =>
        {
            stream.Write(data, 0, Math.Max(1, data.Length / 2));
            throw new InjectedWriteException();
        });
        Assert.Throws<InjectedWriteException>(() => partialWriter.Append(queue.Entries[0].Data, url));
        queue.Fail("mid-write failure");

        Assert.Equal(earlierLine, File.ReadAllBytes(url));
        var afterFailure = JSONLines.DecodeReport<TranscriptLine>(File.ReadAllBytes(url));
        Assert.Equal([earlier], afterFailure.Records);
        Assert.Empty(afterFailure.MalformedLineNumbers);
        Assert.Equal(earlierResult.Revision, queue.Snapshot.PersistedRevision);
        Assert.Equal(1, queue.Snapshot.PendingEvents);
        Assert.True(IsBlocked(queue.Snapshot));

        SessionArchiveFileAppender.Live().Append(queue.Entries[0].Data, url);
        queue.Acknowledge(pendingResult.Revision);
        var savedBytes = File.ReadAllBytes(url);
        var report = JSONLines.DecodeReport<TranscriptLine>(savedBytes);
        Assert.Equal([.. earlierLine, .. pendingLine], savedBytes);
        Assert.Equal([earlier, pending], report.Records);
        Assert.Empty(report.MalformedLineNumbers);
        Assert.True(IsSaved(queue.Snapshot));
        Assert.Equal(0, queue.Snapshot.PendingEvents);
        Assert.Equal(pendingResult.Revision, queue.Snapshot.PersistedRevision);
    }

    [Fact]
    public void FailedReceiptRetainsEditedNotesAndVoiceEventsForRetry()
    {
        var target = Temp("queue");
        var notesUrl = Path.Combine(target, "notes.json");
        var voicesUrl = Path.Combine(target, "voices.jsonl");

        var correction = new TranscriptCorrection { Line = 0, Original = "缓存击穿", Corrected = "缓存击穿" };
        var notes = new StructuredNotesDoc("general", "通用", "zh", "会议", [], Corrections: [correction]);
        var lineId = Guid.NewGuid();
        var queue = new SessionArchiveWriteQueue();
        Assert.True(queue.Enqueue("notes.json", JSONLines.Line(notes), SessionArchiveWriteMode.Replace).Queued);
        Assert.True(queue.Enqueue("voices.jsonl", JSONLines.Line(new SessionVoiceTag(lineId, 1))).Queued);

        var failing = new SessionArchiveFileAppender((_, _, _) => throw new InjectedWriteException());
        try
        {
            failing.Append(queue.Entries[^1].Data, voicesUrl);
            Assert.Fail("expected injected write failure");
        }
        catch (InjectedWriteException)
        {
            queue.Fail("disk full");
        }
        Assert.True(IsBlocked(queue.Snapshot));

        // A further edit starts from the pending notes snapshot and queues the
        // merged voice value instead of losing either change when the disk recovers.
        notes = JSONLines.Decode<StructuredNotesDoc>(queue.Entries[0].Data).Single() with
        {
            Corrections = [JSONLines.Decode<StructuredNotesDoc>(queue.Entries[0].Data).Single().Corrections![0]
                with { Reverted = true }]
        };
        var last = queue.Enqueue("notes.json", JSONLines.Line(notes), SessionArchiveWriteMode.Replace);
        var voice = queue.Enqueue("voices.jsonl", JSONLines.Line(new SessionVoiceTag(lineId, 2)));
        Assert.True(last.Queued);
        Assert.True(voice.Queued);

        foreach (var entry in queue.Entries)
            SessionArchiveFileAppender.Live().Write(entry.Data, Path.Combine(target, entry.FileName), entry.Mode);
        queue.Acknowledge(Math.Max(last.Revision, voice.Revision));

        var savedNotes = JSONLines.Decode<StructuredNotesDoc>(File.ReadAllBytes(notesUrl)).Single();
        var savedVoices = JSONLines.Decode<SessionVoiceTag>(File.ReadAllBytes(voicesUrl));
        Assert.True(savedNotes.Corrections![0].Reverted);
        Assert.Equal(new SessionVoiceTag(lineId, 2), savedVoices[^1]);
        Assert.True(IsSaved(queue.Snapshot));
    }

    [Fact]
    public void QueueCapacityIsBoundedAndFailureIsNotClearedByEarlierReceipts()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingBytes: 4);
        var first = queue.Enqueue("a", [1, 2, 3]);
        Assert.True(first.Queued);
        Assert.False(queue.Enqueue("b", [4, 5]).Queued);
        queue.Acknowledge(first.Revision);
        Assert.True(IsBlocked(queue.Snapshot));
        Assert.Contains("队列已满", queue.Snapshot.LastFailure!);
        Assert.False(queue.Enqueue("c", [6]).Queued);

        var eventLimited = new SessionArchiveWriteQueue(maximumPendingBytes: 100, maximumPendingEvents: 2);
        Assert.True(eventLimited.Enqueue("a", [1]).Queued);
        Assert.True(eventLimited.Enqueue("b", [2]).Queued);
        Assert.False(eventLimited.Enqueue("c", [3]).Queued);
        Assert.Equal(2, eventLimited.Snapshot.PendingEvents);
    }

    [Fact]
    public void OverflowRecoveryKeepsLossVisibleAndReopensFutureWritesOnlyAfterAcknowledgement()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingBytes: 4);
        var accepted = queue.Enqueue("transcript.jsonl", [1, 2, 3]);
        Assert.True(accepted.Queued);
        Assert.False(queue.Enqueue("transcript.jsonl", [4, 5]).Queued);
        Assert.False(queue.Enqueue("transcript.jsonl", [6]).Queued);
        Assert.Equal(1, queue.Snapshot.PendingEvents);
        Assert.Equal(2, queue.Snapshot.UnqueuedWriteCount);
        Assert.False(queue.Snapshot.CanAcknowledgeDataLoss);
        Assert.False(queue.AcknowledgeDataLossAndResume());

        // The only accepted entry must be acknowledged before the missing writes can
        // be acknowledged. Clearing the queue failure never claims the rejected data saved.
        queue.Acknowledge(accepted.Revision);
        Assert.Equal(0, queue.Snapshot.PendingEvents);
        Assert.True(IsBlocked(queue.Snapshot));
        Assert.True(HasUnacknowledgedDataLoss(queue.Snapshot));
        Assert.True(queue.Snapshot.CanAcknowledgeDataLoss);
        Assert.False(DataLossAcknowledged(queue.Snapshot));

        Assert.True(queue.AcknowledgeDataLossAndResume());
        Assert.True(IsSaved(queue.Snapshot));
        Assert.Equal(2, queue.Snapshot.UnqueuedWriteCount);
        Assert.True(DataLossAcknowledged(queue.Snapshot));

        var future = queue.Enqueue("transcript.jsonl", [7]);
        Assert.True(future.Queued);
        Assert.Equal(accepted.Revision + 1, future.Revision);
        queue.Acknowledge(future.Revision);
        Assert.True(IsSaved(queue.Snapshot));
        Assert.Equal(2, queue.Snapshot.UnqueuedWriteCount);
        Assert.True(DataLossAcknowledged(queue.Snapshot));
    }

    [Fact]
    public void ReceiptOnlyAcknowledgesACompletedPrefixAndDoesNotHidePermanentLoss()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingBytes: 20);
        var firstResult = queue.Enqueue("a", [1]);
        var secondResult = queue.Enqueue("b", [2]);
        Assert.True(firstResult.Queued);
        Assert.True(secondResult.Queued);
        queue.Fail("disk full");
        queue.Acknowledge(secondResult.Revision + 1);
        Assert.Equal(0, queue.Snapshot.PersistedRevision);
        Assert.Equal(2, queue.Snapshot.PendingEvents);
        queue.Acknowledge(firstResult.Revision);
        Assert.Equal(firstResult.Revision, queue.Snapshot.PersistedRevision);
        Assert.Equal(1, queue.Snapshot.PendingEvents);
        Assert.True(IsBlocked(queue.Snapshot));
        queue.Acknowledge(secondResult.Revision);
        Assert.True(IsSaved(queue.Snapshot));

        queue.Fail("encoding failed", permanent: true);
        var third = queue.Enqueue("c", [3]);
        Assert.True(third.Queued);
        queue.Acknowledge(third.Revision);
        queue.Fail("another transient error");
        Assert.True(IsBlocked(queue.Snapshot));
        Assert.Equal("encoding failed", queue.Snapshot.LastFailure);
    }

    [Fact]
    public void TranslationRefreshWaitsForRetryReceiptAndIsSessionScoped()
    {
        var queue = new SessionArchiveWriteQueue();
        var first = queue.Enqueue("translations.jsonl", "one\n"u8.ToArray());
        var second = queue.Enqueue("translations.jsonl", "two\n"u8.ToArray());
        Assert.True(first.Queued);
        Assert.True(second.Queued);
        var firstSession = new SessionTranslationWriteReceipts();
        firstSession.RecordSubmittedRevision(first.Revision);
        firstSession.RecordSubmittedRevision(second.Revision);
        var otherSession = new SessionTranslationWriteReceipts();
        otherSession.RecordSubmittedRevision(second.Revision);

        queue.Fail("disk full");
        Assert.False(firstSession.Acknowledge(queue.Snapshot.PersistedRevision));
        Assert.False(otherSession.Acknowledge(queue.Snapshot.PersistedRevision));

        // The first receipt persists a prefix; one refresh covers that whole successful batch.
        queue.Acknowledge(first.Revision);
        Assert.True(firstSession.Acknowledge(queue.Snapshot.PersistedRevision));
        Assert.False(firstSession.Acknowledge(queue.Snapshot.PersistedRevision));
        Assert.True(otherSession.HasPending);

        // A retry receipt for the remaining write refreshes only its owning session.
        queue.Acknowledge(second.Revision);
        Assert.True(otherSession.Acknowledge(queue.Snapshot.PersistedRevision));
        Assert.False(otherSession.Acknowledge(queue.Snapshot.PersistedRevision));
    }

    [Fact]
    public void LiveAppenderSeparatesAnOlderFileWithoutTrailingNewline()
    {
        var url = Path.Combine(Temp("appender"), "transcript.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(url)!);
        var first = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(10), Speaker = "remote", Text = "旧会议" };
        var second = new TranscriptLine { Date = DateTimeOffset.FromUnixTimeSeconds(20), Speaker = "me", Text = "新发言" };
        var firstLine = JSONLines.Line(first).ToList();
        firstLine.RemoveAt(firstLine.Count - 1);
        File.WriteAllBytes(url, firstLine.ToArray());

        SessionArchiveFileAppender.Live().Append(JSONLines.Line(second), url);

        var report = JSONLines.Decode<TranscriptLine>(File.ReadAllBytes(url));
        Assert.Equal(new[] { first, second }, report);
    }

    [Fact]
    public void LiveAppenderReplacesSnapshotFilesInsteadOfAppendingJson()
    {
        var url = Path.Combine(Temp("appender"), "meta.json");
        var appender = SessionArchiveFileAppender.Live();
        appender.Write("{\"version\":1}"u8.ToArray(), url, SessionArchiveWriteMode.Replace);
        appender.Write("{\"version\":2}"u8.ToArray(), url, SessionArchiveWriteMode.Replace);

        Assert.Equal("{\"version\":2}"u8.ToArray(), File.ReadAllBytes(url));
    }

    [Fact]
    public void AFolderWithOnlyDerivedFilesIsALeftover()
    {
        Assert.True(SessionArchiveLeftover.IsLeftover(["notes.json"]));
        Assert.True(SessionArchiveLeftover.IsLeftover(["notes.json", "summary.json", ".DS_Store"]));
        Assert.True(SessionArchiveLeftover.IsLeftover([]));
    }

    [Fact]
    public void AnythingRecordedMakesItAMeetingToRepair()
    {
        Assert.False(SessionArchiveLeftover.IsLeftover(["notes.json", "transcript.jsonl"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["context.json"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["answers.jsonl", "notes.json"]));
        Assert.False(SessionArchiveLeftover.IsLeftover(["meta.json", "notes.json"]));
    }
}
