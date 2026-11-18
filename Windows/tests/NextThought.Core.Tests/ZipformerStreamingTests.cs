using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the streaming transducer model descriptions and runtime errors.</summary>
public sealed class ZipformerStreamingTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("nt-zipformer-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { }
    }

    [Fact]
    public void FileNamesAndRateMatchTheModels()
    {
        Assert.Equal("encoder-epoch-99-avg-1.int8.onnx", ZipformerModelFiles.Encoder);
        Assert.Equal("decoder-epoch-99-avg-1.onnx", ZipformerModelFiles.Decoder);
        Assert.Equal("joiner-epoch-99-avg-1.int8.onnx", ZipformerModelFiles.Joiner);
        Assert.Equal("tokens.txt", ZipformerModelFiles.Tokens);
        Assert.Equal(16000, ZipformerModelFiles.SampleRate);
    }

    [Fact]
    public void LegacyBilingualSetsCjkBpeAndZipformerType()
    {
        var model = OnlineTransducerModel.LegacyBilingual(directory);
        Assert.Equal("cjkchar+bpe", model.ModelingUnit);
        Assert.Equal("zipformer", model.ModelType);
        Assert.Equal("bpe.vocab", model.BpeVocabulary);
        Assert.Equal(Path.Combine(directory, "tokens.txt"), model.Path(model.Tokens));
    }

    [Fact]
    public void XasrModelsUseTheirOwnFileNames()
    {
        var online = OnlineTransducerModel.XasrStreaming(directory);
        Assert.Equal("encoder.int8.onnx", online.Encoder);
        Assert.Equal("decoder.onnx", online.Decoder);
        Assert.Equal("joiner.int8.onnx", online.Joiner);
        Assert.Equal("", online.ModelType);
        var offline = OfflineTransducerModel.XasrOffline(directory);
        Assert.Equal(ZipformerModelFiles.Encoder, offline.Encoder);
        Assert.Equal("bpe", offline.ModelingUnit);
    }

    [Fact]
    public void RequiredFilesAndVocabularyExistence()
    {
        var online = OnlineTransducerModel.XasrStreaming(directory);
        Assert.Equal(4, online.RequiredFiles.Count);
        Assert.False(online.HasVocabulary);
        File.WriteAllText(Path.Combine(directory, "bpe.vocab"), "x");
        Assert.True(online.HasVocabulary);
        Assert.False((online with { BpeVocabulary = null }).HasVocabulary);
    }

    [Fact]
    public void HotwordsAreSplitTrimmedAndWritten()
    {
        var words = ZipformerHotwords.Clean(" Kubernetes， 开源 、\nGraphQL;; ");
        Assert.Equal(["Kubernetes", "开源", "GraphQL"], words);
        Assert.Empty(ZipformerHotwords.Clean(" ，、 "));
        Assert.Equal("", ZipformerHotwords.Write([]));
        var path = ZipformerHotwords.Write(words);
        try
        {
            Assert.Equal("Kubernetes\n开源\nGraphQL", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HypothesisCarriesTiming()
    {
        var start = DateTimeOffset.UtcNow;
        var hypothesis = new ZipformerHypothesis("文本", true, start, start.AddSeconds(2), 0.3, 1.8);
        Assert.True(hypothesis.IsFinal);
        Assert.Equal(0.3, hypothesis.InferenceSeconds);
        Assert.Equal(1.8, hypothesis.AudioSeconds);
        Assert.Equal(start.AddSeconds(2), hypothesis.EndedAt);
    }

    [Theory]
    [InlineData(ZipformerRuntimeErrorKind.MissingModelFile, "encoder.onnx")]
    [InlineData(ZipformerRuntimeErrorKind.MissingRecognizer, null)]
    public void ErrorsMatchTheMacWording(ZipformerRuntimeErrorKind kind, string? file)
    {
        var error = new ZipformerRuntimeError(kind, file);
        if (kind == ZipformerRuntimeErrorKind.MissingModelFile) Assert.Contains("encoder.onnx", error.Message);
        else Assert.Equal("Zipformer 运行时尚未加载。", error.Message);
    }

    [Fact]
    public void EveryErrorKindHasAMessage()
    {
        foreach (var kind in Enum.GetValues<ZipformerRuntimeErrorKind>())
            Assert.NotEmpty(new ZipformerRuntimeError(kind).Message);
    }
}
