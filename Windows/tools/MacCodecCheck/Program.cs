using System.Text.Json;

// A15: Windows-only check for fixtures emitted on macOS. It does not decrypt
// Mac codecs or run Swift; it verifies every pure-data fixture family decodes.
// Fixture families that need the seven CryptoKit files or four platform files
// are listed as skipped with a reason instead of being silently ignored.

var root = args.Length > 0 && args[0] == "--fixtures" && args.Length > 1
    ? args[1]
    : Path.Combine("Windows", "tests", "Fixtures", "mac");
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"MacCodec check: fixture directory not found: {root}");
    Console.Error.WriteLine("Run the macOS emit command from Windows/MAC_FIXTURE_REQUEST.md first.");
    return 2;
}

var failures = new List<string>();
JsonDocument ParseJson(string name)
{
    var path = Path.Combine(root, name);
    if (!File.Exists(path)) { failures.Add($"{name}: missing"); return null!; }
    try { return JsonDocument.Parse(File.ReadAllText(path)); }
    catch (JsonException ex) { failures.Add($"{name}: invalid JSON: {ex.Message}"); return null!; }
}
void Require(JsonDocument? document, string name, params string[] properties)
{
    if (document is null) return;
    foreach (var property in properties)
        if (!document.RootElement.TryGetProperty(property, out _)) failures.Add($"{name}: missing property {property}");
}
void Lines(string name, params string[] properties)
{
    var path = Path.Combine(root, name);
    if (!File.Exists(path)) { failures.Add($"{name}: missing"); return; }
    var index = 0;
    foreach (var raw in File.ReadLines(path))
    {
        index++;
        if (string.IsNullOrWhiteSpace(raw)) continue;
        try
        {
            using var document = JsonDocument.Parse(raw);
            foreach (var property in properties)
                if (!document.RootElement.TryGetProperty(property, out _)) failures.Add($"{name}:{index}: missing property {property}");
        }
        catch (JsonException ex) { failures.Add($"{name}:{index}: invalid JSON: {ex.Message}"); }
    }
}

var meta = ParseJson("session-meta.json");
Require(meta, "session-meta.json", "id", "title", "startedAt", "mode", "workspaceID");
var summary = ParseJson("summary.json");
Require(summary, "summary.json", "title", "markdown", "generatedAt", "questions", "model");
var user = ParseJson("user-content.json");
Require(user, "user-content.json", "schemaVersion", "revision", "personalNotes", "highlights");
var workspace = ParseJson("workspace.json");
Require(workspace, "workspace.json", "id", "name", "sessionIds", "lessons", "seriesItems");
if (workspace is not null && workspace.RootElement.TryGetProperty("seriesItems", out var items) && items.ValueKind == JsonValueKind.Array)
{
    foreach (var item in items.EnumerateArray())
        foreach (var property in new[] { "id", "kind", "text", "revision", "history" })
            if (!item.TryGetProperty(property, out _)) failures.Add($"workspace.json: series item missing {property}");
}
var backup = ParseJson("backup-manifest.json");
Require(backup, "backup-manifest.json", "format", "version", "createdAt", "includesSecrets", "sessions", "workspaces");
var revisions = ParseJson("revisions.json");
Require(revisions, "revisions.json", "transcriptSourceRevision", "claimSourceRevision", "sourceRevision", "expected");
Lines("transcript.jsonl", "id", "date", "speaker", "text");
Lines("translations.jsonl", "id", "text", "inputRevision");
Lines("questions.jsonl", "id", "text", "intent", "disposition");
foreach (var name in new[] { "skills.json", "templates.json", "glossary.json" })
{
    var doc = ParseJson(name);
    if (doc is not null && doc.RootElement.ValueKind != JsonValueKind.Array && name != "glossary.json")
        failures.Add($"{name}: expected an array");
    if (doc is not null && name == "glossary.json" && !doc.RootElement.TryGetProperty("entries", out _))
        failures.Add("glossary.json: missing entries");
}

var skipped = new[]
{
    "CompanionCrypto.swift HKDF/AES-GCM: Windows check does not decrypt; fixtures carry plaintext only",
    "DocumentJobs.swift SQLite3: no SQLite fixture emitted by A15",
    "ProcessLineReader.swift Darwin: no Darwin-only fixture",
    "ScrollFollowInputClassifier.swift CoreGraphics: no CoreGraphics fixture",
    "ZipformerStreaming.swift SherpaOnnx: no Sherpa fixture"
};
foreach (var reason in skipped) Console.WriteLine($"SKIP: {reason}");
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
    Console.Error.WriteLine($"MacCodec check: {failures.Count} failure(s)");
    return 1;
}
Console.WriteLine("MacCodec check: all pure-data A15 fixtures decoded; skipped families listed above.");
return 0;
