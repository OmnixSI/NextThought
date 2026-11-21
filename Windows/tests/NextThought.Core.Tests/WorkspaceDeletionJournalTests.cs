using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class WorkspaceDeletionJournalTests
{
    [Fact]
    public void JournalUsesTheMacTransactionKeys()
    {
        var journal = new WorkspaceDeletionJournal { TransactionId = "tx", WorkspaceId = "ws" };
        var json = System.Text.Json.JsonSerializer.Serialize(journal, JsonDefaults.Options);
        Assert.Contains("\"transactionID\"", json);
        Assert.Contains("\"workspaceID\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<WorkspaceDeletionJournal>(json, JsonDefaults.Options)!;
        Assert.Equal("tx", back.TransactionId);
        Assert.Equal("ws", back.WorkspaceId);
    }
}
