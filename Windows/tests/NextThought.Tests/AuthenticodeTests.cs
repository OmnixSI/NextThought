using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class AuthenticodeTests
{
    [Fact]
    public void SignerAllowlistNormalizesThumbprints()
    {
        var previous = Environment.GetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT");
        try
        {
            Environment.SetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT", "aa bb, CC-DD");
            Assert.True(Authenticode.SignerAllowed("AABB"));
            Assert.True(Authenticode.SignerAllowed("cc-dd"));
            Assert.False(Authenticode.SignerAllowed("EEFF"));
            Assert.Equal(2, Authenticode.TrustedSigners.Count);
        }
        finally { Environment.SetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT", previous); }
    }

    [Fact]
    public void EmptyAllowlistRejectsEverything()
    {
        var previous = Environment.GetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT");
        try
        {
            Environment.SetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT", null);
            Assert.False(Authenticode.SignerAllowed("AABB"));
            Assert.Empty(Authenticode.TrustedSigners);
        }
        finally { Environment.SetEnvironmentVariable("NEXTTHOUGHT_SIGNER_THUMBPRINT", previous); }
    }
}
