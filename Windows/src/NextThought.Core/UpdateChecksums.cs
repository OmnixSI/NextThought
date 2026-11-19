using System.Security.Cryptography;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// Checksum handling for in-app updates (Mac Updater verifies the downloaded build before
/// offering "重启安装"). Accepts the same SHA-256 text formats as the release workflow:
/// <c>sha256  name</c>, <c>sha256 *name</c> and a bare hash when only one line exists.
/// </summary>
public static class UpdateChecksums
{
    public static string? Find(string text, string fileName)
    {
        string? single = null;
        var count = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit)) continue;
            count++;
            single = parts[0];
            if (parts.Length > 1 && string.Equals(parts[1].TrimStart('*'), fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return count == 1 ? single : null;
    }

    public static bool Verify(string path, string expectedHex)
    {
        if (expectedHex.Length != 64 || !expectedHex.All(Uri.IsHexDigit)) return false;
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        var expected = expectedHex.ToUpperInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected));
    }
}
