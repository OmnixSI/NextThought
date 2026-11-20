namespace NextThought.Platform;

/// <summary>
/// D5: the five Mac motion tokens (DesignSystem.swift, <c>enum Motion</c>) expressed as
/// framework-neutral curves. The desktop shell wraps these into Avalonia <c>Easing</c>
/// instances (Desktop/Motion.cs); keeping the maths here means the spring can be tested
/// against its closed-form solution without an Avalonia render pass.
///
/// SwiftUI <c>.spring(response:dampingFraction:)</c> with mass = 1 is
/// <c>stiffness = (2π / response)²</c> and <c>damping = 2 · ζ · √stiffness</c> (ζ is the
/// damping fraction). Every token below is under-damped, so the step response is the
/// analytic solution of the damped harmonic oscillator. Animation durations are the
/// spring's settling time, not the response value.
/// </summary>
public enum MotionKind { Hover, Expand, Phase, Snappy, Morph }

/// <summary>A mass-1 damped spring, with the analytic step response and velocity.</summary>
public readonly record struct SpringSpec
{
    public SpringSpec(double response, double dampingFraction)
    {
        if (!(response > 0) || double.IsNaN(response) || double.IsInfinity(response))
            throw new ArgumentOutOfRangeException(nameof(response), "response must be a positive finite number");
        if (!(dampingFraction > 0) || double.IsNaN(dampingFraction) || double.IsInfinity(dampingFraction))
            throw new ArgumentOutOfRangeException(nameof(dampingFraction), "dampingFraction must be positive");
        Response = response;
        DampingFraction = dampingFraction;
    }

    /// <summary>SwiftUI's <c>response</c>: the undamped period scale, in seconds.</summary>
    public double Response { get; }

    /// <summary>SwiftUI's <c>dampingFraction</c> (ζ). 1 is critically damped.</summary>
    public double DampingFraction { get; }

    /// <summary>mass = 1, stiffness = (2π / response)².</summary>
    public double Stiffness
    {
        get
        {
            var omega = 2 * Math.PI / Response;
            return omega * omega;
        }
    }

    /// <summary>Undamped natural frequency ω₀ = 2π / response.</summary>
    public double AngularFrequency => 2 * Math.PI / Response;

    /// <summary>damping = 2 · ζ · √stiffness.</summary>
    public double Damping => 2 * DampingFraction * Math.Sqrt(Stiffness);

    /// <summary>ω_d = ω₀ · √(1 − ζ²); zero at or beyond critical damping.</summary>
    public double DampedFrequency => DampingFraction < 1
        ? AngularFrequency * Math.Sqrt(1 - DampingFraction * DampingFraction)
        : 0;

    /// <summary>Analytic unit-step response (0 → 1) with zero initial velocity.</summary>
    public double Step(double seconds)
    {
        if (double.IsNaN(seconds)) return 0;
        if (seconds <= 0) return 0;
        var omega = AngularFrequency;
        var zeta = DampingFraction;
        if (zeta < 1)
        {
            var omegaD = DampedFrequency;
            var decay = Math.Exp(-zeta * omega * seconds);
            return 1 - decay * (Math.Cos(omegaD * seconds) + (zeta * omega / omegaD) * Math.Sin(omegaD * seconds));
        }
        if (zeta == 1)
        {
            var decay = Math.Exp(-omega * seconds);
            return 1 - (1 + omega * seconds) * decay;
        }
        // Over-damped: two real roots of s² + 2ζω₀s + ω₀² = 0.
        var root = omega * Math.Sqrt(zeta * zeta - 1);
        var r1 = -zeta * omega + root;
        var r2 = -zeta * omega - root;
        return 1 + (-r2 * Math.Exp(r1 * seconds) + r1 * Math.Exp(r2 * seconds)) / (r2 - r1);
    }

    /// <summary>Analytic velocity of <see cref="Step"/> (same normalisation, in 1/seconds).</summary>
    public double Velocity(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0) return 0;
        var omega = AngularFrequency;
        var zeta = DampingFraction;
        if (zeta < 1)
        {
            var omegaD = DampedFrequency;
            return Math.Exp(-zeta * omega * seconds) * (omega * omega / omegaD) * Math.Sin(omegaD * seconds);
        }
        if (zeta == 1)
        {
            var decay = Math.Exp(-omega * seconds);
            return omega * omega * seconds * decay;
        }
        var root = omega * Math.Sqrt(zeta * zeta - 1);
        var r1 = -zeta * omega + root;
        var r2 = -zeta * omega - root;
        return (-r2 * r1 * Math.Exp(r1 * seconds) + r1 * r2 * Math.Exp(r2 * seconds)) / (r2 - r1);
    }

    /// <summary>
    /// First-peak overshoot of an under-damped spring: e^(−πζ / √(1 − ζ²)).
    /// Returns 0 for critically or over-damped springs.
    /// </summary>
    public double PeakOvershoot => DampingFraction < 1
        ? Math.Exp(-Math.PI * DampingFraction / Math.Sqrt(1 - DampingFraction * DampingFraction))
        : 0;

    /// <summary>
    /// How long the spring needs to settle inside <paramref name="tolerance"/> (default 0.1%).
    /// Under-damped: from the envelope |1 − x(t)| ≤ e^(−ζω₀t)(1 + ζ/√(1−ζ²)).
    /// Critically/over-damped: a numeric search, because both terms decay monotonically.
    /// </summary>
    public double SettlingDuration(double tolerance = 0.001)
    {
        if (!(tolerance > 0)) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var omega = AngularFrequency;
        var zeta = DampingFraction;
        if (zeta < 1)
        {
            var envelope = 1 + zeta / Math.Sqrt(1 - zeta * zeta);
            return Math.Log(envelope / tolerance) / (zeta * omega);
        }
        // Monotone decay: bracket the settling time, then bisect inside it.
        var low = 0d;
        var high = 1d;
        for (var i = 0; i < 40 && Math.Abs(1 - Step(high)) > tolerance; i++) high *= 2;
        while (high - low > 1e-9)
        {
            var mid = (low + high) / 2;
            if (Math.Abs(1 - Step(mid)) <= tolerance) high = mid;
            else low = mid;
        }
        return high;
    }

    /// <summary>Position of the first peak (a quarter of the damped period past the start).</summary>
    public double FirstPeakSeconds => DampingFraction < 1 ? Math.PI / DampedFrequency : 0;
}

