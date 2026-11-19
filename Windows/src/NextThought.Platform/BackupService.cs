using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record BackupImportResult(int SessionsAdded, int MaterialsAdded, int ConflictsSkipped, string SafetyBackup, string PreservedArchive, int WorkspacesAdded = 0, bool SettingsRestored = false);
public sealed class BackupService(AppPaths paths)
{
    private static readonly string[] PortableDirectories = ["Sessions", "Materials", "Migration", "Workspaces", "Originals", "Glossary"];
    private static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    public async Task ExportAsync(string destination, CancellationToken ct = default)
    {
        destination = Path.GetFullPath(destination); var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var leases = new List<FileStream>();
        try
        {
            var sessions = new SessionStore(paths); var records = sessions.List();
            foreach (var meta in records) { if (meta.EndedAt is null) throw new IOException("请先结束会议再备份。"); leases.Add(sessions.AcquireLease(meta.Id)); }
            var files = PortableDirectories.SelectMany(name => SafeFiles(Path.Combine(paths.Root, name)))
                .Where(f => !f.EndsWith(".active.lock", StringComparison.OrdinalIgnoreCase) && !Path.GetRelativePath(paths.Root, f).Split(Path.DirectorySeparatorChar).Any(n => n.StartsWith('.'))).ToList();
            // Settings and the glossary are part of a full backup; API keys live in DPAPI and never enter it.
            var settingsFile = Path.Combine(paths.Root, "settings.json");
            if (File.Exists(settingsFile)) files.Add(settingsFile);
            files = files.Where(f => !Path.GetFullPath(f).Equals(destination, StringComparison.OrdinalIgnoreCase)).ToList();
            if (files.Any(f => (File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0)) throw new IOException("备份不支持重解析点。");
            var hashes = new Dictionary<string, string>();
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested(); var name = Path.GetRelativePath(paths.Root, file).Replace('\\', '/');
                    hashes[name] = Hash(file); zip.CreateEntryFromFile(file, name, CompressionLevel.Fastest);
                }
                var manifest = new { format = "nextthought-backup", version = 1, kind = "full", createdAt = DateTimeOffset.UtcNow, appVersion = "0.2.0", appBuild = "windows-p2", includesSecrets = false,
                    workspaceCount = Directory.Exists(Path.Combine(paths.Root, "Workspaces")) ? Directory.GetFiles(Path.Combine(paths.Root, "Workspaces"), "*.json").Length : 0, sessionCount = records.Count, windowsExtensionVersion = 1, fileHashes = hashes };
                void AddText(string name, string text)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    hashes[name] = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    using var stream = zip.CreateEntry(name).Open();
                    stream.Write(bytes);
                }
                // Mac reads these root files; keep the Windows-only settings.json as well.
                AddText("settings.plist", System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(new SettingsStore(paths).Load())));
                AddText("skills.json", "[]");
                AddText("note-templates.json", "[]");
                var glossary = Path.Combine(paths.Root, "Glossary", "glossary.json");
                if (File.Exists(glossary)) AddText("glossary.json", await File.ReadAllTextAsync(glossary, ct).ConfigureAwait(false));
                using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); await writer.WriteAsync(JsonSerializer.Serialize(manifest, JsonDefaults.ArchiveOptions).AsMemory(), ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested(); File.Move(temporary, destination, true);
        }
        finally { foreach (var lease in leases) lease.Dispose(); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static IEnumerable<string> SafeFiles(string directory)
    {
        if (!Directory.Exists(directory)) yield break;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("备份不支持目录重解析点。");
        foreach (var file in Directory.EnumerateFiles(directory)) yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory)) foreach (var file in SafeFiles(child)) yield return file;
    }
    public async Task<BackupImportResult> ImportAsync(string archive, CancellationToken ct = default)
    {
        archive = Path.GetFullPath(archive); var staging = Path.Combine(paths.Root, ".restore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        var committed = new List<string>(); string safetyBackup = ""; int addedSessions = 0, addedMaterials = 0, conflicts = 0;
        try
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                if (zip.Entries.Count > 20000 || zip.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new InvalidDataException("备份超过导入上限（20000 文件 / 1 GB）。");
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries)
                {
                    ct.ThrowIfCancellationRequested(); var name = entry.FullName;
                    var segments = name.TrimEnd('/').Split('/');
                    if (name.Contains('\\') || segments.Any(s => s is "" or "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || s.EndsWith('.') || s.EndsWith(' ') || Regex.IsMatch(s, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase)) || !names.Add(name.TrimEnd('/')) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("备份包含非法、重复路径或符号链接。");
                    if (entry.Length > 200L * 1024 * 1024 || entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 500) throw new InvalidDataException("备份条目大小或压缩比异常。");
                    var target = Path.GetFullPath(Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("非法备份路径。");
                    if (name.EndsWith('/')) Directory.CreateDirectory(target);
                    else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target); }
                }
            }
            var manifests = Directory.GetFiles(staging, "manifest.json", SearchOption.AllDirectories).Where(f => Path.GetDirectoryName(f) == staging || Path.GetDirectoryName(Path.GetDirectoryName(f)) == staging).ToArray();
            if (manifests.Length != 1) throw new InvalidDataException("无法唯一确定备份根目录。");
            var root = Path.GetDirectoryName(manifests[0])!; using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifests[0], ct).ConfigureAwait(false)); var m = manifest.RootElement;
            if (m.GetProperty("format").GetString() is not ("nextthought-backup" or "interviewhacker-backup") || m.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("不支持的备份格式或版本。");
            if (m.GetProperty("includesSecrets").GetBoolean() || File.Exists(Path.Combine(root, "secrets.plist"))) throw new InvalidDataException("请从 Mac 重新导出不包含凭据的备份；当前导入不会接收 API Key。");
            if (m.TryGetProperty("windowsExtensionVersion", out var extension) && extension.GetInt32() != 1) throw new InvalidDataException("Windows 备份扩展版本过新。");
            if (m.TryGetProperty("fileHashes", out var hashes))
            {
                var actual = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => f != manifests[0]).Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).ToArray();
                if (hashes.EnumerateObject().Count() != actual.Length) throw new InvalidDataException("备份文件清单不完整。");
                foreach (var name in actual) if (!hashes.TryGetProperty(name, out var hash) || hash.GetString() != Hash(Path.Combine(root, name))) throw new InvalidDataException("备份文件校验失败。");
            }
            string[] Folders(string name) => Directory.Exists(Path.Combine(root, name)) ? Directory.GetDirectories(Path.Combine(root, name)) : [];
            var incomingSessions = Folders("Sessions");
            var workspaces = Directory.Exists(Path.Combine(root, "Workspaces")) ? Directory.GetFiles(Path.Combine(root, "Workspaces"), "*.json") : [];
            if (incomingSessions.Length != m.GetProperty("sessionCount").GetInt32() || workspaces.Length != m.GetProperty("workspaceCount").GetInt32()) throw new InvalidDataException("备份会议或场景数量不完整。");
            foreach (var folder in incomingSessions)
            {
                if (!Guid.TryParse(Path.GetFileName(folder), out var id)) throw new InvalidDataException("会议 ID 无效。");
                var meta = JsonSerializer.Deserialize<SessionMeta>(await File.ReadAllTextAsync(Path.Combine(folder, "meta.json"), ct).ConfigureAwait(false), JsonDefaults.Options)!;
                if (meta.Id != id) throw new InvalidDataException("会议目录与元数据 ID 不一致。");
                foreach (var name in new[] { "transcript.jsonl", "answers.jsonl", "voices.jsonl", "translations.jsonl" })
                    if (File.Exists(Path.Combine(folder, name))) foreach (var line in File.ReadLines(Path.Combine(folder, name))) if (!string.IsNullOrWhiteSpace(line)) { using var valid = JsonDocument.Parse(line); }
            }
            foreach (var file in workspaces) { using var workspace = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct)); if (!Guid.TryParse(workspace.RootElement.GetProperty("id").GetString(), out _)) throw new InvalidDataException("Mac 场景 ID 无效。"); }
            var originals = Path.Combine(root, "Originals");
            if (Directory.Exists(originals)) foreach (var file in Directory.GetFiles(originals)) if (!Regex.IsMatch(Path.GetFileNameWithoutExtension(file), "^[a-fA-F0-9]{64}$") || !Hash(file).Equals(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Mac 原文件哈希无效。");
            var incomingMaterials = new List<string>();
            var materialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in Folders("Materials"))
            {
                if (File.Exists(Path.Combine(folder, "document.json")))
                {
                    var document = JsonSerializer.Deserialize<MaterialDocument>(await File.ReadAllTextAsync(Path.Combine(folder, "document.json"), ct), JsonDefaults.Options)!;
                    if (!Guid.TryParse(document.Id, out _) || Path.GetFileName(folder) != document.Id || !DocumentReader.Extensions.Contains(document.Extension) || Hash(Path.Combine(folder, "source" + document.Extension)) != document.Sha256) throw new InvalidDataException("资料原文件或 ID 校验失败。");
                    if (materialIds.Add(document.Id)) incomingMaterials.Add(folder);
                    continue;
                }
                // Mac layout: Materials/<series>/<documentID>.md, with no Windows document.json.
                foreach (var markdown in Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly))
                {
                    if (!Guid.TryParse(Path.GetFileNameWithoutExtension(markdown), out var parsed)) throw new InvalidDataException("Mac 资料文件名不是有效 ID。");
                    var id = parsed.ToString("N");
                    if (!materialIds.Add(id)) continue;
                    var text = await File.ReadAllTextAsync(markdown, ct).ConfigureAwait(false);
                    var convertedFolder = Path.Combine(staging, "converted", id);
                    Directory.CreateDirectory(convertedFolder);
                    var source = Path.Combine(convertedFolder, "source.md");
                    await File.WriteAllTextAsync(source, text, ct).ConfigureAwait(false);
                    var converted = new MaterialDocument
                    {
                        Id = id,
                        Name = "Mac 资料 " + id[..8],
                        Extension = ".md",
                        Sha256 = Hash(source),
                        Blocks = [new DocumentBlock("Mac 归档", text, 1)],
                        Warnings = ["位置来自 Mac 的 Materials 归档；原始备份已保留在 Migration/ 中。"]
                    };
                    await File.WriteAllTextAsync(Path.Combine(convertedFolder, "document.json"), JsonSerializer.Serialize(converted, JsonDefaults.Options), ct).ConfigureAwait(false);
                    incomingMaterials.Add(convertedFolder);
                }
            }
            foreach (var file in workspaces)
            {
                using var workspace = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct));
                if (!workspace.RootElement.TryGetProperty("documents", out var documents)) continue;
                foreach (var document in documents.EnumerateArray())
                {
                    var id = Guid.Parse(document.GetProperty("id").GetString()!).ToString("N");
                    if (!document.TryGetProperty("pages", out var pages) || pages.GetArrayLength() == 0 || incomingMaterials.Any(f => Path.GetFileName(f) == id)) continue;
                    var folder = Path.Combine(staging, "converted", id); Directory.CreateDirectory(folder);
                    var blocks = pages.EnumerateArray().Select((p, i) => new DocumentBlock($"Mac 存档第 {i + 1} 页", p.GetString() ?? "", i + 1)).ToList();
                    await File.WriteAllTextAsync(Path.Combine(folder, "source.md"), string.Join("\n\n", blocks.Select(b => "## " + b.Location + "\n" + b.Text)), ct);
                    var converted = new MaterialDocument { Id = id, Name = (document.GetProperty("name").GetString() ?? "Mac 资料") + "（迁移文本）", Extension = ".md", Sha256 = Hash(Path.Combine(folder, "source.md")), Blocks = blocks,
                        Warnings = ["位置来自 Mac 存档页序；此处打开的是迁移文本。原 PDF、Office、场景设置和其他 Mac 数据完整保存在 Migration 原始备份中，尚未映射全部 Mac 界面功能。"] };
                    await File.WriteAllTextAsync(Path.Combine(folder, "document.json"), JsonSerializer.Serialize(converted, JsonDefaults.Options), ct); incomingMaterials.Add(folder);
                }
            }
            Directory.CreateDirectory(Path.Combine(paths.Root, "Backups")); safetyBackup = Path.Combine(paths.Root, "Backups", "before-import-" + Guid.NewGuid().ToString("N") + ".ntbackup");
            await ExportAsync(safetyBackup, ct).ConfigureAwait(false);
            void CommitFolder(string folder, string destination)
            {
                ct.ThrowIfCancellationRequested(); if (Directory.Exists(destination)) { conflicts++; return; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); Directory.Move(folder, destination); committed.Add(destination);
            }
            foreach (var folder in incomingSessions) { var before = committed.Count; CommitFolder(folder, Path.Combine(paths.Sessions, Path.GetFileName(folder).ToUpperInvariant())); if (committed.Count > before) addedSessions++; }
            foreach (var folder in incomingMaterials) { var before = committed.Count; CommitFolder(folder, Path.Combine(paths.Root, "Materials", Path.GetFileName(folder))); if (committed.Count > before) addedMaterials++; }
            // Mac series files are per-workspace JSON; merge them instead of discarding them in Migration.
            var addedWorkspaces = 0;
            if (Directory.Exists(Path.Combine(root, "Workspaces")))
            {
                var importedWorkspaces = new List<WorkspaceSeries>();
                foreach (var file in Directory.GetFiles(Path.Combine(root, "Workspaces"), "*.json"))
                {
                    try
                    {
                        if (JsonSerializer.Deserialize<WorkspaceSeries>(await File.ReadAllTextAsync(file, ct), new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true,
                            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                        }) is { } series) importedWorkspaces.Add(series);
                    }
                    catch (JsonException) { }
                }
                if (importedWorkspaces.Count > 0)
                {
                    var store = new WorkspaceStore(paths.Root);
                    var current = store.Load();
                    var merged = BackupMerge.Workspaces(current.Series, importedWorkspaces);
                    if (merged.Added > 0 || merged.Updated > 0)
                    {
                        store.Save(new WorkspaceData(merged.Merged));
                        addedWorkspaces = merged.Added + merged.Updated;
                    }
                }
            }
            // Restore non-secret settings from our own backups; the API key stays in DPAPI.
            var settingsRestored = false;
            var settingsFile = Path.Combine(root, "settings.json");
            var settingsPlist = Path.Combine(root, "settings.plist");
            try
            {
                AppSettings? importedSettings = null;
                if (File.Exists(settingsFile))
                    importedSettings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(settingsFile, ct), JsonDefaults.Options);
                else if (File.Exists(settingsPlist))
                    importedSettings = MacSettingsPlist.Apply(await File.ReadAllBytesAsync(settingsPlist, ct), new SettingsStore(paths).Load());
                if (importedSettings is not null)
                {
                    await new SettingsStore(paths).SaveAsync(importedSettings, ct).ConfigureAwait(false);
                    settingsRestored = true;
                }
            }
            catch (JsonException) { }
            var glossarySource = Path.Combine(root, "Glossary", "glossary.json");
            if (!File.Exists(glossarySource)) glossarySource = Path.Combine(root, "glossary.json");
            if (File.Exists(glossarySource))
            {
                try
                {
                    var glossaryTarget = Path.Combine(paths.Root, "Glossary");
                    Directory.CreateDirectory(glossaryTarget);
                    File.Copy(glossarySource, Path.Combine(glossaryTarget, "glossary.json"), true);
                }
                catch (IOException) { }
            }
            // Preserve the complete source archive, including Mac-only fields, workspaces, skills, notes, and originals.
            var preserved = Path.Combine(paths.Root, "Migration", Hash(archive) + ".ntbackup"); Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
            if (!File.Exists(preserved)) { File.Copy(archive, preserved); committed.Add(preserved); }
            return new(addedSessions, addedMaterials, conflicts, safetyBackup, preserved, addedWorkspaces, settingsRestored);
        }
        catch
        {
            foreach (var path in committed.AsEnumerable().Reverse()) { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); }
            throw;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
