using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the byte-level BPE tokenizer with a small synthetic vocabulary.</summary>
public sealed class ByteLevelBPETokenizerTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("nt-bpe-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { }
    }

    private string Tokenizer(string json, string name = "tokenizer.json")
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    private const string SimpleJson = """
        {"model":{"type":"BPE","vocab":{"a":0,"b":1,"ab":2},"merges":[["a","b"]]},
         "added_tokens":[{"content":"<|endoftext|>","id":3}]}
        """;

    [Fact]
    public void EncodesMergesAndAppendsEndOfText()
    {
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(SimpleJson),
            new ByteLevelBPETokenizer.Configuration(3, SplitPattern: @"\S+"));
        Assert.Equal([2, 3], tokenizer.Encode("ab"));
        Assert.Equal(2, tokenizer.TokenId("ab"));
        Assert.Equal(3, tokenizer.TokenId("<|endoftext|>"));
        Assert.Null(tokenizer.TokenId("missing"));
        Assert.Equal("ab", tokenizer.Decode([2]));
    }

    [Fact]
    public void SplitsWordsAndKeepsAddedTokensDecodable()
    {
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(SimpleJson),
            new ByteLevelBPETokenizer.Configuration(3, SplitPattern: @"\S+"));
        Assert.Equal([0, 1, 3], tokenizer.Encode("a b"));
        Assert.Equal("ab<|endoftext|>", tokenizer.Decode([0, 1, 3]));
    }

    [Fact]
    public void EndOfTextIsOptional()
    {
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(SimpleJson),
            new ByteLevelBPETokenizer.Configuration(3, AppendEndOfText: false, SplitPattern: @"\S+"));
        Assert.Equal([2], tokenizer.Encode("ab"));
    }

    [Fact]
    public void MergesCanBeGivenAsStrings()
    {
        const string json = """
            {"model":{"type":"BPE","vocab":{"a":0,"b":1,"ab":2},"merges":["a b"]},
             "added_tokens":[{"content":"<|endoftext|>","id":3}]}
            """;
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(json, "strings.json"),
            new ByteLevelBPETokenizer.Configuration(3, AppendEndOfText: false, SplitPattern: @"\S+"));
        Assert.Equal([2], tokenizer.Encode("ab"));
    }

    [Fact]
    public void UnicodeBytesRoundTrip()
    {
        const string json = """
            {"model":{"type":"BPE","vocab":{"ä":0,"¸":1,"\u0143":2},"merges":[]},
             "added_tokens":[]}
            """;
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(json, "unicode.json"),
            new ByteLevelBPETokenizer.Configuration(3, AppendEndOfText: false, SplitPattern: @"\S+"));
        var ids = tokenizer.Encode("中");
        Assert.Equal([0, 1, 2], ids);
        Assert.Equal("中", tokenizer.Decode(ids));
    }

    [Fact]
    public void NfcCompositionHappensBeforeSplitting()
    {
        const string json = """
            {"model":{"type":"BPE","vocab":{"Ã":0,"©":1,"e":2,"́":3},"merges":[]},
             "added_tokens":[]}
            """;
        var tokenizer = new ByteLevelBPETokenizer(Tokenizer(json, "nfc.json"),
            new ByteLevelBPETokenizer.Configuration(3, AppendEndOfText: false, SplitPattern: @"\S+"));
        var composed = tokenizer.Encode("é");
        var decomposed = tokenizer.Encode("e\u0301");
        Assert.Equal(composed, decomposed);
        Assert.Equal("é", tokenizer.Decode(composed));
    }

    [Fact]
    public void InvalidFilesAreRejected()
    {
        Assert.Throws<ByteLevelBPETokenizer.TokenizerException>(() =>
            new ByteLevelBPETokenizer(Tokenizer("""{"model":{"type":"WordPiece","vocab":{}}}""", "wordpiece.json")));
        Assert.Throws<ByteLevelBPETokenizer.TokenizerException>(() =>
            new ByteLevelBPETokenizer(Tokenizer("""{"nope":true}""", "missing.json")));
        Assert.Throws<ByteLevelBPETokenizer.TokenizerException>(() =>
            new ByteLevelBPETokenizer(Tokenizer("not json", "broken.json")));
        Assert.Throws<ByteLevelBPETokenizer.TokenizerException>(() =>
            new ByteLevelBPETokenizer(Path.Combine(directory, "absent.json")));
    }

    [Fact]
    public void DefaultConfigurationMatchesTheQwenFamily()
    {
        Assert.Equal(151_643, new ByteLevelBPETokenizer.Configuration().EndOfTextToken);
        Assert.True(new ByteLevelBPETokenizer.Configuration().AppendEndOfText);
        Assert.Equal(BpeTokenization.DefaultSplitPattern, new ByteLevelBPETokenizer.Configuration().SplitPattern);
        Assert.Contains("\\p{L}+", BpeTokenization.DefaultSplitPattern);
    }
}
