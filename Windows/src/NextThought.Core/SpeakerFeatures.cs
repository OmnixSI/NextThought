using System.Numerics;

namespace NextThought.Core;

/// <summary>Kaldi-compatible 16 kHz Povey log-mel features used by the upstream voice model.</summary>
public static class SpeakerFeatures
{
    public const int Bins = 80;
    private static readonly double[] window = Enumerable.Range(0, 400).Select(i => Math.Pow(.5 - .5 * Math.Cos(2 * Math.PI * i / 399), .85)).ToArray();
    private static readonly double[,] banks = Banks();
    public static float[] Compute(short[] samples)
    {
        if (samples.Length < 400) return [];
        var frames = 1 + (samples.Length - 400) / 160; var output = new float[frames * Bins];
        var fft = new Complex[512]; var frame = new double[400];
        for (int f = 0; f < frames; f++)
        {
            double mean = 0; for (int i = 0; i < 400; i++) { frame[i] = samples[f * 160 + i]; mean += frame[i]; } mean /= 400;
            for (int i = 0; i < 400; i++) frame[i] -= mean;
            for (int i = 399; i > 0; i--) frame[i] -= .97 * frame[i - 1]; frame[0] *= .03;
            Array.Clear(fft); for (int i = 0; i < 400; i++) fft[i] = new(frame[i] * window[i], 0); Fft(fft);
            for (int b = 0; b < Bins; b++)
            {
                double energy = 0;
                for (int k = 0; k < 256; k++) energy += banks[b, k] * (fft[k].Real * fft[k].Real + fft[k].Imaginary * fft[k].Imaginary);
                output[f * Bins + b] = (float)Math.Log(Math.Max(1.1920928955078125e-7, energy));
            }
        }
        for (int b = 0; b < Bins; b++)
        { double mean = 0; for (int f = 0; f < frames; f++) mean += output[f * Bins + b]; mean /= frames; for (int f = 0; f < frames; f++) output[f * Bins + b] -= (float)mean; }
        return output;
    }
    private static double[,] Banks()
    {
        static double Mel(double frequency) => 1127 * Math.Log(1 + frequency / 700);
        var result = new double[Bins, 256]; var low = Mel(20); var step = (Mel(8000) - low) / (Bins + 1);
        for (int b = 0; b < Bins; b++) for (int k = 0; k < 256; k++)
        { var m = Mel(k * 16000.0 / 512); result[b, k] = Math.Max(0, Math.Min((m - low - b * step) / step, (low + (b + 2) * step - m) / step)); }
        return result;
    }
    private static void Fft(Complex[] values)
    {
        int n = values.Length, j = 0;
        for (int i = 1; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) (values[i], values[j]) = (values[j], values[i]); }
        for (int length = 2; length <= n; length <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (int start = 0; start < n; start += length)
            { var rotation = Complex.One; for (int i = 0; i < length / 2; i++) { var even = values[start + i]; var odd = rotation * values[start + i + length / 2]; values[start + i] = even + odd; values[start + i + length / 2] = even - odd; rotation *= step; } }
        }
    }
}

public sealed class VoiceGroups
{
    private readonly List<float[]> sums = [];
    public int VoiceCount => sums.Count;
    public int? Assign(float[] vector, double seconds)
    {
        if (vector.Length != 256 || vector.Any(v => !float.IsFinite(v)) || Math.Abs(vector.Sum(v => (double)v * v) - 1) > .02) return null;
        var candidates = sums.Select((sum, id) => (Id: id, Score: Similarity(vector, sum))).OrderByDescending(c => c.Score).ToArray();
        // Upstream uses 0.5; retain a separate uncertainty band before creating a new group.
        if (candidates.Length > 0 && candidates[0].Score >= .5 && (candidates.Length == 1 || candidates[0].Score - candidates[1].Score >= .05))
        { var target = sums[candidates[0].Id]; for (int i = 0; i < vector.Length; i++) target[i] += vector[i]; return candidates[0].Id; }
        if (seconds >= 2 && sums.Count < 16 && (candidates.Length == 0 || candidates[0].Score < .4)) { sums.Add((float[])vector.Clone()); return sums.Count - 1; }
        return null;
    }
    public static double Similarity(float[] unit, float[] sum)
    { double dot = 0, norm = 0; for (int i = 0; i < unit.Length; i++) { dot += (double)unit[i] * sum[i]; norm += (double)sum[i] * sum[i]; } return norm == 0 ? 0 : dot / Math.Sqrt(norm); }
}
