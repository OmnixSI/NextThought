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
}
