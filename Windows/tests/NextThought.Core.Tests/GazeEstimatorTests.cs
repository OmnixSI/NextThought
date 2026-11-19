using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for eye geometry, calibration and gaze readings.</summary>
public sealed class GazeEstimatorTests
{
    private static EyeGeometry Eye(double pupilX = 5, double pupilY = 2.5, double height = 5) => new(
        [new GazePoint(0, height / 2), new GazePoint(5, 0), new GazePoint(10, height / 2), new GazePoint(5, height)],
        new GazePoint(pupilX, pupilY));

    private static GazeFrame Frame(double pupilX = 5, double? yaw = 0, double? pitch = 0, double time = 0) =>
        new(Eye(pupilX), Eye(pupilX), yaw, pitch, time);

    [Fact]
    public void ReadingCentresThePupilInTheOpening()
    {
        var reading = Eye().Reading();
        Assert.NotNull(reading);
        Assert.Equal(0, reading!.Value.X, 6);
        Assert.Equal(0, reading.Value.Y, 6);
        Assert.Equal(0.5, reading.Value.Openness, 6);
        Assert.Null(new EyeGeometry([new GazePoint(5, 5), new GazePoint(5, 5), new GazePoint(5, 5), new GazePoint(5, 5)], new GazePoint(5, 5)).Reading());
        Assert.Null(new EyeGeometry([new GazePoint(0, 0)], new GazePoint(0, 0)).Reading());
    }

    [Fact]
    public void SignatureAveragesOpenEyes()
    {
        var frame = new GazeFrame(Eye(), Eye(6), 0.1, 0.2, 0);
        var signature = GazeEstimator.Signature(frame);
        Assert.NotNull(signature);
        Assert.Equal(0.1, signature!.X, 6);
        Assert.Equal(0.1, signature.Yaw, 6);
        Assert.Equal(0.2, signature.Pitch, 6);
    }

    [Fact]
    public void ClosedEyesGiveNoSignature()
    {
        var closed = new GazeFrame(Eye(height: 1), Eye(height: 1), null, null, 0);
        Assert.Null(GazeEstimator.Signature(closed));
        // One open eye is enough.
        Assert.NotNull(GazeEstimator.Signature(new GazeFrame(Eye(height: 1), Eye(), null, null, 0)));
    }

    [Fact]
    public void CalibrationUsesTheMedian()
    {
        var estimator = new GazeEstimator();
        for (var index = 0; index < 9; index++) estimator.AddCalibration(Frame(4 + index * 0.25, yaw: 0.1, time: index));
        Assert.True(estimator.FinishCalibration());
        Assert.True(estimator.IsCalibrated);
        Assert.Equal(0, estimator.Baseline.X, 6);
        Assert.Equal(0.1, estimator.Baseline.Yaw, 6);
        Assert.False(new GazeEstimator().FinishCalibration());
    }

    [Fact]
    public void LookingAtTheCalibratedLensReadsAsOnCamera()
    {
        var estimator = new GazeEstimator();
        estimator.Restore(new GazeSignature(0, 0));
        var reading = estimator.Ingest(Frame());
        Assert.Equal(GazeReading.StateValue.OnCamera, reading.State);
        Assert.Equal(0, reading.Deviation, 6);
    }

    [Fact]
    public void MovingThePupilAwayBreaksAndReturnsEyeContact()
    {
        var estimator = new GazeEstimator();
        estimator.Restore(new GazeSignature(0, 0));
        estimator.Ingest(Frame(time: 0));
        var away = estimator.Ingest(Frame(pupilX: 9, time: 1));
        Assert.Equal(GazeReading.StateValue.Away, away.State);
        Assert.True(away.Deviation > 1);
        // Back near the lens: the hysteresis needs a clear return (smoothed over frames).
        GazeReading? back = null;
        for (var second = 2; second <= 5; second++) back = estimator.Ingest(Frame(time: second));
        Assert.Equal(GazeReading.StateValue.OnCamera, back!.State);
    }

    [Fact]
    public void BlinksKeepThePreviousVerdict()
    {
        var estimator = new GazeEstimator();
        estimator.Restore(new GazeSignature(0, 0));
        estimator.Ingest(Frame(time: 0));
        var blink = estimator.Ingest(new GazeFrame(Eye(height: 1), Eye(height: 1), 0, 0, 1));
        Assert.Equal(GazeReading.StateValue.OnCamera, blink.State);
    }

    [Fact]
    public void NoFaceAndContactRatio()
    {
        var estimator = new GazeEstimator();
        estimator.Restore(new GazeSignature(0, 0));
        Assert.Equal(GazeReading.StateValue.NoFace, estimator.Ingest(null).State);
        for (var second = 0; second <= 11; second++) estimator.Ingest(Frame(time: second));
        Assert.NotNull(estimator.ContactRatio);
        Assert.Equal(1, estimator.ContactRatio!.Value, 6);
        estimator.ResetSession();
        Assert.Null(estimator.ContactRatio);
    }
}
