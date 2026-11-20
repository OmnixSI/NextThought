using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from VoiceActivitySegmenterTests.swift: a sentence is cut out of the short audio
/// window by the start and end the recognizer reported, so a cut can be checked for voice quality.
/// This class covers the segmenter's waiting, partials, overlap and reset behaviour.
/// </summary>
public sealed class VoiceActivitySegmenterTestsParityTests
{
    private static short[] Samples(int count, short value) => Enumerable.Repeat(value, count).ToArray();

    [Fact]
    public void WaitsForSpeechAndFinalizesAfterPause()
    {
        var segmenter = new VoiceActivitySegmenter();
        var silence = Samples(8_000, 0);
        var speech = Samples(8_000, 5_000);

        Assert.Empty(segmenter.Append(silence));
        Assert.Empty(segmenter.Append(speech));
        Assert.Empty(segmenter.Append(speech));
        Assert.Empty(segmenter.Append(silence));
        var events = segmenter.Append(silence);
        Assert.Single(events);
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, events[0].Kind);
        Assert.True(events[0].Samples.Length >= 24_000);
        Assert.True(events[0].StartSample < events[0].EndSample);
    }

    [Fact]
    public void EmitsTemporaryPartialBeforeFinal()
    {
        var segmenter = new VoiceActivitySegmenter();
        var speech = Samples(8_000, 5_000);
        var kinds = new List<SpeechSegmentEvent.KindValue>();

        for (var index = 0; index < 10; index++)
            kinds.AddRange(segmenter.Append(speech).Select(item => item.Kind));
        kinds.Add(segmenter.Flush()?.Kind ?? SpeechSegmentEvent.KindValue.Partial);

        Assert.Contains(SpeechSegmentEvent.KindValue.Partial, kinds);
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, kinds[^1]);
    }

    [Fact]
    public void CarriesOverlapWhenContinuousSpeechReachesLimit()
    {
        var segmenter = new VoiceActivitySegmenter();
        var speech = Samples(8_000, 5_000);
        var finalEvents = new List<SpeechSegmentEvent>();

        for (var index = 0; index < 40; index++)
            finalEvents.AddRange(segmenter.Append(speech).Where(item => item.Kind == SpeechSegmentEvent.KindValue.Final));

        Assert.True(finalEvents.Count >= 1);
        Assert.True(finalEvents[0].Samples.Length >= 192_000);
        Assert.True(finalEvents[0].Samples.Length <= 200_000);
        Assert.True(finalEvents.Count >= 1);
        Assert.True(finalEvents[0].EndSample > finalEvents[0].StartSample);
    }

    [Fact]
    public void FiltersSilenceAndVeryShortVoice()
    {
        var segmenter = new VoiceActivitySegmenter();
        var silence = Samples(8_000, 0);
        var shortSpeech = Samples(2_000, 5_000);

        Assert.Empty(segmenter.Append(silence));
        Assert.Empty(segmenter.Append(shortSpeech));
        Assert.Null(segmenter.Flush());
    }

    [Fact]
    public void ResetDiscardsPendingAudio()
    {
        var segmenter = new VoiceActivitySegmenter();
        var speech = Samples(16_000, 5_000);
        Assert.Empty(segmenter.Append(speech));
        segmenter.Reset();
        Assert.Null(segmenter.Flush());
    }

    [Fact]
    public void RetainsWholeFirstSpeechChunkAndPreRoll()
    {
        var segmenter = new VoiceActivitySegmenter();
        _ = segmenter.Append(Samples(8_000, 0));
        var firstWord = Samples(8_000, 5_000);
        _ = segmenter.Append(firstWord);
        var segment = segmenter.Flush();
        Assert.NotNull(segment);
        Assert.Equal(12_000, segment!.Samples.Length);
        Assert.Equal<short>(firstWord, segment.Samples[^8_000..]);
        Assert.Equal(4_000L, segment.StartSample);
        Assert.Equal(16_000L, segment.EndSample);
    }

    [Fact]
    public void ContinuousSpeechUsesHalfSecondOverlap()
    {
        var segmenter = new VoiceActivitySegmenter();
        SpeechSegmentEvent? first = null;
        for (var index = 0; index < 120; index++)
        {
            var events = segmenter.Append(Samples(1_600, 5_000));
            if (events.FirstOrDefault(item => item.Kind == SpeechSegmentEvent.KindValue.Final) is { } finalEvent)
                first = finalEvent;
        }
        _ = segmenter.Append(Samples(1_600, 5_000));
        var tail = segmenter.Flush();
        Assert.NotNull(first);
        Assert.NotNull(tail);
        Assert.Equal(192_000L, first!.EndSample);
        Assert.Equal(184_000L, tail!.StartSample);
        Assert.Equal(9_600, tail.Samples.Length);
        Assert.NotEqual(first.Id, tail.Id);
    }
}
