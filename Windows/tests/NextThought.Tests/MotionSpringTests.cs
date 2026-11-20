using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D5: the Mac five motion tokens are ported as real damped springs (DesignSystem.swift
/// <c>enum Motion</c>), not cubic easings. The analytic step response is checked against an
/// independent numerical integration of the same mass-spring equation, the overshoot against
/// the closed-form first-peak value, and the Reduce-motion path against "no animation".
/// Avalonia rendering is deliberately not exercised here.
/// </summary>
public sealed class MotionSpringTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Motion.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    // ---- Mac token values -------------------------------------------------------------

    [Fact]
    public void MacMotionTokensMatchDesignSystemSwift()
    {
        Assert.Equal(0.14, MotionTokens.HoverSeconds);
        Assert.Equal(new CubicBezierCurve(0.2, 0.8, 0.2, 1), MotionTokens.HoverCurve);

        Assert.Equal(new SpringSpec(0.32, 0.9), MotionTokens.Expand); // Motion.expand
        Assert.Equal(new SpringSpec(0.46, 0.9), MotionTokens.Phase);  // Motion.phase
        Assert.Equal(new SpringSpec(0.28, 0.78), MotionTokens.Snappy); // Motion.snappy
        Assert.Equal(new SpringSpec(0.5, 0.82), MotionTokens.Morph);  // Motion.morph

        foreach (var kind in new[] { MotionKind.Expand, MotionKind.Phase, MotionKind.Snappy, MotionKind.Morph })
        {
            var curve = MotionTokens.Curve(kind);
            Assert.True(curve.IsSpring);
            Assert.Null(curve.Bezier);
        }
        Assert.False(MotionTokens.Curve(MotionKind.Hover).IsSpring);
    }

    [Fact]
    public void SwiftSpringDefinitionUsesMassOneStiffnessAndCriticalDamping()
    {
        foreach (var spring in new[] { MotionTokens.Expand, MotionTokens.Phase, MotionTokens.Snappy, MotionTokens.Morph })
        {
            var omega = 2 * Math.PI / spring.Response;
            Assert.Equal(omega * omega, spring.Stiffness, 12);          // (2π/response)²
            Assert.Equal(omega, spring.AngularFrequency, 12);
            Assert.Equal(2 * spring.DampingFraction * omega, spring.Damping, 12); // 2ζ√stiffness
            Assert.Equal(omega * Math.Sqrt(1 - spring.DampingFraction * spring.DampingFraction), spring.DampedFrequency, 12);
        }
    }

    // ---- analytic solution vs an independently integrated ODE -------------------------

    /// <summary>RK4 integration of x'' = −ω₀²x − 2ζω₀x' from rest at 0 towards 1.</summary>
    private static double IntegrateStep(SpringSpec spring, double seconds, double h = 2e-5)
    {
        var omega = spring.AngularFrequency;
        var zeta = spring.DampingFraction;
        double x = 0, v = 0;
        var steps = (int)Math.Ceiling(seconds / h);
        for (var i = 0; i < steps; i++)
        {
            double Accel(double position, double velocity) => -omega * omega * (position - 1) - 2 * zeta * omega * velocity;
            var k1x = v;
            var k1v = Accel(x, v);
            var k2x = v + 0.5 * h * k1v;
            var k2v = Accel(x + 0.5 * h * k1x, v + 0.5 * h * k1v);
            var k3x = v + 0.5 * h * k2v;
            var k3v = Accel(x + 0.5 * h * k2x, v + 0.5 * h * k2v);
            var k4x = v + h * k3v;
            var k4v = Accel(x + h * k3x, v + h * k3v);
            x += h / 6 * (k1x + 2 * k2x + 2 * k3x + k4x);
            v += h / 6 * (k1v + 2 * k2v + 2 * k3v + k4v);
        }
        return x;
    }

    [Theory]
    [InlineData(0.32, 0.9)]
    [InlineData(0.46, 0.9)]
    [InlineData(0.28, 0.78)]
    [InlineData(0.5, 0.82)]
    public void AnalyticSpringSamplesMatchNumericalIntegration(double response, double dampingFraction)
    {
        var spring = new SpringSpec(response, dampingFraction);
        foreach (var seconds in new[] { 0.02, 0.05, 0.1, 0.2, 0.32, 0.5, 0.75, 1.0 })
        {
            var analytic = spring.Step(seconds);
            var numeric = IntegrateStep(spring, seconds);
            Assert.InRange(Math.Abs(analytic - numeric), 0, 5e-5);
        }
    }

    [Fact]
    public void SpringVelocityMatchesTheDerivativeOfTheStepResponse()
    {
        foreach (var spring in new[] { MotionTokens.Expand, MotionTokens.Phase, MotionTokens.Snappy, MotionTokens.Morph })
        {
            foreach (var seconds in new[] { 0.03, 0.1, 0.25, 0.4, 0.6 })
            {
                const double h = 1e-6;
                var numeric = (spring.Step(seconds + h) - spring.Step(seconds - h)) / (2 * h);
                Assert.InRange(Math.Abs(spring.Velocity(seconds) - numeric), 0, 1e-3);
            }
        }
    }

    // ---- under-damped overshoot -------------------------------------------------------

    [Fact]
    public void UnderDampedSpringsOvershootByTheAnalyticFirstPeak()
    {
        foreach (var spring in new[] { MotionTokens.Expand, MotionTokens.Phase, MotionTokens.Snappy, MotionTokens.Morph })
        {
            Assert.True(spring.DampingFraction < 1, "every Mac token is under-damped");
            // Sample the response densely: it must pass the target and settle back down.
            var peak = 0d;
            var peakAt = 0d;
            for (var t = 0d; t <= 2.0; t += 0.0005)
            {
                var value = spring.Step(t);
                if (value > peak) { peak = value; peakAt = t; }
            }
            Assert.True(peak > 1, $"spring response {response(spring)} should overshoot");
            Assert.InRange(peak, 1 + spring.PeakOvershoot - 1e-4, 1 + spring.PeakOvershoot + 1e-4);
            // The first peak happens a quarter damped period after the start.
            Assert.Equal(spring.FirstPeakSeconds, peakAt, 2);
            // Overshoot shrank by the end: the response has settled.
            Assert.True(spring.Step(2.0) is > 0.999 and < 1.001);
        }
        static string response(SpringSpec spring) => $"response={spring.Response} ζ={spring.DampingFraction}";
    }

    [Fact]
    public void CriticallyDampedSpringDoesNotOvershoot()
    {
        var spring = new SpringSpec(0.4, 1.0);
        var previous = 0d;
        for (var t = 0d; t <= 3; t += 0.001)
        {
            var value = spring.Step(t);
            Assert.True(value <= 1 + 1e-12, $"critically damped response overshoots at t={t}");
            Assert.True(value >= previous - 1e-12, "critically damped response must rise monotonically");
            previous = value;
        }
        Assert.Equal(0, spring.PeakOvershoot);
        Assert.InRange(spring.SettlingDuration(), 0, 3);
    }

    // ---- duration = settling time, not the response -----------------------------------

    [Fact]
    public void AnimationDurationIsTheSpringSettlingTimeNotTheResponse()
    {
        foreach (var kind in new[] { MotionKind.Expand, MotionKind.Phase, MotionKind.Snappy, MotionKind.Morph })
        {
            var spring = MotionTokens.Curve(kind).Spring!.Value;
            var duration = MotionTokens.Duration(kind).TotalSeconds;
            Assert.True(duration > spring.Response * 1.1, $"{kind} duration {duration} must exceed response {spring.Response}");
            Assert.True(duration < 2.0, $"{kind} duration {duration} should stay short");
            Assert.InRange(Math.Abs(1 - spring.Step(duration)), 0, 0.0015);
            Assert.InRange(Math.Abs(1 - spring.Step(duration + 0.05)), 0, 0.001);
        }
        Assert.Equal(TimeSpan.FromSeconds(0.14), MotionTokens.Duration(MotionKind.Hover));
    }

    // ---- hover is a timing curve, not a spring ---------------------------------------

    [Fact]
    public void HoverIsTheMacTimingCurveAndDoesNotOvershoot()
    {
        var curve = MotionTokens.HoverCurve;
        Assert.Equal(0, curve.Ease(0));
        Assert.Equal(1, curve.Ease(1));
        var previous = 0d;
        for (var p = 0d; p <= 1; p += 0.01)
        {
            var value = curve.Ease(p);
            Assert.True(value >= previous - 1e-9, "timing curve must be monotone");
            Assert.InRange(value, 0, 1);
            previous = value;
        }
        // (0.2, 0.8, 0.2, 1) reaches the target quickly: more than half-way by the middle.
        Assert.True(curve.Ease(0.5) > 0.75);
        Assert.True(curve.Ease(0.25) > 0.5);
        Assert.Null(MotionTokens.Curve(MotionKind.Hover).Spring);
    }

    // ---- system "show animations" off => nothing moves --------------------------------

    [Fact]
    public void ReduceMotionJumpsToFinalStateWithoutAnimating()
    {
        foreach (var kind in Enum.GetValues<MotionKind>())
        {
            Assert.False(MotionPlan.IsAnimated(reduceMotion: true));
            Assert.Equal(TimeSpan.Zero, MotionPlan.Duration(kind, reduceMotion: true));
            Assert.Equal(1, MotionPlan.Progress(kind, TimeSpan.Zero, reduceMotion: true));
            Assert.Equal(1, MotionPlan.Progress(kind, TimeSpan.FromSeconds(0.05), reduceMotion: true));
        }
        // With animations on, progress still starts at zero and ends at one.
        foreach (var kind in new[] { MotionKind.Expand, MotionKind.Phase, MotionKind.Snappy, MotionKind.Morph })
        {
            Assert.True(MotionPlan.IsAnimated(reduceMotion: false));
            Assert.Equal(0, MotionPlan.Progress(kind, TimeSpan.Zero, reduceMotion: false));
            Assert.Equal(1, MotionPlan.Progress(kind, MotionTokens.Duration(kind), reduceMotion: false));
            Assert.Equal(0, MotionPlan.EasedProgress(kind, 0));
            Assert.Equal(1, MotionPlan.EasedProgress(kind, 1));
        }
    }

    [Fact]
    public void FrameByFrameProgressMatchesTheSpringAndOvershootsThenSettles()
    {
        var kind = MotionKind.Morph; // the cue island width morph
        var duration = MotionTokens.Duration(kind);
        var spring = MotionTokens.Curve(kind).Spring!.Value;
        var overshoot = false;
        for (var frame = 0; frame < 60; frame++)
        {
            var elapsed = duration * frame / 60.0;
            var progress = MotionPlan.Progress(kind, elapsed, reduceMotion: false);
            Assert.Equal(spring.Step(elapsed.TotalSeconds), progress, 10);
            if (progress > 1) overshoot = true; // a real spring passes the target before settling
        }
        Assert.True(overshoot, "the morph spring must visibly overshoot its target width");
        Assert.Equal(1, MotionPlan.Progress(kind, duration, reduceMotion: false));
        Assert.Equal(1, MotionPlan.Progress(kind, duration + TimeSpan.FromSeconds(1), reduceMotion: false));
    }

    // ---- the desktop shell actually uses the shared model -----------------------------

    [Fact]
    public void DesktopMotionUsesRealSpringsAndHonoursReduceMotion()
    {
        var root = RepoRoot();
        var motion = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Motion.cs"));
        Assert.DoesNotContain("CubicEase", motion);
        Assert.Contains("MotionTokens", motion);
        Assert.Contains("MotionPlan", motion);
        Assert.Contains("Reduce", motion);
        Assert.Contains("FadeIn", motion);
        Assert.Contains("FadeOut", motion);

        // Every animation site goes through Motion: page fade and the cue island morph.
        var shell = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("Motion.FadeIn()", shell);
        Assert.Contains("Motion.FadeOut()", shell);
        Assert.DoesNotContain("CubicEase", shell);

        var island = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs"));
        Assert.Contains("Motion.", island);
        Assert.DoesNotContain("omega * omega * (targetWidth", island); // no hand-rolled Euler spring left

        // The sidebar hover colour transition is the hover token, not an unrelated 280 ms fade.
        var controls = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("0:0:0.14", controls);
        Assert.DoesNotContain("0:0:0.28", controls);
    }
}
