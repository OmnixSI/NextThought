using System.Text.Json;
using System.Text.Json.Serialization;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of ByteLevelBPETokenizerTests.swift. The shared fixtures were produced with the
/// reference implementation (Hugging Face <c>tokenizers</c>), so the port has to split and
/// merge exactly like the model expects.
/// </summary>
public sealed class ByteLevelBPETokenizerParityTests
{
    private sealed class Case
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("ids")]
        public int[] Ids { get; set; } = [];
    }

    private static readonly JsonSerializerOptions CaseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The fixtures live next to the Swift tests and are shared instead of copied.</summary>
    private static string FixturePath(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Tests", "NextThoughtCoreTests", "Fixtures", name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("找不到与 Swift 测试共用的夹具：" + name);
    }

    private static IReadOnlyList<Case> LoadCases() =>
        JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(FixturePath("byte-bpe-fixture-expected.json")), CaseOptions)!;

    [Fact]
    public void EncodesLikeTheReferenceImplementation()
    {
        var tokenizer = new ByteLevelBPETokenizer(FixturePath("byte-bpe-fixture.json"),
            new ByteLevelBPETokenizer.Configuration(-1, AppendEndOfText: false));
        foreach (var testCase in LoadCases())
            Assert.Equal(testCase.Ids, tokenizer.Encode(testCase.Text));
    }

    [Fact]
    public void DecodesBackToTheOriginalText()
    {
        var tokenizer = new ByteLevelBPETokenizer(FixturePath("byte-bpe-fixture.json"),
            new ByteLevelBPETokenizer.Configuration(-1, AppendEndOfText: false));
        foreach (var testCase in LoadCases())
            Assert.Equal(testCase.Text, tokenizer.Decode(testCase.Ids));
    }

    /// <summary>
    /// The end-of-text token is the token the embedding model pools its vector from, so it has
    /// to be appended to every text.
    /// </summary>
    [Fact]
    public void AppendsTheEndOfTextToken()
    {
        var tokenizer = new ByteLevelBPETokenizer(FixturePath("byte-bpe-fixture.json"),
            new ByteLevelBPETokenizer.Configuration(42, AppendEndOfText: true));
        Assert.Equal(42, tokenizer.Encode("hello").Last());
    }

    /// <summary>
    /// The tokenizer of the embedding model that ships with the app, checked against the ids the
    /// reference implementation produced. Like the Swift test this only runs when the model is
    /// installed; Windows ships the same Qwen3 embedding tokenizer under its own model directory.
    /// </summary>
    [Fact]
    public void InstalledEmbeddingTokenizerMatchesTheReference()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "NextThought", "models", "qwen3-embedding-0-6b-coreml", "tokenizer", "tokenizer.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NextThought", "models", "qwen3-embedding-0.6b-onnx-q8", "tokenizer.json")
        };
        var tokenizerPath = candidates.FirstOrDefault(File.Exists);
        if (tokenizerPath is null) return;

        var tokenizer = new ByteLevelBPETokenizer(tokenizerPath);
        var english = tokenizer.Encode("Tell me about the CAP theorem");
        Assert.Equal(151_643, english.Last());
        Assert.Equal([40451, 752, 911, 279, 26101, 57817], english.Take(6));
        var chinese = tokenizer.Encode("请介绍 Kubernetes 的调度器");
        Assert.Equal([14880, 100157, 66374], chinese.Take(3));
    }
}
