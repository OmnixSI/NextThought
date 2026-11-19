using System.Globalization;
using System.Text;

namespace NextThought.Core;

/// <summary>A recognised line with its time span (for matching echo against what was said then).</summary>
public sealed record TimedLine(string Text, DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Without headphones the other side's voice comes out of the speakers and is picked up again by
/// the microphone. The two channels are recognized independently, so the echo would show up as
/// "我" saying the interviewer's words — and make the app think the user is talking.
///
/// <see cref="CrossTalkMonitor"/> compares the loudness envelopes of both channels: a microphone
/// signal that follows the system-audio envelope (with a short acoustic/driver delay) is echo; a
/// microphone signal with its own rhythm is the user. Ported from EchoSuppression.swift.
/// </summary>
public sealed class CrossTalkMonitor
{
    public sealed class Config
    {
        /// <summary>Envelope frame length in samples (20 ms at 16 kHz).</summary>
        public int FrameSamples { get; set; } = 320;
        /// <summary>Analysis window (frames) for the correlation.</summary>
        public int WindowFrames { get; set; } = 80;
        /// <summary>Lags searched, in frames: the system capture can arrive before or after the mic.</summary>
        public int MinimumLag { get; set; } = -15;
        public int MaximumLag { get; set; } = 25;
        /// <summary>Correlation above which the microphone is treated as echo.</summary>
        public double EchoCorrelation { get; set; } = 0.7;
        /// <summary>Microphone frames below this RMS are silence.</summary>
        public double SpeechRms { get; set; } = 0.006;
        /// <summary>System audio below this RMS cannot produce echo.</summary>
        public double SystemActiveRms { get; set; } = 0.004;
    }

    private readonly Dictionary<int, double> systemFrames = [];
    private readonly Dictionary<int, double> microphoneFrames = [];
    private List<short> systemRemainder = [];
    private List<short> microphoneRemainder = [];
    private int? systemFrameCursor;
    private int? microphoneFrameCursor;

    public CrossTalkMonitor(Config? config = null) => Options = config ?? new Config();

    public Config Options { get; }
    public double LastCorrelation { get; private set; }
    public bool MicrophoneIsEcho { get; private set; }
    public bool MicrophoneHasSpeech { get; private set; }

    public void Reset()
    {
        systemFrames.Clear();
        microphoneFrames.Clear();
        systemRemainder = [];
        microphoneRemainder = [];
        systemFrameCursor = null;
        microphoneFrameCursor = null;
        LastCorrelation = 0;
        MicrophoneIsEcho = false;
        MicrophoneHasSpeech = false;
    }

    /// <summary><paramref name="time"/> is the arrival time (seconds, any monotonic clock) of the chunk's last sample.</summary>
    public void AppendSystem(IReadOnlyList<short> samples, double time) => Append(samples, time, true);

    /// <summary>Returns whether the microphone currently looks like an echo of the system audio.</summary>
    public bool AppendMicrophone(IReadOnlyList<short> samples, double time)
    {
        Append(samples, time, false);
        Evaluate();
        return MicrophoneIsEcho;
    }

    private void Append(IReadOnlyList<short> samples, double time, bool system)
    {
        var remainder = system ? systemRemainder : microphoneRemainder;
        var buffer = new List<short>(remainder.Count + samples.Count);
        buffer.AddRange(remainder);
        buffer.AddRange(samples);
        var frameCount = buffer.Count / Options.FrameSamples;
        var frameDuration = Options.FrameSamples / 16_000.0;
        // Frame index of the newest complete frame on a shared 20 ms grid.
        var newest = (int)Math.Floor(time / frameDuration);
        var cursor = system ? systemFrameCursor : microphoneFrameCursor;
        if (cursor is { } known && newest <= known) newest = known + frameCount;
        for (var frame = 0; frame < frameCount; frame++)
        {
            double total = 0;
            for (var i = frame * Options.FrameSamples; i < (frame + 1) * Options.FrameSamples; i++)
            {
                var value = buffer[i] / 32768.0;
                total += value * value;
            }
            var rms = Math.Sqrt(total / Options.FrameSamples);
            var index = newest - (frameCount - 1 - frame);
            if (system) systemFrames[index] = rms; else microphoneFrames[index] = rms;
        }
        buffer.RemoveRange(0, frameCount * Options.FrameSamples);
        if (system) { systemRemainder = buffer; systemFrameCursor = newest; }
        else { microphoneRemainder = buffer; microphoneFrameCursor = newest; }

        // Keep ~4 s of history.
        var horizon = newest - 200;
        var store = system ? systemFrames : microphoneFrames;
        foreach (var key in store.Keys.Where(key => key <= horizon).ToArray()) store.Remove(key);
    }

    private void Evaluate()
    {
        if (microphoneFrameCursor is not { } end) return;
        var mic = new double[Options.WindowFrames];
        for (var index = 0; index < Options.WindowFrames; index++)
        {
            var frame = end - Options.WindowFrames + 1 + index;
            mic[index] = microphoneFrames.TryGetValue(frame, out var value) ? value : 0;
        }
        MicrophoneHasSpeech = mic.Skip(Math.Max(0, mic.Length - 10)).Any(value => value >= Options.SpeechRms);
        if (!MicrophoneHasSpeech)
        {
            MicrophoneIsEcho = false;
            LastCorrelation = 0;
            return;
        }

        var best = 0.0;
        for (var lag = Options.MinimumLag; lag <= Options.MaximumLag; lag++)
        {
            var system = new double[Options.WindowFrames];
            var active = false;
            for (var index = 0; index < Options.WindowFrames; index++)
            {
                var frame = end - Options.WindowFrames + 1 + index - lag;
                system[index] = systemFrames.TryGetValue(frame, out var value) ? value : 0;
                if (system[index] >= Options.SystemActiveRms) active = true;
            }
            if (!active) continue;
            best = Math.Max(best, Correlation(Envelope(mic), Envelope(system)));
        }
        LastCorrelation = best;
        MicrophoneIsEcho = best >= Options.EchoCorrelation;
    }

    /// <summary>Log-energy envelope; correlation of logs is level-independent and dominated by the syllable rhythm.</summary>
    public static double[] Envelope(double[] values) => values.Select(value => Math.Log10(Math.Max(value, 1e-4))).ToArray();

    public static double Correlation(double[] a, double[] b)
    {
        if (a.Length != b.Length || a.Length <= 2) return 0;
        var meanA = a.Average();
        var meanB = b.Average();
        double numerator = 0, varianceA = 0, varianceB = 0;
        for (var index = 0; index < a.Length; index++)
        {
            var x = a[index] - meanA;
            var y = b[index] - meanB;
            numerator += x * y;
            varianceA += x * x;
            varianceB += y * y;
        }
        if (varianceA <= 1e-6 || varianceB <= 1e-6) return 0;
        return numerator / Math.Sqrt(varianceA * varianceB);
    }
}

/// <summary>
/// Text-level backstop: a microphone utterance whose words largely repeat what the system channel
/// said a moment ago is the speakers being re-recorded.
/// </summary>
public static class EchoTextFilter
{
    /// <summary>Share of the microphone text's units (CJK bigrams / words) found in one line.</summary>
    public static double Overlap(string microphoneText, string line)
    {
        var mic = Units(microphoneText);
        if (mic.Count < 3) return 0;
        var other = Units(line).ToHashSet(StringComparer.Ordinal);
        if (other.Count == 0) return 0;
        return mic.Count(unit => other.Contains(unit)) / (double)mic.Count;
    }

    /// <summary>
    /// Matches against each line on its own. A union of everything said recently matched ordinary
    /// replies, which reuse the question's words.
    /// </summary>
    public static bool IsLikelyEcho(string microphoneText, IReadOnlyList<string> recentSystemTexts, double threshold = 0.55)
        => (recentSystemTexts.Count == 0 ? 0 : recentSystemTexts.Max(line => Overlap(microphoneText, line))) >= threshold;

    /// <summary>
    /// A finished microphone utterance is speaker echo when it closely repeats one line the other
    /// side said at about the same time (≥ 80 %), or partly repeats it (≥ 50 %) while the audio
    /// monitor heard the speakers in the microphone.
    /// </summary>
    public static bool IsEcho(string microphoneText, DateTimeOffset start, DateTimeOffset end,
        IReadOnlyList<TimedLine> systemLines, double acousticEchoFraction)
    {
        var nearby = systemLines.Where(line =>
            line.End >= start.AddSeconds(-2.5) && line.Start <= end.AddSeconds(2.5)).ToList();
        if (nearby.Count == 0) return false;
        var best = nearby.Max(line => Overlap(microphoneText, line.Text));
        if (best >= 0.8) return true;
        return best >= 0.5 && acousticEchoFraction >= 0.4;
    }

    /// <summary>Character bigrams for CJK, lowercase words for everything else.</summary>
    public static List<string> Units(string text)
    {
        var result = new List<string>();
        var cjk = new List<string>();
        var word = "";
        void FlushCjk()
        {
            if (cjk.Count == 1) result.Add(cjk[0]);
            if (cjk.Count > 1)
            {
                for (var index = 0; index < cjk.Count - 1; index++)
                {
                    // CJK code points are BMP, so a string element is one character.
                    result.Add(cjk[index] + cjk[index + 1]);
                }
            }
            cjk.Clear();
        }
        void FlushWord()
        {
            if (word.Length > 0) result.Add(word);
            word = "";
        }
        foreach (var element in Elements(text.ToLowerInvariant()))
        {
            var rune = FirstRune(element);
            if (rune.Value is >= 0x3400 and <= 0x9FFF) { FlushWord(); cjk.Add(element); }
            else if (Rune.IsLetter(rune) || Rune.IsNumber(rune)) { FlushCjk(); word += element; }
            else { FlushCjk(); FlushWord(); }
        }
        FlushCjk();
        FlushWord();
        return result;
    }

    private static List<string> Elements(string text)
    {
        var elements = new List<string>(text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            elements.Add(text[starts[i]..end]);
        }
        return elements;
    }

    private static Rune FirstRune(string element)
    {
        foreach (var rune in element.EnumerateRunes()) return rune;
        return Rune.ReplacementChar;
    }
}
