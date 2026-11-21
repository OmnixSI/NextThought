using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §3.3 live 9: live archive writes go through the bounded Core queue and the top bar shows the
/// Mac archiveSaveStatus row (保存中 / 保存受阻 · N 项未保存 · 重试 / 确认缺失).
/// </summary>
public sealed class LiveSaveStatusTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    [Fact]
    public void SessionStoreHasTheRawAppendAndCounterPath()
    {
        var store = Read("Windows", "src", "NextThought.Platform", "SessionStore.cs");
        Assert.Contains("public async Task AppendRawAsync(Guid session, string fileName, byte[] data", store);
        Assert.Contains("public async Task IncrementMetaCountAsync(Guid session, string kind", store);
        Assert.Contains("FileMode.Append", store);
    }

    [Fact]
    public void SaveStatusOwnsTheQueueAndTheFlush()
    {
        var state = Read("Windows", "src", "NextThought.Desktop", "AppState.SaveStatus.cs");
        Assert.Contains("SessionArchiveWriteQueue", state);
        Assert.Contains("TimeSpan.FromMilliseconds(500)", state);
        Assert.Contains("FlushArchiveQueueAsync", state);
        Assert.Contains("SaveStatusKind", state);
        Assert.Contains("SaveStatusText", state);
        Assert.Contains("SaveUnqueuedCount", state);
        Assert.Contains("RetryPersistence", state);
        Assert.Contains("AcknowledgeSaveLoss", state);
        Assert.Contains("EnqueueArchiveWrite", state);
    }

    [Fact]
    public void LiveWritesGoThroughTheQueue()
    {
        var live = Read("Windows", "src", "NextThought.Desktop", "AppState.Live.cs");
        Assert.Contains("EnqueueTranscriptLine(meta, line)", live);
        Assert.Contains("EnqueueQuestion(meta", live);
        Assert.Contains("EnqueueAnswer(meta", live);
        Assert.Contains("FlushArchiveQueueAsync", live);
        var audio = Read("Windows", "src", "NextThought.Desktop", "AppState.Audio.cs");
        Assert.Contains("EnqueueVoiceTag(meta.Id, line.Id", audio);
        var exports = Read("Windows", "src", "NextThought.Desktop", "AppState.Archive.cs");
        Assert.Contains("FlushArchiveQueueAsync", exports);
    }

    [Fact]
    public void TopBarShowsTheSaveStatusRow()
    {
        var view = Read("Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml");
        Assert.Contains("SaveStatusText", view);
        Assert.Contains("HasSaveStatus", view);
        Assert.Contains("RetryPersistenceCommand", view);
        Assert.Contains("AcknowledgeSaveLossCommand", view);
        Assert.Contains("确认缺失并继续保存", view);
        Assert.Contains("重试保存", view);
    }
}