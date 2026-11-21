using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class SpeakerTests
{
    [Fact]
    public void ShortUncertainAndInvalidVoicesStayUnknown()
    {
        var first = new float[256]; first[0] = 1; var other = new float[256]; other[1] = 1;
        var groups = new VoiceGroups(); Assert.Null(groups.Assign(first, .6)); Assert.Equal(0, groups.Assign(first, 3)); Assert.Equal(0, groups.Assign(first, 1));
        Assert.Equal(1, groups.Assign(other, 3)); var ambiguous = new float[256]; ambiguous[0] = ambiguous[1] = (float)Math.Sqrt(.5);
        Assert.Equal(0, groups.Assign(ambiguous, 3)); var orthogonal = new float[256]; orthogonal[2] = 1;
        Assert.Null(groups.Assign(orthogonal, 1)); Assert.Null(groups.Assign(new float[256], 4)); Assert.Null(groups.Assign([float.NaN], 3));
        var nextMeeting = new VoiceGroups(); Assert.Equal(0, nextMeeting.VoiceCount);
    }
    [Fact]
    public void FilterbankHasExpectedFrameShapeAndClipMeanNormalization()
    {
        var pcm = Enumerable.Range(0, 16000).Select(i => (short)(5000 * Math.Sin(i * 2 * Math.PI * 440 / 16000))).ToArray();
        var features = SpeakerFeatures.Compute(pcm); Assert.Equal(98 * 80, features.Length); Assert.All(features, x => Assert.True(float.IsFinite(x)));
        for (int b = 0; b < 80; b++) Assert.InRange(Math.Abs(Enumerable.Range(0, 98).Average(f => features[f * 80 + b])), 0, .0001);
    }
    [Fact]
    public async Task VoiceTagsSurviveReloadWithoutChangingOriginalTranscriptBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-voices-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
            var line = new TranscriptLine { Text = "保留原文", Date = meta.StartedAt }; await store.AppendTranscriptAsync(meta, line);
            var original = File.ReadAllBytes(Path.Combine(store.Folder(meta.Id), "transcript.jsonl"));
            await store.AppendVoiceTagAsync(meta.Id, line.Id, 0); await store.AppendVoiceTagAsync(meta.Id, line.Id, 2);
            Assert.Equal(2, store.Load(meta.Id).Lines.Single().Voice); Assert.Equal(original, File.ReadAllBytes(Path.Combine(store.Folder(meta.Id), "transcript.jsonl")));
            var output = Path.Combine(root, "export.md"); await store.ExportMarkdownAsync(meta.Id, output); Assert.Contains("说话人3", await File.ReadAllTextAsync(output));
        }
        finally { Directory.Delete(root, true); }
    }
}
