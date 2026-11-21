using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class SessionStoreDeletionTests
{
    [Fact]
    public async Task DeletingASessionRemovesItsFolderAndIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root));
            var meta = new SessionMeta { Title = "临时会议" };
            await store.CreateAsync(meta);
            Assert.True(Directory.Exists(store.Folder(meta.Id)));
            await store.DeleteAsync(meta.Id);
            Assert.False(Directory.Exists(store.Folder(meta.Id)));
            await store.DeleteAsync(meta.Id);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task RenamingASessionUpdatesTheArchiveMeta()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-rename-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root));
            var meta = new SessionMeta { Title = "旧标题" };
            await store.CreateAsync(meta);
            await store.RenameAsync(meta.Id, "新标题");
            Assert.Equal("新标题", store.Load(meta.Id).Meta.Title);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ContentSearchFindsArchivedTranscriptLines()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-search-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root));
            var meta = new SessionMeta { Title = "搜索测试" };
            await store.CreateAsync(meta);
            await store.AppendTranscriptAsync(meta, new TranscriptLine { Text = "讨论温度补偿", Speaker = "对方", Date = DateTimeOffset.Now });
            var hit = Assert.Single(store.SearchContent("温度补偿"));
            Assert.Equal(meta.Id, hit.Meta.Id);
            Assert.Equal(1, hit.LineNumber);
            Assert.Empty(store.SearchContent("不存在的词"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ScreenshotsAreKeptWithTheSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-shot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root));
            var meta = new SessionMeta { Title = "截图测试" };
            await store.CreateAsync(meta);
            var source = Path.Combine(root, "shot.png");
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(source, [137, 80, 78, 71]);
            var archived = await store.SaveScreenshotAsync(meta.Id, source);
            Assert.True(File.Exists(archived));
            Assert.Single(store.Screenshots(meta.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void UserNotesRoundTripThroughUserContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-user-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var saved = SessionUserContentStore.Save(new SessionUserContent
            {
                EditedMarkdown = "# 编辑后的纪要",
                PersonalNotes = "个人笔记"
            }, root);
            var loaded = SessionUserContentStore.Load(root);
            Assert.Equal("# 编辑后的纪要", loaded.EditedMarkdown);
            Assert.Equal("个人笔记", loaded.PersonalNotes);
            Assert.Equal(saved.Revision, loaded.Revision);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
