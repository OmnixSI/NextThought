using System.IO.Compression;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class DocxWriterTests
{
    [Fact]
    public void WritesAValidPackageWithEscapedParagraphs()
    {
        var path = Path.Combine(Path.GetTempPath(), "nextthought-docx-" + Guid.NewGuid().ToString("N") + ".docx");
        try
        {
            DocxWriter.Write("""
                # 标题

                ## 摘要
                正文 & <x>
                - 条目
                > 引用
                """, path);
            using var archive = ZipFile.OpenRead(path);
            Assert.Contains(archive.Entries, entry => entry.FullName == "word/document.xml");
            Assert.Contains(archive.Entries, entry => entry.FullName == "word/styles.xml");
            Assert.Contains(archive.Entries, entry => entry.FullName == "[Content_Types].xml");
            using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
            var xml = reader.ReadToEnd();
            Assert.Contains("标题", xml);
            Assert.Contains("Heading1", xml);
            Assert.Contains("摘要", xml);
            Assert.Contains("正文 &amp; &lt;x&gt;", xml);
            Assert.Contains("• 条目", xml);
        }
        finally { File.Delete(path); }
    }
}
