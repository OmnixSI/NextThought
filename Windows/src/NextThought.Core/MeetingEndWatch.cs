namespace NextThought.Core;

/// <summary>
/// Decides when a meeting nobody ended is over, so a forgotten one doesn't run on for hours and pull
/// the next meeting into its notes. Only one signal, on purpose: nothing was recognised for a while.
/// It counts recognised words, not sound, so background music doesn't keep a meeting alive. Pure:
/// the app calls <see cref="Heard"/>, <see cref="KeepGoing"/> and <see cref="Tick"/> once a second.
/// Ported from MeetingEndWatch.swift.
/// </summary>
public sealed class MeetingEndWatch
{
    public enum Mode { Prompt, End, Off }

    public enum Reason { Silence, NobodySpoke }

    public sealed record Config(Mode Mode = Mode.Prompt, double SilenceAfterSeconds = 600, double CountdownSeconds = 60)
    {
        public const int DefaultMinutes = 10;
        /// <summary>What the settings offer.</summary>
        public static readonly int[] MinuteChoices = [5, 10, 20, 30];

        /// <summary>From the two stored settings; anything unknown or missing falls back to the defaults.</summary>
        public static Config FromSettings(string? mode, int? minutes)
        {
            var value = minutes is { } m ? m > 0 ? Math.Min(m, 240) : DefaultMinutes : DefaultMinutes;
            return new Config(ParseMode(mode), value * 60);
        }

        public static Mode ParseMode(string? raw) => raw switch
        {
            "end" => Mode.End,
            "off" => Mode.Off,
            "prompt" => Mode.Prompt,
            _ => Mode.Prompt
        };

        public static string Raw(Mode mode) => mode switch
        {
            Mode.End => "end",
            Mode.Off => "off",
            _ => "prompt"
        };
    }

    private Config config;

    public Config Configuration { get => config; set => config = value; }
    private DateTimeOffset? pendingDeadline;
    private DateTimeOffset quietSince;

    public MeetingEndWatch(Config? config, DateTimeOffset start)
    {
        this.config = config ?? new Config();
        quietSince = start;
    }

    public Config CurrentConfig => config;
    public DateTimeOffset QuietSince => quietSince;
    public DateTimeOffset? PendingDeadline => pendingDeadline;

    /// <summary>Words were recognised (mine or theirs): the quiet starts over and a reminder is withdrawn.</summary>
    public void Heard(DateTimeOffset now)
    {
        quietSince = now;
        pendingDeadline = null;
    }

    /// <summary>"继续记录": withdraw the reminder and count the quiet from now.</summary>
    public void KeepGoing(DateTimeOffset now)
    {
        quietSince = now;
        pendingDeadline = null;
    }

    /// <summary>
    /// Call about once a second while the meeting runs. <paramref name="said"/>: the meeting has at
    /// least one line. <paramref name="paused"/>: the meeting is paused, which isn't counted.
    /// Returns why the meeting should end now, if it should. After returning a reason the quiet
    /// restarts, so a stop that didn't happen doesn't immediately fire again.
    /// </summary>
    public Reason? Tick(DateTimeOffset now, bool said, bool paused = false)
    {
        if (paused || config.Mode == Mode.Off)
        {
            pendingDeadline = null;
            quietSince = now;
            return null;
        }
        if (pendingDeadline is { } deadline)
        {
            if (now < deadline) return null;
            pendingDeadline = null;
            quietSince = now;
            return Reason.Silence;
        }
        if ((now - quietSince).TotalSeconds < config.SilenceAfterSeconds) return null;
        if (!said)
        {
            quietSince = now;
            return Reason.NobodySpoke;
        }
        switch (config.Mode)
        {
            case Mode.End:
                quietSince = now;
                return Reason.Silence;
            case Mode.Prompt:
                pendingDeadline = now.AddSeconds(config.CountdownSeconds);
                return null;
            default:
                return null;
        }
    }

    /// <summary>Seconds the reminder has left, if it is showing.</summary>
    public double? Remaining(DateTimeOffset now) => pendingDeadline is { } deadline
        ? Math.Max(0, (deadline - now).TotalSeconds)
        : null;

    /// <summary>The reminder was cancelled by the user.</summary>
    public void Cancel(DateTimeOffset now) => KeepGoing(now);

    /// <summary>
    /// Where an auto-ended meeting really ended: a little after the last words (not <paramref name="now"/>,
    /// which is minutes of silence later), but never before it started or after <paramref name="now"/>.
    /// </summary>
    public static DateTimeOffset EndTime(DateTimeOffset? lastSpeechEnd, DateTimeOffset startedAt, DateTimeOffset now, double padding = 5)
    {
        if (lastSpeechEnd is not { } last) return startedAt > now ? startedAt : now;
        var candidate = last.AddSeconds(padding);
        return candidate < startedAt ? startedAt : candidate > now ? now : candidate;
    }
}