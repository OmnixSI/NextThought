using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E25: recordSessions and textZoom are stored in settings.json under the Mac key names (the
/// Windows registry and SETTINGS_PARITY rows cover the other side of the bridge).
/// </summary>
public sealed class RecordSessionsSettingsStoreTests
{
    [Fact]
    public void SettingsStoreRoundTripsTheMacKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-e25-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SettingsStore(new AppPaths(root));
            var original = store.Load();
            Assert.True(original.RecordSessions);
            Assert.Equal(1.0, original.TextZoom);
            original.RecordSessions = false;
            original.TextZoom = 1.45;
            store.SaveAsync(original).GetAwaiter().GetResult();
            var reloaded = store.Load();
            Assert.False(reloaded.RecordSessions);
            Assert.Equal(1.45, reloaded.TextZoom);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "settings.json")));
            Assert.False(document.RootElement.GetProperty("recordSessions").GetBoolean());
            Assert.Equal(1.45, document.RootElement.GetProperty("textZoom").GetDouble());
        }
        finally { Directory.Delete(root, true); }
    }
}
