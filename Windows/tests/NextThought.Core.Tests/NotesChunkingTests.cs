using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B9: long meetings are split into parts and merged, never truncated.</summary>
public sealed class NotesChunkingTests
{
    [Fact]
    public void ChunksSplitOnLineBoundariesAndKeepEveryLine()
    {
        const string text = "line one\nline two\nline three";
        var chunks = NotesPrompt.Chunks(text, 12);
        Assert.True(chunks.Count > 1);
        Assert.Equal(text.Replace("\n", ""), string.Concat(chunks).Replace("\n", ""));
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 12));
    }

    [Fact]
    public void MergeUserNumbersEveryPartInOrder()
    {
        var merged = NotesPrompt.MergeUser("header", ["{\"a\":1}", "{\"b\":2}"]);
        Assert.StartsWith("header\n下面是按时间顺序分段整理出的 JSON。", merged);
        Assert.Contains("### 第 1 部分\n{\"a\":1}", merged);
        Assert.Contains("### 第 2 部分\n{\"b\":2}", merged);
    }
}
