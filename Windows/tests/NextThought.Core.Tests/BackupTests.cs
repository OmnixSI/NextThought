using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the backup manifest, template merge and settings snapshot.</summary>
public sealed class BackupTests
{
    private static BackupManifest Manifest(string format = BackupManifest.FormatIdentifier, int version = 1,
        int workspaces = 2, int sessions = 3) => new()
    {
        Format = format,
        Version = version,
        Kind = BackupManifest.BackupKind.Full,
        AppVersion = "0.52.0",
        WorkspaceCount = workspaces,
        SessionCount = sessions
    };

    [Fact]
    public void ManifestAcceptsCurrentAndLegacyFormats()
    {
        Manifest().Validate();
        Manifest(format: "interviewhacker-backup").Validate();
        Assert.Throws<BackupManifest.ValidationException>(() => Manifest(format: "other").Validate());
    }

    [Fact]
    public void ManifestRejectsNewerVersionsAndBadCounts()
    {
        var newer = Assert.Throws<BackupManifest.ValidationException>(() => Manifest(version: 2).Validate());
        Assert.Contains("版本 2", newer.Message);
        Assert.Throws<BackupManifest.ValidationException>(() => Manifest(version: 0).Validate());
        Assert.Throws<BackupManifest.ValidationException>(() => Manifest(workspaces: -1).Validate());
        Assert.Throws<BackupManifest.ValidationException>(() => Manifest(sessions: -1).Validate());
    }

    [Fact]
    public void TemplateMergeAddsKeepsAndReplaces()
    {
        var builtIn = NotesTemplate.BuiltIn(NotesTemplateKind.General);
        var custom = new NotesTemplate("custom-1", "我的模板", []);
        var changed = custom with { Name = "改名后" };
        var result = BackupMerge.NotesTemplates([builtIn], [custom, changed], new HashSet<string> { "custom-1" });
        Assert.Contains(result.Templates, template => template.Id == "custom-1" && template.Name == "改名后");
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Replaced);
        Assert.Equal(0, result.Kept);
    }

    [Fact]
    public void TemplateMergeKeepsLocalChangesByDefault()
    {
        var existing = new NotesTemplate("custom-1", "本地", []);
        var incoming = existing with { Name = "备份" };
        var result = BackupMerge.NotesTemplates([existing], [incoming]);
        Assert.Equal("本地", Assert.Single(result.Templates).Name);
        Assert.Equal(1, result.Kept);
        Assert.Equal(0, result.Replaced);
        Assert.Equal(0, result.Added);
    }

    [Fact]
    public void BuiltInTemplatesFromABackupAreIgnored()
    {
        var builtIn = NotesTemplate.BuiltIn(NotesTemplateKind.General);
        var result = BackupMerge.NotesTemplates([], [builtIn]);
        Assert.Empty(result.Templates);
        Assert.Equal(0, result.Added);
    }

    [Fact]
    public void SessionsToImportSkipsExistingIds()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Assert.Equal([second], BackupMerge.SessionsToImport(new HashSet<Guid> { first }, [first, second]));
    }

    [Fact]
    public void SecretAccountsIncludeProvidersAndPresets()
    {
        Assert.True(SettingsSnapshot.IsSecretAccount("answer.api"));
        Assert.True(SettingsSnapshot.IsSecretAccount("answer.api.preset-1"));
        Assert.True(SettingsSnapshot.IsSecretAccount("github.token"));
        Assert.True(SettingsSnapshot.IsSecretAccount("stt.deepgram"));
        Assert.False(SettingsSnapshot.IsSecretAccount("apiModel"));
        Assert.False(SettingsSnapshot.IsSecretAccount("answer.apix"));
    }

    [Fact]
    public void FilterKeepsOnlyExportedKeys()
    {
        var filtered = SettingsSnapshot.Filter(new Dictionary<string, object?>
        {
            ["apiModel"] = "gpt",
            ["notExported"] = 42,
            ["appearance"] = "dark"
        });
        Assert.Equal(["apiModel", "appearance"], filtered.Keys.OrderBy(key => key));
    }

    [Fact]
    public void ValuesForRestoreMapsTheLegacyKnowledgeFlag()
    {
        var legacy = SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoApplyLessons"] = true,
            ["appearance"] = "dark"
        });
        Assert.Equal(true, legacy["autoCollectKnowledgeCandidates"]);
        Assert.False(legacy.ContainsKey("autoApplyLessons"));

        var newer = SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoCollectKnowledgeCandidates"] = false,
            ["autoApplyLessons"] = true
        });
        Assert.Equal(false, newer["autoCollectKnowledgeCandidates"]);
        Assert.False(newer.ContainsKey("autoApplyLessons"));
    }
}
