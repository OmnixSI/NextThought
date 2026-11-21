using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// G5: SETTINGS_PARITY.md covers all 113 Mac preference keys with type, Mac default, Mac settings
/// view and a Windows status that carries code evidence; the Windows settings store and the Mac
/// settings.plist use the same Mac key names and Mac defaults.
/// </summary>
public sealed class SettingsParityTableTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", ".."));

    [Fact]
    public void TableHasEveryMacPreferenceKeyWithTypeDefaultViewAndStatus()
    {
        var file = Path.Combine(RepositoryRoot, "SETTINGS_PARITY.md");
        Assert.True(File.Exists(file), file);
        var lines = File.ReadAllLines(file);
        var rows = lines.Where(line => line.StartsWith("| `", StringComparison.Ordinal)).ToList();
        // Mac 3.4.0 adds textZoom, which Windows has not ported yet: it is listed as 未做 and is not
        // part of the generated runtime registry until J7 implements the scaling.
        Assert.Equal(MacPreferences.All.Count + 1, rows.Count);
        var listed = new List<string>();
        foreach (var row in rows)
        {
            var cells = row.Split('|').Select(cell => cell.Trim()).ToList();
            // | key | type | default | Mac view | status |
            Assert.True(cells.Count >= 7, row);
            var key = cells[1].Trim('`');
            listed.Add(key);
            Assert.False(string.IsNullOrWhiteSpace(cells[2]), key);
            Assert.False(string.IsNullOrWhiteSpace(cells[3]), key);
            Assert.False(string.IsNullOrWhiteSpace(cells[4]), key);
            var status = cells[5];
            if (key == "textZoom")
            {
                Assert.StartsWith("未做", status, StringComparison.Ordinal);
                continue;
            }
            Assert.True(status.StartsWith("已实现、未验证", StringComparison.Ordinal)
                        || status.StartsWith("本版不做", StringComparison.Ordinal),
                $"{key}: {status}");
            // The status column value itself must never be 未做 (evidence may mention a task).
            Assert.False(status.StartsWith("未做", StringComparison.Ordinal), key);
            var evidence = status.Contains('｜', StringComparison.Ordinal) ? status[(status.IndexOf('｜') + 1)..].Trim() : "";
            Assert.False(string.IsNullOrWhiteSpace(evidence), $"{key} has no evidence");
            Assert.True(evidence.Length > 8, $"{key}: {evidence}");
        }
        Assert.Equal(MacPreferences.All.Count + 1, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("textZoom", listed, StringComparer.Ordinal);
    }

    [Fact]
    public void TableHeaderDocumentsTheRecomputeCommandAndCurrentNumbers()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot, "SETTINGS_PARITY.md"));
        Assert.Contains("python Windows/scripts/settings_parity.py", text, StringComparison.Ordinal);
        Assert.Contains("114 / 114", text, StringComparison.Ordinal);
        var done = MacPreferences.All.Count(preference => StatusFor(preference.Key) == "done");
        Assert.Contains($"{done}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsStoreStartsFromTheMacDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-g5-defaults-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(new AppPaths(root));
            var values = store.LoadMacPreferences();
            var defaults = MacPreferences.CreateDefaults();
            Assert.Equal(113, values.Count);
            foreach (var (key, expected) in defaults)
            {
                Assert.True(MacPreferences.ValuesEqual(expected, values[key]),
                    $"{key}: expected {Describe(expected)}, found {Describe(values[key])}");
            }
            Assert.Equal(1.0, store.LoadMacValue("teleprompterOpacity"));
            Assert.Equal("soniox", store.LoadMacValue("cloudProvider"));
            Assert.Equal("zipformer", store.LoadMacValue("transcriptionBackend"));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task SettingsStoreWritesEveryMacKeyUnderItsMacName()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-g5-names-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var store = new SettingsStore(paths);
            await store.SaveAsync(new AppSettings());
            var document = JsonNode.Parse(await File.ReadAllTextAsync(paths.Settings))!.AsObject();
            foreach (var preference in MacPreferences.All)
                Assert.True(document.ContainsKey(preference.Key), preference.Key);
            // The Mac names replace the old camelCase Windows names for mapped settings.
            Assert.False(document.ContainsKey("baseUrl"));
            Assert.False(document.ContainsKey("model"));
            Assert.False(document.ContainsKey("secondPass"));
            // Windows-only settings keep their own names.
            Assert.True(document.ContainsKey("shortcuts"));
            Assert.True(document.ContainsKey("subtitleWidth"));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task EveryMacKeyRoundTripsThroughSettingsJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-g5-roundtrip-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(new AppPaths(root));
            var written = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["apiBaseURL"] = "https://example.test/v1",
                ["apiModel"] = "model-1",
                ["appearance"] = "dark",
                ["capturePrivacy"] = false,
                ["cloudProvider"] = "openai",
                ["transcriptionBackend"] = "cloud",
                ["codexReasoning"] = "high",
                ["companionPort"] = 9123L,
                ["insightScreenRegion"] = new List<object?> { 1.0, 2.0, 3.0, 4.0 },
                ["localStreamingModel"] = "xasr160",
                ["sceneOverrides"] = new Dictionary<string, object?>
                {
                    ["labMeeting"] = new Dictionary<string, object?>
                    {
                        ["cueStyle"] = "script"
                    }
                },
                ["subtitleBarFontSize"] = 30.0,
                ["teleprompterOpacity"] = 0.5,
                ["customNotesTemplates"] = new List<object?> { "template" },
                ["coachGazeSensitivity"] = "strict",
                ["updateChannel"] = "main",
                ["webSearchMode"] = "off",
                ["workspaceMigrationCompleted"] = true
            };
            await store.SaveMacPreferencesAsync(written);
            var read = store.LoadMacPreferences();
            foreach (var (key, value) in written)
            {
                var preference = MacPreferences.Find(key)!;
                var expected = MacPreferences.Normalize(preference, value);
                Assert.True(MacPreferences.ValuesEqual(expected, read[key]),
                    $"{key}: wrote {Describe(expected)}, read back {Describe(read[key])}");
            }
            Assert.Equal("https://example.test/v1", store.Load().BaseUrl);
            Assert.Equal("model-1", store.Load().Model);
            Assert.Equal("openai", store.Load().CloudStt);
            Assert.Equal(30, store.Load().SubtitleFontSize);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task LegacyCamelCaseSettingsStillLoadAndAreMigratedToMacNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-g5-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            await File.WriteAllTextAsync(paths.Settings, """
                {"baseUrl":"https://legacy.test/v1","model":"legacy-model","secondPass":false,
                 "captureMicrophone":false,"autoSummarize":false,"codexModel":"codex-1",
                 "macOnlyFlag":true}
                """);
            var store = new SettingsStore(paths);
            var settings = store.Load();
            Assert.Equal("https://legacy.test/v1", settings.BaseUrl);
            Assert.Equal("legacy-model", settings.Model);
            Assert.False(settings.SecondPass);
            Assert.False(settings.CaptureMicrophone);
            Assert.False(settings.AutoSummarize);
            Assert.Equal("codex-1", settings.CodexModel);
            await store.SaveAsync(settings);
            var text = await File.ReadAllTextAsync(paths.Settings);
            Assert.Contains("\"apiBaseURL\": \"https://legacy.test/v1\"", text, StringComparison.Ordinal);
            Assert.Contains("\"secondPassEnabled\": false", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"baseUrl\"", text, StringComparison.Ordinal);
            Assert.Contains("macOnlyFlag", text, StringComparison.Ordinal);
            var reloaded = store.Load();
            Assert.True(reloaded.AdditionalProperties!.ContainsKey("macOnlyFlag"));
            Assert.Equal("https://legacy.test/v1", reloaded.BaseUrl);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void MacPlistExportUsesTheMacDefaultsAndImportsThemBack()
    {
        var settings = new AppSettings();
        var text = System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(settings));
        var document = XDocument.Parse(text);
        var keys = document.Root!.Element("dict")!.Elements("key").Select(element => element.Value).ToList();
        Assert.Equal(SettingsSnapshot.ExportedKeys.OrderBy(key => key, StringComparer.Ordinal), keys.OrderBy(key => key, StringComparer.Ordinal));
        var elements = document.Root.Element("dict")!.Elements().ToList();
        string? Value(string key)
        {
            var index = elements.FindIndex(element => element.Name == "key" && element.Value == key);
            return index < 0 || index + 1 >= elements.Count ? null : elements[index + 1].Value;
        }
        Assert.Equal("auto", Value("webSearchMode"));
        Assert.Equal("soniox", Value("cloudProvider"));
        Assert.Equal("zipformer", Value("transcriptionBackend"));
        Assert.Equal("scene", Value("cueStyle"));
        Assert.Equal("22", Value("teleprompterFontSize"));
        Assert.Equal("1", Value("teleprompterOpacity"));
        Assert.Equal("8765", Value("companionPort"));

        // Importing the export of a default install yields the Mac defaults for every key again.
        var imported = MacSettingsPlist.Apply(MacSettingsPlist.Write(settings), new AppSettings());
        var read = MacPreferenceMap.ReadAll(imported);
        var defaults = MacPreferences.CreateDefaults();
        foreach (var (key, expected) in defaults)
            Assert.True(MacPreferences.ValuesEqual(expected, read[key]), $"{key}: {Describe(read[key])}");
    }

    [Fact]
    public void UnmappedMacKeysSurviveThePlistRoundTripAsRawValues()
    {
        var settings = new AppSettings();
        settings.AdditionalProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["customShortcuts"] = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["record"] = new Dictionary<string, object?> { ["keyCode"] = 15L, ["command"] = true, ["shift"] = true }
            }, JsonDefaults.Options)
        };
        var applied = MacSettingsPlist.Apply(MacSettingsPlist.Write(settings), new AppSettings());
        Assert.True(applied.AdditionalProperties!.ContainsKey("customShortcuts"));
        Assert.Contains("keyCode", applied.AdditionalProperties["customShortcuts"].GetRawText(), StringComparison.Ordinal);
    }

    private static string StatusFor(string key)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot, "SETTINGS_PARITY.md"));
        var row = lines.First(line => line.StartsWith($"| `{key}`", StringComparison.Ordinal));
        var cells = row.Split('|').Select(cell => cell.Trim()).ToList();
        return cells[5].StartsWith("本版不做", StringComparison.Ordinal) ? "skip" : "done";
    }

    private static string KindLabel(MacPreferenceKind kind) => kind switch
    {
        MacPreferenceKind.Bool => "Bool",
        MacPreferenceKind.String => "String",
        MacPreferenceKind.Integer => "Integer",
        MacPreferenceKind.Real => "Real",
        MacPreferenceKind.Array => "Array",
        MacPreferenceKind.Dict => "Dict",
        _ => "?"
    };

    private static string Describe(object? value) => value switch
    {
        null => "nil",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(Describe)) + "]",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "?"
    };
}
