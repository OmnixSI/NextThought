using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace NextThought.Platform;

public sealed class QwenEmbedder : IDisposable
{
    public const string PipelineVersion = "qwen3-nfc-bpe-eot-lasttoken-l2-v2-fit512";
    private readonly InferenceSession session;
    private readonly QwenTokenizer tokenizer;
    public string Runtime => OrtEnv.Instance().GetVersionString();
    public int TokenCount(string text) => tokenizer.Encode(text, int.MaxValue).Length;
    public QwenEmbedder(EmbeddingModelStore store)
    {
        tokenizer = new(Path.Combine(store.Root, "tokenizer.json"));
        using var options = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1, GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        session = new(Path.Combine(store.Root, "model.onnx"), options);
        if (!session.InputMetadata.ContainsKey("input_ids") || !session.InputMetadata.ContainsKey("attention_mask") || !session.OutputMetadata.ContainsKey("last_hidden_state")) throw new InvalidDataException("语义模型输入输出不匹配。");
    }
    public float[] Embed(string text, bool query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (query) text = $"Instruct: {EmbeddingModelStore.Manifest.QueryInstruction}\nQuery: {text}";
        if (TokenCount(text) > EmbeddingModelStore.Manifest.MaxTokens) throw new InvalidDataException("输入超过语义模型词元预算，需拆分资料或缩短问题。");
        var ids = tokenizer.Encode(text, EmbeddingModelStore.Manifest.MaxTokens);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, [1, ids.Length])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, ids.Length).ToArray(), [1, ids.Length]))
        };
        if (session.InputMetadata.ContainsKey("position_ids")) inputs.Add(NamedOnnxValue.CreateFromTensor("position_ids", new DenseTensor<long>(Enumerable.Range(0, ids.Length).Select(i => (long)i).ToArray(), [1, ids.Length])));
        foreach (var (name, metadata) in session.InputMetadata.Where(p => p.Key.StartsWith("past_key_values.", StringComparison.Ordinal)))
        {
            if (metadata.ElementType != typeof(float) || metadata.Dimensions.Length != 4) throw new InvalidDataException("不支持的 KV 缓存格式。");
            // Embedding runs the complete input once, without a generation history.
            inputs.Add(NamedOnnxValue.CreateFromTensor(name, new DenseTensor<float>(Array.Empty<float>(), [1, metadata.Dimensions[1], 0, metadata.Dimensions[3]])));
        }
        using var options = new RunOptions(); using var registration = ct.Register(() => options.Terminate = true);
        try
        {
            using var output = session.Run(inputs, ["last_hidden_state"], options);
            var tensor = output.First().AsTensor<float>();
            if (tensor.Dimensions.Length != 3 || tensor.Dimensions[0] != 1 || tensor.Dimensions[1] != ids.Length || tensor.Dimensions[2] != EmbeddingModelStore.Manifest.Dimension) throw new InvalidDataException("语义模型输出维度不匹配。");
            var vector = tensor.ToArray().AsSpan((ids.Length - 1) * EmbeddingModelStore.Manifest.Dimension, EmbeddingModelStore.Manifest.Dimension).ToArray();
            double power = vector.Sum(v => (double)v * v); if (!double.IsFinite(power) || power <= 0) throw new InvalidDataException("无效的语义向量。");
            var norm = Math.Sqrt(power); for (int i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
            ct.ThrowIfCancellationRequested(); return vector;
        }
        catch (OnnxRuntimeException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
    }
    public void Dispose() => session.Dispose();
}
