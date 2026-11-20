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
    [Fact(Skip = "missing API: ZipformerRuntime.configure(modelDirectory:), makeStream(), finishStream(_:), resetStream(_:), commitStream(_:), unload()")]
    public void BilingualFixturesAndStreamingLifecycle()
    {
    }
}
