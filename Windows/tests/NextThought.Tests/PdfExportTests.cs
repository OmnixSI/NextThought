using NextThought.Platform;
using UglyToad.PdfPig;
using Xunit;

namespace NextThought.Tests;

public sealed class PdfExportTests
{
    [Fact]
    public void PdfContainsReadableChineseText()
    {
        var path = Path.Combine(Path.GetTempPath(), "nextthought-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            PdfWriter.Write("# 验收\n\n正文段落\n- 条目", path);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4));
            using var document = PdfDocument.Open(path);
            var text = string.Concat(document.GetPages().Select(page => page.Text));
            Assert.Contains("验收", text);
            Assert.Contains("正文段落", text);
            Assert.True(document.NumberOfPages >= 1);
        }
        finally { File.Delete(path); }
    }
}
