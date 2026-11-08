using System.Net;
using System.Text;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class MeetingWorkflowTests
{
    [Fact]
    public void AutomaticQuestionWaitsForQuietAndTriggersExactlyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new QuestionCandidateTracker().Ingest("为什么要先验证录音？", true, Guid.NewGuid(), now).Candidate!;
        var scheduler = new AutomaticAnswerScheduler(); scheduler.Enqueue(candidate, now);
        Assert.Null(scheduler.Tick(now.AddSeconds(2), true));
        Assert.Null(scheduler.Tick(now.AddSeconds(3), false));
        Assert.Equal(candidate, scheduler.Tick(now.AddSeconds(3.6), false));
        Assert.Null(scheduler.Tick(now.AddSeconds(4), false));
    }
    [Fact]
    public void ExpiredQuestionIsDiscardedAndRevisedQuestionReplacesPendingOne()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new QuestionCandidateTracker().Ingest("为什么要先验证录音？", true, Guid.NewGuid(), now).Candidate!;
        var scheduler = new AutomaticAnswerScheduler(); scheduler.Enqueue(first, now);
        Assert.Null(scheduler.Tick(now.AddSeconds(16), false));
        scheduler.Enqueue(first, now);
        var revised = first with { Text = "为什么要先验证双路录音？", Revision = 1 };
        scheduler.Enqueue(revised, now.AddSeconds(1));
        Assert.Null(scheduler.Tick(now.AddSeconds(2), false));
        Assert.Equal(revised, scheduler.Tick(now.AddSeconds(3), false));
    }
    [Theory]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"只有一半\"}}]}\n\n", "完整响应前中断", typeof(IOException))]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"只有一半\"},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n", "长度上限", typeof(InvalidDataException))]
    public async Task IncompleteModelOutputCannotBeMistakenForACompleteAnswer(string payload, string expected, Type exceptionType)
    {
        using var http = new HttpClient(new Handler(payload));
        var error = await Assert.ThrowsAsync(exceptionType, () => new ChatCompletionClient(http).AnswerAsync(new("http://localhost/v1", "fixture", ""), "s", "u"));
        Assert.Contains(expected, error.Message);
    }
    [Fact]
    public async Task SseErrorRedactsStoredApiKey()
    {
        const string key = "fixture-secret-not-a-real-key";
        using var http = new HttpClient(new Handler("data: {\"error\":{\"message\":\"invalid " + key + "\"}}\n\n"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new ChatCompletionClient(http).AnswerAsync(new("http://localhost/v1", "fixture", key), "s", "u"));
        Assert.DoesNotContain(key, error.Message); Assert.Contains("[key]", error.Message);
    }
    private sealed class Handler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "text/event-stream") });
    }
}
