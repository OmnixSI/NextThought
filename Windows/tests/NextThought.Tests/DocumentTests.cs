using System.IO.Compression;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class DocumentTests
{
    [Fact]
    public async Task ImportKeepsOriginalPositionsDeduplicatesAndRollsBackCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "nextthought-doc-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "中文资料.docx");
            using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
                writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>桥梁复核</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>林工</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");
            }
            var store = new MaterialStore(new AppPaths(Path.Combine(root, "data")));
            var doc = await store.ImportAsync(file); var duplicate = await store.ImportAsync(file);
            Assert.Equal(doc.Id, duplicate.Id); Assert.Single(store.List());
            Assert.Equal("段落 1", doc.Blocks[0].Location); Assert.Equal("表 1 行 1 列 1", doc.Blocks[1].Location);
            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(store.SourcePath(doc)));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ImportAsync(file, canceled.Token));
            Assert.Single(Directory.GetDirectories(store.Root));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task OfficeDtdIsRejectedWithoutCommittingMaterial()
    {
        var root = Path.Combine(Path.GetTempPath(), "nextthought-xml-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "bad.docx");
            using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
            { using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()); writer.Write("<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///C:/secret'>]><a>&x;</a>"); }
            var store = new MaterialStore(new AppPaths(Path.Combine(root, "data")));
            await Assert.ThrowsAsync<System.Xml.XmlException>(() => store.ImportAsync(file)); Assert.Empty(store.List());
        }
        finally { Directory.Delete(root, true); }
    }
}
