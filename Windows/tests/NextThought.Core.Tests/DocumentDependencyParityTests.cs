using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from DocumentDependencyTests.swift: older jobs without a typed waiting reason still
/// decode, and the dependency reason survives persistence next to ready text.
/// </summary>
public sealed class DocumentDependencyParityTests
{
    [Fact]
    public void OlderJobsWithoutTypedDependencyStillDecode()
    {
        var original = DocumentJob.Create(Guid.NewGuid().ToString("D"), "paper.pdf");
        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("waitingReason");
        var data = node.ToJsonString();
        var recovered = JsonSerializer.Deserialize<DocumentJob>(data, JsonDefaults.Options)!;

        // Swift compares the whole value (`recovered == original`); the record's list properties
        // compare by reference, so the same equality is expressed over the serialized shape.
        Assert.True(JsonNode.DeepEquals(node, JsonNode.Parse(JsonSerializer.Serialize(recovered, JsonDefaults.Options))));
        Assert.Null(recovered.WaitingFor);
    }

    [Fact]
    public void DependencyReasonSurvivesPersistenceWithReadyText()
    {
        var job = DocumentJob.Create(Guid.NewGuid().ToString("D"), "paper.pdf") with
        {
            Stage = DocumentJob.StageValue.Waiting,
            DocumentId = Guid.NewGuid().ToString("D"),
            WaitingFor = DocumentJob.WaitingReason.Semantic,
            Message = "文字可用，等待语义组件"
        };
        var data = JsonSerializer.Serialize(job, JsonDefaults.Options);
        var recovered = JsonSerializer.Deserialize<DocumentJob>(data, JsonDefaults.Options)!;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(data), JsonNode.Parse(JsonSerializer.Serialize(recovered, JsonDefaults.Options))));
        Assert.NotNull(recovered.DocumentId);
        Assert.Equal(DocumentJob.WaitingReason.Semantic, recovered.WaitingFor);

        // Swift: DocumentDependencyError(reason: .runtime, message: "missing runtime").errorDescription
        // == "missing runtime". C# DocumentDependencyError has no ErrorDescription property; its
        // Message is the only available equivalent and is checked here (reported as a missing API).
        var error = new DocumentDependencyError(DocumentJob.WaitingReason.Runtime, "missing runtime");
        Assert.Equal("missing runtime", error.Message);
        Assert.Equal(DocumentJob.WaitingReason.Runtime, error.Reason);
    }
}
