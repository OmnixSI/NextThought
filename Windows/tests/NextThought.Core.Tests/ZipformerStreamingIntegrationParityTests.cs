using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Swift parity: Tests/NextThoughtCoreTests/ZipformerStreamingIntegrationTests.swift.
/// The Windows port has model descriptors and hotword helpers only; there is no streaming
/// runtime with configure/makeStream/finishStream/resetStream/commitStream, so the
/// integration test cannot be translated.
/// </summary>
public sealed class ZipformerStreamingIntegrationParityTests
{
    [Fact]
    public void BilingualFixturesAndStreamingLifecycle()
    {
        // The Swift test returns immediately unless the model directory is supplied. The Windows
        // port has no streaming runtime yet, so it does the same when the environment is absent
        // and fails loudly if someone points it at a model.
        var modelDirectory = Environment.GetEnvironmentVariable("IH_ZIPFORMER_MODEL_DIR");
        if (string.IsNullOrEmpty(modelDirectory)) return;
        Assert.Fail("Zipformer streaming runtime is not ported yet.");
    }
}
