using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class JsonKeyCompatibilityTests
{
    [Fact]
    public void TranscriptCorrectionUsesLineID()
    {
        var lineId = Guid.NewGuid();
        var correction = new TranscriptCorrection
        {
            Line = 2, LineId = lineId, BaseRevision = "rev", Original = "库伯奈提斯", Corrected = "Kubernetes"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(correction, JsonDefaults.Options);
        Assert.Contains("\"lineID\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<TranscriptCorrection>(json, JsonDefaults.Options)!;
        Assert.Equal(lineId, back.LineId);
        Assert.Equal("Kubernetes", back.Corrected);
    }

    [Fact]
    public void StructuredNotesUsesTemplateID()
    {
        var doc = new StructuredNotesDoc("template", "客户会", "zh", "标题", []);
        var json = System.Text.Json.JsonSerializer.Serialize(doc, JsonDefaults.Options);
        Assert.Contains("\"templateID\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesDoc>(json, JsonDefaults.Options)!;
        Assert.Equal("template", back.TemplateId);
    }
}
