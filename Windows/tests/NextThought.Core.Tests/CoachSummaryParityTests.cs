using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from CoachSummaryTests.swift: too little speech gives nothing, pace and share cover
/// the whole meeting, the longest monologue keeps the maximum and nudges are counted, the summary
/// round-trips through JSON, and a cancelled calibration leaves no frames behind.
/// </summary>
public sealed class CoachSummaryParityTests
{
    private static SpokenSegment Segment(bool me, double start, double end, int chars = 40) =>
        new(me, start, end, new string('话', chars));

    [Fact]
    public void TooLittleSpeechGivesNothing()
    {
        var accumulator = new CoachSummaryAccumulator();
        accumulator.Ingest(Segment(true, 0, 5));
        var summary = accumulator.Summary();
        Assert.True(summary.IsEmpty);
        Assert.Null(summary.Pace);
        Assert.Null(summary.TalkShare);
    }

    [Fact]
    public void PaceAndShareCoverTheWholeMeeting()
    {
        var accumulator = new CoachSummaryAccumulator();
        // 30 s of me at 4 syllables a second, 30 s of them: 240 per minute, half the talking.
        accumulator.Ingest(Segment(true, 0, 30, 120));
        accumulator.Ingest(Segment(false, 30, 60, 120));
        var summary = accumulator.Summary();
        Assert.Equal(240, summary.Pace!.Value, 3);
        Assert.Equal(0.5, summary.TalkShare!.Value, 3);
        Assert.False(summary.IsEmpty);
    }

    [Fact]
    public void LongestMonologueKeepsTheMaximumAndNudgesAreCounted()
    {
        var accumulator = new CoachSummaryAccumulator();
        accumulator.Observe(new CoachSnapshot(Monologue: 130));
        accumulator.Observe(new CoachSnapshot(Monologue: 40));
        accumulator.Record(CoachNudge.WrapUp);
        accumulator.Record(CoachNudge.WrapUp);
        accumulator.Record(CoachNudge.SlowDown);
        accumulator.RecordLookUp();
        accumulator.ObserveEyeContact(0.8);
        accumulator.ObserveEyeContact(null);
        var summary = accumulator.Summary();
        Assert.Equal(130, summary.LongestMonologue);
        // Swift keys nudges by rawValue ("wrapUp"); the C# accumulator lower-cases the enum name.
        Assert.Equal(2, summary.Nudges!["wrapup"]);
        Assert.Equal(1, summary.Nudges!["slowdown"]);
        Assert.Equal(1, summary.LookUpCues);
        Assert.Equal(0.8, summary.EyeContact);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var summary = new CoachSummary(
            Pace: 236,
            TalkShare: 0.41,
            LongestMonologue: 130,
            EyeContact: 0.78,
            Nudges: new Dictionary<string, int> { ["wrapUp"] = 1 },
            LookUpCues: 2);
        var data = JsonSerializer.SerializeToUtf8Bytes(summary, JsonDefaults.Options);
        var recovered = JsonSerializer.Deserialize<CoachSummary>(data, JsonDefaults.Options)!;

        // Swift compares the structs with `==`; the C# record holds a dictionary, so the same
        // value equality is checked field by field.
        Assert.Equal(summary.Pace, recovered.Pace);
        Assert.Equal(summary.TalkShare, recovered.TalkShare);
        Assert.Equal(summary.LongestMonologue, recovered.LongestMonologue);
        Assert.Equal(summary.EyeContact, recovered.EyeContact);
        Assert.Equal(summary.LookUpCues, recovered.LookUpCues);
        Assert.Equal(summary.Nudges!.OrderBy(pair => pair.Key), recovered.Nudges!.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void CancelledCalibrationLeavesNoFramesBehind()
    {
        var estimator = new GazeEstimator();
        for (var index = 0; index < 10; index++)
            estimator.AddCalibration(Frame(new GazePoint(0.4, -0.3), index * 0.1));
        estimator.CancelCalibration();
        Assert.False(estimator.FinishCalibration());
    }

    // Mirrors GazeEstimatorTests.frame(pupil:time:) from SceneAndGazeTests.swift.
    private static GazeFrame Frame(GazePoint pupil, double time)
    {
        const double half = 15.0;
        const double height = 30.0 * 0.4 / 2;
        GazePoint[] contour =
        [
            new(-half, 0), new(-half / 2, height), new(0, height * 1.1), new(half / 2, height),
            new(half, 0), new(half / 2, -height), new(0, -height * 1.1), new(-half / 2, -height)
        ];
        static GazePoint Place(GazePoint center, GazePoint point) =>
            new(center.X + point.X, center.Y + point.Y);
        var left = new EyeGeometry(contour.Select(point => Place(new GazePoint(100, 100), point)).ToList(),
            Place(new GazePoint(100, 100), pupil));
        var right = new EyeGeometry(contour.Select(point => Place(new GazePoint(160, 100), point)).ToList(),
            Place(new GazePoint(160, 100), pupil));
        return new GazeFrame(left, right, 0, 0, time);
    }
}
