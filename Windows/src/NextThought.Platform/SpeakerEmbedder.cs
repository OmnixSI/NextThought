using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class SpeakerEmbedder : IDisposable
{
    public const string Sha256 = "7bb2f06e9df17cdf1ef14ee8a15ab08ed28e8d0ef5054ee135741560df2ec068";
    public const string ModelId = "wespeaker-resnet34-lm-7bb2f06e-fbank80-cmn-v1";
    private readonly InferenceSession session;
    public static string ModelFile(AppPaths paths) => Path.Combine(paths.Models, "wespeaker-resnet34-lm", "model.onnx");
    public SpeakerEmbedder(AppPaths paths)
    {
        var file = ModelFile(paths); using (var input = File.OpenRead(file)) if (Convert.ToHexStringLower(SHA256.HashData(input)) != Sha256) throw new InvalidDataException("声纹模型 SHA-256 不匹配。");
        using var options = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1 };
        session = new(file, options);
        if (!session.InputMetadata.ContainsKey("feats")) throw new InvalidDataException("声纹模型特征输入不匹配。");
    }
    public float[]? Embed(short[] samples, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); if (samples.Length < 8240) return null;
        if (samples.Length > 320000) samples = samples[^320000..];
        var power = samples.Sum(v => Math.Pow(v / 32768.0, 2)) / samples.Length; if (power < .000064) return null;
        var features = SpeakerFeatures.Compute(samples); var frames = features.Length / SpeakerFeatures.Bins;
        using var options = new RunOptions(); using var registration = ct.Register(() => options.Terminate = true);
        try
        {
            using var result = session.Run([NamedOnnxValue.CreateFromTensor("feats", new DenseTensor<float>(features, [1, frames, 80]))], session.OutputMetadata.Keys.ToArray(), options);
            var values = result.First().AsEnumerable<float>().ToArray(); if (values.Length != 256 || values.Any(v => !float.IsFinite(v))) return null;
            var norm = Math.Sqrt(values.Sum(v => (double)v * v)); if (norm <= 0) return null;
            for (int i = 0; i < values.Length; i++) values[i] /= (float)norm; return values;
        }
        catch (OnnxRuntimeException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
    }
    public void Dispose() => session.Dispose();
}
