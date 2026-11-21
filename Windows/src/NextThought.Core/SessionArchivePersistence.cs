using System.Text;

namespace NextThought.Core;

public enum SessionArchiveWriteMode
{
    Append,
    Replace
}

/// <summary>Distinguishes an absent archive root from a directory that exists but cannot be listed.</summary>
public abstract record SessionArchiveDirectoryReadResult
{
    public sealed record Contents(IReadOnlyList<string> Items) : SessionArchiveDirectoryReadResult;
    public sealed record Unavailable(string Path, string Reason) : SessionArchiveDirectoryReadResult;
}

public static class SessionArchiveDirectoryReader
{
    /// <summary>A missing root is a new archive unless content was known to exist there.</summary>
    public static SessionArchiveDirectoryReadResult Read(string root, bool knownContentExpected,
        Func<string, IReadOnlyList<string>> list, Func<Exception, bool> isMissing)
    {
        try
        {
            return new SessionArchiveDirectoryReadResult.Contents(list(root));
        }
        catch (Exception exception)
        {
            if (isMissing(exception))
            {
                return knownContentExpected
                    ? new SessionArchiveDirectoryReadResult.Unavailable(root,
                        "已知会议记录目录现已缺失，原记录未显示或删除。")
                    : new SessionArchiveDirectoryReadResult.Contents([]);
            }
            return new SessionArchiveDirectoryReadResult.Unavailable(root, exception.Message);
        }
    }
}

/// <summary>
/// Durable evidence that at least one session archive was previously created. It lives outside
/// the Sessions directory so losing that root does not erase the evidence.
/// </summary>
public static class SessionArchiveContentMarker
{
    private const string Content = "sessions-archive-present-v1";

    public static bool WasPreviouslyPopulated(string markerPath) => File.Exists(markerPath);

    public static void Record(string markerPath)
    {
        var directory = Path.GetDirectoryName(markerPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = markerPath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, Content);
        File.Move(temporary, markerPath, overwrite: true);
    }
}

/// <summary>Why an archive metadata file could not be used, without turning it into "absent".</summary>
public abstract record SessionArchiveMetadataReadIssue
{
    public sealed record Missing(string Path) : SessionArchiveMetadataReadIssue;
    public sealed record Unreadable(string Path, string Reason) : SessionArchiveMetadataReadIssue;
    public sealed record Malformed(string Path, string Reason) : SessionArchiveMetadataReadIssue;
}

public abstract record SessionArchiveMetadataReadResult<T>
{
    public sealed record Success(T Value) : SessionArchiveMetadataReadResult<T>;
    public sealed record Failure(SessionArchiveMetadataReadIssue Issue) : SessionArchiveMetadataReadResult<T>;
}

/// <summary>
/// Keeps file-read failures distinct from decoding failures so callers never turn an unreadable
/// or malformed metadata file into an absent meeting (port of SessionArchiveMetadataReader).
/// </summary>
public static class SessionArchiveMetadataReader
{
    public static SessionArchiveMetadataReadResult<T> Read<T>(string path, Func<string, byte[]> load,
        Func<byte[], T> decode, Func<Exception, bool> isMissing)
    {
        byte[] bytes;
        try
        {
            bytes = load(path);
        }
        catch (Exception exception)
        {
            return new SessionArchiveMetadataReadResult<T>.Failure(isMissing(exception)
                ? new SessionArchiveMetadataReadIssue.Missing(path)
                : new SessionArchiveMetadataReadIssue.Unreadable(path, exception.Message));
        }
        try
        {
            return new SessionArchiveMetadataReadResult<T>.Success(decode(bytes));
        }
        catch (Exception exception)
        {
            return new SessionArchiveMetadataReadResult<T>.Failure(
                new SessionArchiveMetadataReadIssue.Malformed(path, exception.Message));
        }
    }
}

