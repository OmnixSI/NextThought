using System.Security.Cryptography;
using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>A11: the Mac's three on-disk revisions use its length-prefixed SHA-256 digest.</summary>
public sealed class RevisionPortTests
{
    private static string ExpectedDigest(params string[] parts)
    {
        using var stream = new MemoryStream();
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            var length = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    [Fact]
    public void SwiftDigestPrefixesEveryPartWithItsByteLength()
    {
        Assert.Equal(ExpectedDigest("structured-notes-v1", "abc"), SwiftDigest.Of("structured-notes-v1", "abc"));
    }

    [Fact]
    public void StructuredNotesRevisionIsAFullDigestAndTracksEdits()
    {
        var notes = new StructuredNotesDoc("general", "Meeting", "zh", "Notes",
            [new NotesSectionResult("actions", "Todo", NotesSectionKind.Actions, false,
                [new NotesItem { Text = "first" }])]);
        var revision = SessionProjectionBuilder.StructuredNotesRevision(notes);
        Assert.Equal(64, revision.Length);
        Assert.Equal(revision, SessionProjectionBuilder.StructuredNotesRevision(notes));
        var edited = notes with
        {
            Sections = [new NotesSectionResult("actions", "Todo", NotesSectionKind.Actions, false,
                [new NotesItem { Text = "second" }])]
        };
        Assert.NotEqual(revision, SessionProjectionBuilder.StructuredNotesRevision(edited));
    }

    [Fact]
    public void TranscriptSourceRevisionCoversTheOriginalAndCorrectedText()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var line = new TranscriptLine { Id = Guid.NewGuid(), Date = start.AddSeconds(3), Speaker = "remote", Text = "old" };
        var sessionId = Guid.NewGuid();
        var meta = new SessionMeta { Id = sessionId, StartedAt = start };
        var revision = SessionProjectionBuilder.TranscriptSourceRevision(meta, [line], []);
        Assert.Equal(64, revision.Length);
        var correction = new TranscriptCorrection
        { Line = 0, LineId = line.Id, Original = "old", Corrected = "new" };
        var withCorrection = SessionProjectionBuilder.TranscriptSourceRevision(meta, [line], [correction]);
        Assert.NotEqual(revision, withCorrection);
        Assert.Equal(64, withCorrection.Length);
        Assert.Equal(withCorrection, SessionProjectionBuilder.TranscriptSourceRevision(meta, [line], [correction]));
    }
}
