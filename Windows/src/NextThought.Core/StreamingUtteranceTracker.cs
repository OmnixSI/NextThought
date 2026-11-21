namespace NextThought.Core;

/// <summary>
/// J5: the non-ONNX state of Sources/NextThought/ZipformerStreamingTranscriber.swift, extracted so
/// the Windows recognizer session shares the Mac rules:
///  - the offline re-decode of a segment may only replace what that segment covers
///    (<see cref="UtteranceText.Final"/>); the quiet minutes the stream heard stay;
///  - overlapping audio a split segment re-sends is dropped before re-decoding, so words are not
///    lost and not written twice;
///  - words no segmenter will close are closed by the 2.5 s / 20 s pending watch;
///  - a final is delivered once: a replayed or stale final is dropped, and a partial of an already
///    finished utterance is not shown again.
/// </summary>
public sealed class StreamingUtteranceTracker
{
    public const int SampleRate = 16_000;

    /// <summary>Words on screen that are not a sentence yet (Mac PendingUtterance).</summary>
    public PendingUtterance Pending { get; } = new();

    /// <summary>Absolute sample index up to which audio has been committed (Mac committedThroughSample).</summary>
    public long CommittedThroughSample { get; private set; }

    /// <summary>
    /// Audio before this moment went out with an utterance closed by the pending watch, not by the
    /// segmenter; a segment that reaches back before it is trimmed (Mac committedThroughDate).
    /// </summary>
    public DateTimeOffset CommittedThroughDate { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>End of the last delivered final; anything up to it must not be delivered again.</summary>
    public DateTimeOffset LastFinalizedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>A new stream (meeting start, audio device reset).</summary>
    public void Reset()
    {
        CommittedThroughSample = 0;
        CommittedThroughDate = DateTimeOffset.MinValue;
        LastFinalizedAt = DateTimeOffset.MinValue;
        Pending.Closed();
    }

    /// <summary>The live stream decoded <paramref name="text"/> for the utterance in progress.</summary>
    public void Heard(string text, DateTimeOffset at) => Pending.Heard(text, at);

    /// <summary>Nobody closed the utterance: the pending watch should close it now.</summary>
    public bool IsOverdue(DateTimeOffset at) => Pending.IsOverdue(at);

    /// <summary>The utterance was closed (by the segmenter, or by the pending watch).</summary>
    public void ClosedPending() => Pending.Closed();

    /// <summary>Mark the audio a committed final used as committed (the segmenter re-sends overlap).</summary>
    public void MarkCommittedThrough(long endSample) =>
        CommittedThroughSample = Math.Max(CommittedThroughSample, endSample);

    /// <summary>An utterance closed without a segment: later audio before this moment is overlap.</summary>
    public void MarkClosedWithoutSegment(DateTimeOffset at) => CommittedThroughDate = at;

    /// <summary>
    /// Mac ZipformerStreamingTranscriber.newSamples: how many leading samples of a segment were
    /// already committed, so only the uncovered rest is re-decoded.
    /// </summary>
    public long OverlapToDrop(long segmentStartSample, DateTimeOffset? streamStartedAt, DateTimeOffset segmentStartTime)
    {
        var alreadyCommitted = Math.Max(0, CommittedThroughSample - segmentStartSample);
        if (streamStartedAt is { } origin)
        {
            var begins = origin.AddSeconds((double)segmentStartSample / SampleRate);
            alreadyCommitted = Math.Max(alreadyCommitted,
                (long)Math.Floor((CommittedThroughDate - begins).TotalSeconds * SampleRate));
        }
        return Math.Max(0, alreadyCommitted);
    }

    /// <summary>
    /// Mac UtteranceText.final: the offline text replaces the stream only when it covers what the
    /// stream heard; otherwise the stream's words are kept (text is never lost).
    /// </summary>
    public static string FinalText(string streaming, double streamSeconds, string refined, double segmentSeconds) =>
        UtteranceText.Final(streaming, streamSeconds, refined, segmentSeconds);

    /// <summary>Mac deliver: finals are monotone; a partial of a finished utterance is dropped.</summary>
    public bool ShouldDeliver(bool isFinal, DateTimeOffset endedAt) => endedAt > LastFinalizedAt;

    /// <summary>Record a delivered event; only finals advance the delivery point.</summary>
    public void Delivered(bool isFinal, DateTimeOffset endedAt)
    {
        if (isFinal) LastFinalizedAt = endedAt;
    }
}
