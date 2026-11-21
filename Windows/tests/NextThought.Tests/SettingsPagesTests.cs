using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E29: the remaining settings groups follow the Mac pages — 洞察设置 on 高级, 自动收尾 on 纪要,
/// 备份与恢复 and 权限 on 通用 (screen recording dropped on Windows). The page is not
/// instantiated here (no Avalonia), so this locks the source and the pure permission policy.
/// </summary>
public sealed class SettingsPagesTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    private static string SettingsXaml() => Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));

    private static string Source(bool codeBehind)
    {
        var name = codeBehind ? "SettingsView.axaml.cs" : "SettingsView.axaml";
        return Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", name));
    }

    /// <summary>The Tag of the settings card that contains <paramref name="anchor"/>.</summary>
    private static string CardTag(string xaml, string anchor)
    {
        var index = xaml.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(index >= 0, $"settings card 未找到：{anchor}");
        var start = xaml.LastIndexOf("<Border", index, StringComparison.Ordinal);
        Assert.True(start >= 0, "card border not found");
        var tagAt = xaml.IndexOf("Tag=\"", start, StringComparison.Ordinal);
        Assert.True(tagAt > 0, "card Tag not found");
        return xaml[(tagAt + 5)..xaml.IndexOf('"', tagAt + 5)];
    }

    private static string Card(string heading)
    {
        var xaml = SettingsXaml();
        var anchor = $"Text=\"{{loc:Tr {heading}}}\"";
        var start = xaml.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, $"settings card 未找到：{heading}");
        var cardStart = xaml.LastIndexOf("<Border", start, StringComparison.Ordinal);
        var end = xaml.IndexOf("</Border>", cardStart, StringComparison.Ordinal);
        return xaml[cardStart..end];
    }

    [Fact]
    public void InsightSettingsMovedToTheAdvancedPage()
    {
        var card = Card("屏幕与讲稿");
        Assert.Contains("Settings.InsightScreenWatch", card);
        Assert.Contains("Settings.InsightFollowSlides", card);
        Assert.Contains("{loc:Tr 立即识别}", card);
        Assert.Equal("Advanced", CardTag(SettingsXaml(), "Settings.InsightScreenWatch"));
    }

    [Fact]
    public void AutoEndMovedToTheNotesPageWithTheMacLabels()
    {
        var card = Card("会议自动收尾");
        Assert.Contains("{loc:Tr 会议没人说话时}", card);
        Assert.Contains("{loc:Tr 多久没人说话算结束}", card);
        Assert.Contains("{loc:Tr 只数识别到的文字，背景音乐不算。}", card);
        Assert.Contains("Settings.AutoEndCanEndByItself", card);
        Assert.Equal("Notes", CardTag(SettingsXaml(), "Settings.AutoEndModes"));
        // The old General-page copy is gone.
        var xaml = SettingsXaml();
        Assert.DoesNotContain("忘了结束的会议会在长时间没有识别到文字后自动收尾", xaml);
        Assert.DoesNotContain("静音多久后收尾", xaml);
    }

    [Fact]
    public void BackupAndPermissionsLiveOnTheGeneralPage()
    {
        var xaml = SettingsXaml();
        Assert.Contains("views:BackupSettingsSection", xaml);
        Assert.Equal("General", CardTag(xaml, "views:BackupSettingsSection"));

        var permissions = Card("权限");
        Assert.Contains("{loc:Tr 麦克风}", permissions);
        Assert.Contains("{loc:Tr 摄像头}", permissions);
        Assert.DoesNotContain("屏幕录制", xaml);
        Assert.Equal("General", CardTag(xaml, "Text=\"{loc:Tr 权限}\""));

        // The backup section is no longer nested in 数据与关于.
        var about = Card("数据与关于");
        Assert.DoesNotContain("views:BackupSettingsSection", about);
    }

    [Fact]
    public void PermissionRowsOpenWindowsSettingsDeepLinks()
    {
        Assert.Equal("ms-settings:privacy-microphone", WindowsPermissions.MicrophoneSettingsUri);
        Assert.Equal("ms-settings:privacy-webcam", WindowsPermissions.CameraSettingsUri);
        var xaml = SettingsXaml();
        Assert.Contains("OpenMicrophonePermissionSettingsCommand", xaml);
        Assert.Contains("OpenCameraPermissionSettingsCommand", xaml);
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Models.cs"));
        Assert.Contains("MicrophonePermissionDetail", state);
        Assert.Contains("CameraPermissionDetail", state);
        Assert.Contains("WindowsPermissions.Microphone", state);
        Assert.Contains("WindowsPermissions.Camera", state);
    }

    [Fact]
    public void PermissionPolicyPrefersDenyThenAllow()
    {
        Assert.Equal(WindowsPermissions.PermissionState.Unknown, WindowsPermissions.Classify([]));
        Assert.Equal(WindowsPermissions.PermissionState.Allowed,
            WindowsPermissions.Classify(["Allow", null]));
        Assert.Equal(WindowsPermissions.PermissionState.Denied,
            WindowsPermissions.Classify(["Allow", "Deny"]));
        Assert.Equal(WindowsPermissions.PermissionState.Allowed,
            WindowsPermissions.Classify(["allow"]));
        Assert.Equal(WindowsPermissions.PermissionState.Unknown,
            WindowsPermissions.Classify([null, "SomethingElse"]));
    }

    [Fact]
    public void ThePermissionLabelsComeFromTheMacStringTable()
    {
        var catalogue = Read(Path.Combine("Windows", "src", "NextThought.Core", "UiTextMac.g.cs"));
        foreach (var key in new[]
                 {
                     "权限", "麦克风", "摄像头", "打开系统设置",
                     "已允许 · 你说的话标记为“我”", "未允许 · 只记录对方的声音", "第一次使用时才会询问",
                     "已允许 · 临场教练用它留意眼神，画面不保存、不上传",
                     "未允许 · 只影响临场教练的眼神提示", "打开“留意眼神”时才会询问",
                     "会议自动收尾", "会议没人说话时", "多久没人说话算结束", "只数识别到的文字，背景音乐不算。"
                 })
            Assert.Contains($"[\"{key}\"]", catalogue);
    }

    [Fact]
    public void TheSettingsPageRefreshesPermissionsWhenOpened()
    {
        Assert.Contains("state.RefreshPermissions()", Source(true));
    }
}
