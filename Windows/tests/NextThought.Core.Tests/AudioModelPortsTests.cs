using System.Security.Cryptography;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the full VAD port and LocalModelReuse.</summary>
public sealed class AudioModelPortsTests
{
    private static short[] Speech(int samples) => Enumerable.Repeat((short)1600, samples).ToArray();

    private static short[] Silence(int samples) => new short[samples];

    [Fact]
    public void SilenceProducesNoSegments()
    {
        var vad = new VoiceActivitySegmenter();
        for (var i = 0; i < 4; i++) Assert.Empty(vad.Append(Silence(16000)));
        Assert.Null(vad.Flush());
        Assert.False(vad.IsSpeaking);
    }

    [Fact]
    public void SpeechProducesAPartialThenAFinal()
    {
        var vad = new VoiceActivitySegmenter();
        Assert.Empty(vad.Append(Speech(16000)));
        var partial = Assert.Single(vad.Append(Speech(8000)));
        Assert.Equal(SpeechSegmentEvent.KindValue.Partial, partial.Kind);
        Assert.Equal(0, partial.StartSample);
        Assert.Equal(24000, partial.EndSample);

        var final = Assert.Single(vad.Append(Silence(11200)));
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, final.Kind);
        Assert.Equal(0, final.StartSample);
        Assert.Equal(35200, final.EndSample);
        Assert.Equal(35200, final.Samples.Length);
    }

    [Fact]
    public void PreRollKeepsTheStartOfTheUtterance()
    {
        var vad = new VoiceActivitySegmenter();
        Assert.Empty(vad.Append(Silence(4000)));
        Assert.Empty(vad.Append(Speech(16000)));
        // With the 4,000-sample pre-roll the first partial lands after 24,000 buffered samples.
        Assert.Single(vad.Append(Speech(4000)));
        var final = Assert.Single(vad.Append(Silence(11200)));
        Assert.Equal(0, final.StartSample);
        Assert.Equal(35200, final.Samples.Length);
    }

    [Fact]
    public void TwelveSecondLimitSplitsWithHalfSecondOverlap()
    {
        var vad = new VoiceActivitySegmenter();
        var events = vad.Append(Speech(12 * 16000));
        Assert.Equal(2, events.Count);
        Assert.Equal(SpeechSegmentEvent.KindValue.Partial, events[0].Kind);
        var final = events[^1];
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, final.Kind);
        Assert.Equal(192000, final.EndSample);

        // The overlap keeps the tail: continued speech starts the next segment 0.5 s earlier.
        Assert.Empty(vad.Append(Speech(16000)));
        var next = Assert.Single(vad.Append(Silence(11200)));
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, next.Kind);
        Assert.Equal(192000 - 8000, next.StartSample);
    }

    [Fact]
    public void SteadyBackgroundHumIsIgnored()
    {
        var vad = new VoiceActivitySegmenter();
        for (var i = 0; i < 10; i++) Assert.Empty(vad.Append(Enumerable.Repeat((short)65, 16000).ToArray()));
        Assert.False(vad.IsSpeaking);
    }

    [Fact]
    public void FlushClosesTheLastUtterance()
    {
        var vad = new VoiceActivitySegmenter();
        Assert.Empty(vad.Append(Speech(16000)));
        var final = vad.Flush();
        Assert.NotNull(final);
        Assert.Equal(SpeechSegmentEvent.KindValue.Final, final!.Kind);
        Assert.Equal(16000, final.Samples.Length);
        Assert.Null(vad.Flush());
    }

    [Fact]
    public void SearchFindsModelDirectoriesAndSkipsCaches()
    {
        var root = Directory.CreateTempSubdirectory("nt-reuse-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "modelA"));
            File.WriteAllBytes(Path.Combine(root, "modelA", "model.onnx"), [1, 2, 3]);
            Directory.CreateDirectory(Path.Combine(root, ".git", "models"));
            File.WriteAllBytes(Path.Combine(root, ".git", "models", "model.onnx"), [1, 2, 3]);
            var search = LocalModelReuse.Search([root], null, new HashSet<string> { "model.onnx", "models" });
            Assert.Contains(Path.Combine(root, "modelA"), search.Directories);
            Assert.DoesNotContain(search.Directories, directory => directory.Contains(Path.Combine(".git", "models")));
            Assert.False(search.Limited);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void SearchHonoursTheEntryLimit()
    {
        var root = Directory.CreateTempSubdirectory("nt-reuse-limit-").FullName;
        try
        {
            for (var index = 0; index < 10; index++) File.WriteAllText(Path.Combine(root, "f" + index + ".bin"), "x");
            var search = LocalModelReuse.Search([root], null, new HashSet<string> { "model.onnx" }, maximumEntries: 2);
            Assert.True(search.Limited);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void MatchesChecksSizeAndHash()
    {
        var directory = Directory.CreateTempSubdirectory("nt-reuse-match-").FullName;
        try
        {
            var path = Path.Combine(directory, "model.bin");
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.True(LocalModelReuse.Matches(path, new LocalModelFile("model.bin", 4, sha)));
            Assert.False(LocalModelReuse.Matches(path, new LocalModelFile("model.bin", 4, new string('0', 64))));
            Assert.False(LocalModelReuse.Matches(path, new LocalModelFile("model.bin", 5, sha)));
            Assert.False(LocalModelReuse.Matches(Path.Combine(directory, "missing.bin"), new LocalModelFile("x", 1, sha)));
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public void ReuseCopiesVerifiedFilesOnlyOnce()
    {
        var source = Directory.CreateTempSubdirectory("nt-reuse-src-").FullName;
        var destination = Directory.CreateTempSubdirectory("nt-reuse-dst-").FullName;
        try
        {
            var relative = Path.Combine("snapshot", "model.onnx");
            Directory.CreateDirectory(Path.Combine(source, "snapshot"));
            File.WriteAllBytes(Path.Combine(source, relative), [9, 8, 7]);
            var sha = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 9, 8, 7 }));
            var files = new[] { new LocalModelFile("snapshot/model.onnx", 3, sha) };

            var first = LocalModelReuse.Reuse(files, [source], destination);
            Assert.Equal(1, first.ReusedFiles);
            Assert.Equal(3, first.ReusedBytes);
            Assert.True(File.Exists(Path.Combine(destination, relative)));

            var second = LocalModelReuse.Reuse(files, [source], destination);
            Assert.Equal(0, second.ReusedFiles);
        }
        finally
        {
            try { Directory.Delete(source, true); } catch { }
            try { Directory.Delete(destination, true); } catch { }
        }
    }

    [Fact]
    public void ReuseSkipsCorruptCandidatesAndRejectsTraversal()
    {
        var source = Directory.CreateTempSubdirectory("nt-reuse-bad-").FullName;
        var destination = Directory.CreateTempSubdirectory("nt-reuse-bad-dst-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(source, "model.onnx"), [1, 2, 3]);
            var expected = new LocalModelFile("model.onnx", 3, new string('a', 64));
            var result = LocalModelReuse.Reuse([expected], [source], destination);
            Assert.Equal(0, result.ReusedFiles);
            Assert.False(File.Exists(Path.Combine(destination, "model.onnx")));

            Assert.Throws<InvalidDataException>(() =>
                LocalModelReuse.Reuse([new LocalModelFile("../evil.bin", 1, new string('a', 64))], [source], destination));
        }
        finally
        {
            try { Directory.Delete(source, true); } catch { }
            try { Directory.Delete(destination, true); } catch { }
        }
    }
}