/// <summary>
/// A CSS-style cubic-bezier timing curve through (0,0), (x1,y1), (x2,y2), (1,1).
/// This is the Mac <c>Motion.hover</c> curve (0.2, 0.8, 0.2, 1), a timing curve — not a spring.
/// </summary>
public readonly record struct CubicBezierCurve
{
    public CubicBezierCurve(double x1, double y1, double x2, double y2)
    {
        if (x1 is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(x1));
        if (x2 is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(x2));
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }

    private double SampleX(double t)
    {
        var u = 1 - t;
        return 3 * u * u * t * X1 + 3 * u * t * t * X2 + t * t * t;
    }

    private double SampleY(double t)
    {
        var u = 1 - t;
        return 3 * u * u * t * Y1 + 3 * u * t * t * Y2 + t * t * t;
    }

    /// <summary>Eased output (0 → 1) for a linear input progress (0 → 1).</summary>
    public double Ease(double progress)
    {
        if (double.IsNaN(progress)) return 0;
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        var low = 0d;
        var high = 1d;
        for (var i = 0; i < 64; i++)
        {
            var mid = (low + high) / 2;
            if (SampleX(mid) < progress) low = mid;
            else high = mid;
            if (high - low < 1e-12) break;
        }
        return SampleY((low + high) / 2);
    }
}

/// <summary>One of the five Mac motion tokens: either a timing curve or a spring.</summary>
public readonly record struct MotionCurve(SpringSpec? Spring, CubicBezierCurve? Bezier)
{
    public bool IsSpring => Spring is not null;

    public static MotionCurve ForSpring(SpringSpec spring) => new(spring, null);
    public static MotionCurve ForBezier(CubicBezierCurve bezier) => new(null, bezier);
}

