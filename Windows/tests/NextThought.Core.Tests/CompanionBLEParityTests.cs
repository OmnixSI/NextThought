using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of CompanionBLETests.swift and CompanionBLEDeliveryTests.swift: fragmented state
/// survives shuffled, duplicated and interrupted frames, and the outbox retries a bounded
/// number of times before giving up.
/// </summary>
public sealed class CompanionBLEParityTests
{
    [Fact]
    public void ShuffledDuplicateAndInterruptedFrames()
    {
        var source = Enumerable.Range(0, 3000).Select(index => (byte)(index % 251)).ToArray();
        var pieces = CompanionBLEProtocol.Fragments(source, 0x12345678u, 185);
        Assert.All(pieces, piece => Assert.True(piece.Length <= 185));
        Assert.Equal([0x12, 0x34, 0x56, 0x78, 0, 0, 0, 17], pieces[0].Take(8));

        var receiver = new CompanionBLEProtocol.Reassembler();
        Assert.Null(receiver.Receive(pieces[0]));
        Assert.Null(receiver.Receive(pieces[0]));

        (uint Id, byte[] Data)? result = null;
        foreach (var packet in pieces.Skip(1).Reverse())
            if (receiver.Receive(packet) is { } value) result = value;
        Assert.NotNull(result);
        Assert.Equal(0x12345678u, result.GetValueOrDefault().Id);
        Assert.Equal(source, result.GetValueOrDefault().Data);

        Assert.Null(receiver.Receive(new byte[8]));

        var small = CompanionBLEProtocol.Fragments(Encoding.UTF8.GetBytes("new message"), 4, 20);
        _ = receiver.Receive(pieces[0]);
        var newMessage = receiver.Receive(small[0]);
        Assert.NotNull(newMessage);
        Assert.Equal(Encoding.UTF8.GetBytes("new message"), newMessage.GetValueOrDefault().Data);
    }

    [Fact]
    public void ExpiryBoundsAndMinimumMtu()
    {
        var data = Enumerable.Repeat((byte)1, 50).ToArray();
        var start = DateTimeOffset.UtcNow;
        var pieces = CompanionBLEProtocol.Fragments(data, 9, 20);
        var receiver = new CompanionBLEProtocol.Reassembler();
        _ = receiver.Receive(pieces[0], start);
        foreach (var packet in pieces.Skip(1))
            Assert.Null(receiver.Receive(packet, start.AddSeconds(16)));

        Assert.Empty(CompanionBLEProtocol.Fragments(data, 1, 19));
        Assert.Empty(CompanionBLEProtocol.Fragments(Enumerable.Repeat((byte)1, 400000).ToArray(), 1, 20));
    }

    [Fact]
    public void LostAcknowledgementRetriesAndLatestStateWins()
    {
        var start = DateTimeOffset.UtcNow;
        var first = Encoding.UTF8.GetBytes("first");
        var latest = Encoding.UTF8.GetBytes("latest");
        var outbox = new CompanionBLEOutbox(40);
        outbox.EnqueueState(first);
        var packet = outbox.Peek(20, start);
        Assert.NotNull(packet);
        Assert.Equal(41u, outbox.ActiveMessageId);
        outbox.DidSend(start);

        outbox.EnqueueState(Encoding.UTF8.GetBytes("superseded"));
        outbox.EnqueueState(latest);
        Assert.Null(outbox.Peek(20, start.AddSeconds(1)));
        Assert.Equal(packet, outbox.Peek(20, start.AddSeconds(3)));
        outbox.DidSend(start.AddSeconds(3));

        outbox.Acknowledge(40); // Wrong message cannot advance the queue.
        Assert.Equal(41u, outbox.ActiveMessageId);
        outbox.Acknowledge(41);

        var next = outbox.Peek(20, start.AddSeconds(4));
        Assert.NotNull(next);
        Assert.Equal(latest, next!.Skip(8));
        Assert.Equal(42u, outbox.ActiveMessageId);
    }

    [Fact]
    public void BoundedRetriesAndSlowContinuousAssembly()
    {
        var start = DateTimeOffset.UtcNow;
        var outbox = new CompanionBLEOutbox(7);
        outbox.EnqueueState(Encoding.UTF8.GetBytes("old"));
        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.NotNull(outbox.Peek(20, start.AddSeconds(attempt * 3)));
            outbox.DidSend(start.AddSeconds(attempt * 3));
        }

        Assert.Null(outbox.Peek(20, start.AddSeconds(12)));
        Assert.Equal(1, outbox.FailureCount);

        // Total assembly time is over 15 seconds; ongoing progress keeps the frame alive.
        var raw = Enumerable.Repeat((byte)3, 80).ToArray();
        var frames = CompanionBLEProtocol.Fragments(raw, 5, 20);
        var receiver = new CompanionBLEProtocol.Reassembler();
        byte[]? assembled = null;
        for (var index = 0; index < frames.Count; index++)
            if (receiver.Receive(frames[index], start.AddSeconds(index * 4)) is { } value) assembled = value.Data;
        Assert.NotNull(assembled);
        Assert.Equal(raw, assembled!);
    }
}
