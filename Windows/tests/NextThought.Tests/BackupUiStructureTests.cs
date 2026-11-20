using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E30: the backup UI is wired the way SettingsGeneralData.swift / BackupRestoreReview.swift do
/// it — the settings page hosts the section, the review page is a separate confirm step, and a
/// meeting end starts the after-session recovery point. The behavioural half lives in
/// <see cref="BackupRecoveryTests"/>.
/// </summary>
public sealed class BackupUiStructureTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", Path.Combine(parts)));

    [Fact]
    public void SettingsPageHostsTheBackupSection()
    {
        var settings = Source("Views", "SettingsView.axaml");
        Assert.Contains("<views:BackupSettingsSection />", settings);
        Assert.Contains("xmlns:views=\"using:NextThought.Desktop.Views\"", settings);
        var section = Source("Views", "BackupSettingsSection.axaml");
        foreach (var text in new[] { "备份与恢复", "导出全部数据…", "导入…", "立即备份", "会议结束后自动创建恢复点", "恢复…", "删除", "还没有自动备份。" })
            Assert.Contains(text, section);
        // G8: interface text goes through the explicit translation markup.
        Assert.Contains("{loc:Tr 备份与恢复}", section);
    }

    [Fact]
    public void ImportIsAReviewStepBeforeApplying()
    {
        var section = Source("Views", "BackupSettingsSection.axaml.cs");
        Assert.Contains("PrepareBackupImportAsync", section);
        Assert.Contains("BackupRestoreReviewView", section);
        var review = Source("Views", "BackupRestoreReviewView.axaml");
        foreach (var text in new[] { "导入与恢复预览", "新增记录", "相同记录", "冲突记录", "备份模板", "使用备份", "同时导入偏好设置", "取消", "应用所选内容" })
            Assert.Contains(text, review);
        var code = Source("Views", "BackupRestoreReviewView.axaml.cs");
        Assert.Contains("ApplyBackupImportAsync", code);
    }

    [Fact]
    public void MeetingEndStartsTheAutomaticRecoveryPoint()
    {
        var live = Source("AppState.Live.cs");
        // DataBackup.afterSession is asynchronous: stopping a meeting must not wait for the backup.
        Assert.Contains("_ = RunAfterSessionBackupAsync(meta.Id)", live);
        Assert.Contains("BackupBusy", live);
        var archive = Source("AppState.Archive.cs");
        Assert.Contains("CreateRecoveryPointAsync(\"session\")", archive);
        Assert.Contains("CreateRecoveryPointAsync(\"manual\")", archive);
        Assert.Contains("\"autoBackup\"", archive);
        Assert.Contains("DeleteRecoveryPoint", archive);
        Assert.Contains("PrepareImportAsync", archive);
        Assert.Contains("ApplyPreparedImportAsync", archive);
    }

    [Fact]
    public void BackupListCarriesDateReasonAndSize()
    {
        var service = new BackupService(new AppPaths(Path.Combine(Path.GetTempPath(), "nt-e30-structure-" + Guid.NewGuid().ToString("N"))));
        try
        {
            Assert.Empty(service.ListBackups());
        }
        finally { try { Directory.Delete(service.BackupsRoot, true); } catch { } }
        var file = new BackupFile("C:\\x\\00000000-0000-0000-0000-000000000000-manual.ntrestore",
            DateTimeOffset.UnixEpoch.AddHours(1), 2048, "manual");
        Assert.True(file.IsRecoveryPoint);
        Assert.Equal("手动", file.ReasonTitle);
        Assert.Equal("manual", Path.GetFileNameWithoutExtension(file.Name).Split('-')[^1]);
    }
}
