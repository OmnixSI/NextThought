using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record BackupImportResult(
    int SessionsAdded, int MaterialsAdded, int ConflictsSkipped, string SafetyBackup, string PreservedArchive,
    int WorkspacesAdded = 0, bool SettingsRestored = false,
    int SessionsIdentical = 0, int SessionsReplaced = 0, int SessionsKept = 0,
    int TemplatesAdded = 0, int TemplatesReplaced = 0, int TemplatesKept = 0);

/// <summary>One export archive or recovery point, as DataBackup.listBackups reports it.</summary>
public sealed record BackupFile(string Path, DateTimeOffset Date, long Size, string Reason)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public bool IsRecoveryPoint =>
        Path.EndsWith(".ntrestore", StringComparison.OrdinalIgnoreCase)
        || Path.EndsWith(".ihrestore", StringComparison.OrdinalIgnoreCase);

    /// <summary>DataMaintenanceAdvanced.reasonTitle: the Mac original sentences.</summary>
    public string ReasonTitle => Reason switch
    {
        "daily" => "旧每日备份",
        "session" => "会议结束",
        "update" => "更新前",
        "import" => "导入前",
        "manual" => "手动",
        "" => "导出备份",
        _ => Reason
    };
}

/// <summary>DataBackup.SessionImport: one archived meeting of a prepared import (status + fingerprints).</summary>
public sealed record PreparedSessionImport(
    Guid Id, string Title, BackupImportStatus Status, string? LocalFingerprint, string IncomingFingerprint);

/// <summary>DataBackup.TemplateImport: one custom template of a prepared import.</summary>
public sealed record PreparedTemplateImport(string Id, string Title, BackupImportStatus Status);

/// <summary>
/// DataBackup.PreparedImport: the validated staging copy awaiting the review page. Nothing in the
/// user's data directory changes until <see cref="BackupService.ApplyPreparedImportAsync"/> runs.
/// </summary>
public sealed class PreparedBackupImport : IDisposable
{
    internal PreparedBackupImport(string staging, string root, BackupManifest manifest,
        IReadOnlyList<PreparedSessionImport> sessions, IReadOnlyList<PreparedTemplateImport> templates,
        IReadOnlyList<NotesTemplate> incomingTemplates, IReadOnlyList<string> warnings, string source)
    {
        Staging = staging; Root = root; Manifest = manifest; Sessions = sessions; Templates = templates;
        IncomingTemplates = incomingTemplates; Warnings = warnings; Source = source;
    }

    public string Staging { get; }
    public string Root { get; }
    public string Source { get; }
    public BackupManifest Manifest { get; }
    public IReadOnlyList<PreparedSessionImport> Sessions { get; }
    public IReadOnlyList<PreparedTemplateImport> Templates { get; }
    public IReadOnlyList<NotesTemplate> IncomingTemplates { get; }
    public IReadOnlyList<string> Warnings { get; }

    public int NewSessions => Sessions.Count(session => session.Status == BackupImportStatus.New);
    public int IdenticalSessions => Sessions.Count(session => session.Status == BackupImportStatus.Identical);
    public int ConflictSessions => Sessions.Count(session => session.Status == BackupImportStatus.Conflict);

    public void Discard()
    {
        try { if (Directory.Exists(Staging)) Directory.Delete(Staging, true); } catch { }
    }

    public void Dispose() => Discard();
}

