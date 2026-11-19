using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of DocumentFeatureAvailabilityTests.swift.</summary>
public sealed class DocumentFeatureAvailabilityTests
{
    [Fact]
    public void EveryDependencyIsRequiredBeforeCallingFeatureReady()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var status = new DocumentFeatureAvailability(
                Checking: (mask & 32) != 0,
                Runtime: (mask & 1) != 0,
                Ocr: (mask & 2) != 0,
                Layout: (mask & 4) != 0,
                Embedding: (mask & 8) != 0,
                CodexConnected: (mask & 16) != 0);
            Assert.Equal(!status.Checking && status.Runtime && status.Ocr && status.Layout,
                status.Ready(DocumentFeatureAvailability.Feature.Paddle));
            Assert.Equal(!status.Checking && status.Runtime && status.CodexConnected,
                status.Ready(DocumentFeatureAvailability.Feature.Codex));
            Assert.Equal(!status.Checking && status.Runtime && status.Embedding,
                status.Ready(DocumentFeatureAvailability.Feature.Semantic));
            Assert.Equal(!status.Checking && status.Runtime,
                status.Ready(DocumentFeatureAvailability.Feature.Office));
        }
    }
}
