using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from DocumentFeatureAvailabilityTests.swift
/// (testEveryDependencyIsRequiredBeforeCallingFeatureReady): every dependency is required before
/// a feature counts as ready, checked exhaustively over all 64 dependency masks.
/// </summary>
public sealed class DocumentFeatureAvailabilityParityTests
{
    [Fact]
    public void EveryDependencyIsRequiredBeforeCallingFeatureReady()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var runtime = (mask & 1) != 0;
            var ocr = (mask & 2) != 0;
            var layout = (mask & 4) != 0;
            var embedding = (mask & 8) != 0;
            var connected = (mask & 16) != 0;
            var checking = (mask & 32) != 0;
            var status = new DocumentFeatureAvailability(checking, runtime, ocr, layout, embedding, connected);
            Assert.Equal(!checking && runtime && ocr && layout,
                status.Ready(DocumentFeatureAvailability.Feature.Paddle));
            Assert.Equal(!checking && runtime && connected,
                status.Ready(DocumentFeatureAvailability.Feature.Codex));
            Assert.Equal(!checking && runtime && embedding,
                status.Ready(DocumentFeatureAvailability.Feature.Semantic));
            Assert.Equal(!checking && runtime,
                status.Ready(DocumentFeatureAvailability.Feature.Office));
        }
    }
}
