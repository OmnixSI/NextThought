using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E21: the material review sheet's persisted notes and extracted-text preview
/// (ProjectLibrarySupport.swift MaterialReviewSheet + material-review.json).
/// </summary>
public sealed class MaterialReviewTests
{
    private static MaterialDocument Document(params string[] warnings) => new()
    {
        Name = "论文.pdf",
        Extension = ".pdf",
        Warnings = [.. warnings],
        Blocks = [new DocumentBlock("p.1", "第一页正文"), new DocumentBlock("p.2", "第二页正文")]
    };

    [Fact]
    public void ReviewNotesRoundTripPerMaterial()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-review-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal("", MaterialReviewNotes.NoteFor(root, "doc-1"));
            MaterialReviewNotes.Save(root, "doc-1", "第 2 页的表格需要人工核对");
            MaterialReviewNotes.Save(root, "doc-2", "原件缺失");
            Assert.Equal("第 2 页的表格需要人工核对", MaterialReviewNotes.NoteFor(root, "doc-1"));
            Assert.Equal("原件缺失", MaterialReviewNotes.NoteFor(root, "doc-2"));
            // saving a blank note removes it instead of leaving an empty entry
            MaterialReviewNotes.Save(root, "doc-2", "  ");
            Assert.Equal("", MaterialReviewNotes.NoteFor(root, "doc-2"));
            Assert.True(File.Exists(MaterialReviewNotes.PathOf(root)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingOrBrokenFileReadsAsNoNotes()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Empty(MaterialReviewNotes.Load(root));
            File.WriteAllText(MaterialReviewNotes.PathOf(root), "{not json");
            Assert.Empty(MaterialReviewNotes.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PreviewKeepsBlockLocationsAndTruncatesAtTheLimit()
    {
        var (text, truncated) = MaterialReviewPreview.Build(Document().Blocks);
        Assert.False(truncated);
        Assert.Contains("[p.1]", text);
        Assert.Contains("第一页正文", text);
        Assert.Contains("[p.2]", text);

        var blocks = new List<DocumentBlock> { new("p.1", new string('字', 90)) };
        var (shortPreview, truncatedWhenLong) = MaterialReviewPreview.Build(blocks, 50);
        Assert.True(truncatedWhenLong);
        Assert.Equal(50, shortPreview.Length); // "[p.1]\n" + 44 characters
    }

    [Fact]
    public void OnlyWarningsThatNeedALookFlagTheMaterial()
    {
        Assert.False(Document().NeedsReview);
        Assert.False(Document("第 3 页的图片没有读取").NeedsReview);
        Assert.True(Document("扫描件 OCR 可信度偏低").NeedsReview);
        Console.WriteLine(MaterialReviewPreview.Build(Document().Blocks).Text.Length);
    }
}
