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
}
