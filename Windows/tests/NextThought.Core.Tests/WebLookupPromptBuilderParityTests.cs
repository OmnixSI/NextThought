using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of WebLookupPromptBuilderTests.swift (3 Swift Testing tests): a lookup drops the skill
/// set, keeps the subject of the question, bounds every field and reads batched search progress.
/// </summary>
public sealed class WebLookupPromptBuilderParityTests
{
    private static readonly JsonSerializerOptions MacJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// The prompt text as the Swift builder writes it. <c>JsonNode.ToJsonString</c> keeps the
    /// default encoder, which escapes every non-ASCII character as \uXXXX (six characters per CJK
    /// character), so the raw <c>User</c> length is not comparable to the Swift <c>String.count</c>;
    /// re-serializing without escapes measures the same text the Swift test measures.
    /// </summary>
    private static int MacJsonLength(string json) => JsonNode.Parse(json)!.ToJsonString(MacJson).Length;

    [Fact]
    public void LookupDropsSkillsAndBoundsContextWithoutLosingTheSubject()
    {
        var input = new JsonObject
        {
            ["question"] = "它刚发布的版本是什么？",
            ["today"] = "2026-10-17",
            ["recent_transcript"] = string.Concat(Enumerable.Repeat("旧上下文", 5_000)) + "正在讨论 Swift",
            ["recent_answers"] = BuildRecentAnswers(),
            ["skills"] = new JsonArray(new JsonObject { ["content"] = string.Concat(Enumerable.Repeat("技能", 6_000)) })
        };
        var prompt = WebLookupPromptBuilder.MakeParts(input.ToJsonString());
        var output = JsonNode.Parse(prompt.User)!.AsObject();
        Assert.Equal("它刚发布的版本是什么？", output["question"]!.GetValue<string>());
        Assert.EndsWith("正在讨论 Swift", output["recent_transcript"]!.GetValue<string>());
        Assert.Equal(1_200, output["recent_transcript"]!.GetValue<string>().Length);
        Assert.Equal(2, output["recent_answers"]!.AsArray().Count);
        Assert.Null(output["skills"]);
        Assert.True(MacJsonLength(prompt.User) < 4_000);
        Assert.Contains("同一轮检索中并行查询", prompt.System);
        Assert.Contains("最多一轮检索", prompt.System);
    }

    private static JsonArray BuildRecentAnswers()
    {
        var answers = new JsonArray();
        for (var index = 0; index < 6; index++)
        {
            answers.Add(new JsonObject
            {
                ["question"] = "第" + index + "题",
                ["answer"] = string.Concat(Enumerable.Repeat("回答", 2_000))
            });
        }
        return answers;
    }

    [Fact]
    public void MalformedCallerRetainsABoundedQuestion()
    {
        var prompt = WebLookupPromptBuilder.MakeParts(string.Concat(Enumerable.Repeat("车型", 3_000)));
        Assert.True(MacJsonLength(prompt.User) < 2_100);
    }

    [Fact]
    public void ParsesBatchedSearchProgressAndIgnoresOtherTurns()
    {
        var parameters = new JsonObject
        {
            ["turnId"] = "T",
            ["item"] = new JsonObject
            {
                ["id"] = "S",
                ["type"] = "webSearch",
                ["action"] = new JsonObject
                {
                    ["type"] = "search",
                    ["queries"] = new JsonArray("query one", "query two")
                }
            }
        };
        Assert.Equal<CodexTurnEvent?>(new CodexWebSearch("S", false, 2),
            CodexAppServerProtocol.TurnEvent("item/started", parameters, "T"));
        Assert.Equal<CodexTurnEvent?>(new CodexWebSearch("S", true, 2),
            CodexAppServerProtocol.TurnEvent("item/completed", parameters, "T"));
        Assert.Null(CodexAppServerProtocol.TurnEvent("item/started", parameters, "OTHER"));
    }
}
