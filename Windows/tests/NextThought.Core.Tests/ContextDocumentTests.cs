using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class ContextDocumentTests
{
    [Fact]
    public void ContextDocumentKeepsMacContentVerbatim()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","name":"论文","pages":["正文"],"enabled":true,
             "category":"论文","markdownPath":"Materials/x.md","sourceFormat":"pdf",
             "content":{"pipeline":"system","blocks":[{"markdown":"正文","locator":{"page":1}}]}}
            """;
        var document = System.Text.Json.JsonSerializer.Deserialize<ContextDocument>(json, NextThought.Core.JsonDefaults.Options)!;
        Assert.Equal("system", document.Content!.Value.GetProperty("pipeline").GetString());
        var saved = System.Text.Json.JsonSerializer.Serialize(document, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"content\"", saved);
        Assert.Contains("\"pipeline\"", saved);
        Assert.Contains("\"blocks\"", saved);
        var back = System.Text.Json.JsonSerializer.Deserialize<ContextDocument>(saved, NextThought.Core.JsonDefaults.Options)!;
        Assert.Equal(document.Content.Value.GetRawText(), back.Content!.Value.GetRawText());
    }
}