public sealed class BackupService(AppPaths paths)
{
    private static readonly string[] PortableDirectories = ["Sessions", "Materials", "Migration", "Workspaces", "Originals", "Glossary"];
    private static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(stream)); }

    public string BackupsRoot => Path.Combine(paths.Root, "Backups");

    public async Task ExportAsync(string destination, CancellationToken ct = default)
    {
        destination = Path.GetFullPath(destination); var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var leases = new List<FileStream>();
        try
        {
            var sessions = new SessionStore(paths); var records = sessions.List();
            foreach (var meta in records) { if (meta.EndedAt is null) throw new IOException("请先结束会议再备份。"); leases.Add(sessions.AcquireLease(meta.Id)); }
            var files = PortableDirectories.SelectMany(name => SafeFiles(Path.Combine(paths.Root, name)))
                .Where(f => !f.EndsWith(".active.lock", StringComparison.OrdinalIgnoreCase) && !Path.GetRelativePath(paths.Root, f).Split(Path.DirectorySeparatorChar).Any(n => n.StartsWith('.')))
                // A12: Windows-only derivations never enter a Mac-readable backup.
                .Where(f => !IsWindowsOnlyBackupFile(Path.GetFileName(f)))
                .ToList();
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
                var manifest = new { format = "nextthought-backup", version = 1, kind = "full", createdAt = DateTimeOffset.UtcNow, appVersion = ProductVersion.Current, appBuild = "windows", includesSecrets = false,
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
                // Preserve real skills/templates when the data directory has them; empty otherwise.
                var skillsFile = Path.Combine(paths.Root, "skills.json");
                AddText("skills.json", File.Exists(skillsFile) ? await File.ReadAllTextAsync(skillsFile, ct).ConfigureAwait(false) : "[]");
                var templatesFile = Path.Combine(paths.Root, "note-templates.json");
                AddText("note-templates.json", File.Exists(templatesFile) ? await File.ReadAllTextAsync(templatesFile, ct).ConfigureAwait(false) : "[]");
                var glossary = Path.Combine(paths.Root, "Glossary", "glossary.json");
                if (File.Exists(glossary)) AddText("glossary.json", await File.ReadAllTextAsync(glossary, ct).ConfigureAwait(false));
                using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); await writer.WriteAsync(JsonSerializer.Serialize(manifest, JsonDefaults.ArchiveOptions).AsMemory(), ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested(); File.Move(temporary, destination, true);
        }
        finally { foreach (var lease in leases) lease.Dispose(); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool IsWindowsOnlyBackupFile(string name) =>
        name.StartsWith("structured-", StringComparison.OrdinalIgnoreCase)
            ? name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            : name.Equals("corrections.json", StringComparison.OrdinalIgnoreCase)
              || name.Equals("summary-job.json", StringComparison.OrdinalIgnoreCase)
              || name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SafeFiles(string directory)
    {
        if (!Directory.Exists(directory)) yield break;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("备份不支持目录重解析点。");
        foreach (var file in Directory.EnumerateFiles(directory)) yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory)) foreach (var file in SafeFiles(child)) yield return file;
    }

    // MARK: Recovery points (DataBackup.automaticBackup / listBackups)

    /// <summary>DataBackup.listBackups: export archives and recovery points, newest first.</summary>
    public IReadOnlyList<BackupFile> ListBackups()
    {
        var files = new List<BackupFile>();
        foreach (var point in new IncrementalBackupStore(BackupsRoot).List())
            files.Add(new BackupFile(point.Path, point.CreatedAt, point.Size, point.Reason));
        if (Directory.Exists(BackupsRoot))
        {
            foreach (var file in Directory.EnumerateFiles(BackupsRoot, "*", SearchOption.TopDirectoryOnly))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".ntbackup" or ".ihbackup")) continue;
                var info = new FileInfo(file);
                files.Add(new BackupFile(file, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), info.Length,
                    info.Name.StartsWith("before-import-", StringComparison.OrdinalIgnoreCase) ? "import" : ""));
            }
        }
        return files.OrderByDescending(file => file.Date).ToList();
    }

    /// <summary>
    /// DataBackup.automaticBackup: one content-addressed recovery point of settings, knowledge,
    /// workspaces, sessions and originals. The manifest date is fixed so an unchanged data set
    /// deduplicates to the existing point.
    /// </summary>
    public async Task<BackupFile> CreateRecoveryPointAsync(string reason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        files["settings.plist"] = MacSettingsPlist.Write(new SettingsStore(paths).Load());
        var skillsFile = Path.Combine(paths.Root, "skills.json");
        if (File.Exists(skillsFile)) files["skills.json"] = await File.ReadAllBytesAsync(skillsFile, ct).ConfigureAwait(false);
        var glossary = new GlossaryStore(paths.Root).Load();
        if (glossary.Entries.Count > 0)
            files["glossary.json"] = JsonSerializer.SerializeToUtf8Bytes(glossary, JsonDefaults.ArchiveOptions);
        files["note-templates.json"] = JsonSerializer.SerializeToUtf8Bytes(new NotesTemplateStore(paths.Root).Load().ToList(), JsonDefaults.ArchiveOptions);
        var workspaceCount = 0;
        var workspaceDirectory = Path.Combine(paths.Root, "Workspaces");
        if (Directory.Exists(workspaceDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(workspaceDirectory, "*.json"))
            {
                if (Path.GetFileName(file).StartsWith(".", StringComparison.Ordinal)) continue;
                files["Workspaces/" + Path.GetFileName(file)] = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                workspaceCount++;
            }
        }
        var sessionCount = Directory.Exists(paths.Sessions)
            ? Directory.EnumerateDirectories(paths.Sessions).Count(folder => Guid.TryParse(Path.GetFileName(folder), out _))
            : 0;
        // DataBackup.automaticBackup: the point has its own date; a volatile archive date must not
        // defeat deduplication.
        var manifest = new BackupManifest
        {
            Kind = BackupManifest.BackupKind.Full,
            CreatedAt = DateTimeOffset.UnixEpoch,
            AppVersion = ProductVersion.Current,
            AppBuild = "",
            IncludesSecrets = false,
            WorkspaceCount = workspaceCount,
            SessionCount = sessionCount
        };
        files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.ArchiveOptions);
        var directories = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sessions"] = paths.Sessions,
            ["Originals"] = Path.Combine(paths.Root, "Originals")
        };
        var point = await Task.Run(() => new IncrementalBackupStore(BackupsRoot).Create(files, directories, reason), ct)
            .ConfigureAwait(false);
        var info = new FileInfo(point);
        return new BackupFile(point, DateTimeOffset.UtcNow, info.Exists ? info.Length : 0, reason);
    }

    /// <summary>E30: removes one recovery point; export archives are deleted by the user.</summary>
    public void DeleteRecoveryPoint(BackupFile backup)
    {
        if (!backup.IsRecoveryPoint) throw new InvalidOperationException("只能删除自动恢复点；导出的备份请手动管理。");
        new IncrementalBackupStore(BackupsRoot).Delete(backup.Path);
    }

    // MARK: Import (DataBackup.prepareImport / applyImport)

    /// <summary>
    /// DataBackup.prepareImport: writes only to an owned staging directory, validates the whole
    /// archive and plans the merge, so the review page can show new/identical/conflict entries.
    /// </summary>
    public async Task<PreparedBackupImport> PrepareImportAsync(string archive, CancellationToken ct = default)
    {
        archive = Path.GetFullPath(archive);
        var staging = Path.Combine(paths.Root, ".restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            if (archive.EndsWith(".ntrestore", StringComparison.OrdinalIgnoreCase)
                || archive.EndsWith(".ihrestore", StringComparison.OrdinalIgnoreCase))
                new IncrementalBackupStore(BackupsRoot).Restore(archive, staging);
            else
                await Task.Run(() => ExtractArchive(archive, staging, ct), ct).ConfigureAwait(false);
            var root = LocateRoot(staging);
            var manifestPath = FindManifest(root);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false), JsonDefaults.Options)
                           ?? throw new InvalidDataException("备份清单损坏。");
            if (manifest.Format is not ("nextthought-backup" or "interviewhacker-backup") || manifest.Version != 1)
                throw new InvalidDataException("不支持的备份格式或版本。");
            manifest.Validate();
            await Task.Run(() => ExtractArchiveValidation(root, manifestPath, manifest, ct), ct).ConfigureAwait(false);

            var (sessions, templates, incomingTemplates) = await Task.Run(() =>
            {
                var localRoot = paths.Sessions;
                var sessionPlans = new List<PreparedSessionImport>();
                var directory = Path.Combine(root, "Sessions");
                if (Directory.Exists(directory))
                {
                    foreach (var folder in Directory.EnumerateDirectories(directory))
                    {
                        if (!Guid.TryParse(Path.GetFileName(folder), out var id)) continue;
                        var local = Path.Combine(localRoot, id.ToString().ToUpperInvariant());
                        string? localFingerprint = Directory.Exists(local) ? BackupContent.Fingerprint(local) : null;
                        var incoming = BackupContent.Fingerprint(folder);
                        sessionPlans.Add(new PreparedSessionImport(id, ReadSessionTitle(folder), BackupContent.Status(localFingerprint, incoming), localFingerprint, incoming));
                    }
                }
                sessionPlans = sessionPlans
                    .OrderBy(session => session.Title, StringComparer.CurrentCulture)
                    .ThenBy(session => session.Id)
                    .ToList();
                var currentTemplates = new NotesTemplateStore(paths.Root).Load();
                var incomingTemplates = ReadTemplates(root);
                var templatePlans = incomingTemplates
                    .Where(template => !template.IsBuiltIn)
                    .Select(template =>
                    {
                        var current = currentTemplates.FirstOrDefault(existing => SameId(existing.Id, template.Id));
                        return new PreparedTemplateImport(template.Id, template.Name,
                            current is null ? BackupImportStatus.New : current == template ? BackupImportStatus.Identical : BackupImportStatus.Conflict);
                    })
                    .ToList();
                return (sessionPlans, templatePlans, incomingTemplates);
            }, ct).ConfigureAwait(false);

            var warnings = new List<string>();
            if (manifest.IncludesSecrets) warnings.Add("备份包含 API Key 等凭据；Windows 不会导入密钥，原文件保留在迁移存档里。");
            var prepared = new PreparedBackupImport(staging, root, manifest, sessions, templates, incomingTemplates, warnings, archive);
            return prepared;
        }
        catch
        {
            try { Directory.Delete(staging, true); } catch { }
            throw;
        }
    }

    /// <summary>
    /// DataBackup.applyImport: re-checks the reviewed plan, creates the pre-apply recovery point
    /// and merges the chosen content. A failure rolls back everything this call committed.
    /// </summary>
    public async Task<BackupImportResult> ApplyPreparedImportAsync(PreparedBackupImport prepared,
        IReadOnlySet<Guid>? useBackupSessions, IReadOnlySet<string>? useBackupTemplates, bool applySettings,
        CancellationToken ct = default)
    {
        var legacy = useBackupSessions is null;
        var sessionsToUse = useBackupSessions ?? new HashSet<Guid>();
        var templatesToUse = useBackupTemplates ?? new HashSet<string>();
        // DataBackup.applyImport: the preview's local fingerprints must still match.
        await Task.Run(() =>
        {
            foreach (var session in prepared.Sessions)
            {
                if (session.LocalFingerprint is null) continue;
                var local = Path.Combine(paths.Sessions, session.Id.ToString().ToUpperInvariant());
                var now = Directory.Exists(local) ? BackupContent.Fingerprint(local) : null;
                if (now != session.LocalFingerprint)
                    throw new InvalidDataException("预览后本机记录有变化，请重新导入以查看最新差异。");
            }
        }, ct).ConfigureAwait(false);

        // The review page says a local recovery point is created before applying.
        var safety = await CreateRecoveryPointAsync("import", ct).ConfigureAwait(false);

        var committed = new List<string>(); var undo = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var replacedFolders = new List<(string Previous, string Target)>();
        int addedSessions = 0, replacedSessions = 0, keptSessions = 0, identicalSessions = 0, conflicts = 0;
        try
        {
            var root = prepared.Root;
            Directory.CreateDirectory(paths.Sessions);

            // Sessions: identical rows are skipped; a conflicting row is only replaced when the
            // review page selected the backup copy (programmatic callers keep this Mac's copy).
            foreach (var session in prepared.Sessions)
            {
                ct.ThrowIfCancellationRequested();
                if (session.Status == BackupImportStatus.Identical)
                {
                    if (legacy) conflicts++; else identicalSessions++;
                    continue;
                }
                if (session.Status == BackupImportStatus.Conflict && !sessionsToUse.Contains(session.Id))
                {
                    if (legacy) conflicts++; else keptSessions++;
                    continue;
                }
                var incoming = FindSessionFolder(root, session.Id) ?? throw new InvalidDataException("备份会议目录缺失。");
                var target = Path.Combine(paths.Sessions, session.Id.ToString().ToUpperInvariant());
                if (Directory.Exists(target))
                {
                    var previous = target + ".nt-replaced-" + Guid.NewGuid().ToString("N");
                    Directory.Move(target, previous);
                    replacedFolders.Add((previous, target));
                }
                Directory.Move(incoming, target);
                committed.Add(target);
                if (session.Status == BackupImportStatus.Conflict) replacedSessions++; else addedSessions++;
            }

            // Originals (content-addressed source files) are shared and never overwritten.
            var originals = Path.Combine(root, "Originals");
            if (Directory.Exists(originals))
            {
                var originalsTarget = Path.Combine(paths.Root, "Originals");
                Directory.CreateDirectory(originalsTarget);
                foreach (var file in Directory.GetFiles(originals))
                {
                    if (Path.GetFileName(file).StartsWith("._", StringComparison.Ordinal)) continue;
                    var target = Path.Combine(originalsTarget, Path.GetFileName(file));
                    if (File.Exists(target)) continue;
                    File.Copy(file, target);
                    committed.Add(target);
                }
            }

            var incomingMaterials = new List<string>();
            var materialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in SafeDirectories(Path.Combine(root, "Materials")))
            {
                if (File.Exists(Path.Combine(folder, "document.json")))
                {
                    var document = JsonSerializer.Deserialize<MaterialDocument>(await File.ReadAllTextAsync(Path.Combine(folder, "document.json"), ct).ConfigureAwait(false), JsonDefaults.Options)!;
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
                    var convertedFolder = Path.Combine(prepared.Staging, "converted", id);
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
            // Workspace documents that only exist as pages (older Mac archives) get a text material.
            var workspacesDirectory = Path.Combine(root, "Workspaces");
            string[] workspaceFiles = Directory.Exists(workspacesDirectory) ? Directory.GetFiles(workspacesDirectory, "*.json") : Array.Empty<string>();
            foreach (var file in workspaceFiles)
            {
                using var workspace = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false));
                if (!workspace.RootElement.TryGetProperty("documents", out var documents)) continue;
                foreach (var document in documents.EnumerateArray())
                {
                    var id = Guid.Parse(document.GetProperty("id").GetString()!).ToString("N");
                    if (!document.TryGetProperty("pages", out var pages) || pages.GetArrayLength() == 0 || incomingMaterials.Any(f => Path.GetFileName(f) == id)) continue;
                    var folder = Path.Combine(prepared.Staging, "converted", id); Directory.CreateDirectory(folder);
                    var blocks = pages.EnumerateArray().Select((p, i) => new DocumentBlock($"Mac 存档第 {i + 1} 页", p.GetString() ?? "", i + 1)).ToList();
                    await File.WriteAllTextAsync(Path.Combine(folder, "source.md"), string.Join("\n\n", blocks.Select(b => "## " + b.Location + "\n" + b.Text)), ct).ConfigureAwait(false);
                    var converted = new MaterialDocument
                    {
                        Id = id,
                        Name = (document.GetProperty("name").GetString() ?? "Mac 资料") + "（迁移文本）",
                        Extension = ".md",
                        Sha256 = Hash(Path.Combine(folder, "source.md")),
                        Blocks = blocks,
                        Warnings = ["位置来自 Mac 存档页序；此处打开的是迁移文本。原 PDF、Office、场景设置和其他 Mac 数据完整保存在 Migration 原始备份中，尚未映射全部 Mac 界面功能。"]
                    };
                    await File.WriteAllTextAsync(Path.Combine(folder, "document.json"), JsonSerializer.Serialize(converted, JsonDefaults.Options), ct).ConfigureAwait(false);
                    incomingMaterials.Add(folder);
                }
            }
            var addedMaterials = 0;
            Directory.CreateDirectory(Path.Combine(paths.Root, "Materials"));
            foreach (var folder in incomingMaterials)
            {
                ct.ThrowIfCancellationRequested();
                var destination = Path.Combine(paths.Root, "Materials", Path.GetFileName(folder));
                if (Directory.Exists(destination)) { conflicts++; continue; }
                Directory.Move(folder, destination);
                committed.Add(destination);
                addedMaterials++;
            }

            // Mac series files are per-workspace JSON; merge them instead of discarding them in Migration.
            var addedWorkspaces = 0;
            if (Directory.Exists(workspacesDirectory))
            {
                var importedWorkspaces = new List<WorkspaceSeries>();
                foreach (var file in Directory.GetFiles(workspacesDirectory, "*.json"))
                {
                    try
                    {
                        if (JsonSerializer.Deserialize<WorkspaceSeries>(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false), WorkspaceStore.Options) is { } series)
                            importedWorkspaces.Add(series);
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

            // A13: settings, glossary, skills and templates merge into the current data and roll back together.
            void Remember(string path)
            {
                if (!undo.ContainsKey(path)) undo[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            var settingsRestored = false;
            if (applySettings)
            {
                var settingsFile = Path.Combine(root, "settings.json");
                var settingsPlist = Path.Combine(root, "settings.plist");
                try
                {
                    var currentSettings = new SettingsStore(paths).Load();
                    AppSettings? mergedSettings = null;
                    if (File.Exists(settingsFile))
                    {
                        var importedNode = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(settingsFile, ct).ConfigureAwait(false)) as System.Text.Json.Nodes.JsonObject;
                        if (importedNode is not null)
                        {
                            var current = JsonSerializer.SerializeToNode(currentSettings, JsonDefaults.Options) as System.Text.Json.Nodes.JsonObject
                                          ?? new System.Text.Json.Nodes.JsonObject();
                            foreach (var (key, value) in importedNode.ToList())
                            {
                                if (value is null) continue;
                                current[key] = value.DeepClone();
                            }
                            mergedSettings = MacPreferenceMap.WithoutTypedShapeConflicts(current).Deserialize<AppSettings>(JsonDefaults.Options);
                            // G5: settings.json stores the Mac preference key names; apply them to the
                            // typed settings so an imported Windows/Mac backup restores them.
                            if (mergedSettings is not null) MacPreferenceMap.ApplyJson(mergedSettings, current);
                        }
                    }
                    else if (File.Exists(settingsPlist))
                    {
                        mergedSettings = MacSettingsPlist.Apply(await File.ReadAllBytesAsync(settingsPlist, ct).ConfigureAwait(false), currentSettings);
                    }
                    if (mergedSettings is not null)
                    {
                        Remember(paths.Settings);
                        await new SettingsStore(paths).SaveAsync(mergedSettings, ct).ConfigureAwait(false);
                        settingsRestored = true;
                    }
                }
                catch (JsonException) { }
            }

            var glossarySource = Path.Combine(root, "Glossary", "glossary.json");
            if (!File.Exists(glossarySource)) glossarySource = Path.Combine(root, "glossary.json");
            if (File.Exists(glossarySource))
            {
                try
                {
                    var incoming = JsonSerializer.Deserialize<Glossary>(await File.ReadAllTextAsync(glossarySource, ct).ConfigureAwait(false), JsonDefaults.Options);
                    if (incoming is not null)
                    {
                        var store = new GlossaryStore(paths.Root);
                        var current = store.Load();
                        var merged = new List<GlossaryEntry>(current.Entries);
                        foreach (var entry in incoming.Entries)
                        {
                            if (entry.Canonical.Trim().Length == 0) continue;
                            var index = merged.FindIndex(existing => string.Equals(existing.Canonical, entry.Canonical, StringComparison.OrdinalIgnoreCase));
                            if (index >= 0) merged[index] = entry; else merged.Add(entry);
                        }
                        var target = Path.Combine(paths.Root, "Glossary", "glossary.json");
                        Remember(target);
                        store.Save(new Glossary(merged));
                    }
                }
                catch (JsonException) { }
            }
            var skillsSource = Path.Combine(root, "skills.json");
            if (File.Exists(skillsSource))
            {
                try
                {
                    var incoming = JsonSerializer.Deserialize<List<Skill>>(await File.ReadAllTextAsync(skillsSource, ct).ConfigureAwait(false), JsonDefaults.Options);
                    if (incoming is not null)
                    {
                        var store = new SkillStore(paths.Root);
                        var merged = store.Load().ToList();
                        foreach (var skill in incoming)
                        {
                            if (string.IsNullOrWhiteSpace(skill.Name)) continue;
                            var index = merged.FindIndex(existing => existing.Id == skill.Id
                                || string.Equals(existing.Name, skill.Name, StringComparison.Ordinal));
                            if (index >= 0) merged[index] = skill; else merged.Add(skill);
                        }
                        Remember(Path.Combine(paths.Root, "skills.json"));
                        store.Save(merged);
                    }
                }
                catch (JsonException) { }
            }
            // Templates are user data, independent of the preference-import switch; conflicts are
            // resolved from the review page like DataBackup.applyImport.
            var templateMerge = BackupMerge.NotesTemplates(new NotesTemplateStore(paths.Root).Load(), prepared.IncomingTemplates, templatesToUse);
            if (prepared.IncomingTemplates.Count > 0)
            {
                Remember(Path.Combine(paths.Root, "note-templates.json"));
                new NotesTemplateStore(paths.Root).Save(templateMerge.Templates);
            }

            // Preserve the complete source archive, including Mac-only fields, workspaces, skills, notes, and originals.
            var preserved = Path.Combine(paths.Root, "Migration", Hash(prepared.Source) + ".ntbackup"); Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
            if (!File.Exists(preserved)) { File.Copy(prepared.Source, preserved); committed.Add(preserved); }
            return new BackupImportResult(addedSessions, addedMaterials, conflicts, safety.Path, preserved, addedWorkspaces, settingsRestored,
                identicalSessions, replacedSessions, keptSessions, templateMerge.Added, templateMerge.Replaced, templateMerge.Kept);
        }
        catch
        {
            foreach (var path in committed.AsEnumerable().Reverse()) { try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
            foreach (var (previous, target) in replacedFolders.AsEnumerable().Reverse())
            {
                try
                {
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                    if (Directory.Exists(previous)) Directory.Move(previous, target);
                }
                catch (IOException) { }
            }
            foreach (var (path, original) in undo)
            {
                try
                {
                    if (original is null) { if (File.Exists(path)) File.Delete(path); }
                    else File.WriteAllBytes(path, original);
                }
                catch (IOException) { }
            }
            throw;
        }
        finally
        {
            // Successfully replaced copies are no longer needed.
            foreach (var (previous, _) in replacedFolders) { try { if (Directory.Exists(previous)) Directory.Delete(previous, true); } catch (IOException) { } }
        }
    }

    /// <summary>Programmatic import (CLI, tests and older callers): keep this Mac's conflicts.</summary>
    public async Task<BackupImportResult> ImportAsync(string archive, CancellationToken ct = default)
    {
        using var prepared = await PrepareImportAsync(archive, ct).ConfigureAwait(false);
        return await ApplyPreparedImportAsync(prepared, null, null, applySettings: true, ct).ConfigureAwait(false);
    }

    // MARK: Helpers

    private void ExtractArchive(string archive, string staging, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 100000 || zip.Entries.Sum(entry => entry.Length) > 64L * 1024 * 1024 * 1024) throw new InvalidDataException("备份超过导入上限（100000 文件 / 64 GB）。");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested(); var name = entry.FullName;
            var segments = name.TrimEnd('/').Split('/');
            if (name.Contains('\\') || segments.Any(segment => segment is "" or "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.EndsWith('.') || segment.EndsWith(' ') || Regex.IsMatch(segment, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase)) || !names.Add(name.TrimEnd('/')) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("备份包含非法、重复路径或符号链接。");
            if (entry.Length > 200L * 1024 * 1024 || entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 500) throw new InvalidDataException("备份条目大小或压缩比异常。");
            var target = Path.GetFullPath(Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("非法备份路径。");
            if (name.EndsWith('/')) Directory.CreateDirectory(target);
            else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target); }
        }
    }

    private static string LocateRoot(string staging)
    {
        if (File.Exists(Path.Combine(staging, "manifest.json"))) return staging;
        foreach (var child in Directory.EnumerateDirectories(staging))
            if (File.Exists(Path.Combine(child, "manifest.json"))) return child;
        throw new InvalidDataException("无法唯一确定备份根目录。");
    }

    private static string FindManifest(string root)
    {
        var direct = Path.Combine(root, "manifest.json");
        if (File.Exists(direct)) return direct;
        var candidates = Directory.GetFiles(root, "manifest.json", SearchOption.AllDirectories);
        if (candidates.Length != 1) throw new InvalidDataException("无法唯一确定备份根目录。");
        return candidates[0];
    }

    /// <summary>All semantic checks precede the merge: a corrupt archive changes nothing.</summary>
    private static void ExtractArchiveValidation(string root, string manifestPath, BackupManifest manifest, CancellationToken ct)
    {
        var manifestFile = Path.GetFullPath(manifestPath);
        var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(file => file != manifestFile).ToArray();
        // A13: credential-bearing archives are accepted; the secrets stay sealed in Migration/.
        var manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using (manifestJson)
        {
            if (manifestJson.RootElement.TryGetProperty("windowsExtensionVersion", out var extension) && extension.GetInt32() != 1)
                throw new InvalidDataException("Windows 备份扩展版本过新。");
            if (manifestJson.RootElement.TryGetProperty("fileHashes", out var hashes))
            {
                var actual = allFiles.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToArray();
                if (hashes.EnumerateObject().Count() != actual.Length) throw new InvalidDataException("备份文件清单不完整。");
                foreach (var name in actual)
                    if (!hashes.TryGetProperty(name, out var hash) || hash.GetString() != Hash(Path.Combine(root, name)))
                        throw new InvalidDataException("备份文件校验失败。");
            }
        }
        string[] incomingSessions = Directory.Exists(Path.Combine(root, "Sessions")) ? Directory.GetDirectories(Path.Combine(root, "Sessions")) : Array.Empty<string>();
        string[] workspaces = Directory.Exists(Path.Combine(root, "Workspaces")) ? Directory.GetFiles(Path.Combine(root, "Workspaces"), "*.json") : Array.Empty<string>();
        if (incomingSessions.Length != manifest.SessionCount || workspaces.Length != manifest.WorkspaceCount) throw new InvalidDataException("备份会议或场景数量不完整。");
        foreach (var folder in incomingSessions)
        {
            if (!Guid.TryParse(Path.GetFileName(folder), out var id)) throw new InvalidDataException("会议 ID 无效。");
            var meta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(Path.Combine(folder, "meta.json")), JsonDefaults.Options)!;
            if (meta.Id != id) throw new InvalidDataException("会议目录与元数据 ID 不一致。");
            foreach (var name in new[] { "transcript.jsonl", "answers.jsonl", "voices.jsonl", "translations.jsonl" })
                if (File.Exists(Path.Combine(folder, name))) foreach (var line in File.ReadLines(Path.Combine(folder, name))) if (!string.IsNullOrWhiteSpace(line)) { using var valid = JsonDocument.Parse(line); }
        }
        foreach (var file in workspaces) { using var workspace = JsonDocument.Parse(File.ReadAllText(file)); if (!Guid.TryParse(workspace.RootElement.GetProperty("id").GetString(), out _)) throw new InvalidDataException("Mac 场景 ID 无效。"); }
        var originals = Path.Combine(root, "Originals");
        if (Directory.Exists(originals)) foreach (var file in Directory.GetFiles(originals)) if (!Regex.IsMatch(Path.GetFileNameWithoutExtension(file), "^[a-fA-F0-9]{64}$") || !Hash(file).Equals(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Mac 原文件哈希无效。");
        if (manifest.Format is not ("nextthought-backup" or "interviewhacker-backup")) throw new InvalidDataException("不支持的备份格式或版本。");
    }

    private static IEnumerable<string> SafeDirectories(string directory) =>
        Directory.Exists(directory) ? Directory.GetDirectories(directory) : Array.Empty<string>();

    private static string ReadSessionTitle(string folder)
    {
        try
        {
            var meta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(Path.Combine(folder, "meta.json")), JsonDefaults.Options);
            return string.IsNullOrWhiteSpace(meta?.Title) ? Path.GetFileName(folder) : meta.Title;
        }
        catch (Exception) { return Path.GetFileName(folder); }
    }

    private static string? FindSessionFolder(string root, Guid id)
    {
        var directory = Path.Combine(root, "Sessions");
        if (!Directory.Exists(directory)) return null;
        foreach (var folder in Directory.EnumerateDirectories(directory))
            if (Guid.TryParse(Path.GetFileName(folder), out var parsed) && parsed == id) return folder;
        return null;
    }

    private static bool SameId(string left, string right) =>
        Guid.TryParse(left, out var leftId) && Guid.TryParse(right, out var rightId)
            ? leftId == rightId
            : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static List<NotesTemplate> ReadTemplates(string root)
    {
        var file = Path.Combine(root, "note-templates.json");
        if (!File.Exists(file)) return [];
        try { return JsonSerializer.Deserialize<List<NotesTemplate>>(File.ReadAllText(file), JsonDefaults.Options) ?? []; }
        catch (JsonException) { return []; }
    }
}
