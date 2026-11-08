using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class OcrLayoutTests
{
    [Fact]
    public void RecordedNativeBoxesPreserveTableCellsAndCorrectTwoColumnOrder()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../validation/p3/ocr-native-golden.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(root));
        Assert.Equal(3, document.RootElement.GetArrayLength());
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString(); var blocks = EnhancedOcrService.Parse(item.GetRawText(), 3);
            Assert.All(blocks, b => { Assert.Equal(3, b.Page); Assert.Equal(4, b.Box!.Count); });
            if (name == "table")
            {
                var table = Assert.Single(blocks, b => b.Kind == "paddle-table"); Assert.Contains("<tr><td>桥面巡检</td><td>7</td></tr>", table.Text); Assert.Contains("<td>90</td>", table.Text);
            }
            if (name == "columns")
            {
                var text = string.Join("\n", blocks.Select(b => b.Text)); Assert.True(text.IndexOf("吊装前") < text.IndexOf("第二部分")); Assert.True(text.IndexOf("安全培训") < text.IndexOf("验收记录"));
            }
            if (name == "formula") Assert.Contains(blocks, b => b.Kind.Contains("formula") && b.Text.Contains("F") && b.Text.Contains("A"));
        }
    }
    [Fact]
    public void SingleColumnAndUnlocatedTextAreNotReordered()
    {
        DocumentBlock[] plain = [new("line1", "first"), new("line2", "second")]; Assert.Equal(plain, LayoutReadingOrder.Order(plain));
        DocumentBlock[] single = Enumerable.Range(0, 6).Select(i => new DocumentBlock("line" + i, "text" + i, 1, "text", new[] { 10, 10+i*20, 900, 20+i*20 })).ToArray();
        Assert.Equal(single, LayoutReadingOrder.Order(single));
    }
}
