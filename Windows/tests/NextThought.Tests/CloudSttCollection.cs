using Xunit;

namespace NextThought.Tests;

/// <summary>Loopback STT tests bind ephemeral ports; keep them from racing each other.</summary>
[CollectionDefinition("CloudSTT", DisableParallelization = true)]
public sealed class CloudSttCollection;