/// <summary>One answer event of the archive (port of the SessionAnswerEvent fields used here).</summary>
public sealed record SessionAnswerEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public DateTimeOffset Date { get; init; }
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
    public string Model { get; init; } = "";
    public double? Seconds { get; init; }
    public double? FirstTokenSeconds { get; init; }
    public bool Automatic { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? WebAnswer { get; init; }
    public IReadOnlyList<WebResult>? WebSources { get; init; }
    public EvidenceBundle? Evidence { get; init; }

    /// <summary>Whether the answer finished; missing on old archives and unknown values stay incomplete.</summary>
    public AnswerCompletion? Completion { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsComplete => Completion is null or AnswerCompletion.Complete;
}

/// <summary>Earliest sessions.json stored answers without prompts; keep them explicitly labelled.</summary>
public static class LegacySessionAnswerMigration
{
    public static IReadOnlyList<SessionAnswerEvent> Events(IReadOnlyList<string> answers, DateTimeOffset date) =>
        answers.Select((answer, index) => new SessionAnswerEvent
        {
            Date = date,
            Question = $"旧版回答 {index + 1}（问题未保存）",
            Answer = answer,
            Model = "旧版记录",
            Automatic = false
        }).ToList();
}

/// <summary>Tracks submitted translation revisions until the archive reports them persisted.</summary>
public sealed class SessionTranslationWriteReceipts
{
    private readonly HashSet<int> pending = [];

    public bool HasPending => pending.Count > 0;

    public void RecordSubmittedRevision(int revision)
    {
        if (revision > 0) pending.Add(revision);
    }

    /// <returns>True when this receipt acknowledges one or more tracked translation writes.</returns>
    public bool Acknowledge(int persistedRevision)
    {
        var acknowledged = pending.Where(revision => revision <= persistedRevision).ToList();
        if (acknowledged.Count == 0) return false;
        foreach (var revision in acknowledged) pending.Remove(revision);
        return true;
    }
}

/// <summary>
/// Injectable archive writer used by the app and fault-injection tests
/// (port of SessionArchiveFileAppender).
/// </summary>
public sealed class SessionArchiveFileAppender
{
    private readonly Action<byte[], string, SessionArchiveWriteMode> writeBytes;

    public SessionArchiveFileAppender(Action<byte[], string, SessionArchiveWriteMode> write) => writeBytes = write;

    public static SessionArchiveFileAppender Live() => new(WriteBytes);

    /// <summary>B22: append through an injected writer that can fail mid-write; the file is rolled back.</summary>
    public static SessionArchiveFileAppender UsingFileHandleAppend(Action<byte[], Stream> append) =>
        new((data, path, mode) =>
        {
            if (mode == SessionArchiveWriteMode.Replace) { WriteBytes(data, path, mode); return; }
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            var originalEnd = stream.Length;
            try
            {
                stream.Position = stream.Length;
                append(data, stream);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                stream.SetLength(originalEnd);
                throw;
            }
        });

    public void Append(byte[] data, string path) => Write(data, path, SessionArchiveWriteMode.Append);

    public void Replace(byte[] data, string path) => Write(data, path, SessionArchiveWriteMode.Replace);

    public void Write(byte[] data, string path, SessionArchiveWriteMode mode) => writeBytes(data, path, mode);

    public static void WriteBytes(byte[] data, string path, SessionArchiveWriteMode mode)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        if (mode == SessionArchiveWriteMode.Replace)
        {
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, overwrite: true);
            return;
        }

        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        var originalEnd = stream.Length;
        try
        {
            if (originalEnd > 0)
            {
                stream.Position = originalEnd - 1;
                if (stream.ReadByte() != '\n')
                {
                    stream.Position = originalEnd;
                    stream.WriteByte((byte)'\n');
                }
            }
            stream.Position = stream.Length;
            stream.Write(data, 0, data.Length);
            stream.Flush(flushToDisk: true);
        }
        catch
        {
            stream.SetLength(originalEnd);
            throw;
        }
    }

    /// <summary>Appends one UTF-8 JSON line without requiring the caller to add a newline.</summary>
    public void AppendLine(string json, string path)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        Append([.. bytes, (byte)'\n'], path);
    }
}

/// <summary>
/// A session folder without meta.json is a damaged meeting only when recording files are still
/// there; folders holding only derived notes are leftovers, not damaged meetings.
/// </summary>
public static class SessionArchiveLeftover
{
    public static readonly IReadOnlySet<string> RecordingFiles = new HashSet<string>(StringComparer.Ordinal)
    {
        "transcript.jsonl", "questions.jsonl", "answers.jsonl", "context.json",
        "translations.jsonl", "voices.jsonl"
    };

    public static bool IsLeftover(IReadOnlyList<string> fileNames)
    {
        var names = fileNames.Where(name => !name.StartsWith('.')).ToList();
        if (names.Contains("meta.json")) return false;
        return !names.Any(RecordingFiles.Contains);
    }
}
