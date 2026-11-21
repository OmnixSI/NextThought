namespace NextThought.Core;

public static class ZipformerModelFiles
{
    public const string Encoder = "encoder-epoch-99-avg-1.int8.onnx";
    public const string Decoder = "decoder-epoch-99-avg-1.onnx";
    public const string Joiner = "joiner-epoch-99-avg-1.int8.onnx";
    public const string Tokens = "tokens.txt";
    public const string BpeVocabulary = "bpe.vocab";
    public const int SampleRate = 16_000;
}

/// <summary>Files and decoding options of one streaming transducer model.</summary>
public sealed record OnlineTransducerModel
{
    public string Directory { get; init; } = "";
    public string Encoder { get; init; } = ZipformerModelFiles.Encoder;
    public string Decoder { get; init; } = ZipformerModelFiles.Decoder;
    public string Joiner { get; init; } = ZipformerModelFiles.Joiner;
    public string Tokens { get; init; } = ZipformerModelFiles.Tokens;

    /// <summary>SentencePiece vocabulary (piece&lt;TAB&gt;score); required for hotwords.</summary>
    public string? BpeVocabulary { get; init; }

    public string ModelingUnit { get; init; } = "bpe";

    /// <summary>Empty lets sherpa-onnx read the model type from metadata.</summary>
    public string ModelType { get; init; } = "";

    /// <summary>sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20 (no punctuation).</summary>
    public static OnlineTransducerModel LegacyBilingual(string directory) => new()
    {
        Directory = directory,
        BpeVocabulary = ZipformerModelFiles.BpeVocabulary,
        ModelingUnit = "cjkchar+bpe",
        ModelType = "zipformer"
    };

    /// <summary>X-ASR zh-en streaming Zipformer: punctuation, casing, code-switching.</summary>
    public static OnlineTransducerModel XasrStreaming(string directory) => new()
    {
        Directory = directory,
        Encoder = "encoder.int8.onnx",
        Decoder = "decoder.onnx",
        Joiner = "joiner.int8.onnx",
        BpeVocabulary = "bpe.vocab"
    };

    public IReadOnlyList<string> RequiredFiles => [Encoder, Decoder, Joiner, Tokens];

    public string Path(string name) => System.IO.Path.Combine(Directory, name);

    public bool HasVocabulary => BpeVocabulary is { } vocabulary && File.Exists(Path(vocabulary));
}

/// <summary>Files of one non-streaming transducer used to re-decode finished utterances.</summary>
public sealed record OfflineTransducerModel
{
    public string Directory { get; init; } = "";
    public string Encoder { get; init; } = ZipformerModelFiles.Encoder;
    public string Decoder { get; init; } = ZipformerModelFiles.Decoder;
    public string Joiner { get; init; } = ZipformerModelFiles.Joiner;
    public string Tokens { get; init; } = ZipformerModelFiles.Tokens;
    public string? BpeVocabulary { get; init; }
    public string ModelingUnit { get; init; } = "bpe";

    /// <summary>X-ASR zh-en offline Zipformer: same tokenizer as the streaming model.</summary>
    public static OfflineTransducerModel XasrOffline(string directory) => new()
    {
        Directory = directory,
        BpeVocabulary = "bpe.vocab"
    };

    public IReadOnlyList<string> RequiredFiles => [Encoder, Decoder, Joiner, Tokens];

    public string Path(string name) => System.IO.Path.Combine(Directory, name);

    public bool HasVocabulary => BpeVocabulary is { } vocabulary && File.Exists(Path(vocabulary));
}

public static class ZipformerHotwords
{
    private static readonly char[] Separators = [',', '，', '、', '\n', '\r', ';', '；'];

    public static IReadOnlyList<string> Clean(string hotwords) =>
        hotwords.Split(Separators).Select(word => word.Trim()).Where(word => word.Length > 0).ToList();

    /// <summary>Writes a temporary hotwords file; the caller deletes it after creating the recognizer.</summary>
    public static string Write(IReadOnlyList<string> words)
    {
        if (words.Count == 0) return "";
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nextthought-hotwords-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, string.Join("\n", words));
        return path;
    }
}

public sealed record ZipformerHypothesis(
    string Text,
    bool IsFinal,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    double InferenceSeconds,
    double AudioSeconds);

public enum ZipformerRuntimeErrorKind
{
    MissingModelFile,
    RecognizerCreationFailed,
    StreamCreationFailed,
    MissingRecognizer,
    ActiveStreams
}

/// <summary>Recognizer failures with the same user-facing wording as the Mac runtime.</summary>
public sealed record ZipformerRuntimeError(ZipformerRuntimeErrorKind Kind, string? FileName = null)
{
    public string Message => Kind switch
    {
        ZipformerRuntimeErrorKind.MissingModelFile => $"Zipformer 模型文件缺失：{FileName}",
        ZipformerRuntimeErrorKind.RecognizerCreationFailed => "Sherpa-ONNX 无法加载 Zipformer 模型，请重新下载完整模型文件组。",
        ZipformerRuntimeErrorKind.StreamCreationFailed => "Sherpa-ONNX 无法创建在线识别流。",
        ZipformerRuntimeErrorKind.MissingRecognizer => "Zipformer 运行时尚未加载。",
        _ => "Zipformer 正在识别，无法重新加载模型。"
    };
}
