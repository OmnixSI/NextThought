using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E10: the Mac LiveCoach eye-contact cue (LiveCoach.receive + shouldLookUp): while the user is
/// speaking, look-away time accumulates; four seconds of it raise the "看镜头" cue, while looking
/// back, ending the sentence or losing the face clear it. A monologue keeps refreshing the
/// four-second speaking window with each finished line, which is why the tests also call Heard.
/// </summary>
public sealed class GazeAttentionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(8));

    private static GazeReading Reading(GazeReading.StateValue state) => new(state);

    [Fact]
    public void LookingAwayWhileSpeakingRaisesTheCueAfterFourSeconds()
    {
        var attention = new GazeAttention();
        attention.Heard(Start);
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(1)));
        attention.Heard(Start.AddSeconds(3)); // the next sentence keeps the speaking window open
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(4)));
        Assert.True(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(5)));
        Assert.True(attention.ShouldLookUp);
    }

    [Fact]
    public void LookingBackAtTheCameraClearsTheCue()
    {
        var attention = new GazeAttention();
        attention.Heard(Start);
        attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(1));
        attention.Heard(Start.AddSeconds(3));
        Assert.True(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(6)));
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.OnCamera), Start.AddSeconds(7)));
        Assert.False(attention.ShouldLookUp);
    }

    [Fact]
    public void AwayWhileNobodyIsSpeakingIsNotWorthACue()
    {
        var attention = new GazeAttention();
        attention.Heard(Start.AddSeconds(-5)); // the last sentence ended five seconds ago
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(1)));
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(10)));
        Assert.False(attention.ShouldLookUp);
    }

    [Fact]
    public void FinishingTheSentenceClearsAccumulatedAwayTime()
    {
        var attention = new GazeAttention();
        attention.Heard(Start);
        attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(1));
        // The speaking window closes, so the old away interval is dropped.
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(8)));
        attention.Heard(Start.AddSeconds(8));
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(9)));
        attention.Heard(Start.AddSeconds(12));
        Assert.True(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(13)));
    }

    [Fact]
    public void NoFaceDoesNotCountAsLookingAway()
    {
        var attention = new GazeAttention();
        attention.Heard(Start);
        attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(1));
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.NoFace), Start.AddSeconds(3)));
        attention.Heard(Start.AddSeconds(3));
        Assert.False(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(4)));
        attention.Heard(Start.AddSeconds(7));
        Assert.True(attention.Ingest(Reading(GazeReading.StateValue.Away), Start.AddSeconds(8)));
    }
}
