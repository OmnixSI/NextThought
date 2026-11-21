namespace NextThought.Core;

public sealed record GazePoint(double X, double Y);

/// <summary>One eye in image coordinates; only ratios are used (port of EyeGeometry).</summary>
public sealed record EyeGeometry(IReadOnlyList<GazePoint> Contour, GazePoint Pupil)
{
    /// <summary>Pupil position inside the opening in half eye-widths, plus openness.</summary>
    public (double X, double Y, double Openness)? Reading()
    {
        if (Contour.Count < 4) return null;
        var left = Contour.OrderBy(point => point.X).First();
        var right = Contour.OrderBy(point => point.X).Last();
        var axisX = right.X - left.X;
        var axisY = right.Y - left.Y;
        var width = Math.Sqrt(axisX * axisX + axisY * axisY);
        if (width <= 1e-6) return null;
        var ux = axisX / width;
        var uy = axisY / width;
        var centerX = (left.X + right.X) / 2;
        var centerY = (left.Y + right.Y) / 2;
        var low = double.PositiveInfinity;
        var high = double.NegativeInfinity;
        foreach (var point in Contour)
        {
            var across = -(point.X - centerX) * uy + (point.Y - centerY) * ux;
            low = Math.Min(low, across);
            high = Math.Max(high, across);
        }
        var dx = Pupil.X - centerX;
        var dy = Pupil.Y - centerY;
        var along = dx * ux + dy * uy;
        var pupilAcross = -dx * uy + dy * ux - (low + high) / 2;
        return (along / (width / 2), pupilAcross / (width / 2), (high - low) / width);
    }
}

public sealed record GazeFrame(EyeGeometry LeftEye, EyeGeometry RightEye, double? Yaw, double? Pitch, double Time);

public sealed record GazeSignature(double X, double Y, double Yaw = 0, double Pitch = 0);

public sealed record GazeReading(GazeReading.StateValue State, double Deviation = 0, GazeSignature? Offset = null)
{
    public enum StateValue
    {
        OnCamera,
        Away,
        NoFace
    }

    public GazeSignature OffsetValue => Offset ?? new GazeSignature(0, 0);
}

/// <summary>
/// Eye contact from face landmarks alone (port of GazeEstimator): learns the user's own
/// on-camera signature from a short calibration and reports how far a frame is from it.
/// </summary>
public sealed class GazeEstimator
{
    /// <summary>An eye less open than this is blinking or squinting.</summary>
    internal const double MinimumOpenness = 0.14;

    /// <summary>Head turn that still faces the lens (≈14°).</summary>
    internal const double HeadTolerance = 0.25;

    private readonly List<GazeSignature> calibration = [];
    private GazeSignature? smoothed;
    private bool looking = true;
    private double? lastTime;

    public GazeEstimator(double tolerance = 0.12)
    {
        Tolerance = tolerance;
    }

    /// <summary>Pupil travel, in half eye-widths, that still reads as eye contact.</summary>
    public double Tolerance { get; set; }

    public GazeSignature Baseline { get; private set; } = new(0, 0);
    public bool IsCalibrated { get; private set; }
    public double TrackedTime { get; private set; }
    public double ContactTime { get; private set; }

    /// <summary>The signature of a frame, or null while the eyes are closed.</summary>
    public static GazeSignature? Signature(GazeFrame frame)
    {
        if (frame.LeftEye.Reading() is not { } left || frame.RightEye.Reading() is not { } right) return null;
        if (left.Openness < MinimumOpenness && right.Openness < MinimumOpenness) return null;
        // A half-closed eye is ignored; when both are open they are averaged.
        var eyes = new[] { left, right }.Where(eye => eye.Openness >= MinimumOpenness).ToList();
        return new GazeSignature(eyes.Average(eye => eye.X), eyes.Average(eye => eye.Y), frame.Yaw ?? 0, frame.Pitch ?? 0);
    }

    /// <summary>Feeds frames recorded while the user looks into the lens.</summary>
    public void AddCalibration(GazeFrame frame)
    {
        if (Signature(frame) is { } signature) calibration.Add(signature);
    }

    /// <summary>Takes the median of the calibration frames, which shrugs off a blink or a glance.</summary>
    public bool FinishCalibration(int minimumFrames = 8)
    {
        if (calibration.Count < minimumFrames)
        {
            calibration.Clear();
            return false;
        }
        static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(value => value).ToList();
            return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
        }
        Baseline = new GazeSignature(
            Median(calibration.Select(item => item.X)),
            Median(calibration.Select(item => item.Y)),
            Median(calibration.Select(item => item.Yaw)),
            Median(calibration.Select(item => item.Pitch)));
        IsCalibrated = true;
        smoothed = null;
        calibration.Clear();
        return true;
    }

    public void CancelCalibration() => calibration.Clear();

    public void Restore(GazeSignature baseline)
    {
        Baseline = baseline;
        IsCalibrated = true;
        smoothed = null;
    }

    /// <summary>Reads one frame; a null frame means no face was found.</summary>
    public GazeReading Ingest(GazeFrame? frame)
    {
        if (frame is null)
        {
            lastTime = null;
            return new GazeReading(GazeReading.StateValue.NoFace);
        }
        var elapsed = lastTime is { } previous ? Math.Clamp(frame.Time - previous, 0, 1) : 0;
        lastTime = frame.Time;
        // A blink keeps the previous verdict: nobody breaks eye contact by blinking.
        if (Signature(frame) is { } signature)
        {
            if (smoothed is { } prior)
            {
                static double Ease(double from, double to) => from + (to - from) * 0.35;
                smoothed = new GazeSignature(
                    Ease(prior.X, signature.X), Ease(prior.Y, signature.Y),
                    Ease(prior.Yaw, signature.Yaw), Ease(prior.Pitch, signature.Pitch));
            }
            else
            {
                smoothed = signature;
            }
        }
        if (smoothed is not { } current) return new GazeReading(GazeReading.StateValue.NoFace);
        var offset = new GazeSignature(
            current.X - Baseline.X, current.Y - Baseline.Y,
            current.Yaw - Baseline.Yaw, current.Pitch - Baseline.Pitch);
        // Eyelids follow the gaze vertically, so the vertical reading is noisier.
        var eyes = Math.Sqrt(offset.X * offset.X + offset.Y * 0.8 * (offset.Y * 0.8)) / Tolerance;
        var head = Math.Sqrt(offset.Yaw * offset.Yaw + offset.Pitch * offset.Pitch) / HeadTolerance;
        var deviation = Math.Max(eyes, head);
        // Hysteresis: leaving takes a clear move, coming back a clear return.
        if (looking && deviation > 1.25) looking = false;
        else if (!looking && deviation < 0.85) looking = true;
        TrackedTime += elapsed;
        if (looking) ContactTime += elapsed;
        return new GazeReading(looking ? GazeReading.StateValue.OnCamera : GazeReading.StateValue.Away, deviation, offset);
    }

    /// <summary>Share of tracked time spent looking at the lens; null before ten seconds.</summary>
    public double? ContactRatio => TrackedTime >= 10 ? ContactTime / TrackedTime : null;

    public void ResetSession()
    {
        TrackedTime = 0;
        ContactTime = 0;
        lastTime = null;
        smoothed = null;
        looking = true;
    }
}