/// <summary>The five tokens exactly as DesignSystem.swift defines them.</summary>
public static class MotionTokens
{
    /// <summary><c>Motion.hover</c>: 0.14 s timing curve (0.2, 0.8, 0.2, 1) — no spring.</summary>
    public const double HoverSeconds = 0.14;
    public static readonly CubicBezierCurve HoverCurve = new(0.2, 0.8, 0.2, 1);

    /// <summary><c>Motion.expand</c>: spring(response: 0.32, dampingFraction: 0.9).</summary>
    public static readonly SpringSpec Expand = new(0.32, 0.9);

    /// <summary><c>Motion.phase</c>: spring(response: 0.46, dampingFraction: 0.9).</summary>
    public static readonly SpringSpec Phase = new(0.46, 0.9);

    /// <summary><c>Motion.snappy</c>: spring(response: 0.28, dampingFraction: 0.78).</summary>
    public static readonly SpringSpec Snappy = new(0.28, 0.78);

    /// <summary><c>Motion.morph</c>: spring(response: 0.5, dampingFraction: 0.82).</summary>
    public static readonly SpringSpec Morph = new(0.5, 0.82);

    public static readonly CubicBezierCurve Hover = HoverCurve;

    public static MotionCurve Curve(MotionKind kind) => kind switch
    {
        MotionKind.Hover => MotionCurve.ForBezier(HoverCurve),
        MotionKind.Expand => MotionCurve.ForSpring(Expand),
        MotionKind.Phase => MotionCurve.ForSpring(Phase),
        MotionKind.Snappy => MotionCurve.ForSpring(Snappy),
        _ => MotionCurve.ForSpring(Morph)
    };

    /// <summary>Settling time of the spring, or 0.14 s for the hover curve.</summary>
    public static TimeSpan Duration(MotionKind kind) => kind == MotionKind.Hover
        ? TimeSpan.FromSeconds(HoverSeconds)
        : TimeSpan.FromSeconds(Curve(kind).Spring!.Value.SettlingDuration());
}

/// <summary>
/// Runs a token over wall-clock time for hand-driven animations (the cue island morph)
/// and exposes the same progress for Avalonia easings. When the system asks for less
/// motion every token jumps straight to its final value; nothing animates.
/// </summary>
public static class MotionPlan
{
    /// <summary>False when the shell must not animate at all (system "show animations" off).</summary>
    public static bool IsAnimated(bool reduceMotion) => !reduceMotion;

    public static TimeSpan Duration(MotionKind kind, bool reduceMotion) =>
        reduceMotion ? TimeSpan.Zero : MotionTokens.Duration(kind);

    /// <summary>Eased interpolation progress at <paramref name="elapsed"/> into the token.</summary>
    public static double Progress(MotionKind kind, TimeSpan elapsed, bool reduceMotion)
    {
        if (reduceMotion || elapsed >= MotionTokens.Duration(kind)) return 1;
        if (elapsed <= TimeSpan.Zero) return 0;
        return CurveAt(kind, elapsed.TotalSeconds);
    }

    /// <summary>
    /// Easing function for Avalonia key-frame animations: input is linear progress over the
    /// token's duration, output the spring/curve response (springs may overshoot past 1).
    /// </summary>
    public static double EasedProgress(MotionKind kind, double linearProgress)
    {
        if (double.IsNaN(linearProgress)) return 0;
        if (linearProgress <= 0) return 0;
        if (linearProgress >= 1) return 1;
        var duration = MotionTokens.Duration(kind);
        return CurveAt(kind, linearProgress * duration.TotalSeconds);
    }

    private static double CurveAt(MotionKind kind, double seconds)
    {
        var curve = MotionTokens.Curve(kind);
        return curve.IsSpring ? curve.Spring!.Value.Step(seconds) : curve.Bezier!.Value.Ease(seconds / HoverSeconds);
    }

    private const double HoverSeconds = MotionTokens.HoverSeconds;
}
